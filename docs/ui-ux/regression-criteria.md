# Kurx UI/UX Redesign — Regression Criteria & Visual Baselines

Established in Phase 0.4 / 0.5. Defines what "no regression" means for the 57 phases that follow.

---

## 1. Visual baseline strategy (Phase 0.4)

**D-285 chose manual capture over a new test dependency.** `web/vitest.config.mts` records D-109's
deliberate exclusion of Playwright, Cypress and Storybook from this repo; adding a screenshot
harness to `package.json` would contradict an accepted decision for tooling convenience.

### What that means in practice

| Tier | Surface | Method | When |
|---|---|---|---|
| **T1 — unauthenticated** | `(public)/*` (9 routes), auth screens (`/register`, `/recover`, `/reset`), admin `/login`, `/forbidden` | Browser at the six Phase-25 widths; PNG into `docs/ui-ux/baselines/<surface>/<route>@<width>.png` | Before the first phase that touches the surface |
| **T2 — authenticated** | every web and admin route not covered by T1 | Same, against a locally seeded stack (Postgres + API + `next dev`) | Before Phases 11, 25 and 47 |
| **T3 — Flutter** | every page in `inventory-mobile.md` | Simulator screenshots for the 20 journey-critical pages only | Before Phases 33 and 40 |

*Tiers are defined by scope, not by a headcount: the counts that stood here (76 web, 91 Flutter pages)
were the 2026-08-08 baseline and both had drifted by 2026-08-15. The live totals are the
[inventories](inventory-web.md), which are re-derived from the filesystem.*

Session-level browser tooling (the Playwright/Chrome MCP servers available to the agent) may be used
to *take* these captures — it adds nothing to `package.json`, so D-109 is not violated. What is
forbidden is committing a screenshot harness into the repository's dependency graph.

### Baselines are not a substitute for tests

A screenshot proves what a page looked like, never that it worked. Functional criteria in §2 are
independent and always apply.

### Capture discipline

* Deterministic data only — a seeded fixture user, never live data.
* Both themes where the surface supports them.
* Filed in the Screenshot Tracking table of `UI_REDESIGN_PROGRESS.md` on capture, so an uncaptured
  screen is visible as a gap rather than silently skipped. *(That tracker is a historical record as of
  2026-08-08 — see the warning at its top. The discipline stands; a resumed program needs a live home
  for the table.)*

---

## 2. Regression criteria (Phase 0.5)

A phase has regressed if **any** of the following is true after its changes.

### 2.1 Build & static analysis — hard gates

| Suite | Criterion |
|---|---|
| web | `typecheck` exit 0 · `lint` exit 0 · `build` compiles with **≥ 65 static pages** |
| admin | `typecheck` exit 0 · `lint` exit 0 · `build` compiles with **≥ 29 static pages** |
| mobile | `flutter analyze --no-fatal-infos` → **No issues found** |
| backend | `dotnet build -c Release -warnaserror` → 0 warnings, 0 errors |

A drop in static-page count means a route stopped pre-rendering — a real regression, not noise.

### 2.2 Automated tests — hard gates

| Suite | Baseline @ `c66ae16` | Criterion |
|---|---|---|
> ⚠️ **The absolute numbers that stood in this table were the 2026-08-08 baseline and every one of them
> is now far below reality** (it read web 145, mobile 275, backend 1432, and "admin — no suite exists";
> admin has had a suite for weeks). A floor that low passes trivially, so it was not a gate at all.
> **The live baseline is `.claude/CLAUDE.md` §9 and `.claude/memory/testing-standards.md`**, which are
> updated per measured run — read the count there rather than restating it here, which is exactly how
> this table rotted.

| Suite | Gate |
|---|---|
| web `npm test` | **0 failing**, and no fewer passing than the current baseline in `.claude/CLAUDE.md` §9 |
| admin `npm test` | **0 failing**, same rule (this row said "no suite exists") |
| mobile `flutter analyze --no-fatal-infos` + `flutter test` | analyze clean of errors/warnings; **0 failing** |
| backend `dotnet test` | **0 failing**, ≤ 1 skipped — and only a **full-suite** run is evidence (the suite is order-sensitive) |

**Assertions are never weakened to reach these numbers.** Test counts may rise; they may not fall.
The Flutter finder-vs-assertion rule (change finders, never assertions) came from
`UI_REDESIGN_PROGRESS.md`, now a historical record — the rule itself still holds.

### 2.3 Functional criteria — per changed screen

* Route still resolves at the same path. **No route may be renamed or removed** by a redesign phase.
* Every pre-existing user action still reaches the same API call with the same payload shape.
* Server Actions, form submissions and mutations unchanged in behavior.
* Auth/authorization gating unchanged — a route that required a session still does.
* Deep links and shareable URLs still resolve.
* Zod schemas and Dart DTOs untouched (`do-not-change.md` §2).

### 2.4 State coverage — per changed screen

A redesigned screen must handle every state its predecessor handled, plus any the roadmap adds:
initial load · skeleton · empty · partial data · success · warning · error · retry · offline ·
unauthorized · forbidden (403) · not found (404).

Removing a state another screen relied on is a regression even if nothing crashes.

### 2.5 Responsive criteria

Six widths — **360 · 414 · 768 · 1024 · 1440 · 1920 px**. At every one:

* no horizontal page scroll (wide tables/diagrams scroll inside their own container);
* no clipped dialogs, sheets or menus;
* no content trapped behind fixed navigation;
* touch targets **≥ 44 × 44 px** below 768 px;
* no layout shift after fonts/images settle.

### 2.6 Accessibility criteria

* Contrast **≥ 4.5:1** body / **≥ 3:1** large text and UI boundaries, in both themes.
* Every interactive element keyboard-reachable with a visible focus ring.
* Real semantics — `<button>` / `<a>` / `<label>` / ordered headings, one `<h1>` per page.
* Overlays trap focus, close on `Escape`, and restore focus to their trigger.
* Errors associated with their field via `aria-describedby`, never colour alone.
* `prefers-reduced-motion` honoured by every animation introduced.

Matches and extends `.claude/reviews/accessibility-review.md`.

### 2.7 Visual-consistency criteria

After a phase, the screens it touched must show no:

* hardcoded colour, spacing or radius where a token exists;
* mixed icon sets (`lucide-react` on web/admin, Material on Flutter);
* legacy component alongside its replacement in the same view;
* typography outside the scale.

---

## 3. Escalation

| Situation | Action |
|---|---|
| Gate fails from **this phase's** change | Fix before continuing. Not deferrable. |
| Gate fails from a **pre-existing** defect | Record in the Regression Ledger, mark pre-existing, continue. |
| Fix requires touching a frozen surface | **Blocker.** Stop, ledger it, open a `D-NNN`, ask. |
| Fix requires weakening an assertion | **Blocker.** Ledger + explicit approval. |
