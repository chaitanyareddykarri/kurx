# Template: Security Review Report

The **output** scaffold for a security review pass. The pass itself follows `.claude/reviews/security-review.md` (what to check) and `.claude/checklists/security.md`. Mandatory for auth/payment/PII/KYC changes (`.claude/memory/security-rules.md`).

```markdown
# Security Review: <change / branch / PR>

**Reviewer agent**: security-engineer · **Surface**: auth / payment / PII / KYC / other
**Trigger**: mandatory (sensitive surface) / requested

## Scope reviewed
<files, endpoints, hubs, migrations examined>

## Invariants checked
| Invariant | Holds? | Note |
|---|---|---|
| JWT reuse-revokes-all (D-009/D-014) | | |
| Resource-role checks live per request (D-015) | | |
| SignalR group-join re-verifies membership (D-017) | | |
| Hidden resource → 404 not 403 (D-018) | | |
| No secret/PII committed or logged | | |
| Errors → RFC7807, no internal leakage | | |
| Production secret validation not bypassed | | |

## OWASP pass (for the touched surface)
<injection / broken access control / auth / sensitive-data / misconfig / XSS / components / logging — note each relevant one>

## Findings
> Each finding = a CONCRETE exploit scenario (attacker input/state → unauthorized action/data exposure), file:line, severity. Not a theoretical concern.

1. **[severity]** <title> — `path:line`
   - Scenario: <attacker does X → gets Y>
   - Fix: <smallest correct remediation>

## Verdict
- [ ] No exploitable finding — invariants above checked and held
- [ ] Findings above must be fixed before merge

🤖 security-review per .claude/workflows/loop-engineering-os.md §9
```

Rule: "no findings" is only valid when accompanied by the explicit list of invariants checked (an empty report with no stated checks is not a passing review).
