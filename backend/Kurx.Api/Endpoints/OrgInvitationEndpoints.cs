using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record InviteMemberBody(string Phone, string Role);

/// <summary>Org invitations (D-064): invite a teammate to an org; they accept a token to join. Owner/Manager
/// manage; the invitee accepts/declines. Distinct from event/competition invitations.</summary>
public static class OrgInvitationEndpoints
{
    public static void MapOrgInvitationEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/orgs/{orgId:guid}/invitations", async (Guid orgId, InviteMemberBody body,
            ClaimsPrincipal p, IOrgInvitationService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Phone)) return ProblemResults.Problem("phone_required", StatusCodes.Status400BadRequest);
            if (!Enum.TryParse<OrgRole>(body.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
                return ProblemResults.Problem("invalid_role", StatusCodes.Status400BadRequest);
            var r = await svc.InviteAsync(UserId(p), orgId, body.Phone, role, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("org-invitations").Produces<OrgInvitationResponse>();

        app.MapGet("/v1/orgs/{orgId:guid}/invitations", async (Guid orgId, ClaimsPrincipal p, IOrgInvitationService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForOrgAsync(UserId(p), orgId, ct);
            return r.Ok ? Results.Ok(r.Value!.Select(ToJson)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("org-invitations").Produces<IReadOnlyList<OrgInvitationResponse>>();

        app.MapDelete("/v1/orgs/{orgId:guid}/invitations/{id:guid}", async (Guid orgId, Guid id, ClaimsPrincipal p, IOrgInvitationService svc, CancellationToken ct) =>
        {
            var r = await svc.CancelAsync(UserId(p), orgId, id, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
        }).RequireAuthorization().WithTags("org-invitations").Produces<OperationAck>();

        app.MapGet("/v1/me/org-invitations", async (ClaimsPrincipal p, IOrgInvitationService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListMineAsync(UserId(p), ct)).Select(ToJson)))
            .RequireAuthorization().WithTags("me").Produces<IReadOnlyList<OrgInvitationResponse>>();

        app.MapPost("/v1/org-invitations/{token}/accept", async (string token, ClaimsPrincipal p, IOrgInvitationService svc, CancellationToken ct) =>
        {
            var r = await svc.AcceptAsync(UserId(p), token, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("org-invitations").Produces<OrgInvitationResponse>();

        app.MapPost("/v1/org-invitations/{token}/decline", async (string token, ClaimsPrincipal p, IOrgInvitationService svc, CancellationToken ct) =>
        {
            var r = await svc.DeclineAsync(UserId(p), token, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
        }).RequireAuthorization().WithTags("org-invitations").Produces<OperationAck>();
    }

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "already_member" or "already_invited" or "not_pending" or "expired" or "not_your_invitation"
            => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static OrgInvitationResponse ToJson(OrgInvitationView i) => new(
        i.Id,
        i.OrgId,
        i.OrgName,
        i.InvitedPhone,
        i.Role.ToLowerInvariant(),
        i.Status.ToLowerInvariant(),
        i.ExpiresAt,
        i.CreatedAt);

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
