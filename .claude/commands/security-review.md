# Command: security-review

## Goal

Audit a change (or the whole diff on the branch) against `.claude/memory/security-rules.md` before it ships. Mandatory for any auth/payment/PII/KYC touching change.

## Inputs

The diff or area to audit.

## Runs

The `security-review` skill (preferred) or `.claude/reviews/security-review.md` template for a manual structured pass.

## Expected outputs

Findings against OWASP top 10, auth-invariant preservation (JWT reuse-revokes-all, live-checked resource roles), and secret handling — each with file:line and concrete exploit scenario.

## Verification

Every finding demonstrated with a concrete input/state, not theoretical.

## Stop condition

Findings reported; if none, explicitly state the auth/payment/PII invariants that were checked and held.
