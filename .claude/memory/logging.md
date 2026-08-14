# Logging

Structured logging established in Phase 1 (D-017). Correlates a request across the API, hubs, and (future) background jobs.

## What's enriched onto every log

- `CorrelationId` — from the request's `X-Correlation-Id` header (generated if absent), echoed back in the response and in the `ProblemDetails.correlationId` extension (`.claude/memory/error-handling.md`).
- `UserId` — when authenticated.
- `OrgId` — when the request is org-scoped.

These make it possible to trace one user action across every log line it produced.

## Levels

- `Error` — unhandled exceptions (via `GlobalExceptionHandler`), failed startup validation, migration failure. Something an operator must see.
- `Warning` — expected-but-notable: rate-limit hits, refresh-token reuse detection (triggers revoke-all, D-014), KYC rejections.
- `Information` — request lifecycle, state transitions (event publish/unpublish), successful auth.
- `Debug` — dev-only detail; off in Production.

## Hard rules

- **Never log a secret or raw PII.** No OTP codes (stored hashed anyway, D-008), no JWTs, no refresh tokens, no full bank account numbers (only `bank_last4` ever exists — D-016), no connection strings, no `JWT_SECRET`.
- **Never log a full request/response body** on auth/payment/KYC endpoints — log the correlation id and outcome, not the payload.
- Log the **outcome and the why**, not a restatement of the happy path. A log line should help diagnose a failure, not narrate success.
- Errors are logged in full server-side (stack trace, exception type) precisely *because* the client body must not contain them (`error-handling.md`).

## When adding a feature

- New state transition worth tracing (publish, payout, check-in) → one `Information` line with the enriched context, no payload.
- New external call (once real providers exist) → log request id + outcome + latency, never credentials or full payload.
- Don't add per-line `Console.WriteLine`/`Debug` noise to production paths; use the injected `ILogger<T>` at the right level.

See also: `.claude/memory/observability.md` (health/metrics/tracing) and `.claude/memory/security-rules.md` (insufficient-logging is an OWASP item — auth events must be logged).

## Phone numbers are masked, never logged whole (D-290)

Use `PhoneCanonicalizer.Mask` — last four digits, length-agnostic so it is correct for every numbering plan.
Shared by `OtpService`, `SnsSmsProvider` and `WhatsAppLogService`; do not hand-roll another. A masked value
still answers the only question logs need to ("is this the destination I expected?") because whoever is
asking already knows the number.
