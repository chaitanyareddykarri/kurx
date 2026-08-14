# Kurx Authentication — API Contract

Every authentication endpoint: request, response, auth requirement, errors, breaking changes.

**Error model.** RFC7807 `ProblemDetails` everywhere, with `error` (stable machine-readable code) and
`correlationId` extensions. Clients switch on `error`, never on the human-readable message.

**Status-code conventions.**

| Code | Meaning |
|---|---|
| `400` | Policy or input failure — **the user can act on it** |
| `401` | Wrong or missing credential |
| `403` | Authenticated but not permitted |
| `404` | Not found **or deliberately hidden** (D-018 — never 403 for a hidden resource) |
| `423` | Account locked — the client shows a wait, not a retry |
| `429` | Rate limited |

**Rate limiting.** The `otp` policy guards every expensive or identifier-taking route: OTP request,
login start, password set/change, recovery start. Argon2id is deliberately expensive and must never be
freely spinnable. ⚠ The limiter is **in-process** — N× the intended limit across N tasks (D-116).

**Anti-enumeration.** `/passkeys/login/options` and `/recovery/start` return an
**identical response shape** whether or not the identifier exists.

**Phone format.** Identifiers and phone fields are canonical **E.164** with the leading `+` (D-290). A
national-format number with no `+` is read in the legacy `IN` region and will not match a foreign account —
send E.164. Responses read the canonical column, so a phone always states its country.

**Legend.** ✅ shipped · ⚠ code written but unverified · ⛔ not built

---

## 1. Core auth — `/v1/auth`

### `POST /v1/auth/otp/request` ✅
Anonymous. Rate-limited. **Bootstrap and recovery only — never a login factor in the target flow.**

Request `{ phone }` → Response `{ ok: true }` (always, regardless of existence).

⚠ **Delivery is dormant** — `SMS_PROVIDER=console` in every environment. Blocked on AWS SNS production
access and India DLT registration.

### `POST /v1/auth/otp/verify` ✅ *(deprecated as login in Phase 6)*
Anonymous. Request `{ phone, code }` → `{ access_token, refresh_token, user }`.

Errors: `400 invalid_code`, `400 code_expired`, `429`.

> **Deprecation.** This is single-factor login and is retained only until every client migrates. Phase 6
> retires it as a login path; it survives as registration bootstrap and as one half of recovery.

### `POST /v1/auth/refresh` ✅
Anonymous. Request `{ refreshToken, deviceId?, signature? }` → new token pair.

**Sender-constrained (PoP).** For device-bound sessions a bare refresh string is rejected with
`401 proof_required`; the caller must sign the refresh token with the enrolled device key.

**Reuse detection:** replaying a rotated token revokes the whole session **family**, writes a
`security_event` and alerts (D-081).

Errors: `401 proof_required`, `401 invalid_signature`, `401 invalid_token`.

### `POST /v1/auth/logout` ✅
Request `{ refreshToken }` → `{ ok: true }`.

### `GET /v1/me` ✅ · `GET /v1/usernames/availability` ✅ · `POST /v1/me/phone/verify` ✅
Profile and identifier utilities. `/v1/me` requires a bearer token.

---

## 2. Password — `/v1/auth/password` ✅ *(Phase 2)*

**All routes authenticated.** Both mutating routes are rate-limited on the `otp` policy.

| Method | Route | Body | Success |
|---|---|---|---|
| `GET` | `/status` | — | `{ has_password, min_length, max_length }` |
| `POST` | `/set` | `{ password }` | `{ ok: true }` |
| `POST` | `/change` | `{ currentPassword, newPassword }` | `{ ok: true }` |

**`/set`** creates the first password only. A concurrent double-set is safe: the unique index on
`user_id` means the loser gets `400 password_already_set`, never two credential rows.

**`/change`** requires the current password **even with a valid session** — this is what stops a stolen
access token from silently taking ownership of the account. On success it clears any lockout, appends to
history, writes `security_events` + `audit_log`, and enqueues an outbox "your password changed" alert in
the same transaction.

**Errors**

| Code | HTTP | Meaning |
|---|---|---|
| `password_too_short` | 400 | Under 12 characters |
| `password_too_long` | 400 | Over 128 |
| `password_breached` | 400 | On the deny list |
| `password_contains_identifier` | 400 | Derived from username / email / local-part / phone |
| `password_reused` | 400 | Matches one of the last 5 |
| `password_already_set` | 400 | `/set` when a credential exists |
| `password_not_set` | 400 | `/change` before `/set` |
| `invalid_credentials` | 401 | Wrong current password |
| `account_locked` | 423 | 10 failures → 15-minute lock |

**Policy:** min 12 / max 128, no composition rules, no expiry (NIST SP 800-63B), breach deny list,
identifier-derivation refusal in **both** canonical (`+91…`) and digits-only phone form.

`ValidatePolicy` is exposed as a **pure function** on `IPasswordService` specifically so clients enforce
identical rules and never drift from the server.

---

## 3. Trusted devices — `/v1/auth/devices` ✅

All authenticated.

| Method | Route | Body | Notes |
|---|---|---|---|
| `POST` | `/enroll` | `{ name?, platform, publicKeySpki, alg?, attestationJson? }` | Returns `{ device_id, credential_id, challenge_id, nonce, match_number, expires_at }`. `alg` defaults to `ES256`. The device must sign `nonce` to finish. |
| `POST` | `/enroll/verify` | `{ challengeId, deviceId, signature }` | Proof of possession. On success the device becomes `Trusted`. |
| `GET` | `` | — | The caller's enrolled devices. |
| `POST` | `/{id}/revoke` | — | Revokes one device. |

**Enrollment signs the bare nonce**, not the match-number payload — enrollment happens on the device
itself, where there is no second screen to correlate against.

Errors: `400 invalid_signature`, `400 challenge_expired`, `400 challenge_consumed`, `404 not_found`.

---

## 4. Sessions — `/v1/auth/sessions` ✅

| Method | Route | Notes |
|---|---|---|
| `GET` | `` | Live sessions ("your devices"), for remote sign-out |
| `POST` | `/{id}/revoke` | Remote sign-out of one session |

---

## 4A. Trusted browsers — `/v1/auth/trusted-browsers` ✅ *(Phase 2A, D-127)*

A trusted browser satisfies **factor 2 only** — it never bypasses the password (**INV-A**). Only the
SHA-256 of the opaque `kurx_tb` cookie (HttpOnly + Secure + SameSite) is stored. Issuing the cookie happens
in the login flow (Phase 2B); these authenticated routes are the management surface.

| Method | Route | Notes |
|---|---|---|
| `GET` | `` | Active trusted browsers; the one presenting the `kurx_tb` cookie is flagged `isCurrent` |
| `POST` | `/{id}/revoke` | Revoke one; a browser you do not own is **404, not 403** |

**Cascade (INV-A):** a password change (`/v1/auth/password/change`) and account recovery each revoke **all**
of the user's trusted browsers automatically (`RevokeReason = password_changed | recovery`), so re-securing
the account also drops factor 2.

---

## 5. Push-approval login — `/v1/auth/login`

### `POST /start` — **REMOVED (D-289)**
The identifier-only entry to device-approval login, from before password became factor 1. It was anonymous,
took a bare identifier and pushed an approval prompt to that account's devices — an MFA-fatigue primitive on
a route no client ever called. Its decoy response existed only because it answered *before* authentication.

Use `POST /password` instead: it mints the same challenge and returns the same
`challenge_id` / `poll_token` / `match_number`, and needs no decoy because an unknown identifier and a wrong
password already return one generic `invalid_credentials`.

`match_number` is displayed on both screens so the user can confirm they are approving *their* login.

### `POST /status` ✅
Anonymous. Request `{ challengeId, pollToken }` → `{ status }` while pending/rejected/expired; adds the
token pair once `status = "approved"`.

**Poll-token binding:** knowing the challenge id is not enough — a wrong poll token is indistinguishable
from "still pending". **Single issue:** a second poll returns `consumed` and no tokens.

Live updates also arrive over the `/hubs/login` SignalR hub; polling remains the required fallback.

### `GET /pending` ✅
Authenticated. Approvals awaiting this user's trusted device, with context (IP / geo / UA) and the match
number.

### `POST /approve` ✅ **BREAKING CHANGE (D-181) — verified 2026-07-21**

Authenticated. Request:

```json
{ "challengeId": "…", "deviceId": "…", "matchNumber": 42, "signature": "base64" }
```

| Field | Change |
|---|---|
| `matchNumber` | **NEW, required.** The two digits the user read from the browser and typed on the device. Verified **server-side**. |
| `signature` | **SEMANTICS CHANGED.** Now over `"{nonce}.{NN}"`, not `nonce`. |

**Server-side checks, in order:**

1. Purpose binding — a non-Login challenge reports `404 not_found` (never reveals the ceremony)
2. Status must be `Pending` → else `400 challenge_consumed`
3. Expiry → `400 challenge_expired`
4. **Match number compared by the server** → wrong increments `match_attempts` via a conditional UPDATE
5. **3rd wrong attempt rejects the challenge outright** → `400 challenge_rejected` + critical `security_event`
6. Device must be `Trusted` → else `400 device_not_trusted`
7. ES256 verification over `"{nonce}.{NN}"` → `400 invalid_signature`
8. **First approval wins** — `UPDATE … WHERE status='Pending'`; a loser gets `400 challenge_consumed`

**New errors:** `invalid_match_number`, `challenge_rejected`.

> **Migration.** Any client that sends no `matchNumber`, or signs the bare nonce, **will fail**. Backend
> and Flutter must ship together. See [`AUTHENTICATION_MIGRATION.md`](AUTHENTICATION_MIGRATION.md).

### `POST /reject` ✅
Authenticated. Request `{ challengeId }`. A rejected challenge can never be resurrected by a late
signature.

---

## 6. Step-up — `/v1/auth/step-up` ✅

All authenticated.

| Method | Route | Body | Notes |
|---|---|---|---|
| `POST` | `/start` | `{ action? }` | `{ challenge_id, nonce, match_number, expires_at }`. `400 no_trusted_device` if the user has none — they cannot be asked to step up. |
| `POST` | `/verify` | `{ challengeId, deviceId, matchNumber, signature }` | **Same breaking change as `/login/approve`** (D-181) — verified 2026-07-21. |
| `GET` | `/status` | — | `{ satisfied, valid_until, can_step_up }` |

Step-up **strengthens an existing session and never creates one**. A Login challenge can never be
redeemed as a step-up (`404`, D-088).

---

## 7. Passkeys — `/v1/auth/passkeys` ✅

`response` members are the browser's raw `navigator.credentials` payload, passed through to fido2-net-lib
rather than reshaped into local DTOs.

| Method | Route | Auth | Body |
|---|---|---|---|
| `POST` | `/register/options` | required | — → `{ challenge_id, options }` |
| `POST` | `/register` | required | `{ challengeId, response, deviceName? }` → `{ ok, device_id }` |
| `GET` | `` | required | The caller's passkeys |
| `POST` | `/login/options` | none | `{ identifier }` → `{ challenge_id, options }`. Same shape regardless of existence. |
| `POST` | `/login` | none | `{ challengeId, response }` → token pair, or `401` |

Registration and login use **distinct challenge purposes** (`PasskeyRegister` / `PasskeyLogin`).

---

## 8. Recovery — `/v1/auth/recovery-codes`, `/v1/auth/recovery` ✅

| Method | Route | Auth | Notes |
|---|---|---|---|
| `POST` | `/v1/auth/recovery-codes` | required + **step-up** | Mints codes. Step-up gated: minting is exactly what an attacker with a stolen token wants. |
| `GET` | `/v1/auth/recovery-codes` | required | Metadata only — never the codes |
| `POST` | `/v1/auth/recovery/start` | none | Rate-limited. Identical shape regardless of existence. |
| `POST` | `/v1/auth/recovery/redeem` | none | **Requires BOTH an OTP and a recovery code.** Deliberately two-factor — this is the path that bypasses everything else. |

---

## 9. JWKS — `GET /.well-known/jwks.json` ✅

Public ES256 keys for token validation. `Active` and `Retiring` keys are published; `Retired` and
`Compromised` are not — a compromised key is removed **immediately**, with no grace period, deliberately
invalidating every token it signed.

⚠ **Issuance is still HS256.** `TokenService.CreateAccessTokenAsync` has zero callers.

---

## 10. Login orchestration, reset, registration & Security Center — Phases 2B–2F ✅ (D-182)

> Trusted browsers shipped in Phase 2A — see §4A.

### 10.1 Password-first login (2B) — `POST /v1/auth/login/password`
Anonymous, rate-limited. Body `{ identifier, password, rememberBrowser? }`. Reads the `kurx_tb` cookie.
Outcomes:
- `200 { access_token, … , user_id }` — factor 2 was a valid trusted-browser cookie → session now.
- `200 { next: "device_approval", challenge_id, poll_token, match_number, expires_at }` — client waits on
  `/login/status`; `rememberBrowser` sets the `kurx_tb` cookie on the `/status` response after approval.
- `401 invalid_credentials` (wrong password / unknown identifier / moderated — **shape *and* timing
  identical**), `423 account_locked`, `400 no_second_factor` (correct password, no browser cookie and no
  trusted device — the client routes to enrollment).

### 10.2 Password reset ceremony (2C) — INV-B
| Method | Route | Body | Notes |
|---|---|---|---|
| `POST` | `/v1/auth/password/reset/start` | `{ identifier }` | Anonymous, rate-limited, identical shape regardless of existence |
| `POST` | `/v1/auth/password/reset/complete` | `{ identifier, otpCode, newPassword, recoveryCode? }` | **OTP + second factor**: a recovery code, or a device that approved via a recent step-up. **OTP alone → `403 second_factor_required`.** Sets the password, cascade-revokes every browser + session, issues a fresh session. `401 invalid_reset`, `400 password_*`. |

*(The earlier `resetId` / `verify-otp` shape was dropped for the stateless two-call flow — D-182.)*

### 10.3 Registration & email verification (2D)
| Method | Route | Body | Notes |
|---|---|---|---|
| `GET` | `/v1/auth/registration/status` | — | `{ has_password, email, email_verified, phone, phone_verified, has_trusted_device, needs_onboarding, remaining[] }` |
| `POST` | `/v1/auth/email/verify/start` | `{ email }` | Authenticated, rate-limited. `400 invalid_email`, `409 email_taken` |
| `POST` | `/v1/auth/email/verify/complete` | `{ email, code }` | Sets `users.Email` + `EmailVerifiedAt`. `400 invalid_code`, `409 email_taken` |

### 10.4 Security Center (2E)
| Method | Route | Notes |
|---|---|---|
| `GET` | `/v1/auth/security-center` | Factor/credential overview (counts of browsers/devices/passkeys/sessions/recovery codes; step-up state) |
| `GET` | `/v1/auth/security-center/activity?limit=` | The user's own recent `security_events` |
| `POST` | `/v1/auth/security-center/sign-out-all` | Revokes every session + trusted browser → `{ revoked }` |

### 10.5 Step-up integration (2F)
`StepUpGuard.RequireAsync` is the reusable primitive — a high-risk action requires a recent device step-up;
a user with no trusted device is exempt. Applied to recovery-code minting (`403 step_up_required`);
platform endpoints adopt the same call in their own modules.

---

## 11. Breaking-change register

| # | Change | Phase | Affects | Status |
|---|---|---|---|---|
| 1 | `matchNumber` required on `/login/approve` and `/step-up/verify` | 1 (D-181) | Flutter | ✅ backend verified + tested; clients not updated (Phase 5) |
| 2 | Signed payload becomes `"{nonce}.{NN}"` | 1 (D-181) | Flutter, any device-key client | ✅ backend verified + tested; clients not updated (Phase 5) |
| 3 | Password login available (`/login/password`); mandatory-cutover retiring the passwordless path | 2B / 6 | Web, admin, Flutter | ✅ endpoint shipped (additive); mandatory cutover ⛔ Phase 6 |
| 4 | `otp/verify` retired as a login path | 9 | All clients | ⛔ not started |
| 5 | Legacy `users.Phone` column removed | 9 | Backend readers | ⛔ not started |

**Rule.** Changes 1 and 2 are a single co-ordinated release: backend and mobile ship together, or every
enrolled device fails verification.
