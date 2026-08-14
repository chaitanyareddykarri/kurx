# Deployment

## Prerequisites

Production **fails closed** on all of the following: with `ASPNETCORE_ENVIRONMENT=Production` the API
refuses to start rather than running in a silently-degraded state. See
[secret management](../security/secret-management.md).

- A real `JWT_SECRET` (≥32 chars, not the committed dev placeholder), set via your deployment platform's secret store, never committed.
- A real **`TICKET_HMAC_SECRET`** (≥32 chars, not the committed placeholder), **distinct from `JWT_SECRET`** — it signs gate-entry tickets, and reusing the session key would mean a leak of either compromises both, while rotating `JWT_SECRET` would invalidate every outstanding ticket. **Required since [D-216](../DECISIONS.md); previously it silently fell back to `JWT_SECRET`.**
- A real **`OTP_PEPPER`** — no fallback exists; OTP issuance throws rather than hashing under a guessable value (D-115). Since [D-215](../DECISIONS.md) this covers login OTPs too.
- **`REDIS_CONNECTION`** — **required in Production since [D-217](../DECISIONS.md)** (ADR-AM14). Without it the SignalR backplane loses cross-instance fan-out, presence disables itself, and the rate limiters become per-instance (N instances ⇒ N× the configured limit) — all while the app looks healthy. Startup now refuses instead. If you genuinely intend a single instance, point it at a local Redis explicitly.
- A Postgres connection string that does **not** use the dev default password.
- `ALLOWED_ORIGINS` set to your real frontend origin(s) — the default CORS policy allows exactly this list.

Generate each secret with `openssl rand -base64 48`. Resolution goes through `ISecretProvider`, so
setting `SECRETS_PROVIDER=aws` reads them from AWS Secrets Manager instead of the environment.

## Docker Compose

The root `docker-compose.yml` runs the backend stack by default: Postgres, Redis and the API.

```bash
cp .env.example .env
# edit .env: set real JWT_SECRET, TICKET_HMAC_SECRET, OTP_PEPPER, POSTGRES_PASSWORD, ALLOWED_ORIGINS
# (compose provides REDIS_CONNECTION via its own redis service)
docker compose up --build
```

**There are no profiles: a default `up` starts the whole stack** — `postgres`, `redis`, `backend`,
`web` (`:3000`), `admin` (`:3001`) and `clamav` (`:3310`), six services in one command.

| Service | Port | Notes |
|---|---|---|
| `postgres` | 5433 → 5432 | system of record |
| `redis` | 6380 → 6379 | SignalR backplane, presence, rate limits |
| `backend` | 5080 → 8080 | the one dev API port |
| `web` | 3000 | |
| `admin` | 3001 | |
| `clamav` | 3310 | heaviest service — ~1 GB image, `start_period: 120s` while clamd loads its signature database |

Two consequences worth knowing rather than discovering:

- **A native dev server holding `:3000` or `:3001` will fail the whole `up`**, not just that one
  service. `web`/`admin` used to be profiled out for exactly this reason. If you run them natively
  with `npm run dev`, stop the container first (`docker compose up -d --scale web=0`) or stop the
  native server.
- **First `up` after a pull is slow** because of clamav's image and signature download. It is also
  what `FILE_SCANNER=clamav` and the four `ClamAvUploadPathTests` need, so having it up by default is
  what makes those pass.

Startup order is enforced by healthchecks: Postgres/Redis must report healthy before the API container starts; the API must report healthy (`GET /health` via its own healthcheck) before web/admin start. This means a broken database connection or a bad secret fails the API's healthcheck (and thus the container never reports ready) rather than silently serving broken traffic.

Uploads survive rebuilds: `STORAGE_PROVIDER=localdisk` writes under `LOCALDISK_ROOT`, which compose
mounts as the `kurx_storage` volume. Without it that path would live in the container's writable
layer, so `up --build` would destroy every uploaded file while Postgres kept the rows referencing
them. `docker compose down -v` is what deliberately clears it.

### Verification gates in local development

Every external dependency already resolves to a development stub when its flag is unset — the boot
log emits `Development providers active:` naming each one, and that log is the authority, not this
list. What that leaves is a single *gate*: the identity proofs in `TrustService`.

`docker-compose.yml` therefore defaults **`IDENTITY_VERIFICATION_BYPASS=true`**, and is the only
place that does. Rationale for the flag itself lives in
[`EXTERNAL_SERVICES_AND_PROVIDERS.md`](../EXTERNAL_SERVICES_AND_PROVIDERS.md) and `.env.example`
(D-323) — not repeated here. What is specific to compose:

- **Scope.** Only this file defaults it on. The ECS task definition never sets it, so staging and
  production stay enforced, and startup throws if it is true with `ASPNETCORE_ENVIRONMENT=Production`
  — so pointing compose at a Production environment fails closed rather than opening the gate.
- **What it opens.** `can_organize_paid`, `can_receive_payout`, `can_create_public_event` — i.e. most
  of what there is to test.
- **What it does not touch.** `identity_verified` and `bank_verified` keep reporting `false` for an
  account that proved nothing, so no fake verification is ever presented as genuine. The blacklist
  and risk-score checks stay enforced. Authentication, authorization and session security are
  entirely outside this flag.
- **Database state stays real.** The bypass is evaluated at capability-read time and writes nothing,
  so the stored identity rows remain exactly what the real flow would read. Setting
  `IDENTITY_VERIFICATION_BYPASS=false` in `.env` re-enforces the gate with no migration or cleanup.
- **Tests.** Both behaviours are covered in `backend/Kurx.Tests/IdentityVerificationBypassTests.cs`:
  bypassed, enforced, unset/unrecognised values landing on enforced, and Production refusing to start.

#### Org verification is a separate gate, and no flag opens it

`IDENTITY_VERIFICATION_BYPASS` covers the *person*. Publishing a **paid** event also requires the
**organization** to be verified — `PaidOrganizerGateAsync` reads both, and returns `org_not_verified`
when `Organizations.VerificationStatus` is anything but `Verified`. Nothing bypasses that, by design:
unlike the identity proofs, org verification is an ordinary admin review with no external provider
behind it, so relaxing it would remove a real check rather than a mock one.

Clear it locally by running the real flow, which works because the evidence is a marked development
stub rather than because any gate was skipped:

1. Organizer — `POST /v1/orgs/{orgId}/verification/submit` with at least one document
   (`docType` + `storageKey`; both non-empty or it fails `invalid_evidence`) → `pendingreview`.
2. Platform admin — `POST /v1/admin/orgs/{orgId}/verification/review` `{"decision":"approve"}` →
   `verified`.

This writes a genuine `VerificationReviews` row and a `reviewed_at` timestamp, so the state stays
valid once the identity bypass is switched off — the org really was reviewed, by a real admin.

Step 2 needs a platform admin. `SUPERADMIN_BOOTSTRAP_PHONE` grants SuperAdmin to an account that has
**already registered through the normal OTP flow** — it never creates one, and it no-ops entirely
once any SuperAdmin exists (it logs `bootstrap skipped`). Its practical use is recovering admin
access after `docker compose down -v` wipes the database.

Paid publishing then follows the review lifecycle rather than self-publishing: a paid event refuses
`publish` with `paid_event_requires_review`, and must go
`submit_for_review` → `claim_review` → `approve_review` → `publish_approved`, with the last three
reviewer-only (`reviewer_required` otherwise). Transitions are org-scoped:
`POST /v1/orgs/{orgId}/events/{eventId}/transition`.

One-time codes are never delivered in development (`SMS_PROVIDER`/`EMAIL_PROVIDER` default to
`console`), so they exist only in the API log. `scripts/otp.sh` (or `otp.ps1`) prints the most recent
one with its age against the 5-minute expiry.

## Startup behavior

On every boot, the API:

1. Applies pending EF Core migrations (`db.Database.MigrateAsync()`), which also proves it can actually reach Postgres.
2. If that fails for any reason (unreachable database, bad connection string, a migration that can't run), it logs a `Critical` message naming the cause and the process aborts — it will not start serving traffic against a database it can't use.

There is no separate manual migration step required for a normal deploy; only run `dotnet ef database update` manually if you specifically want to apply migrations ahead of a deploy window.

## Health checks for orchestrators

`GET /health` returns JSON:

```json
{
  "status": "Healthy",
  "totalDurationMs": 3.4,
  "checks": [
    { "name": "postgres", "status": "Healthy", "description": null, "durationMs": 2.1 },
    { "name": "storage", "status": "Healthy", "description": "...is writable.", "durationMs": 0.7 }
  ]
}
```

A non-2xx or `Unhealthy` status means at least one real dependency (Postgres always; Redis if `REDIS_CONNECTION` is set; local-disk storage if `STORAGE_PROVIDER=localdisk`) is actually broken — point your orchestrator's liveness/readiness probe at this endpoint.

## Database monitoring and runbooks

Alarms, thresholds, and what to do when one fires: [`DATABASE_RUNBOOKS.md`](DATABASE_RUNBOOKS.md) (DB-9,
[D-328](../DECISIONS.md)). Connection sizing that its thresholds derive from:
[`CONNECTION_POOLING.md`](CONNECTION_POOLING.md) (DB-2).

⚠️ **Configured, not deployed.** `infra/terraform/` has never been applied — no state file, no tfvars, and
`cd.yml` does not touch AWS. Every CloudWatch alarm, the RDS parameter group and `pg_stat_statements` are
**CONFIGURED ONLY**. The runbook ends with the deployment checklist that turns each item into a verified
one; note especially that **neither SNS topic has a subscriber**, and an alarm firing into a topic nobody is
subscribed to is indistinguishable from no alarm at all.

## CI/CD

`.github/workflows/ci.yml` restores, builds, and runs the backend test suite (with a real Postgres service container) plus web typecheck/lint/build on every push and PR. A red CI run means the branch isn't deployable.

`.github/workflows/cd.yml` handles automated delivery:
- **Job 1**: Docker build + push to GHCR (`ghcr.io/{owner}/kurx-{api,web,admin}:sha-{sha}`).
- **Job 2**: Staging auto-deploy on every push to `main` — SSH + `docker compose up`.
- **Job 3**: Production deploy triggered by a GitHub Release — requires manual approval via GitHub Environment; auto-rolls back if the post-deploy health check fails.
