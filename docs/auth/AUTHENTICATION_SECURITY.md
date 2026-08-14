# Kurx Authentication — Security

Every security decision, the threat model, standards mapping, attack vectors and residual risk.

**Mandatory.** Security review is required before merge for any auth / payment / PII / KYC / bank change
(`.claude/commands/security-review.md`). Urgency never waives it.

**Legend.** ✅ implemented and tested · ⚠ implemented but unverified · ⛔ not built · 🔒 blocked externally

---

## 1. Invariants that must never regress

These are load-bearing. A change that breaks one is a security incident, not a bug.

| # | Invariant | ADR | State |
|---|---|---|---|
| 1 | Refresh reuse revokes the session **family** + `security_event` + alert | D-009/D-014/D-081 | ✅ |
| 2 | Resource roles are queried **live** per request, never trusted from a token claim | D-015 | ✅ |
| 3 | SignalR group joins **re-check** membership | D-017 | ✅ |
| 4 | A hidden resource returns **404, not 403** | D-018 | ✅ |
| 5 | Challenge **purpose binding** enforced centrally | D-088 | ✅ |
| 6 | A trusted browser satisfies **factor 2 only** — never the password | D-127 | ✅ 2A (service + cascade; login-flow enforcement 2B) |
| 7 | **OTP alone never resets a password** | Amendment D / D-182 | ✅ 2C (tested: OTP-alone → 403) |
| 8 | Match number **verified server-side** and **bound into the signature** | Amendments A/C | ✅ D-181 (verified) |
| 9 | Production secret validation **fails closed** | D-115 | ✅ |
| 10 | Errors leak no internals; no secret or PII in code, logs or config | — | ✅ |
| 11 | **First approval wins** — one challenge issues at most one session | Amendment B | ⚠ |
| 12 | Lockout applies to the **correct** password too | D-129 | ✅ |

---

## 2. Standards mapping

### OWASP Top 10 (2021)

| Risk | Controls |
|---|---|
| **A01 Broken Access Control** | Live resource-role checks (D-015); 404-not-403 (D-018); challenge scoped to owner; step-up on sensitive actions |
| **A02 Cryptographic Failures** | Argon2id (memory-hard); PHC parameters travel with each hash; only hashes stored for passwords, refresh tokens, browser tokens, recovery codes, OTPs; OTP peppered HMAC-SHA256; ES256 device signatures; KMS envelope encryption for signing keys |
| **A03 Injection** | EF Core parameterisation throughout; no dynamic SQL in the auth module |
| **A04 Insecure Design** | Three-factor model; OTP demoted; match-number anti-phishing; PoP-bound refresh; outbox so alerts survive failure |
| **A05 Security Misconfiguration** | Fail-closed secret validation; unknown provider values **throw at startup**; no default OTLP endpoint |
| **A06 Vulnerable Components** | fido2-net-lib v4, Konscious Argon2, libphonenumber — pinned; ⚠ no automated dependency scanning in CI |
| **A07 Identification & Authentication Failures** | The entire architecture. Lockout, rate limiting, single-use nonces, TTLs, anti-enumeration, no credential in a URL |
| **A08 Software & Data Integrity** | Signed challenges; signature counters for WebAuthn clone detection; transactional outbox with idempotency keys |
| **A09 Logging & Monitoring** | `security_events` + `audit_log` + OpenTelemetry (`kurx.auth.*` metrics); ⚠ no alerting rules deployed |
| **A10 SSRF** | Not applicable to the auth module |

### NIST SP 800-63B

| Requirement | Implementation |
|---|---|
| §5.1.1.2 memorised secret length ≥ 8 | **12** minimum (money + PII) |
| §5.1.1.2 permit ≥ 64 characters | **128** maximum |
| §5.1.1.2 **no** composition rules | Honoured — none |
| §5.1.1.2 **no** periodic rotation | Honoured — no expiry |
| §5.1.1.2 check against breached lists | Deny list ✅; full HIBP k-anonymity **deferred** |
| §5.1.1.2 rate-limit failed attempts | 10 → 15 min lockout |
| §5.1.1.2 salted, memory-hard storage | Argon2id, 16-byte salt |
| §5.2.2 throttle authentication attempts | ✅ per account; ⚠ in-process only across tasks |
| AAL2 / AAL3 | Multi-factor + hardware authenticator; step-up raises assurance per action |

---

## 3. Threat model

### 3.1 Credential stuffing / password spraying
**Vector.** Breached credential lists replayed at scale.
**Controls.** Argon2id makes offline cracking expensive ✅ · breach deny list ✅ · 10-failure lockout with
durable counters ✅ · rate limiting ⚠ · **a correct password alone still grants nothing** — factor 2 and 3
remain ✅.
**Residual.** The limiter is in-process → N× the intended limit across N tasks (D-116). No cross-account
IP throttle. WAF partially mitigates.

### 3.2 Real-time phishing / adversary-in-the-middle
**Vector.** Victim is on a fake site; the attacker relays credentials live and triggers a genuine push.
**Controls.** The match number is displayed on the **legitimate** browser only. A relayed victim sees the
attacker's digits, not their own. The digits are **server-verified** ⚠ and **bound into the hardware
signature** ⚠, so a signature captured for one match number cannot be replayed against another.
**Residual.** Depends entirely on the user reading the digits. ⚠ **All of this is unverified code.**

### 3.3 Push fatigue / blind approval
**Vector.** Attacker spams approvals until the user taps "yes".
**Controls.** Approval is not a tap — the user must **type** two digits they can only get from the browser
⚠, capped at **3** attempts ⚠, and exhausting the cap **rejects the challenge outright** rather than merely
failing the attempt ⚠. A critical `security_event` is written.
**Residual.** ⚠ Unverified. Rate limiting on challenge issuance should be reviewed in Phase 4.

### 3.4 SIM swap
**Vector.** Attacker ports the victim's number and receives every OTP.
**Controls.** **OTP is never a login factor** in the target design ✅ and **never resets a password alone**
⛔. Reset additionally requires a recovery code, or a device approval **bound to that specific reset**
(D-330) ✅ — the approval names the account and the request, carries a browser-held transaction token,
expires in 300 s, and is spent by one conditional UPDATE. The two-digit match number is shown only in the
browser that started the reset, so an attacker's reset cannot be approved by pushing it at the victim.
**Residual.** ⚠ `otp/verify` remains a live single-factor login path until Phase 6 — by design, it buys a
low-assurance session and every sensitive action behind it demands a step-up. Until this decision the
reset second factor was any step-up in the last 300 s, which a SIM-swap attacker could ride; that is
closed. **This section previously claimed reset required "a device approval" when no binding existed —
the claim is now true rather than aspirational.**

### 3.5 Cookie theft (XSS / malware / shared machine)
**Vector.** Stolen trusted-browser cookie.
**Controls (built 2A).** The cookie satisfies **factor 2 only** — the password is still required, so theft
is an inconvenience rather than a takeover. `HttpOnly` blocks JS access; `Secure` forces TLS; `SameSite`
limits cross-site sending. Only the SHA-256 hash is stored. Revocable individually, in bulk, and
automatically on password change or recovery (all tested).
**Residual.** Setting the cookie at login and reading it as factor 2 is Phase 2B. Token rotation on use
would narrow the window further — future work.

### 3.6 Access-token theft
**Vector.** Stolen bearer JWT.
**Controls.** 1-hour lifetime ✅ · password change requires the **current** password even with a valid
session ✅ · sensitive actions demand fresh step-up ✅ · recovery-code minting is step-up gated ✅.
**Residual.** ⚠ ES256 issuance is off — a leaked `JWT_SECRET` currently forges tokens, where ES256 would
require the private key. Cut-over gated on KMS live.

### 3.7 Refresh-token theft
**Vector.** Stolen refresh token.
**Controls.** Sender-constrained (PoP) to the device key — a bare string gets `401 proof_required` ✅ ·
rotation on use ✅ · **reuse revokes the whole family** + `security_event` + alert ✅ · stored hashed ✅.

### 3.8 Concurrent double approval
**Vector.** Two devices approve the same challenge simultaneously.
**Controls.** ⚠ Single conditional `UPDATE … WHERE status='Pending'` — Postgres serialises the row, so
exactly one caller sees a row affected. The loser receives `challenge_consumed`.
**Residual.** ⚠ **Unverified, and this is precisely the class of logic that compiles cleanly and fails
under load.** A concurrency test is mandatory before Phase 3 closes.

### 3.9 Replay
**Vector.** Captured signature or challenge replayed.
**Controls.** Single-use nonce with `UNIQUE` constraint ✅ · 120-second TTL ✅ · status transitions are
one-way ✅ · **purpose binding** so a ceremony from one rail cannot be replayed against another ✅ ·
signature binds the match number ⚠ · WebAuthn signature counters ✅.

### 3.10 Account enumeration
**Vector.** Probing which identifiers exist.
**Controls.** `/passkeys/login/options`, `/recovery/start` return **identical shapes** ✅ ·
for a device-less user **no challenge row is persisted** — the decoy is pure response shaping ✅ · "no
password set" reads as "wrong password" ✅ · purpose mismatch reports **not-found**, never "wrong
ceremony" ✅.
**Residual.** ⚠ **Timing** is not equalised. Argon2id takes ~50 ms for a real user and ~0 ms for a
non-existent one — a measurable oracle. **Phase 4 must address this** (verify against a dummy hash on the
miss path).

### 3.11 Offline cracking after database disclosure
**Controls.** Argon2id is memory-hard — the attacker's advantage is bounded by memory bandwidth rather
than raw compute, the asymmetry PBKDF2 and bcrypt lack ✅ · 16-byte per-hash salt ✅ · cost raisable later
**without invalidating existing passwords** ✅ · no plaintext or reversible material anywhere ✅.

### 3.12 Lockout abuse as denial of service
**Vector.** Attacker deliberately locks a known account.
**Controls.** Threshold **10 not 5** ✅ · fixed 15-minute window rather than escalating ✅ · a successful
change clears the run ✅ · the trusted-device factor means the attacker gains nothing anyway ✅.

### 3.13 Malicious or modified client
**Vector.** A patched app skips client-side checks.
**Controls.** **Every check is server-side.** Client validation is convenience only. The match number is
compared by the server ⚠ (an app-side check alone is a trusted-client claim), device trust state is
server-held ✅, and `RequiresMatchConfirmation` is decided centrally from the purpose so a call site
cannot opt out by passing null ⚠.

### 3.14 Insider / database access
**Controls.** Only hashes at rest across every credential type ✅ · signing-key private halves KMS-wrapped
⚠ never run against real KMS · `audit_log` + `security_events` on every transition ✅ · coarse location
only, never precise ✅.

---

## 4. Secrets

| Secret | Required in Production | Notes |
|---|---|---|
| `JWT_SECRET` | ✅ enforced | HS256 signing today |
| `OTP_PEPPER` | ✅ enforced | HMAC pepper. **No fallback literal** — an earlier defect where it silently fell back to a repo-published constant was fixed in D-115. Since **D-215** this covers *every* OTP in the platform, login included |
| `TICKET_HMAC_SECRET` | ✅ enforced | Not auth, but **enforced since D-216**: resolved via `ISecretProvider`, must be ≥32 chars and not the committed placeholder. Previously it silently fell back to `JWT_SECRET`, reusing one key across two security domains |
| `REDIS_CONNECTION` | ✅ enforced | Not a secret, but **Production refuses to start without it since D-217** (ADR-AM14) — see `docs/deployment/README.md` |
| `FCM_SERVICE_ACCOUNT_JSON` | ✅ | Push approval |
| `RAZORPAY_*` | ✅ | Not auth |

`RequiredSecrets.Names` gates startup for `JWT_SECRET` and `OTP_PEPPER`; `JwtOptions.FromSecret`
gates `TICKET_HMAC_SECRET` (D-216) and `AddKurxInfrastructure` gates `REDIS_CONNECTION` (D-217).
**Production refuses to boot** when any of them is missing or invalid — fail closed. Never bypass
this to "get a deploy out".

Terraform manages these in AWS Secrets Manager. ⚠ Never applied — no AWS account.

---

## 5. Pending security work

| # | Area | Item | Phase |
|---|---|---|---|
| 1 | Challenge | Amendments A/B/C verified (D-181, Phase 1). **Dedicated cap / concurrency / bare-nonce tests** still owed | 2F |
| 2 | Cookie | ~~Entire trusted-browser control set~~ — **built + tested in 2A**; login-flow issuance | 2B |
| 3 | Session | ~~Cascade revocation on password change and recovery~~ — **done in 2A** | ✅ |
| 4 | Recovery | ~~Reset ceremony — OTP + second factor~~ — **built + tested in 2C** (D-182) | ✅ |
| 5 | Enumeration | ~~Equalise timing on the password step~~ — **done in 2B** (`VerifyForLoginAsync` runs a real hash on a miss) | ✅ |
| 6 | Brute force | Rate-limit challenge issuance and the reset ceremony | 4 |
| 7 | Device trust | Contract version negotiation so old clients **fail closed**, not mysteriously | 3–7 |
| 8 | Device trust | Validate attestation payloads (stored but unvalidated today) | future |
| 9 | Rate limiting | Distributed limiter — in-process gives N× the limit | 10 |
| 10 | Password | HIBP k-anonymity (needs a fail-open/closed decision) | future |
| 11 | Password | **Argon2id load validation** — unmeasured under real traffic | 10 |
| 12 | Tokens | ES256 issuance cut-over — 4 call sites, gated on KMS live | 10 |
| 13 | Monitoring | Alerting rules on `security_events` (metrics exist, alerts do not) | 10 |
| 14 | Supply chain | Automated dependency scanning in CI | 10 |
| 15 | Retention | `security_events` pruning policy | future |
| 16 | Review | **AM23 full security review** — OWASP ASVS + API, STRIDE, crypto, pentest, DR | 10 |

---

## 6. Known accepted risks

| Risk | Why accepted |
|---|---|
| Breach deny list is a floor, not full coverage | HIBP deferred pending a failure-mode decision; length + factor 2/3 carry the load |
| No self-service reset without a device **or** recovery codes | The alternative is an SMS-only reset that makes every account as strong as its SIM. Support handles the tail |
| 12-character floor generates support load | Deliberate — this credential guards money movement |
| One extra password entry per sign-in vs. a silent cookie login | The difference between a stolen cookie being an inconvenience and being a takeover (D-127) |
| Reuse check costs up to 5 Argon2id verifications (~250 ms) | Rare, authenticated, rate-limited operation |
| `security_events` has no read API | Operator/CloudWatch-facing by design; `audit_log` covers the user-visible trail |

---

## 7. Blocked security work 🔒

| Item | Blocker |
|---|---|
| Android hardware validation — **factor 3 has never executed on a real device** | `file_picker ^11.0.2` blocks any release APK |
| iOS Secure Enclave | Never compiled — no macOS machine |
| KMS envelope encryption verification | No AWS account |
| Real OTP delivery | AWS SNS production access + India DLT registration (multi-day TRAI process) |
| AM23 security review | External reviewer |

> **The single most important caveat in this document:** the hardware-backed key is the foundation of the
> whole design, and **it has never run on physical hardware.** Every claim about factor 3 rests on tests
> using software-generated P-256 keys via openssl. That is a real gap, not a formality.
