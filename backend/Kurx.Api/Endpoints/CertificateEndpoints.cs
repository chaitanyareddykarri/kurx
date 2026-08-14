using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

public static class CertificateEndpoints
{
    public static void MapCertificateEndpoints(this WebApplication app)
    {
        // Public certificate verification by the unique verify_code printed on the certificate.
        app.MapGet("/v1/certificates/{code}", async (string code, KurxDbContext db, IStorage storage, CancellationToken ct) =>
        {
            var cert = await db.Certificates.AsNoTracking()
                .Where(c => c.VerifyCode == code && c.IsPublic)
                .Join(db.Events.AsNoTracking(), c => c.EventId, e => e.Id,
                    (c, e) => new { Cert = c, Event = e })
                .Join(db.Organizations.AsNoTracking(), x => x.Event.RepresentingOrgId, o => o.Id,
                    (x, o) => new { x.Cert, x.Event, Org = o })
                .Join(db.Users.AsNoTracking(), x => x.Cert.UserId, u => u.Id,
                    (x, u) => new { x.Cert, x.Event, x.Org, User = u })
                .FirstOrDefaultAsync(ct);

            if (cert is null)
                return ProblemResults.Problem("not_found", StatusCodes.Status404NotFound);

            // Revoked certificates stay visible here (200, not 404) — verifying a revocation is the
            // whole point of the endpoint, unlike D-018's hide-drafts pattern (D-036).
            var pdfUrl = cert.Cert.PdfKey is null ? null : await storage.PresignGetAsync(cert.Cert.PdfKey, ct: ct);

            return Results.Ok(new
            {
                verify_code = cert.Cert.VerifyCode,
                kind = cert.Cert.Kind.ToString().ToLowerInvariant(),
                status = cert.Cert.Status.ToString().ToLowerInvariant(),
                is_revoked = cert.Cert.IsRevoked,
                revoked_reason = cert.Cert.RevokedReason,
                issued_to = cert.User.Name,
                event_title = cert.Event.Title,
                event_slug = cert.Event.Slug,
                organizer = cert.Org.Name,
                org_slug = cert.Org.Slug,
                issued_at = cert.Cert.CreatedAt,
                pdf_url = pdfUrl,
            });
        }).WithTags("certificates");

        // Owner/Manager/Admin: bulk-generate certificates for eligible tickets on an event (D-035).
        app.MapPost("/v1/events/{eventId:guid}/certificates/generate",
            async (Guid eventId, ClaimsPrincipal principal, ICertificateService svc, CancellationToken ct) =>
            {
                var result = await svc.GenerateForEventAsync(UserId(principal), eventId, IsAdmin(principal), ct);
                return result.Ok ? Results.Ok(new { generated = result.Value }) : Fail(result.Error);
            })
            .RequireAuthorization()
            .RequireRateLimiting("heavy")
            .WithTags("certificates")
            .WithSummary("Bulk-generate certificates for eligible tickets on an event");

        // Owner/Manager/Admin: the certificate roster for an event (D-064) — list who has one + revoke per event.
        app.MapGet("/v1/events/{eventId:guid}/certificates",
            async (Guid eventId, ClaimsPrincipal principal, ICertificateService svc, CancellationToken ct) =>
            {
                var result = await svc.ListForEventAsync(UserId(principal), eventId, IsAdmin(principal), ct);
                return result.Ok
                    ? Results.Ok(result.Value!.Select(c => new
                    {
                        id = c.Id, event_id = c.EventId, verify_code = c.VerifyCode, user_id = c.UserId,
                        holder_name = c.HolderName, kind = c.Kind.ToLowerInvariant(), status = c.Status.ToLowerInvariant(),
                        is_revoked = c.IsRevoked, revoked_reason = c.RevokedReason, issued_at = c.CreatedAt,
                    }))
                    : Fail(result.Error);
            })
            .RequireAuthorization()
            .WithTags("certificates")
            .WithSummary("Certificate roster for an event");

        // Owner/Manager/Admin: revoke a certificate (D-036) — visible-not-hidden on the verify endpoint.
        app.MapPost("/v1/certificates/{certificateId:guid}/revoke",
            async (Guid certificateId, RevokeCertificateBody body, ClaimsPrincipal principal, ICertificateService svc, CancellationToken ct) =>
            {
                var result = await svc.RevokeAsync(UserId(principal), certificateId, body.Reason, IsAdmin(principal), ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            })
            .RequireAuthorization()
            .WithTags("certificates")
            .WithSummary("Revoke a certificate").Produces<OperationAck>();

        // Authenticated user's own certificate list (D-058).
        app.MapGet("/v1/me/certificates",
            async (ClaimsPrincipal principal, KurxDbContext db, CancellationToken ct) =>
            {
                var userId = UserId(principal);
                var certs = await db.Certificates.AsNoTracking()
                    .Where(c => c.UserId == userId)
                    .Join(db.Events.AsNoTracking(), c => c.EventId, e => e.Id, (c, e) => new { c, e })
                    .OrderByDescending(x => x.c.CreatedAt)
                    .Select(x => new
                    {
                        id = x.c.Id,
                        code = x.c.VerifyCode,
                        event_id = x.c.EventId,
                        event_title = x.e.Title,
                        template_id = x.c.TemplateId,
                        issued_at = x.c.CreatedAt,
                        revoked_at = x.c.RevokedAt,
                        is_revoked = x.c.IsRevoked,
                        pdf_url = (string?)null,
                        verify_code = x.c.VerifyCode,
                    })
                    .ToListAsync(ct);
                return Results.Ok(certs);
            })
            .RequireAuthorization()
            .WithTags("certificates");

        // Real ticket QR image (replaces the decorative/mock QR the web and mobile clients render).
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
            .WithTags("certificates")
            .WithSummary("Real, scannable QR image for a ticket")
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "image/png");
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}

public record RevokeCertificateBody(string Reason);
