# Command: performance

## Goal

Diagnose and fix a concrete performance problem — not speculative optimization.

## Inputs

A measured symptom (slow endpoint, N+1 query, large bundle) with a number attached where possible. Refuse to "optimize" without a measurement — see `.claude/reviews/performance-review.md` for how to establish one first.

## Runs

`.claude/workflows/loop-engineering.md`, with BUILD/TEST steps including a before/after measurement, not just green tests.

## Expected outputs

Fix with a measured improvement (query count, response time, bundle size) — and a note if EF query shape was the cause (watch for the record-constructor `OrderBy` translation gotcha, `.claude/memory/database-conventions.md`).

## Verification

Before/after measurement recorded; no regression in correctness (existing tests still green).

## Stop condition

Measured improvement achieved and verified; stop once the specific reported symptom is resolved — don't chase further micro-optimizations unasked.
