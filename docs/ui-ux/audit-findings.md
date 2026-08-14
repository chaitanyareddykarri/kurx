# Kurx UI/UX Redesign — Product & UX Audit

Phase 1 output. Every finding is backed by a measurement taken against commit `c66ae16`, not by
impression. Counts are reproducible with the commands in §8.

**Severity** = user impact × breadth.

| | Meaning |
|---|---|
| **S1** | Blocks or excludes a user; or breaks on a primary journey. Fix is mandatory. |
| **S2** | Significant friction or inconsistency on a frequently-used surface. |
| **S3** | Localised friction, or consistency debt with a clear owner phase. |
| **S4** | Polish. Worth doing inside a phase that is already touching the file. |

**Headline:** the codebase is in far better shape than a redesign of this size usually finds —
colour-token discipline is near-total on web/admin (2 stray palette classes in ~1,300 colour class
uses), semantics are mostly real HTML (3 `<div onClick>` in 161 files), and all four test suites are
green. The problems are **concentrated in the shared primitives** and in **what the token layer does
not yet cover**. That is good news: a handful of files fix most of them.

---

## S1 — Blocking

### S1-1 · Form errors are invisible to screen readers, product-wide

`aria-describedby` appears **0 times** and `aria-invalid` appears **0 times** across all of
`web/` and `admin/` — 115 screens, 210 raw form controls and 245 `Field` uses.

The repo's own bar requires the opposite. `.claude/reviews/accessibility-review.md`:

> *"Errors announced — validation errors are programmatically associated with their field
> (`aria-describedby`), not colour-only."*

**Root cause is one file.** `packages/ui/src/field.tsx` renders the error as a bare `<p>` with no
`id`, and never links it:

```tsx
<label htmlFor={htmlFor} …>{label}</label>
{children}
{error ? <p className="text-xs text-danger">{error}</p> : …}
```

`Input` accepts `error?: boolean` and uses it **only to change the border colour** — never to set
`aria-invalid`. So every validation failure in Kurx is communicated by colour alone, which also
fails WCAG 1.4.1 (Use of Colour) for sighted colour-blind users.

**Fix:** generate an id inside `Field`, wire `aria-describedby` to the helper/error node and
`aria-invalid` to the control. One file; ~245 call sites fixed without touching any of them.
**Owner: Phase 5 (contract) → Phase 7 (implementation).**

### S1-2 · No overlay traps focus

`Dialog` and `Sheet` both set `role="dialog"` and `aria-modal="true"` and both close on `Escape` —
but neither:

* moves focus into the panel on open,
* traps `Tab` inside it (focus walks straight onto the page behind, which is still fully operable),
* restores focus to the trigger on close,
* locks body scroll.

`aria-modal="true"` on a dialog that does not actually contain focus is worse than no attribute: it
tells assistive tech the background is inert when it is not.

`ConfirmDialog` composes `Dialog`, so **every destructive confirmation in web and admin inherits
this** — including admin's approve/reject actions.

**Owner: Phase 9.** Blocking for Phase 30 (reviewer workflow is confirmation-dense).

### S1-3 · `Sheet` can render as an unnamed dialog

```tsx
aria-label={typeof title === "string" ? title : undefined}
```

When `title` is a `ReactNode` — which is the normal case in the admin workspaces
(`event-workspace-sheet.tsx`, `org-detail-content.tsx`, `taxonomy-detail-sheet.tsx`) — the
accessible name silently becomes **nothing**. A screen reader announces "dialog" and stops.

**Fix:** `aria-labelledby` pointing at the rendered heading, which works for any node.
**Owner: Phase 9.**

### S1-4 · Toasts are silent to assistive tech and cannot be dismissed

`packages/ui/src/toast.tsx` has **no `aria-live` region and no `role="status"`**. Toasts are the
repo-mandated feedback channel for row-action outcomes (`frontend-conventions.md`), so the outcome
of an admin bulk action is announced to sighted users only.

They also auto-dismiss after a fixed **3200 ms with no dismiss control and no pause-on-hover**,
failing WCAG 2.2.1 (Timing Adjustable). A 3.2-second window is short for a long error message.

**Owner: Phase 8.**

### S1-5 · `web/components/ui/` contains 8 forked primitives, and one of them defeats the S1-1 fix

D-185 recorded that web's local primitives were *"promoted to the shared design system … Kept as a
re-export shim … so there is one source"*. **That is true of 10 of the 18 files. The other 8 are
full forks**, still carrying their own implementation:

| File | Divergence from `@kurx/ui` | Importers |
|---|---|---:|
| `field.tsx` | byte-identical copy | **6** |
| `avatar.tsx` | 2 lines | 6 |
| `motion-panel.tsx` | byte-identical copy | 1 |
| `sheet.tsx` | **32 lines — a stale older API** (no `size`, no `actions`, `title` narrowed to `string`) | 0 |
| `section-header.tsx` | 2 lines | 0 |
| `tabs.tsx` | 4 lines | 0 |
| `switch.tsx` | byte-identical copy | 0 |
| `toast.tsx` | byte-identical copy | 0 |

**The consequence is not tidiness, it is correctness.** The six files importing the forked
`@/components/ui/field` are:

```
web/app/(app)/discover/page.tsx
web/components/auth/password-field.tsx
web/components/auth/recovery-panel.tsx
web/components/auth/onboarding-form.tsx
web/components/auth/reset-password-panel.tsx
web/components/auth/registration-flow.tsx
```

Five of the six are **the authentication flow**. Fixing `aria-describedby` / `aria-invalid` in
`packages/ui/src/field.tsx` — the S1-1 remedy — would therefore repair every form in Kurx *except
sign-in, registration, recovery and password reset*, which is precisely where a mis-typed OTP or a
rejected password most needs an announced error. The fix would look complete and would not be.

The remaining five forks (`sheet`, `section-header`, `tabs`, `switch`, `toast`) have **zero
importers** — dead duplicates. `sheet.tsx` is the dangerous one to leave lying around: it is an
older API, so a future author importing it by autocomplete silently gets a Sheet that cannot render
the admin workspace variant.

**Fix:** collapse all 8 to re-export shims *before* Phase 7 touches `Field`, then delete the dead
ones in Phase 49 once `Remaining uses = 0`.
**Owner: Phase 6.6 — and it is a hard prerequisite for S1-1.**

---

## S2 — Significant

### S2-1 · Almost nothing has a loading or error state

| Surface | Coverage |
|---|---|
| web | **8 `loading.tsx` for 88 routes** (9%); **1 `error.tsx`** (`/u/[username]`) for the whole app |
| admin | 1 `loading.tsx` + 1 `error.tsx` at the `(console)` group level, plus `app/error.tsx` and `app/global-error.tsx` |

Next.js App Router gives per-route `loading.tsx` / `error.tsx` for free. Today, a slow query on 80
of 88 web routes shows a blank frame, and a thrown error on any route except one public profile
falls through to the root boundary — losing the shell, the nav and any sense of where you were.

The primitives to fix this already exist and are unused here: `Skeleton`, `ListSkeleton`,
`EmptyState`, `ErrorState`. **Owner: Phase 8, applied per-screen in 12–24 and 29.**

### S2-2 · Six host tables bypass `DataTable`

`frontend-conventions.md` is explicit:

> *"List/table screens: `DataTable` (sort, skeleton loading, empty state, row selection) — not a
> hand-rolled `Card` stack or bare `<table>`."*

Raw `<table>` remains in:

* `web/app/(app)/host/events/[id]/attendees/page.tsx`
* `…/tickets/page.tsx`
* `…/certificates/page.tsx`
* `…/analytics/page.tsx`
* `…/invitations/page.tsx`
* `web/app/(app)/host/representing/[orgId]/finance/page.tsx`
* `web/components/settings/notification-preferences.tsx`

Each therefore has no sort, no skeleton, no empty state and no responsive strategy — on the host
surface, where users spend the most time. Admin is clean (0 raw tables).

**Owner: Phase 21**, consuming the Phase 28 `DataTable` upgrade.

### S2-3 · Typography has no scale, and the product is 88% small text

| Class | Uses |
|---|---:|
| `text-sm` | 703 |
| `text-xs` | 442 |
| `text-lg` | 63 |
| `text-2xl` | 41 |
| `text-base` | **5** |

1,145 of ~1,310 sizing declarations are `text-sm` or `text-xs`. There is effectively **no body
text** in Kurx — everything is caption-sized, so headings carry the entire hierarchy alone and long
content (event descriptions, post bodies, chat) reads as dense and secondary.

No typographic tokens exist on web or admin: no scale, no line-height ramp, no weight ramp.

**Owner: Phase 4.2 (scale) → Phase 3 (hierarchy intent).**

### S2-4 · Spacing and radius vocabularies have drifted

* **Radius:** 5 competing values, including bare `rounded` (4 px, 60 uses) sitting next to
  `rounded-md` (219) and `rounded-lg` (81) — often in sibling elements.
* **Padding:** 15+ distinct values in the top tier alone, including half-steps `px-2.5`, `py-1.5`,
  `px-1.5`, `py-0.5` that belong to no scale.
* **Gap:** 10 distinct values.

Compare Flutter, which already has `KSpace` (4 px base) and `KRadius`. Web/admin have neither, so
every author picks by eye.

**Owner: Phase 4.3.**

### S2-5 · Mobile has 24 hardcoded colours outside the theme

`Color(0x…)` literals appear **24 times** across at least 8 feature pages —
`invitations_page`, `org_verification_page`, `certificate_detail_page`, `my_certificates_page`,
`invitation_rsvp_page`, `ticket_detail_page`, `my_orders_page`, `transfer_claim_page`.

These bypass `KurxColors` entirely, so they will **not follow** the Phase 32 theme change and will
not respond to dark mode. Mobile token drift is worse than web's (24 vs 6).

**Owner: Phase 32.1**, screens fixed in 37 / 38B.

### S2-6 · Reduced motion is unsupported everywhere

* Web/admin animate with `framer-motion` in `Dialog`, `Sheet`, `Toast`, `MotionPanel` — **no
  `prefers-reduced-motion` check anywhere**.
* Mobile has 10 animated widgets and **0** references to `disableAnimations` /
  `accessibleNavigation`.

WCAG 2.3.3. A user with vestibular sensitivity has no way to opt out.

**Owner: Phase 5.4 (contract) → Phases 9, 42, 32.**

### S2-7 · The mobile shell reserves nav space with a magic number

`mobile/lib/features/shell/presentation/app_shell.dart` pads content by a hardcoded
`EdgeInsets.only(bottom: 80)` to clear the floating pill nav, while the nav itself is wrapped in
`SafeArea`. The reservation therefore does not track the actual bar height, the home-indicator inset,
or the user's text-scale setting. At large text sizes or on devices with a taller inset the last row
of any list sits under the bar.

Broader signal: **18 `SafeArea` uses against 98 `Scaffold`s**.

**Owner: Phase 33**, verified in Phase 40.

---

## S3 — Localised

### S3-1 · Four routes are visible dead ends
`/host/templates/certificates`, `/host/templates/editor`, `/host/templates/invites` and
`/host/risk` render the static `WorkflowPage` placeholder. `/templates` is genuinely
backend-blocked (`GET /v1/templates` returns only `Published`, so drafts are unlistable and no
management UI can exist). **Owner: Phase 21 / 49.** Already in the Deferred ledger.

### S3-2 · Four host routes are probably orphaned
`/host/admin/{claims,fraud,orgs,verifications}` predate D-195, which moved trust & safety into
`admin/`. Web's own sidebar `reviewerNav` now points at `siteConfig.adminUrl`. They are reachable
only by typed URL and may duplicate admin logic. **Owner: Phase 49**, after proving zero references.

### S3-3 · Translation infrastructure exists but is unused
`next-intl` is installed, `web/messages/en.json` and `hi.json` exist (144 lines each), and exactly
**2 call sites** use `useTranslations` / `getTranslations`. For an India-facing product this means
the Phase 43 copy pass will produce strings that cannot be translated without a separate migration.
**Owner: Phase 43**, flagged as a decision rather than silently absorbed.

### S3-4 · Header treats Profile and Notifications as unlabelled corners
`web/components/layout/app-shell.tsx` puts Profile top-left and Notifications top-right to mirror
the Flutter shell. Neither carries a badge, count or state, so a user has no way to know something
is waiting without visiting. Notifications is an icon-only link at `p-2` — a **32 px touch target**,
under the 44 px minimum. **Owner: Phase 11.**

### S3-5 · Icon-only controls at sub-minimum touch size
The pattern `className="… p-2 …"` around an 18 px `lucide` icon recurs in the web header, `Dialog`
close, `Sheet` close and admin topbar — 32–34 px effective, below the 44 px floor the redesign
adopts. **Owner: Phase 6.1** (an `IconButton` primitive does not exist yet and should enforce it).

### S3-7 · The one a11y defect ESLint already reports is unfixed
`web/components/host/module-picker.tsx:55` puts `aria-disabled` on an `<li>`, whose implicit
`listitem` role does not support it — so the disabled state of an event module is announced to
nobody. `next lint` has been reporting this as a warning on every run (it is in the Phase 0 baseline
log), and warnings do not fail the build, so it has persisted. **Owner: Phase 22** (module-picker is
the event readiness module list). The redesign should also decide whether lint warnings ought to be
errors — one unfixed warning is how a second becomes invisible.

### S3-6 · 154 raw `<input>` on web against 245 `Field` uses
The form system is roughly half-adopted. Every raw control is a screen that will not benefit from
the S1-1 fix. **Owner: Phase 7**, and it is the Component Migration Matrix denominator.

---

## S4 — Polish

* **S4-1** 4 raw `<img>` on web where `next/image` is used in only 2 files — inconsistent loading
  behaviour and no intrinsic sizing. *Phase 45.*
* **S4-2** 6 hardcoded hex values in `admin/app/global-error.tsx` and
  `admin/components/admin/taxonomy/taxonomy-detail-sheet.tsx`. `global-error.tsx` is defensible (it
  renders outside the token stylesheet); the taxonomy sheet is not. *Phase 29.4.*
* **S4-3** 2 stray Tailwind palette classes (`bg-amber-500`) that should be a semantic token —
  there is no `warning` token today, only `success` / `danger`. *Phase 4.1.*
* **S4-4** Heading ramp is top-heavy: 84 `<h1>`, 116 `<h2>`, 27 `<h3>`, 3 `<h4>`. `h2` is doing two
  jobs. *Phase 10.5 / 43.*
* **S4-5** `mobile` has 1 `semanticLabel` and 23 `Semantics` wrappers across 91 pages. Icon buttons
  are covered by 41 `tooltip:` values, so this is better than it looks, but decorative-vs-meaningful
  imagery is undeclared. *Phase 40.*

---

## Cross-surface consistency

| Dimension | Web | Admin | Mobile | Verdict |
|---|---|---|---|---|
| Palette | GitHub-dark / coral | same (shared preset) | warm orange / cream | **Two registers by decision (D-065)** — resolved by D-286 in Phase 3 |
| Typography | system-ui, no scale | same | Plus Jakarta Sans | Divergent |
| Radius | 4–8 px, 5 values | same | 8–24 px, `KRadius` | Divergent |
| Icons | `lucide-react` | `lucide-react` | Material rounded | Divergent by platform — acceptable, but pairing must be mapped |
| Primary nav | sidebar, 5 + 6 + 3 items | sidebar, 8 groups | 5-tab floating pill | Web and mobile deliberately aligned (same 5 areas) ✅ |
| Semantic colours | success / danger only | same | success / danger / teal | **No `warning` token anywhere** |
| Feedback | `ToastProvider` | `ToastProvider` | `kurx_feedback.dart` | Same concept, different silence problem (S1-4) |

The single largest cross-platform inconsistency is the palette, and it is **intentional** — D-065
chose it. Phase 3 supersedes it via D-286. Everything else in this table is drift, not decision.

---

## What is already good — do not "fix" these

* **Colour-token adoption on web/admin is near-total.** 2 stray palette classes in ~1,300 colour
  class uses. The token layer is thin but genuinely used.
* **Semantics are real.** 3 `<div onClick>` in the entire web + admin surface; 161 `<label>`
  elements; 84 `<h1>` for ~115 screens.
* **The design system is single-source for the components that matter most.** `Button`, `Card`,
  `Badge`, `Dialog`, `Chip`, `Skeleton`, `EmptyState`, `ErrorState` and the data primitives are true
  re-export shims onto `@kurx/ui` (D-185) — there is no second Button to reconcile. Eight other
  files did not complete that migration; see S1-5.
* **`admin` consumes the shared Tailwind preset correctly.** Web is the one that drifted.
* **Flutter is ahead on tokens** — `KSpace`, `KRadius`, `KMotion`, `kCardShadow` and a
  `ThemeExtension` already exist. Phase 4 should copy *its* structure onto web, not the reverse.
* **All four suites are green** at 1433 / 145 / 275 / clean.

---

## Ranked fix order (feeds Phase 2 and the phase sequence)

| Rank | Finding | Sev | Fix cost | Files | Phase |
|---:|---|---|---|---|---|
| 0 | **S1-5 collapse the 8 forks** — prerequisite for rank 1 | S1 | 8 files | `web/components/ui/*` | **6.6** |
| 1 | S1-1 form a11y | S1 | **1 file** | `field.tsx` | 5 → 7 |
| 2 | S1-4 toast a11y | S1 | 1 file | `toast.tsx` | 8 |
| 3 | S1-2 / S1-3 overlay focus | S1 | 2 files | `dialog.tsx`, `sheet.tsx` | 9 |
| 4 | S2-3 typography scale | S2 | token layer | preset + tokens.css | 4.2 |
| 5 | S2-4 spacing / radius scale | S2 | token layer | preset + tokens.css | 4.3 |
| 6 | S2-6 reduced motion | S2 | 4 files + Flutter | primitives | 5.4 → 9, 42, 32 |
| 7 | S2-1 loading / error states | S2 | ~80 small files | per-route | 8 → 12–24, 29 |
| 8 | S2-5 mobile colour drift | S2 | 8 files | feature pages | 32.1 |
| 9 | S2-2 host raw tables | S2 | 7 files | host pages | 21 |
| 10 | S2-7 mobile shell inset | S2 | 1 file | `app_shell.dart` | 33 |

Ranks 0–3 are **eleven files** and remove every S1 in the product. Rank 0 must land first or rank 1
silently skips the authentication flow.

---

## §8 · Reproducing these measurements

```bash
# S1-1
grep -rn "aria-describedby\|aria-invalid" web admin --include='*.tsx' | grep -v node_modules | wc -l

# S2-1
find web/app -name loading.tsx | wc -l ; find web/app -name error.tsx

# S2-2
grep -rln "<table" web/app web/components --include='*.tsx'

# S2-3 / S2-4
grep -rhoE "\btext-(xs|sm|base|lg|xl|2xl|3xl)\b" web admin --include='*.tsx' | sort | uniq -c | sort -rn
grep -rhoE "\brounded(-(none|sm|md|lg|xl|2xl|3xl|full))?\b" web admin --include='*.tsx' | sort | uniq -c

# S2-5
grep -rn "Color(0x" mobile/lib --include='*.dart' | grep -v core/theme/ | wc -l

# S2-6
grep -rn "prefers-reduced-motion" web admin packages --include='*.tsx' --include='*.css' | wc -l
grep -rn "disableAnimations\|accessibleNavigation" mobile/lib --include='*.dart' | wc -l
```
