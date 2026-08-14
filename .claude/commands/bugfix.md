# Command: bugfix

## Goal

Fix a defect at its root cause.

## Inputs

Symptom description (failing test, wrong output, crash) or steps to reproduce.

## Runs

`.claude/workflows/bug-fix.md` (loop: `.claude/workflows/loop-engineering.md`).

## Expected outputs

Reproduction case captured (as a test where possible), root cause fixed, no unrelated refactors bundled in.

## Verification

Reproduction case now passes; full relevant suite still green; if user-visible, re-exercised live.

## Stop condition

Bug fixed at the root, no regressions, no papered-over symptom.
