using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>
/// Event badge printing, for the people who run the event (D-362).
///
/// <para><b>There is deliberately no holder-facing route here.</b> No <c>/v1/me/badges</c>, no
/// self-service download. Badges are pre-printed onto lanyards by the organizer and handed out; a holder
/// never fetches their own. Adding a "my badge" route would be a product change, not a convenience — see
/// <see cref="IIdCardService"/>.</para>
///
/// <para>Every route resolves Manager authority live through <c>IEventAuthority</c> (D-015) inside the
/// service, and answers 404 rather than 403 to a caller with no standing on the event (D-018).</para>
/// </summary>
public static class IdCardEndpoints
{
    public static void MapIdCardEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/events/{eventId:guid}/badges")
            .WithTags("badges")
            .RequireAuthorization();

        // The sizes the organizer may choose from. Served rather than hardcoded in each client so the web
        // console and any later surface cannot drift from what the renderer actually supports.
        //
        // Authenticated only, with no per-event authority check — unlike every other route here. It reads
        // nothing, returns a fixed list of paper sizes, and reveals nothing about the event whose path it
        // sits under. It lives under that path so a client needs one base URL, not two.
        g.MapGet("/sizes", () => Results.Ok(
                BadgeSize.All.Select(s => new
                {
                    key = s.Key, label = s.Label,
                    width_mm = s.WidthMm, height_mm = s.HeightMm, landscape = s.IsLandscape,
                })))
            .WithSummary("Badge sizes available for printing");

        g.MapGet("/recipients", async (Guid eventId, ClaimsPrincipal p, IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.ListRecipientsAsync(eventId, UserId(p), IsAdmin(p), ct);
            return r.Ok
                ? Results.Ok(r.Value!.Select(x => new
                {
                    user_id = x.UserId, name = x.Name,
                    kind = x.Kind.ToString().ToLowerInvariant(),
                    subtitle = x.Subtitle, access_level = x.AccessLevel,
                    has_photo = !string.IsNullOrWhiteSpace(x.PhotoKey),
                    // The QR payload itself is NOT returned. It is a credential: a marshal's console
                    // listing everyone's scannable code would hand out working badges as JSON.
                }))
                : Fail(r.Error);
        }).WithSummary("Everyone at this event who can be given a badge");

        g.MapPost("/sheet", async (
            Guid eventId, BadgeSheetBody body, ClaimsPrincipal p, IIdCardService svc, CancellationToken ct) =>
        {
            var kinds = ParseKinds(body.Kinds);
            var r = await svc.RenderSheetAsync(
                eventId, UserId(p), IsAdmin(p), new BadgeSheetRequest(body.SizeKey, kinds, body.UserIds), ct);

            return r.Ok
                ? Results.File(r.Value!, "application/pdf", $"badges-{eventId}.pdf")
                : Fail(r.Error);
        })
            .WithSummary("Print-ready sheet of badges, laid out N-up on A4 with cut guides")
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "application/pdf");

        g.MapGet("/{recipientUserId:guid}.pdf", async (
            Guid eventId, Guid recipientUserId, string? size, ClaimsPrincipal p, IIdCardService svc,
            CancellationToken ct) =>
        {
            var r = await svc.RenderOneAsync(
                eventId, UserId(p), IsAdmin(p), recipientUserId, size ?? BadgeSize.Lanyard.Key, ct);

            return r.Ok
                ? Results.File(r.Value!, "application/pdf", $"badge-{recipientUserId}.pdf")
                : Fail(r.Error);
        })
            .WithSummary("One badge, print-ready")
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "application/pdf");
    }

    /// <param name="Kinds">`attendee`, `staff`, or both. Absent means both.</param>
    /// <param name="UserIds">Absent or empty prints everyone matching <paramref name="Kinds"/>.</param>
    public record BadgeSheetBody(string SizeKey, string[]? Kinds, Guid[]? UserIds);

    private static IReadOnlyList<BadgeKind> ParseKinds(string[]? kinds)
    {
        if (kinds is null || kinds.Length == 0) return [BadgeKind.Attendee, BadgeKind.Staff];
        return kinds
            .Select(k => Enum.TryParse<BadgeKind>(k, ignoreCase: true, out var parsed) ? parsed : (BadgeKind?)null)
            .Where(k => k is not null)
            .Select(k => k!.Value)
            .Distinct()
            .ToList();
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
