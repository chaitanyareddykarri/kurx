"use client";

import { Badge, DataTable, type Column } from "@kurx/ui";
import type { AuditEntry } from "@/lib/api";

// D-200: same RSC/Client boundary fix as audit-log-table.tsx. No Actor column here — on a single
// user's detail page the actor is always this account, so it would be redundant. Extracted verbatim
// from the former inline `auditColumns` in app/(console)/users/[id]/page.tsx, no behavior change.
const columns: Column<AuditEntry>[] = [
  { key: "created_at", header: "When", width: "11rem", render: (a) => <span className="whitespace-nowrap text-xs text-muted">{new Date(a.created_at).toLocaleString("en-IN")}</span> },
  { key: "action", header: "Action", render: (a) => <Badge tone="neutral">{a.action}</Badge> },
  {
    key: "entity",
    header: "Entity",
    render: (a) => (
      <div className="text-muted">
        {a.entity}
        {a.entity_id ? <span className="block font-mono text-xs">{a.entity_id}</span> : null}
      </div>
    )
  },
  { key: "details", header: "Details", render: (a) => <span className="line-clamp-2 max-w-md break-all text-xs text-muted">{a.details ?? "—"}</span> }
];

export function UserAuditTable({ rows, caption }: { rows: AuditEntry[]; caption: string }) {
  return <DataTable columns={columns} data={rows} keyField={(a) => a.id} caption={caption} />;
}
