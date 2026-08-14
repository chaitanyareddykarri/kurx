# Command: review

## Goal

Review a diff (working tree, branch, or PR) for correctness and quality issues without applying fixes unless asked.

## Inputs

The diff to review (defaults to current working tree changes vs. base branch).

## Runs

`.claude/workflows/review.md`. For deep/high-risk diffs, layer in the matching `.claude/reviews/*.md` template. For the actual multi-pass tool, prefer the `code-review` skill over improvising.

## Expected outputs

Ranked findings, each with file:line and a concrete failure scenario.

## Verification

Every finding re-checked against the actual code before being reported — no findings from a skim.

## Stop condition

Findings reported (possibly empty). No edits made unless explicitly requested.
