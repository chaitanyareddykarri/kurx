using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>
/// What happened to an event's certificates (D-344, Phase 11).
///
/// <para>The two surfaces sit behind different permissions on purpose. The dashboard is counts, and needs
/// <c>ViewAnalytics</c>. The export carries participant names and email addresses — it is the personal
/// data, not a summary of it — and needs <c>ManageContent</c>.</para>
/// </summary>
public static class CertificateAnalyticsEndpoints
{
    public static void MapCertificateAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1").RequireAuthorization().WithTags("certificate-analytics");

        group.MapGet("/events/{eventId:guid}/certificate-dashboard", async (
            Guid eventId, ClaimsPrincipal principal,
            ICertificateAnalyticsService analytics, CancellationToken ct) =>
        {
            var result = await analytics.DashboardAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Ok(result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("Certificate counts and verification activity for an event")
        .Produces<CertificateDashboard>();

        group.MapGet("/events/{eventId:guid}/certificates/export", async (
            Guid eventId, Guid? batchId, ClaimsPrincipal principal,
            ICertificateAnalyticsService analytics, CancellationToken ct) =>
        {
            var result = await analytics.ExportAsync(
                UserId(principal), eventId, batchId, IsAdmin(principal), ct);

            if (!result.Ok) return ProblemResults.Problem(result.Error!, StatusFor(result.Error!));

            // Served as a file rather than JSON: this is the organiser's own record, and the thing they do
            // with it is open it in a spreadsheet.
            return Results.File(result.Value!.Content, "text/csv", result.Value.FileName);
        })
        .RequireRateLimiting("heavy")
        .WithSummary("Download the issued-certificate record as CSV");
    }

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static int StatusFor(string error) => error switch
    {
        "not_found" => StatusCodes.Status404NotFound,
        "forbidden" => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };
}
