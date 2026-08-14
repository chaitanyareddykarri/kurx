# Workflow: API Development

Specializes `.claude/workflows/backend-development.md` for the **public contract** specifically. Owner: `.claude/agents/backend-engineer.md`. Rules: `.claude/memory/api-conventions.md`, `.claude/memory/versioning.md`. Review: `.claude/reviews/api-review.md`.

## Objective

Add or change an HTTP endpoint under `/v1/…` such that the contract is consistent, validated, versioned, documented, and safe to depend on.

## Preconditions

- The backend service the endpoint calls exists (or is being built in the same slice via `backend-development.md`).
- For a change to a **shipped** contract: confirmed it's additive, or a `D-NNN` covers the break/new version (`versioning.md`).

## Step-by-step

1. **Shape the contract** — route (`/v1/…`), method, request DTO, response DTO, status codes. Public discovery endpoints stay read-only and use real data (no fabricated metrics — D-018).
2. **Validate** — FluentValidation validator + `WithValidation<T>()`. Validate at this boundary; trust the service below it.
3. **Authz** — `KurxAdmin` policy for platform-admin gates; live resource-role check for org/event-scoped access. Hidden resources return `not_found`, not `403` (`error-handling.md`).
4. **Errors** — rely on the global RFC7807 model; every error path returns `ProblemDetails` with the `error` code + `correlationId` extensions.
5. **Correlation** — endpoint participates in `X-Correlation-Id` enrichment automatically; don't strip it.
6. **Test** — integration test covering success + each error status against real `kurx_test`.
7. **Document** — update `docs/api/README.md` for any contract change.

## Verification

`.claude/checklists/backend.md` + `.claude/reviews/api-review.md`. Each status code exercised; `docs/api/README.md` matches reality.

## Exit criteria

Endpoint under a version prefix, validated, authorized, error-consistent, tested for success + failures, documented. No breaking change to `/v1` without a `D-NNN`.

## Common mistakes

- New error shape instead of `ProblemDetails` (breaks the uniform model).
- Fabricating/stubbing a metric on a public discovery endpoint (must be real data).
- `403` where `404` is required for a hidden resource.
- Silently tightening validation on a shipped field (a breaking change — needs `versioning` treatment).
- Forgetting `docs/api/README.md` — the contract drifts from the doc.

## Automation opportunities

- Contract snapshot/diff in CI to catch unintended `/v1` breaks (future).
- `security-review` when the endpoint touches auth/payment/PII.
- Generate the `docs/api/README.md` entry from the DTO + validator where possible.
