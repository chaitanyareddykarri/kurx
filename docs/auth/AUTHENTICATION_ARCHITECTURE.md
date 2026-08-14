# Kurx Authentication — Architecture

**Master document.** After reading this file alone, an engineer should understand the complete
authentication architecture: what it is, why each decision was made, and what it does not yet do.

**Status of this document:** describes both **shipped** reality and **approved target**. Every section
marks which is which. Never read an aspirational sentence as a description of running code.

| | |
|---|---|
| **Roadmap, phases and live status** | [`AUTHENTICATION_ROADMAP.md`](AUTHENTICATION_ROADMAP.md) — the single auth status tracker |
| **How to continue building** | [`../../.claude/CLAUDE.md`](../../.claude/CLAUDE.md) + [`../../.claude/workflows/loop-engineering.md`](../../.claude/workflows/loop-engineering.md) |
| **Endpoint contracts** | [`AUTHENTICATION_API.md`](AUTHENTICATION_API.md) |
| **Schema** | [`AUTHENTICATION_DATABASE.md`](AUTHENTICATION_DATABASE.md) |
| **Threats + controls** | [`AUTHENTICATION_SECURITY.md`](AUTHENTICATION_SECURITY.md) |
| **Screens** | [`AUTHENTICATION_UI.md`](AUTHENTICATION_UI.md) |
| **Tests** | [`AUTHENTICATION_TESTING.md`](AUTHENTICATION_TESTING.md) |
| **Rollout** | [`AUTHENTICATION_MIGRATION.md`](AUTHENTICATION_MIGRATION.md) |
| **Session handover** | [`AUTHENTICATION_HANDOVER.md`](../AUTHENTICATION_HANDOVER.md) |
| **Decision log** | `docs/DECISIONS.md` (`D-NNN`) — the binding record |

---

## 1. Design goals

1. **No single stolen artefact grants an account.** Not a password, not a cookie, not an access token,
   not a SIM. Every one of these is compromised routinely at scale; the architecture assumes each will
   be and requires a second independent thing.
2. **Phishing resistance at the approval step.** A user who is being socially engineered in real time
   must be given something concrete to notice. The two-digit match number exists for that moment.
3. **Hardware-bound identity.** The strongest factor is a private key that physically cannot leave the
   device (Android Keystore / iOS Secure Enclave), so remote compromise cannot extract it.
4. **OTP is demoted, not deleted.** SMS OTP is phishable, SIM-swappable and unreliable in India. It is
   retained only where nothing better exists — first registration and recovery — and never alone.
5. **Fail closed.** Missing secrets stop startup. Unknown providers throw. Empty passwords are rejected
   rather than thrown on. A degraded auth system must refuse, not guess.
6. **Every security claim is testable.** Invariants are covered by integration tests against real
   Postgres, not asserted in prose. See [`AUTHENTICATION_TESTING.md`](AUTHENTICATION_TESTING.md).
7. **Global from the start.** Identity is E.164, not "10 digits means India".

### Non-goals

- Not building an IdP for third parties. No OAuth server, no SAML.
- Not supporting password-only login. Ever. A password is one third of the story.
- Not optimising the login path for minimum friction. This system guards money movement and PII.

---

## 2. Authentication philosophy

Authentication is **three factors**, and no factor substitutes for another:

| Factor | Kind | Mechanism |
|---|---|---|
| **1** | Something known | **Password** — Argon2id, NIST 800-63B policy |
| **2** | Something held | **Trusted browser** binding *or* **trusted-device approval** |
| **3** | Hardware possession | **Device signature** — non-exportable P-256 key in Keystore / Secure Enclave |

The load-bearing rule, and the one most often misread:

> **A trusted browser satisfies factor 2 only. It never skips the password.** (D-127)

Read the target flow literally — "if the browser is trusted, sign in" — and the cookie becomes a bearer
credential for the entire account, making cookie theft a full takeover. That is strictly worse than the
flow being replaced. So the browser binding replaces the *device approval*, never the *password*.

Similarly:

> **OTP alone never resets a password.** Reset requires OTP **plus** a device approval **or** a recovery
> code. A user without any trusted device needs OTP **plus** a recovery code.

This preserves the two-factor property of the existing recovery path (D-083) instead of regressing it.
An SMS-only reset would make every account only as strong as its SIM.

---

## 3. Credential rails

Three rails converge on one session model. **[shipped]**

```
  ┌─ Rail 1: phone OTP ──────────────┐
  │  bootstrap + recovery only       │──┐
  └──────────────────────────────────┘  │
  ┌─ Rail 2: device key (ES256) ─────┐  │   ┌───────────────────┐
  │  push-approval login, step-up    │──┼──►│  session issuance │──► access JWT (1h)
  └──────────────────────────────────┘  │   │  auth_sessions    │    + refresh (30d,
  ┌─ Rail 3: WebAuthn passkey ───────┐  │   └───────────────────┘      rotating, PoP-bound)
  │  browser + platform authenticator│──┘
  └──────────────────────────────────┘
```

**Rail 1 — phone OTP.** CSPRNG codes, HMAC-peppered in `otp_codes`, single-use, 5-minute TTL,
attempt-capped, rate-limited. Deliberately retained: it is the only bootstrap for a user with no device,
and one half of recovery. **Delivery is dormant** — blocked on AWS SNS production access and India DLT
registration, so `SMS_PROVIDER=console` in every environment today.

**Rail 2 — device key.** The device generates a P-256 keypair in hardware; the private key never leaves
it. Enrollment is two-step proof-of-possession (`PendingVerification` → sign nonce → `Trusted`). Login is
push-approval over FCM. **Verified with real EC keys in tests; the Keystore/Enclave half has never run on
physical hardware.**

**Rail 3 — WebAuthn passkeys.** fido2-net-lib v4. Registration authenticated, login anonymous. Raw
`navigator.credentials` payloads pass through to the FIDO2 library rather than being reshaped into local
DTOs. Verified in Chrome via virtual authenticator.

---

## 4. The models

### 4.1 Password model **[shipped — Phase 2]**

- **Argon2id** via `Konscious.Security.Cryptography.Argon2`, OWASP balanced parameters
  m=19456 KiB, t=2, p=1, 16-byte salt, 32-byte output.
- Stored as full **PHC strings** (`$argon2id$v=19$m=..,t=..,p=..$salt$hash`) so parameters travel with
  each hash. Cost can be raised later **without invalidating a single existing password**: verification
  uses the parameters the hash was created with, and below-policy hashes are silently re-hashed on the
  next successful sign-in — the only moment plaintext is available.
- Lives in **`user_credentials`, not on `users`** (ADR-AM11). `users` is projected by dozens of queries
  across orgs, events, orders and admin; a hash on that entity would sit one careless `Select` away from
  a response body.
- `IPasswordHasher` is an interface for one concrete reason: production cost is ~50 ms and ~19 MiB *by
  design*, and a suite signing in hundreds of times would otherwise pressure someone into weakening the
  real parameters. The seam lets tests substitute a cheap hasher while production cost stays honest.
- Empty input **fails closed** (`Ok=false`) rather than throwing — the underlying library throws on empty
  input, which would have turned an empty-password login into a `500` distinguishable from a rejection.

**ADRs:** D-126 (architecture), D-129 (policy).

### 4.2 Password policy **[shipped]**

NIST SP 800-63B, which explicitly recommends *against* the classic rules:

| Rule | Value | Why |
|---|---|---|
| Minimum length | **12** | NIST says 8; 12 because this credential guards money and PII |
| Maximum length | **128** | Not a security limit — bounds the Argon2id work an unauthenticated caller can force (DoS lever) |
| Composition rules | **none** | Told to add a symbol, users produce `Password1!` — measurably *less* entropy |
| Forced expiry | **none** | Calendar expiry produces incrementing variants |
| Breach deny list | small, high-signal | Every entry passes a naive "12 chars with a digit" rule — that is the point |
| Identifier derivation | refused | Username / email / email local-part / phone |

**A real defect this caught:** the phone is stored canonically as `+919876543210`, so checking only the
stored form let `919876543210xyz` through — the leading `+` is absent from what a user types. Both
representations are now checked. Identifiers under 4 characters are skipped, or a 3-letter username would
reject far more good passwords than bad.

### 4.3 Lockout **[shipped]**

10 consecutive failures → 15-minute lock, counters in `user_credentials`.

- **Ten, not five**, because lockout is itself a denial-of-service lever against a *known* account, and
  the trusted-device factor already means a guessed password alone grants nothing.
- **Fixed window, not escalating backoff** — predictable for a locked-out legitimate user.
- **The lock applies to the correct password too.** Otherwise it is decorative: an attacker keeps
  learning whether each guess was right.
- Counters are **durable in Postgres, not cache**. An in-process counter resets on every deploy — exactly
  when an attacker mid-spray benefits.
- A successful *change* clears the run, so a victim of a guessing attempt is not left locked out.

### 4.4 Password history **[shipped]**

Last **5** hashes per user, pruned on write. Reuse is detected by **verifying the candidate against each
stored hash** — comparing hashes directly is meaningless when each has its own salt. NIST does not require
rotation, but a "change" must be a real change; this blocks returning to the password just abandoned,
which is the case that matters after a suspected compromise. The window is bounded so a very old password
becomes acceptable again rather than barred forever.

Cost: up to 5 Argon2id verifications (~250 ms) on a *change* — acceptable on a rare, authenticated,
rate-limited operation.

### 4.5 Trusted device model **[shipped — AM2/AM4]**

An explicit lifecycle FSM (ADR-AM12), never a bare boolean:

```
PendingRegistration → PendingVerification → Trusted → Suspended → Revoked → Compromised → Deleted
```

Every transition writes a `security_event` **and** an `audit_log` row. Public key material lives in child
`device_credentials` rows, so a key can rotate without re-enrolling the device. Both rails share this
table: `CredentialType` distinguishes `DeviceKey` from a WebAuthn credential.

Signatures are **ES256** (ECDSA P-256 / SHA-256), accepted in both DER and P1363 encodings because
platform crypto stacks disagree about which they emit.

### 4.6 Trusted browser model **[Phase 2A: service + management + cascade built; login-flow issuance in 2B]**

A browser the user chose to trust, satisfying **factor 2** for a limited window (`TrustedBrowserService`,
D-127). **INV-A: it never bypasses the password.**

- Storage mirrors refresh tokens: only `TokenHash` (SHA-256 of the opaque `kurx_tb` cookie) is persisted, so
  a database disclosure yields nothing replayable.
- Records carry browser, OS, full user agent, IP, coarse (city-level at most) location, created / last
  used / expiry, and are revocable with a reason.
- **Cascade revocation is wired (2A):** a password change and account recovery each revoke every trusted
  browser (`RevokeReason = password_changed | recovery`).
- **Built (2A):** `IssueAsync` / `VerifyAsync` / `ListAsync` (marks the current browser) / `RevokeAsync` /
  `RevokeAllAsync`, and the `GET/POST /v1/auth/trusted-browsers*` management endpoints.
- **Phase 2B:** the login flow sets the cookie on "remember this browser" and reads it to satisfy factor 2.
- Default expiry 30 days, configurable.

**Trade-off accepted:** one extra password entry per sign-in versus a silent cookie login. This is the
difference between a stolen cookie being an inconvenience and being an account takeover.

**ADR:** D-127. **Status:** table and entity exist; **no service, no cookie, no endpoint, no call site.**

### 4.7 Challenge model **[shipped, with Phase 3 hardening UNVERIFIED]**

A single-use nonce the device signs. Every challenge is bound to exactly one purpose —
`Login` / `StepUp` / `DeviceEnroll` / `PasskeyRegister` / `PasskeyLogin` — checked centrally in
`ChallengeService` so a ceremony from one rail can never be replayed against another (D-088). A purpose
mismatch reports **not-found**, so probing with a challenge id cannot reveal which ceremony it belongs to.

**The match number.** Two digits (10–99) shown on the screen that *started* the login and typed on the
device that *approves* it. Its entire purpose is to defeat blind approval under push fatigue and real-time
phishing.

Three amendments make it real rather than decorative. **All three are implemented but UNVERIFIED —
no test has run against them:**

- **A — server-side verification + attempt cap.** The digits are compared **by the server**. A client-side
  check is a trusted-client claim any modified build skips. Capped at 3 wrong attempts
  (`AuthChallenge.MaxMatchAttempts`), because two digits is only 100 possibilities. Exhausting the cap
  **rejects the challenge outright** rather than merely failing the attempt: wrong digits mean the person
  approving is not looking at the browser that started this login, which is exactly the phishing case the
  step exists to catch.
- **B — first approval wins.** The status transition is a single conditional `UPDATE … WHERE
  status='Pending'`, not a read-then-write. Two devices approving concurrently would otherwise both
  observe `Pending` and both be told they succeeded, issuing two sessions from one challenge.
- **C — the match number is bound into the signed payload.** The device signs `"{nonce}.{NN}"`, so the
  hardware signature attests to the digits the user saw. A signature captured for one match number cannot
  be replayed against a challenge showing another.

> **Device contract.** `ChallengeService.SignedPayload(nonce, matchNumber)` defines exactly what is
> signed. Mobile clients must build this string identically. Changing it is a **breaking change** that
> requires backend and mobile to ship together — see [`AUTHENTICATION_MIGRATION.md`](AUTHENTICATION_MIGRATION.md).

Enrollment carries no match number: it happens on the device itself, where there is no second screen to
correlate against, so demanding a code there would be theatre. `RequiresMatchConfirmation(purpose)` decides
this centrally, so a call site cannot opt out by passing null.

### 4.8 Sessions and refresh tokens **[shipped — AM5]**

A first-class, device-bound session replaces "the refresh token IS the session".

- `auth_sessions` rows carry `FamilyId`; refresh tokens are children.
- **Refresh reuse revokes the family**, writes a `security_event` and alerts — deliberately narrower than
  D-014's revoke-everything, so one stolen token does not sign you out of every device.
- Refresh tokens are **sender-constrained (PoP)**: bound to the enrolled device key via
  `PoPKeyThumbprint`. A stolen refresh string alone cannot rotate.
- Access JWT 1 hour, refresh 30 days rotating on use.

**Signing:** `signing_keys` implements a full ES256 lifecycle (`Pending → Active → Retiring → Retired`,
plus `Compromised`) with public keys at `/.well-known/jwks.json`. Exactly one key is `Active`;
`Retiring` keys no longer sign but still validate, which is what makes rotation zero-downtime.

> **ES256 is built but OFF, by call graph rather than by flag.**
> `TokenService.CreateAccessTokenAsync` has **zero callers**; all four issuance sites call the synchronous
> HS256 method. Cut-over is a four-call-site change, gated on KMS being confirmed live first — enabling
> ES256 while private keys sit unwrapped in Postgres is *worse* than HS256.

### 4.9 Step-up / assurance level **[shipped — AM6]**

Strengthens an existing session; **never creates one**. A normal session browses; a genuinely sensitive
action additionally demands a *fresh* device signature, so a stolen access token alone cannot perform it.
Time-boxed, not permanent. Gates recovery-code minting.

### 4.10 Recovery **[shipped — AM7]**

One-time codes, hashed at rest, consumed on use. **Redemption requires both an OTP and a recovery code** —
deliberately two-factor, because this is the path that bypasses everything else.

### 4.11 Security events and audit **[shipped]**

`security_events` is an append-only forensics feed consumed by the risk engine and operators. `UserId` is
nullable because some events precede a known user (enumeration attempts). Distinct from `audit_log`, which
is the user-visible business audit trail surfaced in the admin console.

**No admin page reads `security_events` by design** — it is operator/CloudWatch-facing. Auth writes
`audit_log`, which the existing Audit page already shows.

### 4.12 Risk engine **[shipped — AM8]**

`IRiskEngine` layered on the existing `IFraudService` (ADR-AM22): impossible travel, VPN/Tor, device
fingerprint, reputation → a dynamic assurance demand. Drives step-up requirements and outright denials.

### 4.13 Outbox **[shipped]**

Security-critical notifications are written to `outbox_messages` **in the same transaction** as the state
change and drained by a background job (ADR-AM16). A "your password changed" alert that can be lost is
exactly the alert a victim needs when it was not them.

### 4.14 Registration **[✅ built — Phase 2D, D-182]**

Phone OTP → user row → the ceremony steps are now tracked by `GET /v1/auth/registration/status`
(`remaining[]`): **email verification** (`/v1/auth/email/verify/*`, new `users.EmailVerifiedAt`), **create
password** (mandatory for the target login flow), complete profile, enroll a trusted device (recommended).
Phone is verified implicitly by the OTP login. The adoption path for the *existing* user base remains the
highest-risk item in the programme — see [`AUTHENTICATION_MIGRATION.md`](AUTHENTICATION_MIGRATION.md).

### 4.15 E.164 migration **[partially shipped]**

`PhoneCanonicalizer` wraps libphonenumber and **refuses a national number unless the caller states the
region** — no silent country guessing. `users.PhoneE164` is written alongside the legacy `Phone` column
(staged, additive, reversible — D-089).

**Done (D-290).** `AuthService.NormalizePhone` no longer hard-codes "10 digits ⇒ +91". A leading '+' is the
caller stating their country and always wins; input without one is read in `LegacyInputRegion` (`IN`) so
pre-E.164 clients keep working, but it must *validate* there rather than be assumed, and an unplaceable
number is returned as its digits rather than assigned a country. **No length heuristic remains anywhere.**

**Still open.** The dual-write is live and every read path prefers `PhoneE164`, but **no backfill has run**,
so legacy rows still carry a null canonical column and fall back to `Phone`. That fallback is why the rule
in [database-conventions](../../.claude/memory/database-conventions.md) matters: never re-normalize the
legacy column, because it states no country. Phase 6 does the data; dropping `Phone` is phases 5–6 of D-089.

---

## 5. Target login flow

**[TARGET — steps marked ✅ are shipped, ⛔ are not built]**

```
Web: user opens the sign-in panel
  │  GET /v1/auth/password/status ✅        → min/max length so the client enforces identical rules
  ▼
[Screen 1] Identifier + password — username | email | E.164
  POST /v1/auth/login/password ✅ (+ trusted-browser cookie ✅)
    DB:  AuthIdentifiers.ResolveUserAsync ✅ → users (email → phone → username fallback)
    SEC: one generic invalid_credentials for unknown identifier and wrong password alike ✅
    SEC: rate-limited on the OTP shield ✅
    (The identifier-only POST /v1/auth/login/start that used to sit here was removed in D-289:
     password became factor 1, and no client ever called the passwordless entry.)
  ▼
[Screen 2] Password — ALWAYS. No path skips this (D-127) ✅ 2B
  POST /v1/auth/login/password ✅ 2B (D-182)
    DB:  SELECT user_credentials WHERE user_id ✅
    SEC: locked? → 423 without verifying ✅ (else the lockout is decorative)
    SEC: Argon2id verify ✅; fail → FailedAttempts++; 10 → LockedUntil +15m + security_event ✅
    SEC: success → clear run, LastSuccessfulAt, rehash if below current cost ✅
    SEC: no password set reads as "wrong password" ✅ (no enumeration)
  ▼
  Trusted-browser cookie present and valid? ✅ 2B (VerifyAsync built 2A, wired into login 2B)
    DB: SELECT trusted_browsers WHERE token_hash = SHA256(cookie)
        AND revoked_at IS NULL AND expires_at > now
  ├── YES ──► factor 2 satisfied; UPDATE last_used_at ──────────────► SESSION ISSUANCE
  │
  ├── NO, and the surface/account prefers a code ──► SECOND-FACTOR CHOICE ✅ D-280
  │     The backend returns the factors this account actually holds, ranked (D-283); the client
  │     renders that list and holds none of its own. Only available methods appear.
  │     DB:   INSERT auth_challenges (purpose=SecondFactor, poll_hash in context) — no new table
  │     POST /v1/auth/login/second-factor/send   → IOtpService issues over SMS or email
  │     POST /v1/auth/login/second-factor/verify → consumed via conditional UPDATE ──► SESSION
  │     Ranking: web/admin lead with the device (the approving phone is a separate object);
  │     mobile leads with a code (the "trusted device" there is usually this device).
  │
  └── NO, and a trusted device exists ───► factor 2 + 3 via the trusted device
        DB:   INSERT auth_challenges (purpose=Login, nonce, match_number, expires_at) ✅
        Push: FCM → enrolled trusted devices ✅
        [Screen 3] Waiting — shows the two digits ✅; SignalR /hubs/login ✅, polling fallback ✅
        ▼
      Flutter — the approving device
        GET /v1/auth/login/pending ✅ → challenge + context (IP / geo / UA) + match number
        [Screen] Approve Login — shows the request context ✅
        [Screen] Enter Digits ⛔ NEW — user types the two digits shown in the browser
        Device signs  "{nonce}.{NN}"  in Keystore / Secure Enclave  ⚠ UNVERIFIED contract
        POST /v1/auth/login/approve ✅ (+ matchNumber ⚠ UNVERIFIED)
          SEC: purpose binding — wrong purpose reports not-found ✅
          SEC: status != Pending → challenge_consumed ✅
          SEC: expired → challenge_expired ✅
          SEC: SERVER compares matchNumber ⚠; wrong → MatchAttempts++ via conditional UPDATE ⚠
          SEC: 3rd wrong → challenge Rejected outright + critical security_event ⚠
          SEC: device must be Trusted, not Pending or Revoked ✅
          SEC: ES256 verify over "{nonce}.{NN}" ⚠ — binds the digits to the signature
          SEC: claim via UPDATE … WHERE status='Pending' → first approval wins ⚠
          DB:  auth_challenges → Approved, ConsumedAt, ApprovedByDeviceId + security_event ✅
        SignalR → browser ✅
        ▼
Browser: POST /v1/auth/login/status ✅
  SEC: poll-token binding — the challenge id alone yields nothing ✅
  SEC: single issue — a second poll returns "consumed" ✅
  ▼
SESSION ISSUANCE ✅
  RiskEngine ✅ → allow | demand step-up | deny
  DB: INSERT auth_sessions (family_id, trusted_device_id) ✅
  DB: INSERT refresh_tokens (hashed, SessionId, PoPKeyThumbprint) ✅
  Access JWT 1h (HS256 today ⚠, ES256 pending cut-over) + refresh 30d rotating ✅
  If "trust this browser" was ticked ✅ 2B:
      DB: INSERT trusted_browsers (SHA256(token), UA / IP / coarse geo, +30d)
      Set-Cookie: HttpOnly; Secure; SameSite=Lax; Max-Age=30d
  DB: security_event login.succeeded ✅; outbox → "new sign-in" alert ✅
  ▼
Authenticated session ✅
```

### Password reset ceremony **[✅ built — Phase 2C, D-182: OTP + (recovery code | device step-up); OTP-alone → 403]**

```
Identifier → OTP to a verified channel
          → AND (device approval OR recovery code)      ← never OTP alone
          → set new password (full policy + history check)
          → cascade-revoke every trusted browser AND every session
          → force re-login
```

A user with **no** trusted device needs **OTP + recovery code**. A user with **neither** device nor
recovery codes has no self-service route and must go through support — accepted deliberately, because the
alternative is an SMS-only reset that makes every account as strong as its SIM.

---

## 6. Database design

Twelve authentication tables. Full columns, indexes, constraints and migration history in
[`AUTHENTICATION_DATABASE.md`](AUTHENTICATION_DATABASE.md).

```
users ─┬─1:1─ user_credentials        (password, lockout counters)
       ├─1:N─ password_history        (last 5, pruned on write)
       ├─1:N─ trusted_browsers        (factor 2 — cookie binding)
       ├─1:N─ trusted_devices ──1:N── device_credentials   (public keys, both rails)
       ├─1:N─ auth_challenges         (→ approving trusted_device, SetNull)
       ├─1:N─ auth_sessions ──1:N──── refresh_tokens       (family-scoped revocation)
       ├─1:N─ recovery_codes
       └─0:N─ security_events         (UserId nullable — pre-auth events)

standalone: otp_codes (no FK — registration OTPs precede the user row)
            outbox_messages, signing_keys
```

**Conventions.** New auth tables use **UUID v7** (`Guid.CreateVersion7()`, ADR-AM13) for index locality on
high-insert tables — a deliberate deviation from D-006's v4, for **new auth tables only**; `users` and
`refresh_tokens` keep v4 and are not rewritten. Enums persist as text. JSON columns are `jsonb`.

---

## 7. API design

Full contracts in [`AUTHENTICATION_API.md`](AUTHENTICATION_API.md).

**Principles.**
- One error model everywhere: RFC7807 `ProblemDetails` with `error` and `correlationId` extensions.
- Anti-enumeration: `/passkeys/login/options` and `/recovery/start` return an identical
  shape whether or not the identifier exists.
- Status codes carry meaning: policy failure `400` (the user can act), wrong credential `401`, lockout
  `423 Locked` (so the client shows a wait, not a retry), hidden resource `404` not `403` (D-018).
- Expensive operations sit behind the `otp` rate-limit policy — Argon2id must not be freely spinnable.
- Purpose is an explicit parameter, never inferred, so no call site can silently forget the check.

---

## 8. Security decisions

Complete threat model, OWASP/NIST mapping and residual risk in
[`AUTHENTICATION_SECURITY.md`](AUTHENTICATION_SECURITY.md). The invariants that must never regress:

1. Refresh reuse revokes the family + `security_event` + alert (D-009 / D-014 / D-081)
2. Resource roles are queried **live** per request, never trusted from a token claim (D-015)
3. SignalR group joins re-check membership (D-017)
4. A hidden resource returns 404, not 403 (D-018)
5. Challenge purpose binding is enforced centrally (D-088)
6. A trusted browser never satisfies factor 1 (D-127)
7. OTP alone never resets a password (Amendment D)
8. The match number is verified server-side and bound into the signature (Amendments A/C)
9. Production secret validation fails closed — never bypass it
10. Errors leak no internals; no secret or PII in code, logs or config

---

## 9. Threat model (summary)

| Threat | Control | State |
|---|---|---|
| Credential stuffing | Argon2id + breach list + lockout + rate limit | ✅ / limiter is in-process only |
| Phishing (real-time relay) | Match number bound into the hardware signature | ⚠ unverified |
| Push fatigue / blind approval | Digits must be typed, capped at 3 | ⚠ unverified |
| SIM swap | OTP is never a login factor and never resets alone | ✅ design / ⛔ reset unbuilt |
| Cookie theft | Cookie is factor 2 only; hash-at-rest; revocable | ⛔ unbuilt |
| Access-token theft | Change requires the current password; step-up for sensitive actions | ✅ |
| Refresh-token theft | PoP sender-constraining + family revocation on reuse | ✅ |
| Concurrent double approval | Conditional UPDATE claim | ⚠ unverified |
| Replay | Single-use nonce + TTL + purpose binding + payload binding | ✅ / ⚠ |
| Account enumeration | Identical response shapes; "no password" reads as wrong password | ✅ |
| Database disclosure | Only hashes stored — passwords, refresh tokens, browser tokens, recovery codes, OTPs | ✅ |
| Offline cracking | Memory-hard Argon2id; cost raisable without invalidating hashes | ✅ |
| Lockout as DoS | Threshold 10 not 5; device factor means a guessed password grants nothing | ✅ |
| Malicious client skipping checks | Every check is server-side; client validation is convenience only | ✅ / ⚠ |

---

## 10. ADR index

Every architectural decision, cross-referenced. Full text in `docs/DECISIONS.md`.

| ADR | Subject |
|---|---|
| **D-077** | Auth re-architecture ratified; AM0 schema substrate |
| **D-078** | AM1 — OTP platform, `ISmsProvider` (SNS / India DLT), E.164 |
| **D-079** | AM2 — device enrollment + challenge-response crypto core |
| **D-080** | AM4 — push-approval login |
| **D-081** | AM5 — device-bound sessions, PoP refresh, family revocation |
| **D-082** | Local Windows `dotnet test` is not a valid verifier |
| **D-083** | AM7 — recovery codes, redeemable only as a second factor |
| **D-084** | AM6 — step-up authentication |
| **D-085** | AM8 — outbox dispatcher + risk engine |
| **D-086** | AM3 — WebAuthn / passkeys |
| **D-087** | AM9 backend — realtime login status; E.164 backfill deliberately deferred |
| **D-088** | Challenge purpose binding enforced centrally |
| **D-089** | Global phone identity — staged, additive, reversible E.164 migration |
| **D-090 … D-097** | Auth clients: Next.js, Flutter, native plugins, FCM, Android passkeys |
| **D-098** | Consistency review — the two rails were sharing challenges |
| **D-099** | ES256 signing keys, JWKS, key lifecycle |
| **D-100 / D-101a / D-102a / D-104a** | OpenTelemetry, secrets abstraction, KMS envelope encryption, Terraform |
| **D-101b / D-103a** | Android hardware validation blocked; release signing prerequisites |
| **D-114 / D-115 / D-116 / D-117** | Freeze defects: OTP pepper enforcement, ADR-AM14 superseded, UI wiring audit |
| **D-126** | Password architecture — Argon2id, PHC, separate aggregate |
| **D-127** | Trusted browser satisfies factor 2 only |
| **D-128** | Deterministic test harness + container toolchain |
| **D-129** | Password policy, lockout, reuse history |
| **D-181** | ✅ **Phase 3 challenge hardening** — match-number bound into the signed payload, server-side confirmation with a 3-attempt cap, first-approval-wins (Amendments A/B/C). Written & verified 2026-07-21 |

**Numbering note:** this decision was drafted as "D-130" in earlier notes, but **D-130 … D-180 are reserved
by the Event-System-V3 program** (D-131). The auth-continuation program uses **D-181+**; the ADR is **D-181**.

---

## 11. Future work

**Inside the programme** — see [`AUTHENTICATION_ROADMAP.md`](AUTHENTICATION_ROADMAP.md) for phases 3–10.

**Beyond it, deliberately deferred:**

| Item | Why deferred |
|---|---|
| **HIBP k-anonymity** breach checking | Adds a network dependency on the registration path; needs its own fail-open-or-closed decision |
| **Distributed rate limiter** | The current limiter is in-process → N× the limit across N tasks; WAF partially mitigates. Measure after staging (D-116) |
| **ES256 issuance cut-over** | Four call sites; gated on KMS confirmed live first |
| **Device attestation validation** | Play Integrity / DeviceCheck payloads are stored but not validated |
| **Passkey-only accounts** | Would require rethinking factor 1 |
| **`security_events` retention policy** | Append-only with no pruning today |
| **Trusted-browser token rotation on use** | Would narrow the theft window further |

**Structurally blocked — no amount of implementation resolves these:**

1. **Android hardware unvalidated.** Factor 3 — the foundation of the design — has never executed on a
   physical device. `file_picker ^11.0.2` blocks any release APK, so it cannot currently be attempted.
2. **iOS never compiled.** No macOS machine. The Swift plugin is code nobody has built.
3. **AWS infrastructure never applied.** Terraform validates 65 resources against no account.
4. **OTP delivery does not work.** Needs AWS SNS production access **and** India DLT registration — a
   multi-day TRAI process only the account owner can initiate.
5. **AM23 security review** — an external gate, not a coding task.
6. **Argon2id under load is unmeasured.** A correctness-complete system that falls over at launch traffic
   is not finished.
