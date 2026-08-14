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
| **T2 — authenticated** | the remaining 76 web + 24 admin routes | Same, against a locally seeded stack (Postgres + API + `next dev`) | Before Phases 11, 25 and 47 |
| **T3 — Flutter** | 91 pages | Simulator screenshots for the 20 journey-critical pages only | Before Phases 33 and 40 |

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
  screen is visible as a gap rather than silently skipped.

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
| web `npm test` | 8 files · 145 tests · 145 pass | **≥ 145 passing, 0 failing** |
| mobile `flutter test` | 275 tests · all pass | **≥ 275 passing, 0 failing** |
| backend `dotnet test` | 1433 total · 1432 pass · 1 skip · 0 fail | **≥ 1432 passing, 0 failing, ≤ 1 skipped** |
| admin | no suite exists | recorded browser verification per screen |

**Assertions are never weakened to reach these numbers.** Test counts may rise; they may not fall.
See the Flutter Test Policy in `UI_REDESIGN_PROGRESS.md` for the finder-vs-assertion rule.

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
