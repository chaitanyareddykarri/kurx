# Kurx Accessibility Foundation

Phase 5 output. The contracts every primitive must satisfy, written **before** Phases 6–10 build
them, so accessibility is a property of the design system rather than a later patch.

Companion: `docs/ui-ux/audit-findings.md` (the five S1s this exists to close),
`.claude/reviews/accessibility-review.md` (the review gate), `docs/ui-ux/regression-criteria.md` §2.6.

**Target: WCAG 2.1 AA.** Kurx is India-facing and consumer-scale; this is a requirement, not a
nicety.

---

## 1. Contrast — already enforced

Phase 4 shipped the palette and `web/test/design-tokens.test.ts` re-measures every pair on every
run. Nothing further is required of component authors **except using the right token**:

| Need | Token | Floor |
|---|---|---|
| Body / secondary text | `text`, `muted` | 4.5:1 on background, surface **and** elevated |
| Link, text button | `accent-text` | 4.5:1 — **not** `accent`, which is a fill (2.80:1 on light) |
| Label on an ember fill | `on-accent` | 4.5:1 — ink, never white (white = 2.86:1) |
| Status text | `success` `warning` `danger` `teal` | 4.5:1 |
| **Input / control boundary** | **`border-strong`** | **3:1** — WCAG 1.4.11 |
| Card edge, divider | `border` | none — decorative |

**The single rule most likely to be got wrong:** a control's visible boundary uses `border-strong`,
not `border`. `border` is 1.30:1 and cannot identify a control. Today `field.tsx` uses `border`.

**Colour is never the only channel.** Every status is carried by text or an icon as well as a hue —
required by WCAG 1.4.1 and by the fact that ~8% of male users cannot separate the ember/success/danger
hues reliably.

---

## 2. Focus and keyboard

### 2.1 Every interactive element is reachable and operable

* Real elements: `<button>`, `<a href>`, `<input>`, `<select>`. Never `<div onClick>`.
  The audit found only 3 violations repo-wide — hold that line.
* Anything with a `role` that implies interaction gets a `tabIndex` and key handlers to match.
* No positive `tabIndex`. Ever. DOM order is the tab order.

### 2.2 Focus is always visible

The global `:focus-visible` ring in `globals.css` is the floor, not a component's excuse to skip it.

```css
outline: 2px solid rgb(var(--color-accent));
outline-offset: 2px;
```

**Never** `outline: none` without a replacement of equal or greater visibility. A component that
renders its own focus treatment must still be visible against `background`, `surface` **and**
`elevated`, and in both themes.

### 2.3 Focus is managed across state changes

| Event | Required behaviour |
|---|---|
| Overlay opens | Focus moves into it — to the first interactive element, or the panel itself if none |
| Overlay is open | `Tab`/`Shift+Tab` **cycle inside it**; the page behind is inert |
| Overlay closes | Focus returns to the element that opened it |
| `Escape` | Closes the topmost overlay only |
| Async content replaces a region | Focus is not dropped to `<body>` |
| Route change | Focus moves to the new page's `<h1>` or main landmark |

This closes **S1-2**. `Dialog` and `Sheet` currently satisfy only the `Escape` row while asserting
`aria-modal="true"` — which tells assistive tech the background is inert when it is not, and is
worse than omitting the attribute.

### 2.4 Keyboard interaction per pattern

| Pattern | Keys |
|---|---|
| Button | `Enter`, `Space` |
| Link | `Enter` |
| Checkbox / Switch | `Space` |
| Radio group | `Arrow` moves **and** selects; the group is one tab stop |
| Select / Dropdown | `Enter`/`Space`/`Down` opens; `Arrow` moves; `Enter` selects; `Esc` closes |
| Tabs | `Arrow` moves; `Home`/`End` jump; the tablist is one tab stop |
| Menu | `Arrow` moves; `Esc` closes and restores focus; typeahead where practical |
| Dialog | `Esc` closes; focus trapped |
| Command palette | `Arrow` moves; `Enter` runs; `Esc` closes |
| Table | Cells are not tab stops; interactive cell contents are |

---

## 3. Screen-reader semantics

### 3.1 Names

Every control has an accessible name. In preference order:

1. Visible text content.
2. `aria-labelledby` pointing at visible text (**preferred for anything with a rendered title** —
   it survives a `ReactNode` title, which is how **S1-3** happens: `Sheet` narrows to `string` and
   silently renders an unnamed dialog for every admin workspace).
3. `aria-label` — last resort, and mandatory on every icon-only control.

### 3.2 The form contract — closes S1-1

`aria-describedby` and `aria-invalid` currently appear **zero times** in web and admin. This is the
contract `Field` must implement in Phase 7:

```
Field(label, helper, error, children)
  ├─ generates one stable id per field
  ├─ <label htmlFor={id}>                       ← always present, never placeholder-only
  ├─ control gets  id
  │                aria-describedby = [helperId, errorId].filter(Boolean)
  │                aria-invalid     = Boolean(error)
  │                aria-required    = required
  └─ error node gets  id=errorId  role="alert"  ← announced when it appears
```

Rules that follow from it:

* **A placeholder is not a label.** Placeholders vanish on input and are unreliable to assistive
  tech.
* **An error is text, never a colour.** A red border alone communicates nothing to a screen reader
  and nothing to a colour-blind user. The design database names this exact anti-pattern
  (*"Bad: red border only"*) at High severity.
* **Errors appear beside their field**, not only in a summary at the top.
* **Validate on blur**, not only on submit.
* Required state is conveyed in the label text *and* `aria-required` — not by an unexplained `*`.

### 3.3 Live regions

| Content | Mechanism |
|---|---|
| Toast / transient confirmation | `role="status"` (polite) — closes **S1-4** |
| Validation error appearing | `role="alert"` (assertive) |
| Async region finished loading | `aria-busy` toggled on the region |
| Result-count change after filtering | polite live region |

A live region must exist in the DOM **before** the message arrives, or it will not be announced.

### 3.4 Landmarks and headings

* One `<h1>` per page; heading levels never skip.
* `<main>`, `<nav>`, `<header>`, `<footer>` present and unduplicated; multiple `<nav>`s are
  distinguished by `aria-label`.
* A skip-to-content link is the first focusable element on every page. **None exists today.**
* Decorative icons are hidden (`aria-hidden="true"`); meaningful ones carry a name.

### 3.5 Tables

Real `<table>` with `<caption>`, `<th scope>`, and a programmatic sort state via `aria-sort`.
The seven screens using raw `<table>` (audit S2-2) get this via `DataTable` in Phase 21/28.

---

## 4. Touch targets

| Context | Minimum |
|---|---|
| Mobile web and Flutter | **44 × 44 px** |
| Dense admin, pointer-only | 32 × 32 px, with ≥ 8 px spacing |
| Spacing between adjacent targets | ≥ 8 px |

A target may be smaller **visually** if its hit area is padded to the minimum. The audit found 32 px
icon-only controls in the web header, `Dialog` close, `Sheet` close and admin topbar (S3-5); the
`IconButton` primitive introduced in Phase 6.1 enforces the floor so this cannot recur per-call-site.

---

## 5. Reduced motion

**Web: already shipped.** `packages/ui/src/styles/tokens.css` carries a global
`prefers-reduced-motion: reduce` block that clamps animation and transition duration everywhere,
including components not yet redesigned. Component authors get it for free.

Components that animate *layout or position* must additionally not move under reduced motion —
clamping duration is not enough for a slide-in. `Sheet` and any future drawer resolve to a fade.

**Flutter: not yet.** There are zero references to `disableAnimations` or `accessibleNavigation` in
`mobile/lib`. Phase 32 adds a helper over `MediaQuery.disableAnimationsOf(context)` and routes every
`AnimatedContainer` / `Hero` / page transition through it.

Nothing auto-plays, loops indefinitely, or parallaxes — under any motion preference.

---

## 6. Dialogs, menus and tables — the acceptance bar

A component is not done until it passes **all** of its row.

| Component | Bar |
|---|---|
| Dialog / ConfirmDialog | `role="dialog"` · `aria-modal` · `aria-labelledby` → visible title · focus trapped · focus restored · `Esc` closes · body scroll locked |
| Sheet / Drawer / BottomSheet | as Dialog, plus: named even when the title is a `ReactNode`; fades rather than slides under reduced motion |
| Popover | focus moves in, `Esc` closes and restores, dismisses on outside click, `aria-expanded` on the trigger |
| Dropdown / Menu | `role="menu"`/`menuitem` · arrow navigation · `Esc` restores focus · trigger has `aria-haspopup` + `aria-expanded` |
| Command palette | labelled `combobox` + `listbox`, `aria-activedescendant`, results announced politely |
| Tooltip | keyboard-focusable trigger; never the **only** source of an accessible name |
| Toast | `role="status"` · manually dismissible · does not steal focus |
| Table | `<caption>` · `<th scope>` · `aria-sort` · sortable headers are real buttons |
| Form field | §3.2 in full |
| Tabs | `role="tablist"`/`tab`/`tabpanel` · arrow keys · one tab stop · panel labelled by its tab |

---

## 7. What Phase 46 verifies

Phase 5 is the contract; Phase 46 is the audit. It re-checks, per surface:
contrast · keyboard-only operation of every journey · focus order · focus visibility · semantic
structure · names on every control · the dialog/menu/table bars above · touch targets · reduced
motion.

**Anything asserted here without a test is a claim.** Where a rule can be enforced mechanically it
should be — the contrast floors already are (Phase 4), and the `Field` contract is testable the same
way once Phase 7 lands it.
