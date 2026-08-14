using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record SpeakerBody(string Name, string? Bio, string? PhotoKey, string? Company, string? Role, string? SocialLinksJson, Guid? UserId);
public record AssignSpeakerBody(Guid SpeakerId, Guid? SessionId);

public static class SpeakerEndpoints
{
    public static void MapSpeakerEndpoints(this WebApplication app)
    {
        var speakers = app.MapGroup("/v1/orgs/{orgId:guid}/speakers").WithTags("speakers").RequireAuthorization();

        speakers.MapPost("/", async (Guid orgId, SpeakerBody body, ClaimsPrincipal principal, ISpeakerService svc, CancellationToken ct) =>
        {
            var input = new SpeakerInput(body.Name, body.Bio, body.PhotoKey, body.Company, body.Role, body.SocialLinksJson, body.UserId);
            var result = await svc.CreateAsync(UserId(principal), orgId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<SpeakerBody>().Produces<SpeakerView>();

        speakers.MapGet("/", async (Guid orgId, ClaimsPrincipal principal, ISpeakerService svc, CancellationToken ct) =>
        {
            var result = await svc.ListForOrgAsync(UserId(principal), orgId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).Produces<IReadOnlyList<SpeakerView>>();

        speakers.MapPatch("/{speakerId:guid}", async (Guid orgId, Guid speakerId, SpeakerBody body, ClaimsPrincipal principal, ISpeakerService svc, CancellationToken ct) =>
        {
            var input = new SpeakerInput(body.Name, body.Bio, body.PhotoKey, body.Company, body.Role, body.SocialLinksJson, body.UserId);
            var result = await svc.UpdateAsync(UserId(principal), speakerId, IsAdmin(principal), input, ct);
            return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
        }).WithValidation<SpeakerBody>().Produces<SpeakerView>();

        speakers.MapDelete("/{speakerId:guid}", async (Guid orgId, Guid speakerId, ClaimsPrincipal principal, ISpeakerService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(UserId(principal), speakerId, IsAdmin(principal), ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        var events = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/speakers").WithTags("speakers").RequireAuthorization();

        events.MapGet("/", async (Guid orgId, Guid eventId, ISpeakerService svc, CancellationToken ct)
            => Results.Ok((await svc.ListForEventAsync(eventId, ct)))).Produces<IReadOnlyList<SpeakerView>>();

        events.MapPost("/", async (Guid orgId, Guid eventId, AssignSpeakerBody body, ClaimsPrincipal principal, ISpeakerService svc, CancellationToken ct) =>
        {
            var result = await svc.AssignToEventAsync(UserId(principal), eventId, IsAdmin(principal), body.SpeakerId, body.SessionId, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        events.MapDelete("/{speakerId:guid}", async (Guid orgId, Guid eventId, Guid speakerId, ClaimsPrincipal principal, ISpeakerService svc, CancellationToken ct) =>
        {
            var result = await svc.RemoveFromEventAsync(UserId(principal), eventId, IsAdmin(principal), speakerId, ct);
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

}
