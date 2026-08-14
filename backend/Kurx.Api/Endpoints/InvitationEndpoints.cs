using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record AddInvitationBody(string? Name, string? Email, string? Phone, string Channel, string? Username = null, Guid? GroupId = null);
public record SendInvitationsBody(Guid[]? InvitationIds);
public record RsvpBody(string Response);

/// <summary>D-266 M6 (D9 Method B). <c>MaxSeats</c> null = unlimited; <c>Passcode</c> is hashed on arrival
/// and never stored or returned in the clear.</summary>
public record CreateInviteLinkBody(int? MaxSeats, bool? SingleUse, DateTime? ExpiresAt, string? Passcode);
public record RedeemInviteLinkBody(string? Passcode);

public static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this WebApplication app)
    {
        // ── Host endpoints ─────────────────────────────────────────────────────

        app.MapPost("/v1/events/{eventId:guid}/invitations",
            async (Guid eventId, AddInvitationBody body, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.AddAsync(UserId(principal), eventId, body.Name, body.Email, body.Phone, body.Channel,
                    body.Username, body.GroupId, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().Produces<InvitationView>();

        app.MapPost("/v1/events/{eventId:guid}/invitations/import",
            async (Guid eventId, HttpRequest req, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
            {
                if (!req.HasFormContentType) return Results.BadRequest(new { error = "multipart_required" });
                var file = req.Form.Files.GetFile("file");
                if (file is null) return Results.BadRequest(new { error = "file_required" });
                var result = await svc.ImportCsvAsync(UserId(principal), eventId, file.OpenReadStream(), ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().RequireRateLimiting("heavy").Produces<InvitationImportResult>();

        app.MapPost("/v1/events/{eventId:guid}/invitations/send",
            async (Guid eventId, SendInvitationsBody body, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.EnqueueSendAsync(UserId(principal), eventId, body.InvitationIds, ct);
                return result.Ok ? Results.Ok(new { queued = result.Value }) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().RequireRateLimiting("heavy");

        app.MapPost("/v1/invitations/{invitationId:guid}/resend",
            async (Guid invitationId, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.ResendAsync(UserId(principal), invitationId, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().Produces<InvitationView>();

        app.MapDelete("/v1/invitations/{invitationId:guid}",
            async (Guid invitationId, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.RevokeAsync(UserId(principal), invitationId, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().Produces<OperationAck>();

        app.MapGet("/v1/events/{eventId:guid}/invitations",
            async (Guid eventId, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct,
                string? status = null, string? rsvp = null, string? search = null, int page = 1, int pageSize = 50) =>
            {
                var result = await svc.ListAsync(UserId(principal), eventId, status, rsvp, search, page, pageSize, ct);
                if (!result.Ok) return Fail(result.Error);
                var (items, total, funnel) = result.Value;
                return Results.Ok(new { items, total, funnel });
            }).WithTags("invitations").RequireAuthorization();

        // ── D-266 M6 (D9 Method A) · the invitee's own surface ─────────────────
        // `Invite Only` is ONE policy with two delivery methods; these are Method A's half. Accepting
        // grants PERMISSION TO REGISTER and nothing else — the order still runs and payment still applies.

        app.MapGet("/v1/me/invitations",
            async (bool? pendingOnly, int? page, int? pageSize, ClaimsPrincipal principal,
                   IInvitationService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListMineAsync(UserId(principal), pendingOnly ?? true,
                    page ?? 1, Math.Clamp(pageSize ?? 20, 1, 100), ct)))
            .WithTags("invitations").RequireAuthorization().Produces<IReadOnlyList<MyInvitationView>>();

        foreach (var (verb, accept) in new[] { ("accept", true), ("decline", false) })
            app.MapPost($"/v1/invitations/{{invitationId:guid}}/{verb}",
                async (Guid invitationId, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
                {
                    var result = await svc.RespondAsync(UserId(principal), invitationId, accept, ct);
                    return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
                }).WithTags("invitations").RequireAuthorization().Produces<MyInvitationView>();

        // Username lookup for the invite picker. Deliberately an authenticated alias over the SAME
        // SearchUsersAsync the public profile search uses — a second user index would be a second answer to
        // "who is on Kurx", and it would be the one that forgot profile visibility.
        app.MapGet("/v1/users/search",
            async (string? q, int? page, int? pageSize, IPublicProfileService svc, CancellationToken ct) =>
                Results.Ok((await svc.SearchUsersAsync(q ?? "", page ?? 1, Math.Clamp(pageSize ?? 20, 1, 50), ct))
                    .Select(r => new { id = r.Id, name = r.Name, username = r.Username, avatar_key = r.AvatarKey })))
            .WithTags("invitations").RequireAuthorization();

        // ── D-266 M6 (D9 Method B) · invite links ──────────────────────────────

        app.MapPost("/v1/events/{eventId:guid}/invite-links",
            async (Guid eventId, CreateInviteLinkBody body, ClaimsPrincipal principal,
                   IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.CreateLinkAsync(UserId(principal), eventId, IsAdmin(principal),
                    new CreateInviteLinkInput(body.MaxSeats, body.SingleUse ?? false, body.ExpiresAt, body.Passcode), ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().Produces<InviteLinkView>();

        app.MapGet("/v1/events/{eventId:guid}/invite-links",
            async (Guid eventId, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.ListLinksAsync(UserId(principal), eventId, IsAdmin(principal), ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().Produces<IReadOnlyList<InviteLinkView>>();

        app.MapDelete("/v1/invite-links/{linkId:guid}",
            async (Guid linkId, ClaimsPrincipal principal, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.RevokeLinkAsync(UserId(principal), linkId, IsAdmin(principal), ct);
                return result.Ok ? Results.Ok(new RevokedAck(true)) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().Produces<RevokedAck>();

        // Authenticated: a seat is claimed FOR someone, and an anonymous claim could not be made
        // idempotent per person (D9 rule 7) nor matched to an invitation at registration.
        app.MapPost("/v1/invite-links/{token}/redeem",
            async (string token, RedeemInviteLinkBody? body, ClaimsPrincipal principal,
                   IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.RedeemLinkAsync(UserId(principal), token, body?.Passcode, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("invitations").RequireAuthorization().Produces<InviteLinkView>();

        // ── Public endpoints ───────────────────────────────────────────────────

        // Pre-flight before sign-in: is this link usable, and will it ask for a passcode? Anonymous by
        // necessity — the holder may not have an account yet, which is the case Method B exists for.
        app.MapGet("/v1/public/invite-links/{token}",
            async (string token, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.GetPublicLinkAsync(token, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("invitations").AllowAnonymous().Produces<PublicInviteLinkView>();

        app.MapGet("/v1/public/invitations/{token}",
            async (string token, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.GetPublicAsync(token, ct);
                if (!result.Ok)
                    return result.Error == "event_ended"
                        ? ProblemResults.Problem(result.Error, StatusCodes.Status410Gone)
                        : ProblemResults.Problem(result.Error, StatusCodes.Status404NotFound);
                return Results.Ok(result.Value);
            }).WithTags("invitations").AllowAnonymous().Produces<PublicInvitationView>();

        app.MapPost("/v1/public/invitations/{token}/rsvp",
            async (string token, RsvpBody body, IInvitationService svc, CancellationToken ct) =>
            {
                var result = await svc.RsvpAsync(token, body.Response, ct);
                if (!result.Ok) return Fail(result.Error);
                return Results.Ok(new { registration_url = result.Value });
            }).WithTags("invitations").AllowAnonymous();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "duplicate_email" or "duplicate_phone" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        "max_sends_reached" or "resend_too_soon" or "rate_limited" => ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        "revoked" => ProblemResults.Problem(error, StatusCodes.Status410Gone),
        "username_not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        // D-266 M6. `not_invited` is 404 rather than 403 on purpose: confirming that an invitation exists
        // to someone who was not invited discloses the guest list one probe at a time (D-018).
        "not_invited" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "duplicate_invitation" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        // The link is well-formed and the caller is entitled to try; the link's own state refuses. Same
        // shape as the review lock — a 400 would tell a client to fix a payload that has nothing wrong.
        "invitation_already_responded" or "invite_link_revoked" or "invite_link_expired"
            or "invite_link_exhausted" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        "invite_link_passcode_required" or "invite_link_passcode_invalid"
            => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static bool IsAdmin(ClaimsPrincipal principal)
        => principal.HasClaim("kurx_admin", "true");
}
