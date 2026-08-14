using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record SetPasswordBody(string Password);
public record ChangePasswordBody(string CurrentPassword, string NewPassword);
public record PasswordResetStartBody(string Identifier);
/// <param name="ApprovalId">D-330 — the challenge from <c>/reset/start</c>, approved on a trusted device.
/// Optional because a recovery code is the other way to satisfy factor 2; supply one or the other.</param>
public record PasswordResetCompleteBody(string Identifier, string OtpCode, string NewPassword, string? RecoveryCode,
    Guid? ApprovalId = null, string? ResetToken = null);

// D-330. The two responses here are `PasswordResetStart` and `PendingResetView` from
// Kurx.Application.Abstractions, returned as-is rather than re-declared: that namespace is what
// SnakeCaseResponseConverter keys on, so a copy declared here would silently serialize camelCase and
// break the contract's convention (D-313).
public record ApproveResetBody(Guid ApprovalId, Guid DeviceId, string Signature, int? MatchNumber);

/// <summary>Password lifecycle (D-129) — factor 1 of the trusted-device architecture.
///
/// <para>Every route here is authenticated. Password <b>reset</b> for someone who cannot sign in is a
/// different, unauthenticated ceremony that requires OTP plus a trusted-device approval or a recovery
/// code (Amendment D) and lives in the recovery module — never here, because an unauthenticated
/// password endpoint is exactly the shape an account-takeover wants.</para></summary>
public static class PasswordEndpoints
{
    public static void MapPasswordEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth/password").WithTags("password").RequireAuthorization();

        // Lets the client decide between "create a password" and "change your password" without
        // guessing, and drives the migration prompt for OTP-era accounts that have none yet.
        g.MapGet("/status", async (ClaimsPrincipal p, IPasswordService svc, CancellationToken ct) =>
            Results.Ok(new PasswordPolicyStatus(await svc.HasPasswordAsync(UserId(p), ct), 12, 128))).Produces<PasswordPolicyStatus>();

        // First-time creation. Rate-limited despite being authenticated: policy validation is cheap but
        // Argon2id hashing is deliberately expensive, so an authenticated caller must not be able to
        // spin it freely.
        g.MapPost("/set", async (SetPasswordBody body, ClaimsPrincipal p, IPasswordService svc, CancellationToken ct) =>
        {
            var r = await svc.SetInitialAsync(UserId(p), body.Password ?? "", ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Problem(r.Error);
        }).RequireRateLimiting("otp").Produces<OperationAck>();

        g.MapPost("/change", async (ChangePasswordBody body, ClaimsPrincipal p, IPasswordService svc, CancellationToken ct) =>
        {
            var r = await svc.ChangeAsync(UserId(p), body.CurrentPassword ?? "", body.NewPassword ?? "", ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Problem(r.Error);
        }).RequireRateLimiting("otp").Produces<OperationAck>();

        // Reset ceremony (Phase 2C, D-127, INV-B). Anonymous — the whole point is that the user cannot sign
        // in. OTP is one factor and never enough; /complete also requires a recovery code or a device
        // approval bound to this reset (D-330). Rate-limited on the OTP shield.
        var reset = app.MapGroup("/v1/auth/password/reset").WithTags("password").RequireRateLimiting("otp");

        reset.MapPost("/start", async (PasswordResetStartBody body, HttpContext http, IPasswordResetService svc, CancellationToken ct) =>
        {
            // Identical in shape whether or not the account exists: an unknown identifier gets a decoy
            // approval backed by nothing, so the response cannot be used to enumerate accounts.
            var r = await svc.StartAsync(body.Identifier, http.Connection.RemoteIpAddress?.ToString(), ct);
            return Results.Ok(r);
        }).Produces<PasswordResetStart>();

        reset.MapPost("/complete", async (PasswordResetCompleteBody body, IPasswordResetService svc, CancellationToken ct) =>
        {
            var r = await svc.CompleteAsync(body.Identifier, body.OtpCode ?? "", body.NewPassword ?? "",
                body.RecoveryCode, body.ApprovalId, body.ResetToken, ct);
            if (!r.Ok || r.Tokens is null)
                return Problem(r.Error);
            return Results.Ok(new SessionTokens(
                r.Tokens.AccessToken,
                r.Tokens.AccessExpiresAt,
                r.Tokens.RefreshToken,
                r.Tokens.RefreshExpiresAt,
                r.UserId));
        }).Produces<SessionTokens>();

        // D-330 — the approving half, and the only authenticated part of the ceremony. This is the device
        // the user is still signed in on; it lists resets awaiting approval and signs one off. Deliberately
        // NOT under the `reset` group: that group is anonymous, and these two must never be.
        var approve = app.MapGroup("/v1/auth/password/reset").WithTags("password").RequireAuthorization();

        approve.MapGet("/pending", async (ClaimsPrincipal p, IPasswordResetService svc, CancellationToken ct) =>
        {
            return Results.Ok(await svc.ListPendingAsync(UserId(p), ct));
        }).Produces<IReadOnlyList<PendingResetView>>();

        approve.MapPost("/approve", async (ApproveResetBody body, ClaimsPrincipal p, IPasswordResetService svc,
            CancellationToken ct) =>
        {
            var r = await svc.ApproveAsync(UserId(p), body.ApprovalId, body.DeviceId, body.Signature ?? "",
                body.MatchNumber, ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : Problem(r.Error);
        }).Produces<OperationAck>();
    }

    /// <summary>Policy failures are 400 (the user can fix them); a wrong current password is 401; a
    /// lockout is 423 Locked so the client can show a wait rather than an unhelpful retry.</summary>
    private static IResult Problem(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "invalid_credentials" or "invalid_reset" => ProblemResults.Problem(error, StatusCodes.Status401Unauthorized),
        "account_locked" => ProblemResults.Problem(error, StatusCodes.Status423Locked),
        "second_factor_required" or "account_banned" or "account_suspended" =>
            ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
