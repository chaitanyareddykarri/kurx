using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>
/// Direct messages (D-264).
///
/// <para>Only what is new lives here. Sending, history, attachments, presence and read pointers are
/// <c>/v1/chat/*</c> and are untouched — a DM room is a <c>ChatRoom</c>, so those endpoints already
/// work on it. Adding a parallel `/v1/dm/{room}/messages` would have duplicated every one of them.</para>
/// </summary>
public static class DmEndpoints
{
    public static void MapDmEndpoints(this WebApplication app)
    {
        // Idempotent by database constraint: a repeat returns the same room rather than making another.
        // Rate-limited because this is the abuse entry point — one account opening conversations with
        // strangers in bulk is exactly what the request gate and this limit exist to slow down.
        app.MapPost("/v1/dm/{userId:guid}",
            async (Guid userId, ClaimsPrincipal p, IDmService svc, CancellationToken ct) =>
            {
                var r = await svc.OpenAsync(UserId(p), userId, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("dm").RequireAuthorization().RequireRateLimiting("posts").Produces<ChatRoomView>();

        app.MapGet("/v1/me/dm",
            async (ClaimsPrincipal p, IDmService svc, CancellationToken ct, bool archived = false) =>
                Results.Ok(await svc.ListAsync(UserId(p), archived, ct)))
            .WithTags("dm").RequireAuthorization().Produces<List<DmRoomView>>();

        app.MapGet("/v1/me/dm/requests",
            async (ClaimsPrincipal p, IDmService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListRequestsAsync(UserId(p), ct)))
            .WithTags("dm").RequireAuthorization().Produces<List<DmRoomView>>();

        app.MapPost("/v1/dm/{roomId:guid}/requests/accept",
            async (Guid roomId, ClaimsPrincipal p, IDmService svc, CancellationToken ct) =>
            {
                var r = await svc.RespondToRequestAsync(roomId, UserId(p), accept: true, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("dm").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        app.MapPost("/v1/dm/{roomId:guid}/requests/decline",
            async (Guid roomId, ClaimsPrincipal p, IDmService svc, CancellationToken ct) =>
            {
                var r = await svc.RespondToRequestAsync(roomId, UserId(p), accept: false, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("dm").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        app.MapPost("/v1/dm/{roomId:guid}/archive",
            async (Guid roomId, ClaimsPrincipal p, IDmService svc, CancellationToken ct) =>
            {
                var r = await svc.SetArchivedAsync(roomId, UserId(p), archived: true, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("dm").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        app.MapDelete("/v1/dm/{roomId:guid}/archive",
            async (Guid roomId, ClaimsPrincipal p, IDmService svc, CancellationToken ct) =>
            {
                var r = await svc.SetArchivedAsync(roomId, UserId(p), archived: false, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("dm").RequireAuthorization().Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>A room the caller is not in does not exist to them (D-018) — and `blocked` is 403 rather
    /// than 404 on purpose: the caller already knows this person exists, they just typed their name.</summary>
    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "blocked" or "not_recipient" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "already_answered" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
