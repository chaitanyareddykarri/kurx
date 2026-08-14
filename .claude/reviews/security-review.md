# Review Template: Security

Use for auth/payment/PII/KYC changes, or on request. Mandatory gate per `.claude/memory/security-rules.md`.

## Check against

- `.claude/checklists/security.md`
- OWASP top 10 for the specific surface touched
- JWT reuse-revokes-all invariant preserved
- Resource-role checks queried live, never trusted from a cached claim
- SignalR group-join membership re-verification intact
- No secret committed, logged, or newly required without `docs/security/secret-management.md` guidance followed

## Output

Each finding: concrete exploit scenario (attacker input/state → unauthorized action/data exposure), not a theoretical concern.

## Stop condition

Findings reported, or explicit statement of which invariants were checked and held if none found.
