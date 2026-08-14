using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public record AssignParticipantBody(string RoleSlug, string? Phone, Guid? OrgUnitId, string? CustomLabel,
    string? Visibility, bool WholeEvent = true, IReadOnlyList<Guid>? SubEvents = null);

public record RespondParticipantBody(bool Accept);

/// <summary>V3 §5 (Phase 6) — participants. The platform role registry, event participant CRUD, and the
/// current user's participations. Management authz is the §5.4 union (org grant OR an event ORGANISER
/// participant grant), resolved in the service.</summary>
public static class ParticipantEndpoints
{
    public static void MapParticipantEndpoints(this WebApplication app)
    {
        // The platform participant-role registry (public — like /v1/kinds and /v1/capabilities).
        app.MapGet("/v1/participant-roles", async (IParticipantService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListRolesAsync(ct)))).WithTags("participants").Produces<IReadOnlyList<ParticipantRoleView>>();

        var group = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/participants").WithTags("participants").RequireAuthorization();

        group.MapPost("/", async (Guid orgId, Guid eventId, AssignParticipantBody body, ClaimsPrincipal p, IParticipantService svc, CancellationToken ct) =>
        {
            var input = new AssignParticipantInput(body.RoleSlug, body.Phone, body.OrgUnitId, body.CustomLabel,
                body.Visibility, body.WholeEvent, body.SubEvents);
            var r = await svc.AssignAsync(UserId(p), orgId, eventId, input, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<ParticipantView>();

        group.MapGet("/", async (Guid orgId, Guid eventId, ClaimsPrincipal p, IParticipantService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForEventAsync(UserId(p), eventId, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<IReadOnlyList<ParticipantView>>();

        group.MapDelete("/{participantId:guid}", async (Guid orgId, Guid eventId, Guid participantId, ClaimsPrincipal p, IParticipantService svc, CancellationToken ct) =>
        {
            var r = await svc.RemoveAsync(UserId(p), eventId, participantId, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Fail(r.Error);
        }).Produces<OperationAck>();

        // The invited subject responds to their own participation (no org scope — they may not be a member).
        app.MapPost("/v1/participants/{participantId:guid}/respond",
            async (Guid participantId, RespondParticipantBody body, ClaimsPrincipal p, IParticipantService svc, CancellationToken ct) =>
            {
                var r = await svc.RespondAsync(UserId(p), participantId, body.Accept, ct);
                return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
            }).WithTags("participants").RequireAuthorization().Produces<ParticipantView>();

        app.MapGet("/v1/me/participations", async (ClaimsPrincipal p, IParticipantService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListMineAsync(UserId(p), ct)))).WithTags("participants").RequireAuthorization().Produces<IReadOnlyList<ParticipantView>>();
    }



    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
