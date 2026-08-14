using System.Security.Claims;
using System.Text.Json;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record PasskeyRegisterBody(Guid ChallengeId, JsonElement Response, string? DeviceName);
public record PasskeyLoginOptionsBody(string Identifier);
public record PasskeyLoginBody(Guid ChallengeId, JsonElement Response);

/// <summary>WebAuthn / passkey ceremonies (AM3). Registration is authenticated (you add a passkey to an
/// account you already hold); login is anonymous by necessity. The <c>Response</c> members are passed
/// through as raw JSON — they are the browser's <c>navigator.credentials</c> payload and are parsed by
/// the FIDO2 library, not by our own DTOs.</summary>
public static class PasskeyEndpoints
{
    public static void MapPasskeyEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth/passkeys").WithTags("passkeys");

        g.MapPost("/register/options", async (ClaimsPrincipal principal, IPasskeyService svc, CancellationToken ct) =>
        {
            var options = await svc.BeginRegistrationAsync(UserId(principal), ct);
            return Results.Ok(new
            {
                challenge_id = options.ChallengeId,
                options = JsonDocument.Parse(options.OptionsJson).RootElement,
            });
        }).RequireAuthorization();

        g.MapPost("/register", async (PasskeyRegisterBody body, ClaimsPrincipal principal, IPasskeyService svc, CancellationToken ct) =>
        {
            var r = await svc.CompleteRegistrationAsync(UserId(principal), body.ChallengeId,
                body.Response.GetRawText(), body.DeviceName, ct);
            return r.Ok
                ? Results.Ok(new { ok = true, device_id = r.Value })
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }).RequireAuthorization();

        g.MapGet("", async (ClaimsPrincipal principal, IPasskeyService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(UserId(principal), ct)))
            .RequireAuthorization().Produces<IReadOnlyList<TrustedDeviceView>>();

        // ── Anonymous login ceremony ─────────────────────────────────────────
        g.MapPost("/login/options", async (PasskeyLoginOptionsBody body, IPasskeyService svc, CancellationToken ct) =>
        {
            var options = await svc.BeginLoginAsync(body.Identifier, ct);
            // Always the same shape, whether or not the identifier exists (anti-enumeration).
            return Results.Ok(new
            {
                challenge_id = options.ChallengeId,
                options = JsonDocument.Parse(options.OptionsJson).RootElement,
            });
        }).RequireRateLimiting("otp");

        g.MapPost("/login", async (PasskeyLoginBody body, IPasskeyService svc, CancellationToken ct) =>
        {
            var r = await svc.CompleteLoginAsync(body.ChallengeId, body.Response.GetRawText(), ct);
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
