"use client";

import { useState } from "react";
import { Badge, Button, DataTable, EmptyState, Field, Input, Tabs, useToast, type Column } from "@kurx/ui";
import { ArrowDown, ArrowUp, Layers, Plus, Search, UploadCloud } from "lucide-react";
import type { AdminCategory } from "@/lib/api";
import { reorderCategoriesAction } from "@/lib/admin-actions";
import { TaxonomyDetailSheet } from "./taxonomy-detail-sheet";
import { TaxonomyImportExport } from "./taxonomy-import-export";

const LEVEL_TABS = [
  { id: "audience", label: "Audiences" },
  { id: "category", label: "Categories" },
  { id: "type", label: "Event Types" }
] as const;
type LevelTab = (typeof LEVEL_TABS)[number]["id"];

const STATUS_TONE: Record<string, "success" | "muted" | "danger"> = {
  active: "success",
  disabled: "muted",
  archived: "danger"
};

export function TaxonomyWorkspace({ categories }: { categories: AdminCategory[] }) {
  const [tab, setTab] = useState<string>("audience");
  const [q, setQ] = useState("");
  const [openId, setOpenId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const toast = useToast();

  const byId = new Map(categories.map((c) => [c.id, c]));
  const nameOf = (id: string | null) => (id ? (byId.get(id)?.name ?? "—") : "—");

  const term = q.trim().toLowerCase();
  const rows = categories
    .filter((c) => c.level === tab)
    .filter((c) => !term || c.name.toLowerCase().includes(term) || c.slug.toLowerCase().includes(term))
    .sort((a, b) => a.sort - b.sort || a.name.localeCompare(b.name));

  async function move(row: AdminCategory, dir: -1 | 1) {
    const siblings = categories
      .filter((c) => c.level === row.level && c.parent_id === row.parent_id)
      .sort((a, b) => a.sort - b.sort);
    const idx = siblings.findIndex((s) => s.id === row.id);
    const swapWith = siblings[idx + dir];
    if (!swapWith) return;
    const order = [
      { id: row.id, sort: swapWith.sort },
      { id: swapWith.id, sort: row.sort }
    ];
    const res = await reorderCategoriesAction(row.parent_id, order);
    if (res && "error" in res) toast(String(res.error), "error");
  }

  const columns: Column<AdminCategory>[] = [
    {
      key: "name",
      header: "Name",
      render: (c) => (
        <div className="flex min-w-0 items-center gap-2">
          {c.badge ? <Badge tone="accent">{c.badge}</Badge> : null}
          <span className="truncate font-medium text-text">{c.name}</span>
        </div>
      )
    },
    ...(tab !== "audience"
      ? [
          {
            key: "parent",
            header: tab === "category" ? "Audience" : "Category",
            render: (c: AdminCategory) => <span className="text-xs text-muted">{nameOf(c.parent_id)}</span>
          } as Column<AdminCategory>
        ]
      : []),
    { key: "status", header: "Status", render: (c) => <Badge tone={STATUS_TONE[c.status] ?? "muted"}>{c.status}</Badge> },
    {
      key: "visible",
      header: "Visible",
      render: (c) => (c.is_visible ? <Badge tone="success">visible</Badge> : <Badge tone="muted">hidden</Badge>)
    },
    { key: "usage", header: "Usage", align: "right", render: (c) => c.usage_count },
    {
      key: "updated",
      header: "Updated",
      render: (c) => <span className="text-xs text-muted">{new Date(c.updated_at).toLocaleDateString("en-IN")}</span>
    },
    {
      key: "order",
      header: "Order",
      render: (c) => (
        <div className="flex gap-1" onClick={(e) => e.stopPropagation()}>
          <button type="button" onClick={() => move(c, -1)} className="rounded p-1 text-muted hover:bg-elevated hover:text-text" aria-label="Move up">
            <ArrowUp size={14} />
          </button>
          <button type="button" onClick={() => move(c, 1)} className="rounded p-1 text-muted hover:bg-elevated hover:text-text" aria-label="Move down">
            <ArrowDown size={14} />
          </button>
        </div>
      )
    }
  ];

  const activeLabel = LEVEL_TABS.find((t) => t.id === tab)?.label.replace(/s$/, "") ?? "Item";

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border">
        <Tabs tabs={LEVEL_TABS as unknown as { id: string; label: string }[]} value={tab} onChange={setTab} />
        <div className="mb-2 flex gap-2">
          <Button variant="ghost" onClick={() => setImportOpen(true)}>
            <UploadCloud size={14} /> Export / Import
          </Button>
          <Button variant="secondary" onClick={() => setCreating(true)}>
            <Plus size={14} /> New {activeLabel}
          </Button>
        </div>
      </div>

      <Field label="Search" htmlFor="tx-q">
        <div className="relative w-64">
          <Search size={15} className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-muted" />
          <Input id="tx-q" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Name or slug" className="pl-8" />
        </div>
      </Field>

      <DataTable
        columns={columns}
        data={rows}
        keyField={(c) => c.id}
        onRowClick={(c) => setOpenId(c.id)}
        caption="Taxonomy"
        emptyState={<EmptyState icon={<Layers size={22} />} title="Nothing here yet" message={`Create the first ${activeLabel.toLowerCase()}.`} />}
      />

      {openId && byId.get(openId) ? (
        <TaxonomyDetailSheet category={byId.get(openId)!} allCategories={categories} onClose={() => setOpenId(null)} />
      ) : null}
      {creating ? <TaxonomyDetailSheet level={tab} allCategories={categories} onClose={() => setCreating(false)} /> : null}
      {importOpen ? <TaxonomyImportExport onClose={() => setImportOpen(false)} /> : null}
    </div>
  );
}
