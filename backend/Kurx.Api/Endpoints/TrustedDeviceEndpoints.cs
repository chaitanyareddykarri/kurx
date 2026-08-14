using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record EnrollDeviceBody(string? Name, string Platform, string PublicKeySpki, string? Alg, string? AttestationJson);
public record CompleteEnrollBody(Guid ChallengeId, Guid DeviceId, string Signature);

/// <summary>Trusted-device enrollment + management (AM2). All authenticated; additive — the flag-gated
/// login cutover that consumes these devices lands in AM4/AM9.</summary>
public static class TrustedDeviceEndpoints
{
    public static void MapTrustedDeviceEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth/devices").WithTags("trusted-devices").RequireAuthorization();

        g.MapPost("/enroll", async (EnrollDeviceBody body, ClaimsPrincipal principal, ITrustedDeviceService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.PublicKeySpki) || string.IsNullOrWhiteSpace(body.Platform))
                return ProblemResults.Problem("invalid_request", StatusCodes.Status400BadRequest);
            var e = await svc.BeginEnrollmentAsync(UserId(principal), body.Name, body.Platform,
                body.PublicKeySpki, body.Alg ?? "ES256", body.AttestationJson, ct);
            return Results.Ok(new
            {
                device_id = e.DeviceId,
                credential_id = e.CredentialId,
                challenge_id = e.ChallengeId,
                nonce = e.Nonce,
                match_number = e.MatchNumber,
                expires_at = e.ExpiresAt,
            });
        });

        g.MapPost("/enroll/verify", async (CompleteEnrollBody body, ClaimsPrincipal principal, ITrustedDeviceService svc, CancellationToken ct) =>
        {
            var r = await svc.CompleteEnrollmentAsync(UserId(principal), body.ChallengeId, body.DeviceId, body.Signature, ct);
            return r.Ok
                ? Results.Ok(new { ok = true, state = "Trusted" })
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        });

        g.MapGet("", async (ClaimsPrincipal principal, ITrustedDeviceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(UserId(principal), ct))).Produces<IReadOnlyList<TrustedDeviceView>>();

        g.MapPost("/{id:guid}/revoke", async (Guid id, ClaimsPrincipal principal, ITrustedDeviceService svc, CancellationToken ct) =>
        {
            var r = await svc.RevokeAsync(UserId(principal), id, ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }).Produces<OperationAck>();

        // ── "Your devices": live sessions and remote sign-out (AM5) ──────────
        var sessions = app.MapGroup("/v1/auth/sessions").WithTags("trusted-devices").RequireAuthorization();

        sessions.MapGet("", async (ClaimsPrincipal principal, ITrustedDeviceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListSessionsAsync(UserId(principal), null, ct))).Produces<IReadOnlyList<AuthSessionView>>();

        sessions.MapPost("/{id:guid}/revoke", async (Guid id, ClaimsPrincipal principal, ITrustedDeviceService svc, CancellationToken ct) =>
        {
            var r = await svc.RevokeSessionAsync(UserId(principal), id, ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }).Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
