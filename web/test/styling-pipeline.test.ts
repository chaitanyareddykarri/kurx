import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";
import { describe, it, expect } from "vitest";

/**
 * The global styling pipeline is load-bearing and fails silently.
 *
 * When it breaks the app does not error — it renders as browser-default HTML, which reads as a
 * catastrophic regression rather than a missing file. The failure that prompted this test was
 * environmental (a production `next build` clobbered a running dev server's `.next`, so the
 * dev-path stylesheet 404'd and the browser received `text/html` where CSS should have been), but
 * the same blank-page symptom follows from any break in the chain below, and those ARE source
 * changes a reviewer can miss:
 *
 *   root layout imports tokens.css + globals.css
 *     -> globals.css carries the Tailwind directives
 *       -> postcss runs tailwindcss
 *         -> tailwind's content globs cover every directory that emits classes
 *
 * Each link is asserted separately so a failure names the one that broke.
 */

const WEB = resolve(__dirname, "..");
const read = (p: string) => readFileSync(resolve(WEB, p), "utf8");

/** Source with comment lines stripped. Asserting on the raw text is not enough: a commented-out
 *  `// import "./globals.css";` still satisfies a substring match, so the first version of this
 *  test passed with the import disabled — the exact failure it exists to catch. */
const active = (p: string) =>
  read(p)
    .split("\n")
    .filter((l) => !l.trim().startsWith("//") && !l.trim().startsWith("*") && !l.trim().startsWith("/*"))
    .join("\n");

describe("global styling pipeline", () => {
  it("the root layout imports the design tokens and the global stylesheet", () => {
    const layout = active("app/layout.tsx");
    // Tokens first: globals.css consumes --color-* via rgb(var(...)), so the order is not cosmetic.
    expect(layout).toContain('import "@kurx/ui/styles/tokens.css"');
    expect(layout).toContain('import "./globals.css"');
  });

  it("globals.css carries the Tailwind directives and consumes the shared tokens", () => {
    const globals = active("app/globals.css");
    for (const directive of ["@tailwind base", "@tailwind components", "@tailwind utilities"]) {
      expect(globals).toContain(directive);
    }
    // If this stops referencing the token variables, the tokens import above is dead weight and the
    // palette silently falls back to browser defaults rather than failing.
    expect(globals).toContain("var(--color-background)");
  });

  it("postcss runs tailwind", () => {
    const postcss = active("postcss.config.js");
    expect(postcss).toContain("tailwindcss");
  });

  it("tailwind scans every directory that emits classes, including the shared package", () => {
    const config = active("tailwind.config.ts");
    // A directory missing here does not error — its classes are purged, so only the pages using
    // them look broken. The shared package is the one most easily forgotten: it lives outside the
    // app root, so a default config would not reach it.
    for (const glob of ["./app/**/*.{ts,tsx}", "./components/**/*.{ts,tsx}", "../packages/ui/src/**/*.{ts,tsx}"]) {
      expect(config).toContain(glob);
    }
  });

  it("the shared token stylesheet exists and defines the palette the app renders against", () => {
    const tokens = resolve(WEB, "..", "packages", "ui", "src", "styles", "tokens.css");
    expect(existsSync(tokens)).toBe(true);
    const css = readFileSync(tokens, "utf8");
    // Named explicitly rather than counting declarations: these are the two globals.css binds to
    // body, so losing either is what turns the page into unstyled HTML.
    expect(css).toContain("--color-background");
    expect(css).toContain("--color-text");
  });
});
