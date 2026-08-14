# Review Template: Testing

Use to review test quality on any change before calling it done. Owner: `.claude/agents/qa-engineer.md`.

## Check against

- `.claude/checklists/qa.md`, `.claude/memory/testing-standards.md`, `.claude/workflows/testing.md`

## Pass/fail criteria (measurable)

- [ ] **Integration, not mocked** — new tests hit real `kurx_test` via `WebApplicationFactory<Program>`; zero mocked persistence.
- [ ] **Error/authz branches covered** — not just the happy path; the `403`/`404`/`409`/`400` paths and the member-vs-non-member-vs-admin boundary have assertions.
- [ ] **No weakened assertions** — no assertion loosened or removed to force green; no `[Skip]` without a tracked root cause.
- [ ] **Shared-DB respected** — no test assumes cross-class execution order; parallelization stays disabled.
- [ ] **Placed correctly** — in the matching per-domain class or a new one following the pattern.
- [ ] **Suite green** — `dotnet test` passes; count reported before → after; new coverage named.
- [ ] **Reproduction test** exists for any bug being fixed (guards recurrence).

## Output

Findings file:line + the gap (e.g. "no test for `JoinOrg` non-member rejection — the SignalR authz boundary is unguarded by tests").

## Stop condition

Every criterion evaluated against the actual test code and a real `dotnet test` run — not asserted from reading the diff.
