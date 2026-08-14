# Review Template: API

Use for HTTP contract changes under `/v1/…` and SignalR hubs. Complements `.claude/reviews/backend-review.md` with a contract-consistency lens.

## Check against

- `.claude/memory/api-conventions.md`, `.claude/memory/versioning.md`, `.claude/memory/error-handling.md`
- `docs/api/README.md` (the contract of record)

## Pass/fail criteria (measurable)

- [ ] **Versioned path** — new/changed endpoint lives under `/v1/…` (hubs under `/hubs/*`).
- [ ] **Request DTO validated** via FluentValidation + `WithValidation<T>()` — no manual `if` guards.
- [ ] **Every error path returns RFC7807 `ProblemDetails`** with the `error` code + `correlationId` extensions — no raw string / ad-hoc JSON.
- [ ] **Authz present** — `KurxAdmin` policy and/or live resource-role check; hidden resources return **404, not 403** (D-018).
- [ ] **No breaking `/v1` change** (removed/renamed field, changed status meaning, tightened validation on a shipped field) without a `D-NNN`.
- [ ] **Public discovery endpoints** stay read-only and use real data — zero fabricated/stubbed metrics.
- [ ] **`docs/api/README.md` updated** for any contract change.
- [ ] **Integration test** covers success + each error status.

## Output

Findings file:line + concrete failure (e.g. "removing `subtitle` from the event DTO breaks existing `/v1` consumers with no version bump").

## Stop condition

Every criterion evaluated against the endpoint, DTO, validator, and `docs/api/README.md` — not a skim.
