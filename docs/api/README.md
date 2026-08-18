# API reference

> **User-first, event-first architecture ([D-267](../DECISIONS.md) · [D-268](../DECISIONS.md) · [D-269](../DECISIONS.md), building on [D-074](../DECISIONS.md)/[D-075](../DECISIONS.md)).** Kurx has Users, Events and Representations — no organization accounts, organizer accounts, or personal organizations. **A user owns an event** (`events.created_by`); the organization on an event is the one it *represents*. Event authorization is the single `IEventAuthority`. An institution is created only via an admin-approved **representation request** (`POST /v1/orgs/representation-requests`), which stages a hidden `PendingReview` placeholder org and, on approval, links the submitter as a **Verified Representative** (never Owner). Registry search and the public org profile are **Verified-only**. See [`../architecture/event-creation.md`](../architecture/event-creation.md).
>
> ✅ **[`openapi.json`](openapi.json) regenerated from the running API — 441 paths / 535 operations** (counts re-measured 2026-08-14; last regenerated for the profile validation constraints, 2026-08-12; the messaging additions below landed 2026-08-08 under D-295/D-296). Messaging Phase 2 is on the contract: reactions, search, delivery receipts, forwarding, per-member pin/mute/archive, shared media, and `PinMessageBody.durationHours` with `ChatMessageView.pinned_until`. Regenerate with `scripts/generate-openapi.sh` after any contract change.

Base URL: `http://localhost:5080` in dev. The client env var differs per surface and each reads its own exact name: web `NEXT_PUBLIC_API_BASE_URL` (SSR: `API_INTERNAL_URL`), admin **`NEXT_PUBLIC_API_URL`** (SSR: `API_URL`), mobile `KURX_API_BASE`. See `.env.example`. Swagger UI is available at `/swagger` in Development.

## Property naming: camelCase in, snake_case out

**Requests** are bound camelCase (`{"refreshToken": "…", "ticketTypeId": "…"}`). **Responses** are
snake_case (`{"available_paise": 0, "created_at": "…"}`).

One caveat that is easy to get wrong: the response half is applied to types declared in
`Kurx.Application.Abstractions`. A response record declared elsewhere escapes it and still emits
camelCase, so check the namespace before assuming a field is snake_case. Stored payloads are also
untouched (push `DataJson`, outbox rows), because they are not responses. The rule and its edges live in
`.claude/memory/api-conventions.md`; this page states the contract, that file states how it is enforced.

This is worth stating plainly because it was not always true and the drift was expensive. Most endpoints
hand-build snake_case payloads, but a minority forwarded an `Kurx.Application.Abstractions` record
straight out of `Results.Ok(...)`, where ASP.NET's default policy showed through as camelCase. Web **and**
admin had both written snake_case clients against `/v1/orgs/{orgId}/wallet` on the reasonable assumption
that the majority convention held; `.parse()` threw on every call and two finance pages could not render
at all. `SnakeCaseResponseConverter` now applies the convention to those records too
([D-259 addendum](../DECISIONS.md)) — it is write-only, so request binding is untouched.

If you are writing a client, read [`openapi.json`](openapi.json) rather than inferring from a neighbouring
endpoint. That file is the contract.

## The checked-in OpenAPI contract

[`docs/api/openapi.json`](openapi.json) is generated from the running API and committed. CI regenerates it
and **fails the build on any diff**, so a contract change that skips the regeneration step is caught at the
PR rather than by a client breaking at runtime. Regenerate with
[`scripts/generate-openapi.sh`](../../scripts/generate-openapi.sh) and commit the result alongside the
change that caused it.

**What the gate does not prove** ([D-326](../DECISIONS.md)). It compares the committed spec to the running
API, so it proves *spec ↔ mapper*. It cannot prove *mapper ↔ view*: a `To*Json` that silently drops a
property produces a spec that faithfully describes the diminished shape, and the gate goes green over it.
`CategoryResponse` omitted `ProductClass` for exactly that reason — `/v1/categories` served **0 of 111**
types with `product_class`, and the Create-Event wizard's Private branch offered no categories at all,
with no failing test, no type error and a clean drift check. When adding a field to a `*View`, the wire
record and its mapper are part of that change, and the test that proves it belongs over HTTP.

Two further limits to be honest about ([D-259](../DECISIONS.md)):

- **Response schemas are declared on 461 of 535 operations** ([D-313](../DECISIONS.md)), plus 32 declared
  `204 No Content` and 3 binary downloads — 496 covered, **39 remain undeclared** (4 of them intentionally), every one tracked in
  [`UNDECLARED_TRACKER.md`](UNDECLARED_TRACKER.md). Those still return anonymous
  objects (`Results.Ok(new { … })`), which have no nameable type for Swashbuckle to infer, so they appear
  with no response body. Annotating one means extracting a response DTO first — and the DTO's keys **and
  values** must match what the mapper actually emits, or the spec documents a shape the API never sends.
  Tracked in [`../roadmap/README.md`](../roadmap/README.md). The gate protects everything it describes
  today, and every new annotation lands as a reviewed diff rather than an invisible change.

  **A response DTO must be declared in `Kurx.Application.Abstractions`.** Anywhere else it escapes
  `SnakeCaseResponseConverter` and silently emits camelCase — a wire break with a green build. `204`,
  empty `200` (the two webhook acks) and binary downloads are declared as what they are; no DTO is
  invented for them. Binary responses carry `format: binary`, not `format: byte`, which in OpenAPI 3.0
  would mean base64.

  **Repeated shapes get one contract, not one each.** 80 endpoints across 34 files answered
  `new { ok = true }`; they now share `OperationAck`. Do not add `DeleteXSuccessResponse` siblings.

  The public-profile and identity surfaces were the most recent batch (12 sub-resources plus the two
  roots). Two of them show why "extract the record and return it" is not mechanical: the certificates
  mapper renamed `CreatedAt` to `issued_at`, and the journey mapper nested five fields under `evidence`
  and added a constant `source`. Returning the raw record would have changed both wires silently, so the
  first is pinned with `[JsonPropertyName]` and the second has a real `JourneyNodeView` type.
- **`required` is derived, not native.** Swashbuckle 6.6.2 has no
  `NonNullableReferenceTypesAsRequired` (it landed in 6.7.0), so `RequiredFromNonNullableSchemaFilter`
  marks every non-nullable property required from the nullability `SupportNonNullableReferenceTypes()`
  already computed. Before it, `required` appeared on 36 of 266 schemas and two of `contract-check`'s
  checks were silently inert; it is now on 245 of 290. A nullable-but-always-present key is deliberately
  left out of `required` — understating in the safe direction.
- **The spec describes shape, not all constraints.** Validation rules expressed in FluentValidation are
  projected onto the schema where they can be read mechanically — `enum`, `pattern`, `maxLength`/`minLength`,
  `minimum` and `required` are published. Rules expressed as custom predicates (`.Must(...)`) are not, so a
  request can satisfy the schema and still be rejected with `validation_failed`.

## Error format

Every error response — validation failures, expected business errors, and unhandled exceptions — is an RFC7807 `ProblemDetails` JSON body (`Content-Type: application/problem+json`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "invalid_code",
  "status": 401,
  "error": "invalid_code",
  "correlationId": "b3d1...uuid"
}
```

- `error` is a stable machine-readable code (e.g. `invalid_code`, `rate_limited`, `forbidden`, `not_found`, `already_member`, `validation_failed`, `invalid_token`).
- `correlationId` matches the `X-Correlation-Id` response header — include it when reporting a bug.
- `validation_failed` responses additionally include an `errors` map of field name → messages.
- Unhandled server errors return `500` with a generic `detail` (the real exception message is only included when `ASPNETCORE_ENVIRONMENT=Development`) — check the API logs for the matching `correlationId` for details.

## Auth (`/v1/auth`, unauthenticated)

| Endpoint | Method | Body | Notes |
|---|---|---|---|
| `/v1/auth/otp/request` | POST | `{ phone }` | Rate-limited (3/phone/10min, 10/IP/hour, D-005). Console dev provider prints the OTP to the API log. |
| `/v1/auth/otp/verify` | POST | `{ phone, code }` | 6-digit code, 5-min expiry, max 5 attempts. Returns access/refresh tokens. **Combined sign-in *or* register** (D-011/D-037): an unknown phone gets an account created here, a known one gets a session — `is_new_user` tells them apart. |
| `/v1/auth/refresh` | POST | `{ refreshToken }` | Rotates the refresh token; reusing an already-rotated token revokes all sessions for that user (D-009/D-014). |
| `/v1/auth/logout` | POST | `{ refreshToken }` | Revokes the given refresh token. |

**There is deliberately no "is this phone registered?" endpoint, and `otp/request` answers identically either way.** Asking before the code is verified would hand any anonymous caller an account-enumeration oracle over phone numbers — the same property `/v1/auth/password/reset/start` already protects by returning an identical response whether or not the account exists. Clients must therefore *say* what the flow does ("we'll sign you in, or set up a new account") rather than branch the UI up front; the branch happens after verification, on `is_new_user` (D-311).

`GET /v1/auth/registration/status` (authenticated) → `{ has_password, email, email_verified, phone, phone_verified, has_trusted_device, needs_onboarding, remaining }`. `remaining` is the ordered list of outstanding ceremony steps — `complete_profile` → `create_password` → `verify_email` → `enroll_device` — with the two blocking steps first (D-311). `needs_onboarding` is the same flag `/v1/me` returns, from the same `Kurx.Domain.Onboarding` rule; the first two entries of `remaining` gate it, the last two never do.

Token response shape:

```json
{
  "access_token": "...", "access_expires_at": "...",
  "refresh_token": "...", "refresh_expires_at": "...",
  "user_id": "uuid", "is_new_user": true
}
```

## Trusted-device authentication (AM1–AM10, D-081…D-104a)

The OTP rail above still works unchanged. Everything below is the trusted-device platform layered
on top of it — a device's private key, or a passkey, replaces the OTP as the login factor.

All token-returning endpoints below use the same token response shape shown above.

### Device enrollment and management (`/v1/auth/devices`, authenticated) — AM2

| Endpoint | Method | Body | Notes |
|---|---|---|---|
| `/v1/auth/devices/enroll` | POST | `{ name?, platform, publicKeySpki, alg?, attestationJson? }` | Registers a device public key (`alg` defaults to `ES256`). Returns `{ device_id, credential_id, challenge_id, nonce, match_number, expires_at }`. The device must sign `nonce` to finish. |
| `/v1/auth/devices/enroll/verify` | POST | `{ challengeId, deviceId, signature }` | Proves possession of the private key. On success the device becomes `Trusted`. |
| `/v1/auth/devices` | GET | — | The caller's enrolled devices. |
| `/v1/auth/devices/{id}/revoke` | POST | — | Revokes one device. |

### Password (`/v1/auth/password`, authenticated) — Phase 2

Factor 1 of the target three-factor architecture (D-126, D-129). Both mutating routes are rate-limited
on the `otp` policy because Argon2id is deliberately expensive and must not be freely spinnable.

| Endpoint | Method | Body | Notes |
|---|---|---|---|
| `/v1/auth/password/status` | GET | — | `{ has_password, min_length, max_length }`. |
| `/v1/auth/password/set` | POST | `{ password }` | First password only. `400 password_already_set` if one exists — the unique index on `user_id` makes the race safe. |
| `/v1/auth/password/change` | POST | `{ currentPassword, newPassword }` | Requires the current password **even with a valid session**, so a stolen access token cannot take ownership of the account. |

**Error codes:** `password_too_short`, `password_too_long`, `password_breached`,
`password_contains_identifier`, `password_reused`, `password_already_set`, `password_not_set`,
`invalid_credentials`, `account_locked`.

**Status codes:** policy failures are `400` (the user can act on them); a wrong current password is
`401`; a lockout is `423 Locked` so the client shows a wait rather than an unhelpful retry.

**Policy:** min 12 / max 128, no composition rules, no expiry (NIST SP 800-63B), breach deny list,
and refusal of passwords derived from the user's own username / email / phone. Lockout is 10
consecutive failures → 15 minutes, and **applies to the correct password too** — otherwise it is
decorative and an attacker keeps learning whether each guess was right.

**Password reset is deliberately absent.** A user who cannot sign in resets through the recovery
ceremony (OTP **+** device approval **or** recovery code — never OTP alone), which lands in Phase 4.
See [`docs/auth/AUTHENTICATION_ROADMAP.md`](../auth/AUTHENTICATION_ROADMAP.md).

### Sessions (`/v1/auth/sessions`, authenticated) — AM5

| Endpoint | Method | Body | Notes |
|---|---|---|---|
| `/v1/auth/sessions` | GET | — | Live sessions ("your devices"), for remote sign-out. |
| `/v1/auth/sessions/{id}/revoke` | POST | — | Remote sign-out of one session. |

### Push-approval login (`/v1/auth/login`) — AM4

Passwordless and OTP-free: a signature from an already-trusted device is the factor. `/start` and
`/status` are anonymous — the caller has no session yet.

| Endpoint | Method | Auth | Body | Notes |
|---|---|---|---|---|
| `/v1/auth/login/status` | POST | none | `{ challengeId, pollToken }` | Poll. Returns `{ status }` while pending/rejected; adds the token pair once `status = "approved"`. Live updates are also available over the `/hubs/login` SignalR hub (AM9). |
| `/v1/auth/login/pending` | GET | required | — | Approvals awaiting this user's trusted device. |
| `/v1/auth/login/approve` | POST | required | `{ challengeId, deviceId, signature }` | Approves; the device signs the challenge nonce. |
| `/v1/auth/login/reject` | POST | required | `{ challengeId }` | Rejects. |

### Password login and second factors (`/v1/auth/login/password`) — D-280 / D-283

Factor 1 is always the password (INV-A). What satisfies factor 2 is **the backend's decision**: clients
render the returned `methods[]` and never hold a list of their own.

`POST /v1/auth/login/password` takes `{ identifier, password, rememberBrowser?, surface? }`, where
`surface` is `web` (default) | `mobile` | `admin`. Surface only **reorders** `methods[]` — it never
changes which are available, so a client cannot widen its own options by lying. It replies with one of:

| `next` | Meaning | Payload |
|---|---|---|
| *(absent)* | A valid trusted-browser cookie satisfied factor 2 — signed in. | the token pair |
| `device_approval` | The preferred web path: a challenge was pushed to the user's trusted devices. Unchanged ceremony — match number, signed nonce, first-approval-wins. | `challenge_id`, `poll_token`, `match_number`, `expires_at`, `methods` |
| `second_factor` | No device (or a surface that prefers a code). The client picks from `methods`. | `challenge_id`, `poll_token`, `expires_at`, `methods` |

`methods[]` entries are `{ method, rank, label, hint }`. `method` is the stable key to switch on —
`trusted_device` \| `passkey` \| `sms_otp` \| `email_otp` \| `recovery_code`; `label` is localised
server-side, so never parse it; `hint` is an already-masked destination or device name.
**Only available methods appear** — there is no disabled variant, so a client cannot leak the difference
through its UI. `email_otp` appears only for a verified address (D-282).

| Endpoint | Method | Auth | Body | Notes |
|---|---|---|---|---|
| `/v1/auth/login/second-factor/send` | POST | none | `{ challengeId, pollToken, method }` | Delivers a code for a code-based method. The poll token proves this caller passed the password step. A method the account does not hold is refused (`method_unavailable`), never silently downgraded. |
| `/v1/auth/login/second-factor/verify` | POST | none | `{ challengeId, pollToken, code }` | On success returns the token pair **and sets the `kurx_tb` trusted-browser cookie** if the password step asked to be remembered. A wrong code and an unknown challenge are deliberately indistinguishable (`invalid_code`); the attempt cap lives in the OTP platform. |

**Delivery channel is server-owned (D-281):** phone codes go over SMS (AWS SNS), email codes over SES.
WhatsApp is never used for authentication.

**Browser clients must send credentials on `/second-factor/verify`** (`withCredentials`, or the server-side
equivalent of storing the returned `Set-Cookie`). It is the second place the `kurx_tb` cookie is issued —
the first being `/login/password` — and dropping it makes "remember this browser" fail *silently*: the user
is simply re-challenged next time, with no error to explain it (D-291).

**Phone numbers on the wire are canonical E.164** with the leading `+` (`+6591234567`), in both directions
(D-290). Send E.164: a national-format number with no `+` is interpreted in the legacy `IN` region and will
either fail to match an account or, historically, match the wrong one. Responses that carry a phone read the
canonical column, so an export or admin view always states the country.

### Passkeys / WebAuthn (`/v1/auth/passkeys`) — AM3

`response` members are the browser's raw `navigator.credentials` payload, passed through to the
FIDO2 library rather than reshaped into our own DTOs.

| Endpoint | Method | Auth | Body | Notes |
|---|---|---|---|---|
| `/v1/auth/passkeys/register/options` | POST | required | — | Returns `{ challenge_id, options }` for `navigator.credentials.create()`. |
| `/v1/auth/passkeys/register` | POST | required | `{ challengeId, response, deviceName? }` | Returns `{ ok, device_id }`. |
| `/v1/auth/passkeys` | GET | required | — | The caller's registered passkeys. |
| `/v1/auth/passkeys/login/options` | POST | none | `{ identifier }` | Returns `{ challenge_id, options }` for `navigator.credentials.get()`. Same shape regardless of whether the identifier exists. |
| `/v1/auth/passkeys/login` | POST | none | `{ challengeId, response }` | Returns the token pair, or `401` on failure. |

Registration and login use **distinct challenge purposes** (`PasskeyRegister` / `PasskeyLogin`,
D-098) so a passkey ceremony can never be replayed against the push-approval rail.

### Step-up authentication (`/v1/auth/step-up`, authenticated) — AM6

Step-up strengthens an existing session; it never creates one.

| Endpoint | Method | Body | Notes |
|---|---|---|---|
| `/v1/auth/step-up/start` | POST | `{ action? }` | Returns `{ challenge_id, nonce, match_number, expires_at }`. |
| `/v1/auth/step-up/verify` | POST | `{ challengeId, deviceId, signature }` | Satisfies step-up for the configured window. |
| `/v1/auth/step-up/status` | GET | — | `{ satisfied, valid_until, can_step_up }`. `can_step_up` is false when the user has no trusted device. |

### Recovery codes (`/v1/auth/recovery-codes` + `/v1/auth/recovery`) — AM7

| Endpoint | Method | Auth | Body | Notes |
|---|---|---|---|---|
| `/v1/auth/recovery-codes` | POST | required | — | Mints a fresh set and returns `{ codes, count }` — **the only time plaintext codes leave the server**. Requires step-up (`step_up_required`, 403) for users who *can* step up; users with no trusted device are exempt, since demanding it would lock them out of the feature that protects them. |
| `/v1/auth/recovery-codes` | GET | required | — | `{ remaining }`. |
| `/v1/auth/recovery/start` | POST | none | `{ identifier }` | Always `{ ok: true }`, existing account or not. Rate-limited. |
| `/v1/auth/recovery/redeem` | POST | none | `{ identifier, otpCode, recoveryCode }` | **Both** an OTP and a recovery code — recovery is deliberately two-factor. Returns the token pair, or `401`. |

### JWKS (`/.well-known/jwks.json`, public) — AM10, D-099

Public by design: anything verifying a Kurx token reads it. Serves the ES256 public keys in
`Active` and `Retiring` states.

> **Token issuance is still HS256.** ES256 keys are published and accepted for *validation* only,
> so verifiers can be migrated before issuance cuts over. See the handover document for the gates
> that must pass before ES256 issuance is enabled.

## Current user (`/v1/me`, authenticated) — D-037, D-038

> **`trust` block (D-046, extended by [D-307](../DECISIONS.md)).** `GET /v1/me` carries the caller's live
> capabilities, derived per request from `user_identities` + fraud — never cached, never token-borne:
>
> ```
> trust: { level, can_organize_free, can_organize_paid, can_receive_payout,
>          identity_verified, bank_verified,
>          can_create_public_event,      // D-307 — free OR paid public hosting
>          can_create_private_event }    // D-307 — constant true
> ```
>
> `can_create_public_event` = identity + PAN + bank (which already means **penny drop passed and holder
> name matched**) + fraud-clear. **A free public event requires the full set**: publishing to the public
> is itself a trust event, so verification is not bounded by whether money moved.
>
> `can_create_private_event` is `true` — a Private event cannot be Listed, cannot take payment and reaches
> no discovery surface, so there is nothing to verify.
>
> `can_organize_paid` is **unchanged** and still gates the money path (`OrderService`, payment readiness).
> It shares a predicate with `can_create_public_event` today; they are separate fields because they answer
> different questions and must be free to diverge.
>
> Both new flags are optional on the wire for older clients — but a client must default
> `can_create_public_event` to **false** (the closed position) and `can_create_private_event` to **true**.


`GET /v1/me` → `{ id, phone, name, username, email, date_of_birth, created_at, needs_onboarding, headline, bio, education_json, skills, links_json, avatar_key, cover_key, privacy }`. The public profile root additionally carries `_meta` (D-221) — a per-field provenance map of `verified` / `self_declared` / `derived`, so a client never has to guess whether a value is proof or a claim. It is additive and parallel to the existing flat keys; the root response shape is frozen and a client ignoring `_meta` behaves exactly as before. `verification` likewise gained `phone_verified` (true by construction), `email_verified`, `speaker_verified`, `community_verified`. **No moderation internal is ever exposed publicly** — no risk score, fraud signal, report count, moderation flag, suspension state or trusted-device detail; a test enforces their absence. `needs_onboarding: true` when the name is blank **or** `username` is null **or** — for accounts created at or after `Onboarding.RequirementsEffectiveFrom` (the `AddUserDateOfBirth` migration timestamp) — `date_of_birth` is null **or** the account has no password (D-311; supersedes the Name+Username rule of D-037, which itself superseded Name-only D-012). The date-of-birth and password requirements are **not retroactive**: an account older than the column could not have been asked for one, and onboarding gates new accounts rather than migrating old ones. Grandfathered accounts still see those steps in `remaining` so Security settings can offer them. The rule lives once, in `Kurx.Domain.Onboarding`, and both this endpoint and `/v1/auth/registration/status` call it — they previously carried hand-copied expressions that had already drifted (`Name == ""` here vs `IsNullOrWhiteSpace(Name)` there), so a whitespace name was onboarded according to one and not the other. Email verification and device enrolment appear in `remaining` but deliberately do **not** gate this flag: neither can be completed by a user who mistyped an address or is on a handset that cannot enrol.

`date_of_birth` is a date-only value (`YYYY-MM-DD`, D-311) — not a timestamp, because a birth date has no time or zone and `timestamptz` would shift the calendar day for every user outside UTC. Minimum age 13, enforced server-side (`under_minimum_age`, 400; `invalid_date_of_birth`, 400 for a future or implausible date). It is the only fact on an account that an age-restricted event's `MinAge`/`MaxAge` eligibility can read; it remains self-declared and is never proof of age.

`created_at` is when the account was created — full UTC ISO-8601, served **only to the owner** (D-312). The column has been written on insert since the beginning and was simply never served, so no client could show "member since". It is server-generated once and immutable: nothing on the login, onboarding, profile-edit, password or device-registration paths writes it, and no request body can move it. The public profile carries the same fact at month precision only, as `joined_at`. `email` is unique case-insensitively at the DB level (D-038) — `Test@x.com`/`test@x.com` collide — though no endpoint sets it yet.

The self-declared display fields (`headline` … `cover_key`) are served here as of D-219 so a profile editor prefills from the caller's **own** record. Do **not** prefill an edit form from `GET /v1/public/users/{username}` — that read returns nothing for an unclaimed username or a non-public profile, and a form that submits every field will then overwrite real data with blanks (the real bug D-219 fixed).

`privacy` → `{ profile_public, show_attended, show_certificates, show_allies }`, the same object `PATCH /v1/me/privacy` returns (one shared serializer, so the read and write shapes cannot drift).

`PATCH /v1/me/profile` → any of `{ name, username, headline, bio, educationJson, skills, linksJson, avatarKey, coverKey, dateOfBirth }`, all optional/partial. `dateOfBirth` is `YYYY-MM-DD` and is validated server-side (D-311) — the client's picker bounds are a convenience, never the rule.

**Field limits are enforced here, not only in each client's form** (`UpdateProfileBodyValidator`, D-311): `name` 1–120, `headline` ≤ 200, `bio` ≤ 2000 (matching the organisation profile), `educationJson`/`linksJson` ≤ 4000, `avatarKey`/`coverKey` ≤ 512, and `skills`/`interests` ≤ 50 entries and `languages` ≤ 25, each entry ≤ 60 characters. This endpoint previously ran **no validator at all**, so every one of those was unbounded while mobile's onboarding capped `bio` at 300 in the UI — a hint, not a constraint, and the same shape as a password policy that reads 8 on the client and 12 on the server. Violations are `400`. A username change is validated (format, reserved words), checked for conflicts against active usernames, org slugs, and any username still inside its 30-day reclaim hold (`username_taken`, 409, D-022/D-037), and throttled to once per 30 days (`username_change_throttled`, 429).

`PATCH /v1/me/privacy` → any of `{ profilePublic, showAttended, showCertificates, showAllies }` plus `sections` (D-221), all optional/partial (an absent flag or section is left unchanged). Returns the full `privacy` object, now including `sections`.

`sections` maps a profile section to one of four tiers: `public` · `connections` (an accepted ally) · `event_participants` (shares ≥1 public event with the owner) · `only_me`. Sections: `profile` (the whole page — below the tier it returns **404**, never 403, per D-018), `events`, `attended`, `certificates`, `achievements`, `organizations`, `timeline`, `network`, `metrics` (D-225), `contributions` (D-228). Unknown section → `invalid_section` (400); unknown tier → `invalid_visibility` (400). A named section overrides the boolean covering it, and writes dual-write those booleans so a rollback to the pre-D-221 read path resolves identically. Resolution order for any section is **stored override → legacy boolean → documented default**. These four flags gate every public profile read; before D-219 they were enforced but settable by nothing, so `showAttended` — which defaults to **false** — permanently hid the attended-events lane for every user. Kept separate from `PATCH /v1/me/profile` on purpose: a rejected profile save (e.g. `username_taken`) must not silently discard a privacy change submitted alongside it.

`POST /v1/me/profile-image/presign` → `{ slot, contentType, maxBytes }` ⇒ `{ key, url, headers }` (D-219). `slot` is `avatar` or `cover` — a closed set, because it forms part of the storage key `users/{userId}/{slot}/…`, so a caller can never presign into another user's prefix (`invalid_slot`, 400). `contentType` must be a real image type — `image/jpeg|png|webp|gif|avif` (`invalid_content_type`, 400) — because this key is rendered directly in an `img` tag on a public page, unlike the document presigns. Ceiling 5 MB. Two-step like every other upload here: PUT the bytes to `url`, then persist the returned `key` via `PATCH /v1/me/profile` (`avatarKey`/`coverKey`). No role check — you may always replace your own picture.

`GET /v1/usernames/availability?username=` (unauthenticated) → `{ username, status }` where `status` is one of `available` / `unavailable` (active user, org slug, or reclaim-hold conflict) / `reserved` / `invalid`. Use this to check availability — don't probe via `PATCH /v1/me/profile`.

`POST /v1/me/phone/verify` → `{ phone, code }` (authenticated). Verifies an OTP already requested for the new number via the normal `POST /v1/auth/otp/request`, then reassigns it to the calling user's existing account — `id`/all relationships are unchanged, only `phone` changes. **Revokes every other active refresh token for the user (D-038)** — a stolen device with a live session is signed out the next time it tries to refresh — and returns a fresh token pair for the calling device, in the same shape as `otp/verify`/`refresh` (`access_token, access_expires_at, refresh_token, refresh_expires_at, user_id, is_new_user`). Errors: `otp_not_found`/`invalid_code`/`too_many_attempts` (OTP problems, same codes as login) or `phone_already_registered` (number already belongs to another account, including the case where a concurrent request won the same number first).

## Organizations (`/v1/orgs`, authenticated)

All endpoints require `Authorization: Bearer <access_token>`. Per-org permissions follow the role matrix in `docs/DECISIONS.md` D-015 (Owner/Manager/Staff/Finance) — enforced against the database per request, not by a static policy.

| Endpoint | Method | Who | Notes |
|---|---|---|---|
| `/v1/orgs` | POST | any user | Creates a **personal** org only (`personal: true`). A non-personal create returns `use_representation_request` — institutions go through the representation request below (D-075). |
| `/v1/orgs/representation-requests` | POST `{ name, type?, legalName?, primaryDomain?, documents[] }` | any user | Stages a not-yet-registered institution as a **hidden `PendingReview` placeholder** org (no Owner; caller becomes a *pending* `Representative`). Invisible to search/public profile until an admin approves it via `/v1/admin/orgs/{id}/verification/review` (D-075). Errors: `invalid_name`, `org_blacklisted`, `invalid_domain`, `organization_domain_taken` (vs a verified org), `invalid_evidence`. |
| `/v1/orgs/representation-requests/media/presign` | POST `{ contentType, maxBytes }` | any user | Presign an upload for representation-request evidence — **user-scoped** (the org doesn't exist yet), keyed `representation-requests/{userId}/*`. Returns `{ key, url, headers }`; PUT the file, then pass `key` as a `documents[].storageKey` above (D-076). |
| `/v1/me/representations` | GET | any user | **Which organizations may I represent?** (D-268) — the Representing step and the Representing surfaces read this. Rows are `organization_id` / `name` / `slug` / `logo_key` / `authority`. Self-representation is **not** in it: representing yourself is not an organization, so there is no "personal" row and no flag to filter on. Was `GET /v1/orgs`, which read as organization management. |
| `/v1/orgs/{orgId}` | GET | any member | Org detail including payout status/tier. |
| `/v1/orgs/{orgId}` | PATCH | Owner/Manager | Update name/bio/links/logo. Slug never changes. |
| `/v1/orgs/{orgId}/members` | GET | any member | List members + roles. |
| `/v1/orgs/{orgId}/members` | POST | Owner (any role) / Manager (Staff only) | Unknown phones are provisioned (D-015). |
| `/v1/orgs/{orgId}/members/{userId}` | PATCH | Owner | Change role; the last Owner can't be demoted. |
| `/v1/orgs/{orgId}/members/{userId}` | DELETE | self, Owner, or Manager (Staff only) | Remove a member; the last Owner can't leave. |
| `/v1/orgs/{orgId}/kyc` | GET | Owner/Finance | KYC records + payout account status. |
| `/v1/orgs/{orgId}/kyc/bank` | POST | Owner/Finance | Penny-drop verification; approval activates Razorpay Route payouts (D-016). Full account number is never persisted. |
| `/v1/orgs/{orgId}/kyc/pan` | POST | Owner/Finance | PAN match check. |

## Events (`/v1/events` + `/v1/me/events`, authenticated; `/v1/orgs/{orgId}/events/…` for management)

**Kurx is user-first: a user owns events (D-267).** An organization is optional metadata an event
*represents* — never a container a caller has to open, choose, or create before they can list or make
an event. Three routes carry that:

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me/events` | GET | **Every event the caller owns, plus those they can manage through a representation**, newest first, `{ items, total }` with `page`/`pageSize`. No org id is accepted or needed. Each row carries a `representation` object (`kind: "personal" | "organization"`, `organization_id`, `organization_name`, `verified`) as *metadata* — for rendering "representing X", never as a grouping key and never as the owner. `kind: "personal"` carries no organization identity at all. This is what Workspace reads. |
| `/v1/events` | POST | **Create an event owned by the caller.** `representingOrgId` is a **body field**, chosen inside the creation flow; **omitting it (or sending `null`) means Personal** — the user represents themselves. Authorization here is *representation authority*, not ownership: representing an organization needs a manage-capable seat in it, representing yourself needs nothing. Ownership is recorded as `Event.CreatedBy` and enforced from there (D-268). There is no org-scoped create route: an organization is never required to reach event creation. **`visibility` accepts `Listed` \| `Unlisted` \| `InviteOnly`** — D-266 renamed `Public`→`Listed` and migration `RetireLegacyPrivateVisibility` dropped `Private`; both old values are rejected by `Enum.TryParse`, and stored rows were backfilled. `archetype_slug` and `product` are stamped from the chosen type at creation but are **not yet in any response body** — they surface in M2 alongside `GET .../capabilities`. |
| `/v1/events/{eventId:guid}` | GET | **Authenticated read by event id.** No organization in the path; the caller's role on the event's *own* org authorizes it. This is how a client resolves an event's `org_id` instead of tracking a "current organization". The `:guid` constraint keeps it ahead of the public `/v1/events/{slug}` route below. |

**Authorization for every event surface — management *and* audience — is decided by one service:
`IEventAuthority` (D-269/D-272).** It resolves a caller to a single ordered level and each permission is a
threshold on it:

| Level | Reached by | May |
|---|---|---|
| `Admin` | the `KurxAdmin` claim | everything except content edit (D-191 keeps that organizer-owned) |
| `Manager` | the event's **creator** — needs no membership at all (D-268) · a `Representative` (D-075) · an `Owner`/`Manager` seat in the represented org | manage content + lifecycle, analytics, delete |
| `Staff` | a `Staff` seat in the represented org | read the attendee roster · host the event chat |
| `Participant` | an active participation (speaker/judge/mentor/volunteer) **or a live ticket** (D-272) | view the event · read its feed · attach a post · belong to its chat room |
| `None` | no relationship — including a `Finance` seat, which holds nothing on events | nothing |

**Event payloads carry `representing_org_id` (D-273a).** It is the organization the event *represents* —
never its owner, which is the user in `created_by` (D-268). `org_id` is still emitted alongside it with the
same value but is **deprecated**: it exists only for clients deployed before the rename and will be removed
in the contract phase. New clients must read `representing_org_id`. This applies to the event detail,
event summary, admin event list and pending-approval payloads.

**An organization membership alone is never event access** (D-272). Two permission families sit on the one
ladder: *management* (`ViewAttendees`, `ViewAnalytics`, `ManageContent`, `ManageLifecycle`, `Delete`) and
*audience* (`Participate` at `Participant`, `ModerateAudience` at `Staff`). Audience surfaces —
`GET /v1/events/{eventId}/posts`, `POST /v1/posts` with an `event_id`, and event chat — are gated on
`Participate`, never on a management permission.

A non-Published event 404s (never 403) for anyone with no standing (D-018); a **published, public, listed**
event's feed is readable by any signed-in caller, because that is publicity rather than standing. Three
surfaces have **no admin bypass** and answer only to a seat or ownership — attendees, announcements,
invitations — because their endpoints never passed one; platform staff use the admin console instead.
Attaching a post has no admin bypass either: posting is speech, not moderation. Note that a
`VerificationReviewer` is a *platform role*, not the `KurxAdmin` claim: it reads any event via D-191's
explicit branch but holds no authority over sub-resources.

**`IEventAuthority` performs no capability resolution and the Capability Engine performs no
authorization.** A capability says what an event *supports*; a permission says who may *act*.

**The `/v1/orgs/{orgId}/events/…` group below is event *management*, not navigation.** It survives
because the sub-resources (tickets, speakers, media, sessions, audience, …) are genuinely org-scoped
server-side; the caller reaches them only after opening an event, and derives `orgId` from that event
rather than from any organization they selected. The per-org **list** stays for the admin console's view
of one organization.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/orgs/{orgId}/events` | GET | List the events **representing** this organization — it does not own them (D-268); all statuses visible to members (admin console). Users list their own events via `/v1/me/events`. |
| `/v1/orgs/{orgId}/events/{eventId}` | GET / PATCH / DELETE | Get / update / delete (Draft only, `not_draft` otherwise). **PATCH is refused with `type_conflicts_with_team_ticket` (409, D-367)** when the new `typeId`'s archetype does not support `teams` and the event still has a `Group` ticket type — the Type change is rejected rather than converting the ticket, because converting would delete a `TeamPolicy`, its roster rules and its D-366 price bands as a side effect. Nothing is persisted on that path, including the new archetype. **PATCH on an `Approved` event returns it to `PendingReview` (D-363 §4)** when the payload carries a *material* field — title, categoryId, typeId, audienceLevelId, templateId, startsAt, endsAt, timezone, venue/city/mode/onlineUrl/location, capacity, visibility, eligibility, legal, commerce — clearing the review claim and auditing `event.review.reopened_by_edit`. The edit is applied, not refused; the response carries the new status, so a client asserting `approved` after a save sees `pendingreview`. Cosmetic fields (subtitle, description, tags, content, contact, schedule windows, language) leave the approval standing. The test is *presence in the payload*, not a value change. **DELETE is a SOFT delete (D-364)** — the row is retained with `DeletedAt` set and a global query filter removes it from every read, including the owner's; nothing cascades. It is refused with **`event_has_history`** once the event has any order, ticket or registration (D-363): such an event ends at `Cancelled` (with refunds, D-101) or `Archived`, never at deletion. The slug and short code are released for reuse — both unique indexes are partial on `DeletedAt IS NULL`. |
| `/v1/orgs/{orgId}/events/{eventId}/transition` | POST `{ action, reasonCode?, notes? }` | **D-266 M4 review lifecycle:** `submit_for_review`, `withdraw`, `claim_review`, `release_review`, `request_changes` (**notes required**), `approve_review`, `reject_review` (**`reasonCode` required**, an `EventReviewReason` name), `publish_approved` (from `Approved` only). Reviewer-scoped actions need the `VerificationReviewer` role; an organiser who can manage the event may **not** decide their own submission (`reviewer_required`). Missing reason/notes give `reason_code_required` / `notes_required`. `submit_review` is retained as a **legacy alias that now lands on `PendingReview`** (was `InReview`, retired) — a client asserting on the returned status sees the new value. Legacy `reject` means *send back to `Draft`*; `reject_review` is a formal rejection to `Rejected`. Also: `publish`, `unpublish`, `close`, `archive`, `cancel` — see `EventStatusWorkflow`. **`unpublish` is refused with `event_has_history`** once the event has any order, ticket or registration (D-363): an event people have joined is withdrawn by `cancel` (terminal, obliges refunds) or ended by `close`, never returned quietly to `Draft`. **Publishing (D-377):** the reviewer gate applies to `PendingReview`/`UnderReview` only — a creator publishing from either is refused `reviewer_required`. From `Approved` the creator publishes their own event, paid or free; approval grants the permission, it does not publish. A paid event is refused `paid_event_requires_review` only from `Draft`. **Leaving `Approved` (D-363):** `withdraw` also runs `Approved → Draft` (§1) — an approved event was never public, so it holds no orders and reworking it loses nothing — and `cancel` accepts `Approved` (§2) for abandoning it outright. `invalid_transition` (409) if not valid from the current status. **`cancel`** (D-101) moves `Draft`/`PendingReview`/`UnderReview`/`Approved`/`Published` → terminal `Cancelled` (only `archive` follows); an organizer is refused once the event has started (`event_already_started`, 409) — an admin/reviewer may still cancel. Publishing is refused with `pending_org_verification` (403) while the org is unverified, or `representation_vacant` (403) when a verified org has no verified Representative and no Owner. **Submission completeness (D-378):** `submit_review` refuses an event that has not answered every wizard step — `missing_tagline`, `missing_short_description`, `missing_rules`, `missing_building` / `missing_floor` / `missing_room` / `missing_maps_url` (in-person), `missing_online_url` / `missing_meeting_platform` / `missing_meeting_password` (online or hybrid), `missing_registration_opens` / `missing_registration_closes` / `missing_checkin_opens` / `missing_checkin_closes`, `missing_min_age` / `missing_max_age`, and `missing_terms_url` / `missing_code_of_conduct` / `missing_refund_policy` / `missing_cancellation_policy`. A **Public** product additionally needs `missing_result_date`, `missing_certificate_release` and `missing_max_teams`; a Private one is never shown those controls and is never asked for them. The gate is on this transition **only** — create, update and draft autosave stay permissive so half-filled wizard state still saves (D-266 M8). **`transition_conflict`** when the event's status changed between this request reading it and writing — the transition is claimed in SQL against the status it read, so of two simultaneous decisions exactly one lands and only that one is recorded. Refresh and re-read before retrying: the losing request decided about a state that no longer exists. |
| `/v1/admin/events/review-counts` | GET | D-266 M4 — queue tab counts: `pending_review`, `under_review`, `changes_requested`, `approved`, `rejected`, `legacy_in_review`. Derived from event status in one pass so the tabs cannot disagree with the list they head. `legacy_in_review` is the retired `InReview` bucket, now always `0`, retained until clients drop it. `VerificationReviewer` only. |
| `/v1/admin/events/{eventId}/review-history` | GET | D-266 M4 — that event's decisions from `VerificationReview`, newest first: `decision`, `reviewer_id`, `reviewer_name`, `reason_code`, `notes`, `created_at`. `reviewer_name` is nullable in the contract, but never null in practice for an event review: the FK to `users` is `ON DELETE RESTRICT` and account deletion anonymises rather than removes (D-263), so a departed reviewer reads as `"Deleted user"` and the decision is still returned — an audit trail that loses entries when staff leave is not an audit trail. `VerificationReviewer` only. |
| `/v1/events` | GET | Public search (V3 §15, Phase 16 — see Discovery & Search below): `q`, `categoryId`, `orgId`, `city`, `dateFrom`, `dateTo`, `sort`, `page`, `pageSize`, `price`, `mode`, **+ `kind`, `language`, `lat`, `lng`, `radiusKm`** (all additive/optional). Always Published + Public only. |
| `/v1/events/upcoming` \| `/trending` \| `/featured` \| `/latest` | GET | `?limit=` (default 10). Trending is the deterministic ranking (velocity + conversion + recency), **not raw `ViewCount`**. |
| `/v1/events/for-you` | GET | **Authenticated.** The eligibility-aware feed (§15) — Published + Public events whose audience rule the caller satisfies, ranked. `?limit=`. Internal events are never indexed, so this never leaks their existence (404-not-403). |
| `/v1/events/{slug}` | GET | Detail; appends to the async view stream (D-130). Draft/unlisted/private events 404 for non-members. |
| `/v1/events/{slug}/related` | GET | Related by shared kind / org / unit + text similarity, ranked (from the discovery index). |

**Create-event field groups (D-265).** `POST` and `PATCH` accept six **optional, trailing** object
groups alongside the flat fields — `content`, `legal`, `schedule`, `location`, `eligibility`,
`commerce`. They are grouped rather than flattened because the input records were already 25
positional parameters; twenty-five more would make a transposed argument invisible. Optional and
trailing means **every request body that predates D-265 binds exactly as before** — no versioned
endpoint, no client migration.

| Group | Fields |
|---|---|
| `content` | `tagline` (≤160) · `shortDescription` (≤300) · `logoKey` · `thumbnailKey` · `promoVideoKey` · `rules` · `faqJson` |
| `legal` | `termsUrl` · `termsText` · `codeOfConduct` · `refundPolicy` · `cancellationPolicy` · `requiresConsent` · `consentText` |
| `schedule` | `registrationOpensAt` · `registrationClosesAt` · `checkinOpensAt` · `checkinClosesAt` · `resultDate` · `certificateReleaseAt` · `autoClose` |
| `location` | `building` · `floor` · `room` · `googleMapsUrl` · `meetingPlatform` · `meetingPassword` |
| `eligibility` | `minAge` · `maxAge` · `genderRestriction` (`Any`\|`Male`\|`Female`\|`NonBinary`) · `maxTeams` |
| `commerce` | `platformFeePercent` · `platformFeeFlatPaise` · `taxPercent` · `taxInclusive` · `prizePoolJson` |

**A null field means "leave alone"; an empty string means "clear".** A wizard PATCHes one step at a
time, so a body naming only `content.tagline` must not null the `content.rules` written by another
step. Both clients strip blanks before sending for exactly this reason.

Responses return the same groups back, **plus one deliberate omission**: `location_detail` carries no
`meeting_password`. The event projection serves the organiser reads *and* the public
`GET /v1/events/{slug}`, so a password reachable from it would be published to anonymous visitors.
It is currently write-only; a registrant-gated read is named follow-up work.

New errors (400): `tagline_too_long` · `short_description_too_long` · `consent_text_required` ·
`invalid_registration_window` · `invalid_checkin_window` · `invalid_age_range` ·
`invalid_gender_restriction` · `invalid_platform_fee` · `invalid_tax_percent`.
`EventVisibility` gains **`InviteOnly`** (an author choice — distinct from the admin `IsHidden` flag);
`RegistrationMode` gains **`Both`**; `TicketType` gains `kind` (`Free`/`Paid`/`Donation`/`InviteOnly`),
`minAmountPaise`, `suggestedAmountsJson` and its own `refundPolicy`; `Sponsor` gains `booth`.

Supporting resources follow the same org-scoped pattern: `/v1/orgs/{orgId}/venues`, `/speakers`, `/sponsors`
(each also assignable to an event via `/v1/orgs/{orgId}/events/{eventId}/{speakers|sponsors}`),
`/v1/orgs/{orgId}/events/{eventId}/sessions` (schedule, with a `/reorder` endpoint), `/media` (presign +
attach + remove — media is returned inline on the event detail, not a separate public endpoint), and
`/v1/templates` (the activated Template system — see the Templates & workspace section below).
> **`product_class` on a Type node ([D-307](../DECISIONS.md)).** `GET /v1/categories` now emits
> `product_class` — `"Public"` / `"Private"` / absent — read straight off `EventCategory.ProductClass`
> (D-266 M1). The Create-Event gate uses it to offer only the Types matching the chosen product class.
> **Absent means Public**, matching `ResolveArchetypeAsync`'s own fallback: a client that treated it as
> anything else would offer a Type that then produced an event of the other class. Additive; a client
> ignoring it behaves exactly as before. The gate *filters* — `Event.Product` is still derived and
> snapshotted from the Type at create, never sent by a client.

Categories (`/v1/categories`) and tags (`/v1/tags`) are platform-wide: category writes require `KurxAdmin`;
tags are created automatically from an event's `tags` list and are searchable read-only. A startup seeder
ships the standard 3-tier taxonomy — 3 audiences → 13 categories → 145 types (Audience→Category→Type),
globally-unique slugs, audience-prefixed where a type name recurs across audiences (D-023). The endpoint
contract is unchanged; `?level=Category` returns the 13 categories the host event-creation picker uses.

**Kinds (`/v1/kinds`, public — Event Architecture V3 §2, Phase 1):** `GET /v1/kinds` returns the closed
20-Kind catalog, each with its group (`competitive`/`learning`/`community`/`experience`/`ceremonial`/
`purpose`/`structural`) and the legacy type-name aliases that resolve to it. The 145 legacy taxonomy types
survive as searchable aliases; an event carries a derived `kind_slug`. Additive — the `/v1/categories`
contract and all `/v1/events` response shapes are unchanged.

**Capabilities (Event Architecture V3 §11, Phase 2):** the ~45 event capabilities and how a Kind/event
resolves them. Distinct from *trust capabilities* (organizer permissions, M7) and the *workspace-capabilities*
org-role UI contract (D-116) — both unchanged. `GET /v1/capabilities` (public) lists the registry (slug,
group, `is_universal`, `workspace_tab`, `depends_on`, `available_modes`) — **30 slugs since D-266 M2**, down
from 57: the rest were Registration / Ticketing / Invitation / Scheduling / Eligibility / Finance /
Infrastructure concerns and moved to the subsystems that own them.

`GET /v1/archetypes/{slug}/capabilities` (public) returns what an archetype supports, no event required;
`?mode=` defaults to `Hybrid`, the only value present in every capability's mode list. **This replaces
`GET /v1/kinds/{slug}/capabilities`**, which is gone with the Kind axis. 404 for an unknown archetype.

`GET /v1/orgs/{orgId}/events/{eventId}/capabilities` (authed, reuses the event's view authorization)
returns the event's effective set. `state` is `required` / `on` / `off` / **`locked`** — locked means the
archetype matrix or the product rules forbid it, so a client must not render a toggle for it. `off` means
merely available-and-unchosen; conflating the two is what the retired Kind model could not avoid.

**Resolution performs no authorization.** The engine answers "what does this event support?", never "who
may do this?" — it never consults the caller. Authorization on these routes is ordinary event-view access,
applied before resolution. Full reference: `docs/architecture/CAPABILITY_ENGINE.md`.

**Institutional authorization (D-266 M5).** Evidence that the organization an event *represents* consented
to being represented by it. **Not `IEventAuthority`** (D-269), which decides who may act and stores nothing,
and not a substitute for organization verification (D-044), which says the institution is real rather than
that it agreed to this one event.

| Route | Method | Notes |
|---|---|---|
| `/v1/events/{id}/authorization` | POST | File or replace it. Body: `headName`, `headDesignation`, `officialEmail`, **`officialPhone`** (required, E.164 — `official_phone_invalid`), **`representativeRole`** (required, from the closed list below — `representative_role_invalid`), `representativeRoleOther?` (required when the role is `Other` — `representative_role_other_required`), `representativeUserId?`, `letterheadDocumentKey`, `signatureDocumentKey?`, `supportingDocumentKeys?` (≤10). Document fields are **storage keys** from `/presign`, never bytes and never URLs. `letterhead_required` (400) without a letter; **409 `event_under_review`** while a reviewer holds the event — the same lock a PATCH honours. A resubmission returns the row to `Submitted` and clears the prior verdict — **including one that only changes the signatory**, since a verdict describes the details a reviewer actually read. **D-363 §4:** it also returns an `Approved` event to `PendingReview` — clearing the authorization's own verdict left the *event's* standing, which is the one that gates publication, so an approved event could go live on a decision made about a letter that had since been swapped. |
| `/v1/events/{id}/authorization` | GET | The filed authorization, documents as **presigned URLs**. **204 when none is on file** — the event exists and the caller may see it; a 404 would be indistinguishable from "no such event". Non-manager → 403, stranger → 404 (D-018). |
| `/v1/events/{id}/authorization/presign` | POST | `{ contentType, maxBytes }` → presigned PUT under `events/{eventId}/authorization/…`, so a leaked key is scoped to one event. |
| `/v1/admin/events/{id}/authorization` | GET | Reviewer's read. VerificationReviewer only — that policy *is* the authorization, since a platform reviewer holds no standing on the event. |
| `/v1/admin/events/{id}/authorization/review` | POST | `{ decision, reasonCode?, notes? }` where decision is `approve` \| `reject` \| `request_changes` — the same vocabulary as `/v1/admin/orgs/{orgId}/verification/review`. `reason_code_required` to reject, `notes_required` to request changes. |

**Representative details.** A signatory who cannot be reached is not a verifiable one — contacting the
person who supposedly signed is the reviewer's only check independent of the letter itself — so the phone
is required in E.164 and the role comes from a closed vocabulary:

`Principal`, `Vice Principal`, `Dean`, `HOD`, `Professor`, `Faculty Advisor`, `Event Coordinator`,
`Placement Officer`, `Student Affairs Officer`, `HR Manager`, `Founder`, `CEO`, `Director`, `Secretary`,
`President`, `Club Coordinator`, `Other`.

**`GET /v1/events/authorization/roles`** (authed) serves that list — clients render what the server
validates rather than holding a copy, since a drifted copy offers a role the API then refuses and nothing
on screen says which side is wrong. A test submits every published role and requires each to be accepted,
so the list and the validator cannot diverge.

The list is validated server-side (`RepresentativeRoles.All`) — an unvalidated vocabulary is free text that
merely looks analysable. `Other` is the escape hatch, because a closed list that cannot express a real title
pushes people into picking a wrong one; it must carry `representativeRoleOther`.

**`representativeUserId` is a LINK, never a grant.** Naming a Kurx account lets a reviewer see the signatory
is a known person rather than a name typed into a form. It confers **no** authority over the event and
changes nothing about that account — authority remains `IEventAuthority`'s alone (D-269). The id must
resolve (`representative_user_not_found`); the reviewer's read returns `representative_username` and
`reviewer_name` resolved server-side, both null when the account no longer exists.

A free-mail `officialEmail` is **accepted** — real institutions do run on consumer mail — and flagged to the
reviewer as a prompt to look harder, never as a refusal. That advisory is a console concern, not an API one:
a validator can only refuse, and this is advice.

**Reviewer checklist (D-266 M7).** VerificationReviewer only.

| Route | Method | Notes |
|---|---|---|
| `/v1/admin/events/{id}/review-checklist` | GET | The live checklist merged with **this reviewer's** ticks. `{ event_id, items: [{ key, checked, checked_at, blocking }], is_complete }`. `blocking` marks an item that would refuse the publish on its own rather than merely needing confirmation. |
| `/v1/admin/events/{id}/review-checklist` | PUT | `{ itemKey, checked }`. `unknown_checklist_item` (400) for a key not on the event's current checklist — such a tick would count towards completeness without a real requirement having been read. |

**The items are derived, never stored.** They project `PolicyResolver.ReviewerChecklist` — the same array
the publish blockers come from — so a rule added server-side appears immediately and a reviewer can never
work a list that has drifted from what actually gates the publish. Only ticks are persisted, **per
reviewer**: releasing and reclaiming a queue item must not inherit someone else's sign-off.

**`approve_review` returns `checklist_incomplete` (400) until every item is ticked.** Unticking re-blocks.

**Enriched pending queue.** `GET /v1/admin/events/pending` now carries `status`, `product`,
`archetype_slug`, `is_paid`, `authorization_status` and `city`, so a reviewer can triage without a call per
row. It carries **no `meeting_password`** — `PendingEventView` has no such field, so it is excluded by
construction rather than filtered per call site.

**Who holds an item.** The queue also returns `review_claimed_by`, `review_claimed_by_name` and
`review_claimed_at` — null when unclaimed. **One reviewer holds an item at a time:** `claim_review` records
the holder, and `release_review`/`approve_review`/`reject_review`/`request_changes` refuse with
**`claimed_by_another_reviewer`** for anyone else. An **admin may override**, so a reviewer who goes offline
holding an item cannot strand it. Any decision releases the hold — the verdict is in `verification_reviews`
and a decided event is no longer work in progress. `review_claimed_by_name` is null for accounts that never
completed onboarding; the id is the load-bearing field.

**The publish gate — rewritten by [D-379](../DECISIONS.md), corrected here 2026-08-18.** **Every** event
reports **`event_authorization_required`** in `publish_blockers` and `reviewer_checklist` until an
authorization **exists**, and the transition refuses with **409**.

Two qualifiers this paragraph used to carry are gone, and both were how an event went live unrepresented:

- ~~"A Public event"~~ — the product no longer decides. A Private event represents somebody too;
  visibility never decided who is answerable for an event.
- ~~"until an **approved** authorization exists"~~ — the blocker is keyed on **existence**. The verdict is
  the reviewer's, worked as part of the one event review (the checklist is a projection of this same list,
  so it cannot be skipped) rather than as a prerequisite decision before it.

~~"A self-represented event represents no third party, so the rule does not apply to it."~~ Self-representation
is retired for new events: **`representation_required`** now fires whenever an event does not represent a
real organization, for every archetype — not only the three whose D12 §6 Representation column is *Required*
(Recruitment · Festival · Ceremonial). `PolicyResolver` is the single source for all three violations. Both originate in `PolicyResolver` and reach the gate through
`EventPolicyService` → `PublishBlockers` → `TransitionGateAsync`; there is no second validation.

**Templates & workspace (Event Architecture V3 §13.1/§20, Phase 15):** the inert template scaffold is now the
authoritative **scoped + versioned** Template system. A template is **declarative config only** (`config_json`
carries a capability preset + defaults; it never carries dates/slug/status/inventory/financials) and is validated
against the **closed** capability registry on every write — the single pipeline a future AI generator would reuse
(unknown/disabled capability slugs and invalid config/workspace declarations are rejected, never repaired; `D-183`).
`GET /v1/templates?orgId=&orgUnitId=` (public; auth adds the caller's Personal templates) lists the latest Published
version per family, **most-specific-first** (Personal → Unit → Org → Platform). Authed writes on `/v1/templates`:
`POST /` (scope `Platform`=admin · `Org`/`Unit`=org manager · `Personal`=any user; creates a v1 Draft),
`PATCH /{id}` (Draft only — a Published version is immutable), `POST /{id}/publish`, `POST /{id}/versions` (new
Draft, `version+1`, same family root), `POST /{id}/archive`, `POST /{id}/clone` (new family, into the caller's own
space), `DELETE /{id}` (Draft family never referenced by an event). Creating an event with `templateId` (the family
**root**) **snapshots** the latest Published version: its capability preset is overlaid onto the event's
`event_capabilities`, declarative defaults (timezone) are applied, and `created_from_template_version` is recorded,
so a later template edit never mutates the event. `GET /v1/orgs/{orgId}/events/{eventId}/workspace` (Owner/Manager/
Representative; non-member → 404) returns the **generated** organiser workspace: the event's non-Off capabilities
grouped by `workspace_tab` (via the same resolver, never hand-written per Kind) plus a **read-only publish checklist** —
a projection of the Phase-14 §14.2 validation gates evaluated **without writing** (no approval-request materialisation),
never a second source of truth. Backend only; no wizard/UI this phase. Additive; the public `/v1/templates` list shape
gains fields but keeps `name`/`slug`. **Response codes:** `200` on success; `400` for an invalid scope/name or a
rejected config (`unknown_capability` · `capability_disabled` · `invalid_capability_state` ·
`invalid_workspace_declaration` · `invalid_config_json`) and for an event created from a family with no Published
version (`invalid_template`); `403` `forbidden` when the caller lacks the scope's authority; `404` `not_found` for a
missing template, a template the caller may not view (clone), or a workspace the caller doesn't manage (existence
hidden); `409` for `system_template` · `not_draft` (editing a Published version) · `draft_exists` (an open Draft
already exists, including the concurrent-version race → clean conflict, never a 500) · `template_in_use` (deleting a
family an event snapshotted from).

**Discovery & Search (Event Architecture V3 §15, Phase 16):** the public discovery endpoints are now served by **one
canonical implementation** over an **outbox-fed** index (`event_search_documents`) — Postgres FTS (a weighted, stored
`tsvector`, GIN-indexed) + trigram (`pg_trgm`) for text, typed columns for filters. The index is **never dual-written**:
an event write enqueues a `search.reindex` message in its own transaction and the outbox dispatcher rebuilds (or
removes) the event's document, so discovery is **eventually consistent**. Only Published + Public events are indexed.
**Alias search** — the retired 145 type names are indexed as `kind_aliases`, so "ideathon" finds a Hackathon-kinded
event; **trigram** adds typo tolerance. **Filters:** `kind`, `mode`, `price`, `language`, `city`, `dateFrom/dateTo`,
`orgId`, and proximity (`lat`/`lng`/`radiusKm`). **Ranking** is deterministic — recency (start + creation), velocity
(recent-view stream over a window), conversion (registrations), and proximity (haversine, only when a location is
supplied) — **replacing raw `ViewCount DESC`** everywhere (Trending + the `popular` sort). **Collapse:** a Festival is
**one card** (a sub-event surfaces standalone only on opt-in, `ListedStandalone`); a **RECURRING series is one
listing** (its next-upcoming occurrence). `GET /v1/events/for-you` (authenticated) is the eligibility-aware feed —
events the caller may actually register for; internal events are never indexed, so it can never leak their existence
(404-not-403 preserved). Routes and response DTOs are **unchanged** (new query params are additive/optional); ranking
signals are refreshed asynchronously by the `search-index-refresh` job. Deferred (not §15 ownership): `topics`/`channel`
classification facets (a future §12 phase) and per-user **affinity/personalisation** (a dedicated future phase).

**Audience rules (Event Architecture V3 §4.4, Phase 5):** who may REGISTER for an event, evaluated
server-side — **DENY BY DEFAULT** when a rule exists, open (unchanged) when none does. `GET/PUT/DELETE
/v1/orgs/{orgId}/events/{eventId}/audience` (Owner/Manager/Representative) read/replace/remove the rule
(predicate: `unit_subtree_in`, `role_in`, `cohort_year_in`, `attribute_matches`, `require_verified`,
`external_orgs_allowed`, `applies_to`, `guests_*`). **D-363 §4:** PUT/DELETE are refused with
`event_under_review` (409) while the event is in `PendingReview`/`UnderReview`, and on an `Approved` event
they return it to `PendingReview` — the rule *is* the eligibility a reviewer assessed. A DELETE that
removes nothing (no rule existed) changes nothing and leaves the approval standing. `PATCH /v1/orgs/{orgId}/members/{membershipId}/attributes`
sets a member's `cohort_year`/attributes/source. `GET /v1/events/{eventId}/eligibility` (authed) returns the
caller's `{ allowed, reason }`. The gate runs on **every** ticket-issuing path — order creation, group join /
invitation acceptance, and ticket-transfer claim — all through the one evaluator, so none can bypass it; each
returns **403 `not_eligible`** when denied. It is re-checked at admission — the `/v1/gate/{eventId}/scan`
response gains `eligibility_flag` (the holder is still admitted; the flag is for the organiser). **Enforced
predicates:** `unit_subtree_in`, `role_in`, `cohort_year_in`, `attribute_matches`, `require_verified` (which
also rejects `SelfDeclared` memberships, V3 §4.3), `external_orgs_allowed`, `guests_allowed`. **Stored for a
future phase, not yet enforced:** `applies_to` (team variants → Phase 10), `guest_per_registrant_cap`,
`guests_require_approval`. Additive; no existing field removed. Organiser RBAC is unchanged — eligibility is a
separate registration gate.

**Participants (Event Architecture V3 §5, Phase 6):** the participant model unifying V2's EventAssignment /
capability people-lists / org RBAC. `GET /v1/participant-roles` (public) lists the platform registry (7
hardcoded classes, ~30 slugs, each with `class`, `is_public`, `counts_toward_capacity`, `inventory_segment`,
`default_permissions`). `POST/GET /v1/orgs/{orgId}/events/{eventId}/participants` assign (a Person by phone or
an OrgUnit by id; Team is Phase 10) and list; `DELETE …/participants/{id}` removes; `POST
/v1/participants/{id}/respond` lets the invited subject accept/decline; `GET /v1/me/participations` lists the
caller's. Management authz is the **§5.4 union** — an org Owner/Manager grant OR an *event ORGANISER
participant* grant (`participants:manage`), **event-scoped** and evaluated live; a participant grant never
leaks to org level. **Anti-amplification (§5.4):** assigning a role never confers an *authority* permission
(`participants:manage`/`event:manage`) the actor doesn't already hold — a coordinator can't mint owners/managers
and self-escalate; functional grants (a judge's `scoring:submit`) stay freely appointable. `EventParticipant`
is authoritative for the V3 model + these permissions; the legacy `event_assignments` endpoints are unchanged,
conferred no permissions, and are **replaced-and-retired** (strangler) — backfilled once at startup, not
runtime-synced. `scope` and `visibility` are **stored for a later phase** (Phase 6 grants match the exact
event; no public participant-list read yet). COI (§5.5) is deferred to Phase 11. Additive; no existing route changed.

**Inventory pools (Event Architecture V3 §8, Phase 7):** capacity as a segmented inventory. Phase 7 is an
additive shadow at this phase — one general pool per TicketType. The scalar was the oversell authority at Phase 7;
**pools became authoritative in Phase 9** (below), and every availability read now resolves from the pool.
`GET /v1/orgs/{orgId}/events/{eventId}/inventory` lists the event's pools (`total`/`held`/`allocated`/`consumed`/
`available`); `PATCH …/ticket-types/{ttId}/inventory` sets the general pool's policy (`oversell_allowance`,
`no_show_policy`, `no_show_release_minutes`, `release_policy`, `waitlist_config`). Both reuse the Phase-6
`event:manage` permission union. The waitlist attaches to a pool (§8.5); the reconciliation job proves
`Consumed == count(active admissions)` (Phase 9) and **self-heals** drift. `held`/`allocated` became authoritative
in Phase 9; the release/no-show automation and the non-general segments remain **deferred** (stored config only, not
implemented). Additive; no existing route or response changed.

**Registration layer (Event Architecture V3 §7, Phase 8):** the registration → admission → credential chain +
five-axis `RegistrationPolicy`. At Phase 8 this was an additive **shadow** of the then-authoritative Order/Ticket,
projected — **post-commit, never inline** (§17.1), on its own DbContext (isolated from the request) — a
Registration (the act), an Admission per ticket (the right, linked to the Phase-7 pool), and one Credential per
person per event **tree** (the artifact, keyed on the tree root, §9.3). `GET/PATCH
/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/registration-policy` reads/sets the five axes (subject,
gates, identity_requirement, allocation, payment, windows); `GET /v1/orgs/{orgId}/events/{eventId}/registrations`
lists the shadow. Both reuse the Phase-6 `event:manage` union. A daily reconciliation job proves registrations ==
orders, active admissions == active tickets, and every live admission carries a credential — and **self-heals**
drift by re-projecting the affected orders. **Order/Ticket were authoritative at this phase; the cut-over landed in
Phase 9** (below), where the chain became authoritative and is produced in the money transaction.
Subject is Person (a party booking is one Registration with N Admissions); TEAM/ORG_UNIT and the gate/allocation
values needing later subsystems (LOTTERY, PREREQUISITE, DELEGATED, walk-in) are **stored-but-not-enforced**.

**Authority cut-over (Event Architecture V3 §9/§17.1, Phase 9 — Option A):** the new model is now **authoritative**.
Inventory pools own oversell via a **conditional decrement** (never `Sold += 1`); the registration → admission →
credential chain is produced **in the money transaction** (§6.1), and each order also writes a `Pass` +
`AdmissionRight(SINGLE)` (§9.2) and an immutable **VAR** (§9.5, the sole refund basis). `POST /v1/events/{id}/orders`
now honours a standard **`Idempotency-Key`** header, **scoped per caller** (authenticated user, or guest phone) —
a retried create returns that caller's own original order, never another caller's. §17.1 concurrency is enforced
end-to-end: no oversell, reserve-first holds, ascending pool-id lock order, duplicate-callback + full-refund
idempotency (atomic status claims), per-person credential advisory lock, and outbox-delivered side effects. **All
availability reads (public/org ticket-type availability, the waitlist sold-out gate, cap/sold/available) resolve
from the authoritative pool**, never the legacy `TicketType.Sold` mirror. A late capture after hold expiry consumes
unconditionally so a paid ticket is always honoured; the reconciliation job self-heals inventory drift. Order/Ticket/
`TicketType.Sold` remain as written legacy mirrors (removal is a later phase). Reconciliation is `Consumed ==
count(active admissions)`.
Multi-scope Passes (Subtree/Set/Query) and the later-wave subsystems remain deferred. Additive; no existing route
or response shape changed.

**Structure & Series (Event Architecture V3 §3 / §13.2, Phase 12):** the structural model + EventSeries, added
**additively**. **Composition depth ≤ 3** (§3.4 rule 1) is enforced on `POST /v1/events` when
`parentEventId` is set (→ `max_composition_depth`). **Structural discovery** (§3.4 rule 2): public discovery feeds
list roots and editions; a sub-event is excluded until opted in via `listedStandalone` on `PATCH …/events/{id}` (direct
access by slug/id is unaffected). **AgendaItems** (`event_sessions`) accept an optional `inventoryPoolId` on schedule
create/update (§3.4 rule 3; the pool must belong to the event). **EventSeries** `/v1/…` surface: `POST/GET
/v1/orgs/{orgId}/series`, `GET/PATCH/DELETE /v1/series/{seriesId}` (GET public), `POST /v1/series/{seriesId}/events`
(attach a member — ordinal/label for EDITIONS) + `DELETE /v1/series/{seriesId}/events/{eventId}`, `GET
/v1/series/{seriesId}/events` (member list — **visibility-gated**: a manager of the series' org sees all members,
everyone else sees only `Published` + `Public` editions, so Draft/Private/Unlisted never leak),
`POST/DELETE /v1/series/{seriesId}/follow`. An event belongs to
≤1 series (§3.4 rule 5, → `already_in_series`); RECURRING series validate an RFC-5545 rrule; organiser gates reuse the
org role (Owner/Manager/Representative). Additive; no existing route or response shape changed.

**Delegated & walk-in registration (Event Architecture V3 §7.5/§7.6, Phase 13):** additive over the money path — all
minting reuses the authoritative Order→Ticket projection. **Walk-in:** `POST /v1/events/{id}/walk-ins` — a staff
participant (Operations class or `event:manage`) registers an attendee at the gate against the `WalkIn`-segment pool,
producing Registration+Admission+Credential in one transaction; **offline replay is database-enforced** — a
staff-scoped `idempotencyKey` plus a partial-unique index covering the NONE-identity case means a replayed queued
walk-in (even fired concurrently) returns the original order, never a duplicate registration or double consume.
**SeatBlock / delegate console:** `POST/GET /v1/events/{id}/seat-blocks`
(organiser creates/lists a block reserving N unassigned admissions for an org unit; FREE|DEFERRED payment, `payerId` +
`delegateUserId`), `GET /v1/seat-blocks/{id}`, `GET /v1/seat-blocks/{id}/seats`, `GET /v1/seat-blocks/{id}/status`
(aggregate + incomplete list), `POST /v1/seat-blocks/seats/{seatId}/assign` (bind a person; the block's delegate /
organiser / admin) + `POST …/unassign`. Reassignment is bounded by the block's `assignmentDeadline` + `reassignLimit`
(→ `assignment_deadline_passed` / `reassign_limit_reached`) and audited. No live payment collection this phase.
Additive; no existing route or response shape changed. **D-369:** both creates refuse a
`RegistrationMode.Group` ticket type with `group_ticket_not_supported` — neither channel is told a team
size, so neither can resolve a D-366 price band, and both mint individual seats a team cannot occupy.

**Lifecycle, gates & approvals (Event Architecture V3 §14, Phase 14):** additive over the existing lifecycle —
`Published` stays the authoritative registration-open state. The existing `POST /v1/orgs/{orgId}/events/{id}/transition`
gains the new actions `schedule` (→ Scheduled), `open_registration` (→ Published), `go_live` (→ Live), `complete`
(→ Completed), each governed by a §14.2 gate (→ `missing_venue_or_url` / `missing_owner_unit` / `approval_pending` /
`no_pass` / `no_inventory_pool` / `no_currency` / `no_staff_assigned` / `results_not_published`); the existing `publish`
is preserved and gains only the approval gate (a no-op when no chain applies). **Approval chains:** `POST/GET
/v1/org-units/{orgUnitId}/approval-chains` (org Owner/Manager; steps with approver role|user + condition + SLA/escalation
metadata; SEQUENTIAL|PARALLEL), `PATCH/DELETE /v1/approval-chains/{chainId}`, `GET /v1/events/{eventId}/approval` (the
event's request + step decisions), `POST /v1/approvals/decisions/{decisionId}` (`approve`|`reject`|`bypass`; a decision
needs the step's approver or admin, bypass needs an org Owner/admin and is always audited), and `POST
/v1/events/{eventId}/approval/resubmit` (org Owner/Manager — recover a rejected request into a fresh cycle after fixing
the event; prior decisions preserved in the audit spine). Chains are inherited down the OrgUnit tree and gate publishing;
approval completeness is re-evaluated on every gate check, so a condition that becomes true after the request exists
(e.g. a free event becoming paid) re-opens the request. **Material change (§14.5):** editing an event's date/venue/mode
after any registration exists — or cancelling a sub-event — automatically opens a refund window (`refundWindowEndsAt`,
never shortened once open), notifies registrants, and audits before/after; refunds stay registrant-initiated via the
existing refund path. Additive; no existing route or response shape changed.

**Teams (Event Architecture V3 §6, Phase 10):** the Team subsystem — the only group entity, and only where
competition exists — added **additively**. The purchase `Group` flow and the Phase-9 money path are untouched (Group
is retained as a legacy mirror); this phase is **formation only** (team-slot purchase, §6.5, is a later phase).
Formation: `POST /v1/events/{id}/teams?ticketTypeId=…` (create — creator becomes captain), `GET
/v1/events/{id}/teams`, `GET/PATCH /v1/teams/{teamId}`, `POST /v1/teams/{teamId}/invites` +
`POST /v1/teams/invites/{token}/accept` + `DELETE /v1/teams/invites/{inviteId}`, `POST
/v1/teams/{teamId}/join-requests` + `POST /v1/teams/join-requests/{id}/decide`, `POST /v1/teams/{teamId}/leave`,
`DELETE /v1/teams/{teamId}/members/{membershipId}`, `POST /v1/teams/{teamId}/substitute` (an edge, not a delete),
`GET /v1/me/teams`. Organiser: `POST /v1/teams/{teamId}/transition` (`lock`/`compete`/`disqualify` [reason]/
`withdraw`/`eliminate`/`finalist` — audited), `POST /v1/events/{id}/teams/merge`, `POST /v1/teams/{teamId}/split`
(both organiser-only, blocked once a team is Competing **or once any Stage scoring has begun for it**, §6.4); `GET/PATCH
/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/team-policy` (§6.3, one per competition ticket type).
Captain/co-captain and organiser (`event:manage`) gates enforced in the service. Additive; no existing route or
response shape changed.

**Competition (Event Architecture V3 §10, Phase 11):** the Stage · Fixture · ScoringPolicy · Result engine — added
**additively** (the money path, InventoryPool authority, Registration→Admission, Pass, VAR and §17.1 concurrency are
untouched). Stages: `POST /v1/events/{id}/stages`, `GET /v1/events/{id}/stages`, `GET/PATCH /v1/stages/{stageId}`,
`POST /v1/stages/{stageId}/transition` (`open`/`close`), `DELETE /v1/stages/{stageId}`. Roster: `GET/POST
/v1/stages/{stageId}/participants`, `POST /v1/stages/{stageId}/participants/seed-registered` (competition teams),
`DELETE /v1/stages/participants/{rowId}`. Scoring policy: `POST /v1/events/{id}/scoring-policies`, `GET
/v1/events/{id}/scoring-policies`, `PATCH /v1/scoring-policies/{policyId}`. Fixtures (manual scheduling + conflict
detection): `POST /v1/stages/{stageId}/fixtures`, `GET /v1/stages/{stageId}/fixtures`, `POST
/v1/fixtures/{fixtureId}/state`. Judging + voting: `POST /v1/stages/{stageId}/scores` (Evaluation-class judge; COI +
one-per-subject upsert + Live window), `POST /v1/stages/{stageId}/votes` (authenticated; **one immutable vote per
identity** + rate limit + Live window). Results + advancement: `POST /v1/stages/{stageId}/results/compute`
(deterministic aggregation) + `.../results/publish`, `GET /v1/stages/{stageId}/results` (**anonymous-readable,
visibility-gated** — public sees `Published`/`Corrected` per `results_visibility`), `POST /v1/results/{resultId}/
dispute` + `.../correct` (append-only), `POST /v1/stages/{stageId}/advance` (seeds the next stage from the published
set). Organiser (`event:manage`) gates enforced in the service. Roster and result rows carry
`subjectType` + `subjectId` + **`subjectName`** — the display name is resolved server-side (person name, else
`@username`; team name; `(unknown)` for a deleted subject) because a client cannot resolve it: there is no
by-id person lookup on the admin surface. Resolution is batched (two queries per response regardless of roster
size), and `subjectId` is retained for traceability. Note these competition endpoints return records directly,
so their JSON is **camelCase**, unlike the snake_case `/v1/admin/*` responses. Spectator admission (§10.4) is config-only this phase
(`Stage.spectator_pool_id` links an `InventoryPool`; the purchase flow is deferred). Additive; no existing route or
response shape changed.

## Wallet & payouts (`/v1/orgs/{orgId}/wallet`, Owner/Finance only)

Financial access restricted to `OrgRole.Owner` and `OrgRole.Finance` (D-015/D-028). No other role can reach these endpoints — `forbidden` otherwise.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/orgs/{orgId}/wallet` | GET | Cached balance view: `collected_paise`, `available_paise`, `advanced_paise`, `reserved_paise`, `settled_paise`, `lifetime_earned_paise`, `lifetime_withdrawn_paise`. |
| `/v1/orgs/{orgId}/wallet/ledger` | GET | Paginated `ledger_entries` for the org, newest-first. Query params: `page`, `pageSize` (max 100). |
| `/v1/orgs/{orgId}/wallet/withdraw` | POST `{ amountPaise }` | Initiates a withdrawal from `available_paise`. Bank account must be on file via penny-drop KYC (D-016). Returns withdrawal `id`. Errors: `insufficient_balance`, `wallet_not_found`. |

## Refunds (`/v1/orders/{orderId}/refund`, `/v1/refunds`, `/v1/admin/refunds`) — D-103/D-199

The HTTP surface over `IRefundService` (D-103's reverse-ledger refund engine — appends a Refunded ledger entry,
debits the wallet, voids the order's tickets, all in one transaction). Full-refund only; replay-safe (a second
refund of an already-refunded order returns `outcome: "already_refunded"`, not an error). Authorization mirrors
Wallet's financial-access bar: `OrgRole.Owner`/`OrgRole.Finance` of the order's event's org, **or** a platform
`FinanceOps`/`SuperAdmin`.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/orders/{orderId}/refund` | POST `{ reason }` | Refunds a Paid order in full. `forbidden` for anyone else — including the order's own buyer, who cannot self-initiate. `not_found` for an unknown order; `invalid_order_state` (400) for one that exists but isn't currently Paid. |
| `/v1/orders/{orderId}/refund` | GET | The refund for one order, if any. Visible to the order's own buyer (read-only), the org's Owner/Finance, or FinanceOps/SuperAdmin. `not_found` for a missing refund, an unrelated caller, **or** an unknown order — D-018 anti-enumeration: the three are indistinguishable, never a `forbidden`. |
| `/v1/refunds` | GET | The caller's own refunds, across every event (mirrors `GET /v1/orders`'s "my tickets"). |
| `/v1/admin/refunds` | GET | Platform-wide refund list. `RequireAuthorization("FinanceOps")` — SuperAdmin or FinanceOps only. Query params: `status` (`initiated`/`processed`/`failed`), `limit`, `page`. |

## Organization bank verification (`/v1/orgs/{orgId}/kyc`, Owner/Finance only) — D-016, renamed M9/D-048

**Org *bank* verification** (persisted in `org_bank_verifications`, formerly `kyc_records`, renamed in M9/D-048) — distinct from **person identity** verification (`/v1/me/identity`, below). Full account number and PAN are **never persisted** — only last-4 in `payload_json` (D-016). The dead standalone `KycEndpoints.cs` was deleted in M9; these routes live in `OrgEndpoints`.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/orgs/{orgId}/kyc` | GET | All bank-verification records for the org, newest-first. `[{ kind, status, approved_at?, rejection_reason?, updated_at }]`. |
| `/v1/orgs/{orgId}/kyc/bank` | POST `{ accountNumber, ifsc, holderName }` | Penny-drop bank verification. Stores `account_last4` + IFSC only. Activates Razorpay Route linked account on approval (D-016). |
| `/v1/orgs/{orgId}/kyc/pan` | POST `{ pan, name }` | PAN match check. Stores `pan_last4` + name only. |

## Person identity verification (`/v1/me/identity`, authenticated) — M3, D-042

Person KYC, distinct from an org's bank verification above (D-048). Graduated ID ladder (Phone → Contact → GovernmentId → Bank → Liveness); **only masked last-4 values are ever stored** — full government-ID / PAN / account numbers are never persisted. Backed by `IKycProvider` (mock in dev; real DigiLocker adapter is a gated later phase). Every decision appends a `verification_reviews` row. Capability gates read the `*_last4` presence flags, not the display `level`.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me/identity` | GET | Caller's identity-verification summary (level, status, which last-4s are on file). Also surfaced on `GET /v1/me`. |
| `/v1/me/identity/government-id` | POST | Submit a government ID (DigiLocker / Aadhaar-offline / passport / DL). Stores `govt_id_last4` only. |
| `/v1/me/identity/pan` | POST | PAN match. Stores `pan_last4` only. |
| `/v1/me/identity/bank` | POST | Personal bank penny-drop (individual payouts). Stores `bank_last4` only. |

## Trust & capabilities (M7, D-046)

Trust is a **live capability matrix**, not a stored score. Capability flags (`CanOrganizeFree`, `CanOrganizePaid`, `CanReceivePayout`, `CanRepresentOrg`, `IsOrgVerified`, …) are derived **per request** from identity + org + membership + fraud state; L0–L5 are display labels over them. Nothing is baked into the JWT, so a suspension/revocation is effective on the next request.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me` | GET | Now also carries the caller's identity summary + personal capability flags. |
| `/v1/orgs/{orgId}/my-capabilities` | GET | The caller's capabilities relative to that org (e.g. may this user organize a paid event for this org). |
| `/v1/orgs/{orgId}/workspace-capabilities` | GET | The permission-gated workspace contract (`representation` / `trust` / `permissions` / `workspaces`), derived live. **D-268:** the `organization` block became **`representation`** — `kind: "personal" \| "organization"`, `organization_id`, `name`, `verified`, `verification_status`, `represented_as`, `authority`. `kind: "personal"` sends nulls for the organization fields: representing yourself is not an organization, so none is named. `role` was renamed `authority` — a role over an event would imply ownership, which representation never confers. |

## Organization registry & search (M4, D-043)

`Organization` is typed and canonical — `type`, `legal_name`, `primary_domain`, `canonical_org_id`, `normalized_name` — with `organization_aliases`. Hard dedup on domain; soft (fuzzy, pg_trgm) dedup on name, enforced hard at verification (M5).

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/orgs/search` | GET `?q=` | Fuzzy org search (pg_trgm) across name + aliases — call before creating an org to avoid duplicates. |

## Membership claims (M6, D-045)

An evidence-backed, role-typed claim that a user represents an org. Approval grants a **verified, read-only Staff seat** (organizer rights remain an org-Owner action). The immediate `OrgService.AddMemberAsync` add is unchanged and coexists. Bio is never evidence — the claim carries an explicit `verification_documents` row.

| Endpoint | Method | Who | Notes |
|---|---|---|---|
| `/v1/orgs/{orgId}/membership-claims` | POST | any user | File a claim (role + evidence) to represent an org. An email-domain match is a fast-track hint. |
| `/v1/me/membership-claims` | GET | any user | The caller's own claims + status. |
| `/v1/admin/membership-claims/pending` | GET | VerificationReviewer | Review queue. |
| `/v1/admin/membership-claims/{claimId}/review` | POST | VerificationReviewer | Approve / reject / request changes. |

## Organization verification & admin console (M5 / M12, D-044 / D-051) — VerificationReviewer only

Org verification lifecycle: `Unverified → PendingReview → Verified / Rejected / Suspended / Blacklisted`. All admin endpoints require the `VerificationReviewer` platform role (read live per request, D-040) and are audited via `verification_reviews`.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/orgs` | GET | Paginated, searchable (name/slug/domain) org list, filterable by verification status and org type (D-194). **The only surface carrying `is_personal`** — staff must be able to tell a self-representation persistence row apart from an institution awaiting verification. It appears nowhere user-facing: there is no personal-organization concept in the domain (D-268). |
| `/v1/admin/orgs/{orgId}` | GET | Org detail, bypassing the D-018 membership check (`role` reports as `"admin"`) (D-194). |
| `/v1/admin/orgs/pending` | GET | Orgs awaiting verification review. |
| `/v1/admin/orgs/{orgId}/verification/review` | POST | Approve / reject / request-changes (hard name-dedup enforced on approve). |
| `/v1/admin/orgs/{orgId}/verification/suspend` | POST | Suspend a verified org. |
| `/v1/admin/orgs/{orgId}/verification/blacklist` | POST | Blacklist an org. |
| `/v1/admin/orgs/merge` | POST | Merge a fresh duplicate org into its canonical record. |
| `/v1/admin/verifications/{subjectType}/{subjectId}/history` | GET | Cross-subject verification audit trail (`UserIdentity` / `Organization` / `Membership` / `Event`). |

## Fraud prevention (M13, D-052) — VerificationReviewer only

`blacklist_entries` (hard blocks on normalized phone/email/etc.) + `fraud_signals` (polymorphic risk). A blacklisted or high-risk (score ≥ 100) user loses `CanOrganizePaid`, which cascades into the event-approval (M8) and paid-checkout (M10) gates. Blacklisted org names are rejected at creation (`org_blacklisted`, 403). Automated signal producers (device/velocity/dup-account) are a follow-up.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/blacklist` | POST / GET | Add / list hard blocks (unique `kind`+`value`). |
| `/v1/admin/blacklist/{id}` | DELETE | Remove a block. |
| `/v1/admin/fraud-signals` | POST | Record a polymorphic risk signal. |

## Staff & role management (admin console, D-056) — SuperAdmin only

"Staff" == holding a platform role (`platform_roles`, M2/D-040), read live per request. All routes require the `SuperAdmin` policy.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/staff` | GET | Everyone with ≥1 active platform role, one row per user (`user_id, name, phone, username, roles[]`). Filters: `?q=` (name/phone/username), `?role=`. |
| `/v1/admin/staff/grant` | POST `{ phone, role, expiresAt? }` | Grant a role to an **existing** user (resolved by normalized phone). `invalid_role`, `phone_required`, `user_not_found` (target must have signed in once — no provisioning). Idempotent upsert. |
| `/v1/admin/staff/{userId}/roles/{role}` | DELETE | Revoke one role. `cannot_revoke_last_superadmin` (409) guards against total lockout; `invalid_role`. |

`GET /v1/me` additionally returns `platform_roles: string[]` (the caller's live active roles) — the admin console derives its whole RBAC from this. `is_platform_reviewer` is retained (now `SuperAdmin`-inclusive).

## Event approval queue (admin console, D-057) — VerificationReviewer only

Completes the M8/D-047 flow: paid events sit in the review queue (`PendingReview`/`UnderReview`, D-266 M4)
until a reviewer publishes them.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/events/pending` | GET | Events awaiting review across all orgs — D-266 M4's `PendingReview` **and** `UnderReview`, so a claimed item stays visible — oldest-first: `[{ event_id, representing_org_id, org_id, org_name, title, slug, starts_at, created_at, review_claimed_by, review_claimed_by_name, review_claimed_at }]`. `org_id` is a deprecated duplicate of `representing_org_id` (D-273a). `?limit=` (default 50, max 200). |

**Approve / reject reuse the org-scoped transition** — `POST /v1/orgs/{orgId}/events/{eventId}/transition` with `action: "publish"` (approve) or `"reject"`; a reviewer may drive any event whatever it represents, and the trust gate is re-checked live on publish. No dedicated admin action endpoint.

## Dashboard summary (admin console, D-058) — any platform staff

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/dashboard/summary` | GET | Live counts for the landing page: `{ pending_org_verifications, pending_membership_claims, pending_events, blacklist_entries, staff_count, new_users_24h, total_users, total_orgs, total_events }`. Requires any platform role (403 otherwise) — non-PII aggregates, so not reviewer-only. Reuses the verification/claim/event queues' pending definitions, so a tile always agrees with the queue it links to: `pending_events` counts `PendingReview + UnderReview`, matching `/v1/admin/events/pending`. |

## Content moderation reports (C-5, D-059)

| Endpoint | Method | Who | Notes |
|---|---|---|---|
| `/v1/reports` | POST | any authenticated user | File a report: `{ entityType, entityId, reason, details? }`. `entityType` ∈ {event, review, chat_message, user, org, **post**, **post_comment**}. Errors: `invalid_entity_type`, `reason_required`, `already_reported` (409 — one open report per reporter+subject). Posts reuse this queue rather than owning a second one (D-262). |
| `/v1/admin/reports` | GET | Moderation (SuperAdmin/VerificationReviewer/Support) | Triage queue; `?status=open\|resolved\|dismissed`, `?limit=`. |
| `/v1/admin/reports/{id}/resolve` | POST | Moderation | Close as actioned. `already_closed` (409) if terminal. Audit-logged. |
| `/v1/admin/reports/{id}/dismiss` | POST | Moderation | Close as no-action. Audit-logged. |

## User administration & moderation (admin console, D-060) — Moderation staff

Suspending or banning blocks the account at authentication: it can't verify an OTP or refresh, and existing sessions are revoked. Also surfaces on login as `account_suspended` / `account_banned` (401).

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/users` | GET | Search users: `?q=` (phone/name/username). **Empty `q` returns only currently-suspended/banned accounts** (no full-table dump). Returns PII (phone/email) — Moderation staff only. |
| `/v1/admin/users/{id}/{suspend\|ban\|unban}` | POST `{ reason? }` | Set/clear a moderation hold. `unban` clears both. Errors: `cannot_moderate_self`, `cannot_moderate_superadmin` (409), `invalid_action`, `not_found`. Audit-logged. |

## Global event management (admin console, D-061, D-186/187/191) — VerificationReviewer only

**Moderation is orthogonal to content** (D-186): `IsSuspended`/`IsHidden` are booleans layered over the
event, never a content edit — a Reviewer/SuperAdmin token cannot `PATCH` an event's own fields (D-191
removed that bypass; only a real per-org Owner/Manager role can, or SuperAdmin via Emergency Edit below).

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/events` | GET | Real, paginated search across every status: `?q=`, `?status=`, `?categoryId=`, `?city=`, `?visibility=`, `?isPaid=`, `?verifiedOnly=`, `?dateFrom=`/`?dateTo=`, `?revenueMin=`/`?revenueMax=`, `?registrationsMin=`/`?registrationsMax=`, `?sort=` (`revenue`\|`registrations`\|`attendance`\|`updated`\|`date`, default recency), `?limit=` (default 50, capped 50), `?page=` (default 1). Returns `{ items: [...], total }` — `items[]` carries `tickets_sold`/`checked_in`/`registrations_count`/`revenue_paise` per row, sourced from `IAnalyticsFactSource` (Phase 17, D-192), the same numbers the org-facing event list shows. **D-381 — creator and representation are separate facts on every row:** `creator_id` / `creator_name` are the USER who owns the event (D-268), `org_name` / `org_verification` are the organization it REPRESENTS, and `org_is_personal` marks a pre-D-379 self-representation row so a console can name it legacy instead of printing a person's name as an organization. All three are additive with defaults, so a client that predates them binds unchanged. `creator_name` is nullable in the contract (the FK is `ON DELETE RESTRICT` and deletion anonymises rather than removes, D-263) — a console renders the empty case rather than falling back to the organization. |
| `/v1/admin/events/{id}/{feature\|unfeature}` | POST | Toggle the public `is_featured` flag. |
| `/v1/admin/events/{id}/{suspend\|hide}` | POST `{ reason }` | Moderation override; `reason` **required** (D-193 — was silently optional before). Audit-logged. |
| `/v1/admin/events/{id}/{unsuspend\|unhide}` | POST | Clears the override. No reason field. |
| `/v1/admin/events/{id}/{message\|warn}` | POST `{ message }` | Sends a platform notification to the event's creator (`EventService.NotifyOrganizerAsync`) — composition only, no new entity. |
| `/v1/admin/events/bulk` | POST `{ eventIds: uuid[], action, reason? }` | `action` ∈ `suspend\|unsuspend\|hide\|unhide\|approve\|reject\|archive`; loops the **same** single-event service methods above (no parallel moderation logic) — `approve`/`reject`/`archive` call the org-scoped `transition` action as a reviewer. `reason` required only when `action` is `suspend`/`hide` (D-193, matching the single-event policy exactly). Capped at 200 ids. Returns `{ succeeded: uuid[], failed: [{ event_id, error }] }` — a partial failure never silently swallows the rest. |
| `/v1/admin/events/export` | GET | Same filter params as the list route above, plus `?eventIds=` (comma-joined, "export selected"). Streams a `text/csv` file of the filtered rows — reuses the list route's own filter/query, not a second implementation. |
| `/v1/admin/events/{id}/emergency-edit` | POST `{ reason, input: UpdateEventInput }` | **SuperAdmin only** (distinct policy from the rest of this group). The one exception to "admins don't edit content": reuses the exact same update core the organizer's own `PATCH` uses (`EventService.ApplyUpdateAsync`), gated + audited + `reason` mandatory. Writes a before/after row snapshot to the audit log. |

Force publish/unpublish reuse the org-scoped transition (`POST /v1/orgs/{orgId}/events/{eventId}/transition`).

## Audit log (admin console, D-062) — SuperAdmin / ReadOnlyAuditor

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/audit` | GET | Newest-first `audit_log` rows: `?actor=` (uuid), `?entity=` (e.g. `users`), `?entityId=` (uuid — D-191, e.g. one event's full moderation/emergency-edit history), `?action=` (substring, e.g. `user.ban`), `?limit=`. Returns `[{ id, actor_type, actor_id, action, entity, entity_id, details, created_at }]`. |

## Organizer analytics (Owner/Manager/Finance) — V3 §16, Phase 17 rebuild (D-192)

Every number below is computed **at request time** from leaf-fact tables (`ValueAllocationRecord`,
`Registration`, `Ticket`, `EventView`, `Refund`) through one seam, `IAnalyticsFactSource` — there is no
pre-aggregated rollup table and no nightly job; nothing here can go stale between requests. Revenue is
**gross** (VAR rows are never negated by a refund — `RefundCount` is the separate signal a refund
happened, not a deduction).

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/orgs/{orgId}/analytics` | GET | `?includeDescendantUnits=` (bool, default false) — dual-tree rollup: `false` scopes to the org's root `OrgUnit` only, `true` widens to every descendant unit via the materialised `OrgUnit.Path`. Returns `{ revenuePaise, views, uniqueVisitors, registrations }`. |
| `/v1/orgs/{orgId}/events/{eventId}/analytics/sales` | GET | Day-bucketed `[{ date, ticketCount, revenuePaise }]`. |
| `/v1/orgs/{orgId}/events/{eventId}/analytics/attendance` | GET | `{ totalTickets, checkedIn, attendanceRate }`. |
| `/v1/orgs/{orgId}/events/{eventId}/analytics/revenue` | GET | Per-ticket-type breakdown: `[{ ticketTypeId, name, quantitySold, revenuePaise }]` — VAR-attributed, never sold-count × current price. |
| `/v1/orgs/{orgId}/events/{eventId}/analytics/tickets` | GET | `{ totalCapacity, totalSold, totalAvailable }` from the authoritative inventory pools. |
| `/v1/orgs/{orgId}/events/{eventId}/analytics/export` | GET | `text/csv`: `Date,Views,UniqueVisitors,Registrations,PaidRegistrations,FreeRegistrations,RevenuePaise,CheckIns,WaitlistCount,RefundCount`. |

## Platform analytics (admin console, D-063) — any platform staff

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/admin/analytics` | GET | Growth aggregates over the last `?days=` (default 30, max 365): `{ total_users, total_orgs, total_events, new_users_in_window, signups_by_day[], events_by_day[], events_by_status[], top_organizers[], top_events[] }`. `top_events[]` reads the `EventView` leaf-fact table (Phase 17), not the frozen `events.view_count` column. Requires any platform role (403 otherwise). **No revenue metrics** — money doesn't move yet; those arrive with Finance. |

## Event approval & payment readiness (M8, D-047)

Free events publish directly; a **paid** event (any priced ticket type) cannot self-publish — it goes through `submit_review` (gated on organizer `CanOrganizePaid` + org `Verified`) and a platform reviewer publishes. Whether money can actually be taken is computed **live**, never stored, and reused by paid checkout at charge time.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/orgs/{orgId}/events/{eventId}/clone` | POST `{ title? }` | Owner/Manager/Representative | **(D-101)** Duplicates the event as a fresh `Draft` (new id/slug/short-code; title defaults to `"… (Copy)"`). Copies ticket types (**`Sold` reset to 0**), form fields, media, speakers, sponsors, sessions and tags; never copies tickets/orders/attendees/analytics or `PublishedAt`/`ViewCount`/`IsFeatured`. |
| `/v1/orgs/{orgId}/events/{eventId}/payment-readiness` | GET | Live check: Published + ≥1 priced ticket type + organizer `CanOrganizePaid` + org `Verified`. |

## Orders & groups (`/v1/events/{eventId}/orders`) — D-021, D-036, D-049

Both **free and paid** checkout run on one `Order`/`Payment`/`Refund` model.
- **Free** (D-021): individual/group registration issues tickets immediately.
- **Paid** (D-049, M10): a priced ticket type creates a `Pending` order plus a gateway order
  (`RazorpayOrderId`) and holds inventory for 10 minutes (a `SeatHold`, reclaimed by
  `ExpireSeatHoldsJob` if payment never captures). Tickets issue **only on capture**, when the Razorpay
  webhook (`POST /v1/webhooks/razorpay`, signature-verified) calls `IOrderService.ConfirmPaymentAsync`
  — idempotent: it sets `Order.Status = Paid`, writes the `Payment` row, issues the ticket, appends a
  **Collected** `ledger_entries` row and updates the cached `organization_wallet`. The **payment gate is
  read LIVE at order creation** (M8): the event must be Published, its organizer must have
  `CanOrganizePaid`, and its org must be `Verified`, else `payments_not_enabled` (see
  `GET .../payment-readiness` above). Paid **group** tickets aren't supported yet
  (`paid_group_not_supported_yet`). In dev the gateway is `MockPaymentGateway` (real Razorpay adapter is
  a gated later phase — `MockPaymentGateway.VerifyWebhookSignature` returns `true`, so capture is
  simulated by posting the minimal `{ order_id, payment_id }` webhook body).

Group registration reserves capacity incrementally: the leader takes one slot at creation, each
`POST /v1/groups/join` call takes one more up to the group's target size, then `group_full`.

The order payload carries **`currency`** (ISO-4217) alongside `amount_paise` — the event's settlement
currency (V3 §9.1). Clients must render with it rather than assuming INR; every surface previously
hardcoded ₹ because the field was stored but never sent (D-257).

`GET /v1/orders` also denormalises **`event_title`**, **`event_slug`** and **`ticket_type`** so a list
renders without a second round trip, and accepts optional **`page`** / **`pageSize`** (max 200, which is
also the default — see D-245). `GET /v1/groups` takes the same two parameters. Both still return a bare
JSON array; the cap bounds the response rather than driving a pager.

**Count-based limits are enforced inside the order transaction** under transaction-scoped advisory
locks (D-242): `PerUserLimit` (keyed on buyer + ticket type) and a group's target size (keyed on the
group). Neither is expressible as a unique index, so concurrent requests are serialised rather than
backstopped by a constraint — two simultaneous orders from one buyer, or two simultaneous joins into
the last slot of a group, now yield exactly one success and one `limit_exceeded` / `group_full`.

**Guest checkout (D-036)**: `POST /v1/events/{eventId}/orders` is *not* behind auth — a free,
non-competition (`TicketType.IsCompetition == false`), `Individual`-mode ticket type may be purchased with
no Bearer token by supplying `guestName`/`guestPhone`/`guestEmail?` instead. The response then includes a
`guest_access_token`, the only way to later reach `GET/POST /v1/orders/guest/{accessToken}` (view / resend).
`account_required` is returned for competition or priced ticket types; `guest_group_not_supported` for
`Group`-mode ticket types (guest checkout is Individual-mode only in this pass).

| Endpoint | Method | Auth | Notes |
|---|---|---|---|
| `/v1/events/{eventId}/orders` | POST `{ ticketTypeId, groupSize?, displayName?, answers?, guestName?, guestPhone?, guestEmail? }` | optional | Create an order — free tickets issue immediately; a paid order returns a `Pending` order + gateway order id and issues on capture. Errors: `not_found`, `not_on_sale`, `payments_not_enabled`, `paid_group_not_supported_yet`, `account_required`, `guest_group_not_supported`, `guest_contact_required`, `invalid_group_size`, `missing_required_field`, `limit_exceeded`, `sold_out`. **D-366:** on a ticket priced by team-size bands, `groupSize` selects the band and that band's price is what is charged — never multiplied by the roster. `no_price_for_team_size` (400) when no band covers the size; **`ambiguous_price_rule` (409)** when two do — a configuration error the sale is refused on rather than picking one of the two prices. |
| `/v1/orders/guest/{accessToken}` | GET | none (possession of token) | View a guest order. |
| `/v1/orders/guest/{accessToken}/resend` | POST | none (possession of token) | Re-send a guest order's ticket(s). |
| `/v1/groups/join` | POST `{ joinCode, displayName?, answers? }` | required | Join a group using a `join_code`. Rejected with `competition_requires_invitation` for `IsCompetition` ticket types. Errors: `invalid_join_code`, `already_joined`, `group_full`, `limit_exceeded`, `sold_out`. |
| `/v1/groups/invitations/{token}/accept` | POST `{ answers? }` | required | Accept a named competition-team invitation (the only way to join an `IsCompetition` group). Errors: `not_found`, `already_accepted`, `invitation_declined`, `phone_mismatch` (caller's account phone must match the invite's, when one was set), plus the same capacity/eligibility errors as `/groups/join`. |
| `/v1/orders` | GET | required | My orders (authenticated user), each with its nested `tickets[]`. |
| `/v1/groups` | GET | required | My groups (authenticated user) — as leader or member. |
| `/v1/groups/{groupId}` | GET | required | Group detail including member roster. 404 for a non-member (D-018). Each member row includes `username`/`avatar_key` (D-210, `ProfilePublic` members only) so the client can open a profile and show Connect. |
| `/v1/tickets/{ticketCode}/resend` | POST | required | Re-sends the ticket's QR (real PNG via `IQrCodeGenerator`) by email attachment and a WhatsApp-logged link. 403 if the caller isn't the ticket's owner. |

**Competition team invites (D-036)**: `POST /v1/events/{eventId}/invitations` (documented fully as part of Phase B; not otherwise covered in this file) accepts two new optional fields — `groupId` (turns the invite into a team invite, accepted via `/v1/groups/invitations/{token}/accept` above) and `username` (resolves a Kurx account by username instead of requiring a raw phone; auto-fills `name`/`phone` from that account, `404 username_not_found` if it doesn't exist). When `groupId` is set, authorization is the group's captain (`Group.LeaderUserId`) *or* an org manager, not org-manager-only as for a plain event invite.

## Ticket types & registration forms

Ticket types belong to an event; a ticket type carries a custom registration form built from form fields.
Org-scoped writes are authorized by `IEventAuthority` (D-269), never by a per-service role check: the event's creator, a `Representative`, or an `Owner`/`Manager` seat in the organization it **represents** all reach `Manager`; `KurxAdmin` outranks them. Hidden resources answer 404, not 403 (D-018).
Pricing/inventory/group rules and their error codes are defined in `docs/DECISIONS.md` D-020. Free-path order
endpoints exist (D-021, above); paid checkout does not yet.

| Endpoint | Method | Who | Notes |
|---|---|---|---|
| `/v1/orgs/{orgId}/events/{eventId}/ticket-types` | POST / GET | Owner/Manager/Admin | Create / list all types for the event (any sale window). **`priceTiers` (D-366)** — optional `[{ minSize, maxSize, pricePaise }]`, both ends inclusive, prices a team by its SIZE (`2→₹250, 3→₹300, 4–5→₹400`). Only on `registrationMode: "Group"` (`price_tiers_require_group`); must cover `groupMin..groupMax` exactly — no gap (`price_tier_gap`), no overlap (`overlapping_price_tiers`, also a database exclusion constraint), nothing outside the range (`price_tier_outside_group_size`), every price above zero (`invalid_price_tier`). **`pricePaise` is ignored when bands are sent** — the server derives the headline from the cheapest band, so it stays a truthful "from" figure for sorting and the paid/free filter. Omitted or empty = priced by `pricePaise` alone, which is every ticket type predating D-366. The view returns `price_tiers` ordered by size, absent (not `[]`) when there are none. Blocked when the event is `Archived`. **D-363 §4:** POST is refused with `event_under_review` (409) while the event is in `PendingReview`/`UnderReview`, and on an `Approved` event it returns the event to `PendingReview` — adding a price the reviewer never saw is a material change. |
| `/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ticketTypeId}` | PATCH | Owner/Manager/Admin | Update. Quantity may not drop below `sold` (`quantity_below_sold`). **`priceTiers` REPLACES the stored bands wholesale** (D-366) — a rule set edited row by row can pass through a state with a gap in it. **D-363 §4:** same as POST — `event_under_review` (409) under review, and an `Approved` event returns to `PendingReview`. Price is a field §4 names as material, and it lives here rather than on the event. |
| `/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ticketTypeId}` | DELETE | Owner/Manager/Admin | Delete; refused with `event_under_review` (409) while the event is with a reviewer and returning an `Approved` event to `PendingReview` (D-363 §4); rejected with `tickets_already_sold` when `sold > 0`, **or when any `order_item` references the type** (D-363) — a type whose orders were all refunded reads `sold == 0` but still owns order lines, and `order_items` cascades from `ticket_types`. |
| `.../ticket-types/{ticketTypeId}/fields` | GET / POST | Owner/Manager/Admin | List / add a form field. Field `key` is snake_case and unique within the ticket type (`duplicate_key`). |
| `.../ticket-types/{ticketTypeId}/fields/{fieldId}` | PATCH / DELETE | Owner/Manager/Admin | Update / delete a form field. |
| `/v1/events/{eventId}/ticket-types` | GET | public | On-sale types (`saleStarts ≤ now ≤ saleEnds`) for `Published` events only; `not_found` otherwise. |

Ticket type body (camelCase in): `{ name, pricePaise, pricingUnit, registrationMode, groupMin?, groupMax?, quantity, saleStarts, saleEnds, perUserLimit, isAllAccess, isCompetition? }`.
`pricingUnit` ∈ `PerTicket|PerGroup`; `registrationMode` ∈ `Individual|Group` (case-insensitive). Group mode requires `groupMin`+`groupMax` with `min ≤ max`. `isCompetition` (default `false`, D-036) always requires a Kurx account to register and, in `Group` mode, routes team joining through named invitations only (`/v1/groups/invitations/{token}/accept`) rather than the open `/v1/groups/join` code.

Ticket type response (snake_case): `{ id, event_id, name, price_paise, pricing_unit, registration_mode, group_min, group_max, quantity, sold, available, sale_starts, sale_ends, per_user_limit, is_all_access, is_competition }` (`available = max(0, quantity − sold)`).

Form field body: `{ key, label, type, scope, required, optionsJson?, sort? }`; `type` ∈ `Text|Number|Select|Checkbox|Date|File`, `scope` ∈ `PerRegistration|PerParticipant`. Response snake_case: `{ id, ticket_type_id, key, label, type, scope, required, options_json, sort }`.

Error codes (all RFC7807, `403 forbidden` / `404 not_found`, else `400`): `event_archived`, `invalid_pricing_unit`, `invalid_registration_mode`, `invalid_price`, `invalid_quantity`, `invalid_sale_dates`, `group_size_required`, `invalid_group_size`, `quantity_below_sold`, `tickets_already_sold`, `duplicate_key`, `invalid_type`, `invalid_scope`, and the D-366 band refusals `price_tiers_require_group`, `price_tiers_require_group_size`, `invalid_price_tier`, `price_tier_outside_group_size`, `overlapping_price_tiers`, `price_tier_gap`, and **`teams_not_supported`** (D-367 — a `Group` registration is only legal where the event's archetype supports the `teams` capability; an event with no archetype resolves every capability to Unsupported and is refused too). `UpdateAsync` enforces it only when a ticket is *becoming* `Group`, so a ticket already stored that way stays editable if the matrix later changes. `event_under_review` is **409**.

## Public profiles — Professional Identity System (`/v1/public`, unauthenticated) (D-201/202/203/206)

Verified professional identity, not a social/vanity profile — every field is derived from real
platform activity. Full design: `docs/architecture/PROFESSIONAL_IDENTITY_SPEC.md`.

**Client contract for section failures (D-235).** Every route in this group is viewer-aware, so clients
must treat these three outcomes as distinct and must never collapse them:

| Response | Meaning | Required rendering |
|---|---|---|
| `200` | Visible | The data |
| `403 forbidden` | The viewer is not entitled to this section | **Absence** — no error, no retry affordance. Nothing is wrong. |
| `404 not_found` on `/{username}` | Profile hidden or nonexistent, indistinguishable by design (D-018) | The standard not-found page |
| `5xx`, timeout, transport failure, unparseable body | We do not know | An explicit "couldn't load" notice — **never** an empty state |

Rendering an outage as an empty section makes a claim about the *person* out of a fact about the
*system*: it silently reports that someone has no certificates, no events and no organizations. A count
belonging to a failed fetch must be withheld (`null`) rather than shown as `0` for the same reason.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/public/users/{username}` | GET | Full profile: name, headline, bio, college (self-declared, authority-zero), skills, `summary`, `stats` (`events_conducted`, `events_attended`, `certificates_count`, `participations`, `achievements`, `ally_count` — each deduped to unique `EventId`), `verification` badges, `event_dna`, `achievements[]` (source-agnostic — `source: "certificate"\|"badge"`), `identity_labels[]` (derived tags, never manually editable), `organizations[]` (`roles[]`, `is_verified`, `joined_at`, `valid_until`, `org_events_conducted`, `org_certificates_count`, `org_achievements_count`), and a root-level `joined_at` — the **account's** creation month, ISO year-month `"2026-08"` (D-312). Month precision is enforced in the projection, so the exact signup instant is never on a public wire; the owner reads it from `/v1/me`'s `created_at`. Not to be confused with `organizations[].joined_at`, which is when the person joined that organisation. `404 not_found` for unknown/private username. |
| `/v1/public/users/{username}/events?type=conducted\|attended` | GET | Per-event cards: `roles[]`, `visibility`, `certificate_verify_code`, `is_achievement`. Deduped to one card per event regardless of how many roles/tickets. `type=attended` is `403 forbidden` unless the user has `ShowAttended`. |
| `/v1/public/users/{username}/certificates` | GET | `[{ id, event_title, issued_at, verify_code, is_achievement }]`. `403 forbidden` unless `ShowCertificates`. |
| `/v1/public/users/{username}/timeline` | GET | Milestone timeline (not a raw activity log): lanes `org_joined\|org_verified\|participation\|achievement\|certificate\|organized\|attended`, each `roles[]`-bearing and deduped per event/org; `is_first_event` flags the single earliest entry. |
| `/v1/public/users/{username}/allies` | GET | Accepted, `Visibility=Public` ally connections. **Viewer-aware**, like every other route in this group: the owner's `profile` and `network` section tiers are resolved against the caller, so the Connections and EventParticipants tiers work here (until D-231 this route ignored the caller entirely and gated on the legacy booleans, which returned empty to *everyone* — the owner included — whenever `network` was set to either middle tier). `profile` not visible to this viewer → **404** (D-018); `network` not visible → empty array, not an error. Counterparties are still filtered to those with a public profile, so a card only appears when the other party is publicly visible too. `[{ user_id, name, username, avatar_key, mutual_event_count }]`. |
| `/v1/public/users/{username}/journey` | GET | **Professional Journey** (D-223) — the first time each professional tier was provably reached, **ordered chronologically by first attainment, never by a canonical career ladder**. Unpaginated: at most one node per tier. `[{ tier, first_attained_at, occurrences, source, evidence{kind,event_title,event_slug,detail,org_name} }]`. Tiers: attendee · participant · volunteer · team_lead · competition_winner · speaker · judge · mentor · organizer · host · verified_member. A tier whose gating section the viewer can't see is **omitted entirely**, never summarised. An empty array is the correct answer for a user with no verified activity. |
| `/v1/public/users/{username}/competitions` | GET | Published competition placements (D-222) from `StageResult`. **Only `Published`** — provisional and disputed results never surface. Team results are not attributed to individuals. Gated by the `achievements` section. |
| `/v1/public/users/{username}/assignments` | GET | Organizer-assigned duties with completion (D-222) — the 14-role vocabulary the participation lane flattens. `ShowOnProfile=false` rows stay hidden regardless of the section tier (AND, never override). Gated by `events`. |
| `/v1/public/users/{username}/sessions` | GET | Talks given (D-222), via the organizer-made `Speaker.UserId` link (D-208). `Break` schedule items are excluded. Gated by `events`. |
| `/v1/public/users/{username}/metrics` | GET | Counts, rates and the Event DNA distribution (D-225). **A null member means hidden from this viewer, never zero** — render "—", because 0 would turn a privacy choice into a claim. **Every count is completed activity only (D-234)**: an event contributes nothing until it has ended, so these agree exactly with the `…/events` listings, which have always filtered the same way. `events_attended` and the competition counts carry no time filter because they are completed by construction (a check-in happens *at* the event; a result exists only after judging). Scheduled activity is deliberately not exposed as a metric. No vanity metrics: no profile views, points, leaderboard rank or percentiles. Gated by `metrics`. |
| `/v1/public/users/{username}/experience` | GET | Experience band (Building / Emerging / Active / Established / Distinguished) **plus the counts that produced it** — the two always travel together. Employment and education never contribute. Gated by `events`. |
| `/v1/public/users/{username}/contributions?months=` | GET | Daily contribution density (D-228). Only days **with** activity are returned, each with a 0–4 `level`; the client fills gaps. Each contributing fact is gated **before** bucketing, so a day whose only activity is hidden disappears entirely rather than leaving its intensity as an oracle. Gated by `contributions`. |
| `/v1/public/users/{username}/resume` | GET | **The Professional Resume as a PDF** (D-228). Composed **for the requesting viewer** — a section they may not see is absent from their copy, so it can never be a privacy bypass. One template, no custom sections, never stored (always regenerated; footer carries the generation date). Rate-limited 10/min per IP; the caller controls nothing about composition. |
| `/v1/public/orgs/{slug}` | GET | Public org view: name, slug, bio, tier, events_count (published/closed public events), members_count. `404 not_found` for unknown slug. |
| `/v1/public/users?q=` | GET | People search (D-209) — public-profile-only, name/username `ILIKE`. 2-character floor on `q` (empty result below it, not an error). `?page&pageSize` (`pageSize` clamped 1-50 — bounds the *response*; the scan itself is bounded by the `ix_users_name_trgm`/`ix_users_username_trgm` GIN trigram indexes, D-211). `[{ id, name, username, avatar_key, headline }]`. The foundation people-search/suggestions UI is built on. |

### Allies (`/v1/allies`, `/v1/me/allies`, authenticated) (D-201, extended D-209)

Mutual, explicitly-consented professional connections — not a follow, not derived from shared
events automatically. One `AllyConnection` row per unordered user pair for its entire lifetime;
state machine `Pending → Accepted/Declined/Revoked`. A non-party gets `404 not_found` on any
mutation (never `403`); a party attempting an invalid transition for the current state gets
`409 invalid_state`.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/allies/requests` | POST | Body `{ targetUserId }`. Idempotent if already `Pending` from the same requester; reactivates the same row after a prior Decline/Revoke; auto-accepts on a crossed mutual request. `400 cannot_ally_self`, `429 too_many_pending_requests` (≥200 outstanding). |
| `/v1/allies/requests/{id}/accept` | POST | Addressee only. |
| `/v1/allies/requests/{id}/decline` | POST | Addressee only. |
| `/v1/allies/{id}` | DELETE | Either party — covers both "cancel my pending request" and "remove an existing ally" (one terminal state, `Revoked`). |
| `/v1/allies/{id}/visibility` | PATCH | `{ visibility: "public" \| "hidden" }` (D-219) — hide one connection without turning off `show_allies` entirely. **The flag is shared**: `AllyConnection` is one row per unordered pair, so either party may set it and hiding removes the pair from *both* public profiles — the more private choice wins, consistent with either party's `ProfilePublic=false` already hiding it (D-201). Non-party → **404**, not 403 (D-018): confirming the row exists would leak that two people are connected. Allowed in any status (a display preference, not a state transition); unknown value → `invalid_visibility`, 400. |
| `/v1/allies/requests/incoming` \| `/outgoing` | GET | The caller's pending inbox/outbox. |
| `/v1/me/allies` | GET | The caller's own `Accepted` connections, regardless of per-connection `Visibility` (that flag only affects what *others* see). |
| `/v1/me/allies/status-batch` | POST | Body `{ userIds: Guid[] }`, capped at 200 targets (`400 too_many_targets` above it, D-211). One query for the whole list — the call every person-list surface (attendees/team/org members/speakers/search/suggestions) uses instead of one request per card. Returns `{ [userId]: "none"\|"pending_outgoing"\|"pending_incoming"\|"accepted" }`, defaulting any id with no row to `"none"`. |
| `/v1/me/allies/mutual/{otherUserId}` | GET | Real shared history with one specific person, not just a count — `{ shared_events: [{ id, title, slug, starts_at }], shared_orgs: [{ id, name, slug }] }` (reuses the same set-intersection helpers `PublicProfileService` uses for `mutual_event_count`). Requires an **Accepted** ally connection between caller and `otherUserId` (D-211) — otherwise both arrays come back empty, same 200 shape, no error (D-018 hide-don't-error). |
| `/v1/me/allies/suggestions?limit=` | GET | Ranked candidates from **shared-event co-participation and shared-org membership only** (v1 signals; college/company/competition/speaker-panel/certificate-overlap are named as deferred, not faked). Excludes existing `AllyConnection` rows in either direction. `[{ user_id, name, username, avatar_key, shared_event_count, shared_org_count, reason }]`, `reason` a human-readable string (e.g. `"3 shared events, same organization"`). |

## Certificates & ticket QR (D-035, D-036)

Real PDF/PNG rendering via QuestPDF (`ICertificateRenderer`) against system layouts (`classic-certificate` / `modern-certificate`, seeded by `DesignTemplateSeeder`). Custom (org-uploaded) templates are not implemented yet. Every generated certificate is currently `kind: "participation"` — Winner/RunnerUp/Finalist/Volunteer/Judge/Speaker/Organizer are modeled (`CertificateKind`) but not yet sourced (blocked on Judging and `EventAssignment` wiring respectively).

| Endpoint | Method | Auth | Notes |
|---|---|---|---|
| `/v1/certificates/{code}` | GET | public | Verify a certificate by `verify_code`. Returns `{ verify_code, kind, status, is_revoked, revoked_reason?, issued_to, event_title, event_slug, organizer, org_slug, issued_at, pdf_url? }` (`pdf_url` is a presigned download link). A revoked certificate still returns `200` with `is_revoked: true` — never `404` (D-036, deliberately the opposite of D-018's hide-drafts pattern). `404 not_found` only for an unknown code. |
| `/v1/events/{eventId}/certificates/generate` | POST | Owner/Manager/Admin | Bulk-generates certificates for eligible tickets (checked-in if `Event.CertificatesEnabled`, else any non-void ticket). Idempotent — already-generated tickets are skipped. Returns `{ generated: <count> }`. `"heavy"` rate-limited. |
| `/v1/certificates/{certificateId}/revoke` | POST `{ reason }` | Owner/Manager/Admin | Marks a certificate revoked (D-036). |
| `/v1/tickets/{code}/qr.png` | GET | ticket owner or event org member | Real scannable QR image (`image/png`) for a ticket — the actual QR payload embedded in `Ticket.Code`. |

## The gate (`/v1/gate/{eventId}`)

Admission at the door. **Two credentials, two routes** — an attendee's is a bare ticket `Guid`, a staff
member's is `staff:{assignmentId}:{sig}`. They are structurally disjoint, and each gets its own route so
neither can be resolved as the other and a scanner never has to guess what it is holding (D-385).

Both require the caller to be able to work this gate: a member of the organization the event represents,
**or** the holder of an accepted `EventAssignment` on this specific event. That is what lets an external
Security or Registration Desk assignee scan for the event they accepted and nothing else.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/gate/{eventId}/scan` | POST `{ ticketCode, deviceInfo? }` | Admits a ticket holder. Writes `gate_entries` and denormalises first check-in onto the ticket. `invalid_code`, `invalid_signature`, `ticket_void`, `event_mismatch` (an all-access fest pass at a child event is allowed), `forbidden`. A repeat scan returns `200` with `is_duplicate: true` and who admitted them first. Admission **succeeds even when the holder is no longer audience-eligible** and reports `eligibility_flag` — it never silently voids, nor silently admits (V3 §4.4). |
| `/v1/gate/{eventId}/scan-staff` | POST `{ pass, deviceInfo? }` | **D-385.** Admits a staff member from the signed pass on their badge. Returns `{ admitted, is_duplicate, name, role, access_level }` — the marshal reads those back against the person in front of them. Writes `staff_gate_entries`. |

**The signature is the weakest of the three checks on a staff pass.** It proves only that Kurx minted the
pass. Whether the assignment is still `Accepted` (`assignment_not_active`) and whether it belongs to
**this** event (`event_mismatch`) are asked of the database at scan time (D-015), so removing someone from
the crew stops the badge already in their pocket and Saturday's badge does not open Sunday's door. A
malformed payload, a bad signature and a deleted assignment all answer `invalid_pass` — telling a forger
which half they got wrong is free help. The comparison is fixed-time.

**A repeat staff scan reports rather than refuses.** Staff come and go all day; the marshal still gets the
name and access level, which is why they scanned again.

**`staff_gate_entries` is its own table, not a nullable column on `gate_entries`.** Every attendance figure
on the platform counts that row, so admitting staff through it would inflate attendee check-ins with people
who never bought anything. It is keyed on the `EventAssignment` — what the badge encodes, and what
revocation acts on — with a unique `(AssignmentId, EventId)`.

## Event badges (`/v1/events/{eventId}/badges`) — D-362

Printed lanyard badges for an event's attendees and staff. **Organizer-only: there is no holder-facing
route here by design** — no `/v1/me/badges`, no self-service download. Badges are pre-printed and handed
out; adding a "my badge" route would be a product change, not a convenience.

Every route that touches an event's data resolves **Manager** authority live through `IEventAuthority`
(D-015) and answers `404`, not `403`, to a caller with no standing on the event (D-018). Staff-level view
access is deliberately not enough: a badge is an entry credential, and minting one for an arbitrary person
is a different power from reading a name. (`/sizes` is the exception — it returns a fixed list of paper
sizes, reads nothing, and is authenticated only.)

Rendering reuses `ICertificateDocumentRenderer` (D-361's millimetre pages) rather than a second renderer;
`BadgeLayout` is a layout, not an engine.

| Endpoint | Method | Auth | Notes |
|---|---|---|---|
| `/v1/events/{eventId}/badges/sizes` | GET | Authenticated | The badge sizes available for printing: `lanyard` (88.9×139.7mm), `large` (101.6×152.4mm), `card` (CR80, 85.6×54mm, the only landscape one). Served rather than hardcoded per client so the console cannot drift from what the renderer supports. |
| `/v1/events/{eventId}/badges/recipients` | GET | Manager | Everyone who can be given a badge — ticket holders and accepted `EventAssignment` staff. Someone who is both appears **once, as staff**. Returns `{ user_id, name, kind: "attendee" \| "staff", subtitle, access_level, has_photo, card }`. `card` is null until one is issued. The QR payload is deliberately **not** returned: it is a working credential, and listing everyone's scannable code would hand out badges as JSON. |
| `/v1/events/{eventId}/badges/template` | GET | Manager | The event's saved card design, or the shipped default. Served **snake_case** including nested records (`font_size_pt`, `z_order`). `fields: null` means *"the server will use its built-in layout"* — **not** an empty card. |
| `/v1/events/{eventId}/badges/template` | PUT | Manager | Save the design. Binds **camelCase**. Colours, geometry and field keys are sanitised before storage, never just before rendering: `x`/`y` clamp to 0–99, `width`/`height` to 1–100, `fontSizePt` to 4–72, a malformed hex is discarded, an unknown `sizeKey` falls back to `lanyard`, and an unknown field key is dropped. A `logoKey`/`backgroundKey` outside `events/{eventId}/id-cards/` is refused (`invalid_logo_key` / `invalid_background_key`) — accepting an arbitrary one would make saving a design a read primitive over the bucket. |
| `/v1/events/{eventId}/badges/template/defaults` | GET `?size=&kind=` | Authenticated | **D-385.** The built-in placements, so the editor opens on the layout the server would actually print. Without it a client renders `fields: null` as an empty card, and the first field enabled replaces the whole default layout — silently dropping the QR. Reads nothing about the event, so it is authenticated only, like `/sizes`. |
| `/v1/events/{eventId}/badges/template/preview` | POST `{ spec, kind? }` | Manager | Renders the spec **in the body** — unsaved — as `image/png` at 96dpi, through the same renderer that prints. `kind` is `attendee` or `staff`; they differ, since only a staff card carries an access band. |
| `/v1/events/{eventId}/badges/template/asset/presign` | POST `{ contentType, purpose? }` | Manager | Presigned upload for the card's artwork (`background`, ≤8MB) or logo (default, ≤2MB). Raster only — PNG/JPEG/WebP; **SVG is refused** (`unsupported_content_type`), since it is a script-capable document the renderer fetches and serves back inside a page. |
| `/v1/events/{eventId}/badges/template/asset-url` | GET `?key=` | Manager | A signed readable URL so the editor canvas can show the artwork under the fields. The key is checked against the event first — this returns a signed URL, so an unchecked key would read anything in the bucket. |
| `/v1/events/{eventId}/badges/generate` | POST `{ sizeKey, kinds?, userIds? }` | Manager | **Issues real cards**: creates the `id_cards` rows, allocates card numbers (`KRX-00001`, unique per issuing org) and verify codes, renders the artefacts and stores them. Returns `{ issued, regenerated }`. Idempotent per (event, holder) — re-running keeps the existing number, so reprinting a damaged badge does not mint a second identity. |
| `/v1/events/{eventId}/badges/sheet` | POST `{ sizeKey, kinds?, userIds? }` | Manager | The print run — one `application/pdf` with badges laid out N-up on A4 with cut guides. `kinds` omitted means both. `userIds` omitted means everyone matching `kinds`. An unrecognised `sizeKey` is **refused** (`unknown_badge_size`), never defaulted: defaulting prints a whole run at the wrong physical size. |
| `/v1/events/{eventId}/badges/{userId}/revoke` | POST `{ reason? }` | Manager | **D-386.** Marks an issued card revoked and returns it. `card_not_issued` when nothing was ever issued, `already_revoked` on a second call — two different states, reported as such. **It marks the card, it does not close a door**: an attendee's entry credential is their ticket and a staff member's is their assignment, so stopping entry means voiding the ticket or removing the assignment. A revoked card keeps its number and stays on the roster, because a verifier must be able to tell a revoked badge from one that never existed (D-331). Undone by re-issuing, which takes a new number — not by un-revoking. |
| `/v1/events/{eventId}/badges/{userId}.pdf` | GET `?size=` | Manager | One badge, print-ready. Defaults to `lanyard`. The reprint path for a single lost lanyard. |

**A stored artefact belongs to the size it was rendered at (D-385).** Issued PDFs and PNGs are keyed
`events/{eventId}/id-cards/{cardId}/{sizeKey}/card.{pdf,png}`, and the sheet and single-badge routes reuse
a stored artefact **only** when it matches the size being asked for. Before that the size in the key was
absent and a lanyard raster was served for a CR80 request, drawn into the landscape slot at 34.4×54mm
instead of 85.6×54mm — the selector silently stopped working the moment anything was issued.

**A field with no value prints nothing, never its own key (D-385).** The shared renderer draws
`{holder_name}` for an empty `dynamicfield` — the right prompt while designing a certificate, a printed
defect on an issued badge. `BadgeLayout` drops such fields, and `IdCardService` falls back from
`User.Name` to `User.Username` first.

**QR payloads differ by kind, and that is load-bearing.** An attendee badge carries the holder's existing
`Ticket.Code`, exactly as `/v1/tickets/{code}/qr.png` encodes it — so a printed badge scans through
`GateEntryService` with no change to the gate. Staff hold no ticket, so their badge carries
`staff:{assignmentId}:{sig}` where `sig = HMAC(TICKET_HMAC_SECRET, "staff-pass|" + assignmentId)`:
domain-separated from `SignTicketCode` so neither can be replayed as the other, and derived rather than
stored, so revocation stays on the assignment's own `Status`. The gate accepts one at
[`/v1/gate/{eventId}/scan-staff`](#the-gate) (D-385).

## Realtime (SignalR)

| Hub | Path | Groups / Methods |
|---|---|---|
| Sales | `/hubs/sales` | `JoinOrg(orgId)` / `LeaveOrg(orgId)` — caller must be a member of that org. |
| Scan | `/hubs/scan` | `JoinEvent(eventId)` / `LeaveEvent(eventId)` — caller must be a member of the org that owns the event. |
| Chat | `/hubs/chat` | `JoinRoom(roomId)` / `LeaveRoom(roomId)` / `SendMessage(roomId, body, replyToId?)` — membership re-checked at connect. Per-room sliding-window rate limit (Redis-backed; always-allow fallback in single-instance dev). Detail: [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md). |
| Notifications | `/hubs/notifications` | `OnConnectedAsync` joins `user:{userId}` automatically. |
| Login | `/hubs/login` | Anonymous by design (AM9) — the poll token authorizes the subscription inside the hub. |

> The Chat row previously credited the hub with delete, pin, react, and typing methods plus read-receipt broadcast. None exist — `ChatHub` has exactly the three methods listed. Corrected 2026-07-18 (D-104).

All hubs except Login require authentication. Since browsers can't set an `Authorization` header on WebSocket/SSE connections, pass the access token as a query string parameter: `wss://.../hubs/sales?access_token=<token>`. A join for a group the caller doesn't have access to throws a `HubException` with message `forbidden` (or `not_found` for an unknown event/room).

## Chat

Chat REST endpoints are **documented in [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md)** — the canonical source for the chat contract, including the frozen Phase 1 changes (cursor pagination, `clientMessageId` idempotency, capabilities object, reserved `attachments` array). See also [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md).

## Engagement: saved events, follows, reviews (D-064)

| Endpoint | Method | Auth | Notes |
|---|---|---|---|
| `/v1/events/{eventId}/save` | POST / DELETE | required | Save / unsave. Save 404s for a non-visible event. Idempotent. |
| `/v1/me/saved` | GET | required | My saved events (event-card summaries). |
| `/v1/orgs/{orgId}/follow` | POST / DELETE | required | Follow / unfollow an org. |
| `/v1/me/following` | GET | required | Orgs I follow (`org_id, name, slug, logo_key`). |
| `/v1/events/{eventId}/reviews` | POST | required | Post/edit my review `{ rating(1–5), title?, body?, isAnonymous? }`. `review_requires_ticket` (403) if I hold no ticket; `invalid_rating`. |
| `/v1/events/{eventId}/reviews` | GET | none | Public list: `{ items[], summary{average,count}, total }`, `?page=&pageSize=`. Anonymous reviews hide the author (`author_name`/`author_username`/`author_avatar_key` all null). Non-anonymous reviews additionally expose `author_username`/`author_avatar_key` when the author's profile is public (D-210), for a profile link — no Connect button on this surface. |
| `/v1/events/{eventId}/reviews/mine` | DELETE | required | Delete my review (soft). |

## Event invitations — one policy, two methods (D-266 M6 / D9)

**`Invite Only` is a single registration policy.** Username invitations and invite links are *methods*
within it — a policy answers "who may register", a method answers "how were they told". Both produce the
same outcome: **permission to register, and nothing more.** An invited guest of a paid event still pays;
accepting creates no order and issues no ticket.

**Method A — invite by Kurx username.** Needs no email, no phone and no forwardable link.

| Route | Method | Notes |
|---|---|---|
| `/v1/users/search?q=` | GET | Username lookup for the invite picker. An authenticated alias over the **same** index as `/v1/public/users` — a second user search would be a second answer to "who is on Kurx", and it would be the one that forgot profile visibility. A user who has not made their username discoverable is invited by email or phone instead. |
| `/v1/events/{id}/invitations` | POST | `+ username` creates a Method A invitation (`invited_user_id`), emits an in-app `invitation_received`, and needs no contact detail. `invite_target_required` (400) when addressed to nobody; `duplicate_invitation` (409) for a second invite to the same user. |
| `/v1/me/invitations` | GET | The invitee's own inbox. `?pendingOnly=` defaults true. Carries the event, so the recipient need not fetch each one to know what they were invited to. |
| `/v1/invitations/{id}/accept` \| `/decline` | POST | **`not_invited` → 404, never 403**: confirming an invitation exists to someone uninvited discloses the guest list one probe at a time (D-018). `invitation_already_responded` → 409. Notifies the organiser back. |

**Method B — invite links.** For reaching people not yet connected on Kurx.

| Route | Method | Notes |
|---|---|---|
| `/v1/events/{id}/invite-links` | POST · GET | `{ maxSeats?, singleUse?, expiresAt?, passcode? }`. `maxSeats` null = unlimited. The passcode is hashed on arrival and **never returned by any surface**. |
| `/v1/invite-links/{id}` | DELETE | Revoke. Stops new arrivals; never retracts seats already claimed. |
| `/v1/public/invite-links/{token}` | GET | **Anonymous** pre-flight — usable? passcode needed? seats left? The holder may not have an account yet, which is the case this method exists for. |
| `/v1/invite-links/{token}/redeem` | POST | Authenticated: a seat is claimed *for* someone, and an anonymous claim could be neither idempotent per person nor matched at registration. `{ passcode? }`. |

**Seat claiming is atomic.** `used_count` is claimed with a conditional `ExecuteUpdateAsync` carrying cap,
expiry, revocation and single-use in its WHERE, so the database decides who gets the last seat (D-240/D-261
— the coupon over-redemption bug class). Redeeming twice consumes no second seat (D9 rule 7), guarded by a
unique `(link, user)` index rather than a read-then-write. A wrong passcode is refused **before** the claim,
so guessing cannot drain a link.

**Errors:** `invite_link_revoked` · `invite_link_expired` · `invite_link_exhausted` (409) ·
`invite_link_passcode_required` · `invite_link_passcode_invalid` (403) · `not_invited` (404) ·
`invitation_already_responded` (409) · `invite_target_required` (400).

**Enforcement is in the one eligibility engine.** `AudienceService.EvaluateAsync` — which order creation,
both group-join tails, ticket transfer, gate admission and discovery all route through — refuses an
uninvited registrant on an `Invite Only` event with `not_invited`. The check sits **above** the no-rule
early return: an invite-only event rarely also carries an `AudienceRule`, so checking after it would leave
every such event wide open.

## Org invitations & event assignments (D-064)

| Endpoint | Method | Who | Notes |
|---|---|---|---|
| `/v1/orgs/{orgId}/invitations` | POST / GET | Owner / Manager | Invite `{ phone, role }` (Manager → Staff only) / list pending. |
| `/v1/orgs/{orgId}/invitations/{id}` | DELETE | Owner / Manager | Cancel a pending invite. |
| `/v1/me/org-invitations` | GET | required | Pending invitations addressed to me. |
| `/v1/org-invitations/{token}/{accept\|decline}` | POST | required | Accept (creates membership) / decline. `not_your_invitation`, `expired`, `not_pending`. |
| `/v1/orgs/{orgId}/events/{eventId}/assignments` | POST / GET | Owner / Manager | Assign `{ phone, role, customRole?, notes? }` (14 roles or Custom) / list. `invalid_role`, `user_not_found`. |
| `/v1/orgs/{orgId}/events/{eventId}/assignments/{id}` | DELETE | Owner / Manager | Remove an assignment. |
| `/v1/assignments/{id}/{accept\|decline}` | POST | the invited user | Respond to an assignment. |
| `/v1/me/assignments` | GET | required | My invited/accepted assignments. |

## In-app notifications & devices (D-064)

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me/notifications` | GET | `{ items[], unread_count }`, `?page=&pageSize=`. |
| `/v1/me/notifications/{id}/read` | POST | Mark one read. |
| `/v1/me/notifications/read-all` | POST | Mark all read. |
| `/v1/me/notifications/{id}` | DELETE | Delete one notification (D-212 — ported from the removed `/v1/notifications/{id}` on the retired dead-code endpoint file; same underlying service call). |
| `/v1/me/devices` | POST | Register `{ fcmToken, platform }` (upsert by token). The FCM push *send* is deferred (provider). |
| `/v1/me/devices/{id}` | DELETE | De-register a device. |
| `/v1/events/{eventId}/certificates` | GET | Owner/Manager/Admin certificate roster: `[{ id, verify_code, holder_name, kind, status, is_revoked, … }]`. |

## Gamification — points, badges, leaderboards (D-210, D-212; `/v1/referrals*` still undocumented)

Every route in `GamificationEndpoints.cs` returns `Results.Ok(record)` directly, so the wire
format is **camelCase** throughout (`userId`/`totalPoints`, not `user_id`/`total_points`) — the
same quirk documented for `AttendeeRow` in D-208. `PointsSummary` has **no level/tier
concept** — a prior Flutter client DTO invented one (`level`, `levelName`, `nextLevelPoints`)
that never existed on this contract; fixed in D-212.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me/points` | GET | `{ totalPoints: number, history: [{ id, source, points, reason, createdAt }] }`. No level/tier — just a running total and its ledger. |
| `/v1/me/badges` | GET | `[{ id, name, type, description, iconKey, earnedAt }]` — every returned badge is, by construction, one the caller has earned; there is no separate "unearned/catalog" shape or `earned` flag to check. |
| `/v1/leaderboards` \| `/v1/leaderboards/events/{eventId}` \| `/v1/leaderboards/organizations` | GET | `?limit=` (default 50, max 100). `[{ rank, userId, name, username, points }]`. `username` is null unless the entry's user has `ProfilePublic = true` (D-210) — `name` itself is always shown (a leaderboard's whole point is ranking real names; only the profile-link field is gated). **No time-period dimension exists** (no week/month/all-time filter) — `Leaderboard` rows are scoped `global`/`organization` only. |

## Waitlist & discovery filter (D-064)

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/events/{eventId}/ticket-types/{ticketTypeId}/waitlist` | POST / DELETE | Join a **sold-out** ticket type (`tickets_available` if not sold out, `already_waitlisted`) / leave. |
| `/v1/me/waitlist` | GET | My active waitlist entries (position, status). |
| `/v1/events?price=free\|paid` | GET | Public search now also filters by price (`free` = no priced ticket type). |
| `/v1/events?mode=offline\|online\|hybrid` | GET | Filter by delivery mode (D-064 A7). |

Events also accept `eventMode` (offline/online/hybrid) + `onlineUrl` on create/update (`online_url_required` for online/hybrid), and return `event_mode` + `online_url` on the detail. The detail also returns `settlement_currency` (ISO-4217, V3 §9.1, Phase 3) — the single currency the event settles in, bound from its Org at creation; every money-bearing table carries an additive `currency` column (default INR) alongside its existing `*_paise` amount (D-004 unchanged). Multi-currency settlement on one event is out of scope. Every money row created through an event flow inherits the event's settlement currency, and the money-showing responses (org events revenue, event analytics, wallet, ledger entries) expose `currency` alongside their amounts so clients never assume INR.

## Posts — the social feed (D-262)

A feed alongside Events: text, images, video, documents, polls, event posts, mentions, hashtags,
like / comment / reply / share / save / report.

**Every value-returning route here publishes a response schema** (`.Produces<T>()`), so `swagger.json`
carries the full contract for this module — unlike most of the platform (see the frontier note in
[`docs/roadmap/README.md`](../roadmap/README.md#not-built-yet)).

**Cursors are opaque strings** (the rule chat set in D-104). Clients must not parse, order, or
construct them. `next_cursor` is `null` when the page is the last one. `limit` ≤ 50, default 20.

### Visibility

`visibility` ∈ `public` | `connections` | `event_participants` | `only_me`, enforced server-side on
**every** read path.

| Tier | Who can read it |
|---|---|
| `public` | anyone, including anonymous callers on the public profile route |
| `connections` | the author and their **accepted allies** (D-201) |
| `event_participants` | the author and anyone holding a non-void ticket for `event_id`. Checked **live** per request (D-015) — voiding a ticket revokes access on the next request. Requires an `eventId`, else `invalid_visibility`. |
| `only_me` | the author |

A post the caller may not see answers **404 `post_not_found`, never 403** (D-018) — a 403 would confirm
that the post exists. The one exception is `post_hidden` (404), which only ever reaches the post's own
author, so platform moderation is not silent to the person it happened to.

### The feed graph

`GET /v1/feed` is an explicit graph, **not** "everything public": the caller's own posts, posts by
accepted allies, posts attached to events of orgs the caller follows, and posts attached to events the
caller holds a ticket for. A stranger's public post is reachable through their profile and through
hashtag pages, but does not arrive uninvited. Rationale in D-262.

### Reads

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/feed` | GET | `?cursor=&limit=` → `PostPage`. |
| `/v1/posts/{postId}` | GET | `PostView`. |
| `/v1/me/posts` | GET | The caller's own posts, including their restricted ones. |
| `/v1/me/posts/saved` | GET | Ordered by when they were **saved**. A post whose author later restricts it drops out — a save is a bookmark, not a grant. |
| `/v1/public/users/{username}/posts` | GET | **Public.** A bearer token is optional and widens what is returned; anonymous sees `public` only. An unknown handle returns an empty page rather than 404, so the route is not a username-enumeration oracle. |
| `/v1/events/{eventId}/posts` | GET | `event_not_found` (404) for an unknown event. |
| `/v1/hashtags/{tag}/posts` | GET | Tag matched lowercase, with or without a leading `#`. |
| `/v1/hashtags/trending` | GET | `?limit=` (default 10, max 50) → `[{ tag, post_count }]` over the last 7 days, counted **only over posts the caller can see**. |
| `/v1/posts/{postId}/comments` | GET | `PostCommentPage`. The response is flat and carries `parent_comment_id` — the client groups replies under their parent — but a **page is a page of roots**, newest-first, and every root arrives with all of its replies (oldest-first, capped at `limit × 10`). `limit` and `next_cursor` therefore count roots, not rows, so a reply is never delivered on a different page from the parent it belongs to. |

### Writes

| Endpoint | Method | Body (camelCase) | Notes |
|---|---|---|---|
| `/v1/posts` | POST | `{ body, kind, visibility, mediaIds?, eventId?, sharedPostId?, poll? }` | `PostView`. Rate-limited (`posts`, per user). |
| `/v1/posts/{postId}` | PATCH | `{ body?, visibility? }` | Author only. Media, poll, event attachment and `kind` are **immutable after creation** — the request carries no field for them. Sets `edited_at`. |
| `/v1/posts/{postId}` | DELETE | — | `204`. Soft delete; author or platform moderator. |
| `/v1/posts/{postId}/like` | POST / DELETE | — | `PostLikeResult` `{ liked, like_count }`. Idempotent both ways; the counter never goes negative. |
| `/v1/posts/{postId}/save` | POST / DELETE | — | `204`, idempotent. |
| `/v1/posts/{postId}/poll/vote` | POST | `{ optionIds: [] }` | `PostPollView`. One ballot per person, cast once. |
| `/v1/posts/{postId}/comments` | POST | `{ body, parentCommentId? }` | `PostCommentView`. Rate-limited. Threading is **exactly one level** — a reply to a reply attaches to the same parent, the rule chat uses. |
| `/v1/comments/{commentId}` | DELETE | — | `204`. The comment's author, the post's author, or a moderator. Deleting a root **takes its replies with it**, and `comment_count` drops by all of them. |
| `/v1/comments/{commentId}/like` | POST / DELETE | — | `PostLikeResult`. |

`kind` is **derived server-side** from what is actually attached (share → poll → video → images →
document → event → text); the value the client sends is advisory, so the discriminator can never
disagree with the payload it describes.

Hashtags and mentions are **extracted server-side** from `body` on create and on edit — never accepted
from the client. `#tag` is lowercased; `@username` is resolved to a real account and dropped silently if
unknown. The grammar matches what the web client linkifies:
`/(^|[\s(])([#@][A-Za-z0-9_]{1,30})(?=$|[\s).,!?:;])/g`.

**Sharing a share re-targets the original** and never nests, so `shared_post` is at most one level deep.
`share_count` moves on the original in both directions — deleting a reshare gives the share back.

### Media (two-step upload, mirroring chat attachments D-110)

| Endpoint | Method | Body | Notes |
|---|---|---|---|
| `/v1/posts/media/presign` | POST | `{ fileName, contentType, sizeBytes }` | `{ media_id, upload_url, storage_key }`. Rate-limited. |
| `/v1/posts/media/confirm` | POST | `{ mediaId, storageKey }` | `PostMediaView`. Idempotent per media id. |

PUT the bytes to `upload_url`, then confirm. **Every authoritative check runs on confirm** — size, MIME,
extension, magic bytes, image dimensions and the malware scan — because it is the first moment the server
can look at the actual object; the declared `contentType` is only a hint. Allow-list: JPEG/PNG/GIF/WebP,
MP4/WebM, PDF/DOCX/XLSX/PPTX/TXT/CSV. Ceiling: **25 MB** for every kind, video included — that is a
platform limit, not a product choice. The only storage provider implemented today presigns back at this
API, so an upload is an ordinary Kestrel request capped by `MaxRequestBodySize` (~28.6 MB); a higher
ceiling would presign happily and then die part-way through the bytes on an opaque 413. Raise it when
real object storage lands and the client PUTs straight to the bucket. Oversize files are refused at
**presign** with `invalid_media`, before a byte is sent. Per-post caps: **10 images, 1 video, 5 documents**.

`duration_seconds` is always `null` today: no media probe is wired, and a fabricated duration is worse
than an absent one. Media `url` is a short-lived signed link minted per read, never a stored public URL.

### Moderation

| Endpoint | Method | Who | Notes |
|---|---|---|---|
| `/v1/admin/posts` | GET | Moderation | `?q=&visibility=&reported=&page=&pageSize=` → `{ items[], total }`. Offset-paginated, and deliberately **not** visibility-filtered — a reported private post is exactly what a moderator must be able to see. `?reported=true` narrows to posts with an open report. |
| `/v1/admin/posts/{postId}/hide` | POST | Moderation | `{ reason }` (required) → `204`. Audit-logged. |
| `/v1/admin/posts/{postId}/unhide` | POST | Moderation | `204`. Audit-logged. |

Reporting a post or a comment goes through the existing [`POST /v1/reports`](#content-moderation-reports-c-5-d-059)
with `entityType` `post` / `post_comment`.

### Notifications

`post_like`, `post_comment`, `post_reply`, `post_mention` — through the shared notification pipeline, so
the in-app row, unread count, SignalR push and FCM fan-out are inherited. `data` carries
`{ notificationType: "post", postId, commentId, actorId, deep_link, route }`. Nobody is ever notified
about their own action; an edit notifies only the mentions it **added**.

### Errors

`post_not_found` · `comment_not_found` · `not_post_author` · `body_too_long` · `body_required` ·
`too_many_media` · `invalid_media` · `poll_needs_two_options` · `poll_closed` · `already_voted` ·
`invalid_option` · `invalid_visibility` · `cannot_share_a_share` · `event_not_found` ·
`not_event_participant` · `post_hidden` · `rate_limited`

Status mapping: `404` for `post_not_found` / `comment_not_found` / `event_not_found` / `post_hidden`;
`403` for `not_post_author` / `not_event_participant`; `409` for `already_voted`; `429` for
`rate_limited`; `400` for the rest.

## Account settings (D-263)

Notification preferences, blocks, language, username history, the email/phone change ceremonies and
scheduled deletion. All authenticated; all under `/v1/me`.

**No TOTP.** The platform already ships passkeys (AM3), trusted devices with device-signed step-up
(AM2/AM6) and recovery codes (AM7). TOTP is a shared secret — phishable and replayable inside its
window — so it would be the weakest of four factors. Reasoning in D-263.

### Notification preferences

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me/notification-preferences` | GET | `{ categories: [{ category, in_app, push, email, whats_app, locked }] }` — all 12 categories, stored row or default. |
| `/v1/me/notification-preferences` | PATCH | `{ categories: [{ category, inApp?, push?, email?, whatsApp? }] }`. Absent channels are left unchanged. |

Categories: `event_updates` · `invitations` · `staff_invitations` · `team_invitations` ·
`connection_requests` · `payments` · `certificates` · `messages` · `posts` · `announcements` ·
`security` · `system`.

**No row means all channels on except `whatsapp`** — the pre-existing behaviour, which is why shipping
this needed no backfill. **`security` is `locked` and cannot be switched off**: a PATCH that tries
returns `category_not_optional` (403), and dispatch re-checks independently, so a row forged straight
into the database still cannot silence it.

Enforcement lives inside `INotificationService.NotifyAsync` — the one dispatch point on the platform —
so every existing and future emitter inherits it. **Scope, stated plainly:** that method drives `in_app`
and `push`, and those two are enforced today. The `email` and `whatsapp` switches are stored and
returned but are not yet consulted by the senders that own those channels (announcements, WhatsApp log),
which live outside this module. Named here rather than left as a silent half-enforcement.

### Blocks

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me/blocks` | GET | `BlockedUserView[]`. |
| `/v1/me/blocks/{userId}` | POST | `204`, idempotent. `cannot_block_self` (400). |
| `/v1/me/blocks/{userId}` | DELETE | `204`, unconditional — unblocking someone never blocked is the state asked for. |

The row is one-way; **enforcement is symmetric**. If either direction exists, neither party sees the
other's posts or comments (404, not 403 — D-018), and a pending ally request in either direction is
declined.

### Email and phone change

**Phone change was not rebuilt.** A complete user-initiated ceremony already existed at `/v1/me/phone/verify`; D-263 added the two things it lacked — step-up and a change notice — rather than shipping a second one beside it (D-018).

| Endpoint | Method | Body | Notes |
|---|---|---|---|
| `/v1/me/email/change/start` | POST | `{ newEmail }` | `202`. Step-up required. OTP to the **new** address; the **old** address is notified a change was requested. |
| `/v1/me/email/change/complete` | POST | `{ newEmail, code }` | `{ email, email_verified }`. |
| `/v1/me/phone/verify` | POST | `{ phone, code }` | **Pre-existing route, hardened by D-263** with a step-up gate and a change notice. Dual-writes all four phone columns (D-089), **revokes every session** (D-038) and re-issues tokens. Request the OTP first via `POST /v1/auth/otp/request`. |

Both ceremonies reuse the existing OTP substrate (`IOtpService`, D-215) and sit behind the `otp` rate
limit. Phone is the primary auth identifier (ADR-A6), so a completed change is treated as a recovery
event. The old-address notice is sent on **start**, not on success: on an account already compromised
it is the only signal the real owner gets. Errors: `invalid_email`, `email_taken`, `invalid_phone`,
`phone_taken`, `invalid_code`, `step_up_required` (403).

### Language

`PATCH /v1/me/profile { language }` — extends the existing endpoint. `en` | `hi`; anything else is
`invalid_language` (400) rather than a silent fallback.

### Username history

`GET /v1/me/username-history` → `{ username, released_at }[]`. Read-only projection of existing rows.

### Deletion (India DPDP)

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/me/deletion` | GET | `{ pending, scheduled_for, requested_at }`, or **404** when nothing is scheduled — so a client cannot mistake "never requested" for "cancelled". |
| `/v1/me/deletion` | POST | `{ reason? }` → `{ scheduled_for }`. Step-up required. 30-day grace. |
| `/v1/me/deletion` | DELETE | `204`. Cancels during grace. **Deliberately not step-up gated** — cancelling is the safe direction, and someone losing their devices must always be able to stop the clock. |

After the grace window a sweep anonymises. **Cleared:** name, all four phone columns, email, username,
avatar/cover keys, headline, bio, skills, links, education; authored posts and comments are
soft-deleted. **Retained by design:** orders, order items, payments, refunds, wallet ledger, tickets,
certificates and audit logs — statutory retention that erasure does not override, still pointing at the
now-anonymised id. Full boundary in D-263.

## Direct messages (D-264)

A DM **is a `ChatRoom`** with no event, so messages, history, attachments, presence and read pointers
are the existing `/v1/chat/*` endpoints, unchanged. Only what is genuinely new lives under `/v1/dm`.

| Endpoint | Method | Notes |
|---|---|---|
| `/v1/dm/{userId}` | POST | `ChatRoomView`. **Idempotent by database constraint** — a unique index on the canonical `(low, high)` pair, so two simultaneous taps cannot make two rooms. Also un-archives for the caller. `cannot_dm_self` (400), `blocked` (403). Rate-limited. |
| `/v1/me/dm` | GET | `DmRoomView[]`, newest activity first. `?archived=true` for the archive folder. |
| `/v1/me/dm/requests` | GET | `DmRoomView[]` awaiting **this** caller's decision. A request you sent is not here. |
| `/v1/dm/{roomId}/requests/accept` | POST | `204`. Recipient only — `not_recipient` (403), `already_answered` (409). |
| `/v1/dm/{roomId}/requests/decline` | POST | `204`. Further sends return `dm_declined`. |
| `/v1/dm/{roomId}/archive` | POST / DELETE | `204`. Per-member folder. |

**Message requests are the spam control.** A DM from someone who is not an accepted ally lands
`pending`: the room exists, the message is stored, and **nobody is notified** until the recipient
accepts. Allies skip straight to `accepted`. Without this an open DM endpoint is an abuse surface from
the hour it ships.

**Blocks (D-263) are enforced both ways** — no room is created, no message is delivered, and the
conversation disappears from both lists. Re-checked live on every send, so a block bites on the next
message rather than the next login.

**Archiving is per-member** (`ChatMember.ArchivedAt`) and is deliberately *not*
`ChatRoomStatus.Archived`, which is event lifecycle, one-way and shared. Writing into an archived
conversation un-archives it.

Sending is `POST /v1/chat/rooms/{roomId}/messages` exactly as for event chat. Notifications use kind
`dm_message` and respect the `messages` notification category.

## Health

`GET /health` — see [deployment docs](../deployment/README.md#health-checks-for-orchestrators).

**Financial review (D-266 M7, D12 §6 A11).** `POST /v1/admin/events/{id}/financial-review` —
`{ passed, notes? }`. **FinanceOps only**, not VerificationReviewer: clearing a money path is a different
competence from reviewing content. `notes_required` (400) to fail — a refusal the organiser cannot act on
is a dead end, not a review outcome. Required for archetypes whose D12 §6 Review cell is *Required +
financial* (**A11 Fundraising alone**), keyed on the archetype rather than on "is it paid": a ticketed
concert (A8) takes more money and raises none of the same risk. Reports `financial_review_required` in
`publish_blockers` until passed.

**Wizard autosave (D-266 M8).** `PUT`/`GET /v1/events/{id}/draft` — `{ payloadJson, stepKey? }`.
**Per (event, caller)**, so two managers mid-edit never overwrite each other. The payload is never parsed:
it is the wizard's in-progress form, half-filled steps included, and validating it would refuse exactly the
state autosave exists to preserve (only size and "is it JSON" are checked). Stored as `jsonb`, so **content
round-trips intact — including fields the server knows nothing about — but the exact bytes do not**:
whitespace and key order are normalised.
**GET answers 204 when nothing is saved** — a state, not an error. A draft can never modify its event.
Stranger → 404 (D-018), non-manager → 403.
