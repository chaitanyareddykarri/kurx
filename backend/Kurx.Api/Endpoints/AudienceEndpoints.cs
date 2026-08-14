using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public record AudienceRuleBody(
    IReadOnlyList<Guid>? UnitSubtreeIn,
    IReadOnlyList<string>? RoleIn,
    IReadOnlyList<int>? CohortYearIn,
    IReadOnlyDictionary<string, string>? AttributeMatches,
    bool RequireVerified = false,
    bool ExternalOrgsAllowed = false,
    string? AppliesTo = null,
    bool GuestsAllowed = false,
    int GuestPerRegistrantCap = 0,
    bool GuestsRequireApproval = false);

public record MemberAttributesBody(int? CohortYear, IReadOnlyDictionary<string, string>? Attributes, string? Source);

/// <summary>V3 §4.4 (Phase 5) — audience rules + member attributes + the current user's eligibility check.
/// The registration gate itself lives in <c>OrderService</c>; these endpoints configure and read it.</summary>
public static class AudienceEndpoints
{
    public static void MapAudienceEndpoints(this WebApplication app)
    {
        var rules = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/audience").WithTags("audience").RequireAuthorization();

        rules.MapGet("/", async (Guid orgId, Guid eventId, ClaimsPrincipal p, IAudienceService svc, CancellationToken ct) =>
        {
            var r = await svc.GetRuleAsync(UserId(p), orgId, eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value is null ? new { open = true } : r.Value) : Fail(r.Error);
        }).Produces<AudienceRuleView>();

        rules.MapPut("/", async (Guid orgId, Guid eventId, AudienceRuleBody body, ClaimsPrincipal p, IAudienceService svc, CancellationToken ct) =>
        {
            var input = new AudienceRuleInput(body.UnitSubtreeIn, body.RoleIn, body.CohortYearIn, body.AttributeMatches,
                body.RequireVerified, body.ExternalOrgsAllowed, body.AppliesTo, body.GuestsAllowed,
                body.GuestPerRegistrantCap, body.GuestsRequireApproval);
            var r = await svc.SetRuleAsync(UserId(p), orgId, eventId, IsAdmin(p), input, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<AudienceRuleView>();

        rules.MapDelete("/", async (Guid orgId, Guid eventId, ClaimsPrincipal p, IAudienceService svc, CancellationToken ct) =>
        {
            var r = await svc.DeleteRuleAsync(UserId(p), orgId, eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
        }).Produces<OperationAck>();

        app.MapPatch("/v1/orgs/{orgId:guid}/members/{membershipId:guid}/attributes",
            async (Guid orgId, Guid membershipId, MemberAttributesBody body, ClaimsPrincipal p, IAudienceService svc, CancellationToken ct) =>
            {
                var r = await svc.SetMemberAttributesAsync(UserId(p), orgId, membershipId, IsAdmin(p),
                    new MemberAttributesInput(body.CohortYear, body.Attributes, body.Source), ct);
                return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
            }).WithTags("audience").RequireAuthorization().Produces<OperationAck>();

        app.MapGet("/v1/events/{eventId:guid}/eligibility",
            async (Guid eventId, ClaimsPrincipal p, IAudienceService svc, CancellationToken ct) =>
            {
                var e = await svc.EvaluateAsync(UserId(p), eventId, ct);
                return Results.Ok(e);
            }).WithTags("audience").RequireAuthorization().Produces<EligibilityResult>();
    }


    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" or "not_eligible" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
