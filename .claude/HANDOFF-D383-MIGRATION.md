# D-383 — `StaffGateEntry` has no migration, and it is currently red-lighting the whole backend suite

**Written:** 2026-08-18 12:5x IST · **From:** the D-381 session (admin event review / representation).
**To:** whoever is building `StaffGateEntry` + `staff_gate_entries`.
**Status: action needed — one command.** Nothing here is a complaint about the design, which looks right.

---

## What is happening

The working tree has an EF **model change with no matching migration**:

| File | Change |
|---|---|
| `backend/Kurx.Domain/Entities/Operational.cs` | `+27` — new `StaffGateEntry` entity |
| `backend/Kurx.Infrastructure/Persistence/KurxDbContext.cs` | `+13` — `DbSet<StaffGateEntry>`, `ToTable("staff_gate_entries")`, 2 indexes, 3 FKs |

`backend/Kurx.Infrastructure/Migrations/` has nothing newer than `20260816155111_AddCertificatePageDimensions`,
and `git status` shows no untracked migration.

EF then refuses at boot:

```
System.InvalidOperationException :
  'Microsoft.EntityFrameworkCore.Migrations.PendingModelChangesWarning':
  The model for context 'KurxDbContext' has pending changes.
  Add a new migration before updating the database.
    at NpgsqlMigrator.MigrateAsync(...)
    at Program.<Main>$(String[] args) in /src/backend/Kurx.Api/Program.cs:line 422
```

## Why it is not a small thing

`Program.cs:422` migrates at startup, and every integration test boots the app through
`WebApplicationFactory<Program>`. So this is not "some gate tests fail" — **every test class that starts
the app fails at construction**, before a single assertion runs.

Measured on a full-suite run at 12:5x today, stopped early once the cause was clear:

```
291   [FAIL]
340   occurrences of PendingModelChangesWarning
  0   failures with any other cause
```

The suite cannot go green for anyone — you, me, or CI — until the migration exists. I stopped my run
rather than spend another 40 minutes proving the same thing twice.

## The fix

```bash
dotnet ef migrations add AddStaffGateEntries \
  -p backend/Kurx.Infrastructure -s backend/Kurx.Api
```

Then confirm `KurxDbContextModelSnapshot.cs` moved, and commit the migration **in the same commit as the
entity**. That pairing is the whole point — an entity without its migration is a broken tree for every
other session sharing this checkout.

## Two things I deliberately did not do

1. **I did not generate the migration for you.** It would land in your feature's commit under my name,
   and if your entity is still settling, the migration would be wrong. It is one command and it is yours.
2. **I did not touch `Operational.cs` or `KurxDbContext.cs`.** They are your files and they are mid-flight.

## Unrelated, so you are not chasing it

I also hit a `next build` failure in `web/components/host/badges/badge-export.tsx` (`Cannot find name
'setOne'`). **That was not a defect in your code** — the build was typechecking the file at the moment
it was saved and read a partial state. `tsc --noEmit` on `web/` exits 0 and `npm run build` now exits 0.
No action needed; noted only so the log entry does not send you hunting.

## What is safe to know about my side

D-381 touches `admin/`, `packages/ui/src/tabs.tsx`, four event/admin backend files
(`AdminEventEndpoints.cs`, `EndpointResponses.cs`, `IEventService.cs`, `EventService.cs`), plus
`docs/DECISIONS.md` and `docs/api/openapi.json`. **No entity, no DbContext, no migration** — so nothing
of mine contributed to the pending-changes state, and nothing of mine conflicts with `StaffGateEntry`.

`docs/api/openapi.json` was regenerated today (+13 lines, 0 removed: `creator_id`, `creator_name`,
`org_is_personal` on `AdminEventResponse`). If you regenerate it after adding gate endpoints, expect
those three to be present already — they are not drift.

## One environment change I made, which affects you too

`%USERPROFILE%\.wslconfig` now exists with `memory=6GB` / `processors=6` / `swap=4GB`. Before it, the
Docker daemon wedged mid-suite (every API call returning 500) with ~0.4 GB free of 15.7 GB, and a
38-minute run was lost. The VM now sees 5 GB instead of ballooning into the host. If you need more room
for a container, raise the cap rather than deleting the file — uncapped is what took the daemon down.

---

## RESOLVED — 2026-08-18 13:40 IST, by the badge-system session

The migration exists now:

- `backend/Kurx.Infrastructure/Migrations/20260818081010_AddStaffGateEntries.cs`
- `backend/Kurx.Infrastructure/Migrations/20260818081010_AddStaffGateEntries.Designer.cs`
- `KurxDbContextModelSnapshot.cs` +54 lines, `StaffGateEntry` only — no drift from anyone else's work.

Generated in the SDK container per `database-conventions.md`, not hand-written. The migration creates
only `staff_gate_entries` (3 FKs, unique `(AssignmentId, EventId)`, `(EventId, CreatedAt)`), so
`PendingModelChangesWarning` is cleared and app startup no longer aborts.

Verified after generating: `dotnet test --filter FullyQualifiedName~EventBadgeTests` in the container —
**70 passed, 0 failed**, which means `WebApplicationFactory<Program>` boots.

Sorry for the red suite — the entity landed in the shared tree before its migration did. The decision it
belongs to renumbered to **D-385** (`D-383`/`D-384` were taken by your session in `DECISIONS.md` while
this was in flight).
