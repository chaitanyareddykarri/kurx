using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

/// <summary>
/// The scannable QR image for a ticket (D-343).
///
/// <para><b>Why this file exists.</b> This route used to live in <c>CertificateEndpoints</c>, which is an
/// accident of history rather than a grouping: a ticket QR has nothing to do with certificates, and it was
/// only there because both needed <c>IQrCodeGenerator</c>. Removing the certificate feature would have
/// taken this endpoint with it and broken the gate flow — the tickets page, the mobile wallet and
/// <c>TicketTransferTests</c> all fetch it — so it moves here rather than disappearing.</para>
///
/// <para>Authorization is deliberately not just "is this your ticket": event staff must be able to pull a
/// holder's QR at the door when a phone is dead or a screenshot will not scan.</para>
/// </summary>
public static class TicketQrEndpoints
{
    public static void MapTicketQrEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/tickets/{code:guid}/qr.png",
            async (Guid code, ClaimsPrincipal principal, KurxDbContext db, IQrCodeGenerator qr, CancellationToken ct) =>
            {
                var userId = UserId(principal);
                var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code, ct);
                if (ticket is null) return ProblemResults.Problem("not_found", StatusCodes.Status404NotFound);

                if (ticket.UserId != userId)
                {
                    var orgId = await db.Events.AsNoTracking()
                        .Where(e => e.Id == ticket.EventId).Select(e => (Guid?)e.RepresentingOrgId).FirstOrDefaultAsync(ct);
                    var isStaff = orgId is not null
                        && await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == userId, ct);
                    if (!isStaff) return ProblemResults.Problem("forbidden", StatusCodes.Status403Forbidden);
                }

                var png = qr.GeneratePng(ticket.Code.ToString());
                return Results.File(png, "image/png");
            })
            .RequireAuthorization()
            .WithTags("tickets")
            .WithSummary("Real, scannable QR image for a ticket")
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "image/png");
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
