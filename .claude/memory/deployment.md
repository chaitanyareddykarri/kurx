# Deployment

How Kurx runs — locally (sudo-free native), in Docker, and in CI. Full detail: `docs/deployment/`. This is the enforceable summary.

## Two supported run modes

**Native local dev (D-001, the clean-machine reality):** no sudo, no Docker required, no `psql`.
- .NET 10 SDK at `~/.dotnet` — **`export DOTNET_ROOT="$HOME/.dotnet"`** before any `dotnet`/`dotnet-ef` command or it crashes with a libhostfxr error.
- Postgres 17 (zonky embedded) at `~/tools/pg`, data dir `~/tools/pg-data/kurx`, port **5432**, user/pass `kurx`/`kurx`, DBs `kurx` + `kurx_test`. Start if down: `~/tools/pg/bin/pg_ctl -D ~/tools/pg-data/kurx start`.
- Node 22 LTS at `~/tools/node`; Redis built from source at `~/tools/redis`.
- API dev port **5080**; web **3000**; admin **3001**.

**Docker (`docker-compose.yml`):** boots postgres, redis, backend, web, admin — verified end-to-end. Same `kurx`/`kurx` credentials so `.env` values work in both modes (D-003). Use it for full-stack verification (`.claude/checklists/release.md`), not just `dotnet run` alone.

**Both modes publish the API on the same port — 5080 (D-318).** Compose used to publish 5001 while `launchSettings`, `.env.example`, `infra/docker-compose.yml` and all three client defaults said 5080, so switching topology silently broke every client and mobile (`KURX_API_BASE` default `http://localhost:5080`) could not reach a compose backend at all. One port, so the two modes are interchangeable and no client config changes when you switch. Postgres/Redis are still remapped under compose (**5433**/**6380**) so they cannot collide with a native local instance on 5432/6379.

All three images run **unprivileged** (D-256): the API as `$APP_UID` (the non-root user the `aspnet` base image ships), web and admin as `node`. The trap when changing them is that dropping privilege breaks writes at *runtime*, not at build — `LocalDiskStorage` calls `Directory.CreateDirectory` on `LOCALDISK_ROOT` in its constructor, and `next start` writes `.next/cache`. Both paths are chowned in the image; if you add another writable path, chown it too or the container starts and then fails on first use.

## Startup behavior (fail-closed, D-017)

- EF migrations **auto-apply** at API startup (`db.Database.MigrateAsync()`); a failed migration **aborts the host** rather than serving a stale schema.
- Production secret validation aborts startup on a weak/default `JWT_SECRET` or a connection string containing the committed `Password=kurx`. Don't bypass it for convenience — it's a security gate.
- `/health` reflects real dependency state (`observability.md`); a deploy isn't healthy until `/health` is.
- **Reference-data seeders run at boot; per-row convergence does not (D-250).** The six seeders (taxonomy, Kind/Capability/ParticipantRole registries, system + design templates) write a fixed, bounded set of rows the API cannot serve a request without, so they stay on the fail-closed path. The passes that backfill *existing* rows moved to `DataBackfillJob` — they scale with table size, and a rolling deploy had every replica running the same anti-joins over the same rows at once. The job is `[DisableConcurrentExecution]`, runs hourly, and is triggered once at boot; the trigger **enqueues**, so the host finishes starting while a worker converges.
- Redis is a **degraded dependency, not a boot dependency** (D-253). The multiplexer is lazy with `AbortOnConnectFail=false`, so a Redis that is down at boot no longer takes the API down — it starts, serves, reports `redis` unhealthy on `/health`, and reconnects on its own with no restart.

## Config & secrets

- Config via env vars; `.env.example` is the documented shape, `.env` is real and **never committed / never edited without flagging to the user** (CLAUDE.md, `security-rules`).
- Web reads `NEXT_PUBLIC_API_BASE_URL` — must match the actual API URL with no stale port fallback (a real bug fixed once; the wrong var name + stale `5050` fallback pointed web at nothing, D-017). Confirm var name + value when touching web config.

## CI (`.github/workflows/ci.yml`)

- Backend: restore → build → test against a **real Postgres service container** (mirrors local `kurx_test`, no mocked persistence).
- Web/admin: typecheck → lint → build.
- **API contract drift gate (D-259):** CI regenerates the OpenAPI document from the running API and fails the build if it differs from the committed `docs/api/openapi.json`. Regenerate with `scripts/generate-openapi.sh` and commit the result with the change that caused it. This exists because three clients hand-maintain their models and nothing compared them to the API — which is how web *and* admin both shipped a wrong wallet schema that threw on every call.
- CI must stay in parity with local dev. Never weaken/skip a CI check without explicit user sign-off (devops agent boundary) — it's a trust boundary.

## Not built / out of scope

- `.github/workflows/cd.yml` defines the deploy pipeline (GHCR image push, SSH deploy to staging on push to `main`, manual-approval-gated production deploy on GitHub Release, post-deploy `/health` check + rollback capture) — but no actual staging/production host is provisioned anywhere in this repo, and the required GitHub secrets (`STAGING_HOST`, `PRODUCTION_HOST`, SSH keys, etc.) aren't set. No real secret store (Vault, AWS Secrets Manager, etc.) is wired up. See `docs/EXTERNAL_SERVICES_AND_PROVIDERS.md` §16 for the full picture. Don't introduce cloud infra provisioning without a phase + `D-NNN`.
