# Review Template: Backend

Use for .NET changes in `backend/Kurx.*`.

## Check against

- `.claude/checklists/backend.md`
- `.claude/memory/api-conventions.md` (validation, error shape, auth pattern)
- `.claude/memory/coding-standards.md` (.NET section)
- EF query translation gotchas (`.claude/memory/database-conventions.md`)

## Output

Findings ranked by severity, each with file:line and a concrete failure scenario (bad input → wrong status code/leak/crash).

## Stop condition

All findings verified against actual code, not inferred from a skim.
