# Template: User Story

One vertical slice of value — the unit a single feature-development loop delivers. Fill during Task Breakdown (`.claude/workflows/loop-engineering-os.md` §4).

```markdown
# Story: <short title>

**Epic**: <.claude/templates/epic.md link, if part of one> · **Phase**: <phaseN>

## As a / I want / So that
As a <organizer / attendee / platform admin / finance role>,
I want <capability>,
so that <outcome>.

## Surfaces
schema / service / endpoint / UI (strike unused) — in dependency order.

## Acceptance criteria (testable)
- [ ] <observable behavior 1 — phrased so it can be a test assertion>
- [ ] <authz boundary: who can / cannot do this>
- [ ] <error case: what happens on invalid input / missing resource>

## Live acceptance check
<the curl/browser exercise that proves this slice end-to-end>

## Authorization
- Roles allowed: <Owner/Manager/Staff/Finance/admin> (per D-015)
- Hidden-resource behavior: 404 not 403 where applicable (D-018)

## Out of scope
<adjacent work this story does NOT include>
```

Rule: acceptance criteria must be observable and testable; "works well" is not an acceptance criterion. Each ties to an integration test (`.claude/workflows/testing.md`).
