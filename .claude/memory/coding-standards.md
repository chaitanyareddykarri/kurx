# Coding Standards

## Philosophy (applies to every language in this repo)

- No premature abstraction. Three similar lines beat a shared helper built for one caller.
- No speculative flags, feature toggles, or future-proofing for requirements not yet stated.
- No comments that restate code. Only comment a non-obvious constraint, workaround, or invariant.
- Validate only at system boundaries (HTTP request DTOs, external API responses) — trust internal calls and framework guarantees.
- Match existing patterns in the file/module you're editing over introducing a new one, unless the task is specifically to change the pattern.

## .NET (`backend/`)

- Request DTOs validated via FluentValidation + `WithValidation<T>()` filter, not manual `if` checks in handlers.
- Services expose interfaces in `Kurx.Application`, implementations in `Kurx.Infrastructure` — never reference EF types from `Kurx.Api` handlers directly.
- Authorization: pass `bool isAdmin` into services rather than threading `ClaimsPrincipal` through the domain/infrastructure layers.
- Errors: throw domain exceptions, let `GlobalExceptionHandler` translate to RFC7807 `ProblemDetails` — don't hand-roll error responses per endpoint.

## TypeScript / Next.js (`web/`, `admin/`)

- Server Components + Server Actions by default; `"use client"` only where interactivity requires it.
- Session state via httpOnly cookies only — never pass tokens to client-side JS.
- Env var reads for API base URLs must match the actual runtime var name and have no stale port fallback (a real bug here already, see `docs/DECISIONS.md`/git history for the fix).

## Naming and structure

Follow the existing folder-per-domain convention (`Infrastructure/Orgs`, `Infrastructure/Events`, etc.) — new domains get their own folder, not a dump into a shared one.
