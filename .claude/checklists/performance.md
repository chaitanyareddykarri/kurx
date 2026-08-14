# Checklist: Performance

- [ ] Symptom measured before any change (query count, latency, bundle size)
- [ ] Root cause identified (e.g. N+1 query, unindexed lookup, unnecessary client bundle)
- [ ] Fix applied without changing correctness (existing tests still green)
- [ ] Measurement repeated after the fix, improvement quantified
- [ ] No unrequested speculative optimization beyond the reported symptom
