# Kurx Loop Engineering OS

This `.claude/` directory is a **Loop Engineering Operating System**: a self-consistent set of rules, roles, processes, and checklists that let humans and AI tools (Claude Code, Codex, Copilot) develop Kurx iteratively, autonomously, and safely — always closing the loop from idea to verified release.

> **New here?** Read `CLAUDE.md` (the operating contract) → this file (how it fits together) → `index.md` (the router to everything else). Then load only the leaf file your current task needs.

## The core idea

Every piece of work travels one **outer lifecycle** of 15 gated stages, and each build task inside it runs a small **inner loop**. Nothing is "done" until it's been exercised live and documented.

```
Outer lifecycle  (.claude/workflows/loop-engineering-os.md)
 Discovery → Planning → Architecture → Task Breakdown
   → Implementation ── contains ──▶ Inner loop (loop-engineering.md)
   → Verification → Testing → Code Review → Security Review
   → Performance Review → Documentation → Decision Logging
   → Git Commit → Pull Request → Release

Inner loop  (.claude/workflows/loop-engineering.md)
 PLAN → IMPLEMENT → BUILD → TEST → FIX → VERIFY → DOCUMENT → STOP
```

Each stage is a **gate** with entry/exit conditions and a validation checklist. Skipping one is a decision you state, not an omission.

## The layers, and what each is for

| Layer | Directory | Purpose | Entry point |
|---|---|---|---|
| **Contract** | `CLAUDE.md` | Always-loaded rules that override defaults. | `CLAUDE.md` |
| **Router** | `index.md` | Find the right file for the job. | `index.md` |
| **Lifecycle** | `workflows/` | The processes work follows (outer OS + inner loop + per-surface + continuous-learning). | `workflows/loop-engineering-os.md` |
| **Roles** | `agents/` | Specialized engineer personas owning each stage/surface. | `agents/README.md` |
| **Memory** | `memory/` | Single-source-of-truth conventions, reused not re-derived. | via `index.md` |
| **Gates** | `checklists/` | One-page pass/fail run before "done" on a surface. | via `index.md` |
| **Reviews** | `reviews/` | Deliberate structured passes with measurable criteria. | via `index.md` |
| **Templates** | `templates/` | Fill-in scaffolds so requests don't restate structure. | via `index.md` |
| **Commands** | `commands/` | Task shortcuts that run a workflow. | via `index.md` |
| **Phases** | `phases/` | Roadmap milestones, each integrated with the lifecycle. | `phases/README.md` |
| **Collaboration** | `COLLABORATION.md` | Multi-dev, multi-tool, worktrees, Docker, scale. | `COLLABORATION.md` |

## The five principles that make it hold together

1. **`docs/DECISIONS.md` is the spec.** Every ambiguous call is a `D-NNN` before code depends on it. (There is no other spec document.)
2. **Read before you build.** Grep for existing code; finish scaffolding, don't duplicate it (the D-018 lesson, wired into every workflow and agent).
3. **Done means exercised, not compiled.** Live verification against real `kurx_test` Postgres / a real browser session is required — tests alone never suffice for a runtime surface.
4. **One source of truth per fact.** Conventions live in exactly one `memory/` file; docs describe shipped reality. This is what lets the system scale to thousands of files without drift.
5. **Every loop closes.** Plan → … → verify → document → (on the user's ask) commit/PR/release. No work ends at "probably fine."

## How autonomous iteration works

- **Continuous learning:** after each loop, `workflows/continuous-learning.md` is a decision matrix — "did architecture / DB / API / a business rule / a convention change?" — that fires the exact doc/memory/decision updates needed. The system teaches itself as it goes.
- **Self-correcting gates:** a failed gate hands back to the earliest stage that can fix the cause, never forward with a workaround.
- **Bounded working set:** phases + on-demand loading keep any single task's context small even as the repo grows.

## Where to start for a given task

- **Feature** → `commands/new-feature.md` → `workflows/feature-development.md` (inside the OS lifecycle).
- **Bug** → `commands/bugfix.md` → `workflows/bug-fix.md`; urgent → `workflows/hotfix.md`.
- **Schema** → `workflows/database-migration.md`. **Endpoint** → `workflows/api-development.md`. **UI** → `workflows/frontend-development.md`.
- **Review / ship** → `commands/review.md`, `commands/security-review.md`, `commands/release.md`.
- **Planning a milestone** → `phases/README.md` + `templates/epic.md`.

See `FUTURE-IMPROVEMENTS.md` for where this system should grow next.
