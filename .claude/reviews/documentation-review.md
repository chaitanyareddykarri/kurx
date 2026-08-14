# Review Template: Documentation

Use to confirm a change left the docs describing shipped reality. Owner: `.claude/agents/documentation-engineer.md`. Trigger rules: `.claude/workflows/continuous-learning.md`.

## Check against

- `.claude/workflows/documentation.md`, `.claude/workflows/continuous-learning.md` (the update-trigger matrix)

## Pass/fail criteria (measurable)

- [ ] **Trigger matrix run** — every doc the change-type maps to has been checked.
- [ ] **Contract change → `docs/api/README.md`** updated; no shipped endpoint missing an entry.
- [ ] **Structural change → `docs/architecture/overview.md`** updated; `.claude/memory/architecture.md` pointer still accurate.
- [ ] **Ambiguous call → `D-NNN`** in `docs/DECISIONS.md` (the spec) — no decision left only in a commit/memory/template.
- [ ] **Convention change → the one `.claude/memory/*.md`** that owns it — no rule duplicated into a second file (no new source of truth to drift).
- [ ] **Phase deliverable closed → both** `.claude/phases/phaseN.md` **and** `docs/roadmap/README.md` (not one or the other).
- [ ] **No doc contradicts the code** as shipped.
- [ ] **No documented intent** presented as shipped behavior.

## Output

Findings: which doc is stale/missing/duplicated, and the exact drift from code.

## Stop condition

Every triggered doc verified against the shipped change; no drift and no duplicated source of truth remain.
