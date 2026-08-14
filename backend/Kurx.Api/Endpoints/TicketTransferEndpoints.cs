using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public record InitiateTransferBody(string ToPhone);
public record ClaimTransferBody(string TransferCode);

public static class TicketTransferEndpoints
{
    public static void MapTicketTransferEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/tickets/{ticketId:guid}/transfer",
            async (Guid ticketId, InitiateTransferBody body, ClaimsPrincipal principal, ITicketTransferService svc, CancellationToken ct) =>
            {
                var result = await svc.InitiateAsync(UserId(principal), ticketId, body.ToPhone, ct);
                return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
            }).WithTags("transfers").RequireAuthorization().Produces<TicketTransferView>();

        app.MapPost("/v1/transfers/claim",
            async (ClaimTransferBody body, ClaimsPrincipal principal, ITicketTransferService svc, KurxDbContext db, CancellationToken ct) =>
            {
                var userId = UserId(principal);
                // Canonical column first (D-089). The legacy bare-digit column carries no country, so
                // re-normalizing it re-interprets it in the legacy region: a Singapore number stored as
                // "6591234567" is also a well-formed Indian mobile, so it would come back "916591234567"
                // and never match the transfer it was addressed to.
                var phone = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.PhoneE164 ?? u.Phone)
                    .FirstOrDefaultAsync(ct) ?? "";
                var result = await svc.ClaimAsync(userId, phone, body.TransferCode, ct);
                return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
            }).WithTags("transfers").RequireAuthorization().Produces<TicketTransferView>();

        app.MapDelete("/v1/transfers/{transferId:guid}",
            async (Guid transferId, ClaimsPrincipal principal, ITicketTransferService svc, CancellationToken ct) =>
            {
                var result = await svc.CancelAsync(UserId(principal), transferId, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("transfers").RequireAuthorization().Produces<OperationAck>();
    }


    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" or "phone_mismatch" or "not_eligible" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "transfers_disabled" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
