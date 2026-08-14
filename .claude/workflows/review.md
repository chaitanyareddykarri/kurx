# Workflow: Review

Specializes `.claude/workflows/loop-engineering.md` for reviewing a diff/PR rather than authoring one. Paired command: `.claude/commands/review.md`. For a deliberate, non-diff-triggered review pass, use `.claude/reviews/` templates instead.

## Entry

A diff exists (working tree changes, a branch vs. main, or a PR) to review.

## Planning

Pick the review lens matching the change: correctness (default), or a structured pass from `.claude/reviews/` (architecture/backend/frontend/security/performance/code-quality) if the diff is large or high-risk.

## Implementation

Read the diff against `.claude/memory/coding-standards.md` and the owning domain agent's scope. Flag deviations, not style preferences already satisfied.

## Verification

Every finding must be traceable to a concrete failure scenario (bad input → wrong output/crash), not a hypothetical "could be an issue." Verify a finding against the actual code before reporting it — don't report from a skim.

## Documentation

Findings reported most-severe first, each with file:line and the concrete failure case. No fix applied unless asked.

## Exit

Findings reported (or empty list if none survive verification). No unrequested edits made during a review pass.

## Rollback

Not applicable — review is read-only unless the user explicitly asks for fixes to be applied, in which case follow `.claude/workflows/bug-fix.md` per finding.

## Common mistakes

- Reporting a finding from a skim without verifying it against the actual code.
- Flagging style preferences already satisfied instead of real defects.
- Applying fixes during a review pass that was asked to be read-only.
- Reporting vague concerns ("could be an issue") with no concrete failure scenario.

## Automation opportunities

- `/code-review` at low/medium for high-confidence findings, high/max for broader coverage.
- Route large/high-risk diffs through the matching `.claude/reviews/*-review.md` structured pass.
- `security-review` command auto-applies for auth/payment/PII/KYC diffs.
