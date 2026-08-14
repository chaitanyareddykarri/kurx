"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Badge, DataTable, EmptyState, type Column, type SortState } from "@kurx/ui";
import { Calendar, ShieldCheck, Star } from "lucide-react";
import type { AdminEvent } from "@/lib/api";
import { money } from "@/lib/format";

const STATUS_TONE: Record<string, "success" | "accent" | "muted" | "danger" | "neutral"> = {
  published: "success", live: "success", scheduled: "accent", approved: "success",
  pendingreview: "accent", underreview: "accent", changesrequested: "accent", rejected: "danger",
  draft: "muted", closed: "muted", completed: "muted", archived: "muted", cancelled: "danger"
};


export function EventsTable({
  events,
  reportCounts,
  fraudByEvent,
  emptyMessage,
  selectedIds,
  onSelectionChange
}: {
  events: AdminEvent[];
  reportCounts: Record<string, number>;
  fraudByEvent: Record<string, number>;
  emptyMessage: string;
  selectedIds?: Set<string>;
  onSelectionChange?: (ids: Set<string>) => void;
}) {
  const router = useRouter();
  const searchParams = useSearchParams();

  function openEvent(eventId: string) {
    const next = new URLSearchParams(searchParams.toString());
    next.set("event", eventId);
    router.push(`?${next.toString()}`, { scroll: false });
  }

  // The backend's `sort` is a fixed-direction named selector (D-186/D-187: "revenue" always desc, "date"
  // always asc, …), not a per-column toggle — the same single state the dropdown in EventsFilterBar
  // already drives. The Dates column header is a second entry point into that one state, not a separate
  // asc/desc capability the backend doesn't have.
  const currentSort = searchParams.get("sort") ?? "";
  const sort: SortState | undefined = currentSort === "date" ? { key: "starts_at", dir: "asc" } : undefined;
  function onSortChange(next: SortState) {
    const params = new URLSearchParams(searchParams.toString());
    if (next.key === "starts_at") params.set("sort", "date");
    params.delete("page");
    router.push(`?${params.toString()}`, { scroll: false });
  }

  const columns: Column<AdminEvent>[] = [
    {
      key: "title",
      header: "Event",
      render: (e) => (
        <div className="flex items-center gap-2.5 min-w-0">
          <span className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border bg-elevated text-muted">
            <Calendar size={15} />
          </span>
          <div className="min-w-0">
            <p className="flex items-center gap-1.5 truncate font-medium text-text">
              {e.is_featured ? <Star size={12} className="shrink-0 text-accent-text" fill="currentColor" /> : null}
              <span className="truncate">{e.title}</span>
            </p>
            <p className="truncate font-mono text-[10px] text-muted">{e.event_id}</p>
          </div>
        </div>
      )
    },
    {
      key: "category",
      header: "Category",
      render: (e) => (
        <div className="text-xs">
          <p className="text-text">{e.category ?? "—"}</p>
          {e.subcategory ? <p className="text-muted">{e.subcategory}</p> : null}
        </div>
      )
    },
    {
      key: "visibility",
      header: "Access",
      render: (e) => (
        <div className="flex flex-wrap gap-1">
          <Badge tone={e.visibility === "listed" ? "neutral" : "muted"}>{e.visibility}</Badge>
          <Badge tone={e.is_paid ? "accent" : "muted"}>{e.is_paid ? "paid" : "free"}</Badge>
        </div>
      )
    },
    {
      key: "status",
      header: "Status",
      sortable: false,
      render: (e) => (
        <div className="flex flex-wrap gap-1">
          <Badge tone={STATUS_TONE[e.status] ?? "neutral"}>{e.status}</Badge>
          {e.is_suspended ? <Badge tone="danger">suspended</Badge> : null}
          {e.is_hidden ? <Badge tone="muted">hidden</Badge> : null}
        </div>
      )
    },
    {
      key: "org_name",
      header: "Organization",
      render: (e) => (
        <div className="flex items-center gap-1.5 text-xs">
          <span className="truncate text-text">{e.org_name}</span>
          {e.org_verification === "verified" ? <ShieldCheck size={12} className="shrink-0 text-success" /> : null}
        </div>
      )
    },
    {
      key: "city",
      header: "Location",
      render: (e) => (
        <div className="text-xs">
          <p className="text-text">{e.city || "—"}</p>
          <p className="truncate text-muted">{e.venue_name || "—"}</p>
        </div>
      )
    },
    {
      key: "starts_at",
      header: "Dates",
      sortable: true,
      render: (e) => (
        <div className="whitespace-nowrap text-xs text-muted">
          <p>{new Date(e.starts_at).toLocaleDateString("en-IN")}</p>
          <p>→ {new Date(e.ends_at).toLocaleDateString("en-IN")}</p>
        </div>
      )
    },
    {
      key: "registrations_count",
      header: "Registrations",
      align: "right",
      render: (e) => (
        <div className="whitespace-nowrap text-xs">
          <p className="text-text">{e.registrations_count}{e.capacity ? ` / ${e.capacity}` : ""}</p>
          <p className="text-muted">{e.checked_in} checked in</p>
        </div>
      )
    },
    {
      key: "revenue_paise",
      header: "Revenue",
      align: "right",
      render: (e) => <span className="whitespace-nowrap text-text">{money(e.revenue_paise, e.currency)}</span>
    },
    {
      key: "alerts",
      header: "Alerts",
      render: (e) => {
        const reports = reportCounts[e.event_id] ?? 0;
        const fraud = fraudByEvent[e.event_id] ?? 0;
        if (!reports && !fraud) return <span className="text-xs text-muted">—</span>;
        return (
          <div className="flex flex-wrap gap-1">
            {reports ? <Badge tone="danger">{reports} report{reports === 1 ? "" : "s"}</Badge> : null}
            {fraud ? <Badge tone="danger">risk {fraud}</Badge> : null}
          </div>
        );
      }
    },
    {
      key: "updated_at",
      header: "Updated",
      render: (e) => <span className="whitespace-nowrap text-xs text-muted">{new Date(e.updated_at).toLocaleDateString("en-IN")}</span>
    }
  ];

  return (
    <DataTable
      columns={columns}
      data={events}
      keyField={(e) => e.event_id}
      onRowClick={(e) => openEvent(e.event_id)}
      caption="Events"
      sort={sort}
      onSortChange={onSortChange}
      selectable={!!onSelectionChange}
      selectedIds={selectedIds}
      onSelectionChange={onSelectionChange}
      emptyState={<EmptyState icon={<Calendar size={22} />} title="No events match" message={emptyMessage} />}
    />
  );
}
