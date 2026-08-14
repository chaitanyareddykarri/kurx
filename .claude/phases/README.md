# Phases — Roadmap Milestones × Loop Engineering

> ℹ️ **The phase-numbered roadmap (Phases 1–10) is superseded by the Production
> Re-Architecture (modules M0–M13 / D-039–D-052).** This directory is retained as
> live process scaffolding — the workflows/templates/agents write "the current
> phase file" here — but it is **no longer the build-status tracker.** For what is
> actually built, use [`docs/roadmap/README.md`](../../docs/roadmap/README.md) and
> [`CHANGELOG.md`](../../CHANGELOG.md); for decisions, [`docs/DECISIONS.md`](../../docs/DECISIONS.md).

Roadmap-level milestones. Each phase is a container of features, and **every feature inside a phase runs the full 15-stage lifecycle** (`.claude/workflows/loop-engineering-os.md`). A phase is "done" only when all its features have individually closed that loop *and* the phase-level acceptance/exit criteria below are met.

Cross-check status against `docs/roadmap/README.md` and the project-state memory before assuming a phase's state — the roadmap is authoritative for what's built.

## Standard phase schema

Every `phaseN.md` defines:

| Field | Meaning |
|---|---|
| **Goals / Objective** | The capability the phase delivers. |
| **Dependencies** | Phases/decisions it builds on. |
| **Deliverables** | Concrete artifacts (models, endpoints, UI, jobs). |
| **Acceptance criteria** | Testable conditions that prove the phase works end-to-end (live, not just tests). |
| **Exit criteria** | Everything true before the phase is marked complete (deliverables shipped + verified, coverage grew, roadmap + phase file updated, required reviews passed). |
| **Required reviews** | Which `.claude/reviews/` passes are mandatory for this phase's surfaces. |
| **Required documentation** | Which docs must be updated when the phase closes. |

Completed phases (1–2) keep this as a historical record; forward phases (3–10) use it as the entry contract.

## Status at a glance

**Superseded — do not read build status from this file.** The phase-numbered
milestones below were replaced by the M0–M13 module re-architecture. The
authoritative, current build status lives in
[`docs/roadmap/README.md`](../../docs/roadmap/README.md) (built vs. not) and
[`CHANGELOG.md`](../../CHANGELOG.md) (module-by-module history). Phases 1–3, B, C,
A2 shipped as documented (D-017/D-018/D-020–D-029); everything the old Phases 4–10
described — payments, ledger, admin, fraud, real providers, mobile — was
re-scoped and delivered (or explicitly deferred) under D-039–D-052, so the old
per-phase status labels no longer map cleanly onto the code.

## Required-reviews map (by surface)

| Surface a phase touches | Mandatory review(s) |
|---|---|
| Auth / payment / PII / KYC / money | **security-review** (non-negotiable) |
| New/changed HTTP contract | api-review |
| Schema/migrations | database-review |
| List endpoints / hot paths / financial jobs | performance-review |
| Any `web/`/`admin/` UI | frontend-review + accessibility-review |
| Structural change | architecture-review |
| Every phase close | testing-review + documentation-review + release checklist |

Do not close a phase until each review its surfaces trigger has passed (`.claude/workflows/release.md`).
