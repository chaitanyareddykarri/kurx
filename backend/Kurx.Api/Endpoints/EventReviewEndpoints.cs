using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record CreateReviewBody(int Rating, string? Title, string? Body, bool IsAnonymous);

/// <summary>Event reviews / ratings (D-064). Post/edit is a verified ticket-holder (auth); reading is
/// public. Finishes the scaffolded event_reviews table.</summary>
public static class EventReviewEndpoints
{
    public static void MapEventReviewEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/events/{eventId:guid}/reviews", async (Guid eventId, CreateReviewBody body,
            ClaimsPrincipal p, IEventReviewService svc, CancellationToken ct) =>
        {
            var r = await svc.UpsertAsync(UserId(p), eventId, body.Rating, body.Title, body.Body, body.IsAnonymous, ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : Fail(r.Error);
        }).RequireAuthorization().WithTags("reviews");

        app.MapGet("/v1/events/{eventId:guid}/reviews", async (Guid eventId, int? page, int? pageSize,
            IEventReviewService svc, CancellationToken ct) =>
        {
            var (items, summary, total) = await svc.ListAsync(eventId, page ?? 1, pageSize ?? 20, ct);
            return Results.Ok(new
            {
                items = items.Select(ToJson),
                summary = new { average = summary.Average, count = summary.Count },
                total,
            });
        }).WithTags("reviews");

        app.MapDelete("/v1/events/{eventId:guid}/reviews/mine", async (Guid eventId, ClaimsPrincipal p,
            IEventReviewService svc, CancellationToken ct) =>
        {
            await svc.DeleteMineAsync(UserId(p), eventId, ct);
            return Results.Ok(OperationAck.Success);
        }).RequireAuthorization().WithTags("reviews").Produces<OperationAck>();
    }

    private static IResult Fail(string? error) => error switch
    {
        "review_requires_ticket" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static object ToJson(ReviewView r) => new
    {
        id = r.Id, event_id = r.EventId, rating = r.Rating, title = r.Title, body = r.Body,
        is_anonymous = r.IsAnonymous, is_verified = r.IsVerified, author_name = r.AuthorName, created_at = r.CreatedAt,
        author_username = r.AuthorUsername, author_avatar_key = r.AuthorAvatarKey,
        // D-302 — the key names the object, this fetches it. A bare key renders no face.
        author_avatar_url = r.AuthorAvatarUrl,
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
