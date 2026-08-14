# Template: Performance Review Report

The **output** scaffold for a performance review pass. The pass follows `.claude/reviews/performance-review.md` and `.claude/checklists/performance.md`. Rules: `.claude/memory/performance-rules.md`.

```markdown
# Performance Review: <change / branch / PR>

**Reviewer agent**: performance-engineer · **Surface**: <endpoints/queries/UI>

## Queries examined
| Endpoint/query | Paginated? | Query count | Index-backed filter/sort? | N+1? |
|---|---|---|---|---|
| <GET /v1/...> | yes/no | 1 / N | yes/no | none/found |

## Checks
| Check | Pass? | Note |
|---|---|---|
| Every list endpoint paginated | | |
| No N+1 (EF projection/OrderBy translation ok) | | |
| New filter/sort columns index-backed (same migration) | | |
| No synchronous external/IO call on a request hot path | | |
| No unbounded result set returned | | |
| View-count / hot writes stay single cheap ops | | |

## Findings
1. **[severity]** <title> — `path:line`
   - Impact: <what degrades, at what scale>
   - Fix: <index / paginate / move to background / project>

## Verdict
- [ ] No regression — checks above pass
- [ ] Findings above must be addressed
```

Rule: don't invent a caching/optimization finding for load that doesn't exist — flag a **measured or structurally-certain** problem (missing index on a filtered column, an unpaginated list, an N+1), not speculative tuning (`performance-rules`).
