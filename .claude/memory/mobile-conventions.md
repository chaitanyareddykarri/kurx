# Mobile Conventions

Single source of truth for the Flutter app in `mobile/`. Stack + scope decision: `docs/DECISIONS.md` D-019 (ACCEPTED). Consume the backend contract in `docs/api/README.md`, **verified against `backend/Kurx.Api/Endpoints/*`** — never invent it. Package versions live in `mobile/pubspec.yaml` (pinned at scaffold time) and are the source of truth for numbers; this file names the choices, not the numbers.

## No mock data layer (D-022)

The mobile app has **no mock/fake data seam**. `eventsRepositoryProvider`/`authRepositoryProvider` construct only the real API-backed repository — no `MockConfig`/`Fake*Repository` branch. Where a feature has no backend endpoint yet, the provider/controller **throws `UnimplementedError`**, and the calling UI catches it and shows a "not available yet" message — it never fabricates data to fake the feature working. Username claim/availability (`ProfileController.isUsernameAvailable`/`claimUsername`, `AuthController.checkUsernameAvailable`/`completeOnboarding`) are wired to the real backend (D-037) — no longer stubbed. Genuinely local, non-backend-dependent state (bookmarks — saved-event selections, never intended to hit a server) is not a mock and stays as-is. Before adding a new screen against an endpoint that doesn't exist yet, don't build a fake one — stub the action and record the gap.

## Stack (D-019)

| Concern | Choice |
|---|---|
| SDK | Flutter stable channel (sudo-free, D-001), Dart stable (`>=3.4 <4.0`) |
| State + DI | Riverpod — providers are the DI mechanism, no separate service locator |
| Navigation | GoRouter — declarative, auth-driven redirect guards |
| HTTP | Dio — one client, interceptors for auth + `401`→refresh + RFC7807 parsing |
| Models/codegen | Freezed + json_serializable — immutable DTOs/entities |
| Token storage | flutter_secure_storage only (Keychain/Keystore) |
| Local cache | Hive — non-secret data only, never tokens/PII |
| UI | Material 3, light + dark, localization-ready, `en_IN`-first |

## Architecture — feature-first Clean Architecture

Layered per feature; dependencies point inward (`presentation → domain ← data`), `domain` is framework-free (no Flutter/Dio imports). UI never touches Dio or a DTO directly — it goes **widget → provider → usecase → repository (domain interface) → data repository → datasource → API client**, and DTOs are mapped to `domain/entities` before crossing into `presentation`.

```
mobile/lib/
  core/
    network/     # Dio client, interceptors, ApiError (RFC7807), endpoints
    theme/       # Material 3 light+dark, tokens
    router/      # GoRouter config + redirect guards
    storage/     # secure storage (tokens), Hive boxes (non-secret cache)
    utils/       # formatters (money), result types, extensions
  common/        # cross-feature widgets (loading/error/empty/retry states)
  shared/        # cross-feature domain/value types shared by >1 feature
  features/
    auth/
      presentation/ { widgets/  pages/  providers/ }
      domain/       { entities/  repositories/  usecases/ }
      data/         { datasources/  models/  repositories/  services/ }
    events/
      presentation/ { widgets/  pages/  providers/ }
      domain/       { entities/  repositories/  usecases/ }
      data/         { datasources/  models/  repositories/  services/ }
  app.dart         # root widget: ProviderScope consumers, router, theme
  main.dart        # entrypoint, ProviderScope + DI overrides, bootstrap
```

`domain/repositories` holds abstract interfaces; `data/repositories` implements them; providers wire the implementation. Anything shared by >1 feature moves to `core/`/`common/`/`shared/`, never imported feature-to-feature.

## Dependency injection

Riverpod providers only. Cross-cutting singletons (Dio client, secure storage, Hive, config) are `Provider`s overridden in `main.dart`'s `ProviderScope`; tests override the same providers with fakes. No `GetIt`/service locator.

A feature's datasource provider stays private (`_certsSourceProvider`) when only that file's `FutureProvider`s read it. It is public (`ordersSourceProvider`) once a page calls a **mutation** through it directly — the page needs it, and the widget test overrides it with a fake datasource. Read-only screens should not make theirs public.

## State management

`flutter_riverpod` (+ `riverpod_annotation`/codegen optional). Async screen state is `AsyncNotifier`/`AsyncValue` so every data screen renders the same loading/data/error/empty/retry pattern. `setState` only for ephemeral local widget state.

## Navigation

GoRouter, declarative routes. A redirect guard reads auth state (valid token present): unauthenticated → OTP flow; the public event-browse surface is reachable without a token (matches the unauthenticated `/v1/events/*` backend). Deep-link ready.

**Declaration order is matching order.** GoRouter takes the *first* route that matches, not the most specific one, so a literal path declared after a wildcard at the same depth is dead code — the opposite of Next.js on web, where a static segment always beats `[param]`. Every literal `/x/<word>` goes **above** `/x/:param`. This shipped broken once: `/events/create` sat 370 lines below `/events/:slug`, so Create Event opened the event *detail* page for a slug named "create", fetched `GET /v1/events/create`, took a 404 and showed "We couldn't find that" — the gate was unreachable on mobile while web was fine. `test/core/guest_browse_test.dart` asserts the ordering for every `/events/` route.

## API client

One Dio client in `core/network` with:
- **Base URL** from build config (`KURX_API_BASE` via `--dart-define`, default `http://localhost:5080`) — **no stale port fallback** (the D-017 bug class), and **no platform rewriting** (D-318).
  - **A device's `localhost` is the device.** Every request fails as *"Request timed out"* with the backend perfectly healthy on the host. Fix is one command, and it covers a physical phone and an emulator identically: **`adb reverse tcp:5080 tcp:5080`** (add `tcp:3000` if the app opens in-app web links). Confirm with `adb reverse --list`; it does not survive a replug or `adb kill-server`.
  - Do **not** "fix" this in Dart by rewriting the host to `10.0.2.2` — that is emulator-only and *breaks* a physical device, which is the majority case here. It was written and reverted (D-318).
  - Diagnose from the device, never from the host: `adb shell curl -s -o /dev/null -w '%{http_code}' http://localhost:5080/health`. A host-side `curl` proves nothing about what the app can reach.
- **Timeouts**: connect + receive bounded (e.g. 10s/20s), pinned in one place.
- **Retry**: a bounded retry policy for transient network/5xx only — never retries a 4xx business error.
- **Auth interceptor**: attaches `Authorization: Bearer <access>` from secure storage.
- **Refresh interceptor**: on `401`, calls `/v1/auth/refresh` once, retries the original request, and on refresh failure clears the session (reuse revokes all sessions — D-009/D-014). Concurrent 401s share one in-flight refresh.
- **Casing**: sends **camelCase** request bodies, parses **snake_case** responses (D-019). Never reshape the contract client-side.

## RFC7807 ProblemDetails handling

**A DTO is a contract claim — verify it against a real response before trusting it.** Freezed DTOs here were written against an imagined API and shipped broken (D-064): a `required` field the server never sends throws on parse, and a `@Default` on a field no endpoint returns renders as fact. `guard()` converts a parse failure into `ApiError(code: 'response_parse_failed')` rather than letting a raw `TypeError` escape and be repainted as a network outage. Pin new/changed DTOs with a parse test built from a **captured live payload** (`test/features/organizer/org_dto_test.dart`). The points/badges DTOs went further than wrong casing (D-212): they described a level/tier system and a nested badge list that **never existed on the backend at all** — a fabricated contract, not just a miscased one — undetected for as long as it was because nothing exercised the live response.

**~~Recurring camelCase exception~~ — fixed platform-wide (D-259 addendum).** `Results.Ok(record)` used to pick up Minimal API's default camelCase serializer, the *opposite* of this API's snake_case convention, and it bit repeatedly: `GET /v1/orgs/{id}/wallet` (D-064), the attendees endpoint (`AttendeeRow`, D-208), and the whole `GamificationEndpoints.cs` family (D-210/D-212). `SnakeCaseResponseConverter` now applies the convention to those records too, so **assume snake_case** and stop grepping handlers for `Results.Ok(<record>)`.

Two edges survive, and they are the reason to still verify rather than assume: a response record declared **outside** `Kurx.Application.Abstractions` escapes the converter and still emits camelCase, and **stored** payloads are not responses at all — push `DataJson` keeps whatever its producer wrote (`ChatNotificationJob` emits `notificationType`). See `.claude/memory/api-conventions.md` for the rule itself; don't restate it here.

Where a mapper reads both the wire **and** an on-device cache — `chat_mappers.dart` does, because the offline outbox is persisted through the same functions — read tolerantly (snake first, camel second). A hard switch there would have made every queued message and pending attachment already on a user's phone unreadable, which is silent data loss in the one place the app promises none.

Every non-2xx `application/problem+json` body is parsed into one `ApiError { error, status, correlationId, detail, errors? }` (mirrors `ProblemResults.cs`/`GlobalExceptionHandler.cs`/`Program.cs` exactly — no second error shape). UI branches on the stable `error` code (`rate_limited`, `invalid_code`, `not_found`, `validation_failed`, …), reads the `errors` map on `validation_failed`, and surfaces `correlationId` in bug reports. Errors are **handled and shown, never swallowed**.

**Copy for a code belongs in `ApiError._messages`, never in a `switch (e.code)` at the call site (D-315).** A per-screen switch has nothing to compare itself against, which is how the registration screen ended up matching `weak_password` — a code the backend has never emitted — while every code it *does* emit (`password_breached`, `password_contains_identifier`, `password_too_short`) fell to a default reading "Could not set your password. Try again." The user reported that sentence as the bug. Show `e.userMessage`; if the wording is missing, add it to the table, where `web/test/error-copy.test.ts` compares it against `PROBLEM_COPY` and keeps the two platforms saying one thing.

**A wizard step reached by a hard-coded assignment is a step nobody re-derived from server state.** `_savePassword` set `_step = _Step.success` directly instead of re-reading `/registration/status`, so the email-verification step was built, listed in the checklist, and unreachable in a single sitting (D-315). Three copies of that ladder existed in one file and two ended at `success`. Advance by re-reading status and routing through the single `_firstStep`, exactly as web's `refreshAndAdvance` does — same failure class as D-311's two drifted implementations of "is this account onboarded?".

## UI states

Loading / error / empty / retry is a **standard shared pattern** in `common/` consumed by every data-driven screen — not reinvented per screen. Error state renders the mapped `ApiError` message + a retry action.

## Authentication

Phone OTP, delivered over **SMS** (D-281 — never WhatsApp): `POST /v1/auth/otp/request` → `POST /v1/auth/otp/verify` → store tokens → `GET /v1/me`. `otp/verify` is a **combined sign-in-or-register** (D-011/D-037): an unknown number gets an account, a known one gets a session, and `is_new_user` distinguishes them. There is deliberately no "is this phone registered?" check to branch the UI on before the code is verified — that would be an account-enumeration oracle (D-311) — so the screens *state* the branch ("we'll sign you in, or set up a new account") rather than predicting it.

`needs_onboarding: true` when the name is blank **or** Username is null **or** — for accounts created at or after the `AddUserDateOfBirth` migration timestamp — `date_of_birth` is null **or** the account has no password (D-311; supersedes D-037's Name+Username rule, which superseded D-012's Name-only check). The last two are **not retroactive**: an established account is prompted through `remaining`, never blocked. It routes to the registration ceremony, whose steps run in the server's `remaining` order: **complete profile** (name + username with a live availability check against `GET /v1/usernames/availability`, date of birth, optional bio) → **set password** → **verify email** (skippable) → success. Only the first two gate `needs_onboarding`, so only the third has a "Skip for now".

Two rules the flow must keep: never let an unknown status default *past* a blocking step (`_status?.hasPassword ?? true` once meant "assume they have one" and walked new users past the password screen entirely), and never enforce a client-side password policy — read `min_length` from `GET /v1/auth/password/status` instead, or an 8-character password passes the form and is refused by the backend's 12. The profile save completes locally only after the server confirms via `PATCH /v1/me/profile`. `SessionController.bootstrap()` re-checks `/v1/me` on every cold start (not just fresh logins) so an existing session with a still-pending username is gated too. Logout calls `/v1/auth/logout` then clears secure storage. Access 1h, refresh 30d rotated (D-009).

## Secure token storage

`flutter_secure_storage` only (iOS Keychain / Android Keystore). **Never** `SharedPreferences`, Hive, plaintext files, or a query string (except the SignalR `?access_token=` convention, only if/when a real-time feature is in scope). Tokens are never written to logs, analytics, or crash reports.

## No client secrets

The app ships **no** signing keys, provider API keys, or shared secrets — JWTs are minted server-side and held transiently in secure storage only. No `.env` with real secrets committed; nothing in `mobile/` would fail a secret scan.

## Money formatting

Money is `long` minor units on the wire (D-004) and always travels with the currency it is denominated in
(V3 §9.1, D-257). Format with `intl` via the one shared helper in `core/utils/money.dart`, **only at the
display edge** — never store or compute in floating-point.

`Money.fromMinor(minor, currency: …)`, not a hardcoded ₹. The symbol is a property of the amount: an event
settles in its own currency, so assuming INR renders a materially wrong number to whoever is being paid.
`currency` defaults to `'INR'` so a payload that does not carry one yet is unchanged, and an unmapped code
falls back to the code itself (`AUD 1,200`) — a plainly-labelled amount is recoverable, a confidently wrong
symbol is not. The old `Money.fromPaise` was renamed rather than aliased: its name asserted INR in the
signature, which was the bug.

## Localization

`flutter_localizations` + `intl` ARB files; India-first (`en_IN` default, `hi` scaffolded). All user-facing strings go through the localization layer; dates/numbers/currency use locale-aware `intl` formatters (IST-aware event times).

## Theme

One Material 3 `ThemeData` with light + dark `ColorScheme`s driven by system brightness. Colors/spacing/typography are centralized tokens in `core/theme` — no hard-coded colors in widgets.

## Testing strategy

`flutter analyze` (zero issues) + `flutter test` are the **floor**: unit tests (DTO mapping, ProblemDetails parsing, usecase/notifier logic) and widget tests, with Dio/secure storage/Hive faked via Riverpod overrides — no real network in unit tests. Live-device/emulator verification against the **real running backend** is the substitute for an automated integration suite (mirrors the web "browser verification is the substitute" rule, `testing-standards.md`). Report test count before → after. Never weaken an assertion to force green.

## Build verification

`flutter analyze` clean + `flutter test` green is the minimum gate; then run on an emulator/device against the live API before calling a change done (see `.claude/checklists/mobile.md`).

## Performance guidelines

`const` constructors wherever possible; `ListView.builder` for lists; images cached + sized (banner/media are storage keys with no public URL endpoint yet — placeholder until the backend exposes one). No blocking work on the UI isolate; debounce search input against `/v1/events`; Hive-cache list responses for first-paint.

## Code quality bar

No file over ~300 lines without a stated reason. No `TODO`/placeholder/stub implementations left in shipped code. Composition over inheritance. No duplicated logic — extract to `core/`/`common/`/`shared/`. One widget/notifier/usecase/repository per file. Match the surrounding file's idiom; no speculative abstraction *within* a layer (CLAUDE.md #5 — three similar widgets beat a premature base class).

## Naming conventions

Files `snake_case.dart`; types `UpperCamelCase`; members/vars `lowerCamelCase`; constants `lowerCamelCase`. DTOs end `Dto` and live in `data/models`; `domain/entities` drop the suffix. Providers end `Provider`; usecases are verbs (`VerifyOtp`, `GetUpcomingEvents`).

## Known bug class to watch for

Mirror of the web D-017 bug: read the API base URL from the correct build-config key (`KURX_API_BASE`, default `http://localhost:5080`) with **no stale port fallback**. Confirm the key matches `.env.example`/`--dart-define` before shipping.

**Its sibling, and the more common one in practice: a device-side "Request timed out" is almost never the app.** `localhost` on the phone is the phone. Run `adb reverse tcp:5080 tcp:5080` and re-test from the device itself — see §API client above (D-318). Reaching for a code change here produces a *plausible* platform branch that breaks the majority device.


## Main Application navigation is locked (D-262/D-265 client work, 2026-08-03)

- **Profile top-left. Notifications top-right.** Fixed corners on every screen, not nav entries.
- **Home · Community · Posts · Messages · Workspace** — in that order, identical on web and Flutter.
- Browse/Search, Tickets and Saved are **not** tabs; they live under Home and Profile.

The product flow names seven Main Application areas and a pill nav holds five. Profile and
Notifications are the two you visit and come back from rather than dwell in, so they get corners
reachable from everywhere instead of consuming a slot. **Do not reintroduce either as a tab.**

**Workspace is role-scoped, not a screen.** It lists every event you have a role in: registering
unlocks that event's participant workspace; a hosted event opens its host workspace **only after
admin approval** (before that the row shows its status). One person is routinely both, for different
events — per-event role, never an app-wide mode.

**Workspace lists *your events*, never organizations (D-267).** Kurx is user-first: users own events,
and an organization is optional metadata an event *represents*. `myEventsProvider` reads
`GET /v1/me/events` once — never "list my orgs, then list each org's events", which is precisely what
made this screen render as an organization browser. Hosted / Drafts / Pending approval / Archived are
**status filters over one list**, never separate containers. Create Event is `/events/create`, reachable
straight from Workspace, and **Representing is the wizard's first step**, never a gate in front of it.

*"Personal by default" was retired by D-379*: every event represents a real organization Kurx has
verified, whatever its product. Step one asks for that organization **and** its authorization letter for
this event (D-382) — one question, one screen, because the letter names the organization it authorises.
Two Flutter-specific traps that cost a working screen each, both fixed under D-382 and both worth not
reintroducing: a `product == 'Private'` branch that rendered no picker while `_stepErrors` still demanded
a valid representation (a step with nothing to answer and a Continue that could never enable — the app
could not create a private event at all), and an `Authorization` step appended after `Legal`, which
taught an organiser on step twelve that step one was incomplete.

Do not reintroduce: `/orgs`, `/org/create`, `/org/:orgId`, `/org/:orgId/events`, or any page that makes
an organization the container you open to find events. Management routes are `/events/:eventId/manage/…`
— the screens still take `orgId` because the management sub-resources are org-scoped server-side, but
exactly one place resolves it (`EventManageScope`, from `GET /v1/events/{eventId}`) and nobody picks it.
The surfaces that genuinely need an organization live under `/representing/:orgId/…`, reached from
Profile → Representing.

**Representation is answered once, inside Create Event (D-389).** The wizard's Representing step collects
all of it — the organisation, *its registration when it is not on Kurx yet* (rendered in place, submitting
`POST /v1/orgs/representation-requests`), and this event's own authorisation letter. It never pushes: a
navigation out of a wizard holding ten steps of unsaved answers is how an organiser loses the event they
are creating, and since D-379 that hit everyone who represented nothing. `/events/:eventId/manage/representing`
is **deleted**; the correction surface is `/events/:eventId/edit/representing`, reached from Event Status
only on `changesrequested`/`rejected`. `/representing/new` survives for the Profile path and shares the
same field vocabulary (`kOrgTypes`).

**The authorization's account link exists on Flutter now.** `RepresentativePicker`
(`presentation/widgets/representative_picker.dart`) sends `representativeUserId` from both the wizard's
Representing step and `/events/:eventId/edit/representing`. It is **a link, never a grant** — authority
is `IEventAuthority`'s (D-269) — and optional, so a failed lookup never blocks filing the letter. Flutter
had no user search of any kind before this (`searchUsers` on `EventContentRemoteDataSource` is the first),
which is why web had the field and Flutter had it on neither screen. Debounced at 300 ms and floored at
two characters: a request per keystroke rate-limits the organiser out of their own form.

**A pending organisation is selectable.** `_representingValid` accepts any representation the caller holds
(the server grants `Manager` off the pending `Representative` seat); `_representingPaidCapable` is the
separate, stricter check the money path uses. Publication is still refused by `pending_org_verification`.

**Launcher tiles with no endpoint behind them are absent, not dead.** A tile that 404s is a promise
the API cannot keep, and web's participant workspace is legitimately thinner than Flutter's because
web has no competition or leaderboard page yet.

## Wizard payload shaping

A null field means "leave alone" server-side; an empty string means "clear". So a wizard step the
user never opened must send **nothing**. Both clients strip blanks before sending (`cleanGroup` in
`web/lib/event-wizard.ts`, `compactGroup` in `mobile/.../event_wizard_payload.dart`) — kept out of
the components so the rule is testable, and tested case-for-case on both so the same wizard cannot
clear different fields depending on the device.

`false` and `0` are kept: an unchecked switch and a zero fee are real answers. On web,
`datetime-local` is converted through `toIsoUtc` — sent raw the server reads wall-clock as UTC and
every time shifts by the user's offset, which in India is 5h30m nobody notices until check-in.

## Phone inputs use the shared country picker, always (D-290)

`common/widgets/phone_field.dart` — never a bare `TextField` with `TextInputType.phone`, and never a
hardcoded `prefixText: '+91 '`. The widget emits `(e164, valid)`; hold the E.164 in state and send *that*,
not `controller.text`. A bare national number is read by the backend in the legacy `IN` region, which on the
phone-change screen meant texting the confirmation code to a stranger and moving the account onto their
number. For displaying a phone, the API already returns canonical E.164 — print it as-is; do not prepend a
country code. Mixed identifier fields use `common/util/phone_utils.dart` `toE164Identifier`.

## Capabilities are asked, never inferred from a name (D-372/D-366)

Whether an event may have **teams** is the capability engine's answer — `GET /v1/archetypes/{slug}/capabilities`,
via `archetypeSupportsTeamsProvider` — not a list of type names in the app. The matrix is data an admin
can edit, so a second copy here silently disagrees with the server the day someone changes it. The
provider **fails closed**: an unreadable answer, or a Type with no `archetype_slug`, offers individual
entry only.

This needs `archetype_slug` on the taxonomy DTO. It was missing for a long time, and its absence is why
the create-event wizard hardcoded `'pricingUnit': 'PerTicket', 'registrationMode': 'Individual'` — an
organiser on a phone could not create a team registration in any form, on an API that had supported one
since D-020. **When a client "only supports one mode", check whether it is missing the field that would
let it ask.**
