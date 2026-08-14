import { readFileSync, readdirSync, statSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { useState } from "react";

import { DataTable, type Column } from "@kurx/ui";
import { PageHeader } from "@/components/layout/page-header";

/**
 * Guards for Phase 28 — the components 15 admin screens are built from.
 *
 * A defect in `DataTable` is a defect on every one of those screens at once, which is why this is
 * where admin's coverage starts rather than at any individual page.
 */

type Row = { id: string; name: string; status: string };

const ROWS: Row[] = [
  { id: "u1", name: "Asha Rao", status: "Active" },
  { id: "u2", name: "Rahul Nair", status: "Suspended" },
];

const COLUMNS: Column<Row>[] = [
  { key: "name", header: "Name", sortable: true },
  { key: "status", header: "Status" },
];

describe("a clickable row must be operable from the keyboard", () => {
  it("opens the row on Enter", async () => {
    /*
     * `onRowClick` is how three admin screens open a record, and the `<tr>` carried no `tabIndex`,
     * no key handler and no role — so the primary action on those tables was mouse-only.
     */
    const onRowClick = vi.fn();
    render(
      <DataTable
        columns={COLUMNS}
        data={ROWS}
        keyField={(r) => r.id}
        onRowClick={onRowClick}
        rowLabel={(r) => `Open ${r.name}`}
        caption="Users"
      />
    );

    const row = screen.getByRole("button", { name: "Open Asha Rao" });
    row.focus();
    await userEvent.keyboard("{Enter}");
    expect(onRowClick).toHaveBeenCalledWith(ROWS[0]);
  });

  it("opens the row on Space", async () => {
    const onRowClick = vi.fn();
    render(
      <DataTable columns={COLUMNS} data={ROWS} keyField={(r) => r.id} onRowClick={onRowClick}
        rowLabel={(r) => `Open ${r.name}`} caption="Users" />
    );
    screen.getByRole("button", { name: "Open Rahul Nair" }).focus();
    await userEvent.keyboard(" ");
    expect(onRowClick).toHaveBeenCalledWith(ROWS[1]);
  });

  it("leaves rows inert when there is nothing to open", () => {
    render(<DataTable columns={COLUMNS} data={ROWS} keyField={(r) => r.id} caption="Users" />);
    // A row that does nothing must not advertise itself as a control.
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });
});

describe("a scrollable region must be reachable by keyboard", () => {
  it("focuses and names the horizontal scroll container", () => {
    // WCAG 2.1.1 — `overflow-x-auto` with no tabIndex can be scrolled by pointer and nothing else,
    // which on a wide admin table means columns a keyboard user cannot reach.
    render(<DataTable columns={COLUMNS} data={ROWS} keyField={(r) => r.id} caption="All users" />);
    const region = screen.getByRole("group", { name: "All users" });
    expect(region).toHaveAttribute("tabindex", "0");
  });
});

describe("selection must say what it selected", () => {
  function Harness() {
    const [ids, setIds] = useState<Set<string>>(new Set());
    return (
      <DataTable
        columns={COLUMNS}
        data={ROWS}
        keyField={(r) => r.id}
        selectable
        selectedIds={ids}
        onSelectionChange={setIds}
        rowLabel={(r) => r.name}
        caption="Users"
      />
    );
  }

  it("names each row's checkbox after its row", () => {
    render(<Harness />);
    // Every checkbox shared one name — "Select row" — so they were indistinguishable in a list.
    expect(screen.getByRole("checkbox", { name: "Select Asha Rao" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Select Rahul Nair" })).toBeInTheDocument();
  });

  it("shows a partial selection as indeterminate, not as unchecked", async () => {
    render(<Harness />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Select Asha Rao" }));
    // Unchecked states "none of these are selected" while one is.
    const all = screen.getByRole("checkbox", { name: "Select all rows" }) as HTMLInputElement;
    expect(all.indeterminate).toBe(true);
    expect(all.checked).toBe(false);
  });

  it("announces how many are selected", async () => {
    render(<Harness />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Select Asha Rao" }));
    // Selection drives the bulk actions above these tables — suspend, blacklist, archive.
    expect(screen.getByRole("status")).toHaveTextContent("1 selected.");
  });
});

describe("loading is a state, not just a shape", () => {
  it("marks the table busy and says so", () => {
    render(<DataTable columns={COLUMNS} data={[]} keyField={(r) => r.id} loading caption="Users" />);
    expect(screen.getByRole("table")).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("status")).toHaveTextContent("Loading rows.");
  });
});

describe("PageHeader", () => {
  it("gives the page exactly one h1", () => {
    render(<PageHeader kicker="Trust & Safety" title="Verification queue" />);
    expect(screen.getByRole("heading", { level: 1, name: "Verification queue" })).toBeInTheDocument();
  });
});

describe("the admin surface as a whole", () => {
  function adminFiles(): string[] {
    const walk = (d: string): string[] =>
      readdirSync(d).flatMap((e) => {
        const full = resolve(d, e);
        return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(e) ? [full] : [];
      });
    return [...walk(resolve(__dirname, "..", "app")), ...walk(resolve(__dirname, "..", "components"))];
  }

  /** Comment lines, so a note *describing* a defect is not read as the defect. */
  function codeLines(source: string): string[] {
    const out: string[] = [];
    let inBlock = false;
    for (const line of source.split("\n")) {
      const t = line.trim();
      if (inBlock) { if (t.includes("*/")) inBlock = false; continue; }
      if ((t.startsWith("{/*") || t.startsWith("/*")) && !t.includes("*/")) { inBlock = true; continue; }
      if (t.startsWith("//") || t.startsWith("*") || t.startsWith("{/*") || t.startsWith("/*")) continue;
      out.push(line);
    }
    return out;
  }

  it("carries no text on the ember fill token", () => {
    // `accent` is documented in tokens.css as a FILL and measures 2.80:1 as text on light.
    // 24 sites across admin used it for text, every one below AA — including the kicker at the top
    // of all 23 pages.
    const pattern = /text-accent(?!-text)(?![\w-])/;
    const offenders = adminFiles()
      .filter((f) => codeLines(readFileSync(f, "utf8")).some((l) => pattern.test(l)))
      .map((f) => f.split("/admin/")[1]);
    expect(offenders).toEqual([]);
  });
});

describe("the admin screens as a whole", () => {
  function adminFiles(): string[] {
    const walk = (d: string): string[] =>
      readdirSync(d).flatMap((e) => {
        const full = resolve(d, e);
        return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(e) ? [full] : [];
      });
    return [...walk(resolve(__dirname, "..", "app")), ...walk(resolve(__dirname, "..", "components"))];
  }
  function code(source: string): { line: string; n: number }[] {
    const out: { line: string; n: number }[] = [];
    let inBlock = false;
    source.split("\n").forEach((line, i) => {
      const t = line.trim();
      if (inBlock) { if (t.includes("*/")) inBlock = false; return; }
      if ((t.startsWith("{/*") || t.startsWith("/*")) && !t.includes("*/")) { inBlock = true; return; }
      if (t.startsWith("//") || t.startsWith("*") || t.startsWith("{/*") || t.startsWith("/*")) return;
      out.push({ line, n: i + 1 });
    });
    return out;
  }

  it("overrides no control down through the 44px floor", () => {
    // 13 sites shrank a Button with a `h-N` class; the console is used on tablets at the gate too.
    const offenders: string[] = [];
    for (const f of adminFiles()) {
      for (const { line, n } of code(readFileSync(f, "utf8"))) {
        if (/<(Button|LinkButton)\b/.test(line) && /className="[^"]*\bh-\d+/.test(line)) {
          offenders.push(`${f.split("/admin/")[1]}:${n}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("formats every date against an explicit locale", () => {
    /*
     * `toLocaleDateString()` with no locale resolves to the server's on the server and the
     * browser's on the client — a hydration mismatch waiting for an operator whose two disagree,
     * and on an audit log a date that renders differently to two people is worse than useless.
     */
    const offenders: string[] = [];
    for (const f of adminFiles()) {
      for (const { line, n } of code(readFileSync(f, "utf8"))) {
        if (/toLocale(Date|Time)?String\(\s*\)/.test(line)) offenders.push(`${f.split("/admin/")[1]}:${n}`);
      }
    }
    expect(offenders).toEqual([]);
  });
});
