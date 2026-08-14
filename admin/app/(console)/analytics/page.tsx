import { Users, Building2, Calendar, TrendingUp } from "lucide-react";
import { Card, MiniBars, SectionHeader, StatCard } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { getAnalytics, apiErrorMessage, apiErrorStatus } from "@/lib/api";

// Platform analytics (D-063). Read-only growth aggregates over real users/orgs/events data. No revenue —
// that arrives with the Finance module (money doesn't move yet). Any-staff gated (non-PII aggregates).
export default async function AnalyticsPage() {
  const session = await requireStaffSession();

  let a: Awaited<ReturnType<typeof getAnalytics>> | null = null;
  let error: string | null = null;
  try {
    a = await getAnalytics(session.accessToken, 30);
  } catch (err) {
    if (apiErrorStatus(err) === 403) {
      return (
        <div className="space-y-4">
          <h1 className="text-2xl font-semibold text-text">Analytics</h1>
          <Card><p className="text-sm text-muted">You don&apos;t have access to analytics.</p></Card>
        </div>
      );
    }
    error = apiErrorMessage(err);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        kicker="Platform"
        title="Analytics"
        description={`Growth over the last ${a?.window_days ?? 30} days. Revenue metrics arrive with the Finance module.`}
      />

      {error ? (
        <Card><p className="text-sm text-danger">Couldn&apos;t load analytics: {error}</p></Card>
      ) : a ? (
        <>
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard label="Total users" value={String(a.total_users)} icon={<Users size={18} />} />
            <StatCard label={`New users (${a.window_days}d)`} value={String(a.new_users_in_window)} icon={<TrendingUp size={18} />} />
            <StatCard label="Total organizations" value={String(a.total_orgs)} icon={<Building2 size={18} />} />
            <StatCard label="Total events" value={String(a.total_events)} icon={<Calendar size={18} />} />
          </section>

          <section className="grid gap-4 lg:grid-cols-2">
            <Card>
              <p className="text-sm font-medium text-text">Signups / day</p>
              <div className="mt-3"><MiniBars data={a.signups_by_day} /></div>
            </Card>
            <Card>
              <p className="text-sm font-medium text-text">Events created / day</p>
              <div className="mt-3"><MiniBars data={a.events_by_day} /></div>
            </Card>
          </section>

          <section className="grid gap-4 lg:grid-cols-3">
            <Card>
              <SectionHeader title="Events by status" />
              <ul className="mt-1 space-y-1 text-sm">
                {a.events_by_status.map((s) => (
                  <li key={s.status} className="flex justify-between">
                    <span className="capitalize text-muted">{s.status}</span>
                    <span className="text-text">{s.count}</span>
                  </li>
                ))}
              </ul>
            </Card>
            <Card>
              <SectionHeader title="Top organizers" />
              <ul className="mt-1 space-y-1 text-sm">
                {a.top_organizers.length === 0 ? <li className="text-muted">—</li> : a.top_organizers.map((o) => (
                  <li key={o.org_id} className="flex justify-between gap-2">
                    <span className="truncate text-muted">{o.name}</span>
                    <span className="text-text">{o.published_events}</span>
                  </li>
                ))}
              </ul>
            </Card>
            <Card>
              <SectionHeader title="Top events (views)" />
              <ul className="mt-1 space-y-1 text-sm">
                {a.top_events.length === 0 ? <li className="text-muted">—</li> : a.top_events.map((e) => (
                  <li key={e.event_id} className="flex justify-between gap-2">
                    <span className="truncate text-muted">{e.title}</span>
                    <span className="text-text">{e.views}</span>
                  </li>
                ))}
              </ul>
            </Card>
          </section>
        </>
      ) : null}
    </div>
  );
}
