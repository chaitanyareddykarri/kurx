using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record SessionBody(string Title, string? Description, string? Kind, DateTime StartsAt, DateTime EndsAt, int? Sort);
public record ReorderSessionsBody(IReadOnlyList<Guid> SessionIds);

public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this WebApplication app)
    {
        var sessions = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/sessions").WithTags("schedule").RequireAuthorization();

        sessions.MapPost("/", async (Guid orgId, Guid eventId, SessionBody body, ClaimsPrincipal principal, IScheduleService svc, CancellationToken ct) =>
        {
            var input = new SessionInput(body.Title, body.Description, body.Kind, body.StartsAt, body.EndsAt, body.Sort);
            var result = await svc.CreateAsync(UserId(principal), eventId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(ToJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<SessionBody>().Produces<SessionResponse>();

        sessions.MapGet("/", async (Guid orgId, Guid eventId, IScheduleService svc, CancellationToken ct)
            => Results.Ok((await svc.ListForEventAsync(eventId, ct)).Select(ToJson))).Produces<IReadOnlyList<SessionResponse>>();

        sessions.MapPatch("/{sessionId:guid}", async (Guid orgId, Guid eventId, Guid sessionId, SessionBody body, ClaimsPrincipal principal, IScheduleService svc, CancellationToken ct) =>
        {
            var input = new SessionInput(body.Title, body.Description, body.Kind, body.StartsAt, body.EndsAt, body.Sort);
            var result = await svc.UpdateAsync(UserId(principal), sessionId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(ToJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<SessionBody>().Produces<SessionResponse>();

        sessions.MapDelete("/{sessionId:guid}", async (Guid orgId, Guid eventId, Guid sessionId, ClaimsPrincipal principal, IScheduleService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(UserId(principal), sessionId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        sessions.MapPost("/reorder", async (Guid orgId, Guid eventId, ReorderSessionsBody body, ClaimsPrincipal principal, IScheduleService svc, CancellationToken ct) =>
        {
            var result = await svc.ReorderAsync(UserId(principal), eventId, IsAdmin(principal), body.SessionIds, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static SessionResponse ToJson(SessionView s) => new(
        s.Id,
        s.EventId,
        s.Title,
        s.Description,
        s.Kind.ToLowerInvariant(),
        s.StartsAt,
        s.EndsAt,
        s.Sort,
        s.SpeakerIds);
}
