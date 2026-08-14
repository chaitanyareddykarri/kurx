# Workflow: Testing

Specializes `.claude/workflows/loop-engineering.md` for the Testing stage. Owner: `.claude/agents/qa-engineer.md`. Standards: `.claude/memory/testing-standards.md`. Review: `.claude/reviews/testing-review.md`.

## Objective

Lock behavior in with integration tests against real `kurx_test`, without weakening assertions or violating the shared-DB constraint.

## Preconditions

- The behavior was **manually verified first** (Verification stage) — you're testing the right thing.
- `export DOTNET_ROOT="$HOME/.dotnet"`; Postgres running.

## Step-by-step

1. **Place the test** — in the matching per-domain class (`AuthTests`, `OrgTests`, `EventTests`, `SupportingEntityTests`, …), or a new class following the pattern.
2. **Use the real factory** — `WebApplicationFactory<Program>` (`KurxApiFactory`) against real `kurx_test`. **No mocked persistence, ever.**
3. **Respect shared state** — classes share and reset `kurx_test`; cross-class parallelization is disabled in `AssemblyInfo.cs`. Don't write a test that assumes execution order across classes, and don't re-enable parallelization without per-class DB isolation.
4. **Cover the paths** — success + each error status (403/404/409/…) + the authz boundary (member vs non-member vs admin).
5. **Run** — `dotnet test` green.
6. **Report** — test count before → after, and name what's newly covered.

## Verification

`.claude/checklists/qa.md` + `.claude/reviews/testing-review.md`. Full suite green; new code paths covered; no reduced assertions.

## Exit criteria

Suite green end to end, coverage added for the change, no assertion weakened or test skipped to force green, shared-DB assumption respected.

## Common mistakes

- Mocking the DB/persistence (forbidden — integration only).
- Weakening an assertion or `[Skip]`-ing a test to get green instead of fixing the code.
- A test that depends on another class having run first (shared DB, no ordering guarantee).
- Re-enabling parallel execution.
- Testing only the happy path — the authz `not_found`/`forbidden` branches are where security bugs hide.

## Automation opportunities

- CI runs the full suite against a real Postgres container on every push — the primary regression net.
- Track the test count trend (19→35→52 historically) as a coverage-drift signal (`testing-standards`).
- Web has a `vitest` suite (`npm test`), mobile has `flutter test`. **Admin still has none** — a Playwright or vitest smoke test there is the highest-value remaining add (`.claude/FUTURE-IMPROVEMENTS.md`).
