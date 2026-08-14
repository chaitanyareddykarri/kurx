namespace Kurx.Application.Abstractions;

public record OrgAnalyticsView(long RevenuePaise, int Views, int UniqueVisitors, int Registrations);
public record SalesTimelineItem(DateTime Date, int TicketCount, long RevenuePaise);
public record AttendanceMetricView(int TotalTickets, int CheckedIn, double AttendanceRate);
public record TicketTypeRevenueView(Guid TicketTypeId, string Name, int QuantitySold, long RevenuePaise);
public record TicketAnalyticsView(int Capacity, int Sold, int Remaining);

/// <summary>V3 §16 (Phase 17): the single canonical read surface for org/event analytics — revenue,
/// attendance, sales, views, refunds. Every number here is computed from leaf-fact tables (the
/// <see cref="Kurx.Domain.Entities.ValueAllocationRecord"/> for revenue, per §9.5's "sole basis for
/// revenue reporting and refunds"; <c>Registration</c>/<c>Ticket</c>/<c>EventView</c>/<c>Refund</c> for
/// everything else) at request time — no daily pre-aggregation table exists or is needed. The aggregation
/// strategy is an internal implementation detail of the Infrastructure-side implementation (see
/// <c>Kurx.Infrastructure.Analytics.IAnalyticsFactSource</c>); this contract and its DTOs never change
/// because of it.</summary>
public interface IAnalyticsService
{
    Task<ServiceResult<OrgAnalyticsView>> GetOrgAnalyticsAsync(Guid userId, Guid orgId, bool includeDescendantUnits = false, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<SalesTimelineItem>>> GetEventSalesTimelineAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<AttendanceMetricView>> GetEventAttendanceAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<TicketTypeRevenueView>>> GetEventRevenueAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<TicketAnalyticsView>> GetEventTicketsAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<string>> ExportEventAnalyticsCsvAsync(Guid userId, Guid eventId, bool isAdmin = false, CancellationToken ct = default);
}
