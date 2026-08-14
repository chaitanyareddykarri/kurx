# Workflow: Loop Engineering OS (Master Lifecycle)

The outer lifecycle every feature travels end to end. It **contains** the inner dev loop
(`.claude/workflows/loop-engineering.md` — PLAN→IMPLEMENT→BUILD→TEST→FIX→VERIFY→DOCUMENT→STOP),
which runs entirely inside the **Implementation** stage below. Read the inner loop once; read this
when scoping or driving a whole feature.

```
Discovery → Planning → Architecture → Task Breakdown → Implementation → Verification
→ Testing → Code Review → Security Review → Performance Review → Documentation
→ Decision Logging → Git Commit → Pull Request → Release
```

## How to read this file

- Stages are **gates**, not a rigid queue. A small bugfix collapses Discovery/Planning/Architecture into one sentence and skips Security/Performance review; a payments feature runs every stage in full.
- **Skipping a stage is a decision, not an omission** — state which stages you skipped and why in the STOP report. The default for anything touching auth/payments/PII/KYC is *skip nothing*.
- Each stage's **Exit conditions** are the entry conditions of the next. You do not advance on "should be fine."
- "Required files" lists what the stage reads or writes. `docs/DECISIONS.md` is always readable/writable; it is the spec (CLAUDE.md non-negotiable #1).

---

## 1. Discovery

**Purpose** — Understand the real request and what already exists before designing anything. This repo has a documented history of parallel scaffolding (D-018); Discovery is where you prevent repeating it.

**Inputs** — Raw request (feature/bug/chore), `docs/PROJECT_HANDBOOK.md`, `docs/roadmap/README.md`, memory `[[kurx-project-state]]`, relevant `.claude/phases/phaseN.md`.

**Outputs** — A restated problem statement; a list of existing entities/services/endpoints that already touch this area (from grep, not assumption); a list of open product questions.

**Entry conditions** — A request exists.

**Exit conditions** — You can name (a) what the user actually wants, (b) what code already exists for it, (c) which phase it belongs to, (d) every unresolved ambiguity.

**Required files** — `docs/roadmap/README.md`, `docs/PROJECT_HANDBOOK.md`, the codebase (grep).

**Validation checklist**
- [ ] Grepped for existing entities/services/endpoints in the affected domain (D-018 guard)
- [ ] Confirmed which phase (`.claude/phases/`) this work lands in
- [ ] Every ambiguity written down (not silently assumed)
- [ ] Confirmed the change is in scope for the current roadmap position, or flagged that it isn't

---

## 2. Planning

**Purpose** — Decide the smallest slice that delivers the request and the order of work across domains.

**Inputs** — Discovery output, `.claude/memory/coding-standards.md`, the owning domain agents' scope files.

**Outputs** — A vertical-slice plan (schema → service → endpoint → UI, typically), the domain agents involved and their handoff order, an explicit non-goals list, an acceptance check (the live exercise that will prove it works).

**Entry conditions** — Discovery complete; open product questions either resolved or queued for Architecture.

**Exit conditions** — A plan exists that a reviewer could disagree with; scope is bounded; the acceptance check is concrete (a curl/browser/flow, not "tests pass").

**Required files** — `.claude/templates/feature.md` (or `epic.md`/`user-story.md` for larger work), `.claude/phases/phaseN.md`.

**Validation checklist**
- [ ] Plan is a vertical slice, not horizontal half-layers
- [ ] Non-goals listed explicitly (guards against scope creep — CLAUDE.md non-negotiable #5)
- [ ] Domain agents and handoff order identified
- [ ] Acceptance check names a real live exercise
- [ ] For multi-slice work: `.claude/templates/epic.md` filled

---

## 3. Architecture

**Purpose** — Resolve design ambiguity and record it, so implementation never encodes an unrecorded assumption.

**Inputs** — Planning output, `.claude/memory/architecture.md`, `.claude/memory/event-driven-design.md`, `docs/architecture/overview.md`, `docs/DECISIONS.md`.

**Outputs** — For non-trivial design: a filled `.claude/templates/technical-design.md`. For every ambiguous product/architecture call: a drafted `D-NNN` (via `.claude/templates/decision.md`) ready to append at the Decision Logging stage — or appended now if implementation depends on it.

**Entry conditions** — Plan approved; a design question exists that has more than one defensible answer.

**Exit conditions** — Every fork in the design has a chosen branch with a reason. No "we'll decide when we get there" left in the plan. Layering respected (`Api → Infrastructure → Application interfaces`, everything on `Domain`).

**Required files** — `.claude/agents/architect.md`, `.claude/templates/technical-design.md`, `.claude/templates/decision.md`, `.claude/reviews/architecture-review.md`.

**Validation checklist**
- [ ] Every design fork has a decision + rationale
- [ ] Product-ambiguous calls escalated to the user (not guessed — architect boundary)
- [ ] Layering + provider-boundary rules (`.claude/memory/architecture.md`) preserved
- [ ] `D-NNN` drafted for each ambiguous call
- [ ] `.claude/reviews/architecture-review.md` passes for structural changes

---

## 4. Task Breakdown

**Purpose** — Turn the plan into ordered, individually-verifiable units of work.

**Inputs** — Plan + technical design.

**Outputs** — An ordered task list (each task has its own done-check), dependency edges between tasks, the agent that owns each task. For parallel work, the worktree assignment (`.claude/COLLABORATION.md`).

**Entry conditions** — Architecture resolved.

**Exit conditions** — Each task is small enough to implement-and-verify in one inner-loop pass; nothing is blocked by an unmade decision.

**Required files** — `.claude/COLLABORATION.md` (for parallel/worktree work).

**Validation checklist**
- [ ] Each task has an independent verification
- [ ] Task order respects layer dependencies (schema before service before endpoint before UI)
- [ ] Cross-domain handoffs are explicit
- [ ] Parallelizable tasks assigned to separate worktrees where useful

---

## 5. Implementation

**Purpose** — Build the slice. This is where the **inner loop** runs — once per task from Task Breakdown.

**Inputs** — Task list, `.claude/memory/coding-standards.md`, `.claude/memory/backend-conventions.md` / `frontend-conventions.md` / `database-conventions.md` / `api-conventions.md` / `error-handling.md` as applicable, the owning domain agent.

**Outputs** — Working code that compiles and passes tests for each task; new EF migration(s) if schema changed; a green `dotnet build` / `next build`.

**Entry conditions** — Task breakdown complete; decisions recorded.

**Exit conditions** — Inner loop reaches a verified-green STOP for every task. No muted assertions, no `--no-verify`, no half-working state left behind.

**Required files** — the relevant `.claude/workflows/{backend,frontend,api,database-migration}-development.md`, `.claude/workflows/loop-engineering.md`.

**Validation checklist**
- [ ] Smallest change that satisfies each task (no speculative extras — coding-standards)
- [ ] Existing scaffolding finished in place, not duplicated (D-018)
- [ ] Inner loop closed per task (build + test green)
- [ ] New domain code lives in its own folder-per-domain (coding-standards)
- [ ] Errors thrown as domain exceptions → `GlobalExceptionHandler` (`.claude/memory/error-handling.md`)

---

## 6. Verification

**Purpose** — Prove the change works against **real dependencies**, not just that it compiles. CLAUDE.md's core rule: a task is not done until exercised.

**Inputs** — Built code, `.claude/checklists/` for each touched surface, the `verify` skill.

**Outputs** — Evidence of a live exercise: curl transcript, browser confirmation, or a docker-compose end-to-end run against real `kurx_test` Postgres.

**Entry conditions** — Implementation green.

**Exit conditions** — The acceptance check from Planning has actually been run and passed live. "Tests pass" alone never satisfies this stage for anything with a runtime surface.

**Required files** — `.claude/checklists/{backend,frontend,database,qa}.md`, `verify` skill.

**Validation checklist**
- [ ] Matching `.claude/checklists/` run for every touched surface
- [ ] Flow exercised live (curl / browser / compose) — evidence captured
- [ ] `/health` reflects real dependency state if infra touched
- [ ] No capability faked that the dev environment can't actually provide (no real payment provider yet, etc.)

---

## 7. Testing

**Purpose** — Lock the behavior in with automated coverage so it can't silently regress.

**Inputs** — `.claude/memory/testing-standards.md`, `backend/Kurx.Tests`, `.claude/templates/test-plan.md`.

**Outputs** — New/updated integration tests against real `kurx_test` (never mocked persistence); test count before → after; named new coverage.

**Entry conditions** — Verification passed (you tested the right behavior manually first).

**Exit conditions** — `dotnet test` green; every new code path covered; no assertion weakened to force green; shared-DB / no-parallel constraint respected (`AssemblyInfo.cs`).

**Required files** — `.claude/checklists/qa.md`, `.claude/reviews/testing-review.md`, `.claude/agents/qa-engineer.md`.

**Validation checklist**
- [ ] Integration test added in the right per-domain class against real `kurx_test`
- [ ] Test count reported before → after, new coverage named
- [ ] No mocked persistence, no reduced assertions
- [ ] Cross-class parallelization left disabled
- [ ] `.claude/reviews/testing-review.md` passes

---

## 8. Code Review

**Purpose** — Catch correctness, reuse, and simplification issues a second pair of eyes (human or `/code-review`) would flag.

**Inputs** — The diff, `.claude/reviews/code-quality-review.md` + the domain review (`backend-review`/`frontend-review`/`database-review`/`api-review`).

**Outputs** — Reviewed diff; findings either fixed or explicitly deferred with a reason.

**Entry conditions** — Tests green.

**Exit conditions** — No open correctness findings; simplifications applied or consciously declined; diff matches surrounding code's idiom.

**Required files** — `.claude/reviews/code-quality-review.md`, the matching domain `.claude/reviews/*-review.md`.

**Validation checklist**
- [ ] `.claude/reviews/code-quality-review.md` passes
- [ ] Domain review checklist passes for each surface
- [ ] No drive-by refactors bundled in (CLAUDE.md #5)
- [ ] Diff reads like the code around it (naming, comment density, idiom)

---

## 9. Security Review

**Purpose** — Mandatory gate for auth/payments/PII/KYC/bank changes; a fast scan otherwise.

**Inputs** — `.claude/memory/security-rules.md`, `.claude/reviews/security-review.md`, `docs/security/`.

**Outputs** — For each finding: a concrete exploit scenario (attacker input/state → unauthorized action/data exposure). Or an explicit statement of which invariants were checked and held.

**Entry conditions** — Code review passed.

**Exit conditions** — For sensitive surfaces: `security-review` command run, no unresolved exploitable finding, JWT reuse-revokes-all + live resource-role checks + SignalR membership re-check invariants intact. For non-sensitive surfaces: OWASP quick-scan clean.

**Required files** — `.claude/commands/security-review.md`, `.claude/reviews/security-review.md`, `.claude/agents/security-engineer.md`, `.claude/checklists/security.md`.

**Validation checklist**
- [ ] Ran `security-review` if auth/payment/PII/KYC touched (non-negotiable)
- [ ] No secret committed, logged, or newly required without secret-management guidance
- [ ] Resource-role checks live per request, never trusted from a token claim
- [ ] Errors leak nothing (RFC7807 `ProblemDetails`, no stack traces to clients)

---

## 10. Performance Review

**Purpose** — Confirm the change doesn't introduce N+1s, unbounded queries, missing indexes, or blocking work on hot paths.

**Inputs** — `.claude/memory/performance-rules.md`, `.claude/reviews/performance-review.md`.

**Outputs** — Query counts / pagination confirmation for new list endpoints; index coverage for new filter columns; a note that no discovery/hot endpoint regressed.

**Entry conditions** — Security review passed.

**Exit conditions** — No unpaginated list endpoint, no obvious N+1, new filter/sort columns are index-backed, no synchronous external call added to a request hot path.

**Required files** — `.claude/reviews/performance-review.md`, `.claude/checklists/performance.md`, `.claude/agents/performance-engineer.md`.

**Validation checklist**
- [ ] New list endpoints paginated (public discovery surfaces especially)
- [ ] No N+1 (watch EF projection/OrderBy translation limits — `database-conventions`)
- [ ] Filter/sort columns index-backed via migration
- [ ] No blocking external/IO call added to a request path

---

## 11. Documentation

**Purpose** — Update the doc that would otherwise go stale. Docs are the second source of truth after code.

**Inputs** — The change, `.claude/workflows/documentation.md`, `.claude/workflows/continuous-learning.md` (the update-trigger matrix).

**Outputs** — Updated `docs/api/README.md` (public contract change), `docs/architecture/overview.md` (structural change), phase file (deliverable closed), memory files (convention changed), `README.md` (setup change).

**Entry conditions** — Behavior finalized (docs describe shipped reality, not intent).

**Exit conditions** — Every doc named by the continuous-learning trigger matrix for this change type is updated; no doc now contradicts the code.

**Required files** — `.claude/workflows/documentation.md`, `.claude/workflows/continuous-learning.md`, `.claude/reviews/documentation-review.md`.

**Validation checklist**
- [ ] Ran the continuous-learning trigger matrix (`.claude/workflows/continuous-learning.md`)
- [ ] Public contract change → `docs/api/README.md`
- [ ] Structural change → `docs/architecture/overview.md`
- [ ] Memory file updated if a convention changed
- [ ] `.claude/reviews/documentation-review.md` passes

---

## 12. Decision Logging

**Purpose** — Persist every ambiguous call as a `D-NNN` in `docs/DECISIONS.md` — the spec. This is where drafts from Architecture land.

**Inputs** — `D-NNN` drafts from Architecture/Implementation, `.claude/templates/decision.md`.

**Outputs** — Appended `D-NNN` entries in `docs/DECISIONS.md`; `.claude/memory/decision-log.md` known-decisions list updated if load-bearing.

**Entry conditions** — Documentation stage reached; all calls made during the loop are known.

**Exit conditions** — No assumption encoded in the code lacks a `D-NNN`. `docs/DECISIONS.md` numbering is contiguous and each entry states context → decision → why.

**Required files** — `docs/DECISIONS.md`, `.claude/templates/decision.md`, `.claude/memory/decision-log.md`.

**Validation checklist**
- [ ] Every ambiguous call from this feature has a `D-NNN`
- [ ] Each entry: what was ambiguous → what we chose → why (simplest satisfying option)
- [ ] `.claude/memory/decision-log.md` updated if the decision is load-bearing
- [ ] No decision left only in a template/memory/commit (DECISIONS.md is authoritative)

---

## 13. Git Commit

**Purpose** — Record the change atomically with a message that explains *why*.

**Inputs** — Verified, reviewed, documented working tree.

**Outputs** — One or more focused commits on a feature branch (never directly on `main`).

**Entry conditions** — All prior gates green. **Only commit when the user has asked** (CLAUDE.md / global git rule) — do not self-commit unprompted.

**Exit conditions** — Commit(s) scoped to the change; message states the root cause / rationale, not just the diff; on a feature branch; co-author trailer present; no `--no-verify`, no secret staged.

**Required files** — none new; `.gitignore`, `.env.example` must not have leaked secrets.

**Validation checklist**
- [ ] On a feature branch, not `main`
- [ ] Message explains why (root cause for fixes)
- [ ] No secret / `.env` / generated artifact staged
- [ ] No `--no-verify`, no force-push
- [ ] `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` trailer

---

## 14. Pull Request

**Purpose** — Package the change for review and CI.

**Inputs** — Feature branch, `.claude/templates/pull-request.md`.

**Outputs** — A PR whose body follows the template (summary, decisions, test evidence, verification evidence, reviews run); green CI.

**Entry conditions** — Commits pushed; user asked for a PR.

**Exit conditions** — PR body complete, CI green (`.github/workflows/ci.yml`), required reviews for the surface linked, `docs/DECISIONS.md` references included.

**Required files** — `.claude/templates/pull-request.md`, `.github/workflows/ci.yml`.

**Validation checklist**
- [ ] PR body follows `.claude/templates/pull-request.md`
- [ ] CI green (backend build+test, web typecheck/lint/build)
- [ ] Verification + test evidence included (counts, live-exercise proof)
- [ ] `D-NNN` references and required-review results linked
- [ ] PR trailer: 🤖 Generated with [Claude Code]

---

## 15. Release

**Purpose** — Ship a phase/deliverable and mark it done everywhere.

**Inputs** — Merged PR(s), `.claude/workflows/release.md`, `.claude/checklists/release.md`, the phase file.

**Outputs** — Updated `docs/roadmap/README.md` status; closed phase deliverable in `.claude/phases/phaseN.md`; release notes (`.claude/templates/release.md`); a full docker-compose smoke test result.

**Entry conditions** — PR merged; phase completion criteria believed met.

**Exit conditions** — `.claude/checklists/release.md` fully green; roadmap + phase file reflect reality; `docker compose up --build` boots all services healthy; core flow smoke-tested live.

**Required files** — `.claude/workflows/release.md`, `.claude/checklists/release.md`, `.claude/phases/phaseN.md`, `.claude/templates/release.md`, `docs/roadmap/README.md`.

**Validation checklist**
- [ ] `.claude/checklists/release.md` fully checked
- [ ] `docs/roadmap/README.md` + phase file updated to real status
- [ ] `docker compose up --build` → all services healthy, `/health` real
- [ ] Release notes written from the template
- [ ] No unreviewed prod secret/infra change bundled

---

## Stage → owner → gate quick map

| Stage | Owning agent | Hard gate |
|---|---|---|
| Discovery | planner | scope + existing-code known |
| Planning | planner | bounded vertical slice |
| Architecture | architect | every fork decided, `D-NNN` drafted |
| Task Breakdown | planner | each task independently verifiable |
| Implementation | backend/frontend/database | inner loop green per task |
| Verification | qa-engineer | live exercise passed |
| Testing | qa-engineer | `dotnet test` green, coverage named |
| Code Review | (any) + `/code-review` | no open correctness finding |
| Security Review | security-engineer | mandatory for auth/pay/PII/KYC |
| Performance Review | performance-engineer | no N+1 / unpaginated / blocking hot-path |
| Documentation | documentation-engineer | trigger matrix run |
| Decision Logging | architect | no unlogged assumption |
| Git Commit | release-manager | branch + why-message, user-asked |
| Pull Request | release-manager | template + green CI |
| Release | release-manager | release checklist green |

## Failure handling (applies to every stage)

A failed gate returns to the **earliest** stage that can fix the cause, not to a workaround. Compile/test failures root-cause back to Implementation. A discovered ambiguity returns to Architecture. A discovered scope gap returns to Discovery. The lifecycle never terminates at "probably fine" — it ends at a verified Release or an explicit blocker reported to the user (inherited from `.claude/workflows/loop-engineering.md`).
