# Workflow: Feature Development

Specializes `.claude/workflows/loop-engineering.md` for net-new functionality. Paired command: `.claude/commands/new-feature.md`.

## Entry

Feature request is concrete enough to identify affected domain(s). If not, resolve via `.claude/agents/architect.md` first — check for a `D-NNN` covering the ambiguity, or write one.

## Planning

- Grep for existing scaffolding first (a recurring pattern here — see D-018). Finishing an unfinished implementation beats building a parallel one.
- Identify which domain agents own the slices involved (backend/frontend/database) and their order (schema → service → endpoint → UI, typically).

## Implementation

Follow each involved domain agent's scope and `.claude/memory/coding-standards.md`. Build vertically (one full slice working end-to-end) rather than horizontally across many partial layers when the feature is small enough.

## Verification

Run `.claude/checklists/` for every surface touched (backend + database + frontend as applicable), then an end-to-end exercise of the new flow — a live smoke test, not just unit/integration tests in isolation.

## Documentation

- New public endpoint → `docs/api/README.md`.
- New structural pattern → `docs/architecture/overview.md`.
- Ambiguous call made along the way → `D-NNN` in `docs/DECISIONS.md`.
- Multi-slice feature completed → note in the relevant `.claude/phases/phaseN.md` if it closes a deliverable.

## Exit

Feature works end-to-end against real dependencies (real `kurx_test` DB, real browser session), documented, tests green.

## Rollback

If a feature slice turns out to conflict with existing scaffolding partway through, prefer reverting the new slice and re-planning over forcing two implementations to coexist.

## Common mistakes

- Building horizontally (many half-layers) instead of one working vertical slice.
- Skipping the existing-scaffolding grep and creating a parallel implementation (the D-018 failure mode).
- Scope creep — implementing adjacent "while I'm here" work not in the plan (CLAUDE.md #5).
- Marking done from tests without the live end-to-end exercise.

## Automation opportunities

- `.claude/templates/feature.md` (or `epic.md`/`user-story.md`) to structure the request up front.
- `/code-review` + `security-review` (if sensitive) before the PR stage.
- CI validates build+test on push; the compose stack validates the full slice.
