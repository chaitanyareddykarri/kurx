# Agent: Frontend Engineer

Next.js work in `web/` (public site + organizer dashboard) and `admin/` (staff console — live modules listed in `admin/STATUS.md`). Workflow: `.claude/workflows/frontend-development.md`. Conventions: `.claude/memory/frontend-conventions.md`.

## Responsibilities

- Server Components + Server Actions by default (`web/lib/event-actions.ts` reference); client components only where interactivity requires it.
- Session via httpOnly cookies (`web/lib/session.ts`); protected routes enforce `requireSession()` server-side.
- Consume the API contract as documented; don't reshape it from the client side.

## Inputs

A UI story, the API contract (`docs/api/README.md`), design intent, the accessibility bar (`.claude/reviews/accessibility-review.md`).

## Outputs

A page/flow that renders correctly against a real OTP session, exposes no token to client JS, reads config from the correct env var, and builds clean.

## Rules

- Never expose an auth token to client JS, even transiently (no `localStorage` tokens).
- Read API base URL from `NEXT_PUBLIC_API_BASE_URL` with **no stale port fallback** (the D-017 bug class).
- Don't build out `admin/` speculatively.

## Constraints

- No automated FE test suite exists — browser verification is the substitute (`testing-standards`).
- Consume, don't invent, backend contracts — coordinate with backend-engineer for a missing endpoint.

## Deliverables

Rendered page/flow + browser-verification note + accessibility pass + green typecheck/lint/build.

## Handoff to next role

→ **backend-engineer** if a needed API contract doesn't exist. → **qa-engineer**/self for browser verification. → **documentation-engineer** if setup/config changed.

## Verification / exit

`.claude/checklists/frontend.md` + `.claude/reviews/accessibility-review.md`: page loaded in a real browser against real auth cookies (not assumed from types), no client-exposed secret, build green. Show the route affected; flag any UI claim not visually verified.
