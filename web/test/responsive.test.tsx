import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { Dialog } from "@kurx/ui";

/**
 * Guards for Phase 25 — the responsive pass.
 *
 * The pass itself was done in a real browser: twelve routes measured at 360 / 414 / 768 / 1024 /
 * 1440 / 1920, checking `scrollWidth` against `clientWidth` and every non-inline control against the
 * 44px floor. D-285 rules out committing that harness — no Playwright in this repo — so what is
 * kept here is the part that *can* be asserted without one: the shapes whose return would reproduce
 * the defects that pass found.
 *
 * The six widths and their criteria are `docs/ui-ux/regression-criteria.md` §2.5.
 */

const CONTENT_BOX_AT_360 = 328; // 360 minus `container-shell`'s 32px gutter.

/**
 * Lines that are prose, not markup.
 *
 * These sweeps read source text, so a comment *describing* a defect reads exactly like the defect —
 * the first run of the width guard flagged its own explanation of `w-96`. Tracks `{/* … *\/}` and
 * `/* … *\/` blocks as well as `//`, so a comment can name the shape it is warning about.
 */
function codeLines(source: string): { line: string; n: number }[] {
  const out: { line: string; n: number }[] = [];
  let inBlock = false;
  source.split("\n").forEach((line, i) => {
    const t = line.trim();
    const opens = t.startsWith("{/*") || t.startsWith("/*");
    const closes = t.includes("*/");
    if (inBlock) {
      if (closes) inBlock = false;
      return;
    }
    if (opens) {
      if (!closes) inBlock = true;
      return;
    }
    if (t.startsWith("//") || t.startsWith("*")) return;
    out.push({ line, n: i + 1 });
  });
  return out;
}

function webSources(dir: string): { path: string; source: string }[] {
  const { readdirSync, statSync } = require("node:fs") as typeof import("node:fs");
  const walk = (d: string): string[] =>
    readdirSync(d).flatMap((entry: string) => {
      const full = resolve(d, entry);
      return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(entry) ? [full] : [];
    });
  return walk(resolve(__dirname, "..", dir)).map((path) => ({
    path: path.split("/web/")[1],
    source: readFileSync(path, "utf8"),
  }));
}

describe("nothing may be wider than the 360px content box", () => {
  it("declares no fixed width past the floor without a responsive prefix", () => {
    /*
     * `/discover`'s loading skeleton carried `w-96` — 384px in a 328px box — so the page scrolled
     * sideways before any content had even loaded.
     */
    const offenders: string[] = [];
    for (const { path, source } of [...webSources("app"), ...webSources("components")]) {
      for (const { line, n } of codeLines(source)) {
        // Unprefixed `w-N` on the spacing scale, and unprefixed `w-[Npx]`.
        for (const m of line.matchAll(/(?<![\w:-])w-(\d{2,3})(?![\w.[])/g)) {
          if (Number(m[1]) * 4 > CONTENT_BOX_AT_360) offenders.push(`${path}:${n} ${m[0]}`);
        }
        for (const m of line.matchAll(/(?<![\w:-])w-\[(\d+)px\]/g)) {
          if (Number(m[1]) > CONTENT_BOX_AT_360) offenders.push(`${path}:${n} ${m[0]}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("keeps every table inside its own horizontal scroll container", () => {
    // §2.5 allows a wide table to scroll — inside itself, never by moving the page.
    const offenders: string[] = [];
    for (const { path, source } of [...webSources("app"), ...webSources("components")]) {
      const lines = source.split("\n");
      lines.forEach((line, i) => {
        if (!line.includes("<table")) return;
        const above = lines.slice(Math.max(0, i - 4), i + 1).join("\n");
        if (!/overflow-x-auto|overflow-auto/.test(above)) offenders.push(`${path}:${i + 1}`);
      });
    }
    expect(offenders).toEqual([]);
  });
});

describe("Dialog must stay inside the viewport", () => {
  it("bounds its height and scrolls its body, not the page", () => {
    /*
     * The panel had no vertical bound and its body no overflow, so a dialog taller than the viewport
     * grew past it — and because the overlay centres its child, it overflowed off the top and the
     * bottom at once. The footer holds Cancel and Confirm, so on a short viewport (a phone in
     * landscape is 640×360) the confirmation could be neither confirmed nor dismissed except by
     * Escape. This is the primitive behind every ConfirmDialog on web and admin.
     */
    render(
      <Dialog open onClose={() => {}} title="Tall" footer={<button type="button">Confirm</button>}>
        <p>body</p>
      </Dialog>
    );
    const panel = screen.getByRole("dialog");
    expect(panel.className).toMatch(/max-h-\[calc\(100dvh/);
    expect(panel.className).toMatch(/\bflex\b/);
    expect(panel.className).toMatch(/flex-col/);

    // The body scrolls; the header and footer hold their size so the actions stay reachable.
    const body = screen.getByText("body").parentElement!;
    expect(body.className).toMatch(/overflow-y-auto/);
    expect(body.className).toMatch(/flex-1/);
    expect(screen.getByRole("button", { name: "Confirm" }).parentElement!.className).toMatch(/shrink-0/);
  });
});

describe("the public header fits the narrowest supported width", () => {
  it("holds the page-link row back until it fits", () => {
    /*
     * At 768 the links row measured 379px and the actions 387px beside a 71px logo — 869px of
     * content in a 736px container, so every public page scrolled sideways at exactly the tablet
     * width. It first fits at 1024. Between 768 and 1023 those destinations stay reachable from the
     * footer, which is why Phase 11 added one.
     */
    const nav = readFileSync(resolve(__dirname, "..", "components/layout/public-nav.tsx"), "utf8");
    expect(nav).toMatch(/hidden items-center gap-1 lg:flex/);
    expect(nav).not.toMatch(/hidden items-center gap-1 md:flex/);
  });

  it("keeps the primary CTA icon-only until its label fits", () => {
    const nav = readFileSync(resolve(__dirname, "..", "components/layout/public-nav.tsx"), "utf8");
    // Icon-only needs an explicit name, or the control is unlabelled below `sm`.
    expect(nav).toMatch(/aria-label=\{t\("getApp"\)\}/);
    expect(nav).toMatch(/hidden sm:inline/);
  });
});
