import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

import {
  SURFACE_STEPS,
  TEXT_TOKENS,
  contrast,
  cssVar,
  dark,
  hex,
  light,
  luminance,
  themeHex,
  type ColorToken,
  type Palette
} from "@kurx/ui";

/**
 * Two jobs.
 *
 * 1. The palette necessarily exists twice — `tokens.css` is what the browser
 *    reads, `tokens.ts` is what build-time consumers read (OG images, manifest,
 *    viewport themeColor) because they render before any stylesheet exists. Two
 *    copies that nothing compares are how the four-way drift in D-285 happened.
 *
 * 2. D-286 chose its values by measuring contrast. A measurement taken once is a
 *    claim; a measurement taken on every run is a guarantee. Every foreground
 *    token is re-checked against all three surface steps here, so no future edit
 *    can quietly drop a value below the floor — which is exactly how mobile
 *    shipped a 2.86:1 primary button under D-065 and nothing caught it.
 */

// Resolved from the vitest cwd (web/), not import.meta.url — Vite rewrites module
// URLs during transform, so they are not guaranteed to carry a file: scheme.
const TOKENS_CSS = resolve(process.cwd(), "../packages/ui/src/styles/tokens.css");
if (!existsSync(TOKENS_CSS)) {
  throw new Error(`tokens.css not found at ${TOKENS_CSS} — this guard cannot verify anything.`);
}
const css = readFileSync(TOKENS_CSS, "utf8");

/** Pull the `--color-*` declarations out of one CSS block, keyed by token name. */
function block(selector: string): Record<string, string> {
  const start = css.indexOf(`${selector} {`);
  expect(start, `tokens.css has no "${selector}" block`).toBeGreaterThan(-1);
  const body = css.slice(start, css.indexOf("}", start));
  const out: Record<string, string> = {};
  for (const m of body.matchAll(/--color-([a-z-]+):\s*([^;]+);/g)) {
    // `--color-border-strong` → `borderStrong`, matching the TS palette keys.
    out[m[1].replace(/-([a-z])/g, (_, c) => c.toUpperCase())] = m[2].trim();
  }
  return out;
}

const THEMES: Array<[string, string, Palette]> = [
  ["dark", ":root", dark],
  ["light", ".light", light]
];

describe("tokens.ts stays in lockstep with tokens.css", () => {
  it.each(THEMES)("%s matches exactly", (_name, selector, palette) => {
    expect(block(selector)).toEqual(palette);
  });

  it("defines the same token names in both themes", () => {
    expect(Object.keys(dark).sort()).toEqual(Object.keys(light).sort());
  });

  it("maps token names to the CSS custom properties actually declared", () => {
    for (const token of Object.keys(dark) as ColorToken[]) {
      expect(css, `${token} → ${cssVar(token)} missing from tokens.css`).toContain(`${cssVar(token)}:`);
    }
  });

  it("emits well-formed triplets", () => {
    for (const [name, value] of [...Object.entries(dark), ...Object.entries(light)]) {
      expect(value, `${name} is not an "R G B" triplet`).toMatch(/^\d{1,3} \d{1,3} \d{1,3}$/);
      for (const channel of value.split(" ")) {
        expect(Number(channel)).toBeLessThanOrEqual(255);
      }
    }
  });

  it("converts triplets to hex for build-time consumers", () => {
    // Consumed by icon.tsx, opengraph-image.tsx, manifest.ts and viewport.themeColor.
    expect(hex("13 17 23")).toBe("#0D1117");
    expect(themeHex.background).toBe(hex(dark.background));
    expect(themeHex.accent).toBe(hex(dark.accent));
  });
});

describe("every palette meets its WCAG floor (D-286)", () => {
  it.each(THEMES)("%s: text tokens clear 4.5:1 on every surface step", (name, _sel, palette) => {
    for (const token of TEXT_TOKENS) {
      for (const step of SURFACE_STEPS) {
        const ratio = contrast(palette[token], palette[step]);
        expect(
          ratio,
          `${name}: ${token} on ${step} is ${ratio.toFixed(2)}:1, below the 4.5:1 AA floor`
        ).toBeGreaterThanOrEqual(4.5);
      }
    }
  });

  it.each(THEMES)("%s: borderStrong clears 3:1 on every surface step", (name, _sel, palette) => {
    for (const step of SURFACE_STEPS) {
      const ratio = contrast(palette.borderStrong, palette[step]);
      expect(
        ratio,
        `${name}: borderStrong on ${step} is ${ratio.toFixed(2)}:1, below the 3:1 WCAG 1.4.11 floor`
      ).toBeGreaterThanOrEqual(3);
    }
  });

  it.each(THEMES)("%s: on-accent is legible on the accent fill", (name, _sel, palette) => {
    // The D-065 regression this exists to prevent: white on the ember fill #F0762B
    // was 2.86:1, and that was every primary CTA in the mobile app.
    const ratio = contrast(palette.onAccent, palette.accent);
    expect(ratio, `${name}: on-accent/accent is ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(4.5);

    // ...and it must be the BETTER of the two candidates, not merely a passing one.
    // D-286 read its own measurement as "the label is ink"; it was really "measure
    // the fill". On D-288's #2563EB the same measurement answers white (5.17:1 vs
    // ink's 4.67:1), so the rule is stated as a comparison rather than a constant.
    const WHITE = "255 255 255";
    // The palette's own darkest value is the ink candidate: `background` on dark,
    // `text` on light.
    const ink = [palette.background, palette.text].sort((a, b) => luminance(a) - luminance(b))[0];
    const better = contrast(WHITE, palette.accent) >= contrast(ink, palette.accent) ? WHITE : ink;
    expect(palette.onAccent, `${name}: on-accent is not the more legible label on the fill`).toBe(better);
  });

  it.each(THEMES)("%s: the fill and the link are different blues on purpose", (name, _sel, palette) => {
    // A fill dark enough to carry a white label is too dark to read as text on a
    // near-black surface, and vice versa. One token cannot do both jobs (D-288).
    expect(palette.accent, `${name}: accent and accent-text collapsed into one value`)
      .not.toBe(palette.accentText);
  });

  it("keeps teal distinct from success so provenance is not diluted", () => {
    for (const palette of [dark, light]) {
      expect(palette.teal).not.toBe(palette.success);
    }
  });

  it("keeps warning distinct in hue from accent, not merely in value", () => {
    // A warning that is just a darker ember is not a different signal. Compare the
    // red:green ratio, which separates gold (~45°) from ember (~23°) robustly.
    for (const [name, palette] of [
      ["dark", dark],
      ["light", light]
    ] as const) {
      const skew = (t: string) => {
        const [r, g] = t.split(" ").map(Number);
        return r / g;
      };
      expect(
        Math.abs(skew(palette.warning) - skew(palette.accent)),
        `${name}: warning and accent are too close in hue to read as different signals`
      ).toBeGreaterThan(0.3);
    }
  });
});

/**
 * A colour class that names no token produces no style at all — silently.
 *
 * Tailwind emits nothing for `bg-card`, and nothing is exactly what it looked like: 20 usages across
 * 8 files named tokens this project has never defined, most of them shadcn's vocabulary
 * (`card`, `fg`, `destructive`, `muted-foreground`). Every incoming chat bubble rendered with no
 * background and no text colour, and every auth error message — login, recovery, passkey — was
 * styled `text-destructive` and therefore was not red.
 *
 * Nothing catches this: it is not a type error, not a lint error, and not a build error. Only a
 * screenshot or this test.
 */
describe("colour classes must name real tokens", () => {
  const PHANTOMS = [
    "card", "fg", "foreground", "destructive", "muted-foreground",
    "popover", "primary", "secondary", "input", "ring"
  ];
  const PREFIXES = ["bg", "text", "border", "ring", "fill", "stroke", "from", "to", "via"];
  const pattern = new RegExp(
    `(?<![\\w-])(?:${PREFIXES.join("|")})-(?:${PHANTOMS.join("|")})(?![\\w-])`,
    "g"
  );

  const roots = ["app", "components", "lib"];

  function walk(dir: string): string[] {
    const { readdirSync, statSync } = require("node:fs") as typeof import("node:fs");
    if (!existsSync(dir)) return [];
    return readdirSync(dir).flatMap((entry: string) => {
      const full = resolve(dir, entry);
      if (statSync(full).isDirectory()) return walk(full);
      return /\.tsx?$/.test(entry) ? [full] : [];
    });
  }

  it("uses no colour class outside the Kurx palette", () => {
    const offenders: string[] = [];
    for (const root of roots) {
      for (const file of walk(resolve(__dirname, "..", root))) {
        const source = readFileSync(file, "utf8");
        for (const hit of source.match(pattern) ?? []) {
          offenders.push(`${file.split("/web/")[1]}: ${hit}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });
});

/**
 * One palette across three surfaces.
 *
 * **UX-1 / D-286** chose a single visual register for web, admin and mobile, superseding D-065's
 * two-register split. Web and admin share it by construction — both consume `@kurx/ui` — but Flutter
 * necessarily keeps its own copy in `design_tokens.dart`, because Dart cannot read a CSS custom
 * property. Two copies that nothing compares are exactly how the four-way colour drift in D-285
 * happened, and that was *within* one surface.
 *
 * Phase 41 measured the two and found them identical across all 28 comparisons. This keeps them
 * that way: nothing else in the repo would notice if a value were changed on one side only, and the
 * symptom would be a product that looks like two products.
 */
describe("the Flutter palette matches the web one", () => {
  const dart = readFileSync(
    resolve(__dirname, "..", "..", "mobile/lib/core/theme/design_tokens.dart"),
    "utf8"
  );

  /**
   * Dart field name → `Palette` key. Identical apart from one: Flutter calls the card fill
   * `cardSurface` where the palette calls it `surface`, which is precisely the kind of quiet
   * divergence a comparison has to spell out rather than assume away.
   */
  const PAIRS: Record<string, ColorToken> = {
    accent: "accent",
    onAccent: "onAccent",
    accentText: "accentText",
    teal: "teal",
    background: "background",
    cardSurface: "surface",
    elevated: "elevated",
    border: "border",
    borderStrong: "borderStrong",
    text: "text",
    muted: "muted",
    success: "success",
    warning: "warning",
    danger: "danger"
  };

  /** The two `Color(0xFF……)` palettes in file order: light first, then dark. */
  function flutterPalettes(): Record<string, string>[] {
    const out: Record<string, string>[] = [];
    let current: Record<string, string> = {};
    for (const m of dart.matchAll(/(\w+):\s+Color\(0x(FF[0-9A-Fa-f]{6})\)/g)) {
      if (m[1] in current) {
        out.push(current);
        current = {};
      }
      current[m[1]] = `#${m[2].slice(2).toUpperCase()}`;
    }
    out.push(current);
    return out;
  }

  const [flutterLight, flutterDark] = flutterPalettes();

  it.each([
    ["light", light, flutterLight],
    ["dark", dark, flutterDark]
  ])("agrees on every %s token", (_theme, webPalette, flutterPalette) => {
    const mismatches: string[] = [];
    for (const [dartName, key] of Object.entries(PAIRS)) {
      const webHex = hex(webPalette[key]).toUpperCase();
      const flutterHex = flutterPalette[dartName];
      if (flutterHex && webHex !== flutterHex) {
        mismatches.push(`${dartName}: web ${webHex} vs flutter ${flutterHex}`);
      }
    }
    expect(mismatches).toEqual([]);
  });
});
