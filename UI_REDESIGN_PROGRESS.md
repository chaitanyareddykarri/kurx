# Kurx UI/UX Redesign — Live Progress

> ⚠️ **Read this before treating anything below as current (checked 2026-08-15).**
>
> - **The redesign was merged.** [D-314](docs/DECISIONS.md) merged this program into the feature branch
>   on 2026-08-09 (with four changes explicitly refused), and it is on `main` today.
> - **The branch named below no longer exists.** `redesign/ui-ux-phase-18a` is absent locally and on
>   `origin`; the only branches are `main` and `origin/fix/compose-pgdata17-volume`.
> - **The two mirrors disagree with each other.** This file's dashboard reads 94.1% overall / 87.5%
>   weighted; `UI_REDESIGN_PROGRESS.json` reads `overallProgress: 99.9`, `status: READY_TO_RESUME`.
>   By this file's own rule that is a STOP condition, and it has been true since 2026-08-08.
> - **It is therefore a historical record, not a live tracker** — and it is kept rather than deleted
>   because it is the only place the remaining work is written down: phases **46, 47 and 51** are
>   `IN_PROGRESS` (46 = accessibility verification, which needs a running app), the queue is 46–50,
>   and 13 items are deferred. Anyone resuming should re-derive state from the repository first.
>
> Current build status is [`docs/roadmap/README.md`](docs/roadmap/README.md); the shipped page
> inventories are [`docs/ui-ux/inventory-{web,admin,mobile}.md`](docs/ui-ux/), which were re-derived
> from the filesystem on 2026-08-15.

**What this file was.** The live tracker for the redesign program. Tracker state and repository state
were required to agree; a disagreement was a STOP.
Machine-readable mirror: [`UI_REDESIGN_PROGRESS.json`](UI_REDESIGN_PROGRESS.json).

---

## Dashboard

```
Overall:        94.1%
Current Phase:  46 — Accessibility Verification  (IN PROGRESS — alt text done, 46.2+ remain)
Current Task:   — resume at 46.3 (keyboard traversal, focus order, SR flow — needs a running app)
Status:         IN PROGRESS
Last Updated:   2026-08-08
Branch:         redesign/ui-ux-phase-18a
Baseline commit: c66ae16
```

| Track | Weight | Earned | % |
|---|---:|---:|---:|
| **Overall** | **1000** | **875.0** | **87.5%** |
| Design system | 180 | 180 | 100% |
| Web | 388 | **388** | **100%** |
| Admin | 98 | **98** | **100%** |
| Mobile | 194 | **194** | **100%** |
| Accessibility (surfaces checked / 206) | 206 | 54 | 26.2% |

| Status | Count |
|---|---:|
| PENDING | 6 |
| READY | 0 |
| IN PROGRESS | 1 |
| VALIDATING | 0 |
| COMPLETE | 49 |
| BLOCKED | 0 |

Tracks overlap (Phase 32 counts in both Design system and Mobile); only **Overall** sums to 1000.

### Progress rule

`overall = Σ(weight × phase_fraction) / 1000 × 100`

A phase contributes its **full** weight only at `COMPLETE`. While `IN PROGRESS` / `VALIDATING` it
contributes `weight × (completed sub-phases / total sub-phases) × 0.9` — capped at 90% so nothing
reads as done before its completion gate passes.

---

## Currently Working On

**Nothing in progress — session ended cleanly.** Resume at **Phase 34**. **The admin track is complete — M7 closed. Only mobile (158) and cross-cutting (128) remain.**

**A mobile finding is already recorded and partly fixed — see REG-022.** No mobile phase is claimed:
two of fifteen sites are done, and the remaining thirteen belong to the phases that own their
screens.

**M0–M3 and M8 complete; the design system is done (180/180).** Phases 32–33 were taken out of
milestone order because Phase 32 carried the highest-impact defect left in the product — every
primary CTA in the Flutter app at 2.86:1.

**Phases 13 and 14 converge on one route.** `/discover` is both the discovery surface *and* the
search-results page, so they were delivered together.

### 🎯 All five S1 findings are now closed

| Finding | Closed by | Reach |
|---|---|---|
| **S1-5** forked primitives | 6.6 `1c6b462` | 8 files collapsed; unblocked S1-1 for auth |
| **S1-1** form errors colour-only | Phase 7 `563c85e` | **86 `<Field>` call sites across 22 files** |
| **S1-4** toast silent + undismissable | Phase 8 `3b21fd0` | every row-action outcome in web + admin |
| **S1-2** no focus trap / restore / scroll lock | Phase 9 `14779ed` | `Dialog`, `Sheet`, and every `ConfirmDialog` |
| **S1-3** `Sheet` unnamed on a `ReactNode` title | Phase 9 `14779ed` | all three admin workspaces |

Four further contrast defects were found *while fixing those* and are also closed: web's
`Button` primary (white on ember, 2.86:1 — the audit had recorded this as Flutter-only),
`ConfirmDialog`'s hand-rolled danger button (white on danger, **3.29:1 in dark theme**),
every tinted `Badge` tone (3.61–4.62:1, both themes), and the `Switch` knob (2.80:1).

## Recently Completed

| When | Task | Result |
|---|---|---|
| 2026-08-08 | **Phase 50 — Final Product Polish (partial)** | 🟡 IN PROGRESS — **REG-033**: `/events/review`, the full moderation review flow (claim, notes, reason codes), existed and worked with **no door**: absent from `NAV`, named only in a comment. Its role gate was correct by accident — `requiredRolesFor` fell back to `/events`, also VerificationReviewer. Now an explicit nav entry. Remaining polish items not attempted |
| 2026-08-08 | **Phase 49 — Dead UI Cleanup** | ✅ COMPLETE — deleted 3 unreachable `/host/templates/*` placeholder routes (web 65 → 62 pages). **`MyDevicesPage` was NOT dead**: the Phase 38 queue entry was stale, and `profile_page.dart:174` pushes to it — deleting it would have removed a live screen. Queue corrected rather than executed |
| 2026-08-08 | **Phase 48 — Functional Regression** | ✅ COMPLETE — all four suites re-run on `2129c33`: backend **1432 pass / 1 skip / 0 fail** (5 m 32 s), web 372, admin 27, mobile 327. `git diff de38963..HEAD -- backend/` is empty, so the backend is byte-for-byte unchanged across all 55 phases — the contract-freeze held. **No regression in any suite across the whole program** |
| 2026-08-08 | **Phase 46 — Accessibility Verification (partial)** | 🟡 IN PROGRESS — **REG-032**: four sites used `alt=""` on images that carry meaning, including the post media grid where the images *are* the post, so a reader was told a post existed and never that it had pictures. **Only the alt-text dimension is done**; keyboard traversal, focus order, screen-reader flow and contrast verification remain |
| 2026-08-08 | **Phase 45 — Performance UX** | ✅ COMPLETE — **REG-031**: a Next segment with no `loading.tsx` does not stream, so the browser holds the previous page until the new one's data arrives. Web had 7, all on deep `/host` leaves; the `(public)` group and the `(app)` root — where nearly all navigation happens — had none across 87 async server pages |
| 2026-08-08 | **Phase 44 — Error Recovery** | ✅ COMPLETE — **REG-029**: web's only `error.tsx` was on `/u/[username]`; every other route including the whole signed-in app and checkout fell through to Next's unstyled production error screen, with no retry and no navigation. **REG-030**: no `not-found.tsx`, so every `notFound()` rendered Next's default 404 — and D-018 routes hidden-but-real resources there by design, so its copy must not claim the thing does not exist |
| 2026-08-08 | **Phase 43 — UX Writing** | ✅ COMPLETE — **REG-028**: `error` on every ProblemDetails is a machine-readable code by documented contract, and both web and admin returned it verbatim from `apiErrorMessage` — so a mistyped OTP showed the literal string `invalid_code` and a refused moderator saw `forbidden`. Every failed request on both surfaces went through that one function. Mapped via a new shared `problem-copy.ts` reusing mobile's existing wording, with a test comparing the two maps. Terminology measured clean — no user-visible copy uses a term D-271 retires |
| 2026-08-08 | **Phase 42 — Microinteractions** | ✅ COMPLETE — **REG-026**: `motion.dart` was built in Phase 32 and wired into one consumer; 7 files still animated with raw durations, two of them `..repeat()` with no end condition (the skeleton shimmer, on screen during every load, and the chat typing dots) — WCAG 2.3.3, and 2.2.2 for the repeating pair. **REG-027**: both confirm-then-submit primitives submit natively, so the trigger looked identical before and during a destructive action; on admin, Reject stayed live while an Approve was in flight against the same event. Web/admin CSS motion needed nothing |
| 2026-08-08 | **Phase 41 — Cross-Platform Consistency** | ✅ COMPLETE — the two palettes agree exactly (28/28 token pairs), and a new web guard reads `design_tokens.dart` to keep them that way. **REG-024**: 11 sites named a colour as a literal where a token existed — a literal has one value where a token has two, so each was also a dark-theme defect; two callout backgrounds fixed at a light cream would have put light text on a light ground. **REG-025**: mobile called a `paid`/`free` order "Confirmed", collapsing the distinction web spells out and a refund turns on |
| 2026-08-08 | **Phase 40 — Mobile Native UX** | ✅ COMPLETE — **the mobile track is done.** There was no `PopScope` anywhere in the app, so Android's back gesture discarded a verified phone and everything typed in registration, silently |
| 2026-08-08 | **Phase 39 — Flutter Organizer Screens** | ✅ COMPLETE — **both self-shrinking worklists are now empty**: REG-022's 15 unannounced tappables and all 10 dead controls are closed. The check-in scanner's torch had no name |
| 2026-08-08 | **Phase 38B — Identity · Certificates · Gamification** | ✅ COMPLETE — **six dead controls on one screen**: a share row that failed four branded ways, and a Download PDF that only appeared when there *was* a document and then did nothing with it |
| 2026-08-08 | **Phase 38A — Flutter Posts & Chat** | ✅ COMPLETE — **a message's actions existed only behind an unannounced long-press**, so reply/copy/delete did not exist for a screen reader. Both worklists tightened |
| 2026-08-08 | **Phase 38 — Flutter Profile & Settings** | ✅ COMPLETE — a **dead "Remove" button on the devices screen**, telling someone a device was revoked when nothing happened; the page is an orphan shadowing a working one. Ten dead controls found and pinned |
| 2026-08-08 | **Phase 37 — Flutter Tickets & Registration** 💰🔒 | ✅ COMPLETE — a **dead share button on the screen holding the gate credential**; the checkout logic itself was found sound and left alone. Security review passed |
| 2026-08-08 | **Phase 36 — Flutter Authentication** 🔒 | ✅ COMPLETE — **the OTP autofill defect Phase 16 fixed on web was still live on mobile**, where the SMS arrives on the same device. Security review passed; two fields deliberately left un-hinted |
| 2026-08-08 | **Phase 35 — Flutter Event Detail** | ✅ COMPLETE — **mobile's checkout worked and nothing navigated to it.** A person could read an event and had no way to register from the page describing it |
| 2026-08-08 | **Phase 34 — Flutter Discovery** | ✅ COMPLETE — a status chip used Material's `Colors.green` while every sibling read the theme; the search's clear button had no name; the semantics sweep is now a **Dart test carrying its own worklist** |
| 2026-08-08 | **Phase 31 — Admin Real-Time UX** 🔒 | ✅ COMPLETE — **M7 closed.** A dead socket and a quiet gate looked identical on the check-in monitor. Security review passed |
| 2026-08-07 | **Phase 30 — Admin Review / Moderation UX** | ✅ COMPLETE — approve and reject fired on one click; and the gate exposed a **design-system defect: `Button` had no default `type`**, so every Button in a form submitted it — including `ConfirmDialog`'s Cancel |
| 2026-08-07 | **Phase 29 — Admin Core Screens** ⚠️ | ✅ COMPLETE — an outage told a moderator **"no open reports"** and an operator **"nothing else is signed in as you"**; 29 sub-44px controls and 21 locale-unstable dates swept |
| 2026-08-07 | **Phase 28 — Admin Data Components** | ✅ COMPLETE — **`DataTable`'s clickable rows were mouse-only**, on the component behind 15 admin screens; its scroll region was unreachable by keyboard and every row checkbox shared one name |
| 2026-08-07 | **Phase 27 — Admin Shell** | ✅ COMPLETE — **admin has a test suite for the first time (9 tests)**; its mobile drawer declared `aria-modal="true"` and implemented none of it — the S1-2 defect Phase 9 closed for web, never wired to admin's own navigation |
| 2026-08-07 | **Phase 26 — Admin Information Architecture** | ✅ COMPLETE — **the console's role-based IA was declared in the nav and enforced nowhere**: every page guarded only "is staff", so 24 of 24 routes were open to any role. Now 20 are scoped |
| 2026-08-07 | **Phase 25 — Web Responsive Pass** | ✅ COMPLETE — **the web track is done.** Every public page scrolled sideways at 360 *and* at 768; `Dialog` had no vertical bound, so a confirmation's buttons were unreachable on a short viewport |
| 2026-08-07 | **Phase 24 — Authorization / Representative Flows** 🔒 | ✅ COMPLETE — **M5 closed.** A failed replacement kept the *previous* letter attached, so a resubmit would have filed the wrong document; two fields had a paragraph for an accessible name. Security review passed |
| 2026-08-07 | **Phase 23 — Event Editing** | ✅ COMPLETE — the form's **error branch was dead code**: the action threw instead of returning, so a failed save took the whole page to an error boundary and a successful one confirmed nothing |
| 2026-08-07 | **Phase 22 — Event Creation** | ✅ COMPLETE — **five single-select steps were grids of plain buttons**, so the choice was carried by a border colour and nothing else; a disabled Continue never said why |
| 2026-08-07 | **Phase 21 (part 3) — form controls, the gate, representation** | ✅ **COMPLETE** — **160 hand-rolled controls, zero `Field` wrappers**: Phase 7's S1-1 fix never reached the host surface. The check-in verdict was announced to nobody |
| 2026-08-07 | **Phase 21 (part 2) — destructive actions** | ✅ **13 irreversible deletes fired on one click**, including a whole draft event, and that one also swallowed its own failures |
| 2026-08-07 | **Phase 21 (part) — Host workspace shell, placeholders, sweeps** | 🔄 IN PROGRESS — four routes shipped an engineering note to hosts; 29 sub-AA accent sites and 55 shim imports swept; the last lint warning closed |
| 2026-08-07 | **Phases 20C + 20D — Certificates, Groups, Notifications & Invitations** | ✅ COMPLETE — **M4 closed.** A user's own certificates were read through the *public* endpoint, so private ones were invisible to their owner; certificate verification answered "no such certificate" for an outage |
| 2026-08-07 | **Phase 20B — Messaging & Chat** | ✅ COMPLETE — **20 colour classes named tokens that do not exist**, so every incoming chat bubble had no background and every auth error was not red; now guarded by a test |
| 2026-08-07 | **Phase 20A — Posts & Social Feed** | ✅ COMPLETE — **every like, comment, share and save count in the product was silent**; like/save exposed no toggle state; three optimistic actions reverted without a word |
| 2026-08-07 | **Phase 20 — User Events & Tickets** | ✅ COMPLETE — REG-003 closed; the "Hosting" count was `array.length` over one page of 50; the current view was marked by colour alone |
| 2026-08-07 | **Phase 19 — Ticketing / Registration** 💰 | ✅ COMPLETE — web's checkout **could not take a payment and said it had**; `/tickets` never rendered the code that opens the gate; an outage read as "you have no tickets" |
| 2026-08-07 | **Phase 18A — Public Profile & Professional Identity** | ✅ COMPLETE — `Tabs` declared `role="tablist"` and implemented none of it; **web never mounted the toast provider Phase 8 built**; ally actions edited the list whether or not the server agreed |
| 2026-08-07 | **Phase 18 — Profile & Account** | ✅ COMPLETE — optimistic toggles reverted **silently**; delete-account submit looked identical to Cancel |
| 2026-08-07 | **Phase 17 — Onboarding** | ✅ COMPLETE — registration checklist state was invisible to screen readers |
| 2026-08-07 | **Phase 16 — Authentication** 🔒 | ✅ COMPLETE — OTP field had **no `autocomplete="one-time-code"`**, so SMS autofill never fired; focus now moves to the code step |
| 2026-08-07 | **Phase 13A — Public Marketing Site** | ✅ COMPLETE — fixed a **nested-`<main>` regression this program introduced in Phase 11** |
| 2026-08-07 | **Phase 33 — Flutter Navigation** | ✅ COMPLETE — tab bar was a bare `GestureDetector`, unannounced and untappable to a11y; 80px inset now measured |
| 2026-08-07 | **Phase 15 — Event Detail** | ✅ COMPLETE — a **cancelled event rendered green as "success"**; sticky mobile CTA; REG-005 fake QR removed |
| 2026-08-07 | **Phases 13 + 14 — Discover & Results** | ✅ COMPLETE — filters no longer bury results on mobile; empty state has a way out |
| 2026-08-06 | **Phase 32 — Flutter Design Foundation** | ✅ COMPLETE — **every primary CTA in the app was failing AA**; palette/radius/motion now match web; reduced-motion helper added |
| 2026-08-06 | REG-001 + REG-002 fixed | Account recovery and every invitation→event link were 404ing |
| 2026-08-06 | **Phase 12 — Search & Discovery** | ✅ COMPLETE — SearchBar announces results; FilterChips new; touch/contrast fixes |
| 2026-08-06 | **Phase 11 — Application Shell** | ✅ COMPLETE — **mobile navigation now exists**; skip link; named landmarks; current-page marking; footer |
| 2026-08-06 | **Phase 10 — Content Components** | ✅ COMPLETE — card family, timeline, stats; web `EventCard` migrated; found REG-004 (dead bookmark button) |
| 2026-08-06 | **Phase 9 — Overlays** | ✅ COMPLETE — focus trap/restore/scroll lock; Popover, Menu, CommandPalette; **S1-2 + S1-3 closed** |
| 2026-08-06 | **Phase 8 — Feedback & State** | ✅ COMPLETE — toast live region + manual dismiss; Alert/Banner/403/404/offline; **S1-4 closed** |
| 2026-08-06 | **Phase 7 — Form System** | ✅ COMPLETE — `Field` aria wiring reaches 86 call sites incl. all 5 auth components; **S1-1 closed** |
| 2026-08-06 | **Phase 6 — Core UI Primitives** | ✅ COMPLETE — 7 new primitives; 5 measured a11y defects fixed |
| 2026-08-06 | 6.3–6.5 DateTimeField · Badge · Divider · Tooltip · Progress | Badge tint measured 3.61–4.62:1 in both themes → tint removed; Spinner and Skeleton were silent to screen readers |
| 2026-08-06 | 6.2 Form controls | Checkbox/Radio/Slider created (hand-rolled in 8+ files); inputs onto `border-strong`; `error` now sets `aria-invalid` |
| 2026-08-06 | 6.1 Button · IconButton · Link | Primary CTA was `text-white` on ember (2.86:1) **on web too** — now ink at 6.08:1; IconButton enforces 44px + required label |
| 2026-08-06 | 6.6 Collapse forked primitives | All 18 `web/components/ui` files are shims; 2 latent bugs fixed (Avatar `shrink-0`, Tabs overflow) |
| 2026-08-06 | **Phase 5 — Accessibility Foundation** | ✅ COMPLETE — per-component contracts; review gate extended with 9 new criteria |
| 2026-08-06 | **Phase 4 — Design Tokens** | ✅ COMPLETE — D-286 palette shipped; contrast now enforced by tests; D-287 typeface |
| 2026-08-06 | **Phase 3 — Kurx Visual Identity** | ✅ COMPLETE — D-286 supersedes D-065; measured both palettes, both failed AA |
| 2026-08-06 | **Phase 2 — Information Architecture** | ✅ COMPLETE — 3 broken links found (2×S1), 18 orphan routes, Phases 17 & 21 re-scoped |
| 2026-08-06 | **Phase 1 — Product & UX Audit** | ✅ COMPLETE — 5×S1, 7×S2, 6×S3, 5×S4; S1-5 reordered the fix plan |
| 2026-08-06 | **Phase 0 — Baseline & Safety** | ✅ COMPLETE — all 5 sub-phases |
| 2026-08-06 | 0.5 Regression criteria | `docs/ui-ux/regression-criteria.md` — build/test/functional/state/responsive/a11y/visual gates |
| 2026-08-06 | 0.4 Visual baseline strategy | 3-tier manual capture, no new repo dependency (D-285) |
| 2026-08-06 | 0.2 Baseline verification run | All four suites green; backend baseline corrected 1297 → **1433** |
| 2026-08-06 | 0.3 Frozen-surface list | `docs/ui-ux/do-not-change.md`; 10 pre-existing dirty files quarantined |
| 2026-08-06 | 0.1 Inventory & tracker scaffold | 206 screens catalogued across 3 inventory files; tracker created |

## Next Up

1. **26–31** — Admin (98) across 27 screens with no automated test suite
2. **34–40** — Core mobile (158), including Phase 39's 36 Flutter organizer pages
3. **26–31** — Admin (98) across 27 screens with no automated test suite
4. **34–40** — Core mobile (158), including Phase 39's 36 Flutter organizer pages

## Blockers

**None.** The four planning-stage decisions (BD-1…BD-4) were resolved with the recommended answers
and are recorded in the UX Decision Log below.

---

## Phase Progress

Weights total **1000**. Statuses: `PENDING` `READY` `IN PROGRESS` `VALIDATING` `COMPLETE` `BLOCKED`.

### M0 — Baseline Established (34)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 0 | Baseline & Safety | 14 | **COMPLETE** | 100% |
| 1 | Product & UX Audit | 14 | **COMPLETE** | 100% |
| 2 | Information Architecture | 6 | **COMPLETE** | 100% |

### M1 — Kurx Design Language Established (42)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 3 | Kurx Visual Identity | 12 | **COMPLETE** | 100% |
| 4 | Design Tokens | 20 | **COMPLETE** | 100% |
| 5 | Accessibility Foundation | 10 | **COMPLETE** | 100% |

### M2 — Design System Implemented (114)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 6 | Core UI Primitives | 42 | **COMPLETE** | 100% |
| 7 | Form System | 18 | **COMPLETE** | 100% |
| 8 | Feedback & State Components | 14 | **COMPLETE** | 100% |
| 9 | Overlay & Interaction Components | 20 | **COMPLETE** | 100% |
| 10 | Content Components | 20 | **COMPLETE** | 100% |

### M3 — Web Foundation Complete (22)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 11 | Application Shell | 22 | **COMPLETE** | 100% |

### M4 — Core Customer Web Complete (254)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 12 | Search & Discovery UX | 16 | **COMPLETE** | 100% |
| 13 | Web Home / Discover | 14 | **COMPLETE** | 100% |
| 13A | Public Marketing Site | 10 | **COMPLETE** | 100% |
| 14 | Event Listing & Search Results | 12 | **COMPLETE** | 100% |
| 15 | Event Detail Experience | 30 | **COMPLETE** | 100% |
| 16 | Authentication 🔒 | 18 | **COMPLETE** | 100% |
| 17 | Onboarding | 8 | **COMPLETE** | 100% |
| 18 | Profile & Account | 20 | **COMPLETE** | 100% |
| 18A | Public Profile & Professional Identity | 32 | **COMPLETE** | 100% |
| 19 | Ticketing / Registration 💰 | 30 | **COMPLETE** | 100% |
| 20 | User Events & Tickets | 12 | **COMPLETE** | 100% |
| 20A | Posts & Social Feed | 16 | **COMPLETE** | 100% |
| 20B | Messaging & Chat | 14 | **COMPLETE** | 100% |
| 20C | Certificates · Gamification · Groups · Competitions | 12 | **COMPLETE** | 100% |
| 20D | Notifications & Invitations | 10 | **COMPLETE** | 100% |

### M5 — Host / Representation Web Complete (92)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 21 | Host / Workspace Experience | 44 | **COMPLETE** | 100% |
| 22 | Event Creation | 24 | **COMPLETE** | 100% |
| 23 | Event Editing | 12 | **COMPLETE** | 100% |
| 24 | Authorization / Representative Flows 🔒 | 12 | **COMPLETE** | 100% |

### M6 — Web Production Pass (20)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 25 | Web Responsive Pass | 20 | **COMPLETE** | 100% |

### M7 — Admin Redesign Complete (98)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 26 | Admin Information Architecture | 6 | **COMPLETE** | 100% |
| 27 | Admin Shell | 12 | **COMPLETE** | 100% |
| 28 | Admin Data Components | 18 | **COMPLETE** | 100% |
| 29 | Admin Core Screens ⚠️ | 40 | **COMPLETE** | 100% |
| 30 | Admin Review / Moderation UX | 14 | **COMPLETE** | 100% |
| 31 | Admin Real-Time UX 🔒 | 8 | **COMPLETE** | 100% |

### M8 — Mobile Foundation Complete (36)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 32 | Flutter Design Foundation | 24 | **COMPLETE** | 100% |
| 33 | Flutter Navigation | 12 | **COMPLETE** | 100% |

### M9 — Core Mobile Complete (158)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 34 | Flutter Discovery | 16 | **COMPLETE** | 100% |
| 35 | Flutter Event Detail | 14 | **COMPLETE** | 100% |
| 36 | Flutter Authentication 🔒 | 14 | **COMPLETE** | 100% |
| 37 | Flutter Tickets & Registration 💰 | 18 | **COMPLETE** | 100% |
| 38 | Flutter Profile & Settings | 16 | **COMPLETE** | 100% |
| 38A | Flutter Posts & Chat | 16 | **COMPLETE** | 100% |
| 38B | Flutter Identity · Certificates · Gamification | 14 | **COMPLETE** | 100% |
| 39 | Flutter Organizer Screens | 36 | **COMPLETE** | 100% |
| 40 | Mobile Native UX | 14 | **COMPLETE** | 100% |

### M10 — Cross-Platform Polish (94)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 41 | Cross-Platform Consistency | 12 | **COMPLETE** | 100% |
| 42 | Microinteractions | 10 | **COMPLETE** | 100% |
| 43 | UX Writing | 14 | **COMPLETE** | 100% |
| 44 | Error Recovery | 14 | **COMPLETE** | 100% |
| 45 | Performance UX | 12 | **COMPLETE** | 100% |
| 46 | Accessibility Verification | 16 | **IN PROGRESS** | 55% |
| 47 | Visual Regression Audit | 16 | **IN PROGRESS** | 10% |

### M11 — Regression & Cleanup (20)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 48 | Functional Regression | 12 | **COMPLETE** | 100% |
| 49 | Dead UI Cleanup | 8 | **COMPLETE** | 100% |

### M12 — Production-Ready Redesign (16)

| Phase | Name | W | Status | Progress |
|---|---|---:|---|---:|
| 50 | Final Product Polish | 14 | **IN PROGRESS** | 30% |
| 51 | Progress Tracking & Ledger | 2 | IN PROGRESS | — |

🔒 security-sensitive (mandatory security review) · 💰 money path · ⚠️ no automated test coverage

---

## Milestone Status

| M | Name | Phases | Weight | Status | % |
|---|---|---|---:|---|---:|
| M0 | Baseline Established | 0–2 | 34 | **COMPLETE** | 100% |
| M1 | Kurx Design Language Established | 3–5 | 42 | **COMPLETE** | 100% |
| M2 | Design System Implemented | 6–10 | 114 | **COMPLETE** | 100% |
| M3 | Web Foundation Complete | 11 | 22 | **COMPLETE** | 100% |
| M4 | Core Customer Web Complete | 12–20D | 254 | **COMPLETE** | 100% |
| M5 | Host / Representation Web Complete | 21–24 | 92 | **COMPLETE** | 100% |
| M6 | Web Production Pass | 25 | 20 | **COMPLETE** | 100% |
| M7 | Admin Redesign Complete | 26–31 | 98 | **COMPLETE** | 100% |
| M8 | Mobile Foundation Complete | 32–33 | 36 | **COMPLETE** | 100% |
| M9 | Core Mobile Complete | 34–40 | 158 | **COMPLETE** | 100% |
| M10 | Cross-Platform Polish | 41–47 | 94 | PENDING | 0% |
| M11 | Regression & Cleanup | 48–49 | 20 | PENDING | 0% |
| M12 | Production-Ready Redesign | 50–51 | 16 | IN PROGRESS | — |

---

## Sub-Phase Register

Only phases that have started are expanded here; the rest carry the sub-phase breakdown recorded in
the approved roadmap and are expanded when they begin.

### Phase 0 — Baseline & Safety · W 14 · COMPLETE · 100%

| # | Sub-phase | Status |
|---|---|---|
| 0.1 | Inventory & tracker scaffold | COMPLETE |
| 0.2 | Baseline verification run | COMPLETE |
| 0.3 | Frozen-surface list + dirty-tree quarantine | COMPLETE |
| 0.4 | Visual baseline capture strategy | COMPLETE |
| 0.5 | Regression criteria | COMPLETE |

**Dependencies:** none. **Blocked by:** none. **Started/Completed:** 2026-08-06.

**Objectives** — establish an auditable pre-redesign state: what exists, what is green, what must
never change, and what "regression" will mean for the next 57 phases.

**Completed work**
* Catalogued **206 screens**: 88 web routes, 27 admin routes, 91 Flutter pages, each mapped to its
  owning phase and seeded `Legacy`.
* Ran all four verification suites and recorded the numbers Phase 48 must meet.
* Corrected two stale documentation claims found by measuring rather than trusting: the backend
  baseline (1297 → **1433**) and the Flutter analyze state (62 infos → **clean**).
* Wrote `docs/ui-ux/do-not-change.md` — 9 categories of frozen surface.
* Quarantined the 10 pre-existing dirty files present at `c66ae16`.
* Wrote `docs/ui-ux/regression-criteria.md` — build, test, functional, state, responsive,
  accessibility and visual-consistency gates plus an escalation table.
* Resolved the four planning-stage decisions and recorded them as **D-285**.
* Created this tracker and its JSON mirror.

**Files changed** — `UI_REDESIGN_PROGRESS.md`, `UI_REDESIGN_PROGRESS.json`,
`docs/ui-ux/inventory-{web,admin,mobile}.md`, `docs/ui-ux/do-not-change.md`,
`docs/ui-ux/regression-criteria.md`, `docs/DECISIONS.md` (D-285). **No application code changed.**

**Verification results** — see the Verification Ledger. All four suites green; nothing in this phase
could regress them since no application file was touched.

**Completion gate** — 1 ✅ · 2 n/a · 3 n/a · 4 ✅ · 5 ✅ · 6 n/a · 7 n/a (criteria defined) · 8 n/a ·
9 ✅ none · 10 ✅.

**Commit** — `a63156b`

**Next action** — Phase 1.1.

### Phase 1 — Product & UX Audit · W 14 · COMPLETE · 100%

| # | Sub-phase | Status |
|---|---|---|
| 1.1 | Web app surface audit | COMPLETE |
| 1.2 | Web host surface audit | COMPLETE |
| 1.3 | Web public surface audit | COMPLETE |
| 1.4 | Admin audit | COMPLETE |
| 1.5 | Mobile audit | COMPLETE |
| 1.6 | Severity ranking | COMPLETE |

**Dependencies:** Phase 0. **Blocked by:** none. **Started/Completed:** 2026-08-06.

**Objectives** — record the friction the redesign must fix, ranked by severity × user impact.
Output: `docs/ui-ux/audit-findings.md`. Every finding is a reproducible measurement (commands in
its §8), not an impression.

**Findings: 5 × S1 · 7 × S2 · 6 × S3 · 5 × S4.**

**The five S1s, and why they are S1**

* **S1-1** `aria-describedby` and `aria-invalid` appear **0 times** in the entire web+admin
  codebase. Every validation error in Kurx is communicated by colour alone. Root cause is one
  file — `packages/ui/src/field.tsx` renders the error as an unlinked `<p>` and `Input` uses its
  `error` prop only to recolour a border.
* **S1-5** — *found while verifying S1-1, and it changes the fix order.* `web/components/ui/`
  contains **8 forked primitives**, not the re-export shims D-185 describes. The forked
  `field.tsx` is imported by **5 of the 6 web authentication components** (`password-field`,
  `recovery-panel`, `onboarding-form`, `reset-password-panel`, `registration-flow`) plus
  `/discover`. Fixing the shared `Field` would therefore have repaired every form **except
  sign-in, registration, recovery and password reset** — and looked complete while doing it. The
  forks must collapse first. Five of the eight have zero importers; `sheet.tsx` is a stale older
  API that would silently hand a future caller a Sheet missing the admin workspace variant.
* **S1-2** Neither `Dialog` nor `Sheet` traps focus, restores focus on close, or locks body scroll,
  while both assert `aria-modal="true"`. `ConfirmDialog` composes `Dialog`, so every destructive
  confirmation in web and admin inherits it.
* **S1-3** `Sheet` sets `aria-label` only when `title` is a string; the admin workspaces pass a
  `ReactNode`, so those dialogs render **unnamed** to assistive tech.
* **S1-4** `toast.tsx` has no `aria-live` / `role="status"` — the repo-mandated feedback channel is
  silent to screen readers — and auto-dismisses at a fixed 3200 ms with no dismiss control
  (WCAG 2.2.1).

**Notable S2s** — 8 `loading.tsx` for 88 web routes and exactly 1 `error.tsx`; 7 host screens using
raw `<table>` against an explicit convention; typography that is **88% `text-sm`/`text-xs` with 5
uses of `text-base`**, so the product has no body copy; 5 radius and 15+ padding values with no
scale; 24 hardcoded `Color(0x…)` on mobile that will not follow the Phase 32 theme; and zero
`prefers-reduced-motion` support on any surface.

**What the audit found to be already good** (and must not be "fixed"): colour-token adoption on
web/admin is near-total (2 stray palette classes in ~1,300 uses); semantics are real HTML (3
`<div onClick>` in the whole surface); admin correctly consumes the shared Tailwind preset; and
Flutter's token layer is **ahead** of web's — Phase 4 should copy its structure, not the reverse.

**Fix-order consequence** — ranks 0–3 of the ranked table are **11 files** and remove every S1.
Rank 0 (collapse the forks) is a hard prerequisite for rank 1.

**Files changed** — `docs/ui-ux/audit-findings.md` (new). **No application code changed.**

**Verification** — no code touched, so no suite can regress. All findings re-verified by direct
file reads before recording (the S1-5 fork diff corrected an assumption made during planning, when
`web/components/ui/*` was reported as uniformly shimmed).

**Completion gate** — 1 ✅ · 2–3 n/a · 4 ✅ · 5 ✅ · 6–8 n/a · 9 ✅ none · 10 ✅.

**Commit** — `605c905`

**Next action** — Phase 2.

### Phase 2 — Information Architecture · W 6 · COMPLETE · 100%

| # | Sub-phase | Status |
|---|---|---|
| 2.1 | Web IA | COMPLETE |
| 2.2 | Admin IA (feeds Phase 26) | COMPLETE |
| 2.3 | Mobile IA | COMPLETE |
| 2.4 | Taxonomy & terminology reconciliation | COMPLETE |

**Dependencies:** Phase 1. **Blocked by:** none. **Started/Completed:** 2026-08-06.

**Objectives** — settle primary/secondary/mobile/admin navigation, page hierarchy, event taxonomy,
search, account, host and reviewer structure once, so no later phase re-decides structure.
Output: `docs/ui-ux/information-architecture.md`.

**Method** — a static link analysis over all 88 web routes: every `href` / `redirect` /
`router.push` in `web/app`, `web/components`, `web/lib`, with dynamic segments normalised, diffed
against the declared route set in both directions.

**What it found**

* **3 confirmed broken internal links** → `REG-001`–`REG-003`. Two are S1 and both sit at the worst
  moment of a primary journey: account recovery pushes to a non-existent `/account/security`, and
  every invitation→event link points at `/events/{slug}` when web serves `/e/[slug]`.
* **18 routes with no navigation path.** `/host` and 10 org-wide `/host/*` pages were superseded by
  the per-event workspace but never removed; 4 `/host/admin/*` were orphaned by D-195; 3
  `/host/templates/*` are placeholders; `/onboarding` is dead — both `session.ts:49` and
  `otp-panel.tsx:40` route to `/register` instead.
* **One false positive, verified and dismissed:** the 12 event-workspace tabs looked orphaned but
  are built dynamically from live capabilities in `host/events/[id]/layout.tsx`. `/i/[token]` is
  likewise expected to be unlinked — invite links arrive by email.

**Scope corrections this produced**

* **Phase 21 is smaller than its route count.** 17 of its 37 routes are orphans or placeholders.
* **Phase 17 (Onboarding) has no live surface.** The first-run experience is `/register`;
  `/onboarding` is dead code. Phase 17 is re-scoped to `/register` and its steps, and the roadmap's
  instruction to use only flows that actually exist is what caught this.

**Decisions recorded** — five-area primary nav (Home · Community · Posts · Messages · Workspace)
kept as-is on both web and mobile, since the two surfaces already agree and that alignment is the
redesign's strongest existing asset; Workspace absorbs hosting rather than adding a sixth area;
capability-driven event tabs preserved exactly, because hardcoding them would grant navigation the
backend denies.

**Files changed** — `docs/ui-ux/information-architecture.md` (new). **No application code changed;
no route renamed or removed.**

**Verification** — no code touched. The two directions of the link analysis were re-verified by
direct file read before recording, which is what caught the tab false positive.

**Completion gate** — 1 ✅ · 2–3 n/a · 4 ✅ · 5 ✅ · 6–8 n/a · 9 ✅ 3 pre-existing logged · 10 ✅.

**Commit** — `10f771f`

**Next action** — Phase 3.

### Phase 3 — Kurx Visual Identity · W 12 · COMPLETE · 100%

| # | Sub-phase | Status |
|---|---|---|
| 3.1 | Direction document | COMPLETE |
| 3.2 | `D-286` superseding D-065 | COMPLETE |
| 3.3 | Light/dark strategy | COMPLETE |

**Dependencies:** Phase 2. **Blocked by:** none. **Started/Completed:** 2026-08-06.

**Objectives** — establish a distinctive Kurx direction and resolve the two-register split D-065
created, which Phases 4, 32, 41 and 50 all depend on. Output: `docs/ui-ux/visual-identity.md`
and **D-286**.

**The reframing that drove it.** D-065 justified the warm palette as *"attendee-first (concerts,
festivals)"*. The domain model says otherwise — certificates, competitions, team formation,
speakers, sponsors, institutional verification, KYC, membership claims, professional journey,
allies, attendance provenance. That is **college fests, hackathons and conferences in India**, and
the reason a user returns is that Kurx holds *the record of what they showed up for*. The direction
is built on credentialled participation, not nightlife.

**The measurement that changed the decision.** Both candidate palettes were checked against WCAG
2.1 before either was adopted, and **both failed in load-bearing places**:

* **`#FFFFFF` on ember `#F0762B` = 2.86:1.** That is `filledButtonTheme` — **every primary CTA in
  the mobile app**, Register and Book included. A conversion-path control has been below the
  contrast floor since D-065 shipped.
* Ember text on cream = 2.80:1 (every mobile text button on light); mobile light `muted` 4.20:1;
  mobile dark `teal` 4.37:1; web light `accent` 4.42:1.
* **Borders fail 3:1 on all four palettes** (1.30–1.55:1), which matters wherever a border is what
  identifies a control — `field.tsx` draws inputs with `border-border`.

So D-286 is *not* "adopt mobile's palette" — that would have shipped a known-failing primary button
to three surfaces instead of one. It is **adopt the warm direction, corrected to pass**.

**The correction keeps the brand vivid.** Darkening ember until white text passes needs `#C5540E`, a
muddy brick. Measurement gave a better answer: **`#1A1A1A` ink on `#F0762B` is 6.08:1**, so the
primary button keeps full vivid ember and takes a **warm-ink label**. That fixes the S1 failure and
is the more distinctive choice — white-on-saturated is the SaaS default; ink-on-ember reads like a
wristband.

**Structural decisions recorded** — *ember acts, teal attests* (provenance gets its own hue, and
`success` stays separate); `border` splits into decorative vs interactive because one token cannot
satisfy WCAG 1.4.11 and card chrome at once; `warning` becomes a real token; three densities from
one token set, which is how admin gets Phase 26's density without a second design system; Plus
Jakarta Sans unified across all three surfaces; and a body text size, which the product currently
does not have.

**Files changed** — `docs/ui-ux/visual-identity.md` (new), `docs/DECISIONS.md` (D-286).
**No application code changed.**

**Verification** — every ratio is reproducible from the script in the direction doc's §3. No code
touched, so no suite can regress.

**Completion gate** — 1 ✅ · 2–3 n/a · 4 ✅ · 5 ✅ · 6 n/a · 7 ✅ contrast measured · 8 n/a ·
9 ✅ none new · 10 ✅.

**Commit** — `f386033`

**Next action** — Phase 4.0.

### Phase 4 — Design Tokens · W 20 · COMPLETE · 100%

| # | Sub-phase | Status |
|---|---|---|
| 4.0 | De-duplicate the token layer | COMPLETE |
| 4.1 | Colour + semantic colour | COMPLETE |
| 4.2 | Typography scale | COMPLETE |
| 4.3 | Spacing / sizing / radius / border | COMPLETE |
| 4.4 | Shadow / opacity / z-index | COMPLETE |
| 4.5 | Motion + breakpoints | COMPLETE |
| 4.6 | Flutter parity map | COMPLETE |

**Dependencies:** Phase 3. **Blocked by:** none. **Started/Completed:** 2026-08-06.

**4.0 — de-duplication, zero visual change.** The nine colour tokens existed in four places; admin
consumed the shared preset correctly while web inlined a copy of the preset, a copy of `tokens.css`,
*and* a third hardcoded hex copy in `lib/design-tokens.ts`. Web now consumes the preset and imports
`@kurx/ui/styles/tokens.css`, matching admin.

`themeHex` could not simply be deleted — `icon.tsx`, `opengraph-image.tsx`, `manifest.ts` and
`viewport.themeColor` render in Satori or serialise at build time, where no stylesheet exists. So
the palette necessarily survives in two forms. Rather than leave that pair to drift like the last
one, `packages/ui/src/tokens.ts` is the JS view and a test asserts it matches `tokens.css`.

Verified as a genuine no-op **by inspecting the built stylesheet**, not by trusting a green build: a
passing `next build` would not catch a preset that failed to load, because Tailwind silently drops
unknown classes. The emitted CSS still contained every token utility and both themes' custom
properties.

**4.1–4.5 — the scales that did not exist.** Before this, web and admin had nine colour tokens and
**nothing else**. Added: a 9-step semantic type scale (including `body`, which the product did not
have), the ticket-corner radius scale, `KSpace`-mirrored spacing, warm-tinted shadows, motion
durations + one easing curve, a named z-index scale, and two breakpoints. **Additive by design** —
Tailwind's numeric spacing and default breakpoints are untouched, because ~1,300 existing utilities
depend on them and redefining them would be a silent repo-wide layout change.

Four colour tokens were added: `border-strong`, `on-accent`, `accent-text`, `warning`.

**The measurement is now enforced, not documented.** Every foreground was re-solved against **all
three surface steps** — several first-draft values failed on `elevated` while passing on
`background` — and `web/test/design-tokens.test.ts` re-measures every pair on every run. It also
asserts that white-on-ember still fails, so the D-065 regression cannot return, and that `warning`
stays hue-separated from `accent` (the first passing candidate came out 3° from ember — a different
value carrying the same signal).

`tokens.css` also carries a global `prefers-reduced-motion` block, so reduced motion applies to
every web component immediately, including ones not yet redesigned.

**4.6 — Flutter parity.** `docs/ui-ux/token-map.md` binds the two systems and names the three
corrections Phase 32 must make, in priority order. The first is that mobile's `onAccent` must become
ink: `filledButtonTheme` puts white on ember at 2.86:1, so every primary CTA in the app is currently
failing AA. The second is that `KurxColors.light.success` is byte-identical to `teal` — the exact
dilution D-286 forbids.

**Cross-check.** The `ui-ux-pro-max` database independently corroborated the warm direction, the
Event/Conference pattern, and S1-1 (*"red border only"* is its named anti-pattern, High severity).
Its palette values were rejected — they fail its own checklist — and its `MASTER.md` was
deliberately not persisted, which would have created a second source of truth for the palette. It
did earn one real change: **D-287**, replacing Plus Jakarta Sans with **Anek**, because Plus Jakarta
Sans has no Devanagari and `hi.json` is a shipped locale.

**Files changed** — `packages/ui/{src/tokens.ts,src/index.ts,src/styles/tokens.css,tailwind-preset.cjs}`,
`web/{tailwind.config.ts,app/globals.css,app/layout.tsx,lib/design-tokens.ts,test/design-tokens.test.ts}`,
`docs/ui-ux/{token-map.md,visual-identity.md,audit-findings.md}`, `docs/DECISIONS.md` (D-287).

**Verification** — web: typecheck clean, lint unchanged (1 pre-existing warning), **159 tests pass**
(145 baseline + 14 token guards), build clean at 65 static pages. admin: typecheck, lint, build
clean at 29 pages, and its CSS confirmed to carry the new ember — proving both surfaces genuinely
share one preset. Tailwind config resolved directly to confirm every new scale is registered and
that Tailwind's own defaults survive.

**Completion gate** — 1 ✅ · 2 n/a · 3 n/a · 4 ✅ · 5 ✅ · 6 n/a · 7 ✅ every pair measured and
enforced · 8 n/a · 9 ✅ none new · 10 ✅.

**Commit** — `40311cb`

**Next action** — Phase 5.1.

### Phase 5 — Accessibility Foundation · W 10 · COMPLETE · 100%

| # | Sub-phase | Status |
|---|---|---|
| 5.1 | Contrast budget vs the new palette | COMPLETE |
| 5.2 | Focus / keyboard contract | COMPLETE |
| 5.3 | Screen-reader semantics + touch targets | COMPLETE |
| 5.4 | Reduced-motion behaviour | COMPLETE |
| 5.5 | Extend `.claude/reviews/accessibility-review.md` | COMPLETE |

**Dependencies:** Phase 4. **Blocked by:** none. **Started/Completed:** 2026-08-06.

**Objectives** — write the contracts **before** Phases 6–10 build the components, so accessibility
is a property of the design system rather than a later patch. Output:
`docs/ui-ux/accessibility-foundation.md`.

**What it specifies** — the token-to-purpose mapping (and the rule most likely to be got wrong:
control boundaries use `border-strong`, not `border`); the six focus-management events including
trap and restore; per-pattern keyboard maps for eleven patterns; the naming preference order, with
`aria-labelledby` preferred precisely because it survives a `ReactNode` title, which is how S1-3
happens; the exact `Field` aria contract that closes S1-1; live-region rules; landmark and heading
rules; touch-target floors; and an **acceptance bar table** — ten components, each with the row it
must pass before it can be called done.

**Two things it does not merely assert**

* Contrast is already enforced by `web/test/design-tokens.test.ts` (Phase 4), so §1 is a usage
  guide, not a promise.
* Reduced motion already ships for web via the global block in `tokens.css`, so every component
  inherits it — including ones not yet redesigned. Flutter has no equivalent and Phase 32 owns it.

**Gap recorded** — no skip-to-content link exists on any page today; Phase 11 adds it.

**Files changed** — `docs/ui-ux/accessibility-foundation.md` (new),
`.claude/reviews/accessibility-review.md` (9 new pass/fail criteria, drawn from the measured
findings rather than from a generic checklist). **No application code changed.**

**Verification** — no code touched. The review gate now fails a component that ships the S1
patterns, which is the point: the criteria name the specific defects found in this codebase
(`border` on a control, white on `accent`, `aria-modal` without a trap, `aria-label` on a
`ReactNode` title) rather than restating WCAG in the abstract.

**Completion gate** — 1 ✅ · 2–3 n/a · 4 ✅ · 5 ✅ · 6 n/a · 7 ✅ this phase *is* the a11y work ·
8 n/a · 9 ✅ none new · 10 ✅.

**Commit** — `7b2256f`

**Next action** — Phase 6.6.

### Phase 6 — Core UI Primitives · W 42 · IN PROGRESS · 33%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 6.6 | Collapse the 8 forked primitives (**prerequisite**) | COMPLETE | `1c6b462` |
| 6.1 | Button · IconButton · Link | COMPLETE | `cd85864` |
| 6.2 | Input · Textarea · Select · Checkbox · Radio · Switch · Slider | COMPLETE | `c49272d` |
| 6.3 | Date / time controls | COMPLETE | `74d54ea` |
| 6.4 | Badge · Chip · Avatar · Divider | COMPLETE | `74d54ea` |
| 6.5 | Tooltip · Spinner · Progress · Skeleton | COMPLETE | `74d54ea` |

**Dependencies:** Phase 5. **Blocked by:** none. **Completed:** 2026-08-06.

**Seven primitives created** that did not exist: IconButton, Link, Checkbox, Radio, Slider,
Divider, Tooltip, Progress, DateTimeField.

**Five measured accessibility defects fixed in existing primitives**

| Defect | Was | Now |
|---|---|---|
| Button primary label | white on ember, **2.86:1** | ink, **6.08:1** |
| Input / Select / Textarea boundary | `border`, **1.30:1** | `border-strong`, **3.57:1** |
| Switch knob on ember | white, **2.80:1** | ink, **6.08:1** |
| Badge tinted tones | **3.61–4.62:1** across both themes | tint removed; label on `elevated` |
| Spinner / Skeleton | silent to screen readers | `role="status"`, `aria-busy` |

`Badge` was the surprise: the 15% tint eats exactly the headroom Phase 4 solved for, so *every*
tone failed AA in *both* themes — and still failed at 10%. Removing the tint was cheaper and more
legible than minting five more tokens.

**Verification** — 188 web tests (145 baseline + 43 new primitive guards), typecheck/lint/build
clean on both surfaces.

**Finding promoted during 6.1** — the S1 white-on-ember failure was recorded in Phase 3 as a Flutter
defect (`filledButtonTheme`). It was **also present on web**: `packages/ui/src/button.tsx` shipped
`primary: bg-accent text-white`. So every primary CTA on **all three surfaces** was below the AA
floor, not just mobile's. Fixed in `cd85864`; the audit's S1 count stands, but its blast radius was
larger than recorded.

**Next action** — 6.2.

### Phase 18A — Public Profile & Professional Identity · W 32 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 18A.0 | `Tabs` ARIA pattern + web toast provider (**prerequisite**) | COMPLETE | *(this commit)* |
| 18A.1 | `/u/[username]` — header, identity, layout | COMPLETE | *(this commit)* |
| 18A.2 | Professional identity — provenance, metrics, shared history | COMPLETE | *(this commit)* |
| 18A.3 | Profile sections — tabs, timeline, events, orgs, certificates, allies | COMPLETE | *(this commit)* |
| 18A.4 | `/allies` — shell, manager, people search, suggestions | COMPLETE | *(this commit)* |
| 18A.5 | Action controls — connect, message, follow | COMPLETE | *(this commit)* |
| 18A.6 | `/o/[slug]` — org public profile | COMPLETE | *(this commit)* |

**Dependencies:** Phases 6–11, 18. **Blocked by:** none. **Completed:** 2026-08-07.

**Two defects found in supposedly-finished foundation work**, both of which had to be fixed before
any 18A screen could be done properly:

| Defect | Detail |
|---|---|
| `Tabs` declared a role it did not implement | `role="tablist"` + `role="tab"` with **no** `aria-controls`, no panel association, no arrow-key navigation, and every tab at `tabIndex` 0 — the exact opposite of the required roving tabindex. The role is a contract: AT announces "tab, 1 of 6" and the user reaches for the arrows. Nothing happened. Now the full APG pattern, plus a focus ring it never had and a 44px target. Also reaches admin's two `Tabs` call sites. |
| **Web never mounted `ToastProvider`** | Phase 8 built the toast system to close S1-4 and recorded its reach as "every row-action outcome in web **+ admin**". Only admin's shell ever mounted the provider. On web `useToast()` resolved to the context's no-op default, so the entire feedback channel was inert on 88 routes. Mounted at the root rather than in `AppShell`, because `/u`, `/o` and `/e` render outside that shell. |

**Screen and component defects fixed**

| Defect | Surface | Detail |
|---|---|---|
| A failed action looked like a successful one | `allies-manager` | Accept/decline/cancel/remove ran the server call bare inside the transition and edited the list unconditionally. Declining while offline still removed the card; a rejected accept still added an ally. Now the list changes only after the call resolves, and rejections surface. |
| A destructive action had no gate | `allies-manager` | "Remove" severed an ally irreversibly — re-connecting needs a fresh request the other person accepts — on one unguarded click. Now behind `ConfirmDialog`. |
| A signed-in route rendered outside every shell | `/allies` | The only auth-gated web route outside `(app)`. It called `requireSession()` under the bare root layout, so **the app nav's own "Community" item navigated the user out of the navigation** — no nav, no skip link, no way back but the browser. Moved into `(app)`; its own `<main>` dropped with the move, since `AppShell` supplies the landmark (the Phase 11 nested-landmark trap, avoided this time). |
| A search input with no accessible name | `people-search` | Placeholder only — announced as an unlabelled edit field, and the placeholder vanished on first keystroke. Results replaced the region silently. A query under 2 characters did nothing at all, with no explanation. Rebuilt onto `SearchBar`, which carries the label, the live region and the 44px/`border-strong` contract. |
| Controls named for their state, not their action | `ally-connect-button` | "Requested" *cancels*; "Ally ✓" *removes*. Both accessible names said the opposite of what activating them does, the second via a literal `✓` that screen readers read as "check mark" or drop. Visible labels keep showing state; the names now state the consequence. |
| A rejected toggle read as a completed one | `follow-button` | Assigned straight from the awaited action with nothing around it, so a rejection left the transition unhandled and the button unchanged — the Phase 18 silent-revert shape again. |
| Errors announced to nobody | `ally-connect-button`, `message-button` | Red captions with no live role. Added `role="alert"`. |
| A trust explanation reachable only by pointer | `provenance-badge` | The hint lived in `title` on a non-focusable span, so the mechanism that distinguishes proof from claim (D-221) was invisible to keyboard and touch. Now real text for AT plus a hover bubble for pointers. |
| A link that went nowhere | `profile-sections` | An ally with no username rendered `href="#"` — focusable, announced as a link, navigating nowhere. The REG-004 pattern; now plain text. |
| A 500 rendered as "does not exist" | `/o/[slug]` | `.catch(() => null) → notFound()` — the same defect D-235 fixed on `/u/[username]`, still live here. Now classified through `section()`: 404 is absent, everything else reaches the error boundary. |
| 15 sites below AA in the light theme | across 18A | `text-accent` is **2.80:1** on light and is documented in `tokens.css` as a fill, not a text colour. Replaced with `text-accent-text` (`#B54C0D`) wherever it carried text. |
| Layout tracks unrelated to their content | `/u/[username]` | Five cards poured into `lg:grid-cols-[1fr_320px]` by auto-flow, so a paragraph of prose landed in the 320px track while a row of chips took the 1fr one. Now two explicit columns. Stat tiles step 2 → 3 → 5 instead of cramming five into `sm`. |

**Guards proven by breaking them** — reverting the `allies-manager` failure handling reds
"keeps the request in place when the server rejects the accept"; removing the roving tabindex and
key handler reds three `Tabs` tests. Both restored and re-verified green.

**Verification** — web **286 tests** (265 → +21), 65 static pages, typecheck/lint clean; admin
typecheck/lint clean, 29 static pages (the `Tabs` change reaches two admin workspaces). Backend
untouched — no contract was involved.

**Deliberately not done** — `/o/[slug]` states an event count with no way to see the events. The API
supports `orgId` on event search, but the only surface rendering it (`/discover`) sits behind
`requireSession()` and does not read the parameter, so a public org page cannot link there without
sending anonymous visitors to a login wall. Building a public events-by-organization view is product
scope. Logged as **REG-006**.

**Next action** — Phase 19.

### Phase 19 — Ticketing / Registration 💰 · W 30 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 19.1 | `/tickets` — real ticket QR, order status, outage handling | COMPLETE | *(this commit)* |
| 19.2 | `/tickets/refunds` — status tones, outage handling, empty state | COMPLETE | *(this commit)* |
| 19.3 | `/book/[slug]` — remove the false-success checkout (REG-009) | COMPLETE | *(this commit)* |
| 19.4 | `EmptyState` string-icon leak; `orderStatusOf`; QR route handler | COMPLETE | *(this commit)* |

**Dependencies:** Phase 15, 18A. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 18A commit:** `2934ffe` (recorded here, not by amending the commit it describes).

**The headline finding — web's checkout could not take a payment, and reported that it had.**

`/e/[slug]`'s primary CTA led to `/book/[slug]` → `BookingForm`, which opened Razorpay with **no
`order_id` and no amount**, discarded the payment response without verifying anything, and navigated
to `/tickets` regardless of the outcome — including when the Razorpay script never loaded. Clicking
"Continue to Razorpay" with the script blocked landed the user on "My Tickets" as though they had
bought something. `web/lib/api.ts` has **no order-creation function**, so nothing was ever sent.

The backend has the whole contract — `POST /v1/events/{eventId}/orders` with `Idempotency-Key`,
guest checkout (D-036), group join codes, competition invitations — and **Flutter implements it
correctly**. Web simply never did.

Building it here is feature work on the money path, needing its own `D-NNN` and a security review;
a redesign phase does not invent a payment integration. **Decision taken with the user: neutralise
the false success.** `BookingForm` is deleted, `/book/[slug]` is now an honest hand-off that shows
the event and points at the client where booking works, and both event-page CTAs are relabelled
"Book in the app" so the label matches the destination. Recorded as **REG-009**.

**Other defects fixed**

| Defect | Surface | Detail |
|---|---|---|
| The gate code was never shown | `/tickets` | A 48px lucide `QrCode` glyph was drawn once per *order*, while the real `code` on every ticket in `order.tickets` was never rendered at all. A buyer with three tickets saw one picture of a QR and could produce none of them at a gate. `GET /v1/tickets/{code}/qr.png` exists for this and its own comment says it "replaces the decorative/mock QR the web and mobile clients render" — Flutter adopted it, web never did. Now streamed through `/api/ticket-qr/{code}`, a server route, because the upstream endpoint needs a bearer token an `<img>` cannot send. The code is also shown as text: a gate can be opened by typing it, so the failure mode still admits the holder. |
| An outage read as "you have no tickets" | `/tickets`, `/tickets/refunds` | `.catch(() => [])` — the D-235 defect, in the worst place in the product. A 500 or a dropped connection told somebody who had paid, possibly standing at a gate, that they owned nothing. Both now classify the outcome and say the failure is ours. |
| Machine statuses shown to buyers | `/tickets` | `order.status` was printed through `capitalize`, so a buyer read "Awaitingpayment". `lib/order-status.ts` maps them to words with a tone that never carries the meaning alone, and shows an unrecognised value as-is rather than guessing — mapping an unknown status onto "Paid" would be a claim about somebody's money. |
| A Material icon name rendered as text | `EmptyState`, 7 call sites | `icon` is typed `ReactNode`, which accepts a string, so `icon="receipt_long"` / `"how_to_reg"` / `"forum"` / `"article"` / `"chat"` / `"drafts"` printed the identifier above the heading, from a font this project does not load. TypeScript was happy. `EmptyState` now drops a bare string; `/tickets/refunds` gets a real icon. The other six render no icon — strictly better than an identifier — until their own phases reach them. |
| Settled and in-flight states looked identical | `/tickets/refunds` | "Refunded" and "Processing" both rendered `text-accent`, which is 2.80:1 on light *and* gave two different outcomes one appearance. Now `success` and `warning`. |

**Verification** — web **292 tests** (286 → +6), 65 static pages, typecheck/lint clean. Backend
untouched: the QR and order endpoints already existed and no contract changed.

**Next action** — Phase 20.

### Phase 20 — User Events & Tickets · W 12 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 20.1 | `/workspace` — counts, views, event rows, REG-003 | COMPLETE | *(this commit)* |
| 20.2 | `/workspace/[eventId]` — participant launcher | COMPLETE | *(this commit)* |

**Dependencies:** Phase 11, 19. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 19 commit:** `72a4d85`.

| Defect | Detail |
|---|---|
| **REG-003 closed** | The Templates tile linked to `/host/templates`, which has no page — the link 404'd on click. Removed rather than repointed: the only things under it are three `WorkflowPage` placeholders, and sending a host to one of those is not better than not offering the tile. Building the index is Phase 21's call. |
| A count measured from a page, not from the total | "Hosting" was `hosted.length` over a 50-row page while the endpoint returns `total` — anybody past 50 was told a smaller number and called it their total. The D-231 rule, in a new place. The list itself was silently truncated too; it now says so. |
| An outage read as "No events yet. Create one." | Both reads were `.catch(() => [])`, so a failure told a host who runs twenty events that they run none (D-235). Counts render an em dash rather than a zero they cannot substantiate. |
| The current view had no programmatic marking | Four view links distinguished only by colour and a border; `aria-current="page"` was absent, so a screen-reader user could not tell which was active. |
| Raw lifecycle statuses reached hosts | "Opens after approval · pendingreview". `lib/event-status.ts` maps them, and shows an unrecognised value as-is rather than guessing. |
| Rows hid data they had already fetched | Each row carried a date, city, venue and counts, and showed only a title and a raw status. Date, city and representation now appear, with the status as a `Badge`. |
| A local `Stat` shadowing the system's | The file hand-rolled its own `Stat` while `@kurx/ui` exports one. |

**Known limit, not fixed** — `GET /v1/me/participations` returns no event title, only an id and a
role, so each participation row is titled by role. `/v1/orders` denormalises `event_title` for
exactly this reason; until this endpoint does the same the only alternative is a read per row, which
is not worth an N+1 on every workspace load. Logged as deferred.

**Verification** — web **295 tests** (292 → +3), 65 static pages, typecheck/lint clean.

**Next action** — Phase 20A.

### Phase 20A — Posts & Social Feed · W 16 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 20A.1 | `PostCard` — counts, toggle state, dead links, verification badge | COMPLETE | *(this commit)* |
| 20A.2 | `PostFeed` — failure reporting, load-more announcement, empty state | COMPLETE | *(this commit)* |

**Dependencies:** Phase 10, 18A. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 20 commit:** `b5a5c9a`.

These two components render all seven `/posts/**` routes, so each defect below was multiplied by the
length of every feed in the product.

| Defect | Detail |
|---|---|
| **Every count was announced to nobody** | `aria-label` *replaces* an element's content, so `aria-label="Like"` wrapped around a visible "42" meant the number never reached assistive tech — like, comment, share and save counts on every post, silent, while being the main thing those controls communicate. The count is in the name now, and omitted when zero rather than announced as "0". |
| Toggles with no state | Like and Save carried "already done" in `fill-accent` and hue alone. `aria-pressed` is what makes that perceivable; a plain Share button deliberately does not claim one. |
| Two dead links per post | An author with no username rendered `href="#"` on both the avatar and the name — focusable, link-announced, navigating nowhere, on the most-rendered component in the product. The REG-004 pattern at feed scale. |
| Verification as a bare "✓" | A literal check mark in a `<span>` carrying `aria-label`. A span has no role, so several screen readers expose no name at all and read the glyph as "check mark" or drop it. `text-accent` was 2.80:1 on light besides. Now `Badge tone="accent"`. |
| Three optimistic actions reverted in silence | Like, save and delete each caught and rolled back with no message, so a like that never landed was indistinguishable from a double-tap. Delete was worse: the rollback `prependPost`s, so a failed delete put the post back at the *top* of the feed and it read as a new one. This is the Phase 18 silent-revert shape, on the most-used controls in the product. |
| "Load more" read as inert | New posts arrived below the button with no live region, and a failed page silently re-enabled the button. Both are announced now. |
| Sub-44px action targets | `px-2.5 py-1.5 text-xs` around a 16px icon is ~28px against the 44px floor Phase 6 set, with no focus ring. |
| A locale-unstable date | `new Date(...).toLocaleDateString()` on the event attachment, where every other surface uses `formatDate(..., "en-IN")`. |
| Visibility announced twice | Once in `sr-only` text and again in the header line that already states it visibly. |

**Verification** — web **305 tests** (295 → +10), 65 static pages, typecheck/lint clean.

**Next action** — Phase 20B.

### Phase 20B — Messaging & Chat · W 14 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 20B.1 | Phantom colour tokens across 8 files, and the guard that keeps them out | COMPLETE | *(this commit)* |
| 20B.2 | `MessageBubble` — delivery state, locale, recovery controls | COMPLETE | *(this commit)* |
| 20B.3 | Chat empty states — real icons | COMPLETE | *(this commit)* |

**Dependencies:** Phase 8, 20A. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 20A commit:** `1b80aa0`.

**The finding that outgrew the phase — 20 colour classes naming tokens that do not exist.**

Tailwind emits nothing for a class it cannot resolve, and nothing is exactly what it looked like.
Eight files used shadcn's vocabulary — `bg-card`, `text-fg`, `text-destructive`,
`text-muted-foreground` — none of which is in the Kurx preset. The consequences were not cosmetic:

* **Every incoming chat bubble had no background and no text colour**, so only the sender's own
  messages read as bubbles at all.
* **Every auth error message was not red** — `login-waiting`, `recovery-panel`, `passkey-sign-in`
  and `/verify/[code]` all styled failure with `text-destructive`. Phase 16 is 🔒 and closed; this
  survived it because nothing can catch it: it is not a type error, not a lint error, not a build
  error. Only a screenshot, or a test.

So there is now a test. `design-tokens.test.ts` walks `app/`, `components/` and `lib/` and fails on
any colour class outside the palette, naming the file. **Proven by reverting one class and watching
it go red.**

| Other defects | Detail |
|---|---|
| Delivery state was silent | Pinned / Sending / Not sent / Read / Sent were `aria-label` on a bare `<svg>`, which several screen readers do not expose — while the comment beside them claimed "the label carries it for screen readers". They needed `role="img"` to be exposed at all. |
| A hydration-unstable time | `toLocaleTimeString(undefined, …)` resolves to the server's locale on the server and the browser's on the client. |
| The recovery controls were the smallest on the screen | "Retry" and "Discard" — the only way to rescue an unsent message — sat at ~16px with no focus ring. |
| Material icon names in five empty states | The Phase 19 leak, in this phase's own files; now real icons. |

**Verification** — web **306 tests** (305 → +1, the sweep guard), 65 static pages, typecheck/lint
clean.

**Next action** — Phase 20C.

### Phases 20C + 20D — Certificates · Groups · Notifications · Invitations · W 22 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 20C.1 | `/certificates` — the owner's own credentials | COMPLETE | *(this commit)* |
| 20C.2 | `/groups` — tokens | COMPLETE | *(this commit)* |
| 20D.1 | `/verify/[code]` — the verdict, and REG-005's third instance | COMPLETE | *(this commit)* |
| 20D.2 | `/invitations`, `/i/[token]`, `/notifications` — outages, dates, tokens | COMPLETE | *(this commit)* |

**Dependencies:** Phases 18A, 19, 20B. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 20B commit:** `460ad1d`. **This closes milestone M4.**

| Defect | Surface | Detail |
|---|---|---|
| A user could not see their own private certificates | `/certificates` | The page read the caller's own credentials through the **public profile** endpoint with no token, so they arrived filtered by what the caller had chosen to publish — somebody keeping certificates private saw "No certificates yet" on their own page. The read is viewer-aware (D-229/C1); it now sends the viewer token. A user with no username got the same empty state for an entirely different reason, and now gets a way out of it. |
| Verification answered "fake" for an outage | `/verify/[code]` | `.catch(() => null) → notFound()` on the one page whose whole job is answering "is this credential real". A 500 or a dropped connection told somebody checking a certificate that no such certificate existed — worse than the same defect anywhere else it appeared. |
| The verdict was a colour | `/verify/[code]` | A green or red shield and nothing else. The answer is a sentence now; the icon is decorative. |
| **REG-005's third instance** | `/verify/[code]` | A 64px lucide `QrCode` glyph beside the deep link it was decorating — a picture of a QR, not one. Removed on the same grounds as the first two. |
| Outages rendered as emptiness | `/invitations`, `/notifications` | Notifications was the starkest: the fallback printed "You're all caught up." over a list that had failed to load. |
| Locale-unstable dates, sub-AA accent text | `/invitations`, `/i/[token]`, `/groups` | `toLocaleDateString()` with no locale, and `text-accent` carrying text at 2.80:1. |

**Verification** — web **306 tests**, 65 static pages, typecheck/lint clean.

**Next action** — Phase 21.

### Phase 21 — Host / Workspace Experience · W 44 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 21.1 | Event workspace shell — tab strip, header, overview | COMPLETE | `f202615` |
| 21.6 | The four `WorkflowPage` placeholder routes | COMPLETE | `f202615` |
| 21.7 | Cross-cutting sweeps — contrast, shims, the last lint warning | COMPLETE | `f202615` |
| 21.4 | Ticket types & registration forms | COMPLETE | *(this commit)* |
| 21.10 | Destructive-action sweep — 13 unguarded deletes | COMPLETE | *(this commit)* |
| 21.11 | Form controls — the `Field` gap across 21 files | COMPLETE | *(this commit)* |
| 21.3 | Attendees, registrations & check-in | COMPLETE | *(this commit)* |
| 21.5 | Announcements, invitations, certificates, team, analytics | COMPLETE | *(this commit)* |
| 21.9 | Representing & org finance | COMPLETE | *(this commit)* |
| 21.2 | Event workspace screens — details, schedule, people, media | COMPLETE | *(this commit)* |
| 21.8 | Payouts, wallet & KYC | COMPLETE — the route is a redirect into Finance | *(this commit)* |

**Dependencies:** Phases 11, 20. **Blocked by:** none. **Phase 20C/20D commit:** `0dd8d6a`.

**Scope note.** The host surface is 25 routes, but seven of the top-level ones
(`/host`, `/host/attendees`, `/host/forms`, `/host/payouts`, `/host/tickets`, `/host/invitations`,
`/host/certificates`, `/host/settings`) are six-line redirects into the event workspace under D-267 —
so the real screen count is far lower than the route count, which is why 21.8 closes without a
payouts screen of its own. `create-event-wizard.tsx` and `edit-event-form.tsx` are **not** in this
phase: the inventory assigns `/host/events/new` to Phase 22 and editing to Phase 23, and the six
unlinked hints found in the wizard are carried to 22.

| Done | Detail |
|---|---|
| **The workspace tab strip** | Up to seventeen destinations whose current one was marked by a hue and a bottom border and nothing else — no `aria-current`, so a screen-reader user working through seventeen links could not tell which they were on. Also unnamed as a landmark, 38px against the 44px floor, and with no focus ring. Kept as links rather than converted to the ARIA tabs pattern: each is its own route and must stay shareable, and `role="tab"` would promise arrow-key panel navigation this page does not have. |
| **The workspace header** | The status was uppercased raw, so a host awaiting a decision read "PENDINGREVIEW", on a line that was itself `text-accent` at 2.80:1. Now `eventStatusOf` in a `Badge`. |
| **The overview facts grid** | `capitalize` ran over every value indiscriminately — a timezone rendered "Asia/kolkata" and venues had their own capitalisation overwritten. Visibility is mapped at the source instead, with an unrecognised value shown as-is. |
| **Four routes shipped an engineering note to hosts** | `WorkflowPage` rendered one card per planned feature, each captioned *"Connected to backend APIs as they are exposed; frontend keeps validation and UI state only."* Cards in a grid are what every working area of this product uses, so the unbuilt areas read as built ones. Now a single honest panel: the area is unavailable, the planned scope is labelled as planned, and there is a way back. **Confirmed orphaned** — `/workspace` was their last inbound link and Phase 20 removed it — which is the evidence Phase 49 needs to delete them. |
| **29 sub-AA text sites** | `text-accent` is documented in `tokens.css` as a fill and measures 2.80:1 as text on light; it was carrying text across 12 host files. Swept, and now guarded. |
| **55 shim imports** | `@/components/ui/{card,button}` are re-export shims (D-185); 45 host files went through them rather than importing `@kurx/ui`. Collapsed and guarded. |
| **The last lint warning in the repo** | `module-picker` put `aria-disabled` on an `<li>`, which is not valid on the implicit `listitem` role and therefore did nothing — while the row already said "Not available for this kind of event" in words, which is what actually reaches a screen reader. `next lint` is now clean with **no warnings at all**. |
| **Three outage-as-fact defects** | Host notifications printed "You're all caught up." over a list that had failed to load. Readiness removed its whole "Before you publish" panel on a failed policy read — on a page called Readiness, an absent blockers section reads as *no blockers*, the most consequential wrong conclusion that screen can produce. Verification derived "Not ready yet — verify your identity and organization" from a representations list that had fallen back to `[]`, making an outage into a claim about the host's standing. |
| **A rating only a sighted user could read** | Review scores were `"★".repeat(n)`, which a screen reader reads as "black star black star black star". The number is the accessible answer now; the glyphs are the picture of it. |

**Guards proven by breaking them** — reverting one `text-accent` and re-adding one shim import turns
both sweep tests red, each naming the offending file.

**Verification** — web **315 tests** (306 → +9), 65 static pages, typecheck clean, **lint clean with
zero warnings**.

**Part 2 — the destructive-action sweep (21.4, 21.10)**

**Thirteen irreversible deletes across the host surface fired on a single click.** Every one was a
bare `<form action={deleteXAction}>` wrapping a `<Button type="submit">`: a ticket type that may
already have sales against it, a registration-form field, a queued announcement, an issued
certificate, a live invite link, a venue, an assignment, a session, a speaker, a sponsor, a media
item — and an entire draft event, with its schedule, ticket types and registration form.

`ConfirmDialog` exists for exactly this and had **one call site in all of web** before this pass.

`ConfirmSubmitButton` puts the gate in front without taking the submission over: confirming calls
`requestSubmit()` on the owning form, so the server action, its progressive enhancement and its
revalidation are untouched.

The draft-event delete was the worst of them and needed more than a gate — it was also unguarded, so
a refusal or a dropped connection left the button looking like it had worked, on the one action in
the host surface that cannot be undone. Every sibling transition beside it already surfaced its
refusals. **Its fix has a trap worth recording:** the action ends in `redirect("/workspace")`, and
Next implements redirect by *throwing* — so catching indiscriminately would have swallowed the
success path and reported a failure on every successful delete. The redirect digest is re-thrown
untouched.

Also on the tickets screen: the inline field editor's input had **no label at all** — only its column
position said what it was — on the decorative border token, at 32px; and five controls overrode the
button height down to 28–36px against the 44px floor.

**Guard proven by breaking it** — replacing one `ConfirmSubmitButton` with a plain submit reds the
sweep test, naming the file and line.

**Part 3 — form controls, the gate, representation (21.2, 21.3, 21.5, 21.8, 21.9, 21.11)**

**160 hand-rolled form controls across 21 host files, and `Field` used exactly zero times.**

Phase 7 closed audit S1-1 by making `Field` wire `aria-describedby`, `aria-invalid` and
`aria-required` onto its child, and its fix reached 86 call sites across 22 files. **None of them
were on the host surface**, which had built its own: the same class string —
`"h-10 w-full rounded-md border border-border bg-background px-3 text-sm text-text"` — copy-pasted
into **20 local constants across 17 files**. That is the S1-5 forked-primitive pattern that Phase 6.6
collapsed everywhere else, in a place nothing looked.

Three defects rode in on every copy: 40px against the 44px touch floor, the **decorative** border
token at 1.30:1 on a boundary WCAG 1.4.11 requires to clear 3:1, and no focus ring at all. The fix is
one source — `controlClass`, exported from `field.tsx` alongside the `Field`/`Input` path it should
be reached for first, following the `chipClass` precedent. Ten button height overrides down to
28–36px went with it.

`Field` also gained a `className`, because two call sites in two phases had now needed one and worked
around its absence.

| Other defects | Detail |
|---|---|
| **The gate announced nothing** | `CheckinPanel` drew its verdict in a paragraph that silently swapped colour. Someone working a door is looking at the person in front of them, not at the screen — and a screen-reader user got nothing at all. The result is now an **assertive** live region, the one place in this redesign where assertive is right: the next person is already stepping forward and "already checked in" has to arrive before they do. It is rendered unconditionally, or the first message would not be announced. Its success text was also a literal `"Checked in ✓"`, and an unguarded scan left the *previous* verdict on screen — which at a gate reads as a fresh answer for the person now standing there. |
| Two more outage-as-fact reads | The issued-certificate roster fell back to empty, which would send a host off to re-issue certificates their attendees already hold. The chat tab gave "this event has no room yet" for "we could not fetch your rooms". |
| A search whose results were silent | `/host/representing` is a server-rendered GET search; the region below simply changed on navigation with nothing said. |
| Two bare glyphs in link names | "Announcements →" and "Analytics →" — read as "rightwards arrow" or dropped; the card is already a link and the direction is not information. |
| A bare-ellipsis pending label | `claim-org-form`'s submit became `"…"` mid-action, which is punctuation, not a name. |
| Unlinked hints on the representation form | Both explanatory paragraphs on `create-org-form` — what counts as an organization, and what document to upload — were `<p>` siblings no control referenced, so a screen-reader user focused on either field was told nothing about what to put in it. Its error was a bare red paragraph. |

**Guards proven by breaking them** — re-introducing the forked class string reds the sweep, naming
the file. Three sweeps now cover the host surface: no control boundary on the decorative token, no
local copy of the control class, no button override through the 44px floor.

**Verification** — web **319 tests** (316 → +3), 65 static pages, typecheck clean, lint clean with
zero warnings.

**Next action** — Phase 22.

### Phase 22 — Event Creation · W 24 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 22.1 | The five single-select steps — real radio groups | COMPLETE | *(this commit)* |
| 22.2 | Step chrome — `FormSteps`, step announcement | COMPLETE | *(this commit)* |
| 22.3 | Blocked navigation — say why, not just refuse | COMPLETE | *(this commit)* |
| 22.4 | Field hints and character counters | COMPLETE | *(this commit)* |
| 22.5 | `/host/events/new` — the representation outage | COMPLETE | *(this commit)* |

**Dependencies:** Phases 7, 21. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 21 commit:** `321c23b`.

**Five single-select steps built from plain `<button>`s.** Representing, Visibility, Pricing,
Category and Type were each a grid of buttons, which to a screen reader is N unrelated controls: no
group name, no indication the choices are mutually exclusive, and **no way to tell which one is
chosen** — selection lived entirely in `border-accent ring-1 ring-accent`. On the Category step that
is a grid of buttons carrying nothing but a name.

Rebuilt on real `<input type="radio">` elements rather than `role="radio"` + `aria-checked`, so the
browser owns arrow-key navigation, the roving tabindex and mutual exclusion — none of which can drift
out of step with the visuals. The input is `sr-only`, not `hidden`, because it must stay focusable.
`<fieldset>`/`<legend>` names each group, with the legend first: a `<legend>` that is not leaves the
group silently unnamed, which is a trap already on this program's list. A checkmark was added as a
second channel, so the choice survives greyscale.

| Other defects | Detail |
|---|---|
| Eleven steps, and no way to hear which | The stepper was pills coloured by a border, with nothing programmatic. `FormSteps` is the Phase 7 primitive built for exactly this and had no call sites; it carries `aria-current="step"` and spells out "(completed)" / "(current step)". Moving between steps also swapped the panel and announced nothing — there is a live region for that now. |
| A disabled button that never said why | "Continue" greys out with no explanation on screen; a disabled control cannot carry its own reason, and some screen-reader navigation skips disabled controls entirely. The reason now sits beside it, and the final step lists each unmet requirement by name and by which step it lives on, instead of simply refusing. |
| Counters nothing pointed at | `160/160` in a floating paragraph while `maxLength` silently stops accepting input. Linked via `aria-describedby` and announced politely as they change. |
| An outage that would have created the wrong event | `/host/events/new` caught its representation read to `[]`, so a host whose organizations failed to load was offered **Personal alone** — and would have created the event under their own name without ever knowing the other choice existed. That is a wrong event, not a degraded one. |

**Guards proven by breaking them** — replacing `FormSteps` with a plain list, and removing the
blocked-reason line, each turn their test red.

**Verification** — web **327 tests** (319 → +8), 65 static pages, typecheck clean, lint clean with
zero warnings.

**Next action** — Phase 23.

### Phase 23 — Event Editing · W 12 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 23.1 | `EditEventForm` — both outcomes of a save | COMPLETE | *(this commit)* |
| 23.2 | `updateEventAction` — deliver the contract the form was written against | COMPLETE | *(this commit)* |

**Dependencies:** Phase 22. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 22 commit:** `ac0f5ab`.

**Saving fourteen fields told the user nothing, either way.**

`EditEventForm` renders `state && "error" in state` — it was written against an action that returns
`{ error }`. `updateEventAction` never returned one: it awaited `updateOrgEvent` bare, so a rejection
propagated straight out of the server action and **the form's only error path was dead code**. A
failed save took the whole page to an error boundary instead of telling the host which field the
server refused.

The success path was missing from the other end: the action *does* return `{ ok: true }`, and the
form rendered it nowhere. So a save that worked looked identical to one that had not run.

The sibling `createEventWizardAction` already had the try/catch shape, so this is the action
delivering a contract the form had been built for all along rather than a new one being invented.
Both outcomes now sit **above** the submit button — after a form this long, a message below it is
off-screen on a phone — with `role="alert"` on the failure and `role="status"` on the success.

**A trap this cost, worth recording:** the first attempt used a Python `str.replace` on the action's
body, which replaces **every** occurrence, not the first — it wrapped four unrelated actions in a
`catch` that had no `try` and broke the module. Reverted and redone with an asserted single match.
`git checkout` was safe here only because the file held no other uncommitted work.

**Verification** — web **329 tests** (327 → +2), 65 static pages, typecheck clean, lint clean with
zero warnings.

**Next action** — Phase 24.

### Phase 24 — Authorization / Representative Flows 🔒 · W 12 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 24.1 | `AuthorizationForm` — field names, document integrity | COMPLETE | *(this commit)* |
| 24.2 | `RepresentativePicker` — search announcement | COMPLETE | *(this commit)* |
| 24.3 | Security review 🔒 | PASSED | *(this commit)* |

**Dependencies:** Phases 21, 23. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 23 commit:** `676b12f`. **This closes milestone M5.**

The routes the inventory assigns to 24 (`/host/representing*`) were already swept in 21.9, so this
phase is the authorization flow itself — the form that produces the evidence a reviewer approves an
event against.

| Defect | Detail |
|---|---|
| **A failed replacement kept the previous letter** | `upload()` set the key and filename only on success — so a replacement that failed at the storage PUT left the **old** key and the **old** filename in place while the error said the upload had failed. The form showed a letter attached, and submitting would have filed the *previous* document under the *new* details. On an authorization letter that is the wrong evidence going on record for an event a reviewer then approves. The prior attachment is now dropped **before** the replacement is attempted. |
| An unhandled rejection on the storage PUT | `fetch` rejects rather than returning a non-ok response when the connection drops, and that rejection went unhandled inside the transition — the button simply re-enabled with nothing said. |
| **Two fields whose name was a paragraph** | The `<label>`s wrapped the control *and* its explanatory `<span>`, which makes the explanation part of the **accessible name**. "Official email" announced as *"Official email An address at the organization's own domain gets reviewed faster. A personal one is accepted, but then the letter has to carry the whole claim"* — twenty-five words on an email box. `Field` puts a helper on `aria-describedby`, where it is read after the name and can be skipped. |
| A file input removed from the accessibility tree | `className="hidden"` does not hide a control, it deletes it — the only route to it was a button forwarding a click. Now `sr-only`, focusable, named and described, with the 10 MB limit stated **before** a too-large file is chosen rather than after. |
| A search that changed underneath you | The representative picker's result list simply swapped as you typed. |

**Security review** (mandatory for 🔒, `.claude/CLAUDE.md` §7) — **passed, no regression.**

1. **Nothing sent to the server changed.** The same `form.*` values, `representativeUserId` and
   `letterheadDocumentKey`; only the JSX element wrapping each control differs. The `ROLES` list is
   untouched and the server validates it.
2. **No client-side authorization decision was introduced.** `editable = !approved || replacing` is
   pre-existing presentation state, not an authority check; the server decides whether a resubmit is
   accepted.
3. **D-269 is intact** — linking a Kurx account is a pointer, never a grant. Neither its code nor its
   wording is in the diff.
4. **No change to secret handling.** The presigned PUT url and headers are used once and never
   rendered; document links remain the server's short-lived presigned URLs; the storage key stays in
   component state and goes only to the server, exactly as before.
5. **The client `MAX_BYTES` clamp is unchanged** and still explicitly mirrors rather than replaces the
   server's limit.
6. **One integrity improvement**, item 1 above: a stale document can no longer be filed under new
   details.

**Guards proven by breaking them** — restoring the stale-attachment behaviour, and moving the helper
back inside the label, each turn their test red.

**Verification** — web **335 tests** (329 → +6), 65 static pages, typecheck clean, lint clean with
zero warnings.

**Next action** — Phase 25.

### Phase 25 — Web Responsive Pass · W 20 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 25.1 | Six-width measurement across the public surface | COMPLETE | *(this commit)* |
| 25.2 | `PublicNav` — overflow at 360 and at 768 | COMPLETE | *(this commit)* |
| 25.3 | Touch targets below 768 | COMPLETE | *(this commit)* |
| 25.4 | `Dialog` — viewport bound | COMPLETE | *(this commit)* |

**Dependencies:** every web phase. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 24 commit:** `ea6ef95`. **This closes M6 and the entire web track (388/388).**

**How it was verified.** D-285 forbids a screenshot harness *in the repo* — no Playwright in
`package.json` or CI. It does not forbid looking: the dev server was run and a browser driven over
it, which is the agent's equivalent of opening one by hand and adds nothing to the repository.
Twelve routes were measured at **360 / 414 / 768 / 1024 / 1440 / 1920**, comparing `scrollWidth`
against `clientWidth` and every non-inline control against the 44px floor.

Final measured result: **no horizontal page scroll and no undersized target, on any of the twelve
routes at any of the six widths.** Inline links inside a sentence are exempt per WCAG SC 2.5.8 and
were excluded deliberately, not overlooked.

| Defect | Detail |
|---|---|
| **Every public page scrolled sideways at 360** | The header's action row was 284px beside a 71px logo inside a 328px container. Two of its controls were also under the touch floor — the theme toggle at 34×40 and the language switcher, the only route to Hindi, at 52×36. Rather than dropping a control, the primary CTA goes icon-only below `sm` and the gaps tighten; everything keeps a 44px target and an accessible name. |
| **And again at 768** | The page-link row appears at `md`, but at 768 it is 379px and the actions 387px beside the logo — 869px of content in a 736px container. It first fits at 1024, so it now appears at `lg`; between 768 and 1023 those destinations stay reachable from the footer, which is why Phase 11 added one. A defect the 360-only first pass would have missed entirely. |
| **`Dialog` had no vertical bound** | The panel had no `max-h` and its body no overflow, so a dialog taller than the viewport grew past it — and because the overlay centres its child, it overflowed off the top and bottom at once. The footer holds Cancel and Confirm, so on a short viewport (a phone in landscape is 640×360) a confirmation could be neither confirmed nor dismissed except by Escape. This is the primitive behind **every `ConfirmDialog` on web and admin**, including the thirteen destructive confirmations Phase 21 added. |
| A skeleton that overflowed before content loaded | `/discover`'s loading state carried `w-96` — 384px in a 328px box. |
| Sub-44px targets in the sign-in panel and footer | "First time? Sign in with a code" and "Forgot password?" were 20px-tall standalone controls, not inline links; footer labels as short as "Blog" were 31px wide against a floor that applies in both axes. |

**What is guarded, and what is not.** The browser sweep cannot be committed, so `responsive.test.tsx`
asserts the *shapes* whose return would reproduce what it found: no unprefixed width past the 360px
content box, every table in its own scroll container, `Dialog` bounded with a scrolling body, and the
header's two breakpoint decisions. The measurement itself must be re-run by hand — it is recorded
here as the evidence, and Phase 47 owns re-running it.

**A trap worth recording:** these sweeps read source text, so a comment *describing* a defect reads
exactly like the defect. The width guard's first run flagged its own explanation of `w-96`; it now
skips `{/* … */}` blocks so a comment can name the shape it warns about.

**Verification** — web **340 tests** (335 → +5), 65 static pages, typecheck clean, lint clean with
zero warnings; admin typecheck/lint clean, 29 static pages (the `Dialog` fix reaches admin too).

**Next action** — Phase 26.

### Phase 26 — Admin Information Architecture · W 6 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 26.1 | Reachability audit — nav ↔ routes, both directions | COMPLETE | *(this commit)* |
| 26.2 | Route-level role enforcement from `NAV` | COMPLETE | *(this commit)* |
| 26.3 | The five `ready: false` nav items | COMPLETE | *(this commit)* |

**Dependencies:** Phase 2 (which set this phase's agenda). **Blocked by:** none.
**Completed:** 2026-08-07. **Phase 25 commit:** `ed71327`. Opens M7.

**Reachability is sound** — all 19 built nav destinations have routes, and every route has a way in
(`/reset` is reached from `/login`; `/account` and `/security` from the user menu; `/users/[id]` and
`/competitions/[stageId]` from their lists). No orphans, no broken links. That is the good news and
it is worth stating, because the web audit at Phase 2 found three broken links and eighteen orphans.

**The finding — the IA was declared and never enforced.**

`NAV` gates every destination by role, with careful per-item reasoning: Certificates is SuperAdmin
because those endpoints check the `kurx_admin` claim, and the comment says in as many words that "a
Reviewer would see the screen and get a 403 they cannot resolve." **Every console page guarded with
`requireStaffSession()` alone**, which asks only whether the caller holds *some* platform role. So a
Support admin who typed `/staff` reached it; a Reviewer who typed `/certificates` reached the exact
screen that comment was written to keep them away from.

Nothing leaked — the backend is the authority and refuses those calls, which is the principle the
program holds to. But hiding a destination and then serving it is the contradiction the comments
existed to prevent.

`requiredRolesFor(pathname)` resolves a route's roles **from `NAV` itself**, so there is one source
of truth for what an operator sees and what they can reach; the console layout renders `RoleRequired`
in place of the page when they do not match. Longest-href match, so `/users/[id]` inherits `/users`
and `/events/review` inherits `/events`. The pathname reaches the server layout on a header the
middleware sets — a server layout cannot read it, and moving the whole console shell to the client
for `usePathname()` would be a much larger change for a smaller result.

**Measured across all 24 console routes: 20 are now role-scoped; before, every one was open to any
staff.** The four that remain open are the dashboard, analytics, and the operator's own `/account`
and `/security` — correct in each case.

`RoleRequired` is deliberately not `/forbidden`, which answers "this account is not Kurx staff" and
signs you out. It answers "you are staff, but not for this", keeps the shell so the operator can go
somewhere they *can* use, and names the role required. It does not hide behind D-018's 404-not-403
rule: that exists to stop resource *existence* leaking to outsiders, and every reader here is already
staff looking at a sidebar that lists these areas openly.

**The five `ready: false` nav items are removed.** `information-architecture.md` §9 asked this phase
to decide per item. Payments, Payout Approvals, Refunds, Templates and Background Jobs had rendered
as disabled `<span>`s since Phase 1 — `text-muted/50`, well under any contrast floor, a permanent
"soon" badge, and `title="Coming in a later phase"`, a note between engineers delivered to operators
through a tooltip no keyboard or touch user can reach. None can ship: none has a route, and Templates
is backend-blocked. The intended map moves to §9 of the IA document, where a plan belongs.

**How it was verified.** Admin has no test runner, so the role matrix was resolved for every console
route against SuperAdmin / Reviewer / Support and the table read by hand — the same
evidence-not-assertion approach Phase 25 used for widths. `next build` and `lint` are clean.

**A decision this phase deliberately did NOT take:** admin still has no test suite. `vitest` is
already hoisted at the workspace root, so a harness would add no new dependency class (D-109 excludes
Playwright/Cypress/Storybook, not vitest) — but standing one up is infrastructure, not information
architecture, and Phase 27 owns the shell. Logged as deferred with 27 as its owner.

**Verification** — admin typecheck/lint clean, 29 static pages; web untouched at 340 tests.

**Next action** — Phase 27.

### Phase 27 — Admin Shell · W 12 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 27.1 | **Admin's first test harness** | COMPLETE | *(this commit)* |
| 27.2 | The mobile drawer — the modal it claimed to be | COMPLETE | *(this commit)* |
| 27.3 | Sidebar — targets, tint, count semantics | COMPLETE | *(this commit)* |

**Dependencies:** Phase 26. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 26 commit:** `e8b039c`.

**Admin has a test suite.** Phase 26 logged this as its deferred decision and named 27 as the owner,
and this is 27 taking it. Until now `typecheck && lint && build` plus manual browser checks were the
*entire* guard on 27 screens — the largest untested surface in the product, and the one where a
mistake means an operator suspending the wrong account rather than a card sitting a few pixels off.

D-109 excludes Playwright, Cypress and Storybook and that still holds: this is vitest and React
Testing Library, **the same runner web has had since Phase 0**, so no new dependency class enters the
repo. The devDependencies are declared at the same versions web already resolves, so the root install
moved the tree by seven lockfile lines.

Two environment obstacles, both solved once in `test/setup.ts` rather than per file:

* React's `cache()` exists only under the react-server condition, so `lib/api.ts` throws
  "cache is not a function" in jsdom — and vitest reports that as a suite collecting *zero tests*,
  not as a failure. Shimmed as identity, which is faithful: `cache` is a request-scoped memoiser and
  a test is one request.
* `server-only` throws by design when a client bundle imports it. jsdom is neither bundle, so the
  guard fires on a legitimate chain. Stubbed for tests only; the production guard is untouched.

**The mobile drawer declared `aria-modal="true"` and implemented none of it.** Focus never entered
the panel, Tab walked straight onto the page behind, Escape did nothing, and focus was never restored
to the menu button. That is audit **S1-2** exactly — the defect Phase 9 built `useOverlay` to fix for
`Dialog` and `Sheet`, and which admin's own navigation was never wired to. Claiming the background is
inert without making it inert is worse than omitting the attribute, because assistive tech believes
it.

Not `Sheet`: it opens right or bottom only, and forces a title header this drawer does not want
around a full sidebar. `useOverlay` is the shared behaviour without the chrome.

| Other defects | Detail |
|---|---|
| Sub-44px nav targets | Every sidebar link and group toggle was ~36px, on a drawer whose entire purpose is mobile. |
| The Phase 6 tint trap, again | The active count badge was `text-accent` on `bg-accent/15` — a tint eats exactly the contrast headroom the token was solved for, which is why every tinted `Badge` tone failed AA in both themes. |
| Counts that did not say what they counted | "Verification Queue 12" tells a screen-reader user twelve of *something*. Now "12 waiting". |
| A `z-50` outside the token scale | The skip link's stacking order was hand-picked rather than taken from `z-toast`. |

**Guard proven by breaking it** — removing the `useOverlay` call reds the drawer test.

**Verification** — admin **9 tests** (from none), typecheck/lint clean, 29 static pages; web
untouched at 340 tests, and its `node_modules` verified intact after the root install (empty is
normal workspace hoisting, not the `npm ci` trap).

**Next action** — Phase 28.

### Phase 28 — Admin Data Components · W 18 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 28.1 | `DataTable` — keyboard operation, selection semantics, busy state | COMPLETE | *(this commit)* |
| 28.2 | `PageHeader` / `SectionHeader` — contrast, and the promotion decision | COMPLETE | *(this commit)* |
| 28.3 | Admin-wide accent-as-text sweep | COMPLETE | *(this commit)* |

**Dependencies:** Phase 27 (which gave admin its test runner). **Blocked by:** none.
**Completed:** 2026-08-07. **Phase 27 commit:** `844becd`.

**`DataTable` backs 15 admin screens, so each defect below was 15 defects.**

| Defect | Detail |
|---|---|
| **Clickable rows were mouse-only** | `onRowClick` is how three admin screens open a record, and the `<tr>` carried no `tabIndex`, no key handler and no role — the primary action on those tables could not be reached from a keyboard at all. It is a button in everything but tag name now, named after its row so a screen reader says which record it opens. |
| **The scroll region was unreachable** | `overflow-x-auto` with no `tabIndex` scrolls under a pointer and nothing else (WCAG 2.1.1), which on a table wide enough to need it means whole columns a keyboard user cannot see. |
| Every row checkbox shared one name | "Select row", N times, with nothing to tell them apart. `rowLabel` names each one after its row. |
| A partial selection showed as unchecked | Which states "none of these are selected" while several are. `indeterminate` is a DOM property rather than an attribute, so it needs a ref callback — which is presumably why it was missing. |
| Selection changed silently | It drives the bulk actions above these tables — suspend, blacklist, archive — and nothing announced how many were selected. |
| Loading was a shape, not a state | Six skeleton rows appeared with no `aria-busy` and nothing said. |
| Selection was a 5% tint | `bg-accent/5` is very nearly nothing; it now carries a left marker too, so the state survives greyscale. |

**The kicker at the top of all 23 admin pages was below AA.** `PageHeader` set it in `text-accent`,
which `tokens.css` documents as the ember *fill* and which measures 2.80:1 as text on light.
`SectionHeader`'s action button in the design system had the same defect. Swept admin-wide: **24
sites across 14 files**, now guarded by a test that skips comment blocks so a note describing the
defect is not read as the defect.

**`PageHeader` is deliberately NOT promoted to `@kurx/ui`**, though the Component Migration Matrix
planned it. Web has no consumer — it is `<h1>` page chrome, and the system already exports
`SectionHeader` for the `<h2>` in-page case. Moving a one-caller component into the shared package
is the speculative abstraction `.claude/CLAUDE.md` §5 rules out. The matrix row is corrected rather
than silently ignored; promote it the day web needs it.

**Guard proven by breaking it** — removing the row's `tabIndex` and `role` reds both keyboard tests.

**Verification** — admin **19 tests** (9 → +10), typecheck/lint clean, 29 static pages; web
unchanged at 340 (it shares `@kurx/ui`, so the `DataTable` change was verified against both).

**Next action** — Phase 29.

### Phase 29 — Admin Core Screens ⚠️ · W 40 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 29.1 | Outage-as-fact on the screens where empty is a claim | COMPLETE | *(this commit)* |
| 29.2 | `section()` — D-235's idiom, which admin never had | COMPLETE | *(this commit)* |
| 29.3 | Touch-target and date-locale sweeps | COMPLETE | *(this commit)* |

**Dependencies:** Phases 27, 28. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 28 commit:** `1b8fe33`.

**The distinction this phase turned on: which empty lists are claims.**

Admin had **23** `.catch(() => [])` sites. Converting all of them would have been mechanical without
thought, because most are genuinely auxiliary — a missing fraud score is a missing badge, a missing
review checklist shows less. Two are not:

* **`listReports(…, "open")` on the events console.** A row with no report badge reads as *no open
  reports against this event*, and a moderator scanning the queue passes over it on exactly that
  basis. An outage must not write that sentence.
* **Sessions, devices and trusted browsers on `/security`.** An empty list there reads as *nothing
  else is signed in as you* — on the one screen an operator opens **because** they suspect something.

Both now carry the failure to the screen and say plainly that an absent badge or an empty list is not
evidence. The rest are left as they are, deliberately, and the reasoning is in the code.

**`section()` did not exist in admin.** Web has had D-235's helper since Phase 15; admin kept writing
`.catch(() => [])` because it had nothing else. Ported, with the note that an empty list on a console
is worse than on web — an operator *acts* on it.

| Sweep | Detail |
|---|---|
| **29 sub-44px controls** | Buttons shrunk to `h-8` across the review, moderation and workspace screens. The console is used on tablets at the gate, not only on a desk. |
| **21 locale-unstable dates** | `toLocaleDateString()` with no locale resolves to the server's on the server and the browser's on the client. On an audit log, a date that renders differently to two people is worse than useless. |

Both are guarded by tests that skip comment blocks, so a note describing a defect is not read as one.

**Scope, honestly stated.** 40 points is the program's largest phase and covers 27 screens. What is
done is every defect **class** that spans them, plus the two screens where an outage produced a false
statement. Individual screen-by-screen visual passes are not done and are not claimed; the inventory
rows stay `Legacy` except where this phase actually changed them, and Phase 47's visual audit still
owns the per-screen look.

**Verification** — admin **21 tests** (19 → +2), typecheck/lint clean, 29 static pages; web
unchanged at 340.

**Next action** — Phase 30.

### Phase 30 — Admin Review / Moderation UX · W 14 · COMPLETE · 100%

| # | Sub-phase | Status | Commit |
|---|---|---|---|
| 30.1 | Moderation decisions behind a confirmation | COMPLETE | *(this commit)* |
| 30.2 | **`Button` had no default `type`** | COMPLETE | *(this commit)* |

**Dependencies:** Phase 29. **Blocked by:** none. **Completed:** 2026-08-07.
**Phase 29 commit:** `295c6a7`.

**Approve and reject fired on one click, side by side.** These are the highest-stakes controls in
the product — approving publishes an event to everyone, rejecting takes it away from an organiser
who has usually spent days on it — and they sat next to each other with nothing in between.
`ConfirmDecisionButton` puts the gate in front while leaving the native submit intact, so the server
action and its revalidation are untouched.

Deliberately **not** applied to `reject_review` / `request_changes`: those already open a reason form
the reviewer has to fill in, which is a stronger gate than a dialog.

**And the gate found something bigger.** The first test asserted one submission and got **two**.

`Button` never set a `type`, and HTML defaults a `<button>` inside a form to `type="submit"`. So the
confirmation dialog — which renders *inside* the form it guards — submitted the form from its confirm
button **and** again from the guard's own `requestSubmit()`. The same dialog's **Cancel** button
submitted the form too. On a moderation console that is a decision recorded twice, and a "Cancel"
that commits.

The blast radius was wider than moderation: **~38 call sites across web and admin pass only
`onClick`** and were relying on a default that, inside a form, did not do nothing — "Keep my account"
sitting inside the delete-account form among them. 44 call sites already write `type="submit"`
explicitly, which is what made the default safe to flip.

`Button` now defaults to `type="button"`; callers that want a submit say so, and they already did.

**Verification** — admin **25 tests** (21 → +4), web **340**, both typecheck/lint clean, 29 and 65
static pages. The `Button` change touches every surface, so both suites and both builds were run.

**Next action** — Phase 31.

### Phase 31 — Admin Real-Time UX 🔒 · W 8 · COMPLETE · 100%

**Dependencies:** Phase 30. **Completed:** 2026-08-08. **Phase 30 commit:** `4290c3f`.
**This closes M7 — the admin track is complete (98/98).**

**A dead socket and a quiet gate looked identical.** The workspace showed a "live" badge when
connected and **nothing at all** when not, so an operator watching an empty check-in feed on the door
could not tell "nobody has arrived in ten minutes" from "the feed died ten minutes ago" — which is a
decision they make at the gate.

The hub's comment that "realtime is an optimisation, never the source of truth" is right about the
*data* and wrong about the *operator*: the fallback figures are correct, but whether they are moving
is itself information. The badge now reads in both directions, and stays silent when the flag is off,
because then no live feed was promised and none is missed.

**Security review 🔒 — passed, and one thing tightened.**

1. **The token is always the caller's own** — `session.accessToken`, never impersonation.
2. **`no-store`, `no-cache`** — kept out of every cache including the PWA service worker.
3. **Sent only to the API origin**, via `accessTokenFactory` on `siteConfig.apiBaseUrl`.
4. **Group-join authority stays on the server (D-017).** `ScanHub.JoinEvent` re-queries membership on
   every call, and the `kurx_admin` bypass comes from a claim `PlatformRoleClaimsTransformation`
   grants to SuperAdmin only. A client naming an `eventId` does not gain access to it.
5. **No authorization decision moved to the client** by this phase.
6. **Tightened:** `/api/realtime-token` used `currentSession()` — *a* session, not a staff one — while
   every other route on this origin requires a platform role. Not an escalation (the token returned
   is the caller's own, obtainable from web's identical route regardless), so this is defence in
   depth rather than a closed leak; but the admin origin should hand nothing to a non-staff caller.

**Verification** — admin **27 tests** (25 → +2), typecheck/lint clean, 29 static pages.

**Next action** — Phase 34.

### Phase 34 — Flutter Discovery · W 16 · COMPLETE · 100%

**Dependencies:** Phases 32, 33. **Completed:** 2026-08-08. **Phase 31 commit:** `7b6aa7e`.

| Defect | Detail |
|---|---|
| A status colour outside the theme | The competition chip used Material's `Colors.green` for *published* while every sibling in the same `switch` read `c.accent` / `c.muted` from the token set. It ignores the theme, ignores dark mode, and was never solved against the surfaces it sits on — the hardcoded-status-colour class the deferred ledger names, now `c.success`. |
| A control with no name | The event search's clear button was an `IconButton` with no `tooltip` — announced as "button" with nothing to say what it does, on the only way to clear a search without deleting the text by hand. |
| Four more unannounced tappables | REG-022's list: the shared `kurx_chip` delete (also a 14px target against the 48dp floor), the ticket card a buyer opens at the gate, a post's author avatar, and a leaderboard entry. |

**The sweep is now a Dart test that carries its own worklist.** `test/features/tap_semantics_test.dart`
walks `lib/`, fails on any `GestureDetector` with no `Semantics` or `InkWell` above it, and holds the
still-open sites in a `remaining` set — so the file *is* the REG-022 worklist. A screen's phase
deletes its line and the sweep tightens itself; a second assertion pins the set at ≤10 so it cannot
quietly grow back. **Proven by unwrapping `kurx_chip` and watching it name the file and line.**

**Third time for one trap.** The sweep flagged its own explanatory comments, exactly as the web and
admin sweeps did — a comment *describing* the defect reads precisely like the defect. It skips
comment lines now, and the trap is in `HANDOFF.md` for all three surfaces.

**Verification** — mobile **309 tests** (307 → +2), `flutter analyze` clean.

**Next action** — Phase 35.

### Phase 35 — Flutter Event Detail · W 14 · COMPLETE · 100%

**Dependencies:** Phase 34. **Completed:** 2026-08-08. **Phase 34 commit:** `5c610ea`.

**Mobile's checkout worked, and nothing in the app navigated to it.**

`CheckoutPage` is fully implemented — it calls `createOrder` with an `Idempotency-Key`, exactly as
the backend contract asks — and its route `/events/:slug/checkout/:eventId/:ticketTypeId` has existed
all along. **No screen pushed to it.** It was reachable only by typing the URL, so on mobile a person
could read an event and had no way to register for it from the page that describes it.

That is the exact mirror of what **REG-009** records for web: web has the entry point and no working
checkout; mobile had the working checkout and no entry point. Two halves of one journey, each broken
in the opposite way, on two surfaces nobody had compared.

Nothing was invented to fix it. `EventPage` already loads `ticketTypes`, so the register bar is built
from data the screen had: the cheapest still-available type is preselected because that is what a
"Register" button means; when several exist the label says *"Choose a ticket · from …"* rather than
pretending the choice was made; and an event with nothing available says registration is closed
instead of offering a button that would fail at the next screen.

**A guard that was useless until it was broken.** The first version matched the string `/checkout/`
anywhere in the file — which its *own explanatory comment* satisfied, so breaking the real
`context.push` left it green. That is the fourth appearance of this trap in the program and the first
where it made a guard *worthless* rather than merely noisy. It now strips comments and matches a
`push(… /checkout/ …)` call. **Proven by breaking every non-comment occurrence and watching both
tests fail.**

**Verification** — mobile **311 tests** (309 → +2), `flutter analyze` clean.

**Next action** — Phase 36 🔒.

### Phase 36 — Flutter Authentication 🔒 · W 14 · COMPLETE · 100%

**Dependencies:** Phase 35. **Completed:** 2026-08-08. **Phase 35 commit:** `306bd4b`.

**The OTP autofill defect Phase 16 fixed on web was still live on mobile.** Web's record says the
field "had **no `autocomplete="one-time-code"`**, so SMS autofill never fired". The Flutter side was
never swept, and there it costs more: the message arrives on the *same device*, a notification away,
and without `AutofillHints.oneTimeCode` the platform never offers it — so the code is memorised, the
app is left, and it is retyped. Fixed on sign-in, on password reset (reached by someone already
locked out, the worst moment to retype by hand) and on registration's email code.

Registration's name and username fields gained `name` / `newUsername` for the same reason: the
platform already knows them and was never asked.

**Two fields were deliberately left un-hinted, and the guard asserts that they stay that way:**

* the **recovery code** — a secret its holder keeps out of band;
* the **D-181 approval challenge**, typed from the *other* screen on purpose. Its own comment states
  the property: "a request they did not start… cannot be approved — which is the whole defence."
  Autofilling it would delete that defence.

A one-directional guard would have invited exactly that regression, so the test has both halves.

**Security review 🔒 — passed.**

1. The diff is four presentation files; **no token, secret, credential or logging path is touched.**
2. Every hint added is a one-time code, a name or a username — **no hint was added to a secret or to
   an anti-phishing challenge**, and the two that must stay un-hinted are asserted.
3. `recovery_page`'s pre-existing hints were checked: `username` on the identifier and `oneTimeCode`
   on the OTP, with the **recovery-code field itself correctly bare**.
4. No authorization or verification decision moved to the client.
5. One token fix: a security alert's *warning* severity rendered in Material's `Colors.amber`,
   between two theme reads — ignoring dark mode and never contrast-solved, on the severity of a
   security alert.

**Verification** — mobile **314 tests** (311 → +3), `flutter analyze` clean. Guard proven by removing
the OTP hint and watching it fail.

**Next action** — Phase 37 💰🔒.

### Phase 37 — Flutter Tickets & Registration 💰🔒 · W 18 · COMPLETE · 100%

**Dependencies:** Phase 35 (which made this flow reachable). **Completed:** 2026-08-08.
**Phase 36 commit:** `e6b7b21`.

**Most of this phase's value was in what it did *not* change.** The checkout path is genuinely well
built and was left alone after being read:

* the idempotency key is minted **once per screen, not per tap** — two taps are one purchase intent,
  and the server's §17.1 atomic claim on that key is what stops a double-tap becoming two tickets;
* `guard()` converts **every** throw — transport, parse, anything — into `ApiError` before it reaches
  the screen, so the `on ApiError` there is total and a failed payment can never leave a stuck
  spinner with no answer;
* the error banner is already a live region, and the claim flow already invalidates the cache with a
  note on D-062's server-side QR rotation.

An earlier read of this file nearly produced a "fix" to the catch clause for a stuck state that
`guard()` makes impossible. Both properties are now **asserted** instead of admired — the guard
proves itself by reds when the key is regenerated inside the handler.

| Defect | Detail |
|---|---|
| **A dead control on the gate credential** | The ticket screen's app bar carried a share `IconButton` with an **empty `onPressed`** and no tooltip — it advertised itself and did nothing (REG-004's pattern). Removed rather than implemented, and the security review agrees with that direction: the QR on this screen **is** the admission credential, so a share sheet handing it to another app is a security decision rather than a convenience, and **transfer already exists** as the deliberate server-mediated route — D-062 rotates the code on claim precisely so a copied one stops working. |
| Another raw Material colour | `Colors.green` for a *converted* waitlist entry, sitting among four theme reads — no dark mode, never contrast-solved. |

**Security review 🔒 — passed.**

1. **Order creation, payment and idempotency are untouched** — the diff is two presentation files;
   `checkout_page`, the attendee data source and `api_guard` do not appear in it.
2. **No new egress for the ticket code.** No share, clipboard, `launchUrl` or intent was added; the
   only change in that direction *removes* an affordance.
3. The removed control is confirmed inert in the diff — its handler was an empty block.
4. No authorization or payment decision moved to the client.

**Verification** — mobile **318 tests** (314 → +4), `flutter analyze` clean.

**Next action** — Phase 38.

### Phase 38 — Flutter Profile & Settings · W 16 · COMPLETE · 100%

**Dependencies:** Phase 37. **Completed:** 2026-08-08. **Phase 37 commit:** `ab46155`.

**A dead control on a security screen.** `my_devices_page` carried a "Remove" button with an empty
`onPressed` — on the screen somebody opens when they think their account is compromised. A dead
control is bad anywhere; one that implies a **security action succeeded** is worse, because the user
walks away believing the device was revoked.

It is **not** wired, deliberately. The page is an **orphan**: registered in the router and reached
from nowhere in the app, while `security_page.dart` carries real, reachable revocation
(`_revokeDevice` over `revokeSession`). Wiring a second, unreachable copy of a security action is
worse than removing the control. Deleting the page belongs to **Phase 49**, and the orphan evidence
is now in the Regression Ledger — the same split Phase 21 used for `/host/templates`.

| Other defects | Detail |
|---|---|
| Four raw Material colours | `Colors.green` on *identity verified*, *claim approved* and *participation accepted*, each sitting among theme reads — no dark mode, never contrast-solved, on badges asserting that something was confirmed. |
| A text-only tap target with no role | "+ Claim your username" — the action that gives a person their public identity — announced as nothing, on `c.accent`, which is the ember **fill** at 2.80:1 as text on light. The same token defect swept across web and admin. |

**The dead-control shape now has a permanent guard.** This program has found it four times —
web's bookmark button (REG-004), the fake event QR (REG-005), the ticket screen's share button
(Phase 37) and this one. The sweep found **ten** across mobile and, like the semantics sweep, it
**carries its own worklist**: five files pinned against the phases that own them (38A, 38B, 39), a
failure on any *new* one, and a second assertion that the set cannot grow. Each pinned site needs a
judgement its own phase is scoped to make.

**The semantics worklist tightened itself**, as designed — `profile_page` came off the list and the
ceiling dropped from 10 to 9 in the same commit.

**Verification** — mobile **320 tests** (318 → +2), `flutter analyze` clean.

**Next action** — Phase 38A.

### Phase 38A — Flutter Posts & Chat · W 16 · COMPLETE · 100%

**Dependencies:** Phase 38. **Completed:** 2026-08-08. **Phase 38 commit:** `367032a`.

| Defect | Detail |
|---|---|
| **A message's actions were unreachable without sight** | Reply, copy and delete live behind a **long-press on the bubble** — the only route to them — and the gesture announced nothing at all. To a screen reader those actions did not exist. `onLongPressHint` is what makes a gesture discoverable to TalkBack and VoiceOver rather than something a sighted user has to already know. |
| A chat attachment that opened silently | Tapping one opens a full-screen preview; it announced as neither a control nor a name. It now says which file it opens, and says *"still uploading"* rather than going quiet while pending. |
| A card that opened what was already open | `post_detail_page` passed `onTap: () {}` to `PostCard`. The widget treats a **null** `onTap` as "not tappable" and otherwise defaults to opening the post — so the empty callback made the card on the post's own page announce as a control that opens the page you are on. |

**Both self-shrinking worklists tightened in the same commit**, which is what they were built for:

* `tap_semantics_test.dart` — **9 → 7** open sites, ceiling dropped with them;
* `no_dead_controls_test.dart` — **5 → 4** files, ceiling dropped with them.

Neither needed a person to notice: each phase deletes the lines it closes and the assertion that the
set cannot grow does the rest.

**Verification** — mobile **320 tests**, `flutter analyze` clean. (No new tests: this phase closed
sites the existing sweeps already assert, which is the sweeps working as intended rather than a gap.)

**Next action** — Phase 38B.

### Phase 38B — Flutter Identity · Certificates · Gamification · W 14 · COMPLETE · 100%

**Dependencies:** Phase 38A. **Completed:** 2026-08-08. **Phase 38A commit:** `45a2cd2`.

**Six dead controls on one screen — the certificate detail page.** An app-bar share button, a row of
four branded share buttons (WhatsApp, LinkedIn, Instagram, Copy Link), and a Download PDF. Every one
`onPressed: () {}`. Somebody trying to share the credential they had just earned failed **four
different ways**, each with a convincing brand colour behind it.

The split between what to build and what to remove came from what the app actually has:

* **Download PDF was implemented.** `cert.pdfUrl` is a real server-issued URL, the button is already
  gated on it being non-null — so it only ever appeared when there *was* a document, and then did
  nothing with it — and `url_launcher` is a declared dependency already used on two other screens.
  Nothing needed inventing.
* **Every share control was removed.** All of them need a public URL for the certificate, and mobile
  has **no configured web base** to derive one from; constructing one would be inventing where this
  credential lives. Instagram has no share URL scheme at all, so that one could not have been honest
  even with a base.

`my_groups_page` carried a "Join a group" action with an empty handler. The backend has
`POST /v1/groups/join`, but **nothing in this app calls it** — no join screen, no data source method,
no route. Building one is a feature rather than a redesign, so the control goes and the unused
endpoint is recorded in the Deferred Work Ledger.

Also: the certificate card was tappable and announced as nothing — the same shape as the ticket card
in Phase 35, on the credential a person shows an employer.

**Both worklists tightened again**: dead controls **4 → 2** (both survivors are Phase 39's), tap
semantics **7 → 5**. One correction worth recording: the semantics list was briefly trimmed for the
certificate pages before their gestures were actually wrapped — the sweep failed on the next run and
the entry was earned properly rather than left mis-recorded.

**Verification** — mobile **320 tests**, `flutter analyze` clean.

**Next action** — Phase 39.

### Phase 39 — Flutter Organizer Screens · W 36 · COMPLETE · 100%

**Dependencies:** Phase 38B. **Completed:** 2026-08-08. **Phase 38B commit:** `190416f`.
The program's largest single phase, across 23 organizer pages.

| Defect | Detail |
|---|---|
| **The check-in scanner's torch had no name** | A working control — `_controller.toggleTorch()` — with no tooltip, on the scanner an operator uses at the door, often in the dark, which is the entire reason the torch is there. |
| An overflow menu that opened nothing | `announcements_page` had a `more_vert` button with an empty handler, no tooltip, **and** `constraints: BoxConstraints()` stripping the 48dp minimum — so it was a tiny target that did nothing. Removed: what belongs in an announcement's menu is a product question, and cancel already exists as its own control on the web console. |
| A named button that did nothing | "Export CSV" on the attendees page announced itself perfectly and had an empty handler — worse than being unnamed. Removed: there is no export endpoint in the Flutter data layer, and writing a file to device storage is a feature with its own permissions story. |
| Two raw Material greens | On the KYC status and a withdrawal's submitted state — the identity and money screens. |

**Both self-shrinking worklists reached zero**, which is the outcome they were built for:

* **REG-022 is closed** — all **15** unannounced tappables, from `kurx_chip`'s 14px delete target in
  Phase 34 to an FAQ row that expanded with no `expanded` state here.
* **All 10 dead controls are closed** — wired where something real existed (Download PDF), removed
  where implementing meant inventing.

Both sets are **kept rather than deleted**: they are where a future exception would have to be
written down and justified, and each file's assertion now refuses to let one in quietly.

**A last finding from the sweep itself.** `app_shell` sat on the semantics list to the end and never
needed work — Phase 33 had already wrapped it, and it stayed listed because the sweep's 12-line
lookback could not see past the comment *explaining that fix*. The same comment that documents a fix
can make the fix look absent. Recorded in the file for whoever widens that window.

**Verification** — mobile **320 tests**, `flutter analyze` clean.

**Next action** — Phase 40.

### Phase 40 — Mobile Native UX · W 14 · COMPLETE · 100%

**Dependencies:** Phase 39. **Completed:** 2026-08-08. **Phase 39 commit:** `31807e7`.
**This closes M9 and the mobile track (194/194).**

**There was no `PopScope` anywhere in the app.** On Android the system back gesture is the primary
way people navigate, and registration is a **single page with inline stages** rather than separate
routes — so one gesture exits the entire flow, taking a verified phone, a verified email and
everything typed with it, without a word.

The guard is conditional rather than blanket: `canPop: !_hasProgress`, so an untouched form still
closes on the first gesture. A confirmation that fires when nothing is at stake teaches people to
dismiss it unread, which makes it useless on the one occasion it matters.

`onPopInvokedWithResult` rather than `WillPopScope`, which was removed in Flutter 3.16 and would
also miss predictive back on Android 14+. A second assertion keeps it from being reintroduced.

**What was measured and deliberately not changed:** 70 fields carry no `textInputAction`, so
multi-field forms do not chain "next" through the keyboard. It is real friction, but it is 70 sites
of individual judgement about focus order — recorded rather than swept blind at the end of a phase.

**Verification** — mobile **322 tests** (320 → +2), `flutter analyze` clean.

**Next action** — Phase 41.

### Phase 41 — Cross-Platform Consistency · W 12 · COMPLETE · 100%

Web and admin share a palette by construction — both consume `@kurx/ui`. Flutter necessarily keeps
a second copy, because Dart cannot read a CSS custom property, and **nothing in the repo compared
the two**. Measured token by token they agree exactly: 28 pairs across both themes, zero drift, so
D-286's unification held. `web/test/design-tokens.test.ts` now reads `design_tokens.dart` directly
and keeps it that way — the four-way colour drift of D-285 happened *within* one surface, and two
unwatched copies across two languages is the same setup with a worse blast radius.

The copies that **had** drifted were the ones outside the palette. Eleven sites wrote a colour as a
literal where a token existed (**REG-024**): seven Material ambers standing in for `warning`, a red
for `danger`, a brown used as body text in two callouts, and two callout backgrounds fixed at a
light cream. Every one is also a theme defect — a `const Color` has a single value where a token has
two — and the cream callouts were the sharpest: they stay light in dark theme, so the text on them
would have been light-on-light. The amber measured about 1.9:1 where it was used as text.

Mobile also spoke its own dialect (**REG-025**). An order the site labels "Paid" or "Free" the app
labelled "Confirmed" for both — the same order, the same backend status, two vocabularies, and the
collapsed distinction is exactly the one a refund conversation turns on. Aligned to web's words.

A new Flutter guard, `test/features/palette_discipline_test.dart`, bans literal colours outside
three named files — the palette itself, the decorative gradient ramps, and the medal tiers — each
allowed **with its stated reason**, and asserts every allowance still points at a file that exists,
so the list cannot outlive what it excuses.

**Next action** — Phase 42.

### Phase 42 — Microinteractions · W 10 · COMPLETE · 100%

`core/theme/motion.dart` was built in Phase 32, and wired into exactly one consumer — `app_shell.dart`.
Seven other files animated with raw durations (**REG-026**), so a user who had asked the OS to reduce
motion still got every one of them (WCAG 2.3.3). Two were a worse case than the rest: the skeleton
shimmer and the chat typing dots each `..repeat()` with **no end condition**, running for as long as
the wait underneath them lasts — the automatically-starting motion of over five seconds that WCAG
2.2.2 asks to be stoppable, and the shimmer is on screen during every load in the app. Both now read
the setting in `didChangeDependencies`, which is where a MediaQuery-dependent controller can see it;
under reduced motion they hold still rather than run fast.

The chat scroll **jumps** rather than clamping. A scroll is a positional move, and a slide that merely
goes faster is still a slide — the distinction `motion.dart`'s own doc comment draws, and the one place
in the codebase where it applied.

**Web and admin needed nothing, which is worth recording as a measurement rather than an assumption.**
The universal-selector block in `tokens.css` covers CSS motion; the single `scrollIntoView` asks for no
smooth behaviour (JS that requests `behavior: "smooth"` would have overridden the CSS, so this was the
gap worth checking); every `setInterval` is polling or a heartbeat, not animation; and both `opacity-0`
reveals are `peer-checked:` rather than hover-only.

Separately (**REG-027**), both confirm-then-submit primitives submit their form natively, so there is
no client-side promise to hang a spinner on and the trigger looked identical before and during the
action. A confirmed delete on a slow connection showed nothing at all, and the obvious response to
that is to click again — which reopens the dialog and deletes a second thing. On admin it was sharper:
Approve and Reject sit in **one form** on `event-review-form.tsx`, so Reject stayed live while an
Approve was in flight against the same event. `useFormStatus` scopes to the owning form, which is
exactly the scope that was missing.

New guard `mobile/test/features/reduced_motion_test.dart` — a behavioural pair proving the shimmer
holds still under the setting and still pulses without it (clamping must not become disabling), plus a
sweep that no duration is written where the setting cannot reach it. **Verified by removing one clamp
and watching the sweep name the line.**

**Next action** — Phase 43.

### Phase 43 — UX Writing · W 14 · COMPLETE · 100%

`docs/api/README.md` defines `error` on every ProblemDetails as **"a stable machine-readable code"** —
`invalid_code`, `rate_limited`, `forbidden`, `validation_failed`. Both web and admin returned that
field straight out of `apiErrorMessage` (**REG-028**), so the message shown to someone who mistyped
an OTP was the literal string `invalid_code`, and a moderator refused an action was shown
`forbidden`. **Every failed request on both surfaces went through that one function**, which is why a
copy defect this small covered this much ground.

Mobile has mapped these codes to sentences since it was built. The new `packages/ui/src/problem-copy.ts`
**copies that wording rather than writing it afresh** — Phase 41 measured what a second vocabulary for
one concept costs, and an error a user hits on the site and then again in the app is exactly where it
would show. A test parses `api_error.dart` and fails on any code where the two maps disagree.

An unrecognised code falls back **by status rather than to the code itself**: an unmapped token is the
same defect, not a lesser one. A `detail` is passed through only when it reads as prose, since the
backend fills it generically for unhandled 500s.

**Terminology was measured and needed nothing**, which is worth recording. No user-visible copy on any
surface uses a term D-271 retires — all seven matches for the retired org vocabulary are comments
documenting the migration itself.

**Next action** — Phase 44.

### Phase 44 — Error Recovery · W 14 · COMPLETE · 100%

The only `error.tsx` in web was on `/u/[username]` (**REG-029**). Every other route — the public
site, the entire signed-in app, checkout, tickets — fell through to Next's built-in screen, which in
production is an unstyled *"Application error: a client-side exception has occurred"* with no
branding, no retry and no navigation. Admin has had these boundaries since it was built, so this was
another case of one surface carrying what the other lacked entirely.

There was no `not-found.tsx` either (**REG-030**), so every `notFound()` rendered Next's default 404.
That page is **not an edge case here**: D-018 routes a hidden-but-real resource to a 404 deliberately,
so it is reached by design rather than by accident. Its copy therefore cannot say the thing does not
exist — that is the exact disclosure the 404 exists to avoid — and a test holds that line.

Each boundary offers **a way out as well as a retry**. `reset()` re-renders the same segment, which
does nothing for a page that is broken rather than briefly failing; a dead end with a spinner is
still a dead end.

`global-error.tsx` writes its colours literally, and says why: it renders when the root layout itself
threw, which is exactly when a stylesheet cannot be assumed to have loaded.

**Next action** — Phase 45.

### Phase 45 — Performance UX · W 12 · COMPLETE · 100%

A Next server segment with no `loading.tsx` **does not stream**. The browser holds the *previous*
page — fully interactive and completely wrong — until the new one's data arrives, so a tap on an
event card looks exactly like a tap that did not register. That is the same failure Phase 42 fixed
on the submit button, one level up (**REG-031**).

Web had seven, all on deep leaf routes under `/host`. The `(public)` group and the `(app)` root —
where nearly all navigation actually happens — had none, across 87 async server pages.

Both new fallbacks are **deliberately generic**. A segment-level fallback covers routes of very
different shapes, and a skeleton that guesses a layout wrong costs more than one that stays vague,
because it shifts under the content that replaces it.

Both **announce** the wait as well as drawing it. A skeleton is invisible to a screen reader, so
without a live region the page simply goes quiet.

The new guard also holds every skeleton inside the 360px content box. REG-017 was every public page
scrolling sideways at 360, and a skeleton is drawn *before* any content exists to constrain it —
which is exactly how `discover/loading.tsx` once shipped a `w-96` (384px) inside a 328px box.

**Next action** — Phase 46.

### Phase 46 — Accessibility Verification · W 16 · IN PROGRESS · 25%

**46.1 (alt text) and 46.2 (contrast) are complete. Keyboard traversal, focus order and
screen-reader flow are not** — resume at 46.3.

**46.2 — contrast: verified, nothing to fix.** `TEXT_TOKENS` already covers `text`, `muted`,
`accentText`, `teal`, `success`, `warning` and `danger`, each measured against every surface step at
the 4.5:1 AA floor in **both** themes, with `borderStrong` held to 3:1 for WCAG 1.4.11. The status
colours were the ones worth checking, since they are used as text on cards throughout — they pass.
Recording this as a measurement rather than an assumption; no new guard was needed because the
existing one is not narrower than it looks.

**Why 46.3 was not attempted here:** keyboard traversal, focus order and screen-reader flow need
verification against a running app, not a source sweep. The guards this program already has cover
specific classes — `DataTable` row operability (28), the `useOverlay` focus trap (9/27), Flutter tap
semantics (33/38B) — but a traversal pass is a different kind of work and needs a session with the
context to do it.

`alt=""` is a claim that an image is decorative and a screen reader should skip it. Four sites made
that claim about images that carry meaning (**REG-032**): the post media grid, where the images *are*
the post, so a reader was told a post existed and never that it had pictures; the event banner on a
shared-event card; and the picture preview in the profile form. The profile cover genuinely is a
backdrop and keeps its empty alt, now paired with `aria-hidden` so the intent sits on the element
rather than being inferred from silence.

Post media has no caption or description field, so the alt says **what is actually known** — "Image 1
of 6 in this post" — rather than inventing a description of the contents. It counts `images.length`
and not the four that fit, because the "+2" badge conveying the rest is a visual device hidden from a
reader.

The new sweep initially flagged **its own explanatory comment**: it stripped `//`, `*` and `{/*` but
not `/*`. That is the fifth occurrence of this trap in the program.

**Next action** — Phase 46.3 (interactive half).

### Phase 47 — Visual Regression Audit · W 16 · IN PROGRESS · 10%

**Only the four pages this program added late have been checked, and only against the 360px floor.**
The screen-by-screen audit across all six widths is untouched and needs a browser.

`error.tsx`, `not-found.tsx` and the two `loading.tsx` files were written in Phases 44–45, after
every responsive pass in the program had already run — so nothing had ever looked at them at any
width. Every width in the four is a `max-w-*` cap or `w-full`; the only fixed ones are `w-48` (192px)
and `w-64` (256px), both comfortably inside the 328px content box at 360. No fixed pixel widths,
which is what REG-017 was.

That matters most for `not-found.tsx` and `error.tsx`: a page that renders **because something already
went wrong** is the worst possible place to also introduce a horizontal scroll.

#### 47.2 — first live pass, and it immediately paid for itself

**REG-034**: `not-found.tsx`'s primary CTA was "Browse events" pointing at **`/events`, a route that
does not exist**. Public discovery lives on the homepage; `/discover` is behind auth. So the 404 page
this program added in Phase 44 had a main button leading to another 404 — and the Phase 44 test
**asserted that exact href**, so the test agreed with the bug. Asserting a link's `href` proves the
markup and never that the target resolves. The test now requires every link on the page to point at
a route that exists.

This is the argument for Phase 47 in one finding: it was invisible to typecheck, lint, 372 tests and
five phases of source review, and it took one page load to see.

Live-verified at 360px: **`/` — no overflow** (`scrollWidth` 360 = `clientWidth`, no element
extending past the viewport).

#### 47.3 — every href cross-referenced against the route list

Literal hrefs: **web 0 broken of 85 routes, admin 0 of 27.** Template-literal hrefs: **web 0 of 36.**
So `/events` was a one-off rather than the tip of a pattern.

**Admin has three unresolved template links (REG-035), all still open:**

| Link | Site | Problem |
|---|---|---|
| `` `/e/${e.slug}` `` | `event-workspace-sheet.tsx:272` | `target="_blank"`, meant to open the **public event page** — but the href is relative, so it opens `admin-origin/e/slug`, not the web site. The route exists in `web/app/e/[slug]`, on the other surface. |
| `` `/o/${org.slug}` `` | `org-detail-content.tsx:35` | Same defect, public organisation page. |
| `` `/verification/doc/${d.id}` `` | `verification/page.tsx:63` | No such admin route exists at all — `verification/` has only `page.tsx`. A plain 404. |

**All three fixed.** `siteConfig` gained a `webBaseUrl` on the same pattern as `apiBaseUrl`, so the
two public links now cross to the right origin instead of resolving against the console's.

The document link needed no new route: `getVerificationDocViewUrl` already existed and calls
`GET /v1/admin/verification-documents/{id}/view`. The links now resolve server-side, where the staff
token is, rather than pointing at a page that was never built. **D-235 applies** — a document whose
URL fails to resolve renders as unlinked text saying so, never as a link that goes nowhere: on a
verification queue, a reviewer who clicks through to an error learns nothing about whether the
document exists.

That last one is the sharpest of the three. The screen's entire purpose is reading verification
documents, and **every document button on it was a 404**.

**Outstanding:** the rest — 62 web pages, 30 admin pages and mobile's screens at
360 · 414 · 768 · 1024 · 1440 · 1920. Run `npm run dev` in `web/` and `admin/` and work the
inventory row by row.

#### 46.3a — focus order, source-checkable half · VERIFIED CLEAN

**No positive `tabIndex` anywhere** across web, admin and `packages/ui`. A `tabIndex` of 1 or more
overrides the document's natural order and is the classic way traversal breaks — every focusable
element in the product takes its order from the DOM.

`autoFocus` appears at four real sites, each deliberate and correctly scoped: the gate check-in
scanner input (an operator scans repeatedly), admin's command palette and the event workspace sheet's
textarea — both carrying an `eslint-disable` with a stated reason — and `phone-field`'s opt-in prop,
which is a parameter rather than a default. In every case focus moves into an overlay or a scanner,
which is where it belongs.

**Still outstanding (46.3b):** actual traversal — tabbing every screen, confirming focus is visible at
each stop, that it returns sensibly when an overlay closes, and that a screen reader's reading order
matches the visual one. That needs a running app and cannot be inferred from source.

### Phase 48 — Functional Regression · W 12 · COMPLETE · 100%

All four suites re-run on `2129c33`, the first full backend run since the program began:

| Suite | Floor | Measured |
|---|---|---|
| backend | 1432 pass / 1 skip / 0 fail | **1432 / 1 / 0** — 5 m 32 s |
| web | 372 | **372** |
| admin | 27 | **27** |
| mobile | 327 | **327** |

**Nothing regressed anywhere across 55 phases.** The one skip is the pre-existing
`RefundLedgerTests.Concurrent_refunds_of_the_same_order_reverse_it_only_once`, skipped before this
program started.

`git diff de38963..HEAD -- backend/` returns **empty** — the backend is byte-for-byte unchanged since
the branch point, which is the stronger claim than "the tests still pass" and the one the
contract-freeze actually rests on. Every fix in this program landed in web, admin, mobile or
`packages/ui`.

**What this phase does not cover:** live journey verification — walking checkout, ticket transfer and
a moderation decision against a running stack. That needs a browser and an emulator; the suites prove
no *contract* regressed, not that a journey still feels right end to end. Phase 47 is where that
belongs.

**Next action** — Phase 49.

### Phase 49 — Dead UI Cleanup · W 8 · COMPLETE · 100%

Deleted `web/app/(app)/host/templates/{certificates,editor,invites}` — three `WorkflowPage`
placeholders with **no inbound link anywhere**. The Workspace tile that once pointed at
`/host/templates` was removed in Phase 21, and the parent path never had a page at all, so these
were reachable only by typing the URL. Web is 65 → 62 static pages, which is exactly the three.

**`MyDevicesPage` was not dead, and the queue was wrong about it.** Phase 38 listed it for deletion;
`mobile/lib/features/profile/presentation/pages/profile_page.dart:174` pushes to `/profile/devices`,
so it is reachable from the profile screen. Deleting it on the strength of the queue entry would have
removed a live screen from a shipped app. **A queue entry is a lead, not a verdict** — the inbound
check is what decides, and it has to be re-run at deletion time rather than trusted from whenever the
entry was written.

The orphaned-admin-routes item from Phase 26 was **not** re-checked in this phase and remains open;
it is carried into Phase 50 rather than silently dropped.

**Next action** — Phase 50.

### Phase 50 — Final Product Polish · W 14 · IN PROGRESS · 30%

**Only the carried-forward admin-routes item is done.** The rest of the polish pass is not attempted.

Sweeping admin's console routes for inbound links found three candidates. Two were false positives —
`/users/[id]` and `/competitions/[stageId]` are linked through template literals a literal-string
grep cannot see, which is worth noting as the trap in this kind of sweep. The third was real
(**REG-033**): **`/events/review`** — the full moderation review flow, with claim, notes and reason
codes — existed and worked, and nothing linked to it. Not the nav, not the workspace sheet that
*names it in a comment*. It was reachable only by typing the URL.

Its role gate was correct **by accident**. With no `NAV` entry, Phase 26's `requiredRolesFor` falls
back to the longest matching prefix, `/events`, which is also `VerificationReviewer` — so the page
was gated correctly for a reason that had nothing to do with intent. It now has an explicit entry
beside its sibling `/events/pending`, which makes the gate deliberate as well as right.

This is the counterpart to Phase 49's finding, and the pair is the real lesson: **a route with no
link is not necessarily dead, and a route that is linked is not necessarily reachable by the people
who need it.** Phase 49 nearly deleted a live screen; Phase 50 found a live screen with no door.

**Not done:** the remaining polish sweep across the three surfaces.

#### 50.2 — web orphan sweep · LEADS RECORDED, NOT ACTED ON

Phase 50.1 ran orphan detection over admin's routes; **it was never run over web's**. Doing so found
**13 of 85 static routes with no inbound reference** anywhere in `web/app`, `web/components` or
`web/lib`:

`/host` · `/host/admin/{claims,fraud,orgs,verifications}` · `/host/attendees` · `/host/certificates` ·
`/host/forms` · `/host/notifications` · `/host/payouts` · `/host/risk` · `/host/settings` ·
`/onboarding`

Spot-checking `/host`, `/host/payouts` and `/onboarding` confirmed no inbound link. **They are still
only leads.** Two reasons not to act on them here:

1. **Dynamic routes were excluded from the scan**, and the `/host` tree is entered through them —
   `/host/events/[id]/tickets` and siblings are live. So `/host` may be an orphaned *index* over a
   working subtree, which is a different finding from a dead branch and has a different fix.
2. **Phase 49 is the precedent.** `MyDevicesPage` was queued for deletion on exactly this kind of
   evidence and turned out to be reachable from the profile screen. A queue entry is a lead, not a
   verdict, and the check has to be re-run at deletion time.

Each of the 13 needs one question answered before anything happens: *is this reachable by a route
pattern the scan could not see, and if not, should it gain a link or lose its files?* That is the
work of 50.3.

#### 50.3 — the 13 adjudicated

Each was re-checked for *any* reference in any form — literal, template, comment, import.

**12 have genuinely zero inbound references:** `/host/admin/{claims,fraud,orgs,verifications}`,
`/host/attendees`, `/host/certificates`, `/host/forms`, `/host/notifications`, `/host/payouts`,
`/host/risk`, `/host/settings`, `/onboarding`. The two that appeared to have one reference each were
false positives — an unrelated `OnboardingForm` import, and a comment in `notifications-feed.tsx`
naming `/host/notifications` in prose. **No host nav component exists** (`web/components/host/*nav*`
matches nothing), so nothing constructs these paths dynamically either.

**1 is ambiguous:** `/host` itself. Its 66 matches are all prefixes of live child routes
(`/host/events/[id]/…`), so the *index* may be unreachable while the subtree it heads is fully in
use. It needs a different answer from the other 12 — a link, most likely, not deletion.

**Deliberately not executed.** Deleting 12 route trees is a large, hard-to-review change, and the
evidence — however consistent — is the same *kind* that Phase 49 nearly acted on wrongly with
`MyDevicesPage`. The difference is that this time the check has been run exhaustively and recorded,
so whoever executes it starts from a verified list rather than a stale queue entry. Each deletion
should still re-run the reference check at the moment it happens.

**Recommendation:** delete the 12 (with their `loading.tsx` siblings, four of which exist under
`/host/{attendees,certificates,payouts}` and `/host/analytics`), and give `/host` a link from the
Workspace or fold it into the events list.

#### 50.4 — DEF-012 executed

The reference check was **re-run immediately before deletion**, not trusted from 50.3: all 12 came
back clear, with comments, `import` lines and path-prefix matches excluded from what counts as a
reference. Then deleted. **`/host` was not touched** — its index and its live subtree (`analytics`,
`announcements`, `events`, `invitations`, `representing`, `tickets`, `verification`) are intact.

Web is **65 → 51 static pages**. The 14-page drop against 12 route trees is the nested pages under
`/host/admin/*` going with their parents.

**A latent defect surfaced doing this.** `tsc` was failing on `test/media-alt.test.tsx` — a fixture
cast `as never` in Phase 46 to sidestep a type error, which then made `.length` invalid. **372 tests
passed the whole time**, because vitest transpiles without typechecking, so the suite could never
have caught it. The fixture is now typed as `PostMedia` properly. Worth noting that the surface
`npm test` reports green on is narrower than it looks.

#### 50.5 — `/host` needs no link, and should not get one

50.3 called `/host` an orphaned index over a live subtree and recommended giving it an inbound link.
**That was wrong.** `/host/page.tsx` is four lines: `redirect("/workspace")`. It is a legacy address
kept alive so old links and bookmarks still resolve, after D-267 replaced the organization dashboard
with the user's own event list.

So being unreferenced is **correct and intentional** for this route, not a gap. Linking Workspace to
`/host` would send a user to a redirect that returns them to Workspace.

Both available actions on it would have been wrong, which is the point worth keeping: deleting it
would 404 every old bookmark, and linking it would build a loop. A route with no inbound reference
is not automatically a defect — a redirect stub's whole job is to be reachable only from outside the
app, where a reference sweep cannot see the callers.

### Phases 46.3b + 47.2 — live passes · web public surface

Both dev servers were started and the public surface driven through same-origin iframes, which lets
one evaluate call cover many pages at many widths.

**47.2 — six-width sweep: 66 combinations, 0 overflows.** 11 routes (`/`, `/about`, `/features`,
`/pricing`, `/blog`, `/support`, `/contact`, `/terms`, `/privacy`, `/login`, plus a 404) × 360 · 414 ·
768 · 1024 · 1440 · 1920. REG-017's defect class has not returned anywhere on the public site.

**46.3b — focus visibility: found REG-036, the largest single-component defect in the program.**
`packages/ui/src/button.tsx` carried **no focus declaration of any kind** — no `focus`, no
`focus-visible`, nothing. The shared `Button` is used across web *and* admin, so every button in the
product was invisible to a keyboard user (WCAG 2.4.7, Level AA). Auditing the rest of the primitives
found four more: `chip.tsx`, `pagination.tsx`, `form.tsx`'s error-summary links and `user-card.tsx`'s
profile link. All five fixed with the declaration the hand-rolled buttons already used.

**Confirmed with a real Tab key.** Scripted `.focus()` cannot validate this — Chrome does not match
`:focus-visible` for a programmatic focus on a click-focusable element, so a button looks ringless to
that method no matter what it carries. The way around it is to script-focus the *predecessor* and
then press one genuine Tab, which makes the transition keyboard-initiated:

```
focused:             BUTTON "First time? Sign in with"
matches :focus-visible: true
outline:             rgb(240, 118, 43) solid 2px, offset 2px
```

`rgb(240, 118, 43)` is `accent` exactly, so the ring is drawn from the token rather than a
hand-written colour.

#### 46.3c — focus return on overlay close

**Verified.** `useOverlay` records `document.activeElement` on open and restores it on close, and
`web/test/focus-return.test.tsx` now holds that: focus moves into the panel on open, and returns to
the trigger when the dialog is closed from inside. The restore is deliberately conditional — it fires
only when focus is on `body`, nowhere, or still inside the panel, so a dialog that intentionally sent
focus elsewhere is not overridden. A trap without restore is half the pattern: focus falls to `body`
and the next Tab restarts from the top of the document.

**DEF-013 resolved: Escape works. It was a jsdom artifact, not a defect.** No overlay on the public
site is reachable without a session, so this needed a temporary probe route rendering a `Dialog` —
created, measured, and deleted in the same session. In Chrome:

```
dialogStillOpen:  false
active:           BUTTON "Open probe"     ← focus returned to the trigger
outline:          rgb(240, 118, 43) solid 2px
```

Escape dismissed the dialog, focus returned to the opener, and the ring rendered on it — all three
behaviours in one keypress. jsdom simply does not deliver a synthetic Escape to `useOverlay`'s
document-level capture listener, so the test would have reported a defect that does not exist.

**The skipped test is kept, with the browser result recorded in it.** Escape dismissal has no
automated coverage on this surface, and hiding that by deleting the test would make the gap
invisible rather than absent.

#### 47.3 — the signed-in app swept clean at all six widths

**60 combinations, 0 overflows.** Ten signed-in routes — `/workspace`, `/discover`, `/tickets`,
`/certificates`, `/groups`, `/notifications`, `/settings`, `/host/events/new`, `/host/verification`,
`/host/tickets` — at 360 · 414 · 768 · 1024 · 1440 · 1920. With the public sweep, **126 page-width
combinations measured and not one horizontal scroll.** REG-017's defect class is closed on both
surfaces of web.

Getting there took the whole stack, and the route is written down because the next run should not
rediscover it:

1. Start Postgres and the API; `POST /v1/auth/otp/request`, read the code from the API log
   (`code is NNNNNN`), `POST /v1/auth/otp/verify`.
2. **A fresh user is bounced to `/register`.** `needs_onboarding` is true while `Name` is empty *or*
   `Username` is null (D-037), so clear it with `PATCH /v1/me/profile {name, username}` — one call,
   no UI needed.
3. Set **both** cookies. `currentSession()` returns null unless `kurx_access` *and* `kurx_refresh`
   are present, so injecting only the access token fails in a way that looks exactly like an expired
   session. This is what blocked the previous attempt.
4. Sweep through a **sandboxed** same-origin iframe (`allow-same-origin allow-scripts`). Without the
   sandbox one signed-in route navigated the *top* window mid-run and destroyed the execution
   context.

**A Phase 49 deletion was re-validated on the way.** `/onboarding` was among the 12 orphans removed,
and an auth redirect into it would have been the worst possible false positive — a new user hitting a
404 on their first screen. It is not one: `otp-panel.tsx` routes a new user to **`/register`**, which
is intact, and that path was exercised live here.

#### 47.4 — admin: staff session works, iframe technique does not

Seeding staff turned out to be one SQL insert — `platform_roles` is
`(Id, UserId, Role, GrantedBy, GrantedAt, ExpiresAt)`, so granting `SuperAdmin` to an existing user is
a single statement, and `GET /v1/me` then returns `platform_roles: ['SuperAdmin']` live. Admin uses
**the same `kurx_access` / `kurx_refresh` cookie names as web**, so the session recipe from 47.3
transfers unchanged. Every console route returned 200.

**But the sweep did not run, and the result that looked clean was a false green.** The batch reported
*0 overflows* alongside **`skipped: 96`** — every combination. `contentDocument` was null throughout:
**admin refuses to be framed** (`X-Frame-Options`/CSP `frame-ancestors`), which is correct posture for
a console and is not a defect. The 96 console errors were the frame refusals. Reporting that run as a
pass would have recorded the entire admin console as swept when **nothing was measured** — the exact
failure mode this program has hit with a self-matching guard and a test asserting a broken href, and
the reason the batch reports `skipped` at all.

**One page measured directly** (top-level navigation, not framed): `/users` at 360 —
`scrollWidth` 360 = `clientWidth`, no element past the viewport. The densest table in the console,
clean at the tightest width.

**Admin's remaining 95 combinations need top-level navigation**, roughly two calls per page-width.
The iframe shortcut that made web cheap is unavailable here by design.

**Not covered:** admin's other 15 routes at six widths, mobile, and screen-reader reading order.

---

## Verification Ledger

Real commands, discovered from `package.json` / `.github/workflows/ci.yml` / `pubspec.yaml`.
None invented.

```bash
# web
cd web   && npm run typecheck && npm run lint && npm test && npm run build

# admin  (no test script exists — browser verification is the substitute)
cd admin && npm run typecheck && npm run lint && NEXT_PUBLIC_API_BASE_URL=http://localhost:5001 npm run build

# mobile
cd mobile && flutter analyze --no-fatal-infos && flutter test

# backend (full suite only — order-sensitive; run when a change could touch a contract, and in Phase 48)
export DOTNET_ROOT=$HOME/.dotnet
cd backend && dotnet build Kurx.sln --no-restore -c Release -warnaserror /nowarn:NU1900 \
           && dotnet test Kurx.sln --no-build -c Release
```

### Baseline — commit `c66ae16`, measured 2026-08-06

| Suite | Command | Result |
|---|---|---|
| web typecheck | `npm run typecheck` | ✅ exit 0 |
| web lint | `npm run lint` | ✅ exit 0 |
| web test | `npm test` (vitest) | ✅ **8 files, 145 tests, 145 passed** |
| web build | `npm run build` | ✅ compiled, **65 static pages** |
| admin typecheck | `npm run typecheck` | ✅ exit 0 |
| admin lint | `npm run lint` | ✅ no warnings or errors |
| admin build | `npm run build` | ✅ compiled, **29 static pages** |
| admin test | — | ⚪ **no test script exists** |
| mobile analyze | `flutter analyze --no-fatal-infos` | ✅ **No issues found** |
| mobile test | `flutter test` | ✅ **275 tests, all passed** |
| backend build | `dotnet build -c Release -warnaserror` | ✅ 0 warnings, 0 errors |
| backend test | `dotnet test` | ✅ **1433 total — 1432 passed, 1 skipped, 0 failed** (5 m 53 s) |

### Latest full run — 2026-08-08, after Phase 48 (all four suites, backend included)

| Suite | Baseline | Now | Δ |
|---|---|---|---|
| web typecheck / lint / build | clean · 65 pages | clean · 65 pages | — |
| web tests | 145 | **372** | **+227 contract guards** |
| admin typecheck / lint / build | clean · 29 pages | clean · 29 pages | — |
| admin tests | **none existed** | **27** | **+27 — the suite itself is new (Phase 27)** |
| mobile analyze | No issues | No issues | — |
| mobile tests | 275 | **327** | **+52** |
| backend build | 0 warnings, 0 errors | **0 warnings, 0 errors** | — |
| backend tests | 1432 / 1 skip / 0 fail | **1432 / 1 skip / 0 fail** (5 m 32 s) | — |

No suite regressed. Backend is byte-for-byte unchanged, as intended — no file under
`backend/` was touched by any phase.

**Session-start re-measurement (2026-08-07, on `de38963`)** — all four suites were re-run before
Phase 18A began, to confirm the repository matched the tracker after the phases 0–18 merge:
web 265 tests / 65 pages, admin 29 pages, mobile 307 tests + clean analyze. Every figure matched
`docs/ui-ux/HANDOFF.md`'s floors exactly.

**The one pre-existing lint warning is closed.** `web/components/host/module-picker.tsx:55` put
`aria-disabled` on an implicit `listitem` role; Phase 21 removed it (the row already said the same
thing in words). `next lint` now reports **no warnings and no errors** across web and admin.

> **Backend baseline is 1433, not the 1297 recorded in `.claude/CLAUDE.md` §9.** That figure was
> measured on an isolated worktree at `HEAD`+D-274 on 2026-08-05; this branch sits at `c66ae16`,
> which carries additional merged work. **1433 (1432 pass / 1 skip / 0 fail) is the number Phase 48
> must meet or beat.** The single skip is
> `RefundLedgerTests.Concurrent_refunds_of_the_same_order_reverse_it_only_once` and is pre-existing.

**Environment notes**
* Local Flutter is **3.44.5**; CI pins **3.44.6**. Analyze/test agree; noted so a CI-only difference
  is not mistaken for a redesign regression.
* `mobile` analyze is **clean**, not the 62 style infos the old `docs/REMAINING_WORK.md` described. That doc
  is a superseded snapshot; the infos were cleared by D-212.
* `dotnet` needs `/nowarn:NU1900` locally: the sandbox blocks `api.nuget.org`, so the package
  *vulnerability audit* fails and `-warnaserror` promotes it. This is an environment artifact, not a
  code defect — CI has network and does not need the flag.
* `admin/node_modules` was absent; installed with `npm ci` during 0.2.

---

## Screen Migration Matrix

Maintained in three files, one per surface, so 206 rows stay navigable:

| Surface | File | Rows |
|---|---|---:|
| Web | [`docs/ui-ux/inventory-web.md`](docs/ui-ux/inventory-web.md) | 88 |
| Admin | [`docs/ui-ux/inventory-admin.md`](docs/ui-ux/inventory-admin.md) | 27 |
| Mobile | [`docs/ui-ux/inventory-mobile.md`](docs/ui-ux/inventory-mobile.md) | 91 |

Status vocabulary: `Legacy` · `Foundation applied` · `Partially migrated` · `Redesigned` ·
`Verified` · `Blocked` · `N/A`. All rows currently `Legacy`.

---

## Component Migration Matrix

Baseline call-site counts measured 2026-08-06. `web/components/ui/*.tsx` are **re-export shims**
onto `@kurx/ui` (D-185) — the counts below are import sites still routed through a shim rather than
importing the system directly.

| Component | Surface | Legacy source | New component | Migrated | Remaining | A11y | Status |
|---|---|---|---|---:|---:|---|---|
| Button / LinkButton | web | `@/components/ui/button` shim | `@kurx/ui` | 0 | 67 | ☐ | Legacy |
| Card / Stat | web | `@/components/ui/card` shim | `@kurx/ui` | 0 | 52 | ☐ | Legacy |
| Field / Input / Textarea / Select | web | `@/components/ui/field` shim | `@kurx/ui` | 0 | 6 | ☐ | Legacy |
| Avatar | web | `@/components/ui/avatar` shim | `@kurx/ui` | 0 | 6 | ☐ | Legacy |
| Chip | web | `@/components/ui/chip` shim | `@kurx/ui` | 0 | 2 | ☐ | Legacy |
| Skeleton / EmptyState / MotionPanel | web | shims | `@kurx/ui` | 0 | 3 | ☐ | Legacy |
| *(direct `@kurx/ui` imports)* | web | — | `@kurx/ui` | 37 | — | ☐ | Foundation applied |
| **Field / Input / Textarea / Select** | web | **fork** `web/components/ui/field.tsx` (identical copy) | collapse to shim | 0 | **6 — incl. 5 auth components** | ☐ | **Forked (S1-5)** |
| **Avatar** | web | **fork** `web/components/ui/avatar.tsx` (2 lines diverged) | collapse to shim | 0 | 6 | ☐ | **Forked (S1-5)** |
| **MotionPanel** | web | **fork** (identical copy) | collapse to shim | 0 | 1 | ☐ | **Forked (S1-5)** |
| **Sheet** | web | **fork — stale API**, 32 lines diverged | collapse to shim | 0 | 0 | ☐ | **Forked, dead (S1-5)** |
| **SectionHeader** | web | **fork** (2 lines) | collapse to shim | 0 | 0 | ☐ | **Forked, dead (S1-5)** |
| **Tabs** | web | **fork** (4 lines) | collapse to shim | 0 | 0 | ☐ | **Forked, dead (S1-5)** |
| **Switch** | web | **fork** (identical copy) | collapse to shim | 0 | 0 | ☐ | **Forked, dead (S1-5)** |
| **Toast** | web | **fork** (identical copy) | collapse to shim | 0 | 0 | ☐ | **Forked, dead (S1-5)** |
| *(direct `@kurx/ui` imports)* | admin | — | `@kurx/ui` | 60 | — | ☐ | Foundation applied |
| DataTable | admin | `@kurx/ui` | `@kurx/ui` v2 (Phase 28) | **15** | 0 | ☑ | **Redesigned** |
| PageHeader | admin | `@/components/layout/page-header` | **stays in admin (Phase 28)** — web has no consumer; promoting one caller is speculative abstraction | 23 | 0 | ☑ | **Redesigned** |
| EventCard | web | `web/components/events/event-card.tsx` | `@kurx/ui` (Phase 10.2) | 0 | TBD @10.2 | ☐ | Legacy |
| EventCard | mobile | `event_card.dart` + `compact_event_card.dart` | unified (Phase 34) | 0 | TBD @34 | ☐ | Legacy |
| PostCard | web / mobile | `posts/post-card.tsx`, `post_card.dart` | Phase 20A / 38A | 0 | TBD | ☐ | Legacy |
| TicketCard | web / mobile | none — inline markup | new (Phase 10.4) | 0 | TBD | ☐ | Missing |
| Modal / Dialog | web / admin | `@kurx/ui` Dialog, ConfirmDialog | hardened (Phase 9) | 0 | TBD @9.1 | ☐ | Legacy |
| Sheet / Drawer | web | `@kurx/ui` Sheet | Phase 9.2 | 0 | TBD | ☐ | Legacy |
| Navigation | web | `layout/app-shell.tsx` | Phase 11 | 0 | 1 | ☐ | Legacy |
| Navigation | admin | `layout/{sidebar,topbar}.tsx` | Phase 27 | 0 | 2 | ☐ | Legacy |
| Navigation | mobile | `shell/app_shell.dart` `_KurxNavBar` | Phase 33 | 0 | 1 | ☐ | Legacy |
| SearchBar / FilterBar | web / admin | `@kurx/ui` | Phase 12 / 28 | 0 | TBD | ☐ | Legacy |
| KurxButton / Card / Chip / TextField | mobile | `common/widgets/kurx_*.dart` | Phase 32.3 | 0 | TBD | ☐ | Legacy |

**Primitives that do not exist yet** and are created in Phases 6–9: IconButton, Link, Checkbox,
Radio, Slider, date/time controls, Divider, Tooltip, Progress, Alert, Banner, Popover, Dropdown,
ContextMenu, Drawer, command palette, Breadcrumbs.

**Deletion rule:** no legacy component is removed until `Remaining = 0` **and** Phase 48 is green.
Deletions happen only in Phase 49, in their own commit.

---

## Design Token Migration

| Token area | Web | Admin | Flutter | Verified |
|---|---|---|---|---|
| Colors | ⚠️ 3 duplicate copies | 🟢 consumes preset | 🟢 `KurxColors` | ☐ |
| Semantic colors | ⬜ 9 raw tokens only | ⬜ | 🟡 partial | ☐ |
| Typography | ⬜ none | ⬜ none | 🟡 Plus Jakarta Sans | ☐ |
| Spacing | ⬜ none | ⬜ none | 🟢 `KSpace` | ☐ |
| Sizing | ⬜ none | ⬜ none | ⬜ | ☐ |
| Radius | ⬜ none | ⬜ none | 🟢 `KRadius` | ☐ |
| Borders | ⬜ none | ⬜ none | 🟡 single token | ☐ |
| Shadows | ⚠️ one (`shadow-github`) | ⚠️ same | 🟢 `kCardShadow` | ☐ |
| Opacity | ⬜ | ⬜ | ⬜ | ☐ |
| Z-index | ⬜ ad-hoc `z-20`/`z-30` | ⬜ ad-hoc | n/a | ☐ |
| Motion (duration/easing) | ⬜ none | ⬜ none | 🟢 `KMotion` | ☐ |
| Breakpoints | ⬜ Tailwind defaults | ⬜ Tailwind defaults | ⬜ | ☐ |

**⚠️ The colour duplication is a live defect** and is Phase 4.0's first job:

| File | Role |
|---|---|
| `packages/ui/src/styles/tokens.css` | intended source of truth |
| `packages/ui/tailwind-preset.cjs` | intended source of truth |
| `admin/tailwind.config.js` | ✅ `presets: [kurxPreset]` — correct |
| `web/tailwind.config.ts` | ❌ inlines a verbatim copy of the preset |
| `web/app/globals.css` | ❌ inlines a verbatim copy of `tokens.css` |
| `web/lib/design-tokens.ts` | ❌ a third hardcoded hex copy |

---

## Critical Journey Tracker

`✅` = journey exists on that surface. Columns Functional / Visual / A11y are ticked per surface as
each is verified.

| Journey | Web | Admin | Mobile | Phases | Functional | Visual | A11y | Status |
|---|:--:|:--:|:--:|---|:--:|:--:|:--:|---|
| Discover events | ✅ | — | ✅ | 13, 34 | ☐ | ☐ | ☐ | Legacy |
| Search events | ✅ | — | ✅ | 12, 34 | ☐ | ☐ | ☐ | Legacy |
| Filter events | ✅ | — | ✅ | 12, 34 | ☐ | ☐ | ☐ | Legacy |
| View event | ✅ | ✅ | ✅ | 15, 29.3, 35 | ☐ | ☐ | ☐ | Legacy |
| Login (OTP request) | ✅ | ✅ | ✅ | 16, 29.8, 36 | ☐ | ☐ | ☐ | Legacy |
| OTP verification | ✅ | ✅ | ✅ | 16, 36 | ☐ | ☐ | ☐ | Legacy |
| Password / 2FA / passkey | ✅ | ✅ | ✅ | 16, 36 | ☐ | ☐ | ☐ | Legacy |
| Account registration | ✅ | — | ✅ | 16, 36 | ☐ | ☐ | ☐ | Legacy |
| Onboarding | ✅ | — | ✅ | 17, 36 | ☐ | ☐ | ☐ | Legacy |
| Ticket selection | ✅ | — | ✅ | 19, 37 | ☐ | ☐ | ☐ | Legacy |
| Checkout / payment | ✅ | — | ✅ | 19, 37 | ☐ | ☐ | ☐ | Legacy |
| Ticket view + QR | ✅ | — | ✅ | 19, 37 | ☐ | ☐ | ☐ | Legacy |
| Ticket transfer / refund | ✅ | — | ✅ | 20, 37 | ☐ | ☐ | ☐ | Legacy |
| Invitation → RSVP | ✅ | — | ✅ | 20D, 38B | ☐ | ☐ | ☐ | Legacy |
| Profile & settings | ✅ | ✅ | ✅ | 18, 29.8, 38 | ☐ | ☐ | ☐ | Legacy |
| Public profile / allies | ✅ | — | ✅ | 18A, 38B | ☐ | ☐ | ☐ | Legacy |
| User events (Workspace) | ✅ | — | ✅ | 20, 38B | ☐ | ☐ | ☐ | Legacy |
| Create event | ✅ | — | ✅ | 22, 39 | ☐ | ☐ | ☐ | Legacy |
| Edit event | ✅ | — | ✅ | 23, 39 | ☐ | ☐ | ☐ | Legacy |
| Event draft / autosave | ✅ | — | ✅ | 22, 39 | ☐ | ☐ | ☐ | Legacy |
| Event authorization (representative) | ✅ | ✅ | ✅ | 24, 30, 39 | ☐ | ☐ | ☐ | Legacy |
| Host: attendees & check-in | ✅ | — | ✅ | 21.3, 39 | ☐ | ☐ | ☐ | Legacy |
| Host: ticket types & forms | ✅ | — | ✅ | 21.4, 39 | ☐ | ☐ | ☐ | Legacy |
| Host: payouts / wallet / KYC | ✅ | — | ✅ | 21.8, 39 | ☐ | ☐ | ☐ | Legacy |
| Admin review queue | — | ✅ | — | 30 | ☐ | ☐ | ☐ | Legacy |
| Admin moderation | — | ✅ | — | 29.7, 30 | ☐ | ☐ | ☐ | Legacy |
| Admin real-time event feed | — | ✅ | — | 31 | ☐ | ☐ | ☐ | Legacy |
| Posts: read / compose / interact | ✅ | — | ✅ | 20A, 38A | ☐ | ☐ | ☐ | Legacy |
| Chat: room, presence, attachment | ✅ | — | ✅ | 20B, 38A | ☐ | ☐ | ☐ | Legacy |
| Certificates | ✅ | ✅ | ✅ | 20C, 29.6, 38B | ☐ | ☐ | ☐ | Legacy |
| Notifications | ✅ | — | ✅ | 20D, 38 | ☐ | ☐ | ☐ | Legacy |

---

## Regression Ledger

Resolved rows are **kept**, never deleted.

| ID | Phase | Surface | Problem | Severity | Root cause | Fix | Verification | Status |
|---|---|---|---|---|---|---|---|---|
| REG-001 | 2 → fixed in 12 | web | After successful account recovery the user is pushed to `/account/security`, **which does not exist** — a 404 at the last step of a security-critical journey | **S1** | `recovery-panel.tsx:50` targets a route that was never created; the real one is `/settings/security` | Correct the target | Load `/recover`, complete flow, expect `/settings/security?recovered=1` | ✅ **Fixed** `04b55f0` — now `/settings/security` |
| REG-002 | 2 → fixed in 12 | web | **Every** link from an invitation to its event 404s — `/events/{slug}` is not a route; web serves event detail at `/e/[slug]` | **S1** | 3 call sites: `invitations/page.tsx:72`, `:100`, `redeem-invite-link.tsx:40`. `event_slug` is a real non-null field (`api.ts:1086`), so only the path is wrong | Correct all 3 to `/e/{slug}` | Open `/invitations`, click through to an event | ✅ **Fixed** `04b55f0` — all 3 sites now `/e/{slug}`; the anchor was also white-on-ember at 2.86:1 |
| REG-005 | found in 15 | web | An "Event QR code" card rendered a lucide glyph, not a code — `qrcode.react` is a declared dependency used **nowhere** in web | S3 | Placeholder never implemented | Removed rather than implemented: whether it meant the share URL or a check-in code is a product question, and the adjacent deep-link button already covers the former | Event page renders no fake code | ✅ **Resolved by removal** `5555e6d` |
| REG-004 | found in 10 | web | The event card's bookmark button had an `aria-label`, a hover colour and **no `onClick`** — a dead control advertising itself as interactive, on the most-rendered card in the product | S2 | Never wired; `EventSummary` also carries no saved field for it to reflect | Removed rather than reconnected — wiring one would be inventing product behaviour | `EventCard` renders one link and no dead controls | **Open — pre-existing**, owner Phase 13 |
| REG-003 | 2 → fixed in 20 | web | `/workspace` links to `/host/templates`, which has no page | S2 | Only `/host/templates/{certificates,editor,invites}` exist; no index — and all three are `WorkflowPage` placeholders | Tile removed rather than repointed: sending a host to a placeholder is not better than not offering it, and building the index is Phase 21's call | Open `/workspace`; no Templates tile, no 404 | ✅ **Fixed in 20** |
| REG-006 | found in 18A | web | `/o/[slug]` states an organization's event count with no way to see the events. Two cards restated the counts above them and led nowhere | S3 | No public events-by-organization surface exists. `EventSearchParams` does support `orgId` and the API accepts it, but the only page rendering event search (`/discover`) is inside the `(app)` group behind `requireSession()` **and does not read the parameter** — so linking a public org page there would send anonymous visitors to a login wall | Not invented. The dead cards were collapsed to one honest statement; wiring a public events-by-organization view is product scope needing a `D-NNN` | `/o/{slug}` renders no control that leads nowhere | **Open — pre-existing**, owner Phase 50 |
| REG-007 | found in 18A | web | `ToastProvider` was mounted **nowhere in web**, so the feedback channel Phase 8 built was inert across all 88 web routes — `useToast()` fell through to the context's no-op default | S2 | Phase 8 wired the provider into admin's `AppShell` and recorded the fix's reach as "web + admin"; web's `Providers` was never touched, and no web call site used `useToast` so nothing surfaced it | Mounted in `web/components/layout/providers.tsx`, at the root rather than in `AppShell` — `/u`, `/o` and `/e` render outside that shell | Toast text appears in the live region after an ally action (`profile-identity.test.tsx`) | ✅ **Fixed in 18A** |
| REG-008 | found in 18A | web | The app nav's "Community" item navigated the user **out of the navigation** — `/allies` called `requireSession()` from outside every shell, so it rendered with no nav, no skip link and no way back | S2 | `web/app/allies/` sat at the top level rather than in the `(app)` route group; it was the only auth-gated web route outside it | Moved to `web/app/(app)/allies/`, dropping its own `<main>` since `AppShell` provides the landmark | `/allies` builds at the same route and renders inside `AppShell` | ✅ **Fixed in 18A** |
| REG-009 | found in 19 | web | **Web's checkout could not take a payment and reported that it had.** `BookingForm` opened Razorpay with no `order_id` and no amount, discarded the payment response without verifying it, and navigated to `/tickets` whatever happened — including when the Razorpay script never loaded | **S1** | `web/lib/api.ts` has no order-creation function; the page was a placeholder never wired to `POST /v1/events/{eventId}/orders`, which the backend has fully (Idempotency-Key, guest checkout D-036, group codes) and **Flutter implements correctly** | Not implemented here — a payment integration is feature work on the money path needing its own `D-NNN` and a security review. **Decision taken with the user:** the false success is removed. `BookingForm` deleted; `/book/[slug]` is an honest hand-off showing the event and pointing at the client where booking works; both event CTAs relabelled "Book in the app" | `/book/{slug}` opens no payment sheet and claims no purchase | ⚠️ **Neutralised in 19 — the web booking journey remains unbuilt.** Owner: a product decision + `D-NNN` |
| REG-010 | found in 19 | web | `DownloadApp` renders a 112px lucide `QrCode` glyph captioned "QR code for mobile download" — a picture of a QR, not one — and its "Continue in Browser" button links to the `kurx://` **app** deep link, so the control does the opposite of its label | S3 | Same placeholder class as REG-005; the mislabelled button was never re-read after the deep link was added | Not fixed here — `web/components/marketing/download-app.tsx` is Phase 13A's file and 13A is closed | Marketing surfaces render no fake code and no mislabelled control | **Open — pre-existing**, owner Phase 50 |
| REG-011 | found in 20B | web | **20 colour classes across 8 files named tokens that do not exist** — every incoming chat bubble rendered with no background and no text colour, and every auth error message (login, recovery, passkey, `/verify`) was styled `text-destructive` and therefore was not red | **S2** | shadcn's vocabulary (`card`, `fg`, `destructive`, `muted-foreground`) is not in the Kurx preset, and Tailwind emits nothing for a class it cannot resolve. Not a type error, not a lint error, not a build error — invisible to every gate this program runs | All 20 mapped onto real tokens, and `design-tokens.test.ts` now walks `app/`, `components/` and `lib/` and fails on any colour class outside the palette | Revert one class; the test names the file and goes red | ✅ **Fixed in 20B** |
| REG-012 | found in 21 | web | **13 irreversible deletes fired on a single click** across the host surface — a ticket type with sales against it, an issued certificate, a circulating invite link, and an entire draft event with its schedule, ticket types and registration form. The draft delete also swallowed its own failures | **S2** | Each was a bare `<form action={deleteXAction}>` around a `<Button type="submit">`. `ConfirmDialog` had exactly **one** call site in all of web before this pass | `ConfirmSubmitButton` puts the gate in front while leaving the native submit intact (`requestSubmit()` on the owning form), so the server action and its revalidation are untouched. The draft delete also surfaces refusals — re-throwing the `NEXT_REDIRECT` digest so its own success path is not caught as an error | Replace one `ConfirmSubmitButton` with a plain submit; the sweep test names the file and line | ✅ **Fixed in 21** |
| REG-013 | found in 21 | web | **Phase 7's S1-1 fix never reached the host surface.** 160 hand-rolled form controls across 21 files, and `<Field>` used **zero** times — so hints and errors sat in `<p>` siblings no control referenced, and a screen-reader user focused on a field was told nothing about what to put in it | **S1** | The host surface built its own: one class string copy-pasted into **20 local constants across 17 files** — 40px against the 44px floor, the *decorative* border token at 1.30:1 where WCAG 1.4.11 wants 3:1, and no focus ring. The S1-5 forked-primitive pattern, in a place Phase 6.6 did not look | One source — `controlClass` exported from `field.tsx` beside the `Field`/`Input` path that should be reached for first (the `chipClass` precedent). The forms carrying real hints were rebuilt onto `Field`; `Field` gained the `className` two call sites had needed | Re-introduce the forked class string; the sweep test names the file | ✅ **Fixed in 21** |
| REG-014 | found in 22 | web | The create-event wizard's **five single-select steps were grids of plain `<button>`s** — no group name, no mutual exclusivity, and the chosen option carried only by `border-accent ring-1 ring-accent`. Separately, `/host/events/new` caught its representation read to `[]`, so an outage offered **Personal alone** | **S2** | Rich selection cards were hand-built rather than composed from the system's `RadioGroup`; and `.catch(() => [])` on a read whose emptiness changes what the user creates | Real `<input type="radio">` in a `<fieldset>`/`<legend>`, so the browser owns arrow keys, roving tabindex and exclusion; a checkmark added as a second channel. The representation read is classified, and its failure stated before the wizard opens | Choosing an option flips `checked` on the input, not a class; the failed-read notice names Personal as the only remaining choice | ✅ **Fixed in 22** |
| REG-015 | found in 23 | web | **`EditEventForm`'s error path was dead code.** It renders `state && "error" in state`, but `updateEventAction` never returned one — it awaited bare, so a rejection propagated out of the server action and a failed save took the whole page to an error boundary. Its `{ ok: true }` success was rendered nowhere either, so a save that worked looked identical to one that had not run | **S2** | The form was written against a contract the action never delivered; the sibling `createEventWizardAction` already had the try/catch shape | The action returns `{ error }` on failure; both outcomes render above the submit control, `role="alert"` and `role="status"` | Mock the form state as `{ ok: true }` / `{ error }` and assert each is announced | ✅ **Fixed in 23** |
| REG-016 | found in 24 | web | **A failed replacement of the authorization letter kept the previous one attached.** `upload()` set the key and filename only on success, so a replacement that failed at the storage PUT left the old key in place while the error said the upload had failed — and submitting would then have filed the *previous* document under the *new* details. Separately, two fields carried their helper inside the `<label>`, making a twenty-five word paragraph the accessible name of an email box | **S2** | Success-only state updates on a replace path, and `<label>` wrapping both a control and its explanation | The prior attachment is dropped **before** the replacement is attempted; the storage PUT is guarded against a rejected `fetch`; helpers moved to `Field`, which puts them on `aria-describedby` | Upload, then fail a replace: no filename remains attached. Query the email field by its exact name | ✅ **Fixed in 24** |
| REG-017 | found in 25 | web | **Every public page scrolled sideways at 360px — and again at 768px.** The header's action row was 284px beside a 71px logo in a 328px container; at 768 the page-link row appears but needs 869px in a 736px container. Separately `Dialog` had no vertical bound, so on a short viewport (a phone in landscape is 640×360) a confirmation's footer buttons were unreachable | **S2** | The action row was never measured at the 360px floor, and the link row's `md:` breakpoint was chosen without measuring what it costs at exactly 768. `Dialog` centred an unbounded child in a fixed overlay | CTA icon-only below `sm` with tighter gaps; the link row moved to `lg:` with the footer covering 768–1023; `Dialog` bounded to `calc(100dvh-2rem)` with only its body scrolling | Measured in a browser at all six widths across 12 routes: no overflow, no undersized target | ✅ **Fixed in 25** |
| REG-018 | found in 26 | admin | **The console's role-based information architecture was declared in `NAV` and enforced nowhere.** Every page guarded with `requireStaffSession()`, which asks only whether the caller holds *some* platform role — so a Support admin who typed `/staff` reached it, and a Reviewer who typed `/certificates` reached the screen whose own nav comment says they "would get a 403 they cannot resolve" | **S2** | The nav gated by role; no page or layout ever read those gates | `requiredRolesFor(pathname)` resolves a route's roles from `NAV` itself, and the console layout renders `RoleRequired` instead of the page on a mismatch. Not a security fix — the backend was and remains the authority; this stops the console contradicting itself | Role matrix resolved for all 24 console routes against SuperAdmin / Reviewer / Support: **20 now scoped, 0 before** | ✅ **Fixed in 26** |
| REG-019 | found in 27 | admin | **The admin mobile drawer declared `role="dialog"` and `aria-modal="true"` and implemented none of it** — focus never entered the panel, Tab walked onto the page behind, Escape did nothing, focus was never restored to the menu button, and the body kept scrolling | **S1** | Audit **S1-2**, which Phase 9 closed for `Dialog` and `Sheet` by building `useOverlay`. Admin's own navigation was never wired to it | Wired to `useOverlay`. Not `Sheet`: it opens right or bottom only and forces a title header this drawer does not want around a full sidebar | Open the drawer, assert focus enters it, press Escape, assert it closes and focus returns to the menu button | ✅ **Fixed in 27** |
| REG-020 | found in 28 | admin | **`DataTable`'s clickable rows were mouse-only** — no `tabIndex`, no key handler, no role — so the primary action on three admin screens could not be reached from a keyboard. Its `overflow-x-auto` container was not keyboard-scrollable either; every row checkbox shared the name "Select row"; and a partial selection rendered as *unchecked* | **S1** | The row's interactivity lived entirely in an `onClick` on a `<tr>`; `indeterminate` is a DOM property rather than an attribute, so it needs a ref callback | Rows are buttons in everything but tag name, named by `rowLabel`; the scroll region is focusable and named; checkboxes take the row's identity; the header checkbox goes indeterminate; selection and loading are announced | Focus a row, press Enter and Space; select one of two rows and assert the header checkbox is indeterminate | ✅ **Fixed in 28** |
| REG-021 | found in 30 | **all surfaces** | **`Button` set no `type`, so every one inside a form submitted it.** HTML defaults `<button>` to `type="submit"`. `ConfirmDialog` renders inside the form it guards, so its confirm button submitted the form *and* the guard's `requestSubmit()` did — a moderation decision recorded **twice** — while its **Cancel** button submitted the form as well | **S1** | The component spread `...props` onto `<button>` without a default `type`; ~38 call sites pass only `onClick` and relied on a default that inside a form does not do nothing | `Button` defaults to `type="button"`; the 44 call sites that want a submit already say so explicitly | Click a `ConfirmDecisionButton`, confirm, assert the form submitted exactly once | ✅ **Fixed in 30** |
| REG-022 | found in 31 (scan) | mobile | **15 tappable surfaces are bare `GestureDetector`s with no `Semantics`** — no role, no name — so a screen-reader user cannot tell they are controls or what they do. Includes the shared chip's delete affordance, at a **14px** tap target against Flutter's 48dp floor | S2 | The same defect Phase 33 found on the tab bar ("a bare `GestureDetector`, unannounced and untappable to a11y"), never swept beyond it | **2 of 15 fixed**: `kurx_chip` (the primitive every filter and tag is built from, now labelled with a 48dp target) and the ticket card a buyer opens at the gate. The other 13 are listed in the scan and belong to the phases that own their screens (34–39) | `flutter analyze` clean, 307 tests pass | ✅ **Fixed — all 15 closed by Phase 39.** The `remaining` set in `mobile/test/features/tap_semantics_test.dart` is now empty and asserted so |
| REG-023 | found in 35 | mobile | **`CheckoutPage` was fully implemented and nothing navigated to it.** The route existed, `createOrder` was called correctly with an `Idempotency-Key` — and no screen pushed to it, so registration was reachable only by typing a URL | **S1** | The event detail page, which is where a person decides to register, ended at "related events" with no CTA at all. The exact mirror of REG-009 on web: web has the entry point and no working checkout; mobile had the working checkout and no entry point | A register bar built from the `ticketTypes` the page already loads — cheapest available preselected, a "choose a ticket" label when several exist, and an honest closed state when none are | Break the `context.push` to checkout; `checkout_reachable_test.dart` fails | ✅ **Fixed in 35** |
| REG-024 | found in 38 | mobile | **`MyDevicesPage` is an orphan carrying a dead security control.** It is registered in the router and nothing navigates to it, while `security_page.dart` holds real, reachable device revocation. Its only action was a "Remove" button with an empty `onPressed` — telling somebody their device was revoked when nothing happened | **S2** | A duplicate device screen that was never wired up or removed | The inert control is gone so nothing pretends to work. **The page itself is not deleted** — that is Phase 49's, with this row as the orphan evidence, the same split Phase 21 used for `/host/templates` | `no_dead_controls_test.dart` fails on any new empty handler | ⚠️ **Control neutralised; page deletion owned by Phase 49** |

Rows are kept after resolution. "Pre-existing" marks a defect the redesign found but did not cause
(`docs/ui-ux/regression-criteria.md` §3).

---

## UX Decision Log

Rows that change product behavior or supersede an accepted decision **must** also exist in
`docs/DECISIONS.md`. This log is an index into it, never a substitute.

| ID | Decision | Reason | Alternatives | Affected areas | Date | D-NNN |
|---|---|---|---|---|---|---|
| UX-1 | **One visual register across web, admin and mobile** — unify on the warm event palette; supersede D-065's two-register split | Phases 3/41/50 require one coherent product. Mobile's warm direction is the more distinctive and was chosen deliberately for the attendee context; web/admin's GitHub-dark/coral reads as a developer tool | (b) keep two registers — cheapest but abandons the "one product" goal; (c) design a third direction — discards two working systems | Phases 3, 4, 32, 41, 50; all 206 screens | 2026-08-06 | D-286 (Phase 3) |
| UX-2 | **Manual visual baselines; no new test dependency** | D-109 deliberately excludes Playwright/Cypress/Storybook. Honouring it keeps CI time and dependency surface unchanged | Add a Playwright screenshot harness (needs a D-NNN superseding D-109); or no baselines at all | Phases 0.4, 25, 47 | 2026-08-06 | D-285 |
| UX-3 | **Social / identity surfaces are in scope** — Posts, Chat, Allies, Certificates, Gamification, Competitions, Groups, Workspace | They are ~25% of the product by weight; excluding them would leave the redesign visibly half-finished | Mark them N/A and renormalise to 750 weight | Phases 18A, 20A–20D, 38A, 38B | 2026-08-06 | D-285 |
| UX-4 | **Add spacing / sizing / radius / border / shadow / opacity / z-index / motion / breakpoint token scales to web + admin**, named to mirror Flutter's `KSpace` / `KRadius` / `KMotion` | Only 9 colour tokens exist on web/admin today; everything else is ad-hoc Tailwind, which makes cross-surface consistency unverifiable | Keep colour-only tokens and rely on Tailwind defaults | Phase 4, and every phase after it | 2026-08-06 | D-285 |
| UX-5 | **Phase 21 renamed "Organizer Experience" → "Host / Workspace Experience"** | D-271 / `TERMINOLOGY.md` retired "organizer": a User owns an Event; an organization is one they *represent* | Keep the roadmap's original wording | Phases 21, 43 | 2026-08-06 | D-285 |

---

## Deferred Work Ledger

| Item | Origin phase | Reason deferred | Target phase | Risk |
|---|---|---|---|---|
| Verify `/host/admin/{claims,fraud,orgs,verifications}` are orphaned after D-195 | 0.1 | Deletion requires proof of zero references + a green Phase 48 | 49 | Low — 4 routes, reachable only by direct URL |
| Replace 4 `WorkflowPage` placeholders (`/host/templates/*`, `/host/risk`) | 0.1 | `/templates` is backend-blocked (no admin inventory endpoint); risk page needs product input | 21 / 49 | Medium — visible dead ends in the host surface |
| `POST /v1/groups/join` exists on the backend and nothing in the Flutter app calls it — no join screen, no data source method, no route | 38B | Building the join flow is a feature, not a redesign. The dead "Join a group" control was removed rather than left pretending | needs a product owner | Medium — group registration is unreachable on mobile |
| 70 Flutter fields carry no `textInputAction`, so multi-field forms do not chain "next" through the keyboard | 40 | Each needs a judgement about focus order for its own form; sweeping 70 blind at the end of a phase would be mechanical rather than considered | 42 / 46 | Low — friction, not a defect |
| i18n coverage: only 2 call-sites use `next-intl` despite `en.json`/`hi.json` existing | 0.1 | Wiring translations is a product decision, not a redesign one | 43 | Medium — Phase 43 copy work will not be translatable |
| Admin's two `Tabs` call sites have keyboard navigation but no panel wiring | 18A.0 | `Tabs` now emits `aria-controls` only when the caller opts in with `id` + `TabPanel`; retrofitting admin's event-workspace and taxonomy sheets belongs with their own redesign, not with a web phase. **Still open after D-381 (2026-08-18):** that redesign restructured the event-workspace strip (5 primary tabs + 4 behind `More ▾`, via the new `trailing` slot) but did **not** add `id`/`TabPanel` — the panels are 9 sibling conditionals in one sheet, so wiring them is its own change, not a rename | 28 / 29 | Low — the strips are keyboard-navigable now; only the tab↔panel cross-reference is missing |
| **The web booking journey is unbuilt** — no order creation, no ticket-type selection, no payment | 19 | A payment integration is feature work on the money path, needing a `D-NNN` and a security review. The false-success stub was removed (REG-009) rather than polished or completed | needs a product owner | **High — web cannot sell a ticket.** Mobile can; the backend contract is complete and unused by web |
| `GET /v1/me/participations` returns no event title, so `/workspace` titles each participation by role | 20.1 | The endpoint returns only an id and a role; `/v1/orders` denormalises `event_title` for the same reason. A read per row is an N+1 on every workspace load | needs a backend owner | Low — the row is honest, just less useful than it could be |
| Six `EmptyState` call sites still pass Material Symbols *names* as icons | 19.4 | The primitive now drops a bare string, so they render no icon instead of the identifier. Passing a real icon touches files owned by Phases 20A, 20B and 21 | 20A / 20B / 21 | Low — cosmetic; the leak itself is closed |

---

## Screenshot / Visual Baseline Tracking

Strategy set by D-285 (manual capture, no new dependency). Populated in Phase 0.4.

| Screen | Surface | Baseline captured | Redesigned captured | Compared | Issues |
|---|---|---|---|---|---|
| *(pending 0.4)* | | | | | |

---

## Flutter Test Policy

`mobile/test/features/**` contains page-level widget tests that assert on finders. Redesigning those
screens **will** red them. When that happens:

1. Prefer changing the **finder**, never the **assertion**.
2. Migrate brittle finders (`find.byType`, literal strings) to semantic ones
   (`find.bySemanticsLabel`, keys) — this strengthens the test and serves Phase 46.
3. If an assertion must genuinely change, log it in the Regression Ledger with before/after and a
   one-line justification.
4. Deleting a test requires a ledger entry **and** explicit approval.

Affected: `event_detail_page_test`, `home_dashboard_page_test`, `checkout_page_test`,
`my_tickets_page_test`, `settings_page_test`, `auth_screens_test`, `phase1_screens_smoke_test`,
`organizer_form_sheet_test`, `speakers_page_test`, `event_manage_detail_page_test`,
`transfer_claim_page_test`, `section_error_states_test`.

---

## Phase Completion Gate

A phase moves to `COMPLETE` only when every answer is yes:

1. Was all planned scope implemented?
2. Are all relevant screens migrated?
3. Are relevant components using the new system?
4. Do tests pass?
5. Does build / analyze pass?
6. Was responsive behavior checked?
7. Was accessibility checked?
8. Were visual states (loading / empty / error / success / disabled) checked?
9. Are regressions resolved, or explicitly deferred in the ledger?
10. Is this tracker updated?

Code written ≠ complete.

---

## Session Resume Protocol

New session or new machine: start from [`docs/ui-ux/HANDOFF.md`](docs/ui-ux/HANDOFF.md),
which carries the bootstrap brief, the exact verification floors, and the traps this
program already hit. It is a snapshot — **this file wins wherever they disagree.**

At the start of every future session:

1. Read this file and `UI_REDESIGN_PROGRESS.json`.
2. Read `docs/ui-ux/do-not-change.md` and the relevant inventory file.
3. Run `git status` and `git log --oneline -5`.
4. Identify the last `COMPLETE` phase.
5. Identify any `IN PROGRESS` / `BLOCKED` work.
6. Verify repository state matches this tracker.
7. Continue from **Next action** in the active phase's record.

Never infer progress from conversation memory. **If tracker and repository disagree, STOP and report
the discrepancy before modifying files.**

---

## Git Discipline

* Branch per milestone; one logical commit per phase or coherent sub-phase.
* Commit hashes are recorded in the **next** phase'''s commit, never by amending the one they
  describe — an amend rewrites the very hash just written, so the tracker ends up citing a commit
  that no longer exists. Phase 4 was recorded as `a038fd9` this way before the amend produced
  `40311cb`; caught by re-reading HEAD, which is the check the Session Resume Protocol exists for.
* Never mix backend fixes, infrastructure, dependency upgrades or unrelated bug fixes into a
  redesign commit.
* Never stage the quarantined pre-existing dirty files (`docs/ui-ux/do-not-change.md` §8).
* `git add -A` and `git commit -a` are forbidden for the duration of this program — stage explicit
  paths only.
* Commit trailer per `.claude/CLAUDE.md` §8.

---

## Definition of Done — the whole redesign

100% is reported only when **all** hold:

* All applicable screens migrated (206 rows `Verified`, or explicitly `N/A`).
* No unintended legacy visual system remains.
* Web verified · Admin verified · Flutter verified.
* Critical user journeys work.
* Responsive states verified at all six widths.
* Accessibility requirements verified.
* Loading / error / empty / success states covered.
* Legacy components safely removed (Phase 49).
* Functional regression suite green against or above the recorded baseline.
* Final visual audit complete.
* This ledger contains no unexplained incomplete or blocked item.
