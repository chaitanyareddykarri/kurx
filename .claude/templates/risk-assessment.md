# Template: Risk Assessment

For changes to auth, payments, PII/KYC, money movement, destructive schema, or anything hard to reverse. Fill during Architecture; attach to the TDD/PR.

```markdown
# Risk Assessment: <change>

## Classification
- Sensitive surface: auth / payment / PII / KYC / money / schema-destructive / infra (mark all)
- Reversibility: easy / hard / irreversible
- Blast radius: single endpoint / one domain / cross-cutting / data integrity

## Risks
| # | Risk | Likelihood | Impact | Mitigation | Residual |
|---|---|---|---|---|---|
| 1 | <e.g. refresh-token reuse not revoking> | | | preserve reuse-revokes-all (D-014) | |
| 2 | <e.g. bank PII persisted> | | | store only last4 (D-016) | |
| 3 | <e.g. draft leaks via 403> | | | 404 not 403 (D-018) | |

## Invariants that must hold after this change
- [ ] JWT reuse-revokes-all preserved (D-009/D-014)
- [ ] Resource-role checks live, not cached (D-015)
- [ ] SignalR group-join membership re-checked (D-017)
- [ ] No secret/PII persisted or logged beyond what's required
- [ ] Errors leak no internals (RFC7807)

## Required gates
- [ ] `security-review` command (mandatory for sensitive surfaces)
- [ ] Destructive schema change confirmed with user
- [ ] Rollback plan stated below

## Rollback plan
<how to revert safely if this goes wrong in verification/production>

## Stop-and-confirm items
<any .env/secret/force-push/destructive op that needs explicit user sign-off before proceeding>
```

Rule: a "hard" or "irreversible" reversibility rating means a user stop is required before proceeding (CLAUDE.md non-negotiable #6).
