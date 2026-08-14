# Kurx — Operating Manual

The always-loaded contract for every Claude Code (and Codex / Copilot) session on this repo. These rules **override** default behavior. Everything else loads on demand — start at `.claude/index.md` for the map, `.claude/README.md` for how the whole system fits together. Keep this file tight; depth lives in the files it points to.

## 1. What Kurx is

India-focused event-ticketing platform. **Backend**: .NET 10, `backend/Kurx.{Api,Application,Infrastructure,Domain}` (Clean-ish layers), Postgres. **Web**: Next.js `web/` (public site + the signed-in app). Three surfaces that are never conflated (D-305): **Profile** owns the *Create Event* entry point; **User Workspace** (`/workspace`) is every user's event-activity hub — participated events plus approved created ones, and **never an organizer dashboard**; **Event Host Workspace** (`host/events/{id}`) manages one approved event. Creation runs a verification/eligibility gate — including the Public/Private choice — *before* the form opens. **Admin**: `admin/` (Next.js console — 24 console pages over 58 `/v1/admin` operations: verification, events + pending/review queues, users, orgs, staff/roles, audit, blacklist, risk, reports, analytics, competitions, certificates, speakers, sponsors, broadcast, security, health, event-taxonomy; plus login/reset/forbidden outside the console shell). **Mobile**: `mobile/` (Flutter — attendee app plus organizer screens: auth incl. trusted devices/passkeys/recovery, events, orders, chat with attachments and presence, certificates, gamification). Detail: `.claude/memory/architecture.md`, `docs/PROJECT_HANDBOOK.md`.

## 2. Architecture (the load-bearing rules)

- **Layering:** `Api → Infrastructure → Application (interfaces) ← Infrastructure`; everything depends on `Domain`; `Domain` is framework-free. `Api` never touches EF for business logic. (`.claude/memory/backend-conventions.md`)
- **Authorization:** static `KurxAdmin` claim policy + per-org/event resource roles (Owner/Manager/Staff/Finance, D-015) queried **live** per request, never trusted from a token claim.
- **Provider boundary:** `Kurx.Application.Abstractions` interfaces (`IEmailSender`, `IPaymentGateway`, `IStorage`, `IKycProvider`, …) select an implementation by env flag and **fail closed** on an unrecognised value. **Real adapters ship for** email (SES), push (Firebase), SMS (SNS), secrets (AWS Secrets Manager), signing-key protection (KMS), malware scanning (ClamAV) and presence (Redis). **Still dev-only:** payments (mock), KYC (mock), storage (localdisk), WhatsApp (console), document rasterizer (stub). Each remaining one is phased work needing a `D-NNN`. The boot log names every dev implementation still in play — read it rather than this line.
- **Errors:** one model — RFC7807 `ProblemDetails` everywhere, with `error` + `correlationId` extensions (`.claude/memory/error-handling.md`).

## 3. Non-negotiables

1. **`docs/DECISIONS.md` is the spec.** No other spec exists. Every ambiguous product/architecture call gets a new `D-NNN` entry there before you build on it.
2. **Read before you build.** Grep for existing entities/services/endpoints before adding a parallel implementation — this repo has a history of unfinished scaffolding (D-018). Finish what exists; don't duplicate it.
3. **Tests run against real Postgres** (a per-class `kurx_test_<guid>` clone), never mocked persistence. No `psql` binary — verify schema via EF/tests. Shared counters are mutated **in SQL**, never read-modify-write (D-240). (`.claude/memory/database-conventions.md`)
4. **Every change ships with the loop closed**: plan → implement → build → test → fix → verify → document. Don't call work done on "should work."
5. **Don't touch what isn't asked.** No drive-by refactors, no speculative abstractions, no unrequested dependency changes.
6. **Secrets and destructive ops require a stop.** No `--no-verify`, no force-push, no `.env`/secret edits, no destructive schema change without flagging to the user first.

## 4. Loop Engineering process

Two nested loops:

- **Outer lifecycle** — `.claude/workflows/loop-engineering-os.md`: Discovery → Planning → Architecture → Task Breakdown → Implementation → Verification → Testing → Code Review → Security Review → Performance Review → Documentation → Decision Logging → Git Commit → Pull Request → Release. Each stage is a gate with entry/exit conditions and a validation checklist. Skipping a stage is a *decision* you state, not an omission.
- **Inner loop** — `.claude/workflows/loop-engineering.md`: PLAN→IMPLEMENT→BUILD→TEST→FIX→VERIFY→DOCUMENT→STOP, run once per task inside the Implementation stage.

Small bugfix: collapse the early stages to a sentence, skip Perf/Security if truly untouched. Auth/payment/PII/KYC change: skip nothing. Neither loop ever ends at "probably fine" — only at a verified state or an explicit blocker reported to the user.

## 5. Coding standards

Three similar lines beat a premature abstraction. No speculative flags/future-proofing. Validate only at system boundaries. No comments that restate code — only ones explaining a non-obvious constraint. Match the surrounding file's idiom. Money is `long` paise / `bigint` `_paise` (D-004); ids are app-side `Guid`/`uuid` (D-006). Full: `.claude/memory/coding-standards.md` + the per-surface `*-conventions.md`.

## 6. Documentation rules

Docs describe **shipped** reality, not intent. One source of truth per fact — extend it, never copy it into a second file. After every change run the update-trigger matrix (`.claude/workflows/continuous-learning.md`): contract → `docs/api/README.md`; structure → `docs/architecture/overview.md`; ambiguous call → `D-NNN`; convention → the owning `.claude/memory/*.md`; deliverable → phase file **and** `docs/roadmap/README.md`.

## 7. Review process

- Day-to-day diff review: `.claude/commands/review.md` / `/code-review`.
- Deliberate structured passes: `.claude/reviews/` (architecture, backend, frontend, api, database, security, performance, accessibility, testing, documentation, code-quality) — each has measurable pass/fail criteria.
- **Security review is mandatory** for any auth/payment/PII/KYC/bank change before merge (`.claude/commands/security-review.md`). Urgency never waives it.

## 8. Git workflow

Branch off `main` (never commit on it directly). **Commit/push/PR only when the user asks** — never self-ship. Messages explain *why* (root cause for fixes). No secret/`.env`/generated artifact staged, no `--no-verify`, no force-push on shared history. Commit trailer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`; PR body from `.claude/templates/pull-request.md` ending with the Claude Code trailer.

## 9. Testing rules

Backend: integration tests only, against real Postgres via `WebApplicationFactory<Program>` — no mocked persistence, ever. Each test class gets its own `kurx_test_<guid>` cloned from a migrated template; cross-class parallelization stays disabled and the suite is order-sensitive, so **only a full-suite run is evidence**. On Windows the host runner is blocked by Application Control — run it in the SDK container, passing `-c` explicitly and identically on build and test. Cover the error/authz branches, not just the happy path. Never weaken an assertion or skip a test to force green. Report test count before → after (**baseline 1759: 1752 pass, 1 skip, 6 fail** — measured 2026-08-14 in the SDK container, 15m45s. The 6: 4 `ClamAvUploadPathTests` (needs `CLAMAV_HOST=clamav` **and** `docker compose --profile scanning up -d clamav` — the service does not start by default), 1 `NoContentDeclarationTests` (an artifact of `-p:ArtifactsPath`, which puts the binary outside the `/src` mount so source-reading tests cannot find the repo), and 1 **flaky** `NotificationDedupTests.Kinds_that_legitimately_repeat_are_not_constrained`, which derives a phone suffix from `Math.Abs(kind.GetHashCode()) % 100` — `string.GetHashCode()` is randomized per process, so ~6% of runs collide two of its four cases onto one phone and hit `IX_users_Phone`. Passes 4/4 in isolation; pre-existing, not anyone's change. The 3 `EventAudienceAuthorizationTests` that used to sit in this list are **fixed** (`LoginAsAsync` now seeds `AuthService.NormalizePhone(typed)`). Treat it as a floor; re-run a failing class in isolation, and never run two SDK containers against one Postgres). Every surface has a suite and CI runs all four jobs — backend, web (`npm test`, 514 pass/1 skip), admin (`npm test`, 32 tests) and mobile (`flutter analyze --no-fatal-infos` + `flutter test`, 417 tests, Flutter pinned 3.44.6). Two things still run nowhere: `node --test scripts/contract-check.test.mjs` (CI runs the contract *tool*, never its test), and mobile's `integration_test/` + `test_live/`, which sit outside `flutter test` and need real hardware or a live backend. (`.claude/memory/testing-standards.md`)

## 10. Security rules

OWASP top-10 on every change. Preserve the invariants: JWT reuse-revokes-all (D-009/D-014), live resource-role checks (D-015), SignalR group-join membership re-check (D-017), hidden resource → 404-not-403 (D-018). No secret/PII in code, logs, or config; production secret validation fails closed — don't bypass it. Errors leak no internals. **One switch deliberately opens a gate: `IDENTITY_VERIFICATION_BYPASS` (D-323)** skips the govt-ID/PAN/bank proofs behind public-event creation, paid organizing and payouts, because all three are mock-backed. It is dev/test only and throws at startup in Production; `fraudClear` stays enforced and the reported verification facts are never forged. Don't widen it, and delete it when a real KYC adapter ships. (`.claude/memory/security-rules.md`, `.claude/memory/trust-verification.md`)

## 11. Definition of Done

A change is done when **all** hold:
- [ ] Builds clean; relevant `dotnet test` / `next build` green.
- [ ] Exercised **live** against real dependencies (curl/browser/compose) — not just typechecked.
- [ ] Matching `.claude/checklists/` passed for every touched surface.
- [ ] Required `.claude/reviews/` passed (security-review if sensitive).
- [ ] Every ambiguous call recorded as a `D-NNN`.
- [ ] Docs updated per the trigger matrix; no doc contradicts the code.
- [ ] No unasked scope, no muted test, no staged secret.

## 12. How AI agents collaborate

Adopt the persona owning your stage/surface (`.claude/agents/README.md` maps stage → agent). Default handoff chain: `planner → architect → backend/database/frontend engineers → qa-engineer → security-engineer → performance-engineer → documentation-engineer → release-manager (⇄ devops)`. A failed gate hands **back** to the earliest role that can fix the cause. One session may wear several hats in sequence; parallel work across worktrees/tools is coordinated by `.claude/COLLABORATION.md`.

## 13. How memory is updated

`.claude/memory/*.md` are the single source of truth for conventions — reused instead of re-derived, never duplicated. When work changes a convention/architecture/API/business-rule, update the one owning file (and `docs/DECISIONS.md` for the decision). The full "what changed → what to update" matrix is `.claude/workflows/continuous-learning.md`; run it before you STOP.

## 14. How to work here

- Start: `.claude/index.md` → pick the matching agent, command, or workflow.
- New feature: `.claude/commands/new-feature.md` + `.claude/workflows/feature-development.md` (inside the OS lifecycle).
- Bug: `.claude/commands/bugfix.md` + `.claude/workflows/bug-fix.md`; urgent: `.claude/workflows/hotfix.md`.
- Anything else: check `.claude/commands/` first; only improvise if nothing fits.
- Precedence when files conflict: `CLAUDE.md` > `memory/` > `agents/` > `workflows/`/`commands/` > `checklists/`/`reviews/`/`templates/`. The higher wins; fix the lower.
- **Terminology precedence (D-271):** `docs/architecture/TERMINOLOGY.md` is the canonical vocabulary. If a word in code, a comment, a diagram or a doc contradicts it, that word is drift — fix it, don't propagate it. The load-bearing ones: **a User owns an Event** (an organization never does); an organization on an event is the one it **represents**; **`IEventAuthority`** decides *who may act* and the **Capability Engine** decides *what an event supports*, and the two never consult each other. There are no organization accounts, organizer accounts, or personal organizations.
