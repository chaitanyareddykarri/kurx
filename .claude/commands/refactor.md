# Command: refactor

## Goal

Improve internal structure without changing external behavior.

## Inputs

Scope of the refactor (a file, module, or pattern to eliminate). Must be explicitly requested — refactors are never bundled unasked into a feature/bugfix pass.

## Runs

`.claude/workflows/loop-engineering.md` directly (no dedicated refactor workflow — behavior-preservation is the only extra constraint).

## Expected outputs

Same external behavior, cleaner internals, matching `.claude/memory/coding-standards.md`.

## Verification

Full existing test suite green with zero changes to test expectations (a test needing to change signals a behavior change, which is out of scope unless flagged and confirmed).

## Stop condition

Structure improved, behavior identical, tests green, no drive-by scope creep into unrelated files.
