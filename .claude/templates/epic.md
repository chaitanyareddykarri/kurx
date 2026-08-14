# Template: Epic

For multi-slice work spanning several tasks/PRs (typically a phase or a large deliverable). Fill during Planning (`.claude/workflows/loop-engineering-os.md` §2). Break down into `.claude/templates/user-story.md` items.

```markdown
# Epic: <name>

**Phase**: <.claude/phases/phaseN.md> · **Owner agent**: <planner/architect>
**Status**: proposed / in-progress / done

## Goal
<the capability delivered when this epic is complete, in one paragraph>

## Why now
<roadmap position / dependency that makes this the right next work>

## In scope
- <slice 1>
- <slice 2>

## Out of scope (explicit non-goals)
- <what a reader might assume is included but isn't>

## Dependencies
- Phases/decisions this builds on: <phaseN, D-NNN>
- Blocked by: <unbuilt provider / unmade decision>

## Stories
- [ ] <user-story 1> (→ .claude/templates/user-story.md)
- [ ] <user-story 2>

## Decisions to make (Architecture stage)
- <open question> → resolve as D-NNN

## Acceptance criteria (epic-level)
- <the end-to-end live exercise that proves the whole epic works>

## Required reviews before "done"
- Security (if auth/payment/PII/KYC anywhere) · Performance (new hot paths) · Architecture (structural)
```

Rule: an epic isn't done until every story is done, verified live, and the phase file + roadmap reflect it.
