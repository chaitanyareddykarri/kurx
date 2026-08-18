# Running the tests — manual runbook

Every command you need to verify Kurx by hand, per surface. This file owns the **commands**;
[`../.claude/memory/testing-standards.md`](../.claude/memory/testing-standards.md) owns the **rules and
the current baselines** (what to test, what "green" means, what each number was last measured at).

Paths below assume the repo at `D:\event\Kurx` and Git Bash. In PowerShell, drop `MSYS_NO_PATHCONV=1` and
write the mount as `-v D:/event/Kurx:/src`.

| Surface | Command | Runs in | Typical time |
|---|---|---|---|
| Backend | `dotnet test Kurx.sln` | **Docker SDK container only** | 18–43 min |
| Web | `npm test` | host, `web/` | ~3 min |
| Admin | `npm test` | host, `admin/` | ~35 s |
| Mobile | `flutter test` | host, `mobile/` | ~3.5 min |

**Run them one at a time.** All four at once starves the box: vitest workers die on RPC timeouts with
zero failed assertions, and the backend container has managed four minutes of test progress in a full
hour while CPU read 18%. A starved run looks like breakage, not like contention.

---

## 0. Prerequisites

```bash
docker compose up -d postgres redis clamav      # clamav takes ~1 min to report healthy
docker ps --format '{{.Names}}\t{{.Status}}'    # all three must say (healthy)
```

`clamav` is not optional — without it `ClamAvUploadPathTests` fails ×4 as `scan_unavailable`. It has been
in the default stack since D-339, so a plain `docker compose up` already starts it.

Host toolchain: **Flutter 3.44.6** (pinned — `flutter --version` must match), **Node 20**, and Docker with
at least ~6 GB available to the VM.

---

## 1. Backend — the full suite

**`dotnet test` on the Windows host does not work and never will here.** Application Control blocks
loading `Kurx.Infrastructure.dll` (`0x800711C7`) and every test fails for that reason alone, which looks
exactly like catastrophic breakage. Run it in a .NET 10 SDK container that shares Postgres's *network
namespace* — `KurxApiFactory` hard-codes `Host=localhost`, so a user-defined Docker network will not do:

```bash
MSYS_NO_PATHCONV=1 docker run --rm --name kurx-suite --memory=4g \
  --network container:kurx-postgres \
  -e CLAMAV_HOST=clamav -e CLAMAV_PORT=3310 \
  -v "/d/event/Kurx:/src" -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  "dotnet build Kurx.sln -c Debug -warnaserror -p:ArtifactsPath=/tmp/artifacts --nologo -v q && \
   dotnet test Kurx.sln --no-build -c Debug -p:ArtifactsPath=/tmp/artifacts --logger 'console;verbosity=minimal'"
```

Every flag on that line is load-bearing:

| Flag | Why |
|---|---|
| `--network container:kurx-postgres` | `KurxApiFactory` hard-codes `localhost`; only the port is configurable. Sharing the namespace also makes `clamav` resolve by service name. |
| `-e CLAMAV_HOST=clamav -e CLAMAV_PORT=3310` | Defaults to `localhost:3310`, where nothing is listening inside Postgres's namespace. |
| `-v "/d/event/Kurx:/src"` — the **repo root**, not `backend/` | `InternationalPhoneTests` loads `docs/api/phone-conformance.json` by walking up from the assembly. Mount `backend/` and it sits above the mount: `FileNotFoundException`. |
| `--memory=4g` | With the dev stack up, an uncapped container fights it for Docker's memory and the build dies mid-copy with `MSB3026 … Cannot allocate memory`, which reads like a locked file. |
| `-c Debug` on **both** build and test | Omit it on either and the test host looks for a configuration that was never built. |
| `-p:ArtifactsPath=/tmp/artifacts` | Keeps build output out of the bind mount, so a container build cannot stamp Linux paths into files the Windows IDE reads. |

**Read the result, not the clock.** Wall time is not a constant — the same green suite has taken 18m07s,
19m35s, 19m59s, 23m27s, 35m56s, 38m44s and 42m40s on the same box, because class order varies between
runs. A slow run is not a hang; scroll for printed failures before killing one.

### Backend — one class or one test

Same container, swap the test command. Roughly 30–60 s once the build is done:

```bash
dotnet test Kurx.sln --no-build -c Debug -p:ArtifactsPath=/tmp/artifacts \
  --filter FullyQualifiedName~TemplateActivationTests --logger 'console;verbosity=minimal'
```

`~` is "contains", so it matches a class or a namespace. For exactly one test, name it in full:
`--filter FullyQualifiedName=Kurx.Tests.TemplateActivationTests.Clone_starts_a_new_family_as_a_draft`.

Chain a filtered run in front of a full one when you are verifying a fix — one build, fast feedback, then
the real evidence:

```bash
... bash -c "dotnet build … && \
   dotnet test … --filter FullyQualifiedName~YourTests … && \
   dotnet test … "
```

### Backend — build only (a valid host pre-flight)

`dotnet build` is *not* blocked on the host, so this is a legitimate fast gate before paying for a suite:

```bash
cd backend && dotnet build Kurx.sln -c Debug -warnaserror
```

Never leave a host build in place and then trust a container run — see the `MvcTestingAppManifest.json`
row in troubleshooting.

### Backend — generating a migration

`dotnet ef` is blocked on the host for the same reason, so it runs in the container too. It needs a build
first, and the SDK image has no `dotnet-ef`:

```bash
MSYS_NO_PATHCONV=1 docker run --rm --memory=4g --network container:kurx-postgres \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -v "/d/event/Kurx:/src" -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  "dotnet tool install --global dotnet-ef >/dev/null 2>&1; export PATH=\$PATH:/root/.dotnet/tools && \
   dotnet build Kurx.Api/Kurx.Api.csproj -c Debug --nologo -v q && \
   dotnet ef migrations add YourMigrationName -p Kurx.Infrastructure -s Kurx.Api -c KurxDbContext --no-build"
```

The dev API applies pending migrations on start, so a new migration reaches the dev database on the next
`kurx-backend` restart — you do not normally run `database update` by hand.

---

## 2. Web

```bash
cd web
npm test              # vitest run
npm run typecheck     # tsc --noEmit
npm run lint
npm run build         # CI runs all four
```

One file, or one test by name:

```bash
npx vitest run test/ticketing.test.tsx
npx vitest run -t "the order call cannot leave the button stuck"
npm run test:watch    # interactive
```

`vitest.config.mts` caps worker forks by **free memory** as well as by core count (~700 MB per jsdom
fork). That is deliberate: uncapped, the forks exhaust RAM, the main thread misses birpc's 60-second
ceiling, and whole *files* report as failed with zero failed assertions. If you see
`[vitest-pool]: Timeout calling "fetch"`, you starved the box — free it and re-run rather than reading the
failure as a defect.

---

## 3. Admin

Identical shape, from `admin/`:

```bash
cd admin
npm test
npm run typecheck
npm run lint
npm run build
```

---

## 4. Mobile

```bash
cd mobile
flutter pub get                        # required after deleting .dart_tool/
flutter analyze --no-fatal-infos       # must be "No issues found!"
flutter test
```

One file, or one test by name:

```bash
flutter test test/features/workspace/lifecycle_visibility_test.dart
flutter test --plain-name "an unknown status falls back to draft rather than guessing forward"
```

`--no-fatal-infos` is the CI setting: infos do not fail the build, warnings and errors do.

---

## 5. API contract checks

These guard the client contracts against the generated spec. CI runs the three tools; run them by hand
after any endpoint or DTO change:

```bash
node scripts/contract-check.mjs           # client calls vs the spec
node scripts/openapi-response-check.mjs   # no NEW undeclared responses
node scripts/response-shape-check.mjs     # declared response types match the handler
```

Regenerating `docs/api/openapi.json` itself runs in the SDK container — see `scripts/generate-openapi.sh`.

---

## 6. What no automated run covers

Know these before you claim a surface is verified:

- **`node --test scripts/contract-check.test.mjs`** — CI runs the contract *tool*, never its own test.
- **`mobile/integration_test/`** (`device_key_test.dart`, `push_test.dart`) — needs real hardware:
  `flutter test integration_test/ -d <device-id>`.
- **`mobile/test_live/`** (`live_backend_test.dart`) — needs a live backend, and sits outside `flutter test`.

---

## 7. Hygiene before a run you intend to trust

Killed runs leave debris that silently changes the next result:

```bash
# 1. Drop leftover per-class test databases (135 had accumulated on 2026-08-18)
docker exec kurx-postgres psql -U kurx -d postgres -tAc \
  "select 'DROP DATABASE IF EXISTS \"'||datname||'\" WITH (FORCE);' \
   from pg_database where datname like 'kurx\_test\_%'" > /tmp/drop.sql
docker exec -i kurx-postgres psql -U kurx -d postgres -q -f - < /tmp/drop.sql

# 2. Delete stale build output (a host build poisons a container run — see troubleshooting)
find backend -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
rm -rf web/.next admin/.next mobile/build mobile/.dart_tool

# 3. Make sure nothing else is already holding the machine
docker ps --filter ancestor=mcr.microsoft.com/dotnet/sdk:10.0
```

Dropping every `kurx_test_*` database is always safe: each test process creates its own template
(`kurx_test_tmpl_<guid>`, a fresh `Guid` per run) and clones a `kurx_test_<guid>` per class, so nothing
there is reused between runs.

**Never run two SDK containers against one Postgres**, and never `--rm`-less-ly abandon one: a `docker run`
whose client you kill keeps running: `docker kill kurx-suite` to be sure.

---

## 8. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Every backend test fails, `0x800711C7` | Host run — Application Control blocks the DLL | Run in the SDK container |
| `DirectoryNotFoundException: …/D:\event\Kurx\backend\Kurx.Api/`, stack ends in `HostBuilder.CreateHostingEnvironment` | A **host** build wrote Windows absolute paths into `MvcTestingAppManifest.json`; 183 phantom failures observed | Delete `bin/`+`obj/`, let the container build. Build in one place only |
| `NETSDK1004: Assets file … not found` | Fresh container, nothing restored at that artifacts path | Add a `dotnet build`/`restore` step before `--no-build` |
| `MSB3026 … Cannot allocate memory` mid-copy | Container competing with the dev stack for Docker memory | `--memory=4g`, or stop the dev stack |
| `ClamAvUploadPathTests` ×4 `scan_unavailable` | clamd down, or `CLAMAV_HOST` unset | `docker compose up -d clamav`, pass `-e CLAMAV_HOST=clamav` |
| Web: N *files* failed, 0 *tests* failed, `Timeout calling "fetch"` | Starved box, dead vitest workers | Run it alone; read the **test** count, not the file count |
| Backend run seems hung at 20+ min | Normal — class order varies run to run | Check for printed `[FAIL]` lines before killing it |
| Failures across unrelated classes in a shared checkout | Another session's in-flight work, not your change | Read the *error*, not the test name; `git status --short` first, and attribute every failure before believing it |

---

## 9. What green looks like

Zero failures. A red test is a defect, not "the environment" — the three long-standing exceptions were
each fixed rather than tolerated. Current per-surface counts, and the history behind them, live in
[`../.claude/memory/testing-standards.md`](../.claude/memory/testing-standards.md); report them as
**before → after** when you change anything.

Only a **full-suite** run is evidence on the backend: cross-class parallelization is disabled and the
suite is order-sensitive, so a green filtered run proves only that class.
