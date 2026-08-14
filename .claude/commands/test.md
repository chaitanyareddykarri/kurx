# Command: test

## Goal

Add or improve test coverage for existing code, without changing production behavior.

## Inputs

Code path or service lacking coverage.

## Runs

`.claude/agents/qa-engineer.md` scope + `.claude/memory/testing-standards.md`.

## Expected outputs

New integration test(s) against real `kurx_test` (backend) following the `WebApplicationFactory<Program>` pattern, or a manual browser verification note (frontend, no test runner yet).

## Verification

New test fails against the pre-fix/pre-change code (proves it tests something real) and passes after; full suite still green.

## Stop condition

Coverage added, suite green, no assertions weakened elsewhere to compensate.
