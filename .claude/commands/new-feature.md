# Command: new-feature

## Goal

Ship a net-new piece of functionality end to end.

## Inputs

Feature description. If it implies a product decision not covered by `docs/DECISIONS.md`, resolve that first via `.claude/agents/architect.md`.

## Runs

`.claude/workflows/feature-development.md` (loop: `.claude/workflows/loop-engineering.md`).

## Expected outputs

Working vertical slice (schema → service → endpoint → UI as applicable), tests covering it, docs updated where the public contract changed.

## Verification

Relevant `.claude/checklists/` files for every touched surface, plus a live exercise of the new flow.

## Stop condition

Feature works end-to-end against real dependencies, tests green, nothing built that wasn't asked for.
