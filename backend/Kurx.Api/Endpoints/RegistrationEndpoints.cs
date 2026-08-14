using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record EmailVerifyStartBody(string Email);
public record EmailVerifyCompleteBody(string Email, string Code);

/// <summary>Registration ceremony + email verification (Phase 2D, architecture §4.14/§5). All authenticated:
/// registration begins with the phone-OTP login that mints the session, then the client completes the
/// remaining steps guided by <c>/registration/status</c>.</summary>
public static class RegistrationEndpoints
{
    public static void MapRegistrationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth").WithTags("registration").RequireAuthorization();

        g.MapGet("/registration/status", async (ClaimsPrincipal p, IRegistrationService svc, CancellationToken ct) =>
        {
            var s = await svc.GetStatusAsync(UserId(p), ct);
            // The record's snake_cased property names reproduce the anonymous object's keys exactly, so
            // the wire is unchanged — and the compiler now enforces that, which the hand-written key
            // list did not.
            return Results.Ok(new RegistrationStatusResponse(
                s.HasPassword, s.Email, s.EmailVerified, s.Phone, s.PhoneVerified,
                s.HasTrustedDevice, s.NeedsOnboarding, s.Remaining));
        }).Produces<RegistrationStatusResponse>();

        // Email verification is deliberately rate-limited (it sends an OTP and touches the unique email index).
        var email = app.MapGroup("/v1/auth/email/verify").WithTags("registration")
            .RequireAuthorization().RequireRateLimiting("otp");

        email.MapPost("/start", async (EmailVerifyStartBody body, HttpContext http, ClaimsPrincipal p, IRegistrationService svc, CancellationToken ct) =>
        {
            var r = await svc.StartEmailVerificationAsync(UserId(p), body.Email ?? "", http.Connection.RemoteIpAddress?.ToString(), ct);
            return r.Ok ? Results.Ok(OperationAck.Success) : Problem(r.Error);
        }).Produces<OperationAck>();

        email.MapPost("/complete", async (EmailVerifyCompleteBody body, ClaimsPrincipal p, IRegistrationService svc, CancellationToken ct) =>
        {
            var r = await svc.CompleteEmailVerificationAsync(UserId(p), body.Email ?? "", body.Code ?? "", ct);
            return r.Ok ? Results.Ok(EmailVerificationResult.Verified) : Problem(r.Error);
        }).Produces<EmailVerificationResult>();
    }

    private static IResult Problem(string? error) => error switch
    {
        "email_taken" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "resend_cooldown" or "rate_limited" => ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
