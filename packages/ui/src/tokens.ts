/**
 * Kurx design tokens, JavaScript view.
 *
 * `styles/tokens.css` is the runtime source of truth — it defines the `--color-*`
 * custom properties that the Tailwind preset resolves, and it is what every
 * component actually renders against. This file exists for the handful of
 * contexts where a CSS custom property cannot reach:
 *
 *   - `next/og` ImageResponse (`icon.tsx`, `opengraph-image.tsx`) renders in an
 *     isolated Satori environment with no stylesheet and no cascade.
 *   - `manifest.ts` and the `viewport.themeColor` metadata export are serialised
 *     to JSON/HTML at build time, long before any CSS is parsed.
 *
 * Those consumers previously kept their own hardcoded copy of the palette in
 * `web/lib/design-tokens.ts` — a third copy alongside `tokens.css` and web's
 * inlined Tailwind config, none of which were checked against each other
 * (D-285). This module replaces that copy, and `web/test/design-tokens.test.ts`
 * asserts it stays equivalent to `tokens.css` AND re-measures every contrast
 * pair, so neither drift nor a sub-threshold value can land.
 *
 * Values are `"R G B"` triplet strings to match the CSS custom-property format
 * exactly; `hex()` converts for consumers that need `#rrggbb`.
 *
 * Palette rationale, including why the fill and the link are two different blues:
 * D-288 and `docs/ui-ux/visual-identity.md`.
 */

export type ColorToken =
  | "background"
  | "surface"
  | "elevated"
  | "border"
  | "borderStrong"
  | "text"
  | "muted"
  | "accent"
  | "onAccent"
  | "accentText"
  | "teal"
  | "success"
  | "warning"
  | "danger";

export type Palette = Record<ColorToken, string>;

/** Dark is the default theme (`:root` in tokens.css). */
export const dark: Palette = {
  background: "10 10 10",       // #0A0A0A
  surface: "17 24 39",          // #111827
  elevated: "22 29 45",         // #161D2D
  border: "31 41 55",           // #1F2937
  borderStrong: "100 116 139",  // #64748B — 3.54:1 worst step
  text: "255 255 255",          // #FFFFFF — 16.83:1 worst step
  muted: "156 163 175",         // #9CA3AF — 6.63:1 worst step
  accent: "37 99 235",          // #2563EB — the FILL; white label is 5.17:1
  onAccent: "255 255 255",      // #FFFFFF
  accentText: "59 130 246",     // #3B82F6 — 4.58:1 worst step
  teal: "48 158 136",
  success: "63 185 80",
  warning: "230 179 30",
  danger: "240 92 89"
};

/** Light overrides, applied by the `.light` class (next-themes toggles it). */
export const light: Palette = {
  background: "255 255 255",    // #FFFFFF
  surface: "248 250 252",       // #F8FAFC
  elevated: "241 245 249",      // #F1F5F9
  border: "226 232 240",        // #E2E8F0
  borderStrong: "100 116 139",  // #64748B — 4.34:1 worst step; one value serves both themes
  text: "15 23 42",             // #0F172A — 16.30:1 worst step
  muted: "71 85 105",           // #475569 — 6.92:1 worst step
  accent: "37 99 235",          // #2563EB — same fill in both themes
  onAccent: "255 255 255",      // #FFFFFF
  accentText: "29 78 216",      // #1D4ED8 — 6.12:1 worst step; the fill clears light at only 4.72:1
  teal: "31 107 92",
  success: "25 124 54",
  warning: "135 104 9",
  danger: "206 44 44"
};

/** CSS custom-property name for a token: `borderStrong` → `--color-border-strong`. */
export function cssVar(token: ColorToken): string {
  return "--color-" + token.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase());
}

/** `"13 17 23"` → `"#0D1117"`. */
export function hex(triplet: string): string {
  const [r, g, b] = triplet.split(/\s+/).map(Number);
  return "#" + [r, g, b].map((n) => n.toString(16).padStart(2, "0")).join("").toUpperCase();
}

/** Relative luminance, WCAG 2.1 §relative-luminance. */
export function luminance(triplet: string): number {
  const [r, g, b] = triplet.split(/\s+/).map((n) => Number(n) / 255);
  const f = (c: number) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

/** Contrast ratio between two triplets, 1–21. */
export function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

function toHexPalette(p: Palette): Record<ColorToken, string> {
  return Object.fromEntries(Object.entries(p).map(([k, v]) => [k, hex(v)])) as Record<ColorToken, string>;
}

/**
 * Hex palette for the default (dark) theme — what a build-time consumer wants
 * when it can only emit one colour and the app boots dark.
 */
export const themeHex = toHexPalette(dark);

/** Hex palette for the light theme. */
export const themeHexLight = toHexPalette(light);

/** The three surface steps a foreground must be legible against, in order. */
export const SURFACE_STEPS: ColorToken[] = ["background", "surface", "elevated"];

/** Tokens that carry text and must clear WCAG AA (4.5:1) on every surface step. */
export const TEXT_TOKENS: ColorToken[] = [
  "text",
  "muted",
  "accentText",
  "teal",
  "success",
  "warning",
  "danger"
];
