"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Badge, DataTable, EmptyState, Pagination, Sheet, type Column } from "@kurx/ui";
import { Building2, ShieldCheck } from "lucide-react";
import type { AdminOrg, AdminOrgDetail, OrgEventRow, WalletAdmin } from "@/lib/api";
import { OrgDetailContent } from "./org-detail-content";

const STATUS_TONE: Record<string, "success" | "accent" | "muted" | "danger" | "neutral"> = {
  verified: "success", pendingreview: "accent", changesrequested: "accent",
  unverified: "muted", rejected: "danger", suspended: "danger", blacklisted: "danger"
};

const PAGE_SIZE = 50;

export function OrgWorkspace({
  orgs,
  total,
  page,
  openOrg,
  detail,
  detailError,
  wallet,
  riskScore,
  events,
  canModerate
}: {
  orgs: AdminOrg[];
  total: number;
  page: number;
  openOrg: AdminOrg | null;
  detail: AdminOrgDetail | null;
  detailError: string | null;
  wallet: WalletAdmin | null;
  riskScore: number | null;
  events: OrgEventRow[];
  canModerate: boolean;
}) {
  const router = useRouter();
  const searchParams = useSearchParams();

  function openRow(orgId: string) {
    const next = new URLSearchParams(searchParams.toString());
    next.set("org", orgId);
    next.delete("page");
    router.push(`?${next.toString()}`, { scroll: false });
  }

  function closeSheet() {
    const next = new URLSearchParams(searchParams.toString());
    next.delete("org");
    router.push(`?${next.toString()}`, { scroll: false });
  }

  function goToPage(p: number) {
    const next = new URLSearchParams(searchParams.toString());
    next.set("page", String(p));
    router.push(`?${next.toString()}`, { scroll: false });
  }

  const columns: Column<AdminOrg>[] = [
    {
      key: "name",
      header: "Organization",
      render: (o) => (
        <div className="flex min-w-0 items-center gap-2.5">
          <span className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border bg-elevated text-muted">
            <Building2 size={15} />
          </span>
          <div className="min-w-0">
            <p className="flex items-center gap-1.5 truncate font-medium text-text">
              <span className="truncate">{o.name}</span>
              {o.verification_status === "verified" ? <ShieldCheck size={12} className="shrink-0 text-success" /> : null}
            </p>
            <p className="truncate font-mono text-[10px] text-muted">{o.org_id}</p>
          </div>
        </div>
      )
    },
    { key: "type", header: "Type", render: (o) => <span className="text-xs text-text">{o.is_personal ? "Self-representation" : o.type}</span> },
    {
      key: "verification_status",
      header: "Verification",
      render: (o) => <Badge tone={STATUS_TONE[o.verification_status] ?? "neutral"}>{o.verification_status}</Badge>
    },
    { key: "primary_domain", header: "Domain", render: (o) => <span className="text-xs text-muted">{o.primary_domain ?? "—"}</span> },
    { key: "member_count", header: "Members", align: "right", render: (o) => <span className="text-text">{o.member_count}</span> },
    { key: "event_count", header: "Events", align: "right", render: (o) => <span className="text-text">{o.event_count}</span> },
    {
      key: "created_at",
      header: "Created",
      render: (o) => <span className="whitespace-nowrap text-xs text-muted">{new Date(o.created_at).toLocaleDateString("en-IN")}</span>
    }
  ];

  return (
    <div className="space-y-4">
      <DataTable
        columns={columns}
        data={orgs}
        keyField={(o) => o.org_id}
        onRowClick={(o) => openRow(o.org_id)}
        caption="Organizations"
        emptyState={<EmptyState icon={<Building2 size={22} />} title="No organizations match" message="Try a different search or filter." />}
      />
      <Pagination page={page} pageSize={PAGE_SIZE} total={total} onPageChange={goToPage} />

      <Sheet open={!!openOrg} onClose={closeSheet} title={openOrg?.name ?? "Organization"} size="xl">
        {openOrg ? (
          <OrgDetailContent
            org={openOrg}
            detail={detail}
            detailError={detailError}
            wallet={wallet}
            riskScore={riskScore}
            events={events}
            canModerate={canModerate}
          />
        ) : null}
      </Sheet>
    </div>
  );
}
