using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record RecoveryStartBody(string Identifier);
public record RecoveryRedeemBody(string Identifier, string OtpCode, string RecoveryCode);

/// <summary>Account recovery codes (AM7). Generation/inspection is authenticated; the redeem path is
/// anonymous by necessity — the whole point is that the user cannot sign in.</summary>
public static class RecoveryEndpoints
{
    public static void MapRecoveryEndpoints(this WebApplication app)
    {
        var codes = app.MapGroup("/v1/auth/recovery-codes").WithTags("recovery").RequireAuthorization();

        codes.MapPost("", async (ClaimsPrincipal principal, IRecoveryCodeService svc, IStepUpService stepUp, CancellationToken ct) =>
        {
            var userId = UserId(principal);
            // Minting recovery codes mints new account-access credentials, so it is a step-up action (AM6):
            // a stolen access token must not be enough to print a fresh way back into the account. The
            // reusable guard also exempts users with no trusted device (they cannot step up), so this stays
            // identical for every high-risk endpoint that adopts it (Phase 2F).
            if (await StepUpGuard.RequireAsync(userId, stepUp, ct) is { } denied)
                return denied;

            var generated = await svc.GenerateAsync(userId, ct);
            // The only time the plaintext ever leaves the server.
            return Results.Ok(new RecoveryCodesIssued(generated, generated.Count));
        }).Produces<RecoveryCodesIssued>();

        codes.MapGet("", async (ClaimsPrincipal principal, IRecoveryCodeService svc, CancellationToken ct) =>
            Results.Ok(new RecoveryCodesRemaining(await svc.RemainingAsync(UserId(principal), ct))))
            .Produces<RecoveryCodesRemaining>();

        var recovery = app.MapGroup("/v1/auth/recovery").WithTags("recovery");

        recovery.MapPost("/start", async (RecoveryStartBody body, HttpContext http, IRecoveryCodeService svc, CancellationToken ct) =>
        {
            await svc.StartAsync(body.Identifier, http.Connection.RemoteIpAddress?.ToString(), ct);
            return Results.Ok(OperationAck.Success);      // identical whether or not the account exists
        }).RequireRateLimiting("otp").Produces<OperationAck>();

        recovery.MapPost("/redeem", async (RecoveryRedeemBody body, IRecoveryCodeService svc, CancellationToken ct) =>
        {
            var r = await svc.RedeemAsync(body.Identifier, body.OtpCode, body.RecoveryCode, ct);
            if (!r.Ok || r.Tokens is null)
                return ProblemResults.Problem(r.Error, StatusCodes.Status401Unauthorized);

            return Results.Ok(new SessionTokens(
                r.Tokens.AccessToken,
                r.Tokens.AccessExpiresAt,
                r.Tokens.RefreshToken,
                r.Tokens.RefreshExpiresAt,
                r.UserId));
        }).RequireRateLimiting("otp").Produces<SessionTokens>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
