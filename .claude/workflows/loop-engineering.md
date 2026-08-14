# Workflow: Loop Engineering

The base loop every other workflow in `.claude/workflows/` specializes. Read this once; other workflows only note where they deviate.

```
PLAN → IMPLEMENT → BUILD → TEST → FIX → REBUILD → RETEST → VERIFY → DOCUMENT → STOP
```

## Entry conditions

A clear task exists (feature, bug, refactor, release) and, if it's product-ambiguous, the ambiguity has been resolved via `.claude/agents/architect.md` (a `D-NNN` entry) before entering the loop.

## Steps

1. **PLAN** — identify affected files/layers using the relevant domain agent's scope. For anything touching >1 domain, note the boundary explicitly.
2. **IMPLEMENT** — smallest change that satisfies the task, following `.claude/memory/coding-standards.md`. No speculative extras.
3. **BUILD** — compile/typecheck (`dotnet build`, `next build`/typecheck). A build failure returns to IMPLEMENT, not to a workaround.
4. **TEST** — run the relevant suite (`.claude/memory/testing-standards.md`). Never skip because "it should work."
5. **FIX** — on failure, diagnose root cause. Do not mute assertions, skip tests, or add `--no-verify` to escape a failure.
6. **REBUILD / RETEST** — repeat 3–4 until green. If stuck after a reasonable number of iterations, stop and report the blocker instead of looping indefinitely.
7. **VERIFY** — run the matching `.claude/checklists/` file. For anything with a runtime surface, exercise it live (curl/browser/compose stack) — passing tests alone is not verification. Use the `verify` skill.
8. **DOCUMENT** — update the doc that would otherwise go stale: `docs/api/README.md`, `docs/architecture/overview.md`, a new `D-NNN`, or a phase file. Skip only if genuinely nothing changed that's documented elsewhere.
9. **STOP** — report what changed, test counts before/after, and what's still open. Don't keep iterating past a completed, verified state.

## Rollback strategy

If FIX doesn't converge and the change is net-negative, revert to the last known-good state (`git stash`/`git checkout <path>` on files you introduced) rather than leaving a half-working change in place. Never force-revert shared history (no `reset --hard` on commits already pushed) without explicit confirmation.

## Failure handling

- Compile/test failure → root-cause fix, not suppression.
- Ambiguous requirement discovered mid-loop → pause, return to PLAN, possibly escalate to architect/user.
- External dependency unavailable (no psql, no real payment provider) → work within `.claude/memory/database-conventions.md` constraints, don't fake a capability that isn't there.

## Stop condition (always defined, never infinite)

The loop ends at a verified green state, or at an explicit blocker reported to the user. It never ends at "probably fine."

## Relationship to the master lifecycle

This is the **inner** loop. It runs once per task inside the **Implementation** stage of `.claude/workflows/loop-engineering-os.md` (the 15-stage outer lifecycle: Discovery → … → Release). Use this file when driving a single task; use the OS file when scoping a whole feature.

## Common mistakes

- Declaring done on a green build without the VERIFY step (compiles ≠ works).
- Muting an assertion / adding `--no-verify` to escape a red TEST instead of root-causing.
- Skipping DOCUMENT because "nothing important changed" when a contract/decision actually did.
- Looping FIX→REBUILD indefinitely instead of stopping and reporting the blocker.

## Automation opportunities

- `/code-review` at the VERIFY boundary for a second pass on correctness/reuse.
- CI (`.github/workflows/ci.yml`) reproduces BUILD+TEST against real Postgres — the shared regression net.
- The `verify` skill scripts the live-exercise step for repeat flows.
