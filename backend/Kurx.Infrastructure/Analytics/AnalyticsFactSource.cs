using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Analytics;

/// <summary>One day of leaf-fact aggregates for one event — the shape the sales timeline / CSV export need.</summary>
public record AnalyticsDayFact(DateTime Date, int Views, int UniqueVisitors, int Registrations,
    int PaidRegistrations, int FreeRegistrations, long RevenuePaise, int CheckIns, int WaitlistCount, int RefundCount);

/// <summary>V3 §16 (Phase 17): the ONE place every analytics number is computed from. `public` only because
/// C# requires a DI-constructed public class's constructor parameters to be at least as accessible as the
/// class itself — by convention this type is never referenced outside <c>Kurx.Infrastructure</c> (not from
/// <c>Application</c>/<c>Api</c>), so the aggregation strategy (today: read-time queries over leaf-fact
/// tables) stays a swappable implementation detail. A future pass can add a second implementation
/// (materialized views, a cache, incremental rollups) and change one DI registration;
/// <see cref="AnalyticsService"/>, <c>IAnalyticsService</c>'s public contract, and every caller stay exactly
/// as they are. <see cref="Events.EventService"/> (a different Infrastructure service, same assembly) also
/// consumes this directly for its own batch list-row stats — the point is one canonical fact source, not
/// one canonical class name.</summary>
public interface IAnalyticsFactSource
{
    /// <summary>Revenue per event — V3 §9.5: <see cref="ValueAllocationRecord"/> is the sole basis for
    /// revenue reporting AND refunds, so this can never diverge from what <c>RefundService</c> refunds.</summary>
    Task<Dictionary<Guid, long>> RevenueByEventAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);

    /// <summary>Revenue for an org — optionally rolled up across every descendant OrgUnit (dual-tree
    /// rollup, §16), via the materialized <see cref="OrgUnit.Path"/> (Phase 4).</summary>
    Task<long> RevenueForOrgAsync(Guid orgId, bool includeDescendantUnits, CancellationToken ct);

    /// <summary>Day-bucketed facts for one event across every leaf table — the sales timeline / CSV export.</summary>
    Task<IReadOnlyList<AnalyticsDayFact>> DailyFactsAsync(Guid eventId, CancellationToken ct);

    /// <summary>Checked-in count per event, from <see cref="Ticket.CheckedInAt"/> — the one real timestamp
    /// leaf fact, not a derived <c>TicketState</c> comparison or a separate <c>GateEntries</c> count.</summary>
    Task<Dictionary<Guid, int>> CheckedInByEventAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);

    /// <summary>Distinct people checked in across a set of events — pass a parent event's id plus its
    /// sub-events' ids to get "attendance at a parent" (§16: distinct people admitted to ≥1 sub-event,
    /// never the sum of sub-event attendance; a person at two sub-events counts once).</summary>
    Task<int> DistinctCheckedInAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);

    Task<int> ViewsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);
    Task<int> UniqueVisitorsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);
    Task<int> RegistrationsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);
    Task<int> RefundCountAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);
}

public class LeafFactSource(KurxDbContext db) : IAnalyticsFactSource
{
    public async Task<Dictionary<Guid, long>> RevenueByEventAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
    {
        if (eventIds.Count == 0) return [];
        var byEvent = await db.ValueAllocationRecords.AsNoTracking().Where(v => eventIds.Contains(v.EventId))
            .GroupBy(v => v.EventId)
            .Select(g => new { EventId = g.Key, Paise = g.Sum(v => v.AllocatedPaise) })
            .ToDictionaryAsync(x => x.EventId, x => x.Paise, ct);
        return eventIds.ToDictionary(id => id, id => byEvent.GetValueOrDefault(id));
    }

    public async Task<long> RevenueForOrgAsync(Guid orgId, bool includeDescendantUnits, CancellationToken ct)
    {
        var eventIds = await ScopedEventIdsAsync(orgId, includeDescendantUnits, ct);
        if (eventIds.Count == 0) return 0;
        return await db.ValueAllocationRecords.AsNoTracking()
            .Where(v => eventIds.Contains(v.EventId)).SumAsync(v => v.AllocatedPaise, ct);
    }

    public async Task<IReadOnlyList<AnalyticsDayFact>> DailyFactsAsync(Guid eventId, CancellationToken ct)
    {
        var views = await db.EventViews.AsNoTracking().Where(v => v.EventId == eventId)
            .GroupBy(v => v.ViewedAt.Date)
            .Select(g => new { Date = g.Key, Views = g.Count(), Uniques = g.Select(v => v.VisitorKey).Distinct().Count() })
            .ToDictionaryAsync(x => x.Date, ct);

        var registrations = await db.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId && r.State != RegistrationState.Cancelled)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var revenue = await db.ValueAllocationRecords.AsNoTracking().Where(v => v.EventId == eventId)
            .GroupBy(v => v.ComputedAt.Date)
            .Select(g => new { Date = g.Key, Paise = g.Sum(v => v.AllocatedPaise) })
            .ToDictionaryAsync(x => x.Date, x => x.Paise, ct);

        var checkIns = await db.Tickets.AsNoTracking()
            .Where(t => t.EventId == eventId && t.CheckedInAt != null)
            .GroupBy(t => t.CheckedInAt!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => r.Status != RefundStatus.Failed)
            .Join(db.Orders.AsNoTracking().Where(o => o.EventId == eventId), r => r.OrderId, o => o.Id, (r, _) => r)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        // Paid/free split needs the ticket type's price at the moment of registration; TicketType.PricePaise
        // is the current price (immutable per V3 §13.1's "never dates/slug/status/financials" doesn't apply
        // here — this is the event's own live ticket type, not a template), acceptable for a day-bucketed
        // split since Kurx doesn't version historical prices per registration.
        var paidTicketTypeIds = (await db.TicketTypes.AsNoTracking()
            .Where(t => t.EventId == eventId && t.PricePaise > 0).Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var regSplit = await db.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId && r.State != RegistrationState.Cancelled)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Paid = g.Count(r => paidTicketTypeIds.Contains(r.TicketTypeId)),
                Free = g.Count(r => !paidTicketTypeIds.Contains(r.TicketTypeId)),
            })
            .ToDictionaryAsync(x => x.Date, ct);

        var waitlist = await db.TicketWaitlists.AsNoTracking()
            .CountAsync(w => w.EventId == eventId && w.Status == WaitlistStatus.Waiting, ct);

        var allDates = views.Keys.Concat(registrations.Keys).Concat(revenue.Keys).Concat(checkIns.Keys).Concat(refunds.Keys)
            .Distinct().OrderBy(d => d).ToList();

        return allDates.Select(date =>
        {
            var v = views.GetValueOrDefault(date);
            var split = regSplit.GetValueOrDefault(date);
            return new AnalyticsDayFact(
                date, v?.Views ?? 0, v?.Uniques ?? 0, registrations.GetValueOrDefault(date),
                split?.Paid ?? 0, split?.Free ?? 0, revenue.GetValueOrDefault(date),
                checkIns.GetValueOrDefault(date), waitlist, refunds.GetValueOrDefault(date));
        }).ToList();
    }

    public async Task<Dictionary<Guid, int>> CheckedInByEventAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
    {
        if (eventIds.Count == 0) return [];
        var byEvent = await db.Tickets.AsNoTracking()
            .Where(t => eventIds.Contains(t.EventId) && t.CheckedInAt != null)
            .GroupBy(t => t.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, ct);
        return eventIds.ToDictionary(id => id, id => byEvent.GetValueOrDefault(id));
    }

    public async Task<int> DistinctCheckedInAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
    {
        if (eventIds.Count == 0) return 0;
        // Distinct by PersonId (never by Ticket/Admission row) — a person checked into two sub-events counts
        // once, matching §16's "attendance at a parent ≠ sum of sub-event attendance."
        return await db.Admissions.AsNoTracking()
            .Where(a => eventIds.Contains(a.EventId) && a.PersonId != null)
            .Join(db.Tickets.AsNoTracking().Where(t => t.CheckedInAt != null), a => a.TicketId, t => t.Id, (a, _) => a.PersonId!.Value)
            .Distinct()
            .CountAsync(ct);
    }

    public async Task<int> ViewsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
        => eventIds.Count == 0 ? 0 : await db.EventViews.AsNoTracking().CountAsync(v => eventIds.Contains(v.EventId), ct);

    public async Task<int> UniqueVisitorsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
        => eventIds.Count == 0 ? 0 : await db.EventViews.AsNoTracking().Where(v => eventIds.Contains(v.EventId))
            .Select(v => v.VisitorKey).Distinct().CountAsync(ct);

    public async Task<int> RegistrationsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
        => eventIds.Count == 0 ? 0 : await db.Registrations.AsNoTracking()
            .CountAsync(r => eventIds.Contains(r.EventId) && r.State != RegistrationState.Cancelled, ct);

    public async Task<int> RefundCountAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
        => eventIds.Count == 0 ? 0 : await db.Refunds.AsNoTracking()
            .Where(r => r.Status != RefundStatus.Failed)
            .Join(db.Orders.AsNoTracking().Where(o => eventIds.Contains(o.EventId)), r => r.OrderId, o => o.Id, (r, _) => r)
            .CountAsync(ct);

    /// <summary>Dual-tree rollup, org side (§16): every event owned by this org, optionally widened to every
    /// OrgUnit under it via the materialized path (Phase 4) — one flat filter, so a descendant's events are
    /// counted exactly once, never fanned out by a self-join.</summary>
    private async Task<List<Guid>> ScopedEventIdsAsync(Guid orgId, bool includeDescendantUnits, CancellationToken ct)
    {
        var root = await db.OrgUnits.AsNoTracking().Where(u => u.OrgId == orgId && u.ParentId == null)
            .Select(u => new { u.Id, u.Path }).FirstOrDefaultAsync(ct);
        if (root is null) return await db.Events.AsNoTracking().Where(e => e.RepresentingOrgId == orgId).Select(e => e.Id).ToListAsync(ct);

        // Not rolled up: only the root unit's own events (an event with no OrgUnitId at all — legacy/backfill
        // edge case — counts as the root's own). Rolled up: widen to every unit under the root via the
        // materialised path prefix (includes the root itself, so this is always a superset of the above).
        var unitIds = includeDescendantUnits
            ? await db.OrgUnits.AsNoTracking().Where(u => u.OrgId == orgId && u.Path.StartsWith(root.Path)).Select(u => u.Id).ToListAsync(ct)
            : [root.Id];
        return await db.Events.AsNoTracking()
            .Where(e => e.RepresentingOrgId == orgId && (e.OrgUnitId == null || unitIds.Contains(e.OrgUnitId.Value)))
            .Select(e => e.Id).ToListAsync(ct);
    }
}
