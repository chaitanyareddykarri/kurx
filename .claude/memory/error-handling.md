# Error Handling

One error model across the whole backend, established in Phase 1 (D-017). Any new endpoint inherits it for free — don't reinvent it per handler.

## The model

- **Every** error response — validation failure, expected business error (403/404/409/…), unhandled exception — is RFC7807 `ProblemDetails`. No raw strings, no ad-hoc `{ "message": … }` shapes.
- `GlobalExceptionHandler` is the single translation point from thrown exception → `ProblemDetails`. Handlers and services throw; they never build an error response.
- Backwards-compat extensions on the `ProblemDetails` body:
  - `error` — the original string error code (`"not_found"`, `"forbidden"`, `"conflict"`, …), preserved so pre-existing `{error:"..."}` consumers and tests keep working.
  - `correlationId` — echoes `X-Correlation-Id` for cross-referencing logs (`.claude/memory/logging.md`).

## How to signal each failure class

| Situation | How | Resulting status |
|---|---|---|
| Bad request shape / invalid field | FluentValidation via `WithValidation<T>()` | 400 |
| Not authenticated | missing/invalid JWT (framework) | 401 |
| Authenticated but not allowed | throw domain `forbidden` | 403 |
| Resource absent — or hidden from this caller | throw domain `not_found` | 404 |
| State/uniqueness conflict | throw domain `conflict` | 409 |
| Rate limit exceeded | limiter / `OtpService` counter (D-005) | 429 |
| Unexpected | let it bubble to `GlobalExceptionHandler` | 500 |

## Rules

- **404 over 403 for hidden resources.** A draft event returns `not_found` (never `403`) to non-members so a guessed id can't confirm the resource exists (D-018 draft-visibility).
- **No internal leakage.** Stack traces, SQL, exception types, and connection strings never reach a client body — `security-rules` + `ProblemDetails` guarantee this. Full detail goes to structured logs only.
- **Validate at boundaries only** (`coding-standards`). Trust internal calls; don't defensively re-validate service inputs that a validator already checked.
- **Fail closed on startup.** Secret/connection-string validation (`SecretValidation`) aborts the host rather than serving traffic with a weak/default secret in Production (D-017). A failed EF migration also aborts startup rather than serving a stale schema (`database-conventions`).

## Frontend surfacing

`web/` reads the `error` extension for known codes and the `detail`/`title` for display. Don't surface `correlationId` or internal fields to end users; log them for support instead.
