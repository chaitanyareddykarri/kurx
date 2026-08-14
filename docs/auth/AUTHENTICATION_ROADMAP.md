# Kurx Authentication — Roadmap

**This is the ONLY roadmap.** Phases are defined here and nowhere else. `PHASE_*_SUMMARY.md` files are
**historical records of completed work** — never a source of phase definitions. Where any other document
disagrees with this file, this file wins and the other is drift to be corrected.

**Updated:** 2026-07-22 · Backend Phases 1–2F **and** client Phases 3–5 (Web/Admin/Flutter, Features 1–5)
**COMPLETE**. Next: **Phase 6 (E.164 + user migration)**. · **Structure:** the approved **7-phase
authentication-implementation plan**
(supersedes the earlier 10-phase draft; the mapping is recorded at the bottom).

**Rule:** update this file in the same change that moves any phase. Never after, never in a separate pass.

---

## Scope rule — authentication ≠ infrastructure

This roadmap contains **only the code required to finish the authentication *system***. Deployment and
operations are tracked separately (see **Operational ledger** below) because they are owner- or
hardware-blocked and orthogonal to whether the auth code is complete. A phase can be "done" without an AWS
account.

## Hard invariants (enforced by tests in the phase that builds them)

- **INV-A — Trusted Browser never bypasses the password.** A trusted browser replaces **factor 2 only**;
  the password is always factor 1. A cookie that skipped the password would be a bearer credential for the
  whole account.
- **INV-B — Password reset is never OTP-alone.** Reset requires **OTP + (trusted-device approval OR
  recovery code)**. With no trusted device: OTP + recovery code.
- Plus the standing platform invariants: no platform authority in the JWT · live role checks (D-015/D-040)
  · refresh reuse revokes the session family · single-use challenge · first-approval-wins (D-181) · decoy
  anti-enumeration · hidden resource → 404 not 403.

---

## At a glance

| Phase | Name | Status | % |
|---|---|---|---|
| 1 | Verify the current uncommitted authentication work | ✅ COMPLETE | 100% |
| **2** | **Complete backend authentication** (six gated sub-phases) | ✅ COMPLETE | 100% |
| 2A | Trusted Browser | ✅ COMPLETE | 100% |
| 2B | Login orchestration (password as factor 1) | ✅ COMPLETE | 100% |
| 2C | Password reset ceremony | ✅ COMPLETE | 100% |
| 2D | Registration ceremony (incl. email verification) | ✅ COMPLETE | 100% |
| 2E | Security Center backend | ✅ COMPLETE | 100% |
| 2F | Kurx step-up integration + D-181 tests | ✅ COMPLETE | 100% (platform-endpoint wiring deferred, see note) |
| 3 | Complete Web authentication | ✅ COMPLETE | 100% (Features 1–5) |
| 4 | Complete Admin authentication | ✅ COMPLETE | 100% (staff scope; self-registration N/A) |
| 5 | Complete Flutter authentication | ✅ COMPLETE | 100% (Features 1–5) |
| 6 | E.164 + user migration | ⏳ PARTIAL — **NEXT** | ~40% |
| 7 | Regression, docs, internal security review, production-ready code | ⏳ NOT STARTED | 0% |

**Each 2x sub-phase is independently gated** — it does not advance to the next until backend build, backend
tests, web typecheck+build, admin typecheck+build, `flutter analyze`, `flutter test`, documentation, and a
phase report all pass. `AUTHENTICATION_API.md` is frozen at the **end of Phase 2** (after 2F) before any
client phase (3–5) begins.

**Predecessor.** Modules AM0–AM10 (the trusted-device platform) are **merged to main** via PR #10
(`71bf18c`). Phases 1–2 of the earlier draft (password foundation + system, D-125…D-129) and the developer
workspace are **implemented on the working branch** and were verified green in Phase 1. This programme
finishes the *system* on top of that substrate.

---

## Dependency graph

```
1 ──► 2 ──┬──► 3 ──┐
          ├──► 4 ──┼──► 6 ──► 7
   (6 E.164 code)  │
          └──► 5 ──┘
```

| Edge | Why |
|---|---|
| 1 → 2 | Nothing is trustworthy until the tree is green; Phase 2 builds on verified code |
| 2 → 3,4,5 | Clients build against a **frozen** contract (`AUTHENTICATION_API.md`), never a moving one |
| 3,4 → 5 | Web and admin are cheap to iterate and prove the contract twice before the expensive, hardware-blocked mobile surface |
| 3,4,5 → 6 | User migration cannot begin while any client still needs the legacy passwordless path |
| 6 → 7 | Readiness is assessed on the migrated system, not the branch |

**Critical path:** 1 → 2 → 5 → 6 → 7. Flutter is on it because factor 3 exists only on mobile and it
carries the one externally blocked dependency.

---

## Phase 1 — Verify the current uncommitted authentication work

**Purpose.** Establish a green baseline. Nothing on this branch had ever been container-tested; the Phase 3
challenge-hardening code (D-181) was written but unverified.

**Scope.** Run every gate; fix **only** verification failures; write the owed decision entry. No new
features.

**Status.** ✅ **COMPLETE — 100% (2026-07-21)** · ADR **D-181**

**Completed.**
- Backend build `-c Release -warnaserror`: **0 / 0** (container, real Postgres 17).
- Backend suite: **521 passed / 522 discovered / 1 skipped / 0 failed** (4 m 48 s). `DEV_AUTH` build:
  **11 DevAuthTests passed**.
- Web typecheck + lint + build green; admin typecheck + lint + build green.
- `flutter analyze --no-fatal-infos` 0 errors/warnings (63 infos); `flutter test` 201 pass / 1 pre-existing
  fail (`guest_browse_test`, excluded).
- **Fixed one stale test** — `The_waiting_client_is_pushed_the_login_status_without_polling` approved by
  signing the bare nonce with no match number, which the D-181 contract correctly rejects, so the realtime
  "approved" push never fired. Brought onto the established `SignMatched(key, nonce, matchNumber)` pattern
  (test-only; assertion unchanged).
- Wrote **D-181** (the Phase 3 hardening ADR the code already depended on), corrected the auth docs' stale
  "D-130" references (D-130 belongs to the Event-V3 program, which reserves D-130…D-180).

**Remaining.** None. **Do not repeat** — corrections land as new `D-NNN`, not re-analysis.

**Risks.** The D-181 concurrency logic passes its happy-path and replay tests but has **no dedicated
race/cap/bare-nonce negative test** yet — pulled into Phase 2.

---

## Phase 2 — Complete backend authentication (six gated sub-phases)

**Purpose.** Assemble the primitives into the ceremonies the architecture describes. Until this phase a
password exists and a login exists and they have never met. **Each sub-phase is its own gate** (full
verification + docs + report) and does not advance until green. `AUTHENTICATION_API.md` is frozen at the
end of 2F.

**Dependencies.** Phase 1.

### Phase 2A — Trusted Browser  ✅ COMPLETE (2026-07-22, D-127)

**Purpose.** Make factor 2 real as a self-service capability. The `trusted_browsers` table existed but no
code read or wrote it; now `TrustedBrowserService` owns its lifecycle.

**Completed.** `ITrustedBrowserService` (issue / verify / list / revoke / revoke-all / cascade — `revoke-all`
and its route were later removed as unused, D-289), the
`kurx_tb` cookie contract (HttpOnly+Secure+SameSite; only the SHA-256 is stored), cascade revocation wired
into `PasswordService.ChangeAsync` **and** recovery redemption, and the management endpoints
(`GET /v1/auth/trusted-browsers`, `POST /{id}/revoke`, `POST /revoke-all`). **10 new tests**, all green:
issue→verify, wrong/empty/cross-user token rejected, expiry, revoke, revoke-all, current-marking,
404-not-403 owner scoping, password-change cascade, hash-only storage. Backend **531/532 (0 failed)** +
**11 DevAuth**; web/admin/flutter gates unchanged and green.

**Verified INV-A holds:** the cookie is factor 2 only — the cascade tests prove a re-secure event drops it,
and no path lets it satisfy the password. **Issuing the cookie at login (Set-Cookie on "remember this
browser") and reading it as factor 2 in the flow is Phase 2B** — 2A built and tested the service + surface.

**Scope / deliverables.**
- `ITrustedBrowserService` — `IssueAsync` (mint opaque token, store SHA-256, set expiry), `VerifyAsync`
  (constant-time hash match, unexpired, unrevoked; touch `LastUsedAt`), `ListAsync`, `RevokeAsync(id)`,
  `RevokeAllAsync`, `RevokeAllForCascadeAsync(reason)`.
- **Cookie contract** — `HttpOnly` + `Secure` + `SameSite=Lax`, opaque token, bound to factor 2 only
  (**INV-A**: never skips the password). Config TTL key.
- **Cascade revocation** wired into the existing surfaces that re-secure an account: `PasswordService.ChangeAsync`
  and recovery redemption revoke the user's trusted browsers (`RevokeReason = password_changed | recovery`).
- **Management endpoints** — `GET /v1/auth/trusted-browsers`, `POST /v1/auth/trusted-browsers/{id}/revoke`,
  `POST /v1/auth/trusted-browsers/revoke-all` (authenticated). *(`revoke-all` was removed in D-289 — it
  shipped but no client ever called it.)*
- **Tests** — issue → verify → expire → revoke → revoke-all → cascade-on-password-change; unknown/tampered
  token rejected; a revoked/expired browser fails verification; cross-user revoke is 404-not-403.

**Explicitly deferred to 2B.** Wiring the cookie *into the login flow* (issuing on "remember this browser"
at session issuance, and reading it to satisfy factor 2). 2A builds and tests the service + management
surface; 2B consumes it.

**Risks.** Cookie flags are a security surface where a mistake is silent — asserted in tests, not eyeballed.

### Phase 2B — Login orchestration (password as factor 1)  ✅ COMPLETE (2026-07-22, D-182)

`POST /v1/auth/login/password` (factor 1) → trusted-browser check (factor 2 satisfied → session; else device
challenge) → digits → signature → session; issue the trusted-browser cookie on "remember this browser";
anti-enumeration parity in **shape and timing**; risk-engine hooks. Every factor-skip must fail a test.

### Phase 2C — Password reset ceremony  ✅ COMPLETE (2026-07-22, D-182)

**INV-B (enforced + tested).** `POST /v1/auth/password/reset/{start,complete}`: OTP + (device step-up OR
recovery code); OTP-alone returns 403. Reset cascade-revokes browsers + sessions and issues a fresh
session. 6 tests incl. the OTP-alone-fails case.

### Phase 2D — Registration ceremony (incl. email verification)  ✅ COMPLETE (2026-07-22, D-182)

**Email verification built** (`POST /v1/auth/email/verify/{start,complete}`, new `users.EmailVerifiedAt`
column) + `GET /v1/auth/registration/status` reporting the remaining steps (verify email → create
password → complete profile → enroll device). Phone verified implicitly by the OTP login. 5 tests.

### Phase 2E — Security Center backend  ✅ COMPLETE (2026-07-22, D-182)

`GET /v1/auth/security-center` (factor/credential overview), `/activity` (the user's own security-event
history), `/sign-out-all` (revoke every session + trusted browser). The per-factor management endpoints
(password/email/phone/browsers/devices/passkeys/recovery/sessions) already exist and are not duplicated.
3 tests.

### Phase 2F — Kurx step-up integration + D-181 tests  ✅ COMPLETE (2026-07-22, D-182)

Reusable `StepUpGuard.RequireAsync` primitive (a high-risk action requires a recent device step-up;
no-device users exempt), applied to recovery-code minting; the same call is how platform endpoints adopt
step-up. **Dedicated D-181 tests landed**: bare-nonce rejection, 3-attempt cap → challenge rejected,
first-approval-wins under concurrent approvals.

> **Deferred (deliberate):** wiring `StepUpGuard` into the *platform* endpoints (event creation, payouts,
> ownership transfer, team management) edits modules owned by the concurrent Event-V3 workstream. Per the
> "auth only, leave Event-V3 alone" constraint — and because that workstream is actively editing those
> files — the primitive is delivered and documented; per-endpoint wiring is a follow-up for the module
> owners. This is the only scoped-out item in Phase 2.

---

## Phase 3 — Complete Web authentication

**Purpose.** Give the browser every screen the new flow requires.

**Dependencies.** Phase 2 contract frozen.

**Deliverables.** Password step in sign-in; create / change / forgot / reset; trust-this-browser consent +
list; match-digit display wired to SignalR; account-locked screen; **full Security Center (GitHub-style)**;
registration ceremony screens (email verify → phone verify → create password → complete profile →
enroll-device prompt); step-up prompts on sensitive web actions. Client policy validation reuses the
server's `ValidatePolicy` shape. See `AUTHENTICATION_UI.md`.

**Status.** ✅ **COMPLETE (2026-07-22)** — Features 1–5: password-first `otp-panel`, `registration-flow`
(`/register`, `/onboarding` redirects here), `password-manager`/`reset-password-panel`, `security-center`
(replaced `security-manager`), `step-up-required`. Verified: tsc + lint + production build + live curl E2E.

**Risks.** No frontend test suite — tsc/lint/build + live E2E is the substitute.

---

## Phase 4 — Complete Admin authentication *(strictest)*

**Purpose.** Bring the admin console onto the same factors — as the highest-value target, the strictest
treatment, not the lightest.

**Dependencies.** Phase 2 contract frozen; should follow Phase 3 to reuse proven patterns.

**Deliverables.** Password step; forgot / reset; trusted browsers; Security Center (sessions / devices /
recovery / login-history); step-up on sensitive admin operations. No trusted-browser shortcut beyond
factor 2.

**Status.** ✅ **COMPLETE (2026-07-22)** — staff scope: password-first login, `/account` (create/change
password), `/reset`, `/security` Security Center (server-side reads, token stays server-side), cross-device
step-up. Self-registration / email verification / passkey login are **N/A by design** (staff are
provisioned). Verified: tsc + lint + production build.

---

## Phase 5 — Complete Flutter authentication *(critical path)*

**Purpose.** The mobile app is the approving device. Without it, factors 2 and 3 do not exist for real
users.

**Dependencies.** Phase 2 contract frozen; the D-181 signed-payload contract final.

**Deliverables.** Password screens; **two-digit confirmation UI**; **signing `"{nonce}.{NN}"`** matching the
frozen contract; create / change / forgot / reset; trusted-browser view; account-locked; mobile Security
Center; registration ceremony; step-up prompts; Keystore / Secure-Enclave integration **code**. Backend and
Flutter ship the signed-payload change **together**. See `AUTHENTICATION_UI.md`.

**Status.** ✅ **COMPLETE (2026-07-22)** — Features 1–5: `password_login_page` (primary), D-181
match-digit entry + `{nonce}.{NN}` signing in `approve_login_page`, `registration_flow_page` (replaced
`onboarding_page`), `password_page`/`password_reset_page`, `security_page` (browsers/activity/step-up),
same-device `step_up_page`. Obsolete passwordless-initiate chain (`login_wait_controller`/
`login_waiting_page`/`Routes.loginWaiting`) **removed**. Verified: `flutter analyze` clean, 63 auth tests.
Keystore/Enclave-on-hardware remains blocked (`file_picker` / no macOS).

**Risks.** Hardware **validation** on real devices is in the operational ledger — externally blocked
(`file_picker`, no macOS). The *code* is in scope; the *validation* is not.

---

## Phase 6 — E.164 + user migration

**Purpose.** Remove the "10 digits means India" assumption and move the existing user base onto the new
architecture without locking anyone out or silently reopening single-factor login.

**Dependencies.** Phases 2–5. Blocks Phase 7.

**Deliverables.** Move every `NormalizePhone` call site to `PhoneCanonicalizer`; **delete** the legacy
helper; backfill `PhoneE164` (reconcile unparseable numbers explicitly, never drop); retire legacy `Phone`
once no reader remains; **password adoption for every existing user** (reach Create Password without lockout
and without OTP quietly becoming a login factor again); coordinated backend + mobile rollout + rollback for
the signed-payload change; retire the legacy passwordless path last. See `AUTHENTICATION_MIGRATION.md`.

**Status.** ⏳ PARTIAL — ~40% · ADR **D-089** (column + dual-read + `PhoneCanonicalizer` done; ~8 write call
sites and the backfill remain).

**Open items in detail** (folded in from the retired `AUTHENTICATION_PROGRESS.md`, 2026-08-08):

| Item | State | Note |
|---|---|---|
| E.164 write call-site cutover | ⏳ partial | `PhoneCanonicalizer` exists and is used at 12 non-test call sites; the remaining `AuthService.NormalizePhone` writers are the "10 digits ⇒ India" assumption. Deleting the helper is the completion signal. |
| `PhoneE164` backfill | ⛔ not started | `PhoneE164BackfillJob` and `POST /v1/admin/phone-backfill` **exist but have never been run**. New registrations populate `PhoneE164` directly (`AuthService.cs`), so a populated column is *not* evidence the backfill ran — legacy rows are the ones at risk. Reconcile unparseable numbers explicitly, never silently drop. |
| Retire `users.Phone` | ⛔ not started | Only once no reader remains. |
| Migration / rollback tests | ⛔ **0 tests** | The highest-risk item in the programme has no coverage. Password adoption is where a mistake either locks out the user base or silently reopens single-factor login. |

> ⚠️ **Do not re-derive the E.164 state from a phone column.** Re-normalising a stored bare-digit
> `Phone` re-homes foreign numbers to India — read `PhoneE164` first. That is the bug D-089 exists to
> retire, and it is easy to reintroduce while doing the cutover.

**Risks.** Password adoption is the highest-risk item in the programme — it is where a mistake either locks
out the user base or silently reopens single-factor login. Its own decision entry and test coverage first.

---

## Phase 7 — Regression, documentation, internal security review, production-ready code

**Purpose.** Prove the assembled system, not its parts.

**Dependencies.** Phase 6.

**Deliverables.** Full regression across all four surfaces; documentation closure (repo = single source of
truth, zero drift); **internal** security review of the auth implementation (OWASP ASVS + API, STRIDE
against the auth code) confirming every invariant; production-ready auth code.

**Status.** ⏳ NOT STARTED — 0%.

---

## Operational / Infrastructure ledger — OUT of this roadmap

Future ops tasks, mostly owner- or hardware-blocked; they gate **production go-live**, not authentication
completeness:

AWS infra apply (Terraform, 65 resources) · SNS production access + India DLT registration (OTP delivery
go-live) · KMS rollout · **ES256 production issuance cut-over** (4 call sites, gated on KMS live) · external
penetration test (AM23 external gate) · production load testing (Argon2id ~50 ms / 19 MiB per verify) ·
Android hardware validation (`file_picker` blocker) + iOS Secure Enclave compilation (no macOS) · CI/CD
deployment pipeline · distributed rate limiter (D-116).

---

## Verification standard (every phase, no exceptions)

A phase is **complete only when all of the following pass**:

✓ Backend build (`-c Release -warnaserror`) · ✓ Backend tests (container / real Postgres) · ✓ Web typecheck
· ✓ Web build · ✓ Admin typecheck · ✓ Admin build · ✓ `flutter analyze` · ✓ `flutter test` (excluding the
documented pre-existing `guest_browse_test` failure) · ✓ Documentation updated · ✓ Phase report written.

- Report test count **before → after**. Never weaken an assertion or skip a test to force green.
- A suite run must never overlap edits to the tree.
- Every ambiguous call gets a `D-NNN` **before** code depends on it. Auth-continuation decisions use
  **D-181+** (above the Event-V3 reserved block D-130…D-180).
- Update **this file** in the same change. It is the only auth status tracker — `AUTHENTICATION_PROGRESS.md`
  was retired on 2026-08-08 because a second tracker drifts by construction, and this one had already
  disagreed with it about the E.164 cutover.

---

## Mapping from the earlier 10-phase draft

| Earlier draft | This roadmap |
|---|---|
| 0 Repo analysis · 1 Foundation · 2 Password system | Predecessor work (done); verified in Phase 1 |
| 3 Trusted-browser + challenge hardening | Code verified in **Phase 1** (D-181); trusted-browser + dedicated tests in **Phase 2** |
| 4 Backend integration | **Phase 2** (+ registration ceremony, Security Center backend, Kurx step-up integration — added) |
| 5 Web · 6 Admin · 7 Flutter | **Phases 3 · 4 · 5** |
| 8 E.164 · 9 Migration | **Phase 6** |
| 10 Regression/security/prod | **Phase 7** (internal review) + **Operational ledger** (infra, external pentest, load, hardware) |
