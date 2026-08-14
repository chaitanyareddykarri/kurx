import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import NotFound from "@/app/not-found";
import RootError from "@/app/error";

/**
 * Phase 44 — a failure must leave a way out.
 *
 * Web had exactly one `error.tsx`, on `/u/[username]`. Every other route — the public site, the
 * signed-in app, checkout, tickets — fell through to Next's built-in screen, which in production is
 * an unstyled "Application error: a client-side exception has occurred" with no branding, no retry
 * and no navigation. There was no `not-found.tsx` either, and D-018 routes every hidden resource to
 * a 404 deliberately, so that page is reached by design rather than by accident.
 */
describe("a broken page offers both a retry and a way out", () => {
  it("retries the segment", () => {
    const reset = vi.fn();
    render(<RootError error={new Error("boom")} reset={reset} />);
    screen.getByRole("button", { name: /Try again/ }).click();
    expect(reset).toHaveBeenCalled();
  });

  it("also offers a link home, because reset cannot fix a page that is broken rather than flaky", () => {
    render(<RootError error={new Error("boom")} reset={vi.fn()} />);
    expect(screen.getByRole("link", { name: /homepage/i })).toHaveAttribute("href", "/");
  });

  it("says nothing about why", () => {
    // The cause is in the correlation id the API logs. Surfacing it here would hand internals to
    // anyone who can trigger a 500.
    render(<RootError error={new Error("Npgsql: relation \"events\" does not exist")} reset={vi.fn()} />);
    expect(document.body.textContent).not.toContain("Npgsql");
  });
});

describe("the 404", () => {
  it("does not claim the thing does not exist", () => {
    render(<NotFound />);
    // D-018 sends a hidden-but-real resource here. "This doesn't exist" would be the exact
    // disclosure the 404 is there to avoid.
    const text = document.body.textContent ?? "";
    expect(text).toMatch(/couldn't find/i);
    expect(text).not.toMatch(/does not exist|doesn't exist|no such/i);
  });

  it("leads somewhere that exists", () => {
    render(<NotFound />);
    // This test previously asserted a "Browse events" link to `/events` — a route with no page.
    // Asserting an href proves the markup, never that the target resolves; Phase 47 caught it by
    // loading the page. Every link here must point at a route that exists in `app/(public)`.
    expect(screen.getByRole("link", { name: /homepage/i })).toHaveAttribute("href", "/");
    for (const a of screen.getAllByRole("link")) {
      expect(a.getAttribute("href")).toBe("/");
    }
  });
});

describe("every surface has its boundaries", () => {
  it.each([
    ["web/app/error.tsx", "a route segment throwing"],
    ["web/app/global-error.tsx", "the root layout throwing"],
    ["web/app/not-found.tsx", "notFound() and D-018's hidden resources"],
    ["admin/app/error.tsx", "a console segment throwing"],
    ["admin/app/global-error.tsx", "the console root layout throwing"]
  ])("keeps %s, which catches %s", (rel) => {
    expect(existsSync(resolve(__dirname, "..", "..", rel))).toBe(true);
  });

  it("gives every boundary a reset", () => {
    // A boundary with no action is a nicer-looking dead end.
    for (const rel of ["web/app/error.tsx", "web/app/global-error.tsx", "admin/app/error.tsx"]) {
      const src = readFileSync(resolve(__dirname, "..", "..", rel), "utf8");
      expect(src, rel).toMatch(/reset/);
    }
  });
});
