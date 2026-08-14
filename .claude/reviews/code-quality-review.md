# Review Template: Code Quality

Use for reuse/simplification/efficiency passes (not correctness bugs — see `.claude/commands/review.md` / `code-review` skill for that). Mirrors the `simplify` skill's lens.

## Check against

- `.claude/memory/coding-standards.md` (no premature abstraction, no restating comments, boundary-only validation)
- Duplicated logic that could collapse to the existing pattern in the same module
- Dead code / unused scaffolding left behind (a recurring issue in this repo's history — see D-018)

## Output

Each finding: what's duplicated/over-engineered, and the simpler existing pattern it should match.

## Stop condition

Findings reported; fixes only applied if requested (matches the `simplify` skill's apply-directly behavior when explicitly invoked).
