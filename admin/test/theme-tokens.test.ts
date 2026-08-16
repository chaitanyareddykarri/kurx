import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Colour comes from the design system's semantic tokens, never from Tailwind's fixed palette.
 *
 * **What went wrong.** The web certificate module was written with `text-slate-900`, `bg-slate-900`
 * and friends — 134 of them — and the numbers on a dark surface came out painted almost exactly the
 * background colour. The console shares the same token set and had drifted the same way in one place
 * (a `bg-amber-500` severity dot sitting between a `bg-danger` and a `bg-accent` sibling).
 *
 * The console is a smaller surface, so this is a guard against recurrence rather than a cleanup.
 *
 * **Why a lint-style test rather than an ESLint rule.** The rule is about one repo-specific vocabulary
 * (`text`, `muted`, `surface`, `accent`, `danger`…) defined in `packages/ui/tailwind-preset.cjs`, and a
 * failing test names the offending file and class directly. A generic no-restricted-syntax rule would
 * have to encode the same list somewhere less discoverable.
 */

const ROOTS = ["components", "app"];

/** Tailwind's fixed palettes. `white`/`black` are deliberately NOT here: they are legitimate on a fill
 *  whose colour is known (`text-on-accent` resolves to white), and banning them would push authors
 *  toward worse workarounds. */
const PALETTES = [
  "slate", "gray", "zinc", "neutral", "stone",
  "red", "orange", "amber", "yellow", "lime", "green", "emerald", "teal",
  "cyan", "sky", "blue", "indigo", "violet", "purple", "fuchsia", "pink", "rose"
];

const PROPERTIES = ["text", "bg", "border", "ring", "divide", "placeholder", "from", "via", "to", "shadow", "outline", "decoration", "accent", "caret", "fill", "stroke"];

const RAW_COLOUR = new RegExp(
  `\\b(?:${PROPERTIES.join("|")})-(?:${PALETTES.join("|")})-\\d{2,3}(?:/\\d{1,3})?\\b`,
  "g"
);

function walk(dir: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules" || entry.startsWith(".")) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) out.push(...walk(full));
    else if (/\.(tsx|ts)$/.test(entry)) out.push(full);
  }
  return out;
}

describe("colour is themeable", () => {
  it("no component reaches past the design system into Tailwind's fixed palette", () => {
    const offences: string[] = [];

    for (const root of ROOTS) {
      for (const file of walk(root)) {
        const source = readFileSync(file, "utf8");
        for (const [index, line] of source.split("\n").entries()) {
          // The token list itself, and prose describing the bug, are allowed to name palette values.
          if (line.includes("eslint-disable") || line.trimStart().startsWith("*")) continue;
          const matches = line.match(RAW_COLOUR);
          if (matches) offences.push(`${file}:${index + 1}  ${[...new Set(matches)].join(", ")}`);
        }
      }
    }

    expect(
      offences,
      "Use the semantic tokens from packages/ui/tailwind-preset.cjs — text, muted, surface, elevated, " +
        "border, border-strong, accent, on-accent, accent-text, teal, success, warning, danger. A fixed " +
        "palette value does not move with the theme, so it is invisible in one of the two.\n\n" +
        offences.join("\n")
    ).toEqual([]);
  });
});
