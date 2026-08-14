using System.Security.Claims;
using Kurx.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record RegisterDeviceBody(string FcmToken, string Platform, string? DeviceName, string? AppVersion);
public record UpdateDeviceBody(string? DeviceName, string? AppVersion, bool? IsActive);

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/devices/register", async (RegisterDeviceBody body, ClaimsPrincipal principal, INotificationService svc, CancellationToken ct) =>
        {
            await svc.RegisterDeviceAsync(UserId(principal), body.FcmToken, body.Platform, body.DeviceName, body.AppVersion, ct);
            return Results.Ok(OperationAck.Success);
        }).RequireAuthorization().WithTags("devices").WithValidation<RegisterDeviceBody>().Produces<OperationAck>();

        app.MapPatch("/v1/devices/{id:guid}", async (Guid id, UpdateDeviceBody body, ClaimsPrincipal principal, INotificationService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateDeviceAsync(UserId(principal), id, body.DeviceName, body.AppVersion, body.IsActive, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).RequireAuthorization().WithTags("devices").WithValidation<UpdateDeviceBody>().Produces<OperationAck>();

        app.MapDelete("/v1/devices/{id:guid}", async (Guid id, ClaimsPrincipal principal, INotificationService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteDeviceAsync(UserId(principal), id, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).RequireAuthorization().WithTags("devices").Produces<OperationAck>();

        app.MapGet("/v1/me/devices", async (ClaimsPrincipal principal, INotificationService svc, CancellationToken ct) =>
        {
            var devices = await svc.ListDevicesAsync(UserId(principal), ct);
            return Results.Ok(devices);
        }).RequireAuthorization().WithTags("devices").Produces<IReadOnlyList<DeviceView>>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
