using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public record ScanBody(Guid TicketCode, string? DeviceInfo);

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
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
