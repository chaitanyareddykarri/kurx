# Review Template: Accessibility

Use for `web/` UI changes (and `admin/` once built). Kurx is India-facing and consumer-scale, so keyboard/screen-reader/contrast support is a real requirement, not a nicety. Complements `.claude/reviews/frontend-review.md`.

## Check against

- **`docs/ui-ux/accessibility-foundation.md`** — the binding per-component contracts (D-285 program).
  §6 is the acceptance bar for dialogs, menus, tables, toasts and form fields; a component is not
  done until it passes its row.
- `.claude/checklists/frontend.md`, `.claude/memory/frontend-conventions.md`
- WCAG 2.1 AA as the target bar

## Pass/fail criteria (measurable)

- [ ] **Keyboard reachable** — every interactive element (buttons, links, form fields, dashboard actions) is focusable and operable without a mouse; visible focus ring present.
- [ ] **Semantic HTML** — real `<button>`/`<a>`/`<label>`/heading hierarchy, not `<div onClick>`; one `<h1>` per page, ordered headings.
- [ ] **Labels** — every form input has an associated `<label>`/`aria-label`; OTP + org/event forms especially.
- [ ] **Contrast** — text meets WCAG AA (4.5:1 normal, 3:1 large) against its background in the Tailwind theme.
- [ ] **Images/media** — meaningful images have `alt`; decorative ones have empty `alt=""`; event gallery media included.
- [ ] **Errors announced** — validation errors are programmatically associated with their field (`aria-describedby`), not colour-only.
- [ ] **Errors carry `aria-invalid`** on the control and `role="alert"` on the message. A red border alone communicates nothing to a screen reader and nothing to a colour-blind user.
- [ ] **Control boundaries use `border-strong`, not `border`** — an input's edge must clear 3:1 (WCAG 1.4.11). `border` is decorative at 1.30:1 and cannot identify a control.
- [ ] **Fills use `on-accent` for their label**, never white on `accent` (2.86:1).
- [ ] **Focus is trapped and restored** — an overlay asserting `aria-modal="true"` must actually contain focus; claiming inertness without enforcing it is worse than omitting the attribute.
- [ ] **Overlays are named even when the title is a `ReactNode`** — prefer `aria-labelledby` over `aria-label`, which silently yields an unnamed dialog when the title is not a string.
- [ ] **Touch targets ≥ 44 px** on mobile web and Flutter (≥ 32 px with 8 px spacing for pointer-only admin density).
- [ ] **Reduced motion honoured** — and components that animate position resolve to a fade, not merely a faster slide.
- [ ] **Skip-to-content link** is the first focusable element on the page.
- [ ] **No keyboard trap** — modals/menus can be dismissed and return focus sensibly.
- [ ] **Language** — `lang` set; content copy readable (India-facing, mixed-language event data tolerated).

## Output

Findings file:line + the barrier (e.g. "publish action is a `div` with `onClick` — unreachable by keyboard, invisible to screen readers").

## Stop condition

Criteria evaluated against the rendered page (loaded in a browser, tabbed through), not inferred from JSX alone — consistent with the frontend "verify in a real browser" rule.
