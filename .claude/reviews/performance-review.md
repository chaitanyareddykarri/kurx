# Review Template: Performance

Use only against a measured symptom — never a speculative "this might be slow."

## Check against

- `.claude/checklists/performance.md`
- N+1 queries, missing indexes, unnecessary EF projections
- Client bundle size / unnecessary client components in `web/`
- The EF `OrderBy`-on-record-projection gotcha (forces client-side evaluation if mistranslated)

## Output

Before/after measurement, root cause, fix — in that order. No fix proposed without a measurement backing it.

## Stop condition

Reported symptom resolved and quantified; no scope creep into unrelated optimization.
