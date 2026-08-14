"use client";

import { Badge, Card, Stat } from "@kurx/ui";
import { AlertTriangle, ExternalLink } from "lucide-react";
import type { AdminOrg, AdminOrgDetail, OrgEventRow, WalletAdmin } from "@/lib/api";
import { money } from "@/lib/format";
import { WEB_URL } from "@/lib/site";
import { OrgAdminTools } from "@/components/admin/org-admin-tools";


/** D-194: org detail composes already-built admin reads (getOrgWalletAsAdmin, getFraudScore,
 * listOrgEventsAsAdmin — all D-186, built for the event workspace's Organizer tab) rather than a
 * second data path. Anything that failed to load is shown as an explicit error, never silently blank. */
export function OrgDetailContent({
  org,
  detail,
  detailError,
  wallet,
  riskScore,
  events,
  canModerate
}: {
  org: AdminOrg;
  detail: AdminOrgDetail | null;
  detailError: string | null;
  wallet: WalletAdmin | null;
  riskScore: number | null;
  events: OrgEventRow[];
  canModerate: boolean;
}) {
  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-2">
        <Badge tone={riskScore && riskScore > 50 ? "danger" : "neutral"}>risk {riskScore ?? "—"}</Badge>
        <a href={`${WEB_URL}/o/${org.slug}`} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 text-xs text-accent-text hover:underline">
          Public profile <ExternalLink size={12} />
        </a>
      </div>

      {detailError ? (
        <Card className="border-danger/30 bg-danger/5">
          <p className="flex items-center gap-2 text-sm text-danger"><AlertTriangle size={14} /> Couldn&apos;t load full detail: {detailError}</p>
        </Card>
      ) : null}

      <Card>
        <h3 className="font-semibold text-text">Overview</h3>
        <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
          {/* A self-representation row, not an organization anyone created (D-268) — staff need to tell
              it apart from an institution awaiting verification, which is why this flag is admin-only. */}
          <Stat label="Type" value={org.is_personal ? "Self-representation" : org.type} />
          <Stat label="Tier" value={detail ? String(detail.tier) : "—"} />
          <Stat label="Payout status" value={detail?.payout_account_status ?? "—"} />
          <Stat label="Domain" value={org.primary_domain ?? "—"} />
        </div>
        {detail?.bio ? <p className="mt-3 text-sm text-muted">{detail.bio}</p> : null}
      </Card>

      <Card>
        <h3 className="font-semibold text-text">Wallet</h3>
        {wallet ? (
          <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Stat label="Available" value={money(wallet.available_paise, wallet.currency)} />
            <Stat label="Reserved" value={money(wallet.reserved_paise, wallet.currency)} />
            <Stat label="Lifetime earned" value={money(wallet.lifetime_earned_paise, wallet.currency)} />
            <Stat label="Lifetime withdrawn" value={money(wallet.lifetime_withdrawn_paise, wallet.currency)} />
          </div>
        ) : (
          <p className="mt-2 text-sm text-muted">Wallet data unavailable.</p>
        )}
      </Card>

      <Card>
        <h3 className="font-semibold text-text">Events ({events.length})</h3>
        {events.length === 0 ? (
          <p className="mt-2 text-sm text-muted">No events yet.</p>
        ) : (
          <ul className="mt-3 space-y-2">
            {events.slice(0, 8).map((e) => (
              <li key={e.id} className="flex items-center justify-between gap-3 border-b border-border/60 pb-2 text-sm last:border-0">
                <span className="truncate text-text">{e.title}</span>
                <span className="shrink-0 text-xs text-muted">{e.status} · {e.tickets_sold} sold</span>
              </li>
            ))}
          </ul>
        )}
        {events.length > 8 ? <p className="mt-2 text-xs text-muted">+{events.length - 8} more</p> : null}
      </Card>

      {canModerate ? (
        <Card>
          <h3 className="font-semibold text-text">Moderation</h3>
          <div className="mt-3">
            <OrgAdminTools defaultOrgId={org.org_id} defaultOrgName={org.name} />
          </div>
        </Card>
      ) : null}
    </div>
  );
}
