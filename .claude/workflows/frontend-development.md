# Workflow: Frontend Development

Specializes `.claude/workflows/loop-engineering.md` for `web/` and `admin/` (both real, built-out Next.js apps — see `admin/STATUS.md` for admin's current build status). Owner: `.claude/agents/frontend-engineer.md`. Conventions: `.claude/memory/frontend-conventions.md`.

## Objective

Ship a Next.js UI change that renders correctly against a **real session**, exposes no token to client JS, and reads config from the correct env var.

## Preconditions

- The API contract the UI consumes exists (`docs/api/README.md`); if not, coordinate with `backend-engineer` — don't stub a shape and move on.
- Both `web/` and `admin/` share `@kurx/ui` (`packages/ui/`) for tokens and primitives — check there before adding a new component locally to either app.

## Step-by-step

1. **Server-first** — Server Components + Server Actions by default (`web/lib/event-actions.ts` is the reference). Add `"use client"` only where interactivity truly requires it.
2. **Session** — auth via httpOnly cookies (`web/lib/session.ts`); protected routes enforce `requireSession()` server-side. Never pass a token to client JS, even transiently.
3. **Config** — read API base URL from the correct env var (`NEXT_PUBLIC_API_BASE_URL`) with **no stale port fallback** (the D-017 bug class).
4. **Build** — typecheck + lint + `next build` clean.
5. **Verify in a browser** — load the actual page against a real OTP session and exercise the flow.

## Verification

`.claude/checklists/frontend.md`. Typecheck/lint/build are necessary but **not sufficient** — a UI change is not done until loaded in a browser against real auth cookies.

## Exit criteria

Page renders and the flow works in a real browser session; no client-exposed secret; env config correct; build green. (No automated FE test suite exists yet — manual browser verification is the substitute, `testing-standards`.)

## Common mistakes

- Reintroducing the stale-fallback / wrong-env-var-name bug (D-017).
- Writing tokens to `localStorage` instead of using the `saveSession` server action (a real past bug).
- Route group that *looks* protected but the session check is dead code (found dead once — verify it actually runs).
- Marking a UI change done from a green typecheck without loading the page.
- Duplicating a UI primitive locally in `web/` or `admin/` instead of checking `@kurx/ui` first (see D-185: eight primitives existed only in `web/` and had never been promoted).

## Automation opportunities

- CI already runs typecheck/lint/build for `web`/`admin`.
- Introduce a Playwright smoke test for the OTP-login → dashboard flow (would remove the "manual browser only" gap — a genuine future improvement, see `.claude/FUTURE-IMPROVEMENTS.md`).
