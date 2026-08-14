# Kurx authentication — handover

**Status:** ✅ **merged** — commit `81b7c1c` on `main` ("feat: auth platform (AM3–AM10) + event chat (M7/M3a/M4) release checkpoint"). §12 below records the merge *decision* as it stood on 2026-07-19 and is history, not a live blocker. What remains open is operational, not code: §5 (deployment prerequisites), §6 (hardware validation), §8 (gates before enabling ES256 **issuance** — validation already accepts both generations), §9 (known limitations).
**Workstream:** trusted-device authentication re-architecture, modules AM0–AM10 (+ ratified ADRs AM11–AM22).
**Audience:** whoever runs infrastructure deployment, Android hardware validation, the AM23 security review, and production rollout.
**Date:** 2026-07-19.

This document is the reference for those four phases. It superseded `docs/SESSION_RECONCILIATION.md` (deleted 2026-08-08)
for authentication scope.

---

> ## ⚠ IN-FLIGHT: password-based authentication migration (from 2026-07-20)
>
> The architecture described below is **being extended, not replaced.** Authentication is moving from
> "identifier + trusted-device signature" to **password (factor 1) + trusted device (factor 2) +
> hardware-backed key (factor 3)**, with OTP demoted to recovery/bootstrap only. Decisions:
> **D-126** (password architecture), **D-127** (trusted browser), **D-128** (test harness + toolchain).
>
> **⚠ This table is a summary. The authoritative roadmap is
> [`docs/auth/AUTHENTICATION_ROADMAP.md`](auth/AUTHENTICATION_ROADMAP.md)** — all 11 phases, status,
> dependencies and progress. Where this document disagrees with that one, that one wins.
>
> **What changed so far (Phases 1–2 complete, verified green):**
>
> | Area | Phase | Status |
> |---|---|---|
> | `user_credentials`, `password_history`, `trusted_browsers` tables + `auth_challenges.match_attempts` | 1 | ✅ migrated & applied |
> | Argon2id hasher (OWASP m=19456/t=2/p=1, PHC format, fail-closed) | 1 | ✅ 12 tests green |
> | Username / email / E.164 all resolve to one user | 1 | ✅ implemented |
> | Password policy, lockout, reuse history, set/change/verify + `/v1/auth/password/*` | 2 | ✅ 14 tests green (D-129) |
> | Trusted-browser cookie + service | 3 | 🔨 in progress — schema in place, logic next |
> | 2-digit confirmation cap, first-approval-wins race fix, match-number signature binding | 3 | 🔨 in progress — schema in place, logic next |
> | Password reset ceremony (Amendment D), password as factor 1 on login | 4 | ⏳ pending |
> | Web / Admin / Flutter password UI | 5–7 | ⏳ pending |
>
> **Three ratified amendments that change what is written below:**
> 1. **The 2-digit code is verified server-side and bound into the signed payload** — an app-side check
>    alone would be a trusted-client claim any modified client could skip.
> 2. **Password reset never accepts OTP alone.** With trusted devices: OTP **+** (device approval **or**
>    recovery code). Without: OTP **+** recovery code. This preserves the existing AM7 two-factor
>    recovery property rather than regressing it.
> 3. **A trusted browser never skips the password** — it satisfies factor 2 only (D-127).
>
> **Verification environment changed.** Local `dotnet test` is blocked by Windows Smart App Control
> (`0x800711C7`); authoritative runs happen in a .NET 10 SDK container. `dotnet ef` works there, so the
> hand-written-migration constraint recorded elsewhere in this repository **no longer applies** (D-128).

---

## 0. How to read the evidence in this document

Every factual claim below carries one of these tags. Nothing is asserted without one.

| Tag | Means |
|---|---|
| **VERIFIED** | Observed directly this session (command run, output read). |
| **VERIFIED BY TESTS** | Covered by an integration test that ran green against real Postgres. |
| **VERIFIED BY REVIEW** | Read in source and reasoned about. Not executed. |
| **PENDING HARDWARE** | Depends on a physical Android/iOS device. No device was ever available. |
| **PENDING DEPLOYMENT** | Depends on deployed AWS infrastructure. No AWS account was ever available. |
| **UNVERIFIED** | Written but never compiled, run, or reviewed end to end. |

> **No runtime claim in this document was fabricated.** Where something was not executed, it says so.
> Two whole surfaces — Android hardware behaviour and all AWS infrastructure — have **never been
> executed at all**, and are marked accordingly throughout.

---

## 1. Completed architecture

Authentication has **three credential rails** converging on one session model.

```
  ┌─ Rail 1: phone OTP ──────────────┐
  │  bootstrap + fallback + recovery │──┐
  └──────────────────────────────────┘  │
  ┌─ Rail 2: device key (ES256) ─────┐  │   ┌──────────────────┐
  │  push-approval login, step-up    │──┼──►│  session issuance │──► access JWT (1h)
  └──────────────────────────────────┘  │   │  auth_sessions    │    + refresh (30d,
  ┌─ Rail 3: WebAuthn passkey ───────┐  │   └──────────────────┘      rotating, PoP-bound)
  │  browser + platform authenticator│──┘
  └──────────────────────────────────┘
```

**Rail 1 — phone OTP (legacy, retained).** CSPRNG codes, HMAC-peppered in `otp_codes`, single-use,
5-min TTL, attempt-capped, rate-limited. Retained deliberately: it is the bootstrap for a user with
no device yet, and one half of account recovery. *(VERIFIED BY TESTS)*

**Rail 2 — device key.** The device generates a P-256 keypair in Android Keystore / iOS Secure
Enclave; the private key never leaves hardware. Enrollment is two-step proof-of-possession
(`PendingVerification` → sign nonce → `Trusted`). Login is push-approval: the browser calls
`/start`, the trusted device receives an FCM push, the user confirms a **match number** shown on
both screens, and the device signs the challenge nonce. *(VERIFIED BY TESTS with real openssl
P-256 keys; the Keystore/Enclave half is PENDING HARDWARE)*

**Rail 3 — WebAuthn passkeys.** fido2-net-lib v4. Registration is authenticated, login anonymous.
Raw `navigator.credentials` payloads pass through to the FIDO2 library rather than being reshaped
into local DTOs. *(VERIFIED BY TESTS; browser ceremony VERIFIED in Chrome via virtual authenticator)*

### Cross-cutting mechanisms

| Mechanism | What it does | Evidence |
|---|---|---|
| **Challenge purposes** (D-098) | Every challenge is bound to one of `Login`/`StepUp`/`DeviceEnroll`/`PasskeyRegister`/`PasskeyLogin`, checked centrally in `ChallengeService`. A ceremony from one rail cannot be replayed against another. | VERIFIED BY TESTS |
| **PoP refresh tokens** (D-081) | Refresh tokens are sender-constrained to the enrolled device key. A stolen token alone is not usable. | VERIFIED BY TESTS |
| **Session families** (D-081) | Refresh reuse revokes the whole family, writes a `security_event`, and alerts — narrower than D-014's revoke-everything, so one stolen token doesn't sign you out of every device. | VERIFIED BY TESTS |
| **Device lifecycle FSM** (ADR-AM12) | `PendingRegistration → PendingVerification → Trusted → Suspended → Revoked → Compromised`. Every transition writes `security_event` + `audit_log`. | VERIFIED BY TESTS |
| **Risk engine** (ADR-AM22) | Layered on `IFraudService`, drives step-up demands and denials. | VERIFIED BY TESTS |
| **Step-up / AAL** (AM6) | Strengthens an existing session; never creates one. Gates recovery-code minting. | VERIFIED BY TESTS |
| **Recovery codes** (AM7) | Redemption requires **both** an OTP and a recovery code — deliberately two-factor, since it is the path that bypasses everything else. | VERIFIED BY TESTS |
| **Anti-enumeration** | `/login/password` answers an unknown identifier and a wrong password with one generic `invalid_credentials`; `/passkeys/login/options` and `/recovery/start` return an identical shape whether or not the identifier exists. (`/login/start` carried this too until it was removed — no client ever called it.) | VERIFIED BY TESTS |
| **Outbox** (ADR-AM16) | Security-critical notifications go through `outbox_messages`, drained every minute, so a "new sign-in" alert survives a push failure. | VERIFIED BY REVIEW |
| **ES256 / JWKS** (D-099) | Signing-key FSM `Pending→Active→Retiring→Retired` (+`Compromised`); public keys served at `/.well-known/jwks.json`. **Validation only — issuance is still HS256.** See §8. | VERIFIED BY TESTS |
| **KMS envelope encryption** (D-102a) | Private signing keys wrapped with AES-GCM under a KMS CMK; plaintext zeroed in `finally`. | VERIFIED BY REVIEW; PENDING DEPLOYMENT |
| **Secrets abstraction** (D-101a) | `ISecretProvider` — configuration or AWS Secrets Manager. Startup fails closed if a required secret is unreadable. | VERIFIED BY REVIEW |
| **Telemetry** (D-100) | `Kurx.Auth` ActivitySource + Meter; `kurx.auth.attempts`, `kurx.auth.security_events`, `kurx.auth.operation.duration`. No default OTLP endpoint by design. | VERIFIED BY REVIEW |
| **One-time code as second factor** (D-280/D-283) | Password is always factor 1; the backend returns the ranked `methods[]` an account actually holds and clients render it without a list of their own. A code is bound to its challenge by OTP id, so a code minted by another ceremony cannot satisfy this one. | VERIFIED BY TESTS |
| **Server-owned delivery channel** (D-281/D-282/D-284) | Phone codes over SMS (SNS), email codes over SES; WhatsApp is transactional only and never carries authentication. `email_otp` is offered only for a **verified** address — an unverified one may belong to someone else. | VERIFIED BY TESTS |
| **Global phone identity** (D-089/D-290) | Identity is canonical E.164. `NormalizePhone` honours a leading `+` and applies no length heuristic; the legacy bare-digit column is never re-normalized (it would re-home foreign numbers to India). Every client phone input goes through the shared country picker. | VERIFIED BY TESTS |
| **Trusted browser issued at both points** (D-182/D-291) | `kurx_tb` is set by `/login/password` *and* by `/login/second-factor/verify`; both browser clients now send credentials on each, so "remember this browser" works on the one-time-code path rather than failing silently. | VERIFIED BY REVIEW |

---

## 2. Completed modules

| Module | Scope | Status | Evidence |
|---|---|---|---|
| **AM0** | Schema substrate: 8 tables, E.164 columns, config | Complete | VERIFIED against real Postgres 17 |
| **AM1** | `ISmsProvider` (Console + SNS w/ India DLT), `IOtpService` | Complete, **delivery dormant** | VERIFIED BY TESTS; live SMS PENDING DEPLOYMENT |
| **AM2** | Device enrollment, keypair, challenge-response core | Complete | VERIFIED BY TESTS + driven E2E with real EC keys |
| **AM3** | WebAuthn / passkeys | Complete | VERIFIED BY TESTS + Chrome virtual authenticator |
| **AM4** | Push-approval login | Complete | VERIFIED BY TESTS + driven E2E |
| **AM5** | Device-bound sessions, PoP refresh, "your devices" | Complete | VERIFIED BY TESTS |
| **AM6** | Step-up / AAL | Complete | VERIFIED BY TESTS |
| **AM7** | Recovery codes | Complete | VERIFIED BY TESTS |
| **AM8** | Anti-abuse → `IFraudService`, risk engine | Complete | VERIFIED BY TESTS |
| **AM9** | Clients: Flutter + Next.js | **Backend/web complete; native PENDING HARDWARE** | see §3 |
| **AM10** | ES256/JWKS, secrets, KMS, OpenTelemetry, IaC | Code-complete | VERIFIED / PENDING DEPLOYMENT |

**Ratified ADRs AM11–AM22** were satisfied inside the above, with one exception: **AM14 (Redis
mandatory, fail-closed) is not implemented** — see §9.3.

**Test coverage:** 79 auth-specific test methods in `TrustedDeviceAuthTests.cs` *(VERIFIED — counted)*.
Full backend suite last observed at **432/432 passing, 0 failed** *(VERIFIED BY TESTS, earlier this
session)*. **Not re-run in this audit:** a concurrent session had a .NET SDK container running, and
two simultaneous runs against the shared `kurx_test` database corrupt each other. Re-run before merge.

**Build:** `dotnet build Kurx.sln` → **0 warnings, 0 errors** *(VERIFIED this session)*.

---

## 3. Client status

| Surface | Status | Evidence |
|---|---|---|
| **Next.js web** | Complete — login-waiting, passkey sign-in, security manager, recovery | VERIFIED in browser |
| **Flutter (Dart)** | Complete — token store, device-key service, passkey service, auth retry, push, login-wait state machine | VERIFIED (compiles, widget tests pass) |
| **Android native (Kotlin)** | Written — `DeviceKeyPlugin`, `PasskeyPlugin`, `MainActivity` | Compiles. Runtime behaviour **PENDING HARDWARE** |
| **iOS native (Swift)** | Written — `DeviceKeyPlugin.swift` | **UNVERIFIED — never compiled.** No macOS machine. |

> **iOS is the weakest artifact in this workstream.** It has never been through a compiler. Treat it
> as a draft, not a deliverable.

---

## 4. Security guarantees

These hold **given** the deployment prerequisites in §5 are met. Each names what would break it.

1. **A stolen access token cannot mint new account access.** Recovery-code generation requires
   step-up. *Breaks if:* step-up is bypassed for users who can step up. *(VERIFIED BY TESTS)*
2. **A stolen refresh token alone is unusable.** PoP binds it to the device key. *Breaks if:* the
   attacker also extracts the hardware-backed private key. *(VERIFIED BY TESTS)*
3. **Refresh-token theft is detected and contained.** Reuse revokes the family, logs a
   `security_event`, and raises a CloudWatch alarm at threshold zero. *(VERIFIED BY TESTS; alarm
   PENDING DEPLOYMENT)*
4. **Account existence does not leak** from any anonymous auth endpoint. *(VERIFIED BY TESTS)*
5. **A ceremony cannot be replayed across rails.** Challenge purpose is checked centrally, not per
   call site. *(VERIFIED BY TESTS — this was a real defect, found and fixed as D-098)*
6. **Platform authority is never trusted from a token.** Admin/reviewer/finance roles are read live
   per request, so revocation is effective immediately. *(VERIFIED BY TESTS)*
7. **Private signing keys are not usable from a database dump** — when `SIGNING_KEY_PROTECTION=kms`.
   *Breaks if:* deployed with `none`. The app refuses `none` in Production. *(VERIFIED BY REVIEW;
   PENDING DEPLOYMENT)*
8. **Secrets never enter Terraform state or CI logs.** Terraform creates containers; values are
   populated out-of-band. *(VERIFIED BY REVIEW)*
9. **A compromised API task cannot exfiltrate the database outward.** Data stores sit in isolated
   subnets with no internet route; the DB security group has no egress rule at all. *(VERIFIED BY
   REVIEW of Terraform; PENDING DEPLOYMENT)*

---

## 5. Deployment prerequisites

**Blocking — nothing can deploy without these:**

1. **AWS account** + S3 bucket and DynamoDB table for Terraform state.
2. **ACM certificate** for the domain, same region.
3. **ECR repository** with an image pushed under an immutable tag.
4. **A registrable domain.** This becomes the WebAuthn RP ID. **Choose once — changing it
   invalidates every existing passkey.** This is the single least reversible decision in the
   workstream.

**Blocking for real OTP delivery (long lead time — start now):**

5. **AWS SNS production access** (out of sandbox).
6. **India DLT registration** — Entity ID + Template IDs. A multi-day-to-multi-week TRAI process.
   Until both land, `SMS_PROVIDER` stays `console` and **no OTP is actually delivered to a user**.

**Blocking for Android passkeys:**

7. **Play App Signing enrolment**, to obtain the release SHA-256 certificate fingerprint.
8. **`assetlinks.json` served** at `https://<domain>/.well-known/assetlinks.json`, returning **200
   directly with `application/json` — no redirect.** Google's verifier does not follow redirects.

**Configuration to set at deploy time:**

| Variable | Production value |
|---|---|
| `SECRETS_PROVIDER` | `aws` |
| `SIGNING_KEY_PROTECTION` | `kms` |
| `AWS_KMS_SIGNING_KEY_ID` | from `signing_key_kms_arn` output |
| `AWS_SECRETS_PREFIX` | `kurx/<env>/` |
| `WEBAUTHN_RP_ID` | the domain |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | ADOT sidecar |
| `REDIS_CONNECTION` | ElastiCache endpoint — **see §9.3, not currently enforced** |
| `OTP_PEPPER` | `openssl rand -base64 32` — **see §9.2, not currently enforced** |

Secrets to populate out-of-band after the first apply: `JWT_SECRET`, `TICKET_HMAC_SECRET`,
`OTP_PEPPER`, `FCM_SERVICE_ACCOUNT_JSON`, `RAZORPAY_*`.

---

## 6. Hardware validation prerequisites

**A physical Android device is required. An emulator is not acceptable** and
`scripts/android-validation.sh` refuses to run on one *(VERIFIED — the refusal was tested live)*.

Emulators do not have real StrongBox/TEE, real biometric hardware, or real Credential Manager
behaviour. A pass on an emulator would be evidence of nothing.

**Needed:**

1. A physical Android device, **API 28+**, with a hardware-backed keystore, biometrics enrolled, and
   a Google account signed in (for passkeys).
2. A **release-signed** build — not debug. Passkeys are bound to the signing certificate.
3. `assetlinks.json` live at the production domain (prerequisite 8 above).
4. `file_picker` resolved — **see §9.1. No release APK can currently be built.**

The 15-case matrix is in `docs/mobile/HARDWARE_VALIDATION.md`. Nothing in it has been executed.

---

## 7. Remaining tasks

### 7a. Deployment

- [ ] Provision Terraform state backend (S3 + DynamoDB).
- [ ] `terraform apply` to **staging**, reviewing the plan resource by resource. Never straight to prod.
- [ ] Populate secrets out-of-band.
- [ ] Point DNS at `alb_dns_name`; serve `assetlinks.json`.
- [ ] Subscribe on-call to `alerts_topic_arn`.
- [ ] Confirm `jwks_url` returns a well-formed key set.
- [ ] **Confirm the KMS health check passed in task logs** — it proves the task role holds *both*
      `GenerateDataKey` and `Decrypt`, the pair most often half-granted.
- [ ] Verify OTel traces reach CloudWatch/X-Ray.
- [ ] Apply to production.

### 7b. Production rollout (strictly ordered)

1. **ES256 issuance cut-over** — gates in §8.
2. **E.164 migration phases 5–6** — drop the legacy phone column once dual-write is proven.
3. **Remove `JWT_SECRET`** — only once no HS256 token can still be live (≥ access-token TTL after
   step 1, with margin).
4. **Enable real OTP delivery** — flip `SMS_PROVIDER=sns` once SNS + DLT land.
5. **Load testing** — particularly the signing path; KMS adds latency per key unwrap.
6. **DR testing** — restore from RDS snapshot; rehearse signing-key compromise (`Compromised` state).

---

## 8. Gates before enabling ES256 issuance

**ES256 issuance is currently OFF.** Not by a flag — by call graph:
`TokenService.CreateAccessTokenAsync` (ES256) **has zero callers**; all four issuance sites call the
synchronous HS256 `CreateAccessToken` *(VERIFIED this session by grep)*.

Cut-over is therefore a **code change at four call sites**, not a config toggle:
`AuthService.cs:345`, `LoginApprovalService.cs:183`, `PasskeyService.cs:258`,
`RecoveryCodeService.cs:107`.

**Do not enable until every gate below passes:**

| # | Gate | Why |
|---|---|---|
| 1 | Infrastructure applied; `SIGNING_KEY_PROTECTION=kms` confirmed live | Otherwise production tokens are signed by keys sitting unprotected in Postgres — strictly worse than HS256 today. |
| 2 | **KMS health check passed in task logs** | Proves both `GenerateDataKey` and `Decrypt` are granted. A half-grant fails only at first unwrap — i.e. at first login after cut-over. |
| 3 | `/.well-known/jwks.json` publicly reachable, well-formed, correct `kid` | Every verifier depends on it. |
| 4 | An `Active` signing key exists and has completed one full rotation in staging | Proves the FSM, not just the happy path. |
| 5 | All verifiers migrated to JWKS and confirmed accepting ES256 | Any verifier still HS256-only breaks at cut-over. |
| 6 | Dual validation confirmed live: an HS256 token minted **before** cut-over still validates **after** | This is the entire zero-downtime property. Test it, don't assume it. |
| 7 | Load test on the signing path | Per-token key unwrap is the new hot path. |
| 8 | Rollback rehearsed in staging | Reverting the four call sites must restore HS256 with no token invalidation. |
| 9 | **AM23 security review passed** | Crypto changes are exactly what it exists to catch. |

**Rollback:** revert the four call sites. Tokens already issued as ES256 stay valid — JWKS still
serves the key, and validation accepts both. Rollback is safe in both directions **only while
`JWT_SECRET` is still present**; step 7b.3 makes it one-way.

---

## 9. Known limitations and open defects

### 9.1 `file_picker` blocks every release build — BLOCKER, not owned by this workstream

`mobile/pubspec.yaml:61` declares `file_picker: ^11.0.2`; the generated registrant references
`com.mr.flutter.plugin.filepicker.FilePickerPlugin`, which does not resolve *(VERIFIED)*.

**No release APK can be built.** That blocks Play App Signing, `assetlinks.json` fingerprints, and
therefore **all Android hardware validation**. It arrived with concurrent chat/attachment work.
**Owner: the concurrent session.**

> **§9.2, §9.3 and §9.4 below are RESOLVED** — kept for the record of what was found and why it
> mattered. §9.2 fixed by D-115 (pepper now resolved through `ISecretProvider` with no fallback,
> `OTP_PEPPER` added to `RequiredSecrets`, dev value made explicit configuration). §9.4 fixed by
> D-115 (dead flag deleted). §9.3 resolved as a decision, not code: D-116 supersedes ADR-AM14 for
> durable state (Postgres is the right home for it) and carries the distributed rate limiter forward
> as scoped future work. A fourth class of defect — implemented backends with no reachable UI — was
> found during the UI audit and fixed in D-117; see §9.6.

### 9.2 `OTP_PEPPER` is not enforced in production — SECURITY DEFECT *(RESOLVED — D-115)*

`OtpService.cs:31` falls back to the literal `"dev-only-otp-pepper-change-me"` when `OTP_PEPPER` is
unset, and `RequiredSecrets.Names` contains **only** `JWT_SECRET` *(VERIFIED)*. `.env.example`
claims the pepper "becomes REQUIRED (fail-closed at startup)". **It does not.**

**Impact:** a production deploy that forgets `OTP_PEPPER` hashes every OTP under a pepper published
in this repository, silently. No error, no log. The pepper's entire purpose is defeated.

**Fix:** add `"OTP_PEPPER"` to `RequiredSecrets.Names` — one line. **Not applied**: the instruction
for this phase was to implement nothing, and this changes startup behaviour (a prod deploy without
the pepper would begin refusing to boot) immediately before a deployment phase. Needs an explicit
decision. **Recommend fixing before the first production apply.**

### 9.3 ADR-AM14 (Redis mandatory, fail-closed) is not implemented

`DependencyInjection.cs:110-113` silently falls back to `AddDistributedMemoryCache()` when
`REDIS_CONNECTION` is unset *(VERIFIED)*. `.env.example` claims fail-closed enforcement. It does not exist.

**Impact is narrower than the ADR feared,** and the reason matters: auth services do **not** use
`IDistributedCache` at all *(VERIFIED)*. Challenges, OTP attempt caps, single-use enforcement and
velocity state all live in **Postgres**, which is shared across instances and therefore correct
under autoscaling.

The real gap is the **ASP.NET rate limiter**, which is in-process. Across 2–10 Fargate tasks the
effective limit is **N× the configured limit**. Partially mitigated by the WAF's per-IP rate limit
on `/v1/auth/*`, which is global — so this is a defence-in-depth weakening, not an open door.

**Recommend:** fix the enforcement gap (or consciously retire ADR-AM14 with a new D-NNN), and treat
the distributed rate limiter as AM23 input.

### 9.4 `AUTH_TRUSTED_DEVICE` is dead configuration — DOCUMENTATION DEFECT

The flag is referenced **only in comments and `.env.example`. No code reads it** *(VERIFIED)*. All
auth endpoints are mapped unconditionally at `Program.cs:345-352`. Source comments and the migration
still describe these tables and endpoints as "inert until `AUTH_TRUSTED_DEVICE`" — **false**. The
trusted-device rail is live the moment the app boots.

This is a documentation defect with a deployment consequence: anyone reading the current comments
would believe they can deploy with the rail disabled. **They cannot.**

**Fix:** either implement the gate or delete the flag and correct the comments. **Recommend
deleting** — the rail is tested and additive, and a flag that has never been exercised is not a
safety mechanism, only the appearance of one.

### 9.6 UI reachability *(RESOLVED — D-117)*

The module work verified that screens were *built*; it never verified they were *reachable*. They
largely were not:

- **Five Flutter auth screens had zero references anywhere** — approve-login, login-waiting,
  recovery, security and step-up. They compiled, so no build ever complained.
- **`enrollThisDevice` had no callers**, so the entire device-key rail was unusable on mobile: no
  enrollment meant no approval and no step-up.
- **This device's trusted-device id was never persisted**, so the screens that must name a signing
  device could not have worked even once reachable.
- **The mobile step-up gate was a dead end** — the 403 told users to confirm their identity without
  offering the screen that does it.
- **Web's `login-waiting.tsx` was rendered by nothing**, leaving push-approval login (AM4) fully
  built and unreachable.

All wired up in D-117. **This is the class of gap to watch for in the remaining phases**: "the tests
pass" and "the endpoint works" say nothing about whether a user can reach the feature.

### 9.5 Non-defect limitations

- **iOS native is uncompiled** (§3). Needs a macOS machine before it is worth reviewing.
- **All AWS infrastructure is unapplied.** `terraform validate` passes against the real provider
  schema; that proves the config is well-formed, **not that the infrastructure works**. IAM
  sufficiency in particular is unproven.
- **No OTP is delivered to any real user** until SNS + DLT land.
- **Duplicate `D-105`** in `docs/DECISIONS.md` — used for two unrelated decisions *(VERIFIED)*.
  Owned by the concurrent session.
- **Frontend has no automated test suite** (repo-wide, pre-existing).

---

## 10. Freeze assessment

| Dimension | Status |
|---|---|
| **Feature complete** | **Yes**, for AM0–AM10 on backend + web. Every planned rail, mechanism and endpoint is implemented and test-covered. |
| **Defect free** | **Yes**, for all known defects. §9.2/§9.4 (documentation claiming protections that did not exist) fixed in D-115; §9.3 resolved as a decision in D-116; §9.6 (implemented backends with no reachable UI) fixed in D-117. |
| **Deployment pending** | **Yes.** Nothing has ever been applied. No AWS account existed. |
| **Hardware validation pending** | **Yes.** No physical device ever existed; blocked additionally by §9.1. |
| **Production rollout pending** | **Yes.** ES256 issuance off; E.164 phases 5–6 outstanding; real OTP delivery dormant. |

### Recommendation

**FROZEN.** All known implementation defects are resolved (D-115, D-116, D-117). No "must fix before
merge" item remains inside the authentication workstream.

Evidence at freeze: backend **447/447 passing, 0 failed** against real Postgres; `dotnet build`
0 warnings / 0 errors; Flutter auth suite **58/58**, analyzer **0 errors**; `web` typecheck clean and
`next build` green; `admin` typecheck clean and `next build` green.

**Next, in order:** infrastructure deployment → Android hardware validation → AM23 → production
rollout. Feed §8 (ES256 gates), §9.2 and §9.3 into AM23 as known inputs; they are exactly the class
of finding it exists to catch, and it should not have to rediscover them.

**Two blockers remain outside this workstream**, both owned by the concurrent chat session:
`file_picker` (no release APK can be built, which blocks all Android hardware validation) and
duplicate decision numbers in `docs/DECISIONS.md` (D-105 and now D-114).

---

## 11. Reference

| Topic | Document |
|---|---|
| Every decision D-081…D-117 | `docs/DECISIONS.md` |
| API contracts | `docs/api/README.md` |
| Production providers & secrets | `docs/deployment/PRODUCTION_PROVIDERS_CHECKLIST.md` |
| Infrastructure | `infra/terraform/README.md` |
| Android hardware matrix | `docs/mobile/HARDWARE_VALIDATION.md` |
| Passkey deployment | `docs/mobile/PASSKEY_DEPLOYMENT.md` |
| Release signing | `docs/mobile/RELEASE_SIGNING.md` |
| Configuration | `.env.example` |

---

## 12. Merge status — ✅ RESOLVED (historical record)

> **This section is closed.** The merge happened: `81b7c1c` on `main`. Everything below is the
> evidence and reasoning as of 2026-07-19 — kept because it documents what was verified before the
> merge, **not** because anything here is still pending. The test figures in it are that day's and
> have been superseded many times over; see `auth/AUTHENTICATION_TESTING.md`.

Final verification was run on 2026-07-19 against the working tree. **Everything that could be run,
passed.** The blocker was not quality — it was that two workstreams shared one branch and one working
tree.

### Verification results (2026-07-19)

| Check | Command (matches `.github/workflows/ci.yml`) | Result |
|---|---|---|
| Backend build | `dotnet build Kurx.sln -c Release -warnaserror` | ✅ **0 warnings / 0 errors** |
| Backend tests | `dotnet test Kurx.sln` (container, real Postgres) | ⛔ **could not run** — see below |
| Web typecheck | `npm run typecheck` | ✅ clean |
| Web lint | `npm run lint` | ✅ **no ESLint warnings or errors** |
| Web build | `npm run build` | ✅ green |
| Admin typecheck | `npm run typecheck` | ✅ clean |
| Admin build | `npm run build` | ✅ green |
| Flutter analyzer | `flutter analyze` | ✅ **0 errors** (63 info-level style lints, pre-existing) |
| Flutter tests | `flutter test` | ⚠️ **201 passed, 1 failed** — pre-existing `guest_browse_test`, reproduced on a clean baseline, not caused by auth |

Hygiene: no conflict markers, **no TODO/FIXME in any auth module**, no debug code, no unintended
deletions. (`web/public/workbox-*.js` shows as delete+add — a content-hashed PWA bundle regenerated by
running `next build`. Tracked build-output churn, not an intentional change.)

**Last known-good backend result: 447/447, 0 failed**, earlier the same day. That run predates the
concurrent session's latest chat changes, so it does not certify the current tree.

### Why the merge is blocked

**1. An auth-only commit is not constructible.** Of ~189 modified files, ~38 belong to the concurrent
chat workstream, and five files contain both workstreams' changes interleaved:

| File | auth lines added | chat lines added |
|---|---|---|
| `docs/DECISIONS.md` | 359 | 162 |
| `backend/Kurx.Infrastructure/DependencyInjection.cs` | 32 | 9 |
| `backend/Kurx.Api/Program.cs` | 25 | 6 |
| `backend/Kurx.Infrastructure/Persistence/KurxDbContext.cs` | 7 | 6 |
| `mobile/lib/main.dart` | 1 | 1 |

Splitting them needs hunk-level surgery, and the result would not compile: the chat entities
registered in `KurxDbContext` are required by chat code in the same tree.

**2. Half the auth workstream is already committed and pushed under another commit.** `2dde486`
(message `"@"`, *"platform M7/M3a/M4-A (D-101..D-103) + auth AM0-AM4 (two concurrent workstreams)"*)
already contains `AuthService`, `OtpService`, `ChallengeService`, `TrustedDeviceService`,
`LoginApprovalService`, the AM0 migration and `TrustedDeviceAuthTests`. It is on `origin`, and the
local branch is level with it. **The auth workstream therefore cannot be delivered as one clean
commit** — its first half is already in shared history, mixed with platform work.

**3. The backend suite cannot be run while the other session is testing.** Both use the shared
`kurx_test` database; concurrent runs corrupt each other (observed earlier in the session as
`23505 pg_database_datname_index`).

**4. Merging would ship the other workstream's unfinished work.** `feat/production-rearchitecture` is
shared, and already carries their in-flight commit.

### Recommended sequence

1. The chat session commits its Phase 5 work first — it is theirs to verify and describe.
2. Auth remainder (AM5–AM10 + freeze fixes D-115/116/117) commits cleanly on top, since the shared
   files would then hold only auth deltas.
3. One uninterrupted full backend run.
4. Push → CI → merge.

**Alternative:** a joint checkpoint commit covering both workstreams, as `2dde486` already did. Honest
about what the tree is, but it needs the other session's sign-off.

### Notes for whoever executes it

- ~~**CI has no mobile job**, so the failing `guest_browse` Flutter test will not block CI.~~
  **No longer true.** `ci.yml` now carries a `mobile` job (`flutter analyze --no-fatal-infos` +
  `flutter test`, Flutter 3.44.6), so a Flutter failure blocks CI like any other.
- `docs/DECISIONS.md` has **two duplicate numbers**: `D-105` and `D-114`, each used by both
  workstreams. Renumbering is the chat session's call, not this one's.
- No commit, push, tag or merge has been performed by this workstream.
