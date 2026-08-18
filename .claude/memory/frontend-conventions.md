# Frontend Conventions

## Stack

Next.js in `web/` (public site + organizer dashboard) and `admin/` (internal staff console, port
3001 — a real, built-out app with 20+ modules; see `admin/STATUS.md` for current build status,
not this file). Both consume `@kurx/ui` (`packages/ui/`) — the single source of truth for design
tokens (`packages/ui/src/styles/tokens.css`) and shared UI primitives. Do not duplicate a
primitive locally in either app if an equivalent could live in `@kurx/ui` — promote it instead
(precedent: D-185).

## Patterns

- Server Components + Server Actions are the default (`web/lib/event-actions.ts` is the reference implementation for mutating actions).
- Session handling: httpOnly cookies only, managed in `web/lib/session.ts` (`admin/lib/session.ts` mirrors it). Route groups like `(app)`/`(console)` enforce session auth server-side — verify that enforcement is real, not dead code (it was found dead once and silently doing nothing).
- Never expose auth tokens to client-side JS, ever, even transiently.

## `@kurx/ui` design system (D-185)

- List/table screens: `DataTable` (sort, skeleton loading, empty state, row selection) — not a
  hand-rolled `Card` stack or bare `<table>`. Status conveyed via `Badge`, never a raw colored
  `<span>`.
- Forms: `Field` wrapping `Input`/`Textarea`/`Select` — not a hand-rolled `<label>` + raw
  `<input className="...">` pair. Confirmation-gated destructive actions use `ConfirmDialog`.
- Feedback: wrap the app shell in `ToastProvider` (`useToast()`) for row-action outcomes, in
  addition to (not instead of) inline validation errors on a form.
- Charts: `Sparkline` (line/area) and `MiniBars` (bar) are dependency-free inline SVG — no chart
  library is installed anywhere in the monorepo, and none should be added for a KPI trend line or
  day-count bar strip; that's a few-line SVG job, not a dependency's job.
- Admin's page-header block (`kicker` / `h1` / description) is `admin/components/layout/page-header.tsx` — reuse it rather than re-inlining the same three lines per page.
- Crowded action bars: `Menu` / `MenuItem` (`packages/ui/src/menu.tsx`) — roving focus, Home/End,
  Escape-restores-focus and a `destructive` tone are already in it. A menu item is a plain `onSelect`,
  so a form-backed action dispatches the same `FormData` its `<form>` would have posted: identical
  server action, identical audit trail, no second code path to drift (D-381).
- Tab strips that outgrow their container: pass `trailing` to `Tabs` and put the low-frequency tabs in a
  `Menu` — do NOT let `overflow-x-auto` scroll silently, which reads as a clipped label rather than as
  more content. `trailing` renders as a SIBLING of the `role="tablist"`, never inside it, or assistive
  tech miscounts ("tab, 6 of 10"). Keep the selected overflow tab in the visible strip so the nav still
  shows where it is (D-381).

## Error copy comes from the shared table (D-315)

`error` on every ProblemDetails is a machine code. The sentence for it lives in **`PROBLEM_COPY`
(`packages/ui/src/problem-copy.ts`)**, reached via `apiErrorMessage` → `problemMessage`. Do not write a
second mapping beside a form.

Registration showed "Something went wrong. Please try again." for every password refusal because the
policy codes were only in `web/lib/password.ts`, which the signup step never called — while
`looksLikeCode()` suppressed the `detail` carrying the same token. Two tables for one vocabulary, and the
screen that needed it used neither. A helper that maps codes is fine where the wording is genuinely
positional ("your current password is incorrect" means nothing outside a change form); everything else
delegates to `problemMessage`.

`web/test/error-copy.test.ts` reads `PasswordService.cs` and fails if a code the backend can return has
no copy, and compares `PROBLEM_COPY` against Flutter's `ApiError._messages` so the two platforms cannot
drift. Match on values that are *returned* — a bare `password_*` grep also picks up audit action names.

**A STATUS is words too, and the same table rule applies.** The API serialises `EventStatus` as the
lowercased enum name, so `pendingreview` reaches the UI unless something maps it: web's
`web/lib/event-status.ts`, mobile's `core/utils/event_status.dart` (Dart cannot import the TS module).
Mobile had no such map for a long time and printed the raw enum in three places — including the literal
`Opens after approval · pendingreview` that web's file was created to fix. The same test now pins both
maps against the backend `EventStatus` enum, so a status added there fails a test rather than reaching a
user as `CHANGESREQUESTED`.

**A code no backend file emits is dead copy, and it hides a live gap.** Mobile mapped `approval_required`
and `no_ticket_types`, neither of which exists in the backend, while `approval_pending` and `no_pass` —
the codes actually returned — fell through to the generic sentence. Before adding copy, grep the code
string in `backend/`; if it has zero hits you are writing for a refusal that never happens.

## A signed-in surface carries a way out (D-316)

`/login` redirects an authenticated caller on `needs_onboarding`, so an account with unfinished setup
lands on `/register`. That page had no sign-out and no account switcher, which meant pressing "Log in"
returned the user there **indefinitely** — a dead end reachable from the primary nav.

Two rules follow. **A page reached by resuming says so**: `/register` branches on `initialStatus` and
shows "Finish setting up your account" plus the signed-in phone, because "Create your Kurx account"
above a ticked "Phone verified" reads as the login button creating a second account. **Anything
reachable while signed in offers sign-out or a switch** — check this whenever adding a redirect that
depends on session state, not only on this page.

## One surface collects, the others report (D-382)

A form that writes a given record exists in **exactly one place per client**. Any other surface that
cares about that record renders its *state* and links to the one that owns it — a status line, a badge
and a button, never a second copy of the inputs.

The rule came from the event authorization letter, which was collected on the Create Event wizard's
Representing step *and* on the event's Readiness page. `event_authorizations` is UNIQUE on `EventId`, so
the two forms wrote one row: whichever was submitted last silently replaced the other, including
replacing a reviewed filing with a half-typed one. A duplicated form is not a duplicated screen — it is
two answers to one question, and the database keeps only the last one.

Applying it:

- **The collector is a route, not a modal**, so a blocker message can name it (`"File it on the event's
  Representing tab"`) and a status card can link to it.
- **Status surfaces derive, never infer.** The Readiness card maps the server's authorization status;
  it does not re-derive "does this event need one" from the organization's shape, which would be the
  client re-implementing a policy rule and disagreeing with it on the first edge case.
- **Pin it structurally.** `web/test/representation-home.test.tsx` walks `app/` and `components/` and
  asserts `<AuthorizationForm` appears in exactly one file, and that the reporting surface renders no
  `input`, no `form` and no `button`. A grep-based test is what stops the second copy coming back in a
  refactor — an import with no JSX would be the next version of the same bug.
- **Cross-client, the same shape.** Flutter mirrors it: the wizard's Representing step collects, and
  `/events/:eventId/edit/representing` is the correction surface.
- **D-389 — the step never navigates.** The organization-registration form renders *inside* the
  Representing step (`<CreateOrgForm onRegistered={…}>`), because a link out of a wizard holding ten steps
  of unsaved answers is how someone loses the event they are creating. The workspace Representing tab is
  gone; the correction path is Edit Event, gated on `Rejected`/`ChangesRequested`.
- **Two representation sets, never one.** `selectableReps` (everything the caller represents — a
  `PendingReview` organization can carry a *draft*) and `paidCapableReps` (verified only — money needs a
  verified institution). Collapsing them is what blocked the free path on a bar only the paid path has.

## Known bug class to watch for

`web/lib/site.ts` previously read the wrong API-base-URL env var name with a stale port fallback, silently pointing at the wrong backend. When touching env-var-driven config, confirm the var name matches what's actually set in `.env.example`/deployment config, and that there's no stale fallback value.

## Verification

Type check + lint + build are necessary but not sufficient — load the actual page in a browser against a real session before calling a UI change done (see `.claude/checklists/frontend.md`).


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
and an organization is optional metadata an event *represents*. Workspace reads `GET /v1/me/events`
once — never "list my orgs, then list each org's events", which is precisely what made both surfaces
render as an organization browser. Hosted / Drafts / Pending approval / Archived are **status filters
over one list**, never separate containers.

**Create Event is entered from Profile, behind a gate (D-305).** It was a direct action on Workspace
until 2026-08-08. The gate runs *before* the creation form: it reuses the caller's existing verification
state (never re-asking a passed check), then asks **Public or Private** — a capability decision that maps
to `EventProduct`, not a form field — and only then opens the form. **Representing (Personal by default)
remains a step inside the form**, never a gate in front of it.

**A wizard step validates its own fields; the last step is a backstop, not the gate (D-327).** The
create-event `canNext` ended in `step >= 4`, so the three fields the server requires all sat behind an
unconditional pass and the wizard first refused on step 11 of 11 — D-305's own failure, reproduced inside
the form D-305 created. Two rules follow, and a reviewer should reject a step that breaks either: **gate
on the step that asks**, and **name the missing field**, never "Make a choice to continue." — a shrug on a
four-input step is what makes a disabled button useless. Where the server already has the rule
(`EndsAt > StartsAt`, the two window pairs), the client surfaces it rather than inventing a second one;
where one client already has it (Flutter's `_basicsValid` carried the date ordering before web did),
copy that rule rather than writing a third.

**One per-step validation table, never a per-step boolean clause (D-370).** D-327 fixed the *symptom* —
`step >= 4` — by writing a clause per step, and the shape it left behind reproduced the bug three more
times: Content, Location and Eligibility ended up as a bare `step === 6 || step === 7 || step === 9`
(an unconditional pass), Details checked 3 of the 9 fields it renders, and Windows checked only pair
ordering. A disjunction of independently-written clauses cannot tell "this step has no rules" from
"nobody wrote this step's rule". The table can: **every step maps to a validator returning
`field → message`, and a step with no required field returns `{}` as a statement.** Continue is
`Object.keys(currentErrors).length === 0`, derived on every render — never memoised (that freezes the
clock on a past-date rule) and never stored (that is how a step stays valid after its data stops being).
Four consumers read the one result: the button, the spoken reason, the click guard, and the submit
backstop. **Disable *and* guard**: `disabled` is presentation, so the Continue handler re-validates
before advancing. The rules themselves live in `web/lib/event-wizard.ts` and
`mobile/.../domain/event_wizard_payload.dart` — pure, DOM-free, and tested there; the component only
renders them. Web and Flutter carry the same messages verbatim so the two clients cannot drift.

**A step may be stricter than the API; it must never be looser (D-370).** The Details step requires all
nine fields it asks for while `CreateEventBodyValidator` takes six as optional — deliberate, because a
step that asks for a field and then waves it through is asking for nothing. Every *other* rule in the
wizard mirrors an existing server refusal (`online_url_required`, `invalid_age_range`,
`consent_text_required`, `invalid_registration_window`) rather than inventing one.

**Hide an input the product cannot use; never disable the submit for it (D-327).** Private events show
no team cap, results date, certificate release or age bounds. Two distinct justifications sit behind
that one list and they must not be conflated: the first three come from the **D12 matrix**
(`private-gathering` marks `teams`/`scoring`/`certificates` Unsupported), the age bounds from a
**product judgment** — there is no `age` capability, and an age rule turns away a stranger who
registered, which a private event has no door for. When hiding a field, say which of the two you are
applying; "the matrix says so" is checkable and "it makes no sense here" is a decision that needs a
`D-NNN`. Filter at **field** level, not step level — the Windows step is still legitimate for a private
event because `check-in` and `forms` are Optional. And keep it a *rendering* decision: the Capability
Engine describes what an event supports and performs no authorization (D-266 M2), so an unclassified
Type — which resolves Public — must never lose a field.

**User Workspace ≠ Event Host Workspace.** `/workspace` is every user's activity hub — participated
events plus approved created ones — and must never become an organizer dashboard. Event-management
surfaces belong to the per-event Host Workspace behind `host/events/{id}`.

Do not reintroduce: an organization list or detail page, a per-organization event list, a "current
organization" selector or cookie, or any route shaped `/orgs`, `/org/create`, `/org/:id/events/create`.
Event management is addressed **by event** — `/host/events/{id}/…`, whose org comes from the event via
`requireEventOrg`, not from anything the user selected. The surfaces that genuinely need an organization
(representation, verification, payouts, team) name it in the path under `/host/representing/{orgId}/…`
and are reached from Settings → Representing.

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

Any field whose value reaches the backend as a phone uses `PhoneField` from `@kurx/ui` — never a bare
`<input>`. A plain text box submits digits with no country, and the backend reads those in the legacy `IN`
region, so an overseas number is silently filed as an Indian one. `PhoneField` is controlled and emits
`(e164, valid)`; in a server-action form, hold the value in state and post it through a
`<input type="hidden" name="phone">`, gating submit on `valid` (or on `value !== "" && !valid` where the
field is optional). For mixed identifier fields that accept phone *or* email *or* username, use
`toE164Identifier` instead — it leaves non-phone input untouched.

**Always pass `label`.** `PhoneField` renders a real `<label>` only when you give it one; without it the
control's accessible name falls back to the placeholder, which disappears the moment the user types. You no
longer need to pass `id` alongside it — the component falls back to `useId()` (D-384 §4), the same way
`Field` does. Before that fix a `label` with no `id` produced `<label htmlFor={undefined}>` beside
`<input id={undefined}>`: a label attached to nothing, correct-looking on screen and silent in every test.
Two shipped screens were sitting on it, including the admin console's own sign-in page.

**Keep the phone libraries in step across surfaces.** Backend `libphonenumber-csharp`, web/admin
`libphonenumber-js`, Flutter `phone_numbers_parser` are three independent ports of Google's metadata on
different release cadences. Because submit is gated on client-side `valid`, a client whose metadata is
*older* than the server's will **block** a user on a newly-allocated number range — the failure lands on the
person who cannot register, and nothing logs it. Bump all three together, and when a "valid number rejected"
report arrives, compare metadata versions before looking at the code.

This is now guarded rather than merely documented: `docs/api/phone-conformance.json` is a shared corpus that
all three platforms assert in their own test suites (D-288), so drift fails CI instead of failing a user.
When it goes red, the disagreeing platform is the outlier — bump its metadata, never edit the fixture.


## The signed-out surfaces have their own visual register (D-380 / D-384)

`web/app/(public)/page.tsx`, `/login`, `/register` and `web/components/marketing/**` may use ambient light,
depth and idle motion. **Everywhere a signed-in user works — product app, host workspace, admin, Flutter —
`visual-identity.md` P1 ("opaque, not glass") and P6 ("motion confirms, never performs") are unchanged and
still binding.** The split is by purpose: a signed-out page has to make a stranger believe the product, a
product surface has to let someone who already believes get work done. Do not lift a component out of
`marketing/` into a product surface without a new decision.

The primitives are CSS 3D over the already-installed framer-motion — no WebGL, no renderer, ~4KB:

* `components/marketing/depth.ts` — the five named Z planes, and `plane(z)`. **Plain module on purpose:**
  `spatial.tsx` is `"use client"`, and a server component importing a value from a client module gets a
  client *reference*, not the function, so calling `plane()` during render fails.
* `components/marketing/spatial.tsx` — `Tilt` (pointer parallax on motion values, never React state),
  `Float`, `SpatialCard`, and `SpatialRoot` (framer `MotionConfig reducedMotion="user"`).
* `components/marketing/atmosphere.tsx` — the ambient wash, as ONE element with two
  `radial-gradient(… at x% y%)` layers so nothing overflows and no section needs `overflow: hidden` to
  crop it.
* `components/auth/auth-shell.tsx` — the frame `/login` and `/register` share so they cannot drift.

**Any scroll-reveal on a public page needs a no-JS guard.** `MotionPanel` writes framer's
`initial={{opacity: 0}}` into the *server-rendered* HTML; the home page shipped 11 blocks that were present
in the markup and invisible on screen with scripting off. `app/(public)/page.tsx` carries a `<noscript>`
style that resets `[style*="opacity:0"]`.
