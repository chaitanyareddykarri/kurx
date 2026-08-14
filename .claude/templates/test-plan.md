# Template: Test Plan

Fill during Task Breakdown / before the Testing stage (`.claude/workflows/loop-engineering-os.md` §7). Standards: `.claude/memory/testing-standards.md`.

```markdown
# Test Plan: <feature/story>

## What's under test
<the behavior, endpoints, state transitions>

## Test type
Integration against real `kurx_test` (default — no mocked persistence).
Manual browser verification for any UI (no FE suite yet).

## Cases
| # | Scenario | Setup | Action | Expected | Status code |
|---|---|---|---|---|---|
| 1 | happy path | <seed> | <call> | <result> | 200 |
| 2 | unauthorized (non-member) | | | forbidden | 403 |
| 3 | hidden resource (non-member) | | | not_found | 404 (not 403) |
| 4 | conflict/duplicate | | | conflict | 409 |
| 5 | invalid input | | | validation error | 400 |

## Authz matrix (per D-015 where org-scoped)
| Role | Allowed? |
|---|---|
| Owner | |
| Manager | |
| Staff | |
| Finance | |
| Platform admin (KurxAdmin) | |
| Non-member | |

## Placement
Test class: <AuthTests/OrgTests/EventTests/SupportingEntityTests/new>
Shared-DB constraint respected (no cross-class ordering assumption).

## Live acceptance check
<the curl/browser/compose exercise proving it end-to-end>

## Coverage report
Test count before → after: <n> → <m>. Newly covered: <paths>.
```

Rule: the error/authz rows are not optional — the `not_found`/`forbidden` branches are where security bugs hide (`.claude/workflows/testing.md`).
