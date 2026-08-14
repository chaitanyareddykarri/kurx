# Workspace Index

> **Architecture: event-first ([D-074](../docs/DECISIONS.md)).** No organizer/owner accounts — every person is a User who may *represent* verified organizations. Product/doc map: [`../docs/README.md`](../docs/README.md); trust model: [`memory/trust-verification.md`](memory/trust-verification.md).

Navigation entry point for `.claude/`. Read `CLAUDE.md` first (the operating contract), `README.md` for how the whole Loop Engineering OS fits together, then come here to find the right file for the job. Nothing below should be read speculatively — load only what the current task needs (this on-demand discipline is what keeps context cost flat as the repo grows — `COLLABORATION.md`).

## Start here

- **`CLAUDE.md`** — always-loaded rules that override defaults.
- **`README.md`** — the OS overview: the two loops, the layers, the five principles.
- **`workflows/loop-engineering-os.md`** — the 15-stage master lifecycle every feature travels.
- **`COLLABORATION.md`** — multi-dev, multi-tool, worktrees, Docker, scale.
- **`FUTURE-IMPROVEMENTS.md`** — where the system should grow next.

## Agents (`agents/`)

Specialized engineer personas; each owns a lifecycle stage/surface. Roster + handoff chain: **`agents/README.md`**.

| Agent | Owns |
|---|---|
| [planner](agents/planner.md) | Discovery, Planning, Task Breakdown |
| [architect](agents/architect.md) | Architecture, Decision Logging, `docs/DECISIONS.md` |
| [backend-engineer](agents/backend-engineer.md) | `backend/Kurx.*` (.NET) |
| [frontend-engineer](agents/frontend-engineer.md) | `web/`, `admin/` (Next.js) |
| [mobile-engineer](agents/mobile-engineer.md) | `mobile/` (Flutter) |
| [database-engineer](agents/database-engineer.md) | EF Core models, migrations, Postgres |
| [qa-engineer](agents/qa-engineer.md) | Verification, Testing, `backend/Kurx.Tests` |
| [security-engineer](agents/security-engineer.md) | Security Review |
| [performance-engineer](agents/performance-engineer.md) | Performance Review |
| [documentation-engineer](agents/documentation-engineer.md) | Documentation, Decision Logging |
| [release-manager](agents/release-manager.md) | Git Commit, Pull Request, Release |
| [devops](agents/devops.md) | CI, Docker, deployment, health/observability |

`backend`/`frontend`/`database`/`qa` (short names) are compatibility aliases → their `*-engineer` files.

## Commands (`commands/`)

Task shortcuts — goal/inputs/outputs/verification/stop condition. Use the matching one instead of improvising.

`new-feature` · `bugfix` · `review` · `refactor` · `test` · `performance` · `security-review` · `release`

## Workflows (`workflows/`)

Processes each command runs inside. Two nested loops plus per-surface specializations.

- **Master:** `loop-engineering-os` (15-stage outer lifecycle) · `loop-engineering` (inner PLAN→…→STOP loop).
- **Lifecycle specializations:** `feature-development` · `bug-fix` · `hotfix` · `review` · `release`.
- **Per-surface:** `backend-development` · `frontend-development` · `api-development` · `database-migration` · `testing` · `documentation`.
- **Cross-cutting:** `continuous-learning` (the memory/doc update-trigger matrix — run before every STOP).

## Memory (`memory/`)

Single-source-of-truth conventions, reused not re-derived. Don't restate their content elsewhere.

- **Architecture & code:** `architecture` · `coding-standards` · `backend-conventions` · `frontend-conventions` · `mobile-conventions` · `api-conventions` · `event-driven-design`.
- **Data:** `database-conventions` · `versioning`.
- **Cross-cutting concerns:** `error-handling` · `logging` · `observability` · `performance-rules` · `security-rules` · `deployment`.
- **Spec pointer:** `decision-log` (→ `docs/DECISIONS.md`, the real spec).

## Checklists (`checklists/`)

One-page pass/fail gates, run before "done" on a surface: `backend` · `frontend` · `mobile` · `database` · `qa` · `security` · `performance` · `release`.

## Reviews (`reviews/`)

Structured passes with measurable pass/fail criteria (distinct from the day-to-day `review` command): `architecture-review` · `backend-review` · `frontend-review` · `api-review` · `database-review` · `security-review` · `performance-review` · `accessibility-review` · `testing-review` · `documentation-review` · `code-quality-review`.

## Templates (`templates/`)

Fill-in scaffolds: `feature` · `epic` · `user-story` · `technical-design` · `test-plan` · `risk-assessment` · `bug` · `api` · `migration` · `pull-request` · `release` · `security-review` · `performance-review` · `documentation` · `decision` (mirrors `docs/DECISIONS.md` `D-NNN`).

## Phases (`phases/`)

Roadmap milestones, each integrated with the lifecycle (Goals/Deps/Deliverables/Acceptance/Exit/Required Reviews/Required Docs). Schema + status table: **`phases/README.md`**. `phase1`…`phase10`. Cross-check status against `docs/roadmap/README.md` first.

## Precedence

`CLAUDE.md` > `memory/` > `agents/` > `workflows/`/`commands/` > `checklists/`/`reviews/`/`templates/`. If two files conflict, the higher one wins and the lower one should be fixed.
