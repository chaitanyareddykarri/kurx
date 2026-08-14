using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public record InventoryPolicyBody(int? OversellAllowance, string? NoShowPolicy, int? NoShowReleaseMinutes,
    string? ReleasePolicyJson, string? WaitlistConfigJson);

/// <summary>V3 §8 (Phase 7) — inventory pools. Read the event's pools and set a pool's policy config
/// (oversell / no-show / release / waitlist). Authz reuses the Phase-6 event-permission union
/// (`event:manage`). The pools themselves are minted/dual-written by the ticketing services.</summary>
public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/orgs/{orgId:guid}/events/{eventId:guid}/inventory",
            async (Guid orgId, Guid eventId, ClaimsPrincipal p, IInventoryService svc, CancellationToken ct) =>
            {
                var r = await svc.GetForEventAsync(UserId(p), eventId, IsAdmin(p), ct);
                return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
            }).WithTags("inventory").RequireAuthorization().Produces<IReadOnlyList<InventoryPoolView>>();

        app.MapPatch("/v1/orgs/{orgId:guid}/events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/inventory",
            async (Guid orgId, Guid eventId, Guid ticketTypeId, InventoryPolicyBody body, ClaimsPrincipal p, IInventoryService svc, CancellationToken ct) =>
            {
                var input = new InventoryPolicyInput(body.OversellAllowance, body.NoShowPolicy, body.NoShowReleaseMinutes,
                    body.ReleasePolicyJson, body.WaitlistConfigJson);
                var r = await svc.SetPolicyAsync(UserId(p), eventId, ticketTypeId, IsAdmin(p), input, ct);
                return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
            }).WithTags("inventory").RequireAuthorization().Produces<InventoryPoolView>();
    }


    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
