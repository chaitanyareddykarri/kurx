# Command: release

## Goal

Verify a branch/phase is actually ready to ship.

## Inputs

Branch name or phase identifier (`.claude/phases/phaseN.md`).

## Runs

`.claude/workflows/release.md`.

## Expected outputs

Punch list: CI status, compose-stack health, completion criteria met/unmet, docs/roadmap updated.

## Verification

`.claude/checklists/release.md` fully checked; live smoke test of the core flow(s) affected.

## Stop condition

Either "ready" with all checklist items green, or a specific, named blocker list — never a vague "mostly done."
