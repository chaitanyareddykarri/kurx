"use client";

import { Badge, DataTable, type Column } from "@kurx/ui";
import type { AuditEntry } from "@/lib/api";

// D-200: columns (with their render closures) must live in a Client Component — a Server Component
// cannot pass functions to DataTable (a Client Component). Extracted verbatim from the former inline
// definition in app/(console)/audit/page.tsx, no behavior change.
const columns: Column<AuditEntry>[] = [
  { key: "created_at", header: "When", width: "11rem", render: (r) => <span className="whitespace-nowrap text-xs text-muted">{new Date(r.created_at).toLocaleString("en-IN")}</span> },
  {
    key: "actor_type",
    header: "Actor",
    render: (r) => (
      <div className="text-xs">
        <p className="text-text">{r.actor_type}</p>
        {r.actor_id ? <p className="font-mono text-[10px] text-muted">{r.actor_id.slice(0, 8)}</p> : null}
      </div>
    )
  },
  { key: "action", header: "Action", render: (r) => <Badge tone="neutral">{r.action}</Badge> },
  {
    key: "entity",
    header: "Entity",
    render: (r) => (
      <div className="text-xs text-muted">
        <p>{r.entity}</p>
        {r.entity_id ? <p className="font-mono text-[10px]">{r.entity_id.slice(0, 8)}</p> : null}
      </div>
    )
  },
  { key: "details", header: "Details", render: (r) => <span className="line-clamp-2 max-w-md break-all font-mono text-[11px] text-muted">{r.details ?? "—"}</span> }
];

export function AuditLogTable({ rows, emptyState }: { rows: AuditEntry[]; emptyState: React.ReactNode }) {
  return (
    <DataTable
      columns={columns}
      data={rows}
      keyField={(r) => r.id}
      caption="Audit log"
      emptyState={emptyState}
    />
  );
}
