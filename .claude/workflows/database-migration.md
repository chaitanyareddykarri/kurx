# Workflow: Database Migration

Specializes `.claude/workflows/loop-engineering.md` for schema changes. Owner: `.claude/agents/database-engineer.md`. Conventions: `.claude/memory/database-conventions.md`. Review: `.claude/reviews/database-review.md`.

## Objective

Change the Postgres schema safely via EF migrations, verified without a `psql` binary, applied cleanly to both `kurx` and `kurx_test`.

## Preconditions

- `export DOTNET_ROOT="$HOME/.dotnet"`.
- **Check whether an existing migration already covers the need** — a full feature slice once needed zero new migrations (D-018/`database-conventions`). Don't add a redundant one.
- Postgres running (`~/tools/pg/bin/pg_ctl -D ~/tools/pg-data/kurx status`).

## Step-by-step

1. **Model** — change the entity in `Kurx.Domain/Entities` (+ enum in `Kurx.Domain/Enums`). Money columns end `_paise` (`long`/`bigint`, D-004); ids are `Guid`/`uuid` (D-006).
2. **Add migration** — `dotnet ef migrations add <Name>`. **Never edit an already-applied migration** — always add a new one.
3. **Additive-first** — prefer a nullable column + backfill over an in-place breaking rename. A destructive change (drop/rename) requires **user confirmation** (hard to reverse on the shared dev DB).
4. **Index** — add an index in the **same** migration for any new filter/sort column on a growing table (`performance-rules`).
5. **Apply & verify** — startup auto-migrate (`db.Database.MigrateAsync()`) or `dotnet ef database update`. Verify via EF/integration tests, **never** assume a manual `psql` query.
6. **Translate-check** — confirm EF can translate new queries; project record-constructor `OrderBy` to an anonymous type first, map after materializing.

## Verification

`.claude/checklists/database.md` + `.claude/reviews/database-review.md`. Migration applies cleanly to `kurx` and `kurx_test`; integration tests green; no orphaned/duplicate migration file.

## Exit criteria

Migration applied to both DBs, EF translates all new queries, tests green, index coverage for new filters, no edited-in-place applied migration.

## Common mistakes

- Editing an applied migration instead of adding a new one.
- A destructive change without user confirmation.
- Forgetting an index on a new filter column (silent full scans as the table grows).
- `OrderBy` on a record-constructor projection → client-side eval or a translation error.
- Adding a migration when an existing one already covered the change.
- Re-enabling cross-class test parallelization (tests share/reset `kurx_test`).

## Automation opportunities

- CI applies migrations against a real Postgres container — a broken migration fails the build.
- Startup auto-migrate means "did it apply" is answered by the app booting.
- A migration-lint step (naming, no data-loss without a flag) is a reasonable future add.
