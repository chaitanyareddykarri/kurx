# Kurx

India-focused event-ticketing platform. .NET 10 API (Clean Architecture), PostgreSQL, Next.js web + admin, Flutter mobile.

- **Backend**: `backend/` — Kurx.Api / Kurx.Application / Kurx.Domain / Kurx.Infrastructure / Kurx.Tests
- **Web**: `web/` — Next.js 14 App Router, public marketing site + OTP login + attendee/host app shell
- **Admin**: `admin/` — Next.js staff console on `:3001` (verification queue, event approval, staff & roles, users, orgs, blacklist, risk, reports, audit log, analytics live; finance pending — see [`admin/STATUS.md`](admin/STATUS.md))
- **Mobile**: `mobile/` — Flutter attendee app (auth, discovery, orders, certificates, gamification, social screens; per-screen wiring status in [`docs/roadmap/README.md`](docs/roadmap/README.md); payments blocked on backend — D-019)
- **Infra**: root `docker-compose.yml` — Postgres, Redis, ClamAV, API, web, admin containers; `infra/` holds the API Dockerfile and Terraform

See [`docs/DECISIONS.md`](docs/DECISIONS.md) for the authoritative record of every non-obvious implementation choice, and [`docs/README.md`](docs/README.md) for the documentation index (architecture, deployment, security, API, roadmap).

> **Architecture: user-first and event-first ([`D-074`](docs/DECISIONS.md) / [`D-267`](docs/DECISIONS.md) / [`D-268`](docs/DECISIONS.md)).** Kurx has **Users**, **Events** and **Representations** — and no organization accounts, organizer accounts, or personal organizations. **Users own events** (`Event.CreatedBy`, enforced from there). A representation is an **attribute of an event** — branding, verification, trust, permissions, payout destination — never its owner. The primary relation is always **User → Event**, never Organization → Event: Workspace lists your own events, Create Event is reachable directly, and choosing who you represent (**Personal** by default) is a step *inside* the creation form. Entry flow: [`docs/architecture/event-creation.md`](docs/architecture/event-creation.md).

**Current state.** The backend has been through a 13-module production re-architecture (**M0–M13 / D-039–D-052**): live trust & verification (person identity, organization registry + verification, membership claims, a live capability matrix), an event-approval + paid-checkout gate, the ledger write-path, an admin verification console, and fraud prevention — backed by the integration suite (**1824 passing / 1 skipped of 1825 as of 2026-08-15**). Event Architecture V3 (all 18 phases) and the Professional Identity System have since landed, followed by a zero-trust production-readiness audit and remediation (D-240…D-246, D-250…D-259) that closed two reproduced P0 races — wallet balance lost updates and non-single-use refresh-token rotation — and added the API contract as a generated, committed, CI-gated artifact. **Six provider boundaries now ship real adapters** — SES (email), SNS (SMS), Firebase (push), ClamAV (malware scanning), KMS (signing-key protection) and AWS Secrets Manager — plus Redis-backed presence. The frontier is the four that are still mocked (Razorpay + Route, DigiLocker/KYC, S3 storage, WhatsApp Cloud) and the 39 of 535 operations that still answer with hand-written anonymous objects rather than named response DTOs the contract can describe. See [`CHANGELOG.md`](CHANGELOG.md) and [`docs/roadmap/README.md`](docs/roadmap/README.md) for exactly what's built, and [`docs/architecture/diagrams.md`](docs/architecture/diagrams.md) for ER / sequence / state diagrams.

## Local setup (no Docker)

Requires: .NET 10 SDK (10.0.301+), Node 20+, PostgreSQL 17 running locally.

```bash
# 1. Start Postgres (adjust to however you run it locally), then create the databases:
#    databases: kurx (app), kurx_test (integration tests) — see docs/DECISIONS.md D-003

# 2. Copy the env template and adjust values if your local Postgres differs from the defaults
cp .env.example .env
#    NOTE: the root .env is read by docker compose ONLY — the backend has no .env loader. A native
#    `dotnet run` gets its dev config from Kurx.Api/appsettings.Development.json (connection string,
#    JWT_SECRET, TICKET_HMAC_SECRET, OTP_PEPPER, ALLOWED_ORIGINS), so it needs no .env at all.
#    To override a value natively, export it as an environment variable or use `dotnet user-secrets`.

# 3. Restore + build the backend
cd backend
dotnet restore
dotnet build

# 4. Run the API — applies EF migrations and verifies DB connectivity automatically on startup
cd Kurx.Api
dotnet run
# API listens on http://localhost:5080, Swagger UI at /swagger (Development only)

# 5. In another terminal, run the web app
cd web
npm install
npm run dev
# http://localhost:3000
```

The dev environment ships with zero-credential providers (console WhatsApp/email/push senders, a mock payment gateway, local-disk storage) so nothing above requires real third-party accounts. See `.env.example` for every variable and what it controls.

## Docker Compose setup

```bash
cp .env.example .env   # docker compose auto-loads a root .env if present
docker compose up --build
```

This starts Postgres, Redis, the API (`:5080` → container `:8080`), the web app (`:3000`), and the admin app (`:3001`), plus ClamAV (`:3310`). All six services have real healthchecks; `depends_on: condition: service_healthy` means the API won't start serving until Postgres/Redis report healthy, and web/admin wait on the API.

## Environment variables

Full reference with defaults and production requirements: [`.env.example`](.env.example). Highlights:

| Variable | Purpose | Production requirement |
|---|---|---|
| `ConnectionStrings__Default` | Postgres connection string | Must be set; must not carry the dev default password |
| `JWT_SECRET` | JWT signing key | Must be set, ≥32 chars, not the committed dev placeholder |
| `TICKET_HMAC_SECRET` | Signs gate-entry ticket codes | **Must be set**, ≥32 chars, not the placeholder, and **distinct from `JWT_SECRET`** (D-216) |
| `OTP_PEPPER` | HMAC pepper for every OTP, login included | **Must be set** — no fallback; issuance throws without it (D-115, D-215) |
| `ALLOWED_ORIGINS` | CORS allow-list | Comma-separated origins |
| `REDIS_CONNECTION` | SignalR backplane / presence / cache | **Required in Production** — startup refuses without it (D-217). Optional in dev. |
| `*_PROVIDER` flags | Email/WhatsApp/payment/storage/KYC/push provider selection | Only dev implementations (`console`/`mock`/`localdisk`) exist today |
| `IDENTITY_VERIFICATION_BYPASS` | Skips the govt-ID/PAN/bank proofs so a dev account can publish a public event, organize paid and receive a payout (D-323) — the KYC provider is a mock that approves everything, so the gate costs four submissions and proves nothing | **Must be unset** — startup throws if it is set in Production. Set `true` locally to test those flows. Blacklist/risk checks are unaffected. |
| `NEXT_PUBLIC_API_BASE_URL` | **Web** app's API base URL (SSR uses `API_INTERNAL_URL`) | — |
| `NEXT_PUBLIC_API_URL` | **Admin** console's API base URL (SSR uses `API_URL`) | Distinct name from web's — admin reads this exact variable |
| `KURX_API_BASE` | Flutter app's API base URL (`--dart-define`) | — |

See [`docs/security/secret-management.md`](docs/security/secret-management.md) for the full secret-validation policy.

## Running migrations

Migrations apply automatically on API startup (see `Program.cs`) — the app refuses to start if they can't be applied or the database is unreachable. To manage them manually:

```bash
cd backend
export DOTNET_ROOT="$HOME/.dotnet"   # only needed if dotnet-ef reports a libhostfxr error
dotnet ef migrations add <Name> --project Kurx.Infrastructure --startup-project Kurx.Api
dotnet ef database update --project Kurx.Infrastructure --startup-project Kurx.Api
```

## Running tests

```bash
cd backend
dotnet test
```

Integration tests (`Kurx.Tests`) boot the real API in-process against a Postgres database — each test class gets its own, cloned from a migrated template — so a reachable Postgres is required. **1825 tests; 1824 passing, 1 skipped, 0 failing as of 2026-08-15** (measured in the SDK container with clamd up, 29m23s). Green is the standard — a red test is a defect, not "the environment".

> **On Windows, run the suite in a container, not on the host.** Windows Application Control blocks the
> test host from loading `Kurx.Infrastructure.dll` (`0x800711C7`), which fails *every* test for a reason
> that looks exactly like catastrophic code breakage. `dotnet build` on the host is unaffected and is a
> valid pre-flight gate; execution is not.
>
> ```bash
> MSYS_NO_PATHCONV=1 docker run --rm --network container:kurx-postgres \
>   -v "/d/event/kurx:/src" -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
>   "dotnet build Kurx.sln -c Debug -p:ArtifactsPath=/tmp/artifacts --nologo -v q && \
>    dotnet test Kurx.sln --no-build -c Debug -p:ArtifactsPath=/tmp/artifacts"
> ```

```bash
cd web
npm run typecheck
npm run lint
npm run build
```

## Project structure

```
kurx/
├── backend/
│   ├── Kurx.Api/            # Minimal API endpoints, hubs, middleware, Program.cs composition root
│   ├── Kurx.Application/    # Abstractions (interfaces) consumed by Api, implemented by Infrastructure
│   ├── Kurx.Domain/         # Entities, enums — no framework dependencies
│   ├── Kurx.Infrastructure/ # EF Core, auth, org/event services, dev providers, DI wiring
│   └── Kurx.Tests/          # xUnit integration tests (WebApplicationFactory + real Postgres)
├── web/                     # Next.js attendee/host app + public marketing site
├── admin/                   # Next.js staff console (trust & safety, users/orgs, events, audit, analytics)
├── mobile/                  # Flutter attendee app (feature-first clean architecture)
├── packages/ui/             # Shared @kurx/ui design system (consumed by web + admin)
├── infra/                   # API Dockerfile (built by compose, push-ecr.sh and CD) + Terraform
├── docs/                    # Architecture, deployment, security, API, roadmap docs + DECISIONS.md
└── docker-compose.yml       # Full-stack compose: postgres, redis, clamav, backend, web, admin
```
