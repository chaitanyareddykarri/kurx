# Kurx Authentication — Testing

Strategy, environment, commands, baseline, known failures and coverage gaps.

---

## 1. Strategy

**Backend: integration tests only, against a real PostgreSQL `kurx_test` database. Persistence is never
mocked.** Tests run through `WebApplicationFactory<Program>`, so every test exercises real routing, real
DI, real EF and real SQL.

Rationale: the auth module's correctness lives in database semantics — unique constraints resolving
races, conditional `UPDATE`s serialising concurrent approvals, partial indexes, cascade behaviour. A
mocked repository proves none of it and would have hidden every bug worth catching.

**Rules.**
- Cover the **error and authorization branches**, not just the happy path.
- **Never weaken an assertion or skip a test to force green.**
- Cross-class parallelisation stays **disabled** — the database is shared and reset between classes.
- Report test count **before → after** for every change.
- Frontend has **no suite**. Browser verification is the substitute — say so explicitly rather than
  implying coverage that does not exist.

---

## 2. Environment

### ⚠ Local Windows `dotnet test` does not work and never will

Windows Smart App Control blocks loading `Kurx.Infrastructure.dll` (`0x800711C7`). A full local run fails
**100%** for that reason alone — not one of those failures is a code defect. **Never diagnose a local test
failure as a code bug without ruling this out first** (D-082, D-128).

`dotnet build -c Release -warnaserror` **does** work locally and is the correct pre-push gate.

### The authoritative environment is a container

.NET 10 SDK container sharing the Postgres container's network namespace, with `ArtifactsPath` isolating
Linux build output from the Windows tree.

```bash
# 1. Bring up the database
docker compose -f D:/event/Kurx/docker-compose.yml up -d postgres redis

# 2. Build and test inside the SDK container
docker run --rm \
  --network container:kurx-postgres \
  -v D:/event/Kurx/backend:/src -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -c "dotnet build Kurx.sln -c Release -warnaserror -p:ArtifactsPath=/tmp/artifacts --nologo && \
           dotnet test Kurx.sln --no-build -c Release -p:ArtifactsPath=/tmp/artifacts \
           --logger 'console;verbosity=normal'"
```

### Exact commands that produced the Phase 1 baseline (reproducible)

Run against the compose stack (`kurx-postgres` healthy on `localhost:5432`). These are the verbatim
commands behind the 2026-07-21 baseline; they mirror the CI jobs in `.github/workflows/ci.yml`.

```bash
# 1. Backend build + full suite
MSYS_NO_PATHCONV=1 docker run --rm --network container:kurx-postgres \
  -e JWT_SECRET=ci-only-secret-not-for-production-0123456789abcdef \
  -e TICKET_HMAC_SECRET=ci-only-ticket-hmac-secret-not-for-prod \
  -e OTP_PEPPER=dev-only-otp-pepper-change-me \
  -v "/d/event/kurx:/src" -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  "dotnet build Kurx.sln -c Release -warnaserror -p:ArtifactsPath=/tmp/artifacts --nologo -v q && \
   dotnet test  Kurx.sln --no-build -c Release -p:ArtifactsPath=/tmp/artifacts --logger 'console;verbosity=minimal'"
#   → Build 0/0 · Passed 521 / Discovered 522 / Skipped 1 / Failed 0 · 4 m 48 s

# 2. Web / Admin (host, Node 20 in CI; local Node 24 is fine for typecheck+build)
cd web   && npm run typecheck && npm run lint && NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 npm run build
cd admin && npm run typecheck && npm run lint && NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 npm run build

# 3. Flutter (host; Flutter 3.44.6 in CI)
cd mobile && flutter pub get && flutter analyze --no-fatal-infos && flutter test
#   → analyze 0 errors/warnings (63 infos) · test 201 pass / 1 pre-existing fail (guest_browse_test)
```

**CI equivalents** (`.github/workflows/ci.yml`): job `backend` = step 1; job `web` = step 2 (web);
job `admin` = step 2 (admin); job `mobile` = step 3. **PR-to-main CI is the authoritative verifier** for
anything that cannot be run locally.

### Signing in locally — the real flow, no shortcuts

There is no developer login, seeded identity, or OTP bypass in any build or environment (D-274).
Local sign-in is the production flow; the only difference is where the code is delivered. The dev
providers are console-backed (`ConsoleSmsProvider`), so `POST /v1/auth/otp/request` writes the real
code to the API log:

```bash
docker compose logs -f backend | grep 'sms→console'
#   [sms→console] to=+919876543210 text=Your Kurx code is 481920. It expires in 5 minutes.
```

Enter that code at `POST /v1/auth/otp/verify` (or in web/admin/Flutter's own sign-in screen) and the
session is a completely ordinary one. To reach the admin console, that account then needs a platform
role: the first `SuperAdmin` comes from `SUPERADMIN_BOOTSTRAP_PHONE` (see
[`../DECISIONS.md`](../DECISIONS.md) D-274), every one after it from `POST /v1/admin/staff/grant`.

### Rules that cost real time when ignored

1. **Never let a suite run overlap edits to the tree.** Doing so once produced a spurious 435-failure run
   — a `DbContext` compiled with new entities executing against a database migrated without them. That
   was operator error, not instability.
2. **Two sessions cannot run the suite simultaneously.** `kurx_test` is shared; a concurrent run corrupts
   it with `23505`.
3. **`docker compose up -d --force-recreate` recreates from the STALE image.** Rebuilt code is silently
   ignored and new endpoints 404 while old ones work. Use:
   ```bash
   docker compose -f D:/event/Kurx/docker-compose.yml build backend
   docker rm -f kurx-backend
   docker compose -f D:/event/Kurx/docker-compose.yml up -d --no-deps backend
   ```
4. **Confirm what is actually deployed** via Swagger, not by grepping the DLL — .NET string literals are
   UTF-16, so `grep -a` gives false negatives:
   ```bash
   curl -s http://localhost:5080/swagger/v1/swagger.json | grep -oP '"/v1/auth[^"]*"'
   ```

### The harness fix that made any of this trustworthy (D-128)

Before Phase 1, **36 of 489** tests failed before any product-code change. Every failure was a
database-lifecycle error (`23505`, `3D000`, `55006`, `42P04`), never an assertion. Both affected classes
passed 100% in isolation, proving order-dependence rather than breakage.

**Root cause:** `KurxApiFactory.ResetDatabase()` issued `pg_terminate_backend(...)` and `DROP DATABASE` as
two separate statements. xUnit disposes the previous class's factory asynchronously, so its Hangfire
server (20 workers) reconnected inside that gap and the drop failed.

**Fix:** `DROP DATABASE … WITH (FORCE)` (PostgreSQL 13+) terminates sessions and drops atomically, plus a
process-level lock and bounded retry on the four known race SQLSTATEs. **36 failures → 0**, verified on an
untouched tree.

---

## 3. CI

`.github/workflows/ci.yml` — jobs: **backend** (build `-warnaserror` + full suite against a real Postgres
service, then the API-contract drift gate and the two response-declaration gates),
**web** (typecheck / lint / test / build), **admin** (typecheck / lint / test / build),
**mobile** (`flutter analyze --no-fatal-infos` / `flutter test`, Flutter pinned to 3.44.6).

A Flutter failure **does** block CI. (It did not until the mobile job was added; older notes in
`docs/AUTHENTICATION_HANDOVER.md` predate it.)

**PR-to-main CI is the authoritative test verifier** for anything that cannot be run locally.

---

## 4. Current baseline

> ⚠️ **The backend row below is stale and is not a floor you can measure against today.** It records
> **1297 discovered** on 2026-08-05; the tree now declares **1500 `[Fact]`/`[Theory]` attributes across
> 145 classes**, and `.claude/CLAUDE.md` §9 carries a different figure again (1433, measured 2026-08-06
> on the shared branch). Three numbers, three dates, one suite.
>
> **Do not substitute a count of attributes for a run.** A `[Theory]` contributes one attribute and many
> cases, so an attribute count is an undercount of discovered tests and is not comparable to the numbers
> below. The only honest baseline is a **full-suite run in the container** (`.claude/memory/testing-standards.md`
> — the Windows host runner is blocked by Application Control, and the suite is order-sensitive, so a
> single class in isolation proves nothing). Re-measure and replace this block rather than citing it.

| Surface | Result | When |
|---|---|---|
| Backend build (`-warnaserror`) | **0 warnings / 0 errors** | 2026-07-21, container, verified |
| Backend suite (Release) | **565 passed / 566 discovered / 1 skipped / 0 failed** — 4 m 42 s | 2026-07-22 (Phase 2A–2F), real Postgres 17, verified |
| Backend suite (current) | **1296 passed / 1 skipped / 0 failed of 1297** — 16 m 13 s | 2026-08-05, isolated worktree at `HEAD`+D-274, verified |
| Web | typecheck + lint + build green | 2026-07-21, verified |
| Admin | typecheck + lint + build green | 2026-07-21, verified |
| `flutter analyze --no-fatal-infos` | 0 errors / 0 warnings (63 info lints) | 2026-07-21, verified |
| `flutter test` | 201 pass / **1 pre-existing fail** (`guest_browse_test`) | 2026-07-21, verified |
| `flutter build apk` | **BLOCKED** | `file_picker ^11.0.2` unresolvable (operational) |

**Baseline movement:** 452 (broken harness) → 488 (harness fixed, D-128) → 500 (+12 hasher tests) →
501 discovered after Phase 2's 14 tests → **522 discovered / 521 passing after Phase 1 verified the Phase 3
hardening (D-181)** → **532 / 531 after Phase 2A (+10 `TrustedBrowserTests`)** → … → **1297 / 1296 at
D-274**, which removed the 13 `DevAuthTests` and added 7 `SuperAdminBootstrapTests`. Since D-274 there is
no separate `DEV_AUTH` build: Debug and Release discover the same tests.

> ### ✅ PHASE 1 — CURRENT TREE VERIFIED (2026-07-21)
> The Phase 3 challenge-hardening code (D-181: match-number binding, 3-attempt cap, first-approval-wins)
> now **executes green** on real Postgres. One realtime test (`The_waiting_client_is_pushed_the_login_status_without_polling`)
> was stale — it approved by signing the bare nonce with no match number, which the new contract correctly
> rejects, so the "approved" push never fired and the SignalR wait timed out. Fixed to the established
> `SignMatched(key, nonce, matchNumber)` pattern (test-only). Full suite then: **521 / 522, 0 failed.**
> Dedicated negative tests for the cap, the concurrency race, and bare-nonce rejection are Phase 2 (below).

---

## 5. Test inventory

| Suite | Tests | Covers |
|---|---|---|
| `TrustedDeviceAuthTests.cs` | 78 | Enrollment, challenges, push-approval login (now started password-first via `StartApprovalLoginAsync`), sessions, PoP refresh, step-up, recovery, passkeys. The `/login/start` decoy test went with its route (D-289); `/login/password` anti-enumeration is covered by `PasswordLoginTests` |
| `AuthTests.cs` | 18 | Legacy OTP auth, refresh, logout |
| `PasswordSystemTests.cs` | 14 | Policy, lockout, history, set/change, authz |
| `TrustedBrowserTests.cs` | 9 | Factor-2 trusted browser (2A): issue/verify/expire/revoke/current-marking/owner-scope/password-change cascade/hash-only storage (revoke-all removed with its route, D-289) |
| `SecretProviderTests.cs` | 13 | Secret resolution, fail-closed |
| `SigningKeyProtectorTests.cs` | 12 | KMS envelope wrap/unwrap |
| `SuperAdminBootstrapTests.cs` | 7 | `/v1/dev/*` returns 404 from a booted host (D-274); first-SuperAdmin config bootstrap: no-op unset, never creates a user, grants + audits once, then locked |
| `SigningKeyTests.cs` | 9 | Key FSM, JWKS |
| `InternationalPhoneTests.cs` | 37 | Global phone identity (D-089/D-290): register + sign in from 10 numbering plans, ten-digit E.164 is not given an Indian country code, an unplaceable number is never assigned one, legacy bare national input still resolves, cross-country last-ten-digit collision, and why the canonical column is read before re-normalizing |
| `OutboxDispatchTests.cs` | 12 | Outbox dispatcher (ADR-AM16): every event type maps to a handler, an unknown type fails loudly as `Failed` + a critical security event rather than being silently dropped, idempotency key uniqueness, retry/at-least-once |
| `SesMimeBuilderTests.cs` | 9 | SES MIME construction (D-284): headers, multipart boundaries, base64 attachment encoding, RFC 2045 line limits on payload bodies |
| `TelemetryTests.cs` | 8 | `kurx.auth.*` metrics |
| `SecretValidationTests.cs` | 7 | Startup validation |
| `PasswordHasherTests.cs` | 6 | Argon2id, PHC, rehash, empty-input fail-closed |
| `HubSecurityTests.cs` | 4 | SignalR group membership |
| `PlatformRoleTests.cs` | 4 | Platform authority |

### Notable properties under test

- Anti-enumeration — identical shape, **and no challenge row persisted** for a device-less user
- Poll-token binding — the challenge id alone yields nothing
- Single issue — a second poll returns `consumed`
- Signature replay rejected
- A `Login` challenge cannot be redeemed as a step-up
- Approval by a pending or revoked device rejected
- Cross-user approval is **404, not 403**
- A rejected challenge cannot be resurrected by a late signature
- Lockout applies to the **correct** password
- Phone identifier checked in canonical **and** digits-only form (this caught a real defect)
- Concurrent password-set resolved by the unique index

---

## 6. Coverage gaps

> Phase numbers below follow the **approved 7-phase roadmap** (`AUTHENTICATION_ROADMAP.md`). The Phase 3
> hardening *code* is verified (D-181); the **dedicated negative/concurrency tests** for it are still owed and
> are pulled into Phase 2 alongside the trusted-browser and orchestration work.

| Gap | Phase | Priority |
|---|---|---|
| **Match-digit cap** — 3 wrong attempts rejects the challenge (dedicated test) | 2 | **Blocking** |
| **First-approval-wins under real concurrency** (dedicated race test) | 2 | **Blocking** — compiles + happy-path pass, race untested under load |
| **Signed-payload binding** — a signature over the bare nonce must be *rejected* (negative test) | 2 | **Blocking** |
| ~~Trusted-browser lifecycle — issue, verify, expire, revoke, cascade~~ | ✅ 2A | Covered by `TrustedBrowserTests` |
| Password-as-factor-1 — every branch that could skip it | 2 | Blocking |
| Reset ceremony — OTP alone must fail; every combination | 2 | Blocking |
| **Timing equalisation** on the password step | 2 | High |
| Migration / rollback — password adoption, phone backfill | 6 | High |
| Argon2id under load | Operational | High |
| Frontend — no suite exists at all | 3–5 | Medium |
| Device-contract version negotiation | 5 | Medium |

---

## 7. Known failures

| Failure | Status |
|---|---|
| `guest_browse_test` — "bottom nav shell switches branches" | **Pre-existing**, proven unrelated by re-running on a clean stashed baseline. Not a blocker; no mobile CI job. Excluded from the Flutter gate by prior agreement |
| 1 skipped backend test (`RefundLedgerTests.Concurrent_refunds_…`) | Intentional `[Skip]`; long-standing; not auth |
| `TrustedDeviceAuthTests.The_waiting_client_is_pushed_the_login_status_without_polling` | **FIXED in Phase 1** — was stale (bare-nonce approval under the D-181 contract). Now passes |

---

## 8. Writing tests here

- Put auth tests in the existing suites; do not create a parallel file for a variant of covered behaviour.
- Use `WithScopeAsync` to reach the `DbContext` for assertions about persisted state.
- Generate real EC keys — `ECDsa.Create(ECCurve.NamedCurves.nistP256)`. Never fake a signature.
- Sign login and step-up challenges with `SignMatched(key, nonce, matchNumber)`; enrollment signs the
  bare nonce. Getting this wrong produces a confusing `invalid_signature`.
- Assert the **error code**, not the message — codes are the contract.
- A test that would pass with the security control removed is not a security test.

**openssl equivalents for manual driving** (openssl 3.5.6 on the host):
```bash
openssl ecparam -name prime256v1 -genkey -noout -out k.pem
openssl ec -in k.pem -pubout -outform DER | base64 -w0          # SPKI for enrollment
printf '%s' "$nonce.$match" | openssl dgst -sha256 -sign k.pem | base64 -w0
```
