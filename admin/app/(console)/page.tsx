import Link from "next/link";
import {
  ShieldCheck, UserCheck, CalendarCheck, Ban, Users, Building2, Calendar, KeyRound,
  ArrowRight, UserPlus, Megaphone, FolderPlus, TrendingUp, Wallet, AlertTriangle, ScrollText
} from "lucide-react";
import { StatCard, Badge, Card, SectionHeader, Sparkline, MiniBars } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  getDashboardSummary, getAnalytics, listAudit, listPendingOrgVerifications, listPendingEvents,
  apiErrorMessage, type DashboardSummary, type Analytics, type AuditEntry, type PendingOrg, type PendingEvent
} from "@/lib/api";
import { relativeTime } from "@/lib/security-activity";
import { ROLE_LABELS, hasRole } from "@/lib/roles";

const ACTIVITY_LIMIT = 8;
const REVIEW_PREVIEW_LIMIT = 4;

function actionIcon(action: string) {
  if (action.startsWith("user.")) return Users;
  if (action.startsWith("org.")) return Building2;
  if (action.startsWith("event.")) return Calendar;
  if (action.includes("verif")) return ShieldCheck;
  if (action.includes("blacklist") || action.includes("ban")) return Ban;
  return ScrollText;
}

// Operational dashboard. Live counts/trends come from GET /v1/admin/dashboard/summary (D-058),
// /v1/admin/analytics (D-063) and /v1/admin/audit (D-062) — every widget here is data one of
// those endpoints already returns elsewhere in admin; nothing is fabricated. Revenue and a fraud
// signal *feed* have no backing endpoint yet (Finance module unbuilt; /risk is record-only), so
// those render as honest placeholders rather than invented numbers (D-185).
export default async function DashboardPage() {
  const { me, roles, accessToken } = await requireStaffSession();
  const isSuperAdmin = roles.includes("SuperAdmin");
  const canReview = hasRole(roles, ["VerificationReviewer"]);
  const canAudit = hasRole(roles, ["SuperAdmin", "ReadOnlyAuditor"]);

  let summary: DashboardSummary | null = null;
  let summaryError: string | null = null;
  try {
    summary = await getDashboardSummary(accessToken);
  } catch (err) {
    summaryError = apiErrorMessage(err);
  }

  let analytics: Analytics | null = null;
  try {
    analytics = await getAnalytics(accessToken, 14);
  } catch {
    analytics = null;
  }

  let activity: AuditEntry[] = [];
  if (canAudit) {
    try {
      activity = (await listAudit(accessToken)).slice(0, ACTIVITY_LIMIT);
    } catch {
      activity = [];
    }
  }

  let pendingOrgs: PendingOrg[] = [];
  let pendingEvents: PendingEvent[] = [];
  if (canReview) {
    try {
      [pendingOrgs, pendingEvents] = await Promise.all([
        listPendingOrgVerifications(accessToken, REVIEW_PREVIEW_LIMIT),
        listPendingEvents(accessToken, REVIEW_PREVIEW_LIMIT)
      ]);
    } catch {
      pendingOrgs = [];
      pendingEvents = [];
    }
  }

  const v = (n?: number) => (summary ? String(n ?? 0) : "—");
  const needsReview = pendingOrgs.length + pendingEvents.length;

  return (
    <div className="space-y-8">
      <PageHeader
        kicker="Platform operations"
        title={`Welcome, ${me.name}`}
        description={
          <span className="flex flex-wrap gap-1.5">
            {roles.map((r) => (
              <Badge key={r} tone="neutral">
                {ROLE_LABELS[r]}
              </Badge>
            ))}
          </span>
        }
      />

      {summaryError ? (
        <Card className="border-danger/30 bg-danger/5">
          <p className="text-sm text-danger">Couldn&apos;t load live metrics: {summaryError}</p>
        </Card>
      ) : null}

      <section aria-label="Work queues" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Pending org verifications" value={v(summary?.pending_org_verifications)} icon={<ShieldCheck size={18} />} href="/verification" />
        <StatCard label="Membership claims" value={v(summary?.pending_membership_claims)} icon={<UserCheck size={18} />} href="/verification" />
        <StatCard label="Events awaiting approval" value={v(summary?.pending_events)} icon={<CalendarCheck size={18} />} href="/events/pending" />
        <StatCard label="Blacklist entries" value={v(summary?.blacklist_entries)} icon={<Ban size={18} />} href="/blacklist" />
      </section>

      <section aria-label="Platform" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Total users"
          value={v(summary?.total_users)}
          icon={<Users size={18} />}
          delta={summary && summary.new_users_24h > 0 ? { value: `+${summary.new_users_24h} today`, positive: true } : undefined}
        />
        <StatCard label="Total organizations" value={v(summary?.total_orgs)} icon={<Building2 size={18} />} />
        <StatCard label="Total events" value={v(summary?.total_events)} icon={<Calendar size={18} />} />
        {isSuperAdmin ? (
          <StatCard label="Platform staff" value={v(summary?.staff_count)} icon={<KeyRound size={18} />} href="/staff" />
        ) : null}
      </section>

      {analytics ? (
        <section aria-label="Growth" className="grid gap-4 lg:grid-cols-2">
          <Card>
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-text">Signups, last 14 days</p>
              <TrendingUp size={15} className="text-accent-text" />
            </div>
            <div className="mt-3">
              <Sparkline data={analytics.signups_by_day} />
            </div>
          </Card>
          <Card>
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-text">Events created, last 14 days</p>
              <Calendar size={15} className="text-accent-text" />
            </div>
            <div className="mt-3">
              <MiniBars data={analytics.events_by_day} />
            </div>
          </Card>
        </section>
      ) : null}

      <section aria-label="Needs review and activity" className="grid gap-4 lg:grid-cols-2">
        {canReview ? (
          <Card>
            <SectionHeader
              title={`Needs your review${needsReview ? ` (${needsReview})` : ""}`}
              subtitle="Newest first — full queues link out."
            />
            {needsReview === 0 ? (
              <p className="py-6 text-center text-sm text-muted">Nothing waiting. Queues are clear.</p>
            ) : (
              <ul className="mt-2 divide-y divide-border">
                {pendingOrgs.map((o) => (
                  <li key={o.org_id}>
                    <Link href="/verification" className="flex items-center justify-between gap-3 py-2.5 text-sm hover:text-accent-text">
                      <span className="flex items-center gap-2 truncate">
                        <ShieldCheck size={14} className="shrink-0 text-muted" />
                        <span className="truncate text-text">{o.name}</span>
                        <span className="shrink-0 text-xs text-muted">org verification</span>
                      </span>
                      <ArrowRight size={14} className="shrink-0 text-muted" />
                    </Link>
                  </li>
                ))}
                {pendingEvents.map((e) => (
                  <li key={e.event_id}>
                    <Link href="/events/pending" className="flex items-center justify-between gap-3 py-2.5 text-sm hover:text-accent-text">
                      <span className="flex items-center gap-2 truncate">
                        <CalendarCheck size={14} className="shrink-0 text-muted" />
                        <span className="truncate text-text">{e.title}</span>
                        <span className="shrink-0 text-xs text-muted">event approval</span>
                      </span>
                      <ArrowRight size={14} className="shrink-0 text-muted" />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        ) : null}

        {canAudit ? (
          <Card>
            <SectionHeader title="Recent activity" subtitle="Latest platform actions from the audit log." />
            {activity.length === 0 ? (
              <p className="py-6 text-center text-sm text-muted">No recent activity.</p>
            ) : (
              <ul className="mt-2 divide-y divide-border">
                {activity.map((a) => {
                  const Icon = actionIcon(a.action);
                  return (
                    <li key={a.id} className="flex items-center gap-3 py-2.5 text-sm">
                      <Icon size={14} className="shrink-0 text-muted" />
                      <span className="flex-1 truncate font-mono text-xs text-text">{a.action}</span>
                      <span className="shrink-0 text-xs text-muted">{relativeTime(a.created_at)}</span>
                    </li>
                  );
                })}
              </ul>
            )}
            <Link href="/audit" className="mt-3 inline-flex items-center gap-1 text-xs font-semibold text-accent-text hover:opacity-80">
              View full audit log <ArrowRight size={12} />
            </Link>
          </Card>
        ) : null}
      </section>

      <section aria-label="Not yet available" className="grid gap-4 sm:grid-cols-2">
        <Card className="border-dashed">
          <div className="flex items-start gap-3">
            <Wallet size={18} className="mt-0.5 shrink-0 text-muted" />
            <div>
              <p className="text-sm font-medium text-text">Revenue summary</p>
              <p className="mt-1 text-xs text-muted">Arrives with the Finance module — money doesn&apos;t move through the platform yet.</p>
            </div>
          </div>
        </Card>
        <Card className="border-dashed">
          <div className="flex items-start gap-3">
            <AlertTriangle size={18} className="mt-0.5 shrink-0 text-muted" />
            <div className="flex-1">
              <p className="text-sm font-medium text-text">Fraud alert feed</p>
              <p className="mt-1 text-xs text-muted">
                Risk signals are recorded but there&apos;s no list endpoint yet — record one at{" "}
                <Link href="/risk" className="text-accent-text hover:opacity-80">Risk signals</Link>.
              </p>
            </div>
          </div>
        </Card>
      </section>

      <section aria-label="Quick actions" className="space-y-2">
        <SectionHeader title="Quick actions" />
        <div className="flex flex-wrap gap-2">
          {isSuperAdmin ? (
            <QuickAction href="/staff" icon={UserPlus} label="Grant staff role" />
          ) : null}
          {canReview ? (
            <QuickAction href="/blacklist" icon={Ban} label="Add to blacklist" />
          ) : null}
          {isSuperAdmin ? (
            <QuickAction href="/broadcast" icon={Megaphone} label="Send broadcast" />
          ) : null}
          {isSuperAdmin ? (
            <QuickAction href="/platform/event-taxonomy" icon={FolderPlus} label="New category" />
          ) : null}
        </div>
      </section>

      <p className="text-xs text-muted">
        Live counts from <code className="rounded bg-elevated px-1 py-0.5">GET /v1/admin/dashboard/summary</code>.
      </p>
    </div>
  );
}

function QuickAction({ href, icon: Icon, label }: { href: string; icon: typeof UserPlus; label: string }) {
  return (
    <Link
      href={href}
      className="inline-flex items-center gap-2 rounded-md border border-border bg-surface px-3.5 py-2 text-sm font-medium text-text transition hover:border-accent/60 hover:bg-elevated"
    >
      <Icon size={15} className="text-accent-text" />
      {label}
    </Link>
  );
}
