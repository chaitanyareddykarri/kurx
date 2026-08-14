import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  listAdminEvents, listCategories, listAudit, listReports, getFraudScoresBatch,
  getEventAnalyticsAsAdmin, listTicketTypesAsAdmin, listAttendeesAsAdmin,
  getOrgWalletAsAdmin, getOrgWalletLedgerAsAdmin, listOrgEventsAsAdmin, getFraudScore,
  getEventDetailAsAdmin,
  apiErrorMessage, apiErrorStatus, section, type AdminEvent, type AdminEventListParams
} from "@/lib/api";
import { EventsWorkspace } from "@/components/admin/events-workspace/events-workspace";
import { TAB_STATUS, parseTab } from "@/lib/event-tabs";
import { Card } from "@kurx/ui";

// Admin Event Management (D-186) — one workspace replacing the old thin "All events" list. Every
// tab is the same reusable table over `GET /v1/admin/events` with a different status preset; opening
// a row composes ALREADY-EXISTING org-scoped endpoints (analytics/ticket-types/attendees/wallet) the
// same way web's organizer dashboard does — the admin session's own kurx_admin claim already bypasses
// their org-membership check, so nothing here is a rewritten query.

type SearchParams = {
  tab?: string; q?: string; category?: string; city?: string; visibility?: string; paid?: string;
  verified?: string; dateFrom?: string; dateTo?: string; revenueMin?: string; revenueMax?: string;
  regMin?: string; regMax?: string; sort?: string; event?: string; page?: string;
};

const PAGE_SIZE = 50;

export default async function EventsPage({ searchParams }: { searchParams: SearchParams }) {
  const session = await requireStaffSession();
  const tab = parseTab(searchParams.tab);

  const params: AdminEventListParams = {
    q: searchParams.q || undefined,
    status: TAB_STATUS[tab],
    categoryId: searchParams.category || undefined,
    city: searchParams.city || undefined,
    visibility: searchParams.visibility || undefined,
    isPaid: searchParams.paid === "paid" ? true : searchParams.paid === "free" ? false : undefined,
    verifiedOnly: searchParams.verified === "1" ? true : undefined,
    // "Upcoming" = Published and starting in the future — Published is the authoritative
    // registration-open state (Enums.cs), so this is the common case; a future-dated Scheduled
    // event (a newer, rarer Phase-14 state) would need a second query and isn't included here.
    dateFrom: tab === "upcoming" ? new Date().toISOString() : (searchParams.dateFrom || undefined),
    dateTo: searchParams.dateTo || undefined,
    revenueMin: searchParams.revenueMin ? Number(searchParams.revenueMin) * 100 : undefined,
    revenueMax: searchParams.revenueMax ? Number(searchParams.revenueMax) * 100 : undefined,
    registrationsMin: searchParams.regMin ? Number(searchParams.regMin) : undefined,
    registrationsMax: searchParams.regMax ? Number(searchParams.regMax) : undefined,
    sort: searchParams.sort || undefined,
    limit: PAGE_SIZE,
    // D-191: the backend has returned real {items,total} pagination since D-187 — this page just never
    // adopted it (previously hardcoded limit:100 and discarded total, so row 101+ was silently unreachable
    // exactly the way D-187's own fix description warns about, just one layer up from where it was fixed).
    page: Math.max(Number(searchParams.page) || 1, 1)
  };

  try {
    const [{ items: events, total }, categories] = await Promise.all([
      listAdminEvents(session.accessToken, params),
      listCategories(session.accessToken, { level: "Category" })
    ]);
    const totalPages = Math.max(Math.ceil(total / PAGE_SIZE), 1);

    // Reports/fraud are separate services (D-186 §7: never reach into another service's table from
    // EventService) — composed here at the page layer, one call each for the whole visible page, not
    // per-row. Best-effort: a stalled reports/fraud call must never blank the events table.
    const eventIds = events.map((e) => e.event_id);
    //
    // "Best-effort" is right for fraud — a missing score is a missing badge. It is NOT right for
    // reports: a row with no report badge reads as *no open reports against this event*, and a
    // moderator scanning the queue passes over it on that basis. An outage must not produce that
    // sentence, so the failure is carried to the table rather than flattened into an empty list.
    const [reportsR, fraudScores] = await Promise.all([
      section(listReports(session.accessToken, "open")),
      getFraudScoresBatch(session.accessToken, "Event", eventIds).catch(() => [])
    ]);
    const openReports = reportsR.state === "ok" ? reportsR.data : [];
    const reportsUnavailable = reportsR.state !== "ok";
    const reportCounts = new Map<string, number>();
    for (const r of openReports) if (r.entity_type === "event") reportCounts.set(r.entity_id, (reportCounts.get(r.entity_id) ?? 0) + 1);
    const fraudByEvent = new Map(fraudScores.map((s) => [s.subject_id, s.risk_score]));

    // The open event's full workspace — every tab's data fetched in parallel up front (admin-scale
    // traffic; no per-tab lazy fetch machinery needed) so the Sheet is pure presentation.
    let workspace: Awaited<ReturnType<typeof loadWorkspace>> | null = null;
    let workspaceError: string | null = null;
    const openEvent = searchParams.event ? events.find((e) => e.event_id === searchParams.event) : undefined;
    if (openEvent) {
      try {
        workspace = await loadWorkspace(session.accessToken, openEvent);
      } catch (err) {
        workspaceError = apiErrorMessage(err);
      }
    }

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Events"
          title="Event Management"
          description="Every event on the platform — search, filter, and open any row for the full operational workspace."
        />
        {reportsUnavailable ? (
          <p role="status" className="rounded-lg border border-dashed border-warning/50 bg-warning/10 p-3 text-sm text-text">
            Open reports couldn&apos;t be loaded, so no row below shows a report count. An absent
            badge here does <strong>not</strong> mean an event is unreported — refresh before
            triaging.
          </p>
        ) : null}
        <EventsWorkspace
          tab={tab}
          events={events}
          total={total}
          page={params.page ?? 1}
          totalPages={totalPages}
          categories={categories.map((c) => ({ id: c.id, name: c.name }))}
          reportCounts={Object.fromEntries(reportCounts)}
          fraudByEvent={Object.fromEntries(fraudByEvent)}
          searchParams={searchParams}
          openEvent={openEvent ?? null}
          workspace={workspace}
          workspaceError={workspaceError}
          isSuperAdmin={session.roles.includes("SuperAdmin")}
        />
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Event Management</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have Verification Reviewer access.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load events: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}

/** Everything the 9-tab workspace needs for one event, fetched in parallel. Every call here is an
 *  already-existing endpoint (org-scoped analytics/tickets/attendees/wallet, or newly-admin-authorized
 *  wallet/fraud reads) — nothing is a bespoke aggregation written for this page. */
async function loadWorkspace(accessToken: string, event: AdminEvent) {
  // The organisation the event REPRESENTS (D-268/D-273a) — what the org-scoped admin reads are keyed on.
  const { representing_org_id: orgId, event_id: eventId } = event;
  const [analytics, ticketTypes, attendees, wallet, ledger, orgEvents, orgFraud, eventFraud, auditTrail, reports, detail] =
    await Promise.all([
      getEventAnalyticsAsAdmin(accessToken, orgId, eventId).catch(() => null),
      listTicketTypesAsAdmin(accessToken, orgId, eventId).catch(() => []),
      listAttendeesAsAdmin(accessToken, orgId, eventId).catch(() => ({ items: [], total: 0 })),
      getOrgWalletAsAdmin(accessToken, orgId).catch(() => null),
      getOrgWalletLedgerAsAdmin(accessToken, orgId, 1, 20).catch(() => []),
      listOrgEventsAsAdmin(accessToken, orgId).catch(() => ({ items: [], total: 0 })),
      getFraudScore(accessToken, "Organization", orgId).catch(() => null),
      getFraudScore(accessToken, "Event", eventId).catch(() => null),
      // D-191: a real server-side per-event query — previously fetched every `events`-entity row and
      // filtered client-side.
      listAudit(accessToken, { entity: "events", entityId: eventId }).catch(() => []),
      listReports(accessToken, undefined).catch(() => []),
      getEventDetailAsAdmin(accessToken, orgId, eventId).catch(() => null)
    ]);

  return {
    analytics, ticketTypes, attendees, wallet,
    ledger: ledger.filter((l) => l.event_id === eventId),
    orgEvents: orgEvents.items,
    orgRiskScore: orgFraud?.risk_score ?? 0,
    eventRiskScore: eventFraud?.risk_score ?? 0,
    timeline: auditTrail,
    moderation: reports.filter((r) => r.entity_type === "event" && r.entity_id === eventId),
    media: detail?.media ?? [],
    publishedAt: detail?.published_at ?? null
  };
}
