# Workflow: Backend Development

Specializes `.claude/workflows/loop-engineering.md` for `backend/Kurx.*` work. Owner: `.claude/agents/backend-engineer.md`. Conventions: `.claude/memory/backend-conventions.md`.

## Objective

Add or change a .NET service/endpoint without violating layering, the error model, or the authz model, and with the loop closed against real `kurx_test`.

## Preconditions

- `export DOTNET_ROOT="$HOME/.dotnet"` (else `dotnet` crashes — `database-conventions`).
- Grepped for existing service/endpoint in this domain (D-018 guard).
- Any product ambiguity resolved as a `D-NNN` first.
- Schema changes go through `.claude/workflows/database-migration.md` **before** this.

## Step-by-step

1. **Interface** — add the method to `Kurx.Application.Abstractions`.
2. **Implementation** — implement in `Kurx.Infrastructure/<Domain>` (folder-per-domain). Take `Guid userId, bool isAdmin`; do resource-role checks live against Postgres; throw domain exceptions for expected failures.
3. **Endpoint** — map in `Kurx.Api/Endpoints`; DTO + FluentValidation validator; apply `WithValidation<T>()`. Handler stays thin.
4. **Wire DI** — register the service (a recurring past bug was a service that existed but was never registered — D-018).
5. **Build** — `dotnet build` clean.
6. **Test** — integration test in the matching `Kurx.Tests` class against real `kurx_test`.
7. **Verify live** — curl the endpoint (auth header, real token) or exercise via the compose stack.

## Verification

`.claude/checklists/backend.md` in full. Errors return RFC7807 `ProblemDetails`; test count reported before → after; endpoint exercised live, not just unit-asserted.

## Exit criteria

Build + `dotnet test` green, endpoint verified live or via integration test, `docs/api/README.md` updated if the public contract changed, authz enforced (`KurxAdmin` policy and/or live resource-role check).

## Common mistakes

- Referencing an EF type from an `Api` handler (layering violation).
- Threading `ClaimsPrincipal` into a service instead of passing `userId`/`isAdmin`.
- Reading a role from a token claim instead of querying it live (D-015).
- Hand-rolling an error response instead of throwing a domain exception.
- Building a parallel service when unfinished scaffolding already exists (D-018).
- Forgetting DI registration — the endpoint 500s or the service is dead code.

## Automation opportunities

- `/code-review` after implementation for correctness/reuse findings.
- `security-review` command auto-triggered when the touched domain is auth/payment/PII/KYC.
- CI already runs build+test against a real Postgres container — rely on it as the regression net.
