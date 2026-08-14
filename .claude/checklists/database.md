# Checklist: Database

- [ ] Checked whether an existing migration already covers this need
- [ ] New migration added via `dotnet ef migrations add <Name>` (never hand-edited an applied one)
- [ ] No raw SQL run against dev DB outside of EF (no `psql` available anyway)
- [ ] `OrderBy` on record-constructor projections avoided (project anonymous, map after)
- [ ] Migration applies cleanly to both `kurx` and `kurx_test`
- [ ] Destructive change (drop/rename) confirmed with user first
- [ ] Integration tests green after migration
