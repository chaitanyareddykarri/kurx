import { requireEventOrg } from "@/lib/event-org";
import { getEventAnalytics, getEventAttendance, getEventTickets, getEventRevenue, getEventSales, section, sectionData } from "@/lib/api";
import { Card, Stat } from "@kurx/ui";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { CsvDownloadButton } from "@/components/host/csv-download-button";
import { exportEventAnalyticsAction } from "@/lib/event-actions";

export default async function EventAnalyticsPage({ params }: { params: { id: string } }) {
  const { session, orgId } = await requireEventOrg(params.id);

  // Each read is tolerated independently. A bare Promise.all rejected the whole page on the first
  // failure, so one 403 — every analytics read shares an authorization check, so it is never just one —
  // surfaced as an Unhandled Runtime Error naming whichever call lost the race, rather than as the
  // permission outcome it was. `section` is the existing convention for a read that may legitimately be
  // unavailable to this viewer (403/404 -> hidden, anything else -> unavailable).
  const [aR, attendanceR, ticketsR, revenueR, salesR] = await Promise.all([
    section(getEventAnalytics(session.accessToken, orgId, params.id)),
    section(getEventAttendance(session.accessToken, orgId, params.id)),
    section(getEventTickets(session.accessToken, orgId, params.id)),
    section(getEventRevenue(session.accessToken, orgId, params.id)),
    section(getEventSales(session.accessToken, orgId, params.id))
  ]);

  // Nothing resolved: the viewer cannot see this event's analytics at all. Saying so is the whole point
  // — the previous behaviour told them the application had crashed.
  if (aR.state !== "ok" && attendanceR.state !== "ok" && ticketsR.state !== "ok"
      && revenueR.state !== "ok" && salesR.state !== "ok") {
    const unavailable = aR.state === "unavailable";
    return (
      <div className="space-y-6">
        <h2 className="text-lg font-semibold">Analytics</h2>
        <div role="status" className="rounded-lg border border-dashed border-border bg-surface p-6 text-center">
          <p className="text-body text-text">
            {unavailable ? "Analytics couldn't be loaded." : "You don't have access to this event's analytics."}
          </p>
          <p className="mt-1 text-sm text-muted">
            {unavailable
              ? "This is a temporary problem on our side, not a change to your event. Try refreshing."
              : "Ask an owner or manager of the organizing team to grant you analytics access."}
          </p>
        </div>
      </div>
    );
  }

  const a = sectionData(aR, { event_id: params.id, view_count: 0, ticket_types: 0, tickets_issued: 0,
    checked_in: 0, orders_paid: 0, gross_paise: 0 });
  const attendance = sectionData(attendanceR, { checked_in: 0, total_tickets: 0, attendance_rate: 0 });
  const tickets = sectionData(ticketsR, { capacity: 0, sold: 0, remaining: 0 });
  const revenue = sectionData(revenueR, [] as Awaited<ReturnType<typeof getEventRevenue>>);
  const sales = sectionData(salesR, [] as Awaited<ReturnType<typeof getEventSales>>);
  const maxRevenue = Math.max(1, ...sales.map((s) => s.revenue_paise));

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold">Analytics</h2>
        <CsvDownloadButton action={exportEventAnalyticsAction.bind(null, orgId, params.id)} filename={`event-${params.id}-analytics.csv`} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <Stat label="Page views" value={a.view_count.toLocaleString("en-IN")} />
        <Stat label="Tickets issued" value={a.tickets_issued.toLocaleString("en-IN")} />
        <Stat label="Checked in" value={attendance.checked_in.toLocaleString("en-IN")} />
        <Stat label="Attendance rate" value={`${Math.round(attendance.attendance_rate * 100)}%`} />
        <Stat label="Sold / capacity" value={`${tickets.sold} / ${tickets.capacity}`} />
        <Stat label="Gross revenue" value={formatCurrency(a.gross_paise)} />
      </div>

      <Card>
        <h3 className="font-semibold">Sales over time</h3>
        {sales.length === 0 ? (
          <p className="mt-2 text-sm text-muted">No daily sales recorded yet.</p>
        ) : (
          <div className="mt-4 space-y-1.5">
            {sales.map((s) => (
              <div key={s.date} className="flex items-center gap-3 text-xs">
                <span className="w-16 shrink-0 text-muted">{formatDate(s.date, "en-IN", { day: "numeric", month: "short" })}</span>
                <div className="h-3 flex-1 rounded bg-elevated">
                  <div className="h-3 rounded bg-accent" style={{ width: `${(s.revenue_paise / maxRevenue) * 100}%` }} />
                </div>
                <span className="w-28 shrink-0 text-right text-muted">{formatCurrency(s.revenue_paise)} · {s.ticket_count} tix</span>
              </div>
            ))}
          </div>
        )}
      </Card>

      <section className="space-y-3">
        <h3 className="text-lg font-semibold">Revenue by ticket type</h3>
        <Card className="p-0">
          {/* Focusable like DataTable's wrapper, so keyboard users can scroll the table. */}
          <div tabIndex={0} aria-label="Revenue by ticket type" className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-left text-muted">
                <th scope="col" className="px-5 py-3 font-medium">Ticket type</th>
                <th scope="col" className="px-5 py-3 font-medium">Sold</th>
                <th scope="col" className="px-5 py-3 text-right font-medium">Revenue</th>
              </tr>
            </thead>
            <tbody>
              {revenue.length === 0 ? (
                <tr><td colSpan={3} className="px-5 py-6 text-center text-muted">No ticket types yet.</td></tr>
              ) : (
                revenue.map((r) => (
                  <tr key={r.ticket_type_id} className="border-b border-border last:border-0">
                    <td className="px-5 py-3">{r.name}</td>
                    <td className="px-5 py-3 text-muted">{r.quantity_sold}</td>
                    <td className="px-5 py-3 text-right font-medium text-text">{formatCurrency(r.revenue_paise)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          </div>
        </Card>
      </section>
    </div>
  );
}
