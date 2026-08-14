# Workflow: Bug Fix

Specializes `.claude/workflows/loop-engineering.md` for defects. Paired command: `.claude/commands/bugfix.md`.

## Entry

A reproducible symptom exists (failing test, wrong output, crash) or can be made reproducible before any fix is attempted. Don't fix a bug you can't first demonstrate.

## Planning

- Reproduce first — write/run the failing case before touching source.
- Root-cause, don't patch symptoms. E.g. the `web/lib/site.ts` env-var bug was fixed at the source-of-truth var name, not by adding a fallback on top of a fallback.

## Implementation

Smallest change that fixes the root cause. Resist the urge to refactor surrounding code in the same pass — file that separately if warranted.

## Verification

- The original reproduction case now passes.
- Full relevant test suite still green (regression check).
- If the bug had a runtime-visible symptom, re-exercise it live to confirm the fix, not just via the new test.

## Documentation

Note the root cause in the commit message ("why", not "what"). Only add to `docs/DECISIONS.md` if the fix reveals a previously-undocumented architectural assumption.

## Exit

Reproduction case fixed, no regressions, root cause addressed (not papered over).

## Rollback

If the fix doesn't resolve the root cause on first attempt, revert and re-diagnose rather than layering a second patch on an incorrect first one.

## Common mistakes

- Attempting a fix before reproducing the symptom.
- Patching a symptom (stacking a fallback) instead of fixing the source of truth (the D-017 env-var lesson).
- Bundling an unrelated refactor into the fix, enlarging the blast radius.
- Not adding a regression test, so the same bug can recur silently.

## Automation opportunities

- Turn the reproduction into a permanent integration test (`.claude/workflows/testing.md`) — CI then guards recurrence.
- For urgent/release-blocking defects, escalate to `.claude/workflows/hotfix.md` (compressed, gates intact).
