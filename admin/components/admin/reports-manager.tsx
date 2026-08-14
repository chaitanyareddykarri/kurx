"use client";

import { useEffect } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { CheckCircle2, XCircle } from "lucide-react";
import { Badge, DataTable, EmptyState, useToast, type Column } from "@kurx/ui";
import { resolveReportAction } from "@/lib/admin-actions";
import type { Report } from "@/lib/api";

function ActionButton({ dismiss, label, tone }: { dismiss: boolean; label: string; tone: "resolve" | "dismiss" }) {
  const { pending } = useFormStatus();
  const cls = tone === "resolve"
    ? "border-success/40 text-success hover:bg-success/10"
    : "border-border text-muted hover:bg-elevated hover:text-text";
  return (
    <button
      type="submit"
      name="dismiss"
      value={dismiss ? "true" : "false"}
      disabled={pending}
      className={`h-8 rounded-md border px-2.5 text-xs font-semibold disabled:opacity-50 ${cls}`}
    >
      {pending ? "…" : label}
    </button>
  );
}

function ReportActions({ report }: { report: Report }) {
  const [state, action] = useFormState(resolveReportAction.bind(null, report.id), null);
  const toast = useToast();

  useEffect(() => {
    if (state && "ok" in state) toast("Report updated.", "success");
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  if (report.status !== "open") return null;
  return (
    <form action={action} className="flex justify-end gap-1.5">
      <ActionButton dismiss={false} label="Resolve" tone="resolve" />
      <ActionButton dismiss={true} label="Dismiss" tone="dismiss" />
    </form>
  );
}

export function ReportsManager({ reports }: { reports: Report[] }) {
  const columns: Column<Report>[] = [
    {
      key: "entity_type",
      header: "Entity",
      render: (r) => (
        <div>
          <p className="text-xs uppercase tracking-wide text-muted">{r.entity_type.replace(/_/g, " ")}</p>
          <p className="font-mono text-xs text-text">{r.entity_id}</p>
        </div>
      )
    },
    {
      key: "reason",
      header: "Reason",
      render: (r) => (
        <div className="max-w-sm">
          <p className="text-text">{r.reason}</p>
          {r.details ? <p className="mt-0.5 truncate text-xs text-muted" title={r.details}>{r.details}</p> : null}
        </div>
      )
    },
    {
      key: "status",
      header: "Status",
      render: (r) => (
        <Badge tone={r.status === "open" ? "danger" : "muted"} icon={r.status === "open" ? undefined : <CheckCircle2 size={11} />}>
          {r.status}
        </Badge>
      )
    },
    { key: "created_at", header: "Reported", render: (r) => <span className="text-xs text-muted">{new Date(r.created_at).toLocaleString("en-IN")}</span> },
    { key: "actions", header: "", srHeader: "Row actions", align: "right", width: "12rem", render: (r) => <ReportActions report={r} /> }
  ];

  return (
    <DataTable
      columns={columns}
      data={reports}
      keyField={(r) => r.id}
      caption="Open reports"
      emptyState={<EmptyState icon={<XCircle size={22} />} title="No open reports" message="Nothing is waiting for triage." />}
    />
  );
}

