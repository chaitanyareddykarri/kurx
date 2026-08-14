# Workflow: Release

Specializes `.claude/workflows/loop-engineering.md` for shipping a branch/phase. Paired command: `.claude/commands/release.md`.

## Entry

A branch has reached a coherent, tested stopping point (e.g. a phase's completion criteria from `.claude/phases/phaseN.md` are met).

## Planning

Confirm CI (`.github/workflows/ci.yml`) is green on the branch. Confirm the phase's completion criteria are actually met, not assumed.

## Implementation

No new features in this pass — release is about verifying and shipping what exists, per `.claude/agents/devops.md` scope.

## Verification

- `docker compose up --build` boots all services healthy (postgres, redis, backend, web, admin).
- Full test suite green.
- `.claude/checklists/release.md` fully checked.
- Live smoke test of the core flow(s) the release affects.

## Documentation

Update `docs/roadmap/README.md` status and the relevant `.claude/phases/phaseN.md` completion state. Summarize what shipped for the commit/PR description (why, not a diff restatement).

## Exit

CI green, compose stack healthy, phase marked complete, changes committed per user instruction (never pushed/merged without explicit request).

## Rollback

If post-verification a regression is found, do not ship — return to `.claude/workflows/bug-fix.md` for that regression before re-attempting release.

## Common mistakes

- Assuming phase completion criteria are met instead of checking them against `.claude/phases/phaseN.md`.
- Updating the phase file but not `docs/roadmap/README.md` (or vice-versa) — the two drift.
- Bundling an unreviewed prod secret/infra change into the release.
- Trusting a green test suite without the `docker compose up --build` all-healthy check.

## Automation opportunities

- `.claude/checklists/release.md` is the gate — run it top to bottom.
- CI gates the branch; the compose stack gates the full-system smoke test.
- Release notes from `.claude/templates/release.md`.
