# Agents — Roster & Collaboration

Specialized AI engineer personas. Adopt the one that owns the stage/surface you're in (`.claude/workflows/loop-engineering-os.md` maps stage → owning agent). A single Claude session may wear several hats in sequence; a multi-developer / multi-tool setup may assign them to different actors (`.claude/COLLABORATION.md`).

Each role file defines: **Responsibilities · Inputs · Outputs · Rules · Constraints · Deliverables · Handoff to next role**.

## Roster

| Role | Owns (lifecycle stage / surface) | File |
|---|---|---|
| Planner | Discovery, Planning, Task Breakdown | [planner](planner.md) |
| Architect | Architecture, Decision Logging | [architect](architect.md) |
| Backend Engineer | Implementation — `backend/Kurx.*` | [backend-engineer](backend-engineer.md) |
| Frontend Engineer | Implementation — `web/`, `admin/` | [frontend-engineer](frontend-engineer.md) |
| Mobile Engineer | Implementation — `mobile/` (Flutter) | [mobile-engineer](mobile-engineer.md) |
| Database Engineer | Implementation — EF/migrations/Postgres | [database-engineer](database-engineer.md) |
| QA Engineer | Verification, Testing | [qa-engineer](qa-engineer.md) |
| Security Engineer | Security Review | [security-engineer](security-engineer.md) |
| Performance Engineer | Performance Review | [performance-engineer](performance-engineer.md) |
| Documentation Engineer | Documentation, Decision Logging | [documentation-engineer](documentation-engineer.md) |
| Release Manager | Git Commit, Pull Request, Release | [release-manager](release-manager.md) |
| DevOps | CI, Docker, infra, health/observability | [devops](devops.md) |

## Handoff chain (default feature)

```
planner → architect → (backend-engineer ⇄ database-engineer) → (frontend-engineer | mobile-engineer)
        → qa-engineer → security-engineer → performance-engineer
        → documentation-engineer → release-manager (⇄ devops)
```

`mobile-engineer` sits at the same implementation stage as `frontend-engineer` — a UI client of the backend contract (`mobile/`, Flutter). It hands **back** to `backend-engineer` for any missing endpoint and **forward** to `qa-engineer` (device verification) and, for any auth/token/PII change, `security-engineer` (mandatory).

A failed gate hands **back** to the earliest role that can fix the cause, not forward with a workaround (`loop-engineering-os.md` failure handling).

## Rules every role shares

- Read before you build; finish existing scaffolding, don't duplicate it (D-018).
- Smallest change that satisfies the task; no drive-by refactors (CLAUDE.md #5).
- A task isn't done until exercised live, not just typechecked.
- Ambiguous product/architecture call → `D-NNN` in `docs/DECISIONS.md` before building on it.
- Secrets / destructive ops / commits require the stops in CLAUDE.md #6.
