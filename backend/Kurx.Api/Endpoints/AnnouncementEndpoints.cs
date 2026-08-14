using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record CreateAnnouncementBody(
    string Title, string Body, string Audience, string[] Channels,
    DateTime? ScheduledAt, bool IncludeChildEvents);

public record UpdateAnnouncementBody(string? Title, string? Body, DateTime? ScheduledAt);

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/events/{eventId:guid}/announcements",
            async (Guid eventId, CreateAnnouncementBody body, ClaimsPrincipal principal, IAnnouncementService svc, CancellationToken ct) =>
            {
                var result = await svc.CreateAsync(UserId(principal), eventId,
                    body.Title, body.Body, body.Audience, body.Channels,
                    body.ScheduledAt, body.IncludeChildEvents, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("announcements").RequireAuthorization().RequireRateLimiting("heavy")
              .Produces<AnnouncementView>();

        app.MapGet("/v1/events/{eventId:guid}/announcements",
            async (Guid eventId, ClaimsPrincipal principal, IAnnouncementService svc, CancellationToken ct) =>
            {
                var result = await svc.ListAsync(UserId(principal), eventId, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("announcements").RequireAuthorization().Produces<List<AnnouncementView>>();

        app.MapGet("/v1/announcements/{announcementId:guid}",
            async (Guid announcementId, ClaimsPrincipal principal, IAnnouncementService svc, CancellationToken ct) =>
            {
                var result = await svc.GetAsync(UserId(principal), announcementId, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("announcements").RequireAuthorization().Produces<AnnouncementView>();

        app.MapDelete("/v1/announcements/{announcementId:guid}",
            async (Guid announcementId, ClaimsPrincipal principal, IAnnouncementService svc, CancellationToken ct) =>
            {
                var result = await svc.CancelAsync(UserId(principal), announcementId, ct);
                return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
            }).WithTags("announcements").RequireAuthorization().Produces<OperationAck>();

        app.MapPatch("/v1/announcements/{announcementId:guid}",
            async (Guid announcementId, UpdateAnnouncementBody body, ClaimsPrincipal principal, IAnnouncementService svc, CancellationToken ct) =>
            {
                var result = await svc.UpdateAsync(UserId(principal), announcementId, body.Title, body.Body, body.ScheduledAt, ct);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error);
            }).WithTags("announcements").RequireAuthorization().Produces<AnnouncementView>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "rate_limited" => ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        "cannot_cancel" or "cannot_update" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
