using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

public record ApprovalChainBody(string? Name, string? Mode, IReadOnlyList<ApprovalStepInput>? Steps);
public record ApprovalDecisionBody(string Action, string? Note);

/// <summary>V3 §14.3 (Phase 14) — internal approval chains: configure a chain on an OrgUnit (inherited down the tree),
/// read an event's approval request, and approve/reject/bypass a step. The chain gates publishing (Publish→Scheduled,
/// and the existing publish) before platform review. Chain config is org Owner/Manager; a decision requires the step's
/// approver (role or user) or admin; bypass requires an org Owner or admin and is always audited. Additive; no existing
/// route or response shape changed.</summary>
public static class ApprovalEndpoints
{
    public static void MapApprovalEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1").WithTags("approvals").RequireAuthorization();

        g.MapPost("/org-units/{orgUnitId:guid}/approval-chains", async (Guid orgUnitId, ApprovalChainBody b, ClaimsPrincipal p, IApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateChainAsync(UserId(p), orgUnitId, IsAdmin(p), new ApprovalChainInput(b.Name, b.Mode, b.Steps), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ApprovalChainView>();

        g.MapGet("/org-units/{orgUnitId:guid}/approval-chains", async (Guid orgUnitId, ClaimsPrincipal p, IApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForOrgUnitAsync(UserId(p), orgUnitId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<ApprovalChainView>>();

        g.MapPatch("/approval-chains/{chainId:guid}", async (Guid chainId, ApprovalChainBody b, ClaimsPrincipal p, IApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.UpdateChainAsync(UserId(p), chainId, IsAdmin(p), new ApprovalChainInput(b.Name, b.Mode, b.Steps), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ApprovalChainView>();

        g.MapDelete("/approval-chains/{chainId:guid}", async (Guid chainId, ClaimsPrincipal p, IApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.DeleteChainAsync(UserId(p), chainId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapGet("/events/{eventId:guid}/approval", async (Guid eventId, ClaimsPrincipal p, IApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.GetRequestAsync(UserId(p), eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ApprovalRequestView>();

        g.MapPost("/approvals/decisions/{decisionId:guid}", async (Guid decisionId, ApprovalDecisionBody b, ClaimsPrincipal p, IApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.DecideAsync(UserId(p), decisionId, IsAdmin(p), b.Action, b.Note, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ApprovalRequestView>();

        g.MapPost("/events/{eventId:guid}/approval/resubmit", async (Guid eventId, ClaimsPrincipal p, IApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.ResubmitAsync(UserId(p), eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ApprovalRequestView>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
