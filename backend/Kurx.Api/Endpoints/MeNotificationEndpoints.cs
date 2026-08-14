using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>In-app notifications + device registration (D-064, A6). Endpoints over the existing (previously
/// dead) NotificationService. The FCM push *send* is a provider concern (deferred); listing/reading the
/// in-app feed and registering a device token are pure DB and ship now.</summary>
public static class MeNotificationEndpoints
{
    public static void MapMeNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/me/notifications", async (int? page, int? pageSize, ClaimsPrincipal p,
            INotificationService svc, CancellationToken ct) =>
        {
            var uid = UserId(p);
            var items = await svc.ListAsync(uid, page ?? 1, pageSize ?? 20, ct: ct);
            var unread = await svc.UnreadCountAsync(uid, ct);
            return Results.Ok(new NotificationPage(items, unread));
            // Envelope, not a bare list — `items` plus `unread_count`. Declared in Stage E with a named
            // envelope DTO; annotating the element type here would misdescribe the body.
        }).RequireAuthorization().WithTags("me").Produces<NotificationPage>();

        app.MapPost("/v1/me/notifications/{id:guid}/read", async (Guid id, ClaimsPrincipal p, INotificationService svc, CancellationToken ct) =>
        {
            var r = await svc.MarkReadAsync(UserId(p), id, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).RequireAuthorization().WithTags("me").Produces<OperationAck>();

        app.MapPost("/v1/me/notifications/read-all", async (ClaimsPrincipal p, INotificationService svc, CancellationToken ct) =>
        {
            await svc.MarkAllReadAsync(UserId(p), ct);
            return Results.Ok(OperationAck.Success);
        }).RequireAuthorization().WithTags("me").Produces<OperationAck>();

        app.MapDelete("/v1/me/notifications/{id:guid}", async (Guid id, ClaimsPrincipal p, INotificationService svc, CancellationToken ct) =>
        {
            var r = await svc.DeleteAsync(UserId(p), id, ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound);
        }).RequireAuthorization().WithTags("me").Produces<OperationAck>();

        app.MapPost("/v1/me/devices", async (RegisterDeviceBody body, ClaimsPrincipal p, INotificationService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.FcmToken)) return ProblemResults.Problem("fcm_token_required", StatusCodes.Status400BadRequest);
            await svc.RegisterDeviceAsync(UserId(p), body.FcmToken.Trim(), (body.Platform ?? "").Trim().ToLowerInvariant(), ct: ct);
            return Results.Ok(OperationAck.Success);
        }).RequireAuthorization().WithTags("me").Produces<OperationAck>();

        app.MapDelete("/v1/me/devices/{id:guid}", async (Guid id, ClaimsPrincipal p, INotificationService svc, CancellationToken ct) =>
        {
            await svc.RemoveDeviceAsync(UserId(p), id, ct);
            return Results.Ok(OperationAck.Success);
        }).RequireAuthorization().WithTags("me").Produces<OperationAck>();
    }


    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
