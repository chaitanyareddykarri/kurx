using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public record ScanBody(Guid TicketCode, string? DeviceInfo);

/// <param name="Pass">The raw scanned payload from a staff badge, <c>staff:{assignmentId}:{signature}</c>.</param>
public record StaffScanBody(string Pass, string? DeviceInfo);

public static class GateEndpoints
{
    public static void MapGateEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/gate/{eventId:guid}/scan",
            async (Guid eventId, ScanBody body, ClaimsPrincipal principal, IGateEntryService svc, CancellationToken ct) =>
            {
                var result = await svc.ScanAsync(UserId(principal), eventId, body.TicketCode, body.DeviceInfo, ct);
                if (result.IsDuplicate)
                    return Results.Ok(new
                    {
                        admitted = false,
                        is_duplicate = true,
                        first_checked_in_at = result.FirstCheckedInAt,
                        first_checked_in_by = result.FirstCheckedInByName,
                        message = $"Already checked in at {result.FirstCheckedInAt:t} by {result.FirstCheckedInByName}",
                    });
                if (!result.Admitted)
                {
                    var statusCode = result.RejectionReason == "forbidden"
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status400BadRequest;
                    return ProblemResults.Problem(result.RejectionReason, statusCode);
                }
                // V3 §4.4: the admission succeeds even when flagged — the organiser decides on eligibility_flag.
                return Results.Ok(new { admitted = true, is_duplicate = false, eligibility_flag = result.EligibilityFlag });
            }).WithTags("gate").RequireAuthorization();

        // A separate route rather than a polymorphic payload on /scan (D-385). The two credentials are
        // structurally different — a bare Guid against `staff:{id}:{sig}` — and giving each its own route
        // means a scanner never has to guess which it holds and neither can be resolved as the other.
        app.MapPost("/v1/gate/{eventId:guid}/scan-staff",
            async (Guid eventId, StaffScanBody body, ClaimsPrincipal principal, IGateEntryService svc, CancellationToken ct) =>
            {
                var result = await svc.ScanStaffAsync(UserId(principal), eventId, body.Pass ?? "", body.DeviceInfo, ct);

                // A repeat scan is reported, not refused: staff come and go all day, and the marshal still
                // needs the name and access level on screen.
                if (result.IsDuplicate)
                    return Results.Ok(new
                    {
                        admitted = false,
                        is_duplicate = true,
                        name = result.Name,
                        role = result.Role,
                        access_level = result.AccessLevel,
                        first_scanned_at = result.FirstScannedAt,
                        first_scanned_by = result.FirstScannedByName,
                        message = $"Already scanned in at {result.FirstScannedAt:t} by {result.FirstScannedByName}",
                    });

                if (!result.Admitted)
                    return ProblemResults.Problem(result.Reason,
                        result.Reason == "forbidden" ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest);

                return Results.Ok(new
                {
                    admitted = true,
                    is_duplicate = false,
                    name = result.Name,
                    role = result.Role,
                    access_level = result.AccessLevel,
                });
            }).WithTags("gate").RequireAuthorization()
            .WithSummary("Admit a staff member from the signed pass on their badge");
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
