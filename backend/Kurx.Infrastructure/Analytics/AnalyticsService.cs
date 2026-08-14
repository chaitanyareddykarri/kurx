using System.Globalization;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Analytics;

/// <summary>V3 §16 (Phase 17). Authorization + DTO assembly only — every actual number comes from
/// <see cref="IAnalyticsFactSource"/>, the one place the leaf-fact queries live (see its own doc comment
/// for why that split exists). This class is the only public entry point analytics ever reaches through:
/// <see cref="Kurx.Infrastructure.Events.EventService"/>'s admin/org list-row stats and single-event
/// analytics route are thin delegations to this same class (see its own doc comment), so there is exactly
/// one computation for every number, everywhere it's shown.</summary>
public class AnalyticsService(KurxDbContext db, IInventoryService inventory, IAnalyticsFactSource facts,
    IEventAuthority authority) : IAnalyticsService
{
    public async Task<ServiceResult<OrgAnalyticsView>> GetOrgAnalyticsAsync(Guid userId, Guid orgId, bool includeDescendantUnits = false, CancellationToken ct = default)
    {
        if (!await HasAccess(userId, orgId, ct)) return ServiceResult<OrgAnalyticsView>.Fail("forbidden");

        var eventIds = await db.Events.AsNoTracking().Where(e => e.RepresentingOrgId == orgId).Select(e => e.Id).ToListAsync(ct);
        var revenue = await facts.RevenueForOrgAsync(orgId, includeDescendantUnits, ct);
        var views = await facts.ViewsAsync(eventIds, ct);
        var uniques = await facts.UniqueVisitorsAsync(eventIds, ct);
        var regs = await facts.RegistrationsAsync(eventIds, ct);

        return ServiceResult<OrgAnalyticsView>.Success(new OrgAnalyticsView(revenue, views, uniques, regs));
    }

    public async Task<ServiceResult<IReadOnlyList<SalesTimelineItem>>> GetEventSalesTimelineAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<IReadOnlyList<SalesTimelineItem>>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, eventId, isAdmin, ct)).Can(EventPermission.ViewAnalytics))
            return ServiceResult<IReadOnlyList<SalesTimelineItem>>.Fail("forbidden");

        var daily = await facts.DailyFactsAsync(eventId, ct);
        IReadOnlyList<SalesTimelineItem> list = daily.Select(d => new SalesTimelineItem(d.Date, d.Registrations, d.RevenuePaise)).ToList();
        return ServiceResult<IReadOnlyList<SalesTimelineItem>>.Success(list);
    }

    public async Task<ServiceResult<AttendanceMetricView>> GetEventAttendanceAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<AttendanceMetricView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, eventId, isAdmin, ct)).Can(EventPermission.ViewAnalytics))
            return ServiceResult<AttendanceMetricView>.Fail("forbidden");

        var totalTickets = await db.Tickets.CountAsync(t => t.EventId == eventId && t.State != TicketState.Void, ct);
        var checkedIn = (await facts.CheckedInByEventAsync([eventId], ct))[eventId];
        var rate = totalTickets > 0 ? (double)checkedIn / totalTickets : 0.0;

        return ServiceResult<AttendanceMetricView>.Success(new AttendanceMetricView(totalTickets, checkedIn, rate));
    }

    public async Task<ServiceResult<IReadOnlyList<TicketTypeRevenueView>>> GetEventRevenueAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<IReadOnlyList<TicketTypeRevenueView>>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, eventId, isAdmin, ct)).Can(EventPermission.ViewAnalytics))
            return ServiceResult<IReadOnlyList<TicketTypeRevenueView>>.Fail("forbidden");

        var ticketTypes = await db.TicketTypes.AsNoTracking().Where(t => t.EventId == eventId).ToListAsync(ct);

        // V3 §9.5: revenue per ticket type is VAR allocated-paise attributed through the order item's ticket
        // type — never sold-count × current-price (that diverges the moment a price changes after some
        // tickets sell; VAR is a purchase-time snapshot, immune to that).
        var revenueByType = await db.ValueAllocationRecords.AsNoTracking()
            .Where(v => v.EventId == eventId)
            .Join(db.OrderItems.AsNoTracking(), v => v.OrderItemId, oi => oi.Id, (v, oi) => new { v.AllocatedPaise, oi.TicketTypeId })
            .GroupBy(x => x.TicketTypeId)
            .Select(g => new { TicketTypeId = g.Key, Paise = g.Sum(x => x.AllocatedPaise), Count = g.Count() })
            .ToDictionaryAsync(x => x.TicketTypeId, ct);

        var results = ticketTypes.Select(tt =>
        {
            var r = revenueByType.GetValueOrDefault(tt.Id);
            return new TicketTypeRevenueView(tt.Id, tt.Name, r?.Count ?? 0, r?.Paise ?? 0);
        }).ToList();

        return ServiceResult<IReadOnlyList<TicketTypeRevenueView>>.Success(results);
    }

    public async Task<ServiceResult<TicketAnalyticsView>> GetEventTicketsAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<TicketAnalyticsView>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, eventId, isAdmin, ct)).Can(EventPermission.ViewAnalytics))
            return ServiceResult<TicketAnalyticsView>.Fail("forbidden");

        var ticketTypes = await db.TicketTypes.AsNoTracking().Where(t => t.EventId == eventId).ToListAsync(ct);

        // Sold/available come from the AUTHORITATIVE inventory pools (§17.1, Phase 9) — unchanged, already correct.
        var counts = await inventory.PoolCountsAsync(ticketTypes.Select(t => t.Id).ToList(), ct);
        var totalCap = ticketTypes.Sum(t => t.Quantity);
        var totalSold = ticketTypes.Sum(t => counts.TryGetValue(t.Id, out var c) ? c.Sold : t.Sold);
        var totalAvail = ticketTypes.Sum(t => counts.TryGetValue(t.Id, out var c) ? c.Available : Math.Max(0, t.Quantity - t.Sold));

        return ServiceResult<TicketAnalyticsView>.Success(new TicketAnalyticsView(totalCap, totalSold, totalAvail));
    }

    public async Task<ServiceResult<string>> ExportEventAnalyticsCsvAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<string>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, eventId, isAdmin, ct)).Can(EventPermission.ViewAnalytics))
            return ServiceResult<string>.Fail("forbidden");

        var daily = await facts.DailyFactsAsync(eventId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Date,Views,UniqueVisitors,Registrations,PaidRegistrations,FreeRegistrations,RevenuePaise,CheckIns,WaitlistCount,RefundCount");
        foreach (var d in daily)
        {
            csv.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                d.Date, d.Views, d.UniqueVisitors, d.Registrations, d.PaidRegistrations, d.FreeRegistrations,
                d.RevenuePaise, d.CheckIns, d.WaitlistCount, d.RefundCount));
        }

        return ServiceResult<string>.Success(csv.ToString());
    }

    /// <summary>ORG-level analytics only. Event-level reads resolve through <see cref="IEventAuthority"/>
    /// instead (D-269): this membership test asks "does the caller hold a seat in the organization",
    /// which is a different and strictly narrower question than "may the caller see this event". An event
    /// creator, or a Representative who set the org up, holds no Owner/Manager/Finance seat and was
    /// refused the analytics of an event they own — every one of the five reads 403'd at once, which the
    /// host page surfaced as an unhandled crash rather than as a permission outcome.</summary>
    private async Task<bool> HasAccess(Guid userId, Guid orgId, CancellationToken ct)
        => await db.Memberships.AnyAsync(
            m => m.OrgId == orgId && m.UserId == userId
                 && (m.Role == OrgRole.Owner || m.Role == OrgRole.Manager || m.Role == OrgRole.Finance), ct);
}
