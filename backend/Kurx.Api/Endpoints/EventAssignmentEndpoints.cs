using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record AssignBody(string Phone, string Role, string? CustomRole, string? Notes);

/// <summary>Event staff assignments (D-064). Owner/Manager assign; the invitee accepts/declines their own.</summary>
public static class EventAssignmentEndpoints
{
    public static void MapEventAssignmentEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/orgs/{orgId:guid}/events/{eventId:guid}/assignments", async (Guid orgId, Guid eventId,
            AssignBody body, ClaimsPrincipal p, IEventAssignmentService svc, CancellationToken ct) =>
        {
            var r = await svc.AssignAsync(UserId(p), orgId, eventId, body.Phone ?? "", body.Role ?? "", body.CustomRole, body.Notes, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("assignments").Produces<AssignmentResponse>();

        app.MapGet("/v1/orgs/{orgId:guid}/events/{eventId:guid}/assignments", async (Guid orgId, Guid eventId,
            ClaimsPrincipal p, IEventAssignmentService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForEventAsync(UserId(p), orgId, eventId, ct);
            return r.Ok ? Results.Ok(r.Value!.Select(ToJson)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("assignments").Produces<IReadOnlyList<AssignmentResponse>>();

        app.MapDelete("/v1/orgs/{orgId:guid}/events/{eventId:guid}/assignments/{id:guid}", async (Guid orgId, Guid eventId,
            Guid id, ClaimsPrincipal p, IEventAssignmentService svc, CancellationToken ct) =>
        {
            var r = await svc.RemoveAsync(UserId(p), orgId, eventId, id, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
        }).RequireAuthorization().WithTags("assignments").Produces<OperationAck>();

        app.MapPost("/v1/assignments/{id:guid}/accept", async (Guid id, ClaimsPrincipal p, IEventAssignmentService svc, CancellationToken ct) =>
        {
            var r = await svc.RespondAsync(UserId(p), id, accept: true, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("assignments").Produces<AssignmentResponse>();

        app.MapPost("/v1/assignments/{id:guid}/decline", async (Guid id, ClaimsPrincipal p, IEventAssignmentService svc, CancellationToken ct) =>
        {
            var r = await svc.RespondAsync(UserId(p), id, accept: false, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("assignments").Produces<AssignmentResponse>();

        app.MapGet("/v1/me/assignments", async (ClaimsPrincipal p, IEventAssignmentService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListMineAsync(UserId(p), ct)).Select(ToJson)))
            .RequireAuthorization().WithTags("me").Produces<IReadOnlyList<AssignmentResponse>>();
    }

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" or "user_not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "not_pending" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static AssignmentResponse ToJson(AssignmentView a) => new(
        a.Id,
        a.EventId,
        a.OrgId,
        a.UserId,
        a.Role,
        a.CustomRole,
        a.Status.ToLowerInvariant(),
        a.ShowOnProfile,
        a.Notes,
        a.CreatedAt,
        a.AssigneeName,
        a.AssigneeUsername,
        a.AssigneeAvatarKey,
        a.EventTitle,
        a.EventSlug,
        a.EventStartsAt,
        a.RepresentingOrgName);

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
