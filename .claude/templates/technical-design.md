# Template: Technical Design Document (TDD)

For non-trivial design work — fill during the Architecture stage (`.claude/workflows/loop-engineering-os.md` §3) before implementing. Small changes don't need one; anything cross-domain, schema-affecting, or introducing a new pattern does. Store alongside the epic or link from the PR.

```markdown
# TDD: <feature>

**Author agent**: architect · **Reviewers**: <backend/database/security/...>
**Related**: Epic <link> · Phase <phaseN> · Decisions <D-NNN...>

## Problem
<what we're solving and the constraints that bound it>

## Existing state (Discovery output)
<what code/entities/endpoints already exist here — grep results, not assumption. D-018 guard.>

## Proposed design
### Data model
<entities, columns (money → _paise, ids → uuid), indexes, migration name>
### Services & interfaces
<Kurx.Application interface(s), Kurx.Infrastructure impl, authz inputs (userId/isAdmin)>
### API surface
<endpoints, DTOs, status codes, /v1 versioning impact>
### State/workflow
<state machine in Domain if stateful (event-driven-design.md)>
### Frontend
<Server Components/Actions, session touchpoints, if UI>

## Layering & boundary check
- [ ] Api → Infrastructure → Application interfaces; Domain framework-free
- [ ] No real external provider added without a D-NNN
- [ ] Authz queried live, not from a claim

## Alternatives considered
<option → why rejected>

## Decisions required
<each open fork → the chosen branch → D-NNN to log>

## Risks
<link .claude/templates/risk-assessment.md if non-trivial>

## Test & verification plan
<link .claude/templates/test-plan.md; the live acceptance exercise>
```

Rule: every design fork in a TDD ends with a chosen branch and a `D-NNN`; "decide later" is not allowed to reach Implementation.
