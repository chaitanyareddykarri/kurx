"use client";

import { useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Button, Card, Dialog, Field, Textarea, useToast } from "@kurx/ui";
import { Archive, CheckCircle2, Download, Eye, EyeOff, ShieldAlert, ShieldCheck, XCircle } from "lucide-react";
import { TABS, type TabId } from "@/lib/event-tabs";
import { bulkManageEventsAction, exportAdminEventsCsvAction } from "@/lib/admin-actions";
import { EventsFilterBar } from "./events-filter-bar";
import { EventsTable } from "./events-table";
import { EventWorkspaceSheet, type WorkspaceData } from "./event-workspace-sheet";
import type { AdminEvent, AdminEventListParams } from "@/lib/api";

type BulkAction = "suspend" | "unsuspend" | "hide" | "unhide" | "approve" | "reject" | "archive";

const BULK_ACTIONS: { action: BulkAction; label: string; icon: typeof ShieldAlert; tone: "ok" | "warn" | "neutral" }[] = [
  { action: "approve", label: "Approve", icon: CheckCircle2, tone: "ok" },
  { action: "reject", label: "Reject", icon: XCircle, tone: "warn" },
  { action: "suspend", label: "Suspend", icon: ShieldAlert, tone: "warn" },
  { action: "unsuspend", label: "Unsuspend", icon: ShieldCheck, tone: "ok" },
  { action: "hide", label: "Hide", icon: EyeOff, tone: "warn" },
  { action: "unhide", label: "Unhide", icon: Eye, tone: "ok" },
  { action: "archive", label: "Archive", icon: Archive, tone: "neutral" }
];

export function EventsWorkspace({
  tab,
  events,
  total,
  page,
  totalPages,
  categories,
  reportCounts,
  fraudByEvent,
  searchParams,
  openEvent,
  workspace,
  workspaceError,
  isSuperAdmin
}: {
  tab: TabId;
  events: AdminEvent[];
  total: number;
  page: number;
  totalPages: number;
  categories: { id: string; name: string }[];
  reportCounts: Record<string, number>;
  fraudByEvent: Record<string, number>;
  searchParams: Record<string, string | undefined>;
  openEvent: AdminEvent | null;
  workspace: WorkspaceData | null;
  workspaceError: string | null;
  isSuperAdmin: boolean;
}) {
  const params = useSearchParams();
  const toast = useToast();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [pendingAction, setPendingAction] = useState<BulkAction | null>(null);
  const [exporting, setExporting] = useState(false);

  function tabHref(id: TabId) {
    const next = new URLSearchParams(params.toString());
    next.set("tab", id);
    next.delete("event");
    next.delete("page");
    return `?${next.toString()}`;
  }

  function closeHref() {
    const next = new URLSearchParams(params.toString());
    next.delete("event");
    return `?${next.toString()}`;
  }

  function pageHref(nextPage: number) {
    const next = new URLSearchParams(params.toString());
    if (nextPage > 1) next.set("page", String(nextPage)); else next.delete("page");
    return `?${next.toString()}`;
  }

  async function runBulkAction(action: BulkAction, reason: string) {
    const res = await bulkManageEventsAction(Array.from(selected), action, reason.trim() || undefined);
    if (!res.result) { toast(res.error ?? "Something went wrong.", "error"); return; }
    const { succeeded, failed } = res.result;
    if (failed.length === 0) toast(`${succeeded.length} event${succeeded.length === 1 ? "" : "s"} updated.`, "success");
    else toast(`${succeeded.length} succeeded, ${failed.length} failed.`, failed.length === succeeded.length ? "error" : "success");
    setSelected(new Set());
    setPendingAction(null);
  }

  // D-191: fetched server-side (real auth), then a client-side Blob download — the access token never
  // touches a URL.
  async function runExport(selectedOnly: boolean) {
    setExporting(true);
    try {
      const exportParams: AdminEventListParams & { eventIds?: string } = {
        q: searchParams.q, status: TABS.find((t) => t.id === tab)?.id === "all" ? undefined : tab,
        categoryId: searchParams.category, city: searchParams.city, visibility: searchParams.visibility,
        sort: searchParams.sort,
        eventIds: selectedOnly ? Array.from(selected).join(",") : undefined
      };
      const res = await exportAdminEventsCsvAction(exportParams);
      if (!res.csv) { toast(res.error ?? "Something went wrong.", "error"); return; }
      const blob = new Blob([res.csv], { type: "text/csv" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `admin-events-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } finally {
      setExporting(false);
    }
  }

  return (
    <div className="space-y-4">
      <nav aria-label="Event status" className="flex gap-1 overflow-x-auto border-b border-border">
        {TABS.map((t) => {
          const active = t.id === tab;
          return (
            <Link
              key={t.id}
              href={tabHref(t.id)}
              scroll={false}
              aria-current={active ? "page" : undefined}
              className={`-mb-px shrink-0 border-b-2 px-4 py-2.5 text-sm font-semibold transition ${
                active ? "border-accent text-text" : "border-transparent text-muted hover:text-text"
              }`}
            >
              {t.label}
            </Link>
          );
        })}
      </nav>

      <EventsFilterBar tab={tab} categories={categories} values={searchParams} />

      <div className="flex items-center justify-between">
        <p className="text-xs text-muted">{total} event{total === 1 ? "" : "s"}</p>
    <Button variant="ghost" className="gap-1.5" disabled={exporting} onClick={() => runExport(false)}>
          <Download size={13} /> {exporting ? "Exporting…" : "Export CSV"}
        </Button>
      </div>

      {selected.size > 0 && (
        <div className="flex flex-wrap items-center gap-1.5 rounded-lg border border-accent/40 bg-accent/5 p-2.5">
          <span className="px-1.5 text-xs font-semibold text-text">{selected.size} selected</span>
          {BULK_ACTIONS.map(({ action, label, icon: Icon, tone }) => (
            <Button
              key={action}
              variant="ghost"
              className={`h-8 gap-1.5 px-2.5 text-xs ${tone === "warn" ? "text-danger hover:bg-danger/10" : tone === "ok" ? "text-success hover:bg-success/10" : ""}`}
              onClick={() => setPendingAction(action)}
            >
              <Icon size={13} /> {label}
            </Button>
          ))}
     <Button variant="ghost" className="gap-1.5" disabled={exporting} onClick={() => runExport(true)}>
            <Download size={13} /> Export selected
          </Button>
     <Button variant="ghost" className="text-muted" onClick={() => setSelected(new Set())}>
            Clear
          </Button>
        </div>
      )}

      <EventsTable
        events={events}
        reportCounts={reportCounts}
        fraudByEvent={fraudByEvent}
        selectedIds={selected}
        onSelectionChange={setSelected}
        emptyMessage={
          tab === "all"
            ? "No events match these filters."
            : `No events are currently ${TABS.find((t) => t.id === tab)?.label.toLowerCase()}.`
        }
      />

      {totalPages > 1 && (
        <nav className="flex items-center justify-between border-t border-border pt-3" aria-label="Pagination">
          {page > 1 ? (
            <Link href={pageHref(page - 1)} scroll={false} className="text-sm text-accent-text hover:underline">Previous</Link>
          ) : (
            <span className="text-sm text-muted opacity-50">Previous</span>
          )}
          <span className="text-xs text-muted">Page {page} of {totalPages}</span>
          {page < totalPages ? (
            <Link href={pageHref(page + 1)} scroll={false} className="text-sm text-accent-text hover:underline">Next</Link>
          ) : (
            <span className="text-sm text-muted opacity-50">Next</span>
          )}
        </nav>
      )}

      {openEvent ? (
        workspaceError ? (
          <Card className="border-danger/30 bg-danger/5">
            <p className="text-sm text-danger">Couldn&apos;t load the event workspace: {workspaceError}</p>
          </Card>
        ) : workspace ? (
          <EventWorkspaceSheet event={openEvent} data={workspace} closeHref={closeHref()} isSuperAdmin={isSuperAdmin} />
        ) : null
      ) : null}

      {pendingAction ? (
        <BulkReasonDialog action={pendingAction} count={selected.size} onClose={() => setPendingAction(null)} onConfirm={runBulkAction} />
      ) : null}
    </div>
  );
}

// D-193: one dialog for every bulk action — `reason` is required only for suspend/hide (matching
// BulkEventActionBodyValidator server-side); approve/unsuspend/unhide/reject/archive have no reason
// field in this data model at all, same as their single-event counterparts.
function BulkReasonDialog({
  action,
  count,
  onClose,
  onConfirm
}: {
  action: BulkAction;
  count: number;
  onClose: () => void;
  onConfirm: (action: BulkAction, reason: string) => Promise<void>;
}) {
  const [reason, setReason] = useState("");
  const [pending, setPending] = useState(false);
  const label = BULK_ACTIONS.find((a) => a.action === action)?.label ?? action;
  const reasonRequired = action === "suspend" || action === "hide";

  async function confirm() {
    setPending(true);
    try {
      await onConfirm(action, reason);
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onClose={onClose} title={`${label} ${count} event${count === 1 ? "" : "s"}?`}>
      <div className="space-y-3">
        <p className="text-sm text-muted">
          This applies to every selected event. Events that can&apos;t {label.toLowerCase()} (wrong status, already
          in that state) are reported back individually — the rest still succeed.
        </p>
        <Field
          label="Reason"
          htmlFor="bulk-reason"
          helper={reasonRequired ? "Required — recorded in the audit trail" : "Optional, recorded in the audit trail"}
        >
          <Textarea id="bulk-reason" value={reason} onChange={(e) => setReason(e.target.value)} rows={3} required={reasonRequired} />
        </Field>
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose} disabled={pending}>Cancel</Button>
          <Button
            type="button"
            onClick={confirm}
            disabled={pending || (reasonRequired && !reason.trim())}
          >
            {pending ? "Working…" : label}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
