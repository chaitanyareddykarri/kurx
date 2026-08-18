using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.IdCards;

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
                    //
                    // The issued card IS returned — its number and verify code are printed on the badge's
                    // face, so they are not secrets, and the console needs them to show what exists.
                    card = x.Card is null ? null : new
                    {
                        id = x.Card.Id, card_number = x.Card.CardNumber, verify_code = x.Card.VerifyCode,
                        status = x.Card.Status.ToLowerInvariant(), is_revoked = x.Card.IsRevoked,
                        generated_at = x.Card.GeneratedAt,
                    },
                }))
                : Fail(r.Error);
        }).WithSummary("Everyone at this event who can be given a badge");

        // ── The card design (D-362 editor) ──────────────────────────────────────────────────────
        //
        // One design per event, stored on DesignTemplate with Kind = IdCard. GET answers the shipped
        // default when nothing has been saved, so the editor always opens on a working card.

        // The built-in placements, so the editor opens on the layout the server would actually print
        // instead of on an empty card (D-385). `GET /template` answers `fields: null` for an event whose
        // design has never been saved — that means "use the built-in layout", and a client that renders it
        // as *no fields* both lies about the card and, on the first tick of a checkbox, sends a one-field
        // layout that replaces the whole default.
        //
        // Ungated for the same reason as `/sizes`: it reads nothing and reveals nothing about the event.
        g.MapGet("/template/defaults", (string? size, string? kind) =>
        {
            var badgeSize = BadgeSize.FromKey(size) ?? BadgeSize.Lanyard;
            var isStaff = string.Equals(kind, "staff", StringComparison.OrdinalIgnoreCase);
            return Results.Ok(BadgeLayout.Defaults(badgeSize, isStaff));
        }).WithSummary("The built-in card layout, as placements the editor can load and drag");

        g.MapGet("/template", async (Guid eventId, ClaimsPrincipal p, IIdCardTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(eventId, UserId(p), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).WithSummary("This event's ID card design");

        g.MapPut("/template", async (
            Guid eventId, IdCardTemplateSpec body, ClaimsPrincipal p, IIdCardTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.SaveAsync(eventId, UserId(p), IsAdmin(p), body, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).WithSummary("Save this event's ID card design");

        // Renders the spec in the request body, NOT the saved one — the editor previews unsaved edits,
        // and through the same renderer that prints, so preview and output cannot drift.
        g.MapPost("/template/preview", async (
            Guid eventId, TemplatePreviewBody body, ClaimsPrincipal p, IIdCardTemplateService svc,
            CancellationToken ct) =>
        {
            var kind = string.Equals(body.Kind, "staff", StringComparison.OrdinalIgnoreCase)
                ? BadgeKind.Staff : BadgeKind.Attendee;
            var r = await svc.PreviewAsync(eventId, UserId(p), IsAdmin(p), body.Spec, kind, ct);
            return r.Ok ? Results.File(r.Value!, "image/png") : Fail(r.Error);
        })
            .WithSummary("Render a sample card from an unsaved design")
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "image/png");

        g.MapPost("/template/asset/presign", async (
            Guid eventId, AssetPresignBody body, ClaimsPrincipal p, IIdCardTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.PresignAssetAsync(eventId, UserId(p), IsAdmin(p), body.ContentType, body.Purpose, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).WithSummary("Presigned upload for the card's artwork or logo");

        g.MapGet("/template/asset-url", async (
            Guid eventId, string key, ClaimsPrincipal p, IIdCardTemplateService svc, CancellationToken ct) =>
        {
            var r = await svc.AssetUrlAsync(eventId, UserId(p), IsAdmin(p), key, ct);
            return r.Ok ? Results.Ok(new { url = r.Value }) : Fail(r.Error);
        }).WithSummary("Readable URL for an uploaded card asset, for the editor canvas");

        g.MapPost("/generate", async (
            Guid eventId, BadgeSheetBody body, ClaimsPrincipal p, IIdCardService svc, CancellationToken ct) =>
        {
            var r = await svc.GenerateAsync(
                eventId, UserId(p), IsAdmin(p),
                new BadgeIssueRequest(body.SizeKey, ParseKinds(body.Kinds), body.UserIds), ct);

            return r.Ok
                ? Results.Ok(new { issued = r.Value!.Issued, regenerated = r.Value.Regenerated })
                : Fail(r.Error);
        })
            .WithSummary("Issue ID cards: create the id_cards rows, render and store their artefacts");

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

        // D-386. Marks the card document dead so the verification lookup stops reporting it current. It
        // does NOT close a door by itself — an attendee's entry credential is their ticket and a staff
        // member's is their assignment — and the summary says so, because the opposite assumption is the
        // dangerous one.
        g.MapPost("/{recipientUserId:guid}/revoke", async (
            Guid eventId, Guid recipientUserId, RevokeBadgeBody? body, ClaimsPrincipal p, IIdCardService svc,
            CancellationToken ct) =>
        {
            var r = await svc.RevokeAsync(eventId, UserId(p), IsAdmin(p), recipientUserId, body?.Reason, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        })
            .WithSummary("Revoke an issued badge — marks the card, does not void the ticket or assignment");

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

    /// <param name="Kind">`attendee` or `staff` — which sample card to draw. They differ: only a staff
    /// card carries an access band, so previewing one tells you nothing about the other.</param>
    public record TemplatePreviewBody(IdCardTemplateSpec Spec, string? Kind);

    /// <param name="Purpose">`background` or `logo` (default).</param>
    public record AssetPresignBody(string ContentType, string? Purpose);

    /// <param name="Reason">Optional. "Revoked" is the fact a verifier needs; the why is often
    /// operational and is not required to record the act.</param>
    public record RevokeBadgeBody(string? Reason);

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
