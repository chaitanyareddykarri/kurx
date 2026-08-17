# Kurx UI/UX Redesign — Handoff

A self-contained brief for continuing this program in a new session or on another
machine. **Snapshot as of Phase 50.1, `2b97f5c` (2026-08-08). Phase 46 is PARTIAL — see below.**

> ⚠️ **Superseded framing (corrected 2026-08-15).** This said the root trackers
> `UI_REDESIGN_PROGRESS.md` / `.json` are "the source of truth" and "regenerated from real state every
> phase". Neither has been regenerated since 2026-08-08, the branch they name no longer exists, the
> redesign was merged by [D-314](../DECISIONS.md) on 2026-08-09 — and the two mirrors now disagree with
> each other (94.1% vs 99.9%). **Treat all three files as one historical record**, kept because they
> are the only written trace of what is still open (phases 46/47/51, 13 deferred items).
>
> For *current* state: [`../roadmap/README.md`](../roadmap/README.md) is the build-status authority,
> and [`inventory-web.md`](inventory-web.md) / [`inventory-admin.md`](inventory-admin.md) /
> [`inventory-mobile.md`](inventory-mobile.md) were re-derived from the filesystem on 2026-08-15.

---

## Start here

Repo root — wherever this checkout lives; the absolute macOS path that stood here
(`/Users/naralanaveen/kurx/Kurx`) was one machine's and never portable.

Phases 0–18 are **merged into `main`** (merge commit `554fa1f`). Phases **18A, 19,
20, 20A, 20B, 20C, 20D, 21–31, 34–40** are on the unmerged branch
**`redesign/ui-ux-phase-18a`** (head `6b5cfb7`), one commit per phase. Continue on
that branch, or merge it and branch afresh — `.claude/CLAUDE.md` §8 forbids working
on `main` directly:

```bash
git checkout redesign/ui-ux-phase-18a    # 7 phases ahead of main
```

The original `redesign/ui-ux-phase-0` branch is fully merged and can be deleted.

Run the Session Resume Protocol from `UI_REDESIGN_PROGRESS.md`:

1. Read `UI_REDESIGN_PROGRESS.md` + `.json` — current phase, next action, ledgers.
2. Read `docs/ui-ux/` — `visual-identity.md`, `accessibility-foundation.md`,
   `token-map.md`, `regression-criteria.md`, `do-not-change.md`,
   `information-architecture.md`, `audit-findings.md`.
3. `git status` and `git log --oneline -5`.
4. Verify repo state matches the tracker. **If it does not, STOP and report.**

**98.1% complete** (981/1000 weighted points, 56 of 58 phases complete; 46 is 55%, 47 is 25%, 50 is 30%).
**REG-035 is OPEN** — three broken admin links, see the Phase 47.3 table in the tracker.
**Web (388/388) and admin (98/98) are both finished. M0–M8 are closed.
Resume at Phase 46.3b — 46.1 (alt text), 46.2 (contrast) and 46.3a (focus order,
source half) are done. What remains is genuinely interactive: tabbing every screen,
focus visibility at each stop, focus return on overlay close, SR reading order.
That needs a RUNNING APP, not a source sweep. Contrast needed no work: TEXT_TOKENS already covers every status
colour at 4.5:1 on every surface step in both themes.**

**All three surface tracks are finished** — web 388/388, admin 98/98, mobile 194/194.
M0–M9 are closed. Everything left is **cross-cutting**: 46.3b, 47.2, 50.2 — 3 phases, 30 points.

These are the program's own closing gates rather than new screens — consistency (41),
microinteractions (42), UX writing (43), error recovery (44), performance (45), the
accessibility verification (46) and visual audit (47) that several phases deferred
per-screen checks to, the functional regression (48), the dead-UI deletions (49) that
Phases 21, 26 and 38 all queued up, and final polish (50).
No security-sensitive phase remains: 24, 31, 36 and 37 have all passed review.

**Both mobile sweeps are now at zero and assert that they stay there.** `tap_semantics_test.dart`
(REG-022's 15 sites) and `no_dead_controls_test.dart` (10 dead controls) each keep an **empty**
exception set, so a new bare `GestureDetector` or empty handler anywhere in `lib/` fails the suite.
Do not add to those sets without writing down why.

**Mobile's money path is verified and asserted** — per-screen idempotency key, `guard()` mapping
every throw, both pinned by `ticketing_money_path_test.dart`. Treat them as invariants.

**The mobile worklist is a test, not a document.**
`mobile/test/features/tap_semantics_test.dart` holds REG-022's still-open sites in a
`remaining` set and fails if a new bare `GestureDetector` appears anywhere in `lib/`.
Five of fifteen are done; delete a line as its screen is redesigned and the sweep
tightens itself. A second assertion pins the set at ≤10 so it cannot grow back.

The same scan measured **106 hardcoded colour literals** in `mobile/lib` — the 14 status
colours in the deferred ledger are a subset, and Phase 34 closed one of them
(`Colors.green` on a competition chip). Most need a `BuildContext` threaded through,
which is the work Phases 37/38B/39 were scoped for.

**Admin has a test suite as of Phase 27** — `cd admin && npm test`, vitest + RTL, the
same runner web uses. It starts at **9 tests** and that is now a floor. Its
`test/setup.ts` already shims the two things that otherwise stop admin tests dead:
React's `cache()` (undefined in jsdom, and vitest reports the failure as a suite with
*zero tests* rather than an error) and `server-only`.

⚠️ Admin is the largest untested surface in the product: 27 screens, **no test
suite at all**, guarded only by `next build` plus browser verification. Phase 29
alone is 40 points. The browser approach Phase 25 used works and is worth
reusing — run `npm run dev` in `admin/` and drive it; it adds nothing to the repo,
so D-285 is satisfied.

---

## What already exists — do not rebuild it

**The design system is complete** (`packages/ui`, 180/180 points):

Button · IconButton · Link · LinkButton · Field · Input/Textarea/Select
(forwardRef) · Checkbox · Radio · RadioGroup · Slider · Switch · Badge · Chip ·
Avatar · Divider · Tooltip · Spinner · Progress · Skeleton · DateTimeField ·
Dialog · Sheet · ConfirmDialog · Popover · Menu · CommandPalette · Alert · Banner ·
EmptyState · ErrorState · NotFoundState · PermissionDeniedState · OfflineState ·
Toast · DataTable · SearchBar · FilterBar · FilterChips · Breadcrumbs · Card ·
LinkCard · EventCard · CategoryCard · TicketCard · MetaRow · MediaFrame · Stat ·
Timeline · FormGroup · FormActions · FormBusy · FormSteps · FormErrorSummary.

Tokens are complete **and enforced by tests** on web and Flutter: colour,
typography, spacing, radius, shadow, motion, z-index, breakpoints.

`docs/DECISIONS.md` **D-285 / D-286 / D-287** govern scope, palette and typeface.

Reach for an existing primitive before writing markup. If a pattern repeats,
extend the system rather than duplicating it.

---

## Trap — scripted `.focus()` cannot test `:focus-visible`

Chrome does not match `:focus-visible` for a programmatic `.focus()` on a click-focusable element,
so every button looks ringless to a script that focuses and reads computed style. Anchors do match,
which makes the result look selectively real. **Confirm focus rings with an actual Tab key.** The cheap way: script-focus the
element *before* your target, then press one real Tab — the transition is then keyboard-initiated
and `:focus-visible` matches, without tabbing from the top of the document.

---

## Trap — an unreferenced route is not automatically dead

`/host` has no inbound reference and must keep it that way: it is a `redirect()` stub for old
bookmarks, so its callers are outside the app where no sweep can see them. Deleting it would 404
every stale link; linking it would loop back to where the link came from. **Before acting on an
orphan, open the file** — a redirect, a deep-link target, an OAuth callback and an email-link
landing page all look identical to a reference sweep.

---

## Trap — `npm test` does not typecheck

vitest transpiles without typechecking, so a type error in a test file passes the suite and fails
`tsc`. Phase 46 cast a fixture `as never` to sidestep one; 372 tests stayed green while `npx tsc
--noEmit` was broken, and it surfaced only when something else ran tsc. **Run `npx tsc --noEmit`
separately — a green suite is not a green typecheck.**

---

## Trap — an asserted `href` is not a working link

Phase 44 shipped a 404 page whose primary CTA pointed at `/events`, a route with no page, and the
test asserted that href — so the test agreed with the bug. Typecheck, lint, 372 tests and five
phases of source review all passed it; one page load caught it. **When you assert a link, assert
that its target exists**, or verify it live.

---

## Trap — `web/lib/api.ts` cannot be imported by a test

It calls React's server-only `cache()`, which is not a function under vitest, so importing it fails
with `cache is not a function`. Existing tests either mock `@/lib/api` wholesale or, as in
`test/error-copy.test.ts`, exercise the pure helper and assert the wiring at source level.

---

## Trap — a helper is not a fix until it has consumers

Twice now a foundation has been built and left with one call site: `motion.dart` (Phase 32, wired
into `app_shell.dart` only — Phase 42 found seven files still animating raw) and `Field` (Phase 13,
with 160 hand-rolled controls still bypassing it). **When you add a helper, measure its adoption in
the same phase**, and prefer a sweep that fails on the next un-migrated call site over a note saying
the rest should follow.

Related: `useFormStatus` does not exist at runtime in the react-dom build vitest resolves here, so
web tests mock it (see `test/event-creation.test.tsx` and `test/pending-feedback.test.tsx`). A test
that renders it unmocked fails with `useFormStatus is not a function`, which looks like a component
bug and is not one.

---

## Trap — a second copy of the palette

Flutter cannot read a CSS custom property, so `mobile/lib/core/theme/design_tokens.dart` is a
hand-maintained duplicate of `packages/ui/src/styles/tokens.css`. Phase 41 measured them (28 pairs,
zero drift) and added a guard in `web/test/design-tokens.test.ts` that reads the Dart file, so a
one-sided edit now fails the web suite — **which is where you will see it, not in `flutter test`.**

Colour written as a literal is the related trap: `const Color(0x……)` has one value where a token has
two, so it silently ignores dark theme. `mobile/test/features/palette_discipline_test.dart` bans it
outside three named files.

---

## Remaining work — 3 phases, 30 points

| Track | Phases (weight) | Points |
|---|---|---:|
| **web** | — complete | 0 |
| **admin** | — complete | 0 |
| **mobile** | — complete | 0 |
| **cross-cutting** | 46(6 of 16 left) 47(14 of 16 left) 50(10 of 14 left) | 30 |

Phase definitions and sub-phases live in `UI_REDESIGN_PROGRESS.md`.

🔒 **Security review required:** none remain. (24, 31, 36 and 37 all passed — see their records in the tracker.)
⚠️ **Admin's screens are covered by class, not one by one.** Phases 26–31 fixed every
defect *class* that spans the 27 screens and the two where an outage produced a false
statement, and left screen-by-screen visual passes to Phase 47. A recorded browser check
is still what moves an inventory row past `Redesigned`.

**Phase 48 must re-run the backend suite** — no phase has touched `backend/`, so
`1432 pass / 1 skip / 0 fail` should still hold, but that is a claim to verify rather
than assume. **Phase 49 has a queue waiting**: 4 `/host/templates` placeholder routes
(Phase 21), the orphaned admin routes (26), and `MyDevicesPage` (38).

---

## Verification — exact commands, after every phase

```bash
cd web    && npm run typecheck && npm run lint && npm test && npm run build
cd admin  && npm run typecheck && npm run lint && npm run build   # no test suite
cd mobile && flutter analyze --no-fatal-infos && flutter test

# backend: only when a change could touch a contract, and always in Phase 48
export DOTNET_ROOT=$HOME/.dotnet
cd backend && dotnet build Kurx.sln --no-restore -c Release -warnaserror /nowarn:NU1900 \
           && dotnet test Kurx.sln --no-build -c Release
```

`/nowarn:NU1900` is needed only where the sandbox blocks `api.nuget.org`; CI has
network and does not need it.

### Floors — never go below these

| Suite | Floor |
|---|---|
| web | **372 tests**, **51 static pages**, typecheck + lint clean (**zero warnings** — keep it there) |
| admin | **27 tests**, **29 static pages**, typecheck + lint clean |
| mobile | **327 tests**, `analyze` reports no issues |
| backend | **1432 pass / 1 skip / 0 fail**, 0 build warnings — re-measured in Phase 48; `git diff de38963..HEAD -- backend/` is empty |

Counts may rise. They may not fall. **Never weaken an assertion to reach green.**

---

## How to work

1. **Measure, don't assume.** Every colour decision here came from a computed
   contrast ratio. Four values that looked fine failed WCAG — including *every
   primary CTA in the Flutter app*, at 2.86:1, shipped since D-065.
2. **Prove important guards by breaking them.** Revert the fix, confirm the test
   goes red, restore. A guard that cannot fail is decoration.
3. **Never invent product behaviour.** Where something is non-functional and intent
   is not derivable from the codebase, prefer the safest non-invented option —
   usually removal — and log it in the Regression Ledger. Precedents: **REG-004**
   (a bookmark button with no `onClick`), **REG-005** (an "Event QR code" that
   rendered a lucide glyph; `qrcode.react` is installed but used nowhere).
4. **Preserve everything else.** Routes, API contracts, auth, business logic, Zod
   schemas, Dart DTOs. `backend/**` is frozen — 0 files changed across 38 commits.
5. **One commit per phase**, explaining *why*. Trailer:
   `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
6. **Update both tracker files in the same commit** as the work they describe.
7. **Never stage** `.env*`, secrets, or unrelated dirty files.

---

## Traps this program already hit

Each of these cost real time. They are not hypothetical.

| Trap | What happens |
|---|---|
| `npm ci` inside `admin/` | Prunes the workspace root and **empties `web/node_modules`**. Reinstall web afterwards. (A *root* `npm install --workspaces` is safe: Phase 27 ran one and web stayed green. An empty `web/node_modules` is normal hoisting, not damage — check `npm test` before assuming otherwise.) |
| Stale Vite cache | A newly created file fails as `Failed to load url @/...`. `rm -rf web/node_modules/.vite`, re-run. |
| `\b` at a hyphen | `/\btext-accent\b/` **matches inside** `text-accent-text`. Use a negative lookahead. |
| `git commit --amend` after recording a hash | The amend invalidates the hash just written. Record hashes in the **next** commit. |
| Trimming a worklist before the work is done | Phase 38B briefly removed two files from the semantics list before wrapping their gestures. The sweep failed on the next run and caught it — which is the argument for running the suite after every trim, not just at the end. |
| Assuming a defect before reading the layer beneath | Phase 37 nearly "fixed" checkout's `on ApiError` for a stuck state that `guard()` makes impossible — it maps *every* throw. Read the data layer before hardening the screen. |
| A guard that only asserts one direction | Phase 36's autofill test asserts OTP fields **are** hinted *and* that the recovery code and D-181 challenge are **not** — autofilling either would delete a security property. A one-way guard invites the regression it was written to stop. |
| `google_fonts` | Fetches at *theme construction*, so it needs the network. `AppTheme.light/dark` take an injectable `TextTheme`; pass a plain one in tests. |
| `<legend>` not first child of `<fieldset>` | The group is **silently unnamed**. |
| Capture-phase `Escape` handler | Runs before React's `onKeyDown` and stops propagation, so a component's own Escape logic never fires. |
| Tinting a token | A tint eats contrast headroom — every tinted `Badge` tone failed AA. Solve foregrounds against **all three** surface steps, not just `background`. |
| Nested landmarks | Phase 11 added `<main>` to the `(public)` layout while all 8 pages inside already had one. **This program caused that** and did not catch it for two phases. |
| A colour class naming no token | Tailwind emits **nothing**, silently — not a type, lint or build error. 20 such classes shipped (`bg-card`, `text-destructive`, …). `web/test/design-tokens.test.ts` now fails on them; keep it green. |
| `aria-label` on a bare `<svg>` | Not exposed by several screen readers. Icons that carry meaning need `role="img"` as well. |
| `aria-label` on a control with a visible count | The label **replaces** the content, so the count is announced to nobody. Put it in the name. |
| An import inserted above `"use client"` | Voids the directive and turns a client component into a server one. Keep the directive first. |
| `try/catch` around a server action that redirects | Next implements `redirect()` by **throwing**. Catching indiscriminately swallows the success path and reports a failure on every success. Re-throw anything whose `digest` starts with `NEXT_REDIRECT`. |
| `git checkout <file>` to undo a break-the-guard test | Reverts to the last **commit**, taking any *uncommitted* work in that file with it. Snapshot to a temp file instead. |
| `Button size="sm"` on web | 36px — the system reserves it for pointer-only admin density. Web's floor is 44px; omit the size. |
| A hand-rolled input class | Phase 21 found the same string forked into 20 constants across 17 files, all 40px on the decorative border token with no focus ring. Use `Field` + `Input`, or `controlClass` where neither fits. Three sweep tests in `host-workspace.test.tsx` hold the line. |
| A global regex over `"  >"` or similar | Reformats JSX closing brackets across every file it touches. Anchor whitespace edits to a specific match. |
| Python `str.replace` on a code block | Replaces **every** occurrence, not the first. It once wrapped four unrelated actions in a `catch` with no `try`. Assert the match count first, then pass `1`. |
| A form written against a contract its action never delivered | Phase 23 found `state && "error" in state` rendered against an action that threw instead of returning. Check both ends before trusting either. |
| A `<label>` wrapping both a control and its hint | The hint becomes part of the **accessible name**, not the description. Phase 24 found a 25-word name on an email box. Use `Field`'s `helper`. |
| `className="hidden"` on a control | Deletes it from the accessibility tree rather than hiding it. Use `sr-only` when it must stay reachable. |
| Success-only state updates on a *replace* path | A failed replace then leaves the previous value looking current. Clear first, then attempt. |
| Testing responsive at the narrowest width only | Phase 25 fixed 360 and then found 768 **worse** — that is where a `md:` row appears but does not yet fit. Measure all six. |
| **A source-scanning guard satisfied by its own comment** | Phase 35's checkout guard matched `/checkout/` anywhere in the file, which its own explanatory comment satisfied — so breaking the real `push()` left it **green**. Strip comments *before* matching, and always break the guard to prove it. This trap has now appeared on all three surfaces. |
| A source-scanning test flagging its own comment | A comment describing a defect reads exactly like the defect. Skip `{/* … */}` blocks, or the guard fails on its own explanation. Bit twice: also assert on an *import line*, not the whole file, when a comment names what it replaced. |
| `Button` inside a form | Defaults to `type="submit"` in HTML. Phase 30 fixed the primitive to default `type="button"` — a `ConfirmDialog` inside a form was submitting it twice, and its **Cancel** submitted too. Keep `type="submit"` explicit where you want it. |

---

## Open items to carry

**Regressions still open**

* **REG-004** — the event card's save control was removed rather than invented; it
  needs a real one once the list endpoint exposes saved state. Owner: Phase 13/20.
* **REG-006** — `/o/[slug]` states an event count with no way to see the events; there
  is no public events-by-organization surface. Owner: Phase 50.
* **REG-009** — 🚨 **the web booking journey is unbuilt.** The old checkout opened
  Razorpay with no order and reported success regardless; it was neutralised with the
  user's agreement, so `/book/[slug]` is now an honest hand-off to the app. Building
  real web checkout is feature work on the money path: it needs a `D-NNN` and a
  security review. **This needs a product owner, not a redesign phase.**
* **REG-010** — `DownloadApp` renders a decorative QR captioned as a download QR, and
  its "Continue in Browser" button links to the `kurx://` app deep link. Owner: Phase 50.

**Closed this session:** REG-003 (Phase 20), REG-007, REG-008 (Phase 18A), REG-011
(Phase 20B).

**Deferred ledger**

* Verify `/host/admin/*` is orphaned after D-195 → Phase 49
* Four `WorkflowPage` placeholders → Phase 21/49
* i18n wired at only 2 call sites, so Phase 43 copy will not be translatable → Phase 43
* Self-host the Anek webfont binaries (needs network) → Phase 45
* 14 hardcoded Flutter status colours, each needing a `BuildContext` threaded
  through a tuple-returning helper → Phases 37/38B/39
* Admin's two `Tabs` call sites have keyboard navigation but no tab↔panel wiring → 28/29
* Six `EmptyState` call sites still pass Material Symbols *names* as icons (they now
  render no icon rather than the identifier) → 20A/20B/21
* `GET /v1/me/participations` returns no event title, so `/workspace` titles each
  participation by role → needs a backend owner
* **Privacy and Terms are one paragraph each.** For a product taking payments in
  India this is a compliance gap needing a real owner — legal copy is not
  something a redesign should invent.

---

## Working style

Work autonomously through phases. Do not stop between them for approval or to
report progress — record a checkpoint in the tracker and continue. Stop only for a
genuine blocker requiring information, credentials or authorisation you do not
have.

If context runs out before completion, leave both tracker files exact and stop
cleanly. **Do not claim phases you did not finish.**

### Final report should cover

1. Completion status and phases/milestones done
2. Major UI/UX and accessibility changes
3. Defects found beyond the original plan
4. Test / typecheck / lint / build results for all four projects
5. Remaining known issues, limitations and deferred items
6. Whether the planned redesign is complete
