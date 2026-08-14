using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>Attendee engagement (D-064): save/unsave events, follow/unfollow orgs, and the "my" lists.
/// Authenticated; finishes the scaffolded saved_events / organization_followers tables. No provider.</summary>
public static class SocialEndpoints
{
    public static void MapSocialEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/events/{eventId:guid}/save", async (Guid eventId, ClaimsPrincipal p, ISocialService svc, CancellationToken ct) =>
        {
            var r = await svc.SaveEventAsync(UserId(p), eventId, ct);
            return r.Ok ? Results.Ok(new SavedAck(true)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).RequireAuthorization().WithTags("social").Produces<SavedAck>();

        app.MapDelete("/v1/events/{eventId:guid}/save", async (Guid eventId, ClaimsPrincipal p, ISocialService svc, CancellationToken ct) =>
        {
            await svc.UnsaveEventAsync(UserId(p), eventId, ct);
            return Results.Ok(new SavedAck(false));
        }).RequireAuthorization().WithTags("social").Produces<SavedAck>();

        app.MapGet("/v1/me/saved", async (int? limit, ClaimsPrincipal p, ISocialService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListSavedAsync(UserId(p), limit ?? 50, ct)).Select(EventEndpoints.ToSummaryJson)))
            .RequireAuthorization().WithTags("me").Produces<IReadOnlyList<EventSummaryResponse>>();

        app.MapPost("/v1/orgs/{orgId:guid}/follow", async (Guid orgId, ClaimsPrincipal p, ISocialService svc, CancellationToken ct) =>
        {
            var r = await svc.FollowOrgAsync(UserId(p), orgId, ct);
            return r.Ok ? Results.Ok(new FollowingAck(true)) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).RequireAuthorization().WithTags("social").Produces<FollowingAck>();

        app.MapDelete("/v1/orgs/{orgId:guid}/follow", async (Guid orgId, ClaimsPrincipal p, ISocialService svc, CancellationToken ct) =>
        {
            await svc.UnfollowOrgAsync(UserId(p), orgId, ct);
            return Results.Ok(new FollowingAck(false));
        }).RequireAuthorization().WithTags("social").Produces<FollowingAck>();

        app.MapGet("/v1/me/following", async (int? limit, ClaimsPrincipal p, ISocialService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListFollowingAsync(UserId(p), limit ?? 50, ct)).Select(f =>
                new FollowedOrg(f.OrgId, f.Name, f.Slug, f.LogoKey))))
            .RequireAuthorization().WithTags("me").Produces<IReadOnlyList<FollowedOrg>>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
