import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Phase 45 — a navigation has to show that it started.
 *
 * A Next server segment with no `loading.tsx` does not stream. The browser holds the *previous*
 * page, fully interactive and completely wrong, until the new one's data arrives — so a tap on an
 * event card looks exactly like a tap that did not register. Web had seven, all on deep leaf routes
 * under `/host`; the public site and the signed-in app root, which is where nearly all navigation
 * happens, had none.
 */
const WEB = resolve(__dirname, "..");

describe("every route group can stream", () => {
  it.each([
    ["(public)", "the event pages a first-time visitor lands on"],
    ["(app)", "tickets, orders, groups and settings"]
  ])("gives %s a loading fallback, covering %s", (group) => {
    expect(existsSync(resolve(WEB, "app", group, "loading.tsx"))).toBe(true);
  });

  it("announces the wait rather than only drawing it", () => {
    // A skeleton is invisible to a screen reader: without a live region the page simply goes quiet.
    for (const group of ["(public)", "(app)"]) {
      const src = readFileSync(resolve(WEB, "app", group, "loading.tsx"), "utf8");
      expect(src, group).toContain('role="status"');
      expect(src, group).toContain('aria-busy="true"');
    }
  });

  it("holds every skeleton inside the 360px content box", () => {
    /*
     * REG-017 was every public page scrolling sideways at 360. A skeleton is drawn before any content
     * exists to constrain it, so a fixed width here reintroduces that scroll during the load — which
     * is how `discover/loading.tsx` shipped a `w-96` (384px) inside a 328px box.
     */
    const offenders: string[] = [];
    const walk = (d: string): string[] =>
      readdirSync(d).flatMap((e) => {
        const full = resolve(d, e);
        return statSync(full).isDirectory() ? walk(full) : e === "loading.tsx" ? [full] : [];
      });
    for (const f of walk(resolve(WEB, "app"))) {
      for (const [i, line] of readFileSync(f, "utf8").split("\n").entries()) {
        const t = line.trim();
        if (t.startsWith("//") || t.startsWith("*") || t.startsWith("{/*")) continue;
        // A `w-N` above 80 (320px) is safe only when `w-full` caps it, or when it is a `max-w-`.
        const m = line.match(/(?<![-\w])w-(\d{2,3})(?![\w-])/);
        if (m && Number(m[1]) > 80 && !line.includes("w-full")) {
          offenders.push(`${f.split("/web/")[1]}:${i + 1}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });
});
