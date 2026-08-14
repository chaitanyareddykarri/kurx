# Review Template: Frontend

Use for `web/`/`admin/` changes.

## Check against

- `.claude/checklists/frontend.md`
- `.claude/memory/frontend-conventions.md` (Server Components default, httpOnly session cookies, env-var correctness)
- No client-exposed tokens or secrets
- Route auth enforcement is real (not dead code — verify the guard actually runs, don't just trust its presence)

## Output

Findings with file:line and the concrete user-visible symptom (wrong redirect, leaked token, broken auth gate).

## Stop condition

Every finding confirmed by reading the actual guard/component logic, and ideally exercised in a browser.
