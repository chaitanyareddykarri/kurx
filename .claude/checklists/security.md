# Checklist: Security

- [ ] No secrets in code, logs, or config committed
- [ ] Error responses leak no internals (RFC7807 only)
- [ ] Auth invariant preserved: JWT refresh reuse-revokes-all
- [ ] Resource-scoped role checks queried live, not trusted from token claims
- [ ] SignalR hub group joins re-verify org/event membership
- [ ] OWASP top 10 considered for the specific change surface
- [ ] Any auth/payment/PII/KYC touch ran the full `security-review` command
- [ ] No `--no-verify`, force-push, or `.env` edit without explicit user confirmation
