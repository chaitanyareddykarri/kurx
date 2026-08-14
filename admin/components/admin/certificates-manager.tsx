"use client";

import { useMemo, useState, useTransition } from "react";
import { Award, ShieldOff } from "lucide-react";
import {
  Badge, Button, Card, ConfirmDialog, DataTable, Dialog, EmptyState, Field, Input, Pagination, SearchBar,
  type Column, type SortState
} from "@kurx/ui";
import { generateCertificatesAction, revokeCertificateAction } from "@/lib/admin-actions";
import type { Certificate } from "@/lib/api";

const PAGE_SIZE = 20;

/** Both the locale and the time zone are pinned. `toLocaleDateString("en-IN")` with neither is resolved by
 *  the runtime, and the SSR runtime is not the browser: the container is UTC while an operator's
 *  machine is not, and Node and Chrome can pick different default locales. That produced a real
 *  React hydration mismatch ("Text content does not match server-rendered HTML"), so the format has
 *  to be identical on both sides. UTC is stated in the column header rather than silently applied. */
function formatUtcDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-GB", { timeZone: "UTC" });
}

export function CertificatesManager({
  eventId,
  eventTitle,
  certificates
}: {
  eventId: string;
  eventTitle: string;
  certificates: Certificate[];
}) {
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<SortState>({ key: "issued_at", dir: "desc" });
  const [page, setPage] = useState(1);
  const [generating, setGenerating] = useState(false);
  const [revoking, setRevoking] = useState<Certificate | null>(null);
  const [revokeError, setRevokeError] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [notice, setNotice] = useState<{ tone: "ok" | "error"; text: string } | null>(null);
  const [pending, startAction] = useTransition();

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const rows = certificates.filter(
      (c) =>
        !q ||
        (c.holder_name ?? "").toLowerCase().includes(q) ||
        c.verify_code.toLowerCase().includes(q)
    );
    const dir = sort.dir === "asc" ? 1 : -1;
    return [...rows].sort((a, b) => {
      if (sort.key === "issued_at") return (Date.parse(a.issued_at) - Date.parse(b.issued_at)) * dir;
      const key = sort.key as "verify_code" | "kind" | "status";
      return (a[key] ?? "").localeCompare(b[key] ?? "") * dir;
    });
  }, [certificates, query, sort]);

  const pageRows = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const revokedCount = certificates.filter((c) => c.is_revoked).length;

  function runGenerate() {
    setNotice(null);
    startAction(async () => {
      const res = await generateCertificatesAction(eventId);
      if ("error" in res) setNotice({ tone: "error", text: String(res.error) });
      else {
        const n = res.generated;
        setNotice({
          tone: "ok",
          text: n === 0 ? "No eligible tickets — nothing generated." : `Generated ${n} certificate${n === 1 ? "" : "s"}.`
        });
      }
    });
  }

  // The revoke dialog is closed by this handler, not by the primitive, so its error stays visible
  // inside the open modal instead of being hidden behind it.
  function runRevoke() {
    if (!revoking) return;
    setRevokeError(null);
    setNotice(null);
    startAction(async () => {
      const res = await revokeCertificateAction(revoking.id, reason);
      if (res && "error" in res) setRevokeError(String(res.error));
      else {
        setNotice({ tone: "ok", text: "Certificate revoked." });
        setRevoking(null);
        setReason("");
      }
    });
  }

  const columns: Column<Certificate>[] = [
    {
      key: "holder_name",
      header: "Holder",
      render: (c) => <span className="text-text">{c.holder_name ?? "—"}</span>
    },
    {
      key: "verify_code",
      header: "Verify code",
      sortable: true,
      render: (c) => <span className="font-mono text-xs text-muted">{c.verify_code}</span>
    },
    { key: "kind", header: "Kind", sortable: true, render: (c) => <Badge tone="neutral">{c.kind}</Badge> },
    {
      key: "status",
      header: "Status",
      sortable: true,
      render: (c) =>
        c.is_revoked ? (
          <span className="flex flex-col gap-0.5">
            <Badge tone="danger">revoked</Badge>
            {c.revoked_reason ? <span className="text-xs text-muted">{c.revoked_reason}</span> : null}
          </span>
        ) : (
          <Badge tone="success">{c.status}</Badge>
        )
    },
    {
      key: "issued_at",
      header: "Issued (UTC)",
      sortable: true,
      render: (c) => <span className="text-xs text-muted">{formatUtcDate(c.issued_at)}</span>
    },
    {
      key: "actions",
      header: "",
      srHeader: "Row actions",
      align: "right",
      width: "8rem",
      render: (c) =>
        c.is_revoked ? null : (
          <Button
            variant="ghost"
            className="h-8 px-2 text-xs text-danger hover:bg-danger/10"
            onClick={() => {
              setNotice(null);
              setRevokeError(null);
              setReason("");
              setRevoking(c);
            }}
          >
            <ShieldOff size={14} /> Revoke
          </Button>
        )
    }
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-text">{eventTitle}</p>
          <p className="text-xs text-muted">
            {certificates.length} issued · {revokedCount} revoked
          </p>
        </div>
        <Button onClick={() => setGenerating(true)} disabled={pending}>
          <Award size={15} /> Generate certificates
        </Button>
      </div>

      {notice ? (
        <Card>
          <p role="status" className={`text-sm ${notice.tone === "ok" ? "text-success" : "text-danger"}`}>
            {notice.text}
          </p>
        </Card>
      ) : null}

      <SearchBar
        value={query}
        onChange={(v) => {
          setQuery(v);
          setPage(1);
        }}
        placeholder="Search holder or verify code"
        aria-label="Search certificates"
      />

      <DataTable
        columns={columns}
        data={pageRows}
        keyField={(c) => c.id}
        loading={pending}
        sort={sort}
        onSortChange={setSort}
        caption={`Certificates issued for ${eventTitle}`}
        emptyState={
          <EmptyState
            icon={<Award size={20} />}
            title={query ? "No matching certificates" : "No certificates issued"}
            message={
              query
                ? "Clear the search to see the whole roster."
                : "Generate certificates to issue them to eligible ticket holders."
            }
          />
        }
      />

      <Pagination page={page} pageSize={PAGE_SIZE} total={filtered.length} onPageChange={setPage} />

      <ConfirmDialog
        open={generating}
        onClose={() => setGenerating(false)}
        onConfirm={runGenerate}
        title="Generate certificates for this event?"
        description={`Issues a certificate for every eligible ticket on “${eventTitle}” that does not already have one. Tickets that already hold a certificate are skipped.`}
        confirmLabel="Generate"
      />

      {revoking ? (
        <Dialog open onClose={() => setRevoking(null)} title={`Revoke ${revoking.verify_code}?`}>
          <div className="space-y-3">
            <p className="text-sm text-muted">
              The certificate stays verifiable and will publicly report as revoked. A reason is required and is
              shown on the verification page.
            </p>
            <Field label="Reason" htmlFor="revoke-reason">
              <Input
                id="revoke-reason"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="e.g. issued to the wrong attendee"
              />
            </Field>
            {revokeError ? (
              <p role="alert" className="text-sm text-danger">
                {revokeError}
              </p>
            ) : null}
            <div className="flex justify-end gap-2 pt-1">
              <Button variant="ghost" onClick={() => setRevoking(null)} disabled={pending}>
                Cancel
              </Button>
              <Button
                onClick={runRevoke}
                disabled={pending || !reason.trim()}
                className="border-danger bg-danger text-white hover:bg-danger/90"
              >
                {pending ? "Revoking…" : "Revoke"}
              </Button>
            </div>
          </div>
        </Dialog>
      ) : null}
    </div>
  );
}
