using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record LoginApproveBody(Guid ChallengeId, Guid DeviceId, string Signature, int? MatchNumber);
public record LoginRejectBody(Guid ChallengeId);
public record LoginStatusBody(Guid ChallengeId, string PollToken);
/// <param name="Surface">Which client is asking — <c>web</c> | <c>mobile</c> | <c>admin</c>. Optional and
/// defaulting to web, so existing clients keep working unchanged. It only reorders the returned methods
/// (D-283); it never changes which ones are available, so a client cannot widen its own options by lying.</param>
public record PasswordLoginBody(string Identifier, string Password, bool? RememberBrowser, string? Surface = null);

/// <param name="Method">One of the machine keys returned in <c>methods[].method</c>.</param>
public record SecondFactorSendBody(Guid ChallengeId, string PollToken, string Method);
public record SecondFactorVerifyBody(Guid ChallengeId, string PollToken, string Code);

/// <summary>Trusted-device push-approval login (AM4). Passwordless and OTP-free: the trusted device's
/// signature is the factor. `/start` and `/status` are anonymous (the caller has no session yet);
/// `/pending`, `/approve`, `/reject` are the authenticated device-app side.</summary>
public static class DeviceLoginEndpoints
{
    public static void MapDeviceLoginEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth/login").WithTags("device-login");

        // Password-first login (Phase 2B, architecture §5). Factor 1 = password; factor 2 = a valid
        // trusted-browser cookie (→ tokens now) or a trusted-device approval (→ waiting screen). Anonymous,
        // rate-limited on the OTP shield.
        g.MapPost("/password", async (PasswordLoginBody body, HttpContext http, ILoginApprovalService svc, CancellationToken ct) =>
        {
            var cookie = http.Request.Cookies.TryGetValue(TrustedBrowserEndpoints.CookieName, out var c) ? c : null;
            var ip = http.Connection.RemoteIpAddress?.ToString();
            var ua = http.Request.Headers.UserAgent.ToString();
            var r = await svc.PasswordLoginAsync(body.Identifier, body.Password ?? "", cookie,
                body.RememberBrowser ?? false, new TrustedBrowserContext(UserAgent: ua, Ip: ip), ip, ua,
                ParseSurface(body.Surface), ct);

            switch (r.Outcome)
            {
                case "session":
                    if (r.TrustedBrowserToken is not null)
                        SetTrustedBrowserCookie(http, r.TrustedBrowserToken, r.TrustedBrowserExpiresAt);
                    return Results.Ok(new SessionTokens(
                r.Tokens!.AccessToken,
                r.Tokens!.AccessExpiresAt,
                r.Tokens!.RefreshToken,
                r.Tokens!.RefreshExpiresAt,
                r.UserId));
                case "device_approval":
                    return Results.Ok(new
                    {
                        next = "device_approval",
                        challenge_id = r.ChallengeId,
                        poll_token = r.PollToken,
                        match_number = r.MatchNumber,
                        expires_at = r.ExpiresAt,
                        // The full ranked list travels with the preferred path too, so a client can offer
                        // "use another method" without a second round trip (D-283).
                        methods = ToWire(r.Methods),
                    });
                case "second_factor":
                    return Results.Ok(new
                    {
                        next = "second_factor",
                        challenge_id = r.ChallengeId,
                        poll_token = r.PollToken,
                        expires_at = r.ExpiresAt,
                        methods = ToWire(r.Methods),
                    });
                default:
                    return r.Error switch
                    {
                        "account_locked" => ProblemResults.Problem("account_locked", StatusCodes.Status423Locked),
                        "no_second_factor" => ProblemResults.Problem("no_second_factor", StatusCodes.Status400BadRequest),
                        _ => ProblemResults.Problem("invalid_credentials", StatusCodes.Status401Unauthorized),
                    };
            }
        }).RequireRateLimiting("otp");

        // ── Second factor by one-time code (D-280) ───────────────────────────
        // Anonymous by necessity — the caller passed the password but has no session yet. The poll token
        // issued by /password is what proves that, so knowing a challenge id is not enough.

        g.MapPost("/second-factor/send", async (SecondFactorSendBody body, HttpContext http,
            ILoginApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.SendSecondFactorCodeAsync(body.ChallengeId, body.PollToken, body.Method,
                http.Connection.RemoteIpAddress?.ToString(), ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : r.Error switch
                {
                    "not_found" or "consumed" or "expired" =>
                        ProblemResults.Problem(r.Error, StatusCodes.Status404NotFound),
                    "rate_limited" or "resend_cooldown" =>
                        ProblemResults.Problem(r.Error, StatusCodes.Status429TooManyRequests),
                    _ => ProblemResults.Problem(r.Error, StatusCodes.Status400BadRequest),
                };
        }).RequireRateLimiting("otp").Produces<OperationAck>();

        g.MapPost("/second-factor/verify", async (SecondFactorVerifyBody body, HttpContext http,
            ILoginApprovalService svc, CancellationToken ct) =>
        {
            var result = await svc.VerifySecondFactorCodeAsync(body.ChallengeId, body.PollToken, body.Code, ct);
            if (result.Status != "approved" || result.Tokens is null)
                // Deliberately not distinguishing "wrong code" from "unknown challenge": the OTP platform
                // owns the attempt cap, and a distinct error here would be a probing oracle.
                return ProblemResults.Problem("invalid_code", StatusCodes.Status401Unauthorized);

            // "Remember this browser", if it was ticked at the password step (F3) — same cookie the
            // device-approval path sets, so the next sign-in skips the second factor identically.
            if (result.TrustedBrowserToken is not null)
                SetTrustedBrowserCookie(http, result.TrustedBrowserToken, result.TrustedBrowserExpiresAt);

            return Results.Ok(new SecondFactorSession(
                result.Status,
                result.Tokens.AccessToken,
                result.Tokens.AccessExpiresAt,
                result.Tokens.RefreshToken,
                result.Tokens.RefreshExpiresAt,
                result.UserId));
        }).RequireRateLimiting("otp").Produces<SecondFactorSession>();

        // Anonymous: the waiting web client polls with the poll token it received from /start or /password.
        g.MapPost("/status", async (LoginStatusBody body, HttpContext http, ILoginApprovalService svc, CancellationToken ct) =>
        {
            var result = await svc.StatusAsync(body.ChallengeId, body.PollToken, ct);
            if (result.Status != "approved" || result.Tokens is null)
                return Results.Ok(new { status = result.Status });

            // If "remember this browser" was ticked at password-login, session issuance minted a
            // trusted-browser token; set it as the factor-2 cookie now.
            if (result.TrustedBrowserToken is not null)
                SetTrustedBrowserCookie(http, result.TrustedBrowserToken, result.TrustedBrowserExpiresAt);

            return Results.Ok(new SecondFactorSession(
                result.Status,
                result.Tokens.AccessToken,
                result.Tokens.AccessExpiresAt,
                result.Tokens.RefreshToken,
                result.Tokens.RefreshExpiresAt,
                result.UserId));
        }).RequireRateLimiting("otp");

        // ── Authenticated device-app side ────────────────────────────────────
        g.MapGet("/pending", async (ClaimsPrincipal principal, ILoginApprovalService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPendingAsync(UserId(principal), ct)))
            .RequireAuthorization().Produces<IReadOnlyList<PendingLoginView>>();

        g.MapPost("/approve", async (LoginApproveBody body, ClaimsPrincipal principal, ILoginApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.ApproveAsync(UserId(principal), body.ChallengeId, body.DeviceId, body.Signature, body.MatchNumber, ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }).RequireAuthorization().Produces<OperationAck>();

        g.MapPost("/reject", async (LoginRejectBody body, ClaimsPrincipal principal, ILoginApprovalService svc, CancellationToken ct) =>
        {
            var r = await svc.RejectAsync(UserId(principal), body.ChallengeId, ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }).RequireAuthorization().Produces<OperationAck>();
    }

    /// <summary>Sets the factor-2 trusted-browser cookie: HttpOnly (no JS access), Secure (TLS only),
    /// SameSite=Lax. The value is the opaque token whose SHA-256 the server stored (INV-A: this cookie is
    /// factor 2 only — it never carries password authority).</summary>
    private static void SetTrustedBrowserCookie(HttpContext http, string token, DateTime? expires) =>
        http.Response.Cookies.Append(TrustedBrowserEndpoints.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expires ?? DateTime.UtcNow.AddDays(30),
        });

    /// <summary>Unrecognised values fall back to web rather than failing the login: the surface only reorders
    /// the response (D-283), so a bad value costs a suboptimal ordering, never access.</summary>
    private static AuthSurface ParseSurface(string? surface) => surface?.Trim().ToLowerInvariant() switch
    {
        "mobile" => AuthSurface.Mobile,
        "admin" => AuthSurface.Admin,
        _ => AuthSurface.Web,
    };

    private static object[] ToWire(IReadOnlyList<SecondFactorMethod>? methods) =>
        methods is null
            ? []
            : [.. methods.Select(m => new { method = m.Method, rank = m.Rank, label = m.Label, hint = m.Hint })];

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
