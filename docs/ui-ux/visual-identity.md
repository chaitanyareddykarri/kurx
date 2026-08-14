# Kurx Visual Identity

Phase 3 output. The design direction every later phase builds against. Decisions here are binding;
contradicting one requires a new `D-NNN`.

> **Revised by [D-288]: the hue is blue on black, not ember on warm paper.** The *structure* of this
> document survives the change intact — the token set, the fill/text split, the contrast floors, the
> "one register across three surfaces" rule and every principle but P1's warmth are what D-286 got
> right and D-288 keeps. Only the hue family and the surface ramp moved. §3 carries the shipped
> values; the measurement tables above them are retained as the history of how the floors were set.

Companion: `docs/ui-ux/audit-findings.md` (what is wrong today), `docs/ui-ux/information-architecture.md`
(structure), and Phase 4's token implementation.

---

## 1. What Kurx actually is

D-065 justified mobile's warm palette as *"attendee-first (concerts, festivals)"*. That is only half
the product. The domain model tells a different story:

certificates · competitions · team formation · speakers · sponsors · institutional verification ·
KYC · membership claims · professional journey · allies · attendance provenance · reviewer approval

This is **college fests, hackathons, conferences and institutional events in India** — with a social
layer on top. The centre of gravity is *credentialled participation*, not nightlife. A user's
strongest reason to return is that Kurx holds the **record of what they showed up for**.

That reframing drives everything below.

### Brand personality

| Is | Is not |
|---|---|
| Warm, human, celebratory | Corporate, sterile, "enterprise" |
| Credible, verifiable, precise | Playful to the point of unserious |
| Contemporary Indian | Generic Silicon-Valley SaaS |
| Confident and quiet | Loud, gradient-soaked, animated for its own sake |

**One line:** *Kurx is the warm, credible record of what you showed up for.*

---

## 2. Visual principles

### P1 · Opaque, not glass
Surfaces are **opaque** — flat ink-black panels, not lit ones. No glassmorphism, no frosted panels,
no gradient meshes, no glow. This is the explicit anti-"AI dashboard" rule: when a surface needs
separation it gets a tint step and an honest shadow, never translucency. (Under D-286 the tint steps
were warm; D-288 makes them cool without changing the rule.)

### P2 · Blue acts, teal attests
Colour carries meaning, not decoration.

* **Blue** = *the live and the actionable*. Register, publish, go, now, primary CTA.
* **Teal** = *the verified and the attested*. Provenance badges, verified sections, certificates,
  confirmed allies, approved reviews.

The product already has provenance as a first-class concept (`provenance-badge.tsx`,
`verified-sections.tsx`, the trust engines). Giving it its own hue makes a structural truth visible.
**Never use teal for generic success chrome** — that dilutes it. Success is its own token.

### P3 · Type does the hierarchy
Today 88% of all text is `text-sm`/`text-xs` with five uses of `text-base` — the product has no body
copy, so headings carry hierarchy alone (audit S2-3). The new scale restores a real body size and
uses **weight and colour before size** to separate levels.

### P4 · Density is contextual, from one system
Three densities from a single token set, chosen per surface, never per component:

| Density | Where | Row height | Base text |
|---|---|---|---|
| **Comfortable** | discovery, event detail, profile, all mobile | 48 px | 16 px |
| **Compact** | host workspace, settings, forms | 40 px | 15 px |
| **Dense** | admin tables and queues | 32 px | 14 px |

This is how admin gets the information density Phase 26 demands *without a second design system*.

### P5 · The ticket is the shape
Radius is anchored on a physical metaphor rather than taste, replacing the five drifting values the
audit found:

| Token | Value | Use |
|---|---|---|
| `radius-sm` | 6 px | inputs, chips inside dense rows |
| `radius-md` | 10 px | buttons, inputs, small controls |
| `radius-lg` | 16 px | cards, panels — the "ticket corner" |
| `radius-xl` | 24 px | sheets, modals, hero media |
| `radius-pill` | 999 px | filter chips, tags, avatars, primary mobile CTA |

### P6 · Motion confirms, never performs
Every animation answers *"what just changed?"*. Nothing exists to be admired. Durations are short
(120/200/280 ms), easing is a single decelerating curve, and **`prefers-reduced-motion` is honoured
by every animated component** — today zero components check it (audit S2-6).

### P7 · Both themes are first-class
Dark is the primary register — a **near-black ground** with cool ink-blue panels. Light is a clean
slate-neutral paper, not a tint-inverted afterthought. Both are authored and both are
contrast-verified. (Under D-286 the same principle was stated as warm-dark and warm-paper; D-288
changes the hue, not the rule.)

---

## 3. Colour — and why neither existing palette could simply be adopted

Both candidate palettes were measured against WCAG 2.1 before choosing. **Both fail, in
load-bearing places.**

*(Both tables below are pre-D-286 history. They are kept because they are where the contrast floors
this document enforces came from, and D-288 inherits every one of them.)*

### Measured failures in the pre-D-286 mobile (warm) palette

| Pair | Ratio | Verdict | Where it bites |
|---|---:|---|---|
| white on ember `#FFFFFF`/`#F0762B` | **2.86:1** | ❌ FAIL | `filledButtonTheme` — **every primary CTA in the mobile app**, including Register and Book |
| ember text on cream `#F0762B`/`#FFFCF8` | **2.80:1** | ❌ FAIL | `textButtonTheme` — every text button on light |
| muted on cream `#7A7A7A` | **4.20:1** | ❌ under 4.5 | all secondary text on light |
| teal on warm-dark `#2A8B78` | **4.37:1** | ❌ under 4.5 | the verified signal, on dark |
| border on either bg | **1.30 / 1.39:1** | ❌ FAIL 3:1 | input boundaries (WCAG 1.4.11) |

### Measured failures in the pre-D-286 web (GitHub-dark) palette

| Pair | Ratio | Verdict |
|---|---:|---|
| accent on light `#CF4225`/`#F6F8FA` | **4.42:1** | ❌ marginally under 4.5 |
| border on either bg | **1.36 / 1.55:1** | ❌ FAIL 3:1 |

So **D-286 is not "adopt mobile's palette"** — that would ship a known-failing primary button to
three surfaces. It is *adopt mobile's direction, corrected to pass.*

### The correction, and what generalised out of it

D-286's own fix was to keep the vivid ember fill and label it with ink:

```
#1A1A1A ink on #F0762B ember  =  6.08:1   ✅
#FFFFFF     on #F0762B ember  =  2.86:1   ❌
```

D-288 changed the hue, and the same measurement came out the other way — white on `#2563EB` is
5.17:1 where ink is 3.83:1. **The durable rule is therefore "measure the fill", not "the label is
ink".** Both test suites now assert `on-accent` is the *more legible of the two candidates* rather
than a fixed colour, so the next palette gets the right answer without anyone remembering to check.

The one thing that does not generalise away: **a fill dark enough to carry a white label is too dark
to read as text on a near-black surface.** `#2563EB` is 3.26:1 as text on dark (and a thin 4.72:1 on
light). That is why `accent` and `accent-text` are two tokens and, unlike under the ember palette,
never coincide on either theme.

### The palette

**Core ramp** — a single blue hue (~217°) across both themes, with the fill one step below the link
so each clears its own floor. Surfaces are a near-black ground with cool ink-blue panels above it.

**These are the shipped values** (`packages/ui/src/styles/tokens.css`). Every foreground was solved
against **all three surface steps** — background, surface *and* elevated — not just the page
background: a value that clears 4.5:1 on `background` can fail on `elevated`. The "worst" column is
the lowest of the three.

| Token | Light | worst | Dark | worst | Notes |
|---|---|---:|---|---:|---|
| `accent` (blue fill) | `#2563EB` | — | `#2563EB` | — | a surface; as text it is 4.72 light / **3.26 dark** |
| `on-accent` | `#FFFFFF` | 5.17 | `#FFFFFF` | 5.17 | **white, because the fill measured that way** (ink = 3.45 light, 3.83 dark) |
| `accent-text` | `#1D4ED8` | 6.12 | `#3B82F6` | 4.58 | links, text buttons |
| `teal` (attests) | `#1F6B5C` | 5.78 | `#309E88` | 5.11 | provenance only |
| `background` | `#FFFFFF` | — | `#0A0A0A` | — | paper / near-black |
| `surface` | `#F8FAFC` | — | `#111827` | — | |
| `elevated` | `#F1F5F9` | — | `#161D2D` | — | |
| `text` | `#0F172A` | 16.30 | `#FFFFFF` | 16.83 | |
| `muted` | `#475569` | 6.92 | `#9CA3AF` | 6.63 | |
| `border` | `#E2E8F0` | — | `#1F2937` | — | **decorative only** |
| `border-strong` | `#64748B` | 4.34 | `#64748B` | 3.54 | control boundaries; one value serves both themes |
| `success` | `#197C36` | 4.82 | `#3FB950` | 6.63 | distinct from teal, on purpose |
| `warning` | `#876809` | 4.77 | `#E6B31E` | 9.94 | gold (hue 45°), a different signal from the blue accent |
| `danger` | `#CE2C2C` | 4.77 | `#F05C59` | 5.11 | |

The four semantic hues — `teal`, `success`, `warning`, `danger` — are **unchanged from D-286**.
They are signals, not brand, and re-hueing them to match the accent is exactly the dilution P2
forbids. Every one of them gained headroom under the darker ground.

Two structural additions, both shipped and both retained:

1. **`border` split into `border` + `border-strong`.** WCAG 1.4.11 governs boundaries that *identify
   a control* — an input's edge must reach 3:1; a card's decorative edge need not. One token cannot
   serve both, which is why every pre-D-286 palette failed at 1.30–1.55:1.
2. **`warning` is a real token** — its absence is why the audit found `bg-amber-500` written by hand.
   It is **gold at hue 45°**, which under the ember palette had to be defended against collapsing
   into `accent` at hue 23°. Against a blue accent the separation is no longer at risk, but the test
   that guards it stays.

**These ratios are enforced, not documented.** `web/test/design-tokens.test.ts` and its Dart twin
`mobile/test/core/design_tokens_test.dart` re-measure every pair on every run, assert `accent` and
`accent-text` have not collapsed into one value, and assert `warning` stays hue-separated from
`accent`.

---

## 4. Typography

| Level | Size / line | Weight | Use |
|---|---|---|---|
| `display` | 40 / 44 | 700 | event title on detail, hero |
| `h1` | 30 / 36 | 700 | page title |
| `h2` | 24 / 30 | 650 | section |
| `h3` | 19 / 26 | 650 | card title, subsection |
| `body-lg` | 17 / 26 | 400 | event description, post body |
| **`body`** | **15 / 23** | 400 | **the default — currently missing** |
| `label` | 14 / 20 | 550 | form labels, table headers |
| `caption` | 13 / 18 | 400 | metadata, timestamps |
| `micro` | 12 / 16 | 550 | badges, chips |

**Face: Anek (Ek Type).** — D-287.

Mobile currently loads **Plus Jakarta Sans**, and the first draft of this document adopted it
platform-wide. That was wrong for this product: `web/messages/hi.json` ships a Hindi locale, and
Plus Jakarta Sans **has no Devanagari coverage**, so every Hindi string falls back to an unrelated
system face — a visible typeface change mid-page, in the one market the product targets.

**Anek** is a superfamily from Ek Type, an Indian foundry, with matched cuts across Latin,
Devanagari and eight other Indian scripts. It is variable (weight + width), so the nine steps below
come from one file per script rather than nine static weights. Browsers select per glyph, so the
stack `"Anek Latin", "Anek Devanagari"` needs no runtime branching to render a mixed-script string
correctly.

One family, nine steps; a second display face is **not** introduced — weight and size carry the
difference.

> **Deferred:** self-hosting the binaries. `next/font` fetches at build time and this environment has
> no network, so the family is declared in the stack and degrades to the previous
> `ui-sans-serif, system-ui` rendering until the files are vendored. The token is already correct,
> so adopting the font later is a no-code change. Tracked in the Deferred ledger.

**Rule for every later phase:** `caption`/`micro` are for metadata. Any paragraph a user is expected
to *read* uses `body` or `body-lg`. This is the direct remedy for S2-3.

---

## 5. Imagery

* Event media is the hero and gets the largest radius (`radius-xl`) and a scrim tinted toward the
  **near-black ground** rather than a neutral grey, so text over media stays in-family. Placeholder
  art for events with no uploaded image runs the blue → indigo → sky → teal → violet ramp in
  `event_visuals.dart`; every stop clears 3:1 against the white icon drawn on it.
* Aspect ratios are fixed per context (16:9 hero, 3:2 card, 1:1 avatar) so images cannot cause
  layout shift. This is also the Phase 45 requirement.
* **No stock-photo aesthetic in empty states.** Empty states use type and a single line-icon, not
  illustration. Illustration ages badly and inflates bundle size.
* Every meaningful image carries `alt`; decorative ones carry `alt=""`.

---

## 6. Iconography

* Web/admin: **`lucide-react`**, already the sole icon set. Keep. Stroke 1.75, size 16/18/20.
* Mobile: **Material rounded**, already in use. Keep — matching platform expectation beats
  cross-platform pixel identity.
* Divergence is acceptable **only where the glyph means the same thing**. Phase 41 maintains an
  explicit web↔mobile icon-pairing map so "saved" is never a bookmark on one surface and a heart on
  the other.
* Icons never carry meaning alone: every icon-only control has an accessible name, and every status
  icon is paired with text or a `Badge`.

---

## 7. Shape and elevation

Shadows are **accent-tinted**, never neutral grey — a grey shadow on paper reads as dirt.

| Token | Light | Dark |
|---|---|---|
| `shadow-sm` | blue at 4%, 2 px | black at 24%, 2 px |
| `shadow-md` | blue at 8%, 8 px | black at 32%, 8 px |
| `shadow-lg` | blue at 10%, 20 px | black at 40%, 20 px |

Mobile's `kCardShadow` does exactly this (accent at 8% on light). It is the correct model and
web/admin adopt it. The tint follows the accent, so D-288 moved it from ember to blue rather than
leaving a brown smudge under a blue palette.

Elevation is **one step at a time**: background → surface → elevated → overlay. A card does not sit
on a card.

---

## 8. Density and layout

* 4 px spacing base, matching Flutter's existing `KSpace`.
* Content max-width **1180 px** (already `--container-shell` on web) for reading surfaces; admin
  tables run full-bleed.
* Touch targets **≥ 44 px** below 768 px, **≥ 32 px** for dense admin pointer surfaces — the audit
  found 32 px icon buttons shipped to mobile web.

---

## 9. Motion

| Token | Duration | Use |
|---|---|---|
| `motion-fast` | 120 ms | hover, press, focus |
| `motion-base` | 200 ms | panels, disclosure, tabs |
| `motion-slow` | 280 ms | sheets, modals, route transitions |

Easing: one decelerating curve — `cubic-bezier(0.2, 0.8, 0.2, 1)` (web) / `Curves.easeOutCubic`
(Flutter, already in `KMotion`).

**Reduced motion is mandatory, not optional.** Under `prefers-reduced-motion: reduce` (web) or
`MediaQuery.disableAnimations` (Flutter): transforms become instant, opacity fades are kept at
≤ 80 ms, and nothing auto-plays or parallaxes. Phase 5.4 writes the contract; Phases 9, 32 and 42
implement it.

---

## 10. Light / dark strategy

* **Both authored, neither derived.** Every token has a hand-picked value per theme, contrast-verified.
* **Web/admin currently default to dark** (`:root` is dark, `.light` overrides). Mobile follows the
  system. Phase 4 aligns all three on **system preference with an explicit user override**, which is
  what `next-themes` and `theme_mode_controller.dart` already support.
* Theme switching must not flash — the class is applied before paint.
* Admin stays visually identical to web; it differs by **density**, never by palette (P4).

---

## 11. What this direction explicitly rejects

| Rejected | Because |
|---|---|
| Glassmorphism / frosted panels | P1; also a contrast hazard over arbitrary media |
| Gradient-heavy hero sections | reads as generic SaaS marketing |
| Purple/indigo as the *brand* hue | the "AI product" aesthetic the brief said to avoid; violet appears only as decorative placeholder art |
| A second palette for admin | D-286's one-register rule, unaffected by D-288's hue change |
| An unmeasured label on a saturated fill | white was 2.86:1 on the ember fill and 5.17:1 on the blue one — the colour is never assumed, only measured |
| Illustration-led empty states | ages badly, inflates bundle, weak at small sizes |
| Animated page transitions | P6; they delay every navigation to no informational end |
| A separate admin visual language | P4 gives admin density without a second system |

---

## 11A. Independent cross-check

This direction was cross-checked against the `ui-ux-pro-max` design database (84 styles, 192
palettes, 74 font pairings, 98 UX guidelines) **after** it was drafted. Recording what that changed
and what it did not, so the check is auditable rather than a claim.

**Corroborated — no change made**

* It independently recommends the **warm/orange event direction** for this product type: primary
  `#EA580C`, background `#FFF7ED`, warm border. That is the same family D-286 arrived at from the
  domain model, reached by a different route. **Superseded by D-288**, which chose blue on black as
  a product decision; a database recommendation corroborates a direction, it does not bind one.
* It classifies the product pattern as **Event/Conference Landing**, matching the §1 reframing.
* Its UX ruleset names *"Error messages must be announced … Don't: visual-only error indication …
  Bad: red border only"* at **High** severity — which is exactly audit finding S1-1, and exactly
  what `Input`'s `error?: boolean` prop does today.
* Touch target ≥ 44 pt is flagged **Critical**, safe-area insets **High**, decorative icons hidden
  from screen readers **Medium** — matching audit S3-5, S2-7 and S4-5.

**Overridden — and why**

* **Its palette values were not adopted, because they fail its own checklist.** Measured:
  `#FFFFFF` on `#EA580C` is **3.56:1**; primary-as-text on its own background is **3.35:1**; its
  border is **1.10:1** against the 3:1 rule. The entry ships the same class of defect D-286 exists
  to fix. Direction taken, values rejected.
* **Its typography pairing (Bebas Neue / Source Sans 3) was not adopted.** Bebas Neue is an all-caps
  condensed display face with no Devanagari and poor legibility at the caption sizes this product
  leans on. §4's single-superfamily approach serves an India-facing product better.
* **Its `MASTER.md` was deliberately not persisted.** The tool offers to write a design-system file
  into the repo; doing so would have created a second, contradicting source of truth for the
  palette, which `.claude/CLAUDE.md` §6 forbids. `packages/ui` remains the only source.

**Adopted into later phases**

* The Event/Conference conversion pattern — **sticky Register CTA**, speakers grid, agenda,
  sponsors — becomes structural input to **Phase 15** (event detail) and **Phase 35** (mobile event
  detail). The product already has speakers, sponsors and schedule surfaces to hang it on.

## 12. Cross-surface application

| | Web | Admin | Mobile |
|---|---|---|---|
| Palette | identical | identical | identical |
| Type | Anek (Latin + Devanagari) | Anek | Anek |
| Density | comfortable / compact | **dense** | comfortable |
| Radius | shared scale | shared scale | shared scale |
| Icons | lucide | lucide | Material rounded |
| Motion | shared durations | shared durations | shared durations |
| Nav | sidebar + header | sidebar + topbar | pill tab bar |

One palette, one type scale, one shape language, one motion vocabulary — three densities and two
icon sets. That is what "one product, three surfaces" means here, and it is exactly the bar Phase 41
and Phase 50 audit against.
