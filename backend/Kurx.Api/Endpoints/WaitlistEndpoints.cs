using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>Ticket waitlist (D-064): join a sold-out ticket type, leave, list mine. Authenticated.</summary>
public static class WaitlistEndpoints
{
    public static void MapWaitlistEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/waitlist",
            async (Guid eventId, Guid ticketTypeId, ClaimsPrincipal p, IWaitlistService svc, CancellationToken ct) =>
            {
                var r = await svc.JoinAsync(UserId(p), eventId, ticketTypeId, ct);
                return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
            }).RequireAuthorization().WithTags("waitlist").Produces<WaitlistResponse>();

        app.MapDelete("/v1/events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/waitlist",
            async (Guid eventId, Guid ticketTypeId, ClaimsPrincipal p, IWaitlistService svc, CancellationToken ct) =>
            {
                await svc.LeaveAsync(UserId(p), eventId, ticketTypeId, ct);
                return Results.Ok(OperationAck.Success);
            }).RequireAuthorization().WithTags("waitlist").Produces<OperationAck>();

        app.MapGet("/v1/me/waitlist", async (ClaimsPrincipal p, IWaitlistService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListMineAsync(UserId(p), ct)).Select(ToJson)))
            .RequireAuthorization().WithTags("me").Produces<IReadOnlyList<WaitlistResponse>>();
    }

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "tickets_available" or "already_waitlisted" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static WaitlistResponse ToJson(WaitlistView w) => new(
        w.Id,
        w.EventId,
        w.TicketTypeId,
        w.Position,
        w.Status.ToLowerInvariant(),
        w.OfferExpiresAt,
        w.CreatedAt);

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
