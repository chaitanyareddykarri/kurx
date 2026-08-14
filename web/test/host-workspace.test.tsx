import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ usePathname: () => "/host/events/e1/tickets" }));

import { EventWorkspaceTabs } from "@/components/host/event-workspace-tabs";
import { WorkflowPage } from "@/components/host/workflow-page";

/**
 * Guards for Phase 21 — the host workspace.
 *
 * The tab strip is the navigation for seventeen routes, so what it fails to communicate it fails to
 * communicate seventeen times.
 */

const TABS = [
  { key: "overview", label: "Overview", href: "/host/events/e1" },
  { key: "tickets", label: "Tickets", href: "/host/events/e1/tickets" },
  { key: "team", label: "Team", href: "/host/events/e1/team" },
];

describe("EventWorkspaceTabs — the current destination must be programmatic", () => {
  it("marks the active route with aria-current", () => {
    render(<EventWorkspaceTabs tabs={TABS} />);
    // usePathname is mocked to the tickets tab. It was marked by a hue and a bottom border alone.
    expect(screen.getByRole("link", { name: "Tickets" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Overview" })).not.toHaveAttribute("aria-current");
  });

  it("names the navigation region", () => {
    render(<EventWorkspaceTabs tabs={TABS} />);
    // One of several landmarks on the page; an unnamed one is "navigation" in a list of them.
    expect(screen.getByRole("navigation", { name: "Event workspace" })).toBeInTheDocument();
  });

  it("keeps every destination a real link, not a tab", () => {
    render(<EventWorkspaceTabs tabs={TABS} />);
    // Each is its own route and must stay shareable and openable in a new tab; `role="tab"` would
    // also promise arrow-key navigation between panels this page does not have.
    expect(screen.queryAllByRole("tab")).toHaveLength(0);
    expect(screen.getAllByRole("link")).toHaveLength(3);
  });
});

describe("WorkflowPage — an unbuilt area must not read as a built one", () => {
  it("says the area is unavailable", () => {
    render(<WorkflowPage title="Risk Flags" items={["Duplicate registrations"]} />);
    expect(screen.getByText(/isn't available yet/i)).toBeInTheDocument();
  });

  it("carries no implementation note", () => {
    render(<WorkflowPage title="Risk Flags" items={["Duplicate registrations"]} />);
    // Each planned feature used to be a card captioned "Connected to backend APIs as they are
    // exposed; frontend keeps validation and UI state only" — a note between engineers, shipped.
    expect(screen.queryByText(/Connected to backend APIs/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/frontend keeps validation/i)).not.toBeInTheDocument();
  });

  it("labels the planned scope as planned", () => {
    render(<WorkflowPage title="Risk Flags" items={["Duplicate registrations"]} />);
    expect(screen.getByText("Planned to cover")).toBeInTheDocument();
    expect(screen.getByText("Duplicate registrations")).toBeInTheDocument();
  });

  it("offers a way back", () => {
    render(<WorkflowPage title="Risk Flags" items={[]} />);
    expect(screen.getByRole("link", { name: /Back to your workspace/ })).toHaveAttribute("href", "/workspace");
  });
});

/**
 * The host surface is 25 routes and ~2,600 lines of components, which is too much to assert screen by
 * screen. These sweeps cover the two defect classes that were spread across all of it.
 */
describe("the host surface as a whole", () => {
  function hostFiles(): string[] {
    const { readdirSync, statSync } = require("node:fs") as typeof import("node:fs");
    const walk = (dir: string): string[] =>
      readdirSync(dir).flatMap((entry: string) => {
        const full = resolve(dir, entry);
        return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(entry) ? [full] : [];
      });
    return [
      ...walk(resolve(__dirname, "..", "app/(app)/host")),
      ...walk(resolve(__dirname, "..", "components/host")),
    ];
  }

  it("carries no text on the ember fill token", () => {
    // `accent` is documented in tokens.css as a FILL and is 2.80:1 as text on light. 29 sites here
    // used it for text, every one of them below AA.
    const pattern = /text-accent(?!-text)(?![\w-])/;
    const offenders = hostFiles().filter((file) => {
      return readFileSync(file, "utf8")
        .split("\n")
        .filter((line) => !line.trim().startsWith("//") && !line.trim().startsWith("*"))
        .some((line) => pattern.test(line));
    });
    expect(offenders.map((f) => f.split("/web/")[1])).toEqual([]);
  });

  it("imports the design system directly, not through a shim", () => {
    // 55 imports came through `@/components/ui/{card,button}`, which are re-export shims (D-185).
    const offenders = hostFiles().filter((file) =>
      /@\/components\/ui\/(card|button)/.test(readFileSync(file, "utf8"))
    );
    expect(offenders.map((f) => f.split("/web/")[1])).toEqual([]);
  });
});

describe("destructive host actions must be gated", () => {
  function hostSources(): { path: string; source: string }[] {
    const { readdirSync, statSync } = require("node:fs") as typeof import("node:fs");
    const walk = (dir: string): string[] =>
      readdirSync(dir).flatMap((entry: string) => {
        const full = resolve(dir, entry);
        return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(entry) ? [full] : [];
      });
    return [
      ...walk(resolve(__dirname, "..", "app/(app)/host")),
      ...walk(resolve(__dirname, "..", "components/host")),
    ].map((path) => ({ path: path.split("/web/")[1], source: readFileSync(path, "utf8") }));
  }

  it("routes every destructive server-action form through a confirmation", () => {
    /*
     * Thirteen deletes fired on a single click across the host surface — a ticket type with sales
     * against it, a whole draft event, a certificate, an invite link already circulating. Each was a
     * bare `<form action={deleteXAction}>` with `<Button type="submit">` inside it.
     *
     * The shape to catch: a form bound to a destructive action whose submit is a plain Button rather
     * than `ConfirmSubmitButton`.
     */
    const destructiveForm = /<form action=\{(delete|remove|revoke|cancel)\w*Action/i;
    const offenders: string[] = [];

    for (const { path, source } of hostSources()) {
      const lines = source.split("\n");
      lines.forEach((line, i) => {
        if (!destructiveForm.test(line)) return;
        // The submit control sits within the next few lines of the form it belongs to.
        const body = lines.slice(i, i + 8).join("\n");
        if (/type="submit"/.test(body) && !/ConfirmSubmitButton/.test(body)) {
          offenders.push(`${path}:${i + 1}`);
        }
      });
    }

    expect(offenders).toEqual([]);
  });
});

describe("host form controls must carry the design system's contract", () => {
  function hostSources(): { path: string; source: string }[] {
    const { readdirSync, statSync } = require("node:fs") as typeof import("node:fs");
    const walk = (dir: string): string[] =>
      readdirSync(dir).flatMap((entry: string) => {
        const full = resolve(dir, entry);
        return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(entry) ? [full] : [];
      });
    return [
      ...walk(resolve(__dirname, "..", "app/(app)/host")),
      ...walk(resolve(__dirname, "..", "components/host")),
    ].map((path) => ({ path: path.split("/web/")[1], source: readFileSync(path, "utf8") }));
  }

  it("puts no control boundary on the decorative border token", () => {
    /*
     * `border` is 1.30:1 and documented as decorative; `border-strong` is the one that clears the
     * 3:1 WCAG 1.4.11 needs for a boundary that identifies a control. The host surface had the
     * decorative token under 160 controls, via 20 copies of the same forked class string.
     */
    const offenders: string[] = [];
    for (const { path, source } of hostSources()) {
      source.split("\n").forEach((line, i) => {
        if (!/<(input|select|textarea)\b/.test(line)) return;
        if (/border-border(?!-strong)/.test(line)) offenders.push(`${path}:${i + 1}`);
      });
    }
    expect(offenders).toEqual([]);
  });

  it("defines no local copy of the control class", () => {
    // The forked string lived in 20 constants across 17 files. `controlClass` is the one source.
    const offenders = hostSources()
      .filter(({ source }) => /h-10 w-full rounded-md border border-border\b/.test(source))
      .map(({ path }) => path);
    expect(offenders).toEqual([]);
  });

  it("overrides no button down through the 44px floor", () => {
    const offenders: string[] = [];
    for (const { path, source } of hostSources()) {
      source.split("\n").forEach((line, i) => {
        if (!/<(Button|LinkButton)\b/.test(line)) return;
        if (/className="[^"]*\bh-\d+/.test(line)) offenders.push(`${path}:${i + 1}`);
      });
    }
    expect(offenders).toEqual([]);
  });
});
