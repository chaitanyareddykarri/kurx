using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record SubmitClaimBody(string ClaimedRole, DateTime? ValidUntil, IReadOnlyList<VerificationDocInput>? Documents);
public record ReviewClaimBody(string Decision, string? ReasonCode, string? Notes);

/// <summary>Membership-affiliation claims (M6, D-045). Users submit/track their own claims; platform
/// VerificationReviewers work the queue. Reviewed only from the claim + its evidence, never profile bio.</summary>
public static class MembershipClaimEndpoints
{
    public static void MapMembershipClaimEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/orgs/{orgId:guid}/membership-claims", async (Guid orgId, SubmitClaimBody body,
            ClaimsPrincipal p, IMembershipVerificationService svc, CancellationToken ct) =>
        {
            var evidence = (body.Documents ?? [])
                .Select(d => new MembershipEvidence(d.DocType, d.StorageKey)).ToList();
            var r = await svc.SubmitAsync(UserId(p), orgId, body.ClaimedRole, body.ValidUntil, evidence, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("membership").Produces<MembershipClaimResponse>();

        app.MapGet("/v1/me/membership-claims", async (ClaimsPrincipal p, IMembershipVerificationService svc, CancellationToken ct)
            => Results.Ok((await svc.ListMineAsync(UserId(p), ct)).Select(ToJson)))
            .RequireAuthorization().WithTags("membership").Produces<IReadOnlyList<MembershipClaimResponse>>();

        var admin = app.MapGroup("/v1/admin/membership-claims").WithTags("admin").RequireAuthorization("VerificationReviewer");

        admin.MapGet("/pending", async (int? limit, IMembershipVerificationService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListPendingAsync(limit ?? 50, ct)).Select(c => new PendingClaimResponse(
                c.Id, c.UserId, c.UserName, c.Username, c.OrgId, c.OrgName,
                c.ClaimedRole.ToLowerInvariant(), c.Status.ToLowerInvariant(), c.FastTrack, c.CreatedAt)))).Produces<IReadOnlyList<PendingClaimResponse>>();

        admin.MapPost("/{claimId:guid}/review", async (Guid claimId, ReviewClaimBody body,
            ClaimsPrincipal p, IMembershipVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.ReviewAsync(UserId(p), claimId, body.Decision, body.ReasonCode, body.Notes, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).Produces<MembershipClaimResponse>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "already_claimed" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static MembershipClaimResponse ToJson(MembershipClaimView v) => new(
        v.Id,
        v.OrgId,
        v.OrgName,
        v.OrgSlug,
        v.ClaimedRole.ToLowerInvariant(),
        v.Status.ToLowerInvariant(),
        v.FastTrack,
        v.ValidUntil,
        v.ReviewedAt,
        v.Notes,
        v.CreatedAt);
}
