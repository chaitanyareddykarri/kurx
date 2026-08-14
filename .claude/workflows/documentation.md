# Workflow: Documentation

Specializes the Documentation + Decision Logging stages of `.claude/workflows/loop-engineering-os.md`. Owner: `.claude/agents/documentation-engineer.md`. Trigger rules: `.claude/workflows/continuous-learning.md`. Review: `.claude/reviews/documentation-review.md`.

## Objective

Keep docs describing **shipped reality**, not intent — update exactly the docs a change makes stale, and no more.

## Preconditions

- Behavior is finalized (docs follow the code, never lead it).
- You've run the continuous-learning trigger matrix to know which docs this change touches.

## Step-by-step

1. **Run the trigger matrix** (`.claude/workflows/continuous-learning.md`) — it maps change-type → doc(s) that must update.
2. **Public contract change** → `docs/api/README.md`.
3. **Structural/architectural change** → `docs/architecture/overview.md` (owned by architect); fix the `.claude/memory/architecture.md` pointer if it now misleads.
4. **Ambiguous call made** → `D-NNN` in `docs/DECISIONS.md` (the spec) via `.claude/templates/decision.md`; update `.claude/memory/decision-log.md` if load-bearing.
5. **Convention changed** → the matching `.claude/memory/*.md` (single source of truth — don't duplicate the rule elsewhere).
6. **Phase deliverable closed** → `.claude/phases/phaseN.md` + `docs/roadmap/README.md`.
7. **Setup/run change** → `README.md` / `docs/deployment/`.

## Verification

`.claude/reviews/documentation-review.md`: every doc named by the trigger matrix is updated; nothing now contradicts the code; no duplicated source of truth introduced.

## Exit criteria

All triggered docs updated to match shipped behavior; `docs/DECISIONS.md` captures every ambiguous call; no doc drift left.

## Common mistakes

- Documenting intent before the behavior is verified.
- Copying a rule into two files (creates a second source of truth that drifts — the whole memory/ layer exists to prevent this).
- Closing a phase in the phase file but not `docs/roadmap/README.md` (or vice-versa).
- Leaving a decision only in a commit message or memory instead of `docs/DECISIONS.md`.

## Automation opportunities

- The continuous-learning matrix is the checklist; run it every loop.
- Generate `docs/api/README.md` entries from DTOs/validators (future).
- A doc-freshness CI check (fail if an endpoint exists with no `docs/api/README.md` entry) is a reasonable future gate.
