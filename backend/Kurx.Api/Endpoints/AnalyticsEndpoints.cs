using System.Security.Claims;
using System.Text;
using Kurx.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var analytics = app.MapGroup("/v1/orgs/{orgId:guid}").WithTags("analytics").RequireAuthorization();

        // V3 §16 (Phase 17): includeDescendantUnits rolls the org's totals up across every OrgUnit under it
        // (the dual-tree rollup's org side) — additive, optional, omitting it keeps the prior behaviour.
        analytics.MapGet("/analytics", async (Guid orgId, bool? includeDescendantUnits, ClaimsPrincipal principal, IAnalyticsService svc, CancellationToken ct) =>
        {
            var result = await svc.GetOrgAnalyticsAsync(UserId(principal), orgId, includeDescendantUnits ?? false, ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).Produces<OrgAnalyticsView>();



        analytics.MapGet("/events/{eventId:guid}/analytics/sales", async (Guid orgId, Guid eventId, ClaimsPrincipal principal, IAnalyticsService svc, CancellationToken ct) =>
        {
            var result = await svc.GetEventSalesTimelineAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).Produces<IReadOnlyList<SalesTimelineItem>>();

        analytics.MapGet("/events/{eventId:guid}/analytics/attendance", async (Guid orgId, Guid eventId, ClaimsPrincipal principal, IAnalyticsService svc, CancellationToken ct) =>
        {
            var result = await svc.GetEventAttendanceAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).Produces<AttendanceMetricView>();

        analytics.MapGet("/events/{eventId:guid}/analytics/revenue", async (Guid orgId, Guid eventId, ClaimsPrincipal principal, IAnalyticsService svc, CancellationToken ct) =>
        {
            var result = await svc.GetEventRevenueAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).Produces<IReadOnlyList<TicketTypeRevenueView>>();

        analytics.MapGet("/events/{eventId:guid}/analytics/tickets", async (Guid orgId, Guid eventId, ClaimsPrincipal principal, IAnalyticsService svc, CancellationToken ct) =>
        {
            var result = await svc.GetEventTicketsAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
        }).Produces<TicketAnalyticsView>();

        analytics.MapGet("/events/{eventId:guid}/analytics/export", async (Guid orgId, Guid eventId, ClaimsPrincipal principal, IAnalyticsService svc, CancellationToken ct) =>
        {
            var result = await svc.ExportEventAnalyticsCsvAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            if (!result.Ok) return Fail(result.Error);
            return Results.File(Encoding.UTF8.GetBytes(result.Value!), "text/csv", $"event-{eventId}-analytics.csv");
        }).Produces(StatusCodes.Status200OK, typeof(byte[]), "text/csv");
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    /// <summary>Same claim every other admin-capable endpoint reads. Passed into the service so a
    /// platform admin resolves through IEventAuthority's admin path rather than needing an org seat.</summary>
    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
