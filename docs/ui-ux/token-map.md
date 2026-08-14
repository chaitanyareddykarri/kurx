# Kurx Token Map — web/admin ↔ Flutter

Phase 4.6 output. The binding contract between `packages/ui` and
`mobile/lib/core/theme/`. **Phase 32 implements the Flutter column**; this file defines what it must
produce so the two systems are translatable by inspection rather than by memory.

Web/admin source of truth: `packages/ui/src/styles/tokens.css` + `packages/ui/tailwind-preset.cjs`.
Flutter source of truth: `mobile/lib/core/theme/design_tokens.dart`.

---

## 1. Colour

Flutter already carries the right *structure* — a `KurxColors` `ThemeExtension` with per-theme
constants. Phase 32 changed values and added four tokens; it did not restructure. **D-288 then
replaced every value below with the blue-on-black palette** — again a values-only change, on both
sides at once.

| Token | CSS custom property | Tailwind class | `KurxColors` field | Light | Dark |
|---|---|---|---|---|---|
| background | `--color-background` | `bg-background` | `background` | `#FFFFFF` | `#0A0A0A` |
| surface | `--color-surface` | `bg-surface` | `cardSurface` | `#F8FAFC` | `#111827` |
| elevated | `--color-elevated` | `bg-elevated` | `elevated` | `#F1F5F9` | `#161D2D` |
| border | `--color-border` | `border-border` | `border` | `#E2E8F0` | `#1F2937` |
| **border-strong** | `--color-border-strong` | `border-border-strong` | `borderStrong` | `#64748B` | `#64748B` |
| text | `--color-text` | `text-text` | `text` | `#0F172A` | `#FFFFFF` |
| muted | `--color-muted` | `text-muted` | `muted` | `#475569` | `#9CA3AF` |
| accent | `--color-accent` | `bg-accent` | `accent` | `#2563EB` | `#2563EB` |
| **on-accent** | `--color-on-accent` | `text-on-accent` | `onAccent` | `#FFFFFF` | `#FFFFFF` |
| **accent-text** | `--color-accent-text` | `text-accent-text` | `accentText` | `#1D4ED8` | `#3B82F6` |
| teal | `--color-teal` | `text-teal` | `teal` | `#1F6B5C` | `#309E88` |
| success | `--color-success` | `text-success` | `success` | `#197C36` | `#3FB950` |
| **warning** | `--color-warning` | `text-warning` | `warning` | `#876809` | `#E6B31E` |
| danger | `--color-danger` | `text-danger` | `danger` | `#CE2C2C` | `#F05C59` |

### What the two `accent` tokens are for

1. **`accent` is a fill, never text.** `#2563EB` is one ramp step below `accentText` on light — and
   one step *above* it on dark — precisely so a **white** label on it clears AA at **5.17:1**. As
   text it is 3.26:1 on the dark surfaces, and a thin 4.72:1 on the light ones, which is why the
   link colour is its own token on both themes rather than only on dark. `app_theme.dart`'s
   `filledButtonTheme` pairs
   `foregroundColor: c.onAccent` with `backgroundColor: c.accent`, so this is every primary CTA in
   the mobile app.
2. **`onAccent` is white — because the fill was measured, not because white is the default.** Under
   D-286's ember fill the identical measurement produced the opposite answer: white on `#F0762B` was
   **2.86:1** and the label had to be ink. Both suites assert the label is the *more legible* of the
   two candidates rather than a fixed colour, so the next palette gets the right answer for free.
3. **`textButtonTheme` uses `accentText`.** `#3B82F6` on dark (4.58:1 at the worst surface step),
   `#1D4ED8` on light (6.12:1). Unlike the ember palette, the two never coincide — see §1.1.
4. **`success` is not `teal`.** Teal attests (provenance, certificates, verified allies); success
   confirms an action. One value cannot mean both, and `KurxColors.light` once set them
   byte-identical.

`borderStrong` is a single value serving **both** themes (4.34:1 light, 3.54:1 dark) — a convenience
preserved through the D-288 retune.

---

## 2. Spacing — already aligned

Flutter's `KSpace` was the model the web scale was named after; no change needed.

| Web (`spacing`) | Flutter (`KSpace`) | Value |
|---|---|---|
| `xs` | `KSpace.xs` | 4 |
| `sm` | `KSpace.sm` | 8 |
| `md` | `KSpace.md` | 12 |
| `lg` | `KSpace.lg` | 16 |
| `xl` | `KSpace.xl` | 24 |
| `2xl` | `KSpace.xxl` | 32 |
| `3xl` | `KSpace.xxxl` | 48 |

---

## 3. Radius

| Web | Flutter (`KRadius`) | Value | Use |
|---|---|---|---|
| `rounded-sm` | `KRadius.sm` | 6 → **was 8** | dense controls |
| `rounded-md` | `KRadius.md` | 10 → **was 12** | buttons, inputs |
| `rounded-lg` | `KRadius.lg` | 16 | cards — the ticket corner |
| `rounded-xl` | `KRadius.xl` | 24 | sheets, modals, hero media |
| `rounded-pill` | `KRadius.pill` | 999 | chips, tags, avatars, mobile primary CTA |

Flutter's `sm` (8) and `md` (12) shift by 2px each so the two scales are identical. `lg`, `xl` and
`pill` already match.

---

## 4. Motion — already aligned

| Web | Flutter (`KMotion`) | Value |
|---|---|---|
| `duration-fast` | `KMotion.fast` | 120 ms |
| `duration-base` | `KMotion.base` | 200 ms → **was 220** |
| `duration-slow` | `KMotion.slow` | 280 ms → **was 360** |
| `ease-kurx` | `KMotion.curve` | `cubic-bezier(0.2,0.8,0.2,1)` ≡ `Curves.easeOutCubic` |

**Reduced motion.** Web is handled at the token layer — `tokens.css` carries a
`@media (prefers-reduced-motion: reduce)` block that clamps every animation and transition, so it
applies to all components including ones not yet redesigned. Flutter has no equivalent global hook:
Phase 32 must add a helper reading `MediaQuery.disableAnimationsOf(context)` and route every
`AnimatedContainer` / `Hero` / page transition through it. There are currently **zero** references
to `disableAnimations` or `accessibleNavigation` in `mobile/lib`.

---

## 5. Shadow

Both surfaces use warm-tinted shadows; a neutral grey shadow on cream reads as dirt. Flutter's
`kCardShadow` already tints with ember at 8% on light and is the model the web `boxShadow` scale was
built from.

| Web | Flutter |
|---|---|
| `shadow-sm` | (new — `kShadowSm`) |
| `shadow-md` | `kCardShadow` |
| `shadow-lg` | (new — `kShadowLg`) |

---

## 6. Typography

| Web | Size / line | Flutter `TextTheme` |
|---|---|---|
| `text-display` | 40 / 44 | `displaySmall` |
| `text-h1` | 30 / 36 | `headlineMedium` |
| `text-h2` | 24 / 30 | `headlineSmall` |
| `text-h3` | 19 / 26 | `titleLarge` |
| `text-body-lg` | 17 / 26 | `bodyLarge` |
| `text-body` | 15 / 23 | `bodyMedium` |
| `text-label` | 14 / 20 | `labelLarge` |
| `text-caption` | 13 / 18 | `bodySmall` |
| `text-micro` | 12 / 16 | `labelSmall` |

Face: **Anek** (Ek Type) on all three surfaces — D-287. Mobile currently loads Plus Jakarta Sans via
`google_fonts` and Phase 32 switches it to `GoogleFonts.anekLatin` / `anekDevanagari`.

Plus Jakarta Sans has **no Devanagari coverage**, and `web/messages/hi.json` ships a Hindi locale, so
every Hindi string currently falls back to an unrelated system face. Anek is a superfamily with
matched Latin and Devanagari cuts; browsers select per glyph, so no runtime branching is needed.

> **Deferred:** web/admin self-hosting of the binaries. `next/font` fetches at build time and this
> environment has no network, so the family is declared in the stack and degrades to the previous
> `ui-sans-serif, system-ui` rendering until the files are vendored. The token is already correct, so
> adopting the font later is a no-code change. Tracked in the Deferred ledger.

---

## 7. What does **not** cross over

| Web/admin | Mobile | Why |
|---|---|---|
| `z-index` scale | — | Flutter composes by widget tree order |
| `screens` breakpoints | — | Flutter uses `LayoutBuilder` / `MediaQuery` |
| `lucide-react` icons | Material rounded | platform expectation beats pixel identity (Phase 41 keeps a pairing map) |
| Tailwind numeric spacing | — | web-only legacy scale, retained so ~1,300 existing utilities keep working |
