/**
 * Kurx shared Tailwind preset — the single source of truth for the design tokens.
 * Consumed by both `web` and `admin` so the two surfaces are visually identical by
 * construction. Colours resolve to the CSS custom properties defined in
 * `src/styles/tokens.css` (dark = :root, light = .light).
 *
 * Consumers set only their own `content` globs (and must include this package's
 * source so its classes survive purge).
 *
 * Scales added in Phase 4 (D-285 §3): before it, web and admin had NINE COLOUR
 * TOKENS AND NOTHING ELSE — no typography, spacing, radius, shadow, opacity,
 * z-index, duration, easing or breakpoint tokens, so every author picked those by
 * eye. The audit measured the result: 5 competing radius values, 15+ padding
 * values including half-steps belonging to no scale, and 88% of all text at
 * `text-sm`/`text-xs` with five uses of `text-base`.
 *
 * Names deliberately mirror Flutter's existing `KSpace` / `KRadius` / `KMotion`
 * (`mobile/lib/core/theme/design_tokens.dart`) so the two systems are
 * translatable by inspection rather than by memory.
 *
 * ADDITIVE BY DESIGN: Tailwind's numeric spacing scale and default breakpoints
 * are left intact, because ~1,300 existing utility uses depend on them and
 * redefining them would be a silent, repo-wide layout change. The semantic
 * scales below sit alongside them and are what new work uses.
 *
 * @type {Partial<import('tailwindcss').Config>}
 */
module.exports = {
  darkMode: ["class"],
  theme: {
    extend: {
      colors: {
        background: "rgb(var(--color-background) / <alpha-value>)",
        surface: "rgb(var(--color-surface) / <alpha-value>)",
        elevated: "rgb(var(--color-elevated) / <alpha-value>)",
        border: "rgb(var(--color-border) / <alpha-value>)",
        // Boundaries that identify a control (inputs, selects). >= 3:1, WCAG 1.4.11.
        "border-strong": "rgb(var(--color-border-strong) / <alpha-value>)",
        text: "rgb(var(--color-text) / <alpha-value>)",
        muted: "rgb(var(--color-muted) / <alpha-value>)",
        // Blue FILL — a surface. Not text-safe on dark (3.26:1); use accent-text.
        accent: "rgb(var(--color-accent) / <alpha-value>)",
        // The label ON a blue fill: white, measured at 5.17:1 (D-288).
        "on-accent": "rgb(var(--color-on-accent) / <alpha-value>)",
        // Blue as TEXT — links, text buttons. A lighter step than the fill.
        "accent-text": "rgb(var(--color-accent-text) / <alpha-value>)",
        // Attests: provenance, verification, certificates. Never generic success.
        teal: "rgb(var(--color-teal) / <alpha-value>)",
        success: "rgb(var(--color-success) / <alpha-value>)",
        warning: "rgb(var(--color-warning) / <alpha-value>)",
        danger: "rgb(var(--color-danger) / <alpha-value>)"
      },

      // Semantic type scale. `body` is the one the product did not have.
      fontSize: {
        display: ["2.5rem", { lineHeight: "2.75rem", fontWeight: "700", letterSpacing: "-0.02em" }],
        h1: ["1.875rem", { lineHeight: "2.25rem", fontWeight: "700", letterSpacing: "-0.015em" }],
        h2: ["1.5rem", { lineHeight: "1.875rem", fontWeight: "650", letterSpacing: "-0.01em" }],
        h3: ["1.1875rem", { lineHeight: "1.625rem", fontWeight: "650" }],
        "body-lg": ["1.0625rem", { lineHeight: "1.625rem" }],
        body: ["0.9375rem", { lineHeight: "1.4375rem" }],
        label: ["0.875rem", { lineHeight: "1.25rem", fontWeight: "550" }],
        caption: ["0.8125rem", { lineHeight: "1.125rem" }],
        micro: ["0.75rem", { lineHeight: "1rem", fontWeight: "550" }]
      },

      // The ticket-corner scale (visual-identity P5). Replaces five drifting values.
      borderRadius: {
        DEFAULT: "6px",
        sm: "6px",
        md: "10px",
        lg: "16px",
        xl: "24px",
        pill: "999px"
      },

      // Blue-tinted shadows (D-288). The warm ember tint these carried under D-286
      // reads as a brown smudge under a blue accent; the second layer now tints
      // with the accent itself, which is what mobile's kCardShadow does.
      boxShadow: {
        sm: "0 1px 2px rgb(10 10 10 / 0.06), 0 1px 1px rgb(37 99 235 / 0.04)",
        md: "0 4px 12px rgb(10 10 10 / 0.08), 0 1px 3px rgb(37 99 235 / 0.06)",
        lg: "0 12px 32px rgb(10 10 10 / 0.12), 0 4px 8px rgb(37 99 235 / 0.06)",
        // Retained so the ~pre-D-286 call sites keep compiling; maps to `md`.
        // Migrated away from in Phase 6; do not use in new work.
        github: "0 4px 12px rgb(10 10 10 / 0.08), 0 1px 3px rgb(37 99 235 / 0.06)"
      },

      // Mirrors Flutter's KMotion.{fast,base,slow}.
      transitionDuration: {
        fast: "120ms",
        base: "200ms",
        slow: "280ms"
      },
      transitionTimingFunction: {
        // Flutter: Curves.easeOutCubic.
        kurx: "cubic-bezier(0.2, 0.8, 0.2, 1)"
      },

      // Named so stacking order is a decision, not an escalating integer.
      zIndex: {
        base: "0",
        raised: "10",
        sticky: "20",
        header: "30",
        overlay: "40",
        modal: "50",
        toast: "60"
      },

      // Mirrors Flutter's KSpace (4px base). Numeric Tailwind spacing is untouched.
      spacing: {
        xs: "4px",
        sm: "8px",
        md: "12px",
        lg: "16px",
        xl: "24px",
        "2xl": "32px",
        "3xl": "48px"
      },

      // ADDED, not redefined — sm/md/lg/xl/2xl keep their Tailwind defaults so
      // existing responsive utilities are unaffected. These name the two ends of
      // the six widths the redesign verifies against.
      screens: {
        xs: "375px",
        wide: "1440px",
        ultra: "1920px"
      },

      fontFamily: {
        // Anek (Ek Type) — D-287. A superfamily with matched Latin and Devanagari
        // cuts, which Plus Jakarta Sans has no answer for: web/messages/hi.json
        // exists, so Hindi currently falls back to an unrelated system face
        // mid-page. Browsers select per glyph, so Latin renders from "Anek Latin"
        // and Devanagari from "Anek Devanagari" without any runtime branching.
        //
        // Self-hosting is deferred (no network to vendor the binaries), so the
        // stack degrades to the previous ui-sans-serif rendering rather than
        // blocking on a webfont that is not yet present. Adopting it later is a
        // no-code change — this token is already correct.
        sans: ['"Anek Latin"', '"Anek Devanagari"', "ui-sans-serif", "system-ui", "sans-serif"]
      }
    }
  },
  plugins: []
};
