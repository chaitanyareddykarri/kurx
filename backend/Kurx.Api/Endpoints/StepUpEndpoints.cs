using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record StepUpStartBody(string? Action);
public record StepUpVerifyBody(Guid ChallengeId, Guid DeviceId, string Signature, int? MatchNumber);

/// <summary>Step-up authentication (AM6). All authenticated — step-up strengthens an existing session,
/// it never creates one.</summary>
public static class StepUpEndpoints
{
    public static void MapStepUpEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth/step-up").WithTags("step-up").RequireAuthorization();

        g.MapPost("/start", async (StepUpStartBody body, ClaimsPrincipal principal, IStepUpService svc, CancellationToken ct) =>
        {
            var r = await svc.StartAsync(UserId(principal), body.Action, ct);
            return r.Ok && r.Value is not null
                ? Results.Ok(new StepUpChallengeResponse(
                    r.Value.ChallengeId, r.Value.Nonce, r.Value.MatchNumber, r.Value.ExpiresAt))
                : ProblemResults.Problem(r.Error, StatusCodes.Status400BadRequest);
        }).Produces<StepUpChallengeResponse>();

        g.MapPost("/verify", async (StepUpVerifyBody body, ClaimsPrincipal principal, IStepUpService svc, CancellationToken ct) =>
        {
            var r = await svc.CompleteAsync(UserId(principal), body.ChallengeId, body.DeviceId, body.Signature, body.MatchNumber, ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }).Produces<OperationAck>();

        g.MapGet("/status", async (ClaimsPrincipal principal, IStepUpService svc, CancellationToken ct) =>
        {
            var userId = UserId(principal);
            var status = await svc.StatusAsync(userId, ct);
            return Results.Ok(new StepUpStatusResponse(
                status.Satisfied, status.ValidUntil, await svc.CanStepUpAsync(userId, ct)));
        }).Produces<StepUpStatusResponse>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
