# Testing Standards

## Backend (`backend/Kurx.Tests`)

- Integration tests only, against a real `kurx_test` Postgres DB via `WebApplicationFactory<Program>` (`KurxApiFactory`) — no mocked persistence layer, ever.
- Cross-class parallelization is disabled (`AssemblyInfo.cs`) because classes share and reset `kurx_test`. New test classes must respect that shared-state assumption.
- Every new service/endpoint gets a test class alongside the existing per-domain ones (`AuthTests`, `OrgTests`, `EventTests`, `SupportingEntityTests`, ...).

## Where tests actually run (D-128)

**Local `dotnet test` on Windows does not work and never will here.** Smart App Control blocks loading
`Kurx.Infrastructure.dll` (`0x800711C7`); a full run fails 100% for that reason alone, which looks
exactly like catastrophic breakage. `dotnet build` is unaffected and is a valid pre-flight gate.

Authoritative local verification is a .NET 10 SDK container sharing the Postgres container's network
namespace (so `localhost:5432` resolves the way `KurxApiFactory` hard-codes it):

```bash
MSYS_NO_PATHCONV=1 docker run --rm --network container:kurx-postgres \
  -v "/d/event/kurx:/src" -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  "dotnet build Kurx.sln -c Debug -warnaserror -p:ArtifactsPath=/tmp/artifacts --nologo -v q && \
   dotnet test Kurx.sln --no-build -c Debug -p:ArtifactsPath=/tmp/artifacts --logger 'console;verbosity=minimal'"
```

**Current baseline: 1825 tests — 1824 passing, 1 skipped, 0 failing (2026-08-15, 29m23s).** Measured in the
SDK container with clamd up. **Zero is the standard now — a red test is a defect, not "the environment."**

> ⚠️ **The working tree was red when this was written, and not from the change that measured it.** A later
> run the same day gave **1826 / 1806 pass / 19 fail** — all in `EventFirstTests` and
> `WorkspaceCapabilitiesTests`, all failing as `KeyNotFoundException` on `GetProperty("id")` of a
> **Personal** (no-org) create-event response, i.e. creation itself returning a ProblemDetails. The cause is
> a concurrent session's in-flight work: `OrgService`/`IOrgService`/`EndpointResponses` grew fields on
> `RepresentableOrganization` citing **D-350 and D-352 — numbers that do not exist in `DECISIONS.md`**
> (which ends at D-345), alongside `EventPolicyService`, `PolicyResolver` and `TrustService` edits and
> matching web/mobile create-event-gate changes. They updated `TrustTests`, `IdCardTests` and `OrgTests`;
> these two classes are the collateral they have not reached yet.
>
> The lesson is procedural: **in a shared checkout, a suite number is only evidence for your change if you
> can attribute every failure.** Diff the tree first (`git status --short`), group failures by class, and
> read one failure to its cause before claiming or disclaiming a regression.

Run it exactly like this; the two `-e` flags are what the ClamAV class needs and nothing else supplies:

```bash
docker compose up -d clamav                         # in the default stack since D-339; ~1 min to healthy
MSYS_NO_PATHCONV=1 docker run --rm --memory=4g --network container:kurx-postgres \
  -e CLAMAV_HOST=clamav -e CLAMAV_PORT=3310 \
  -v "/d/event/Kurx:/src" -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  "dotnet build Kurx.sln -c Debug -warnaserror -p:ArtifactsPath=/tmp/artifacts --nologo -v q && \
   dotnet test Kurx.sln --no-build -c Debug -p:ArtifactsPath=/tmp/artifacts --logger 'console;verbosity=minimal'"
```

`--memory=4g` is not decoration. With the six-service dev stack up, the SDK container competes with it for
Docker's memory and the build dies mid-copy with `MSB3026 … Cannot allocate memory` — which reads like a
locked file, not like memory pressure. Cap it, or stop the stack first.

**Why the IDE used to fill with phantom errors (fixed by D-345 — don't reintroduce it).** NuGet writes
absolute paths into `obj/project.assets.json`, and the host and container share this tree over a bind
mount. A container restore therefore stamped `packageFolders: /root/.nuget/packages/` and
`projectPath: /src/backend/…` onto the host; VS Code's C# server resolved no package reference and showed
**52 "type or namespace not found" errors in `Program.cs`** — Serilog, Hangfire, even `IServiceCollection`
— with `<No project>` in the status bar, while the container build was green and the suite passed. If you
ever see that shape again, it is the toolchain, not the code.

`backend/Directory.Build.props` now redirects Unix, non-CI builds to `/tmp/kurx-artifacts`, so a container
build — **including a bare `dotnet restore` with no flags** — cannot write a file the host reads. Windows
and GitHub Actions layouts are untouched, and an explicit `-p:ArtifactsPath` still wins.

Two earlier fixes for this were wrong and are worth not repeating: `dotnet restore` on the host is only a
repair (it was undone by another session's container build within nine minutes), and `UseArtifactsOutput`
just moves the shared file to `backend/artifacts/`, which is still inside the mount. If the IDE goes red
anyway, check whose paths are on disk before doing anything else:
`node -e "console.log(Object.keys(require('./backend/Kurx.Api/obj/project.assets.json').packageFolders))"`
— a `/root/...` path means something bypassed the redirect.

**The three failures this baseline used to carry were fixed, not tolerated.** Each had been written off as
environmental, and two of the three were real defects wearing that label:

- **4 `ClamAvUploadPathTests`** — genuinely just the daemon. `CLAMAV_HOST=clamav` does nothing if nothing
  is listening. Start it and all five pass, EICAR included. (It used to sit behind `profiles: ["scanning"]`;
  D-339 removed profiles, so the default `docker compose up` now starts it.)
  (If `docker pull clamav/clamav:1.4` fails with a TLS error against `auth.docker.io`, retry — it is
  transient Hub auth flakiness, not a broken mirror.)
- **`NoContentDeclarationTests` + `OpenApiCoverageTests`** — both walked up from `AppContext.BaseDirectory`
  to find the repo, which cannot work under `-p:ArtifactsPath` (the binary lands outside the `/src` mount).
  Now resolved through `RepoRoot`, which uses `[CallerFilePath]` — the compile-time source path — with the
  old walk kept as a fallback. **The sibling was the worse half:** `OpenApiCoverageTests` treats "spec not
  found" as "not generated yet" and *returns*, so it silently skipped its own assertion in the container and
  passed against a floor of 64 while real coverage was 464/535. A ratchet that stops ratcheting is worse
  than one that fails. Baseline raised to the measured 464.
- **`NotificationDedupTests`** — keyed a phone suffix on `Math.Abs(kind.GetHashCode()) % 100`, and
  `string.GetHashCode()` is **randomized per process** in .NET Core, so ~6% of runs mapped two of its four
  Theory cases onto one phone and hit `IX_users_Phone` with a `23505` that reads like a real regression.
  Passing 4/4 on an isolated re-run is the tell. **Never key a test fixture on a string hash** — write the
  value down, as every other test in that file already did.

*Previous baselines: 1759 / 1752 pass / 6 fail (2026-08-14, before those fixes); 1709 / 1697 pass / 11 fail
(2026-08-11), whose 11 included **3 `EventAudienceAuthorizationTests`** genuinely failing on committed
`HEAD`, verified in a pristine `git worktree` at `dd04d54`.*

**Those 3 are now fixed, and the cause is worth carrying.** `LoginAsAsync(userId)` reassigns a
user's phone and then signs in — but `otp/verify` is a **sign-in-or-register** ceremony, and the helper
seeded the number *as typed* while the lookup uses the *normalized* form (`91` + the ten). The lookup missed
and verify silently **created a second account**, so the "owner" client was a stranger and every assertion
that an owner can reach their own draft failed as 404. `PostTests.LoginAsAsync` already carried the fix with
a comment describing it; this copy was missed. Seed `AuthService.NormalizePhone(typed)`, never the typed
string — and treat "a test logs in as a seeded user" as a place this bug recurs.

> ⚠️ **`-p:ArtifactsPath=/tmp/artifacts` puts the test binary OUTSIDE the `/src` mount**, so any test that
> walks up from `AppContext.BaseDirectory` looking for repository files cannot find them.
> `NoContentDeclarationTests` (which reads `Kurx.Api/Endpoints/*.cs`) and `OpenApiCoverageTests` (which reads
> the committed spec) are the two that do this. A failure reading `Could not locate the endpoints directory
> from /tmp/artifacts/...` is the runner, not a regression.

> ⚠️ **Never run two SDK containers against one Postgres.** Both runs corrupt. Check
> `docker ps | grep sdk` before starting — a concurrent session's suite looks exactly like a catastrophic
> regression in yours, and yours does the same to theirs.

*Previous baseline: 1433 tests — 1431 passing, 1 skipped, 1 environmental failure (2026-08-06).* Measured
on the shared branch with D-289/D-290/D-291 plus a second workstream's event-creation classes, so it is a
floor and not a clean-room number. The single failure was
`TrustedDeviceAuthTests.A_rotated_device_token_stays_sender_constrained_to_the_same_device`, which died with
`TaskCanceledException` after **7m48s** on a pre-existing `/v1/auth/otp/verify` call while Docker Desktop was
returning HTTP 500s and a second SDK container competed for the host. Re-running the four affected auth
classes alone gave **109/109 in 1m53s** — the way to tell a hang from a defect is to re-run the class in
isolation, not to read the stack trace and guess.

*Previous baseline: 1297 tests — 1296 passing, 1 skipped (2026-08-05).* Measured on `HEAD` + D-274
(Developer Mode removal) in an **isolated worktree**, which is why it is trustworthy: the shared branch
had a second workstream editing `EventService`/`EventStatusWorkflow` mid-run, and a run taken there
reported 175 failures that reproduced nowhere else. Treat this as a floor rather than an equality —
concurrent workstreams push the shared-tree total higher (prior floors: 1018 → 1098 after D-262 Posts →
1163 on 2026-08-04). A total that comes back materially **lower** is worth investigating before it is
believed; a higher one usually just means someone else's tests landed.

**When a run disagrees with the baseline, suspect the tree before the code.** Check whether another
session is mid-edit (`git status`, file mtimes against the run window) and whether a second SDK container
is running — two suites against one Postgres, or an edit landing during the build phase, both produce
large failure counts that look exactly like catastrophic regression. The reliable move is a `git worktree`
carrying only your own changes; that result is attributable, a shared-tree result is not.

> The mount path is lowercase `/d/event/kurx` to match the directory on disk. Docker Compose derives its
> project name from the directory as typed, so a capitalised path creates a *second* project with its own
> volumes — which is one way to end up staring at an empty database that was fine a moment ago.

- `MSYS_NO_PATHCONV=1` — Git Bash otherwise mangles container paths into `C:/Program Files/...`.
- `-p:ArtifactsPath=/tmp/artifacts` — keeps Linux build output out of the Windows `bin/obj`, which
  otherwise causes stale-DLL confusion between the two toolchains.
- `--filter` is incompatible with `--no-build` here (the runner rejects the dll argument); drop
  `--no-build` when filtering.
- A `--filter` spanning several classes can fail tests that pass in a full run and vice versa — the
  suite is order-sensitive by design. **Only a full-suite run is evidence.**
- **Pass `-c` explicitly on both the build and the test invocation.** They must agree: `dotnet test
  --no-build` with a different configuration than the build silently resolves nothing to run.
- **No `#if` gates the test suite any more (D-274).** The `DEV_AUTH` compile constant, the whole
  `/v1/dev/*` module and `DevAuthTests` are deleted, so a Debug and a Release run now discover the same
  tests. A configuration-dependent test count is a bug, not a feature.

CI (`.github/workflows/ci.yml`) remains the final authority.

## What "done" requires

Passing `dotnet test` is necessary but not sufficient for anything with a runtime surface (an endpoint, a UI page, a background job). Also required:
- A live smoke test or manual exercise of the actual flow (curl, browser, or an end-to-end docker-compose run) — see `.claude/checklists/qa.md` and the `verify` skill.
- No reduced/weakened assertions to force a suite green — fix the cause.
- **Assert a client-facing field where the client reads it — over HTTP, not off the entity** ([D-326](../../docs/DECISIONS.md)). `event_categories.ProductClass` had nine assertions across the suite and all nine passed while `/v1/categories` emitted the field for **0 of 111** rows, because every one of them queried `db.EventCategories`. The mapper between the two was the whole defect. An entity assertion proves the value was *computed*; only a request proves it was *delivered*. This is the same class as D-245 — if a client filters, renders or branches on a field, one test must read it from the response body.
- **Two flags this repo has now shipped twice: a switch that no environment forwards, and a config path that only the test harness takes** ([D-323](../../docs/DECISIONS.md)). A feature toggled by an env var needs the var wired into `docker-compose.yml` as well as `.env.example` — the container has no `env_file`, so an unlisted variable is invisible however correct the code is. Tests that construct the options object directly (the right call — they avoid booting a second host per value) cannot see that gap by construction, so **turn the flag on in the running stack once** before calling it done.

## Frontend

Every surface has a suite. Measured 2026-08-09:

| Surface | Command | Files | Cases | Runs in CI? |
|---|---|---|---|---|
| `web` | `npm test` (vitest) | 38 | 486 (1 skipped) | ✅ |
| `admin` | `npm test` (vitest) | 3 | 27 | ✅ |
| `mobile` | `flutter test` | 58 | 410 | ✅ |
| `scripts` | `node --test scripts/contract-check.test.mjs` | 1 | 1 | ❌ CI runs the tool, not its test |

All four CI jobs exist and every client suite runs: `backend` (build `-warnaserror` + full suite against
a real Postgres service + the contract gates), `web` and `admin` (typecheck → lint → **test** → build),
and `mobile` (`flutter analyze --no-fatal-infos` → `flutter test`, Flutter pinned to 3.44.6).

Two coverage gaps worth knowing before you trust a green pipeline:

- **`scripts/contract-check.test.mjs` runs nowhere.** CI runs the *tool* (`node scripts/contract-check.mjs`)
  but never its test, so the gate's own vacuity guards are unproven in the pipeline — and a gate reporting
  zero findings is indistinguishable from a gate that checks nothing (D-303). Run it by hand when you touch
  the gate.
- **`mobile/integration_test/` (2 files) and `mobile/test_live/` (1) run nowhere.** They sit outside
  `test/`, so `flutter test` skips them, and no CI job targets them — they need real hardware or a live
  backend. They cover device keys, push and a live backend round-trip, which nothing else does.

Typecheck/lint/build are still gates, not substitutes: a UI change is not proven by types alone, and
browser verification remains the check for anything the suites do not assert.

## Reporting

State test count before → after and name what's newly covered, so drift in coverage is visible over time (see project-state memory for historical counts as a sanity check, e.g. 19→35→52).


## Running the suite in the SDK container

Windows Application Control blocks the host runner (`dotnet test` and `dotnet ef` both fail with
`0x800711C7`), so both run in the SDK container. `KurxApiFactory` hard-codes `Host=localhost` — only the
port is configurable — so the container has to share Postgres's own network namespace; a user-defined
network does not work, because `localhost` inside the container is the container:

```
docker run -d --name kurx-suite --network "container:kurx-postgres" \
  -v "D:/event/Kurx:/src" -v "kurx-nuget:/root/.nuget/packages" \
  -e CLAMAV_HOST=clamav \
  -w //src/backend mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc "dotnet restore Kurx.sln && dotnet test Kurx.Tests/Kurx.Tests.csproj -c Debug"
```

Four things in that line are load-bearing, each learned from a wasted run (2026-08-09):

- **Mount the REPO ROOT, not `backend/`.** `InternationalPhoneTests` loads `docs/api/phone-conformance.json`
  by walking up from the assembly; mounting `backend:/src` puts it above the mount and the test fails
  `FileNotFoundException`. Hence `-v D:/event/Kurx:/src -w //src/backend`.
- **`-e CLAMAV_HOST=clamav`.** It defaults to `localhost`, which in Postgres's network namespace has
  nothing on 3310, so `ClamAvUploadPathTests` fails ×4 as `scan_unavailable`. With the service name all 7
  pass. (The `127.0.0.1:1` fixture in that file is a deliberate fail-closed case and passes either way.)
- **`dotnet restore` in the same command.** Any earlier container that built *without* the shared NuGet
  volume leaves `obj/project.assets.json` pointing at its own ephemeral cache, and the next volume-mounted
  run dies instantly with `NETSDK1064`. A host build does the same with Windows paths.
- **`-d --name`, never `--rm`.** The run takes ~17 minutes; if the shell watching it is interrupted, a
  `--rm` container self-destructs and takes the tally with it. Detached and named, `docker logs <name>`
  recovers the result regardless.

**Serialize full runs — across sessions and against your own other work.** Running the web build, the
Flutter suite and the admin tests alongside the backend suite pushed the container to 4.4 GiB of 7.59 GiB
and it managed *four minutes of test progress in a full hour*; CPU read 18% and Postgres was idle, so it
looks like a hang rather than contention. Check `docker ps --filter ancestor=mcr.microsoft.com/dotnet/sdk:10.0`
before starting one — another session's run may already hold the machine, and whoever started first keeps it.

**A host build poisons the container test run, and `dotnet restore` does not undo it.**
`WebApplicationFactory` resolves the API's content root from `MvcTestingAppManifest.json`, which the build
writes with *absolute* paths. Build on Windows and it contains `D:\\event\\Kurx\\backend\\Kurx.Api`;
the Linux container then throws
`DirectoryNotFoundException: /src/Kurx.Tests/bin/Debug/net10.0/D:\event\Kurx\backend\Kurx.Api/`
from every test class whose constructor builds the factory — 183 failures in one observed run, none of
them real. The stack always ends in `HostBuilder.CreateHostingEnvironment`, which is the tell: the failure
is in host startup, before any test code runs, so it cannot be a regression in the code under test.

Fix: delete `bin/` and `obj/` for all five projects and let the container do the build. Better, never run
a host `dotnet build` while a container run is the thing you intend to trust — build in one place only.

Never run two suites at once: every class clones the **shared** `kurx_test_template`, so a second run
racing the first corrupts both. Killing a run leaves `kurx_test_<guid>` databases behind; drop them before
the next run.
