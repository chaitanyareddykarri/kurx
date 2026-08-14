# Review Template: Database

Use for EF model / migration / query changes in `backend/Kurx.Infrastructure/Persistence`, `Migrations`, `Kurx.Domain/Entities`.

## Check against

- `.claude/checklists/database.md`
- `.claude/memory/database-conventions.md` (migrations, EF gotchas, test isolation)
- `.claude/memory/performance-rules.md` (index coverage)

## Pass/fail criteria (measurable)

- [ ] **No applied migration edited in place** — the change adds a *new* migration file.
- [ ] **Migration applies cleanly** to `kurx` and `kurx_test` (startup auto-migrate or `dotnet ef database update` succeeds; host does not abort).
- [ ] **Money columns** are `bigint` with a `_paise` suffix; **ids** are `uuid` (D-004/D-006). Zero exceptions.
- [ ] **Every new filter/sort column** on a growing table has an index in the **same** migration.
- [ ] **No `OrderBy` on a record-constructor projection** — anonymous type first, map after materialize (else client-side eval).
- [ ] **Destructive change** (drop/rename) has an explicit user-confirmation note; absent → fail.
- [ ] **No duplicate migration** for a change an existing one already covers.
- [ ] Integration tests touching the schema are **green** against real `kurx_test`.

## Output

Findings ranked by severity, each file:line + concrete failure (e.g. "filter on `Event.City` with no index → full scan at N rows").

## Stop condition

All criteria evaluated pass/fail against the actual migration + entity code, not inferred.
