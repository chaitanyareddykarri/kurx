# Checklist: QA

- [ ] Reproduction/coverage case written before the fix (proves it's real)
- [ ] Full `Kurx.Tests` suite green against real `kurx_test`
- [ ] No cross-class parallelization re-enabled without per-class DB isolation
- [ ] No weakened/skipped assertions to force green
- [ ] Runtime-surface changes exercised live, not just via test suite (`verify` skill)
- [ ] Test count reported before → after
