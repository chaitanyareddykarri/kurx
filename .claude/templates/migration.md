# Template: Migration

Fill in before running `dotnet ef migrations add`, per `.claude/memory/database-conventions.md`.

**Migration name**:
**Schema change**: table/column/index added, changed, or removed
**Additive or destructive**: (destructive requires user confirmation first)
**Existing migration check**: confirmed no current migration already covers this
**EF translation check**: any `OrderBy`/projection queries affected by the record-constructor gotcha?
**Applies to**: `kurx` and `kurx_test` — confirm both
