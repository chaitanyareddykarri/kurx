# Template: Pull Request

Fill in for the **Pull Request** stage (`.claude/workflows/loop-engineering-os.md` §14). Paste as the PR body.

```markdown
## Summary
<one paragraph: what this changes and why. Root cause for a fix.>

## Scope
- Surfaces touched: backend / frontend / database / infra (strike unused)
- Phase: <phaseN> · Deliverable: <which>
- Non-goals: <what this deliberately does NOT do>

## Decisions
- D-NNN: <title> — <one line> (link docs/DECISIONS.md)
- <none if no ambiguous call was made>

## Test evidence
- `dotnet test`: <before> → <after> passing
- New coverage: <what code paths are now tested>

## Verification evidence (live, not just tests)
- <curl transcript / browser confirmation / `docker compose up --build` result>
- Acceptance check from Planning: <passed?>

## Reviews run
- [ ] Code review (`.claude/reviews/code-quality-review.md` + domain review)
- [ ] Security review (`security-review` command) — required if auth/payment/PII/KYC
- [ ] Performance review (if new list endpoint / hot path)

## Docs updated
- [ ] docs/api/README.md (contract change)
- [ ] docs/architecture/overview.md (structural change)
- [ ] .claude/phases/phaseN.md + docs/roadmap/README.md (deliverable closed)
- [ ] .claude/memory/*.md (convention change)

## Checklist
- [ ] On a feature branch, CI green
- [ ] No secret / .env / generated artifact staged
- [ ] No breaking /v1 change without a D-NNN

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

Rule: a PR that can't fill "Verification evidence" with something live isn't ready (CLAUDE.md verification requirement).
