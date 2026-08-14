using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

// camelCase in, snake_case out — these are the "in" half.
public record NotificationPreferenceChangeBody(string Category, bool? InApp, bool? Push, bool? Email, bool? WhatsApp);
public record UpdateNotificationPreferencesBody(IReadOnlyList<NotificationPreferenceChangeBody> Categories);
public record StartEmailChangeBody(string NewEmail);
public record CompleteEmailChangeBody(string NewEmail, string Code);
public record RequestAccountDeletionBody(string? Reason);

/// <summary>
/// Account settings (D-263): notification preferences, blocks, username history, the email-change
/// ceremony and scheduled deletion. Phone change lives on its existing route, hardened in place.
///
/// <para><b>Step-up is enforced here</b>, not in the service — it needs an HTTP result and putting it in
/// Application would drag ASP.NET across the layer boundary. Every write that changes *who can get back
/// into the account* (email, phone, deletion) goes through <see cref="StepUpGuard"/>; a user with no
/// trusted device is exempt by that guard's own rule, because demanding a factor they cannot produce
/// would lock them out of the settings that protect them.</para>
/// </summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        // ── Notification preferences (D-263) ──────────────────────────────────

        app.MapGet("/v1/me/notification-preferences",
            async (ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetNotificationPreferencesAsync(UserId(p), ct)))
            .WithTags("me").RequireAuthorization().Produces<NotificationPreferencesView>();

        app.MapPatch("/v1/me/notification-preferences",
            async (UpdateNotificationPreferencesBody body, ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
            {
                var changes = (body.Categories ?? [])
                    .Select(c => new NotificationPreferenceInput(c.Category, c.InApp, c.Push, c.Email, c.WhatsApp))
                    .ToList();
                var r = await svc.UpdateNotificationPreferencesAsync(UserId(p), changes, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("me").RequireAuthorization().Produces<NotificationPreferencesView>();

        // ── Blocks (D-263) ────────────────────────────────────────────────────

        app.MapGet("/v1/me/blocks",
            async (ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListBlocksAsync(UserId(p), ct)))
            .WithTags("me").RequireAuthorization().Produces<List<BlockedUserView>>();

        app.MapPost("/v1/me/blocks/{userId:guid}",
            async (Guid userId, ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
            {
                var r = await svc.BlockAsync(UserId(p), userId, ct);
                return r.Ok ? Results.NoContent() : Fail(r.Error);
            }).WithTags("me").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        app.MapDelete("/v1/me/blocks/{userId:guid}",
            async (Guid userId, ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
            {
                await svc.UnblockAsync(UserId(p), userId, ct);
                // Unconditional 204: unblocking someone who was never blocked is the state the caller
                // asked for, and a 404 here would leak nothing useful while breaking an idempotent retry.
                return Results.NoContent();
            }).WithTags("me").RequireAuthorization().Produces(StatusCodes.Status204NoContent);

        // ── Username history (D-263) ──────────────────────────────────────────

        app.MapGet("/v1/me/username-history",
            async (ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetUsernameHistoryAsync(UserId(p), ct)))
            .WithTags("me").RequireAuthorization().Produces<List<UsernameHistoryEntryView>>();

        // ── Email change (D-263) ──────────────────────────────────────────────
        // Rate-limited on the existing `otp` policy: this issues a real OTP, so it belongs behind the
        // same per-IP shield every other OTP issuance sits behind (D-005/D-255).

        app.MapPost("/v1/me/email/change/start",
            async (StartEmailChangeBody body, ClaimsPrincipal p, HttpContext http,
                IAccountService svc, IStepUpService stepUp, CancellationToken ct) =>
            {
                var userId = UserId(p);
                if (await StepUpGuard.RequireAsync(userId, stepUp, ct) is { } denied) return denied;

                var r = await svc.StartEmailChangeAsync(userId, body.NewEmail, ClientIp(http), ct);
                return r.Ok ? Results.Accepted() : Fail(r.Error);
            }).WithTags("me").RequireAuthorization().RequireRateLimiting("otp");

        app.MapPost("/v1/me/email/change/complete",
            async (CompleteEmailChangeBody body, ClaimsPrincipal p, IAccountService svc,
                IStepUpService stepUp, CancellationToken ct) =>
            {
                var userId = UserId(p);
                if (await StepUpGuard.RequireAsync(userId, stepUp, ct) is { } denied) return denied;

                var r = await svc.CompleteEmailChangeAsync(userId, body.NewEmail, body.Code, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("me").RequireAuthorization().RequireRateLimiting("otp").Produces<EmailChangeView>();

        // ── Phone change ──────────────────────────────────────────────────────
        // Deliberately NOT re-implemented here. A complete user-initiated phone change already exists
        // at POST /v1/me/phone/verify (AuthService.VerifyPhoneChangeAsync): it consumes the OTP,
        // dual-writes all four phone columns (D-089), revokes every session (D-038) and re-issues
        // tokens. A second ceremony would be the parallel half-implementation D-018 exists to stop.
        // D-263 hardens THAT path with step-up and an old-number notice instead.

        // ── Deletion (D-263, India DPDP) ──────────────────────────────────────

        app.MapGet("/v1/me/deletion",
            async (ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
            {
                var view = await svc.GetDeletionAsync(UserId(p), ct);
                // 404 rather than a `pending: false` body, so a client cannot mistake "never requested"
                // for "request cancelled".
                return view is null ? ProblemResults.Problem("not_found", StatusCodes.Status404NotFound) : Results.Ok(view);
            }).WithTags("me").RequireAuthorization().Produces<AccountDeletionView>();

        app.MapPost("/v1/me/deletion",
            async (RequestAccountDeletionBody? body, ClaimsPrincipal p, IAccountService svc,
                IStepUpService stepUp, CancellationToken ct) =>
            {
                var userId = UserId(p);
                if (await StepUpGuard.RequireAsync(userId, stepUp, ct) is { } denied) return denied;

                var r = await svc.RequestDeletionAsync(userId, body?.Reason, ct);
                return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
            }).WithTags("me").RequireAuthorization().Produces<AccountDeletionView>();

        app.MapDelete("/v1/me/deletion",
            async (ClaimsPrincipal p, IAccountService svc, CancellationToken ct) =>
            {
                // Deliberately NOT step-up gated. Cancelling is the safe direction, and a user part-way
                // through losing their devices must always be able to stop the clock.
                await svc.CancelDeletionAsync(UserId(p), ct);
                return Results.NoContent();
            }).WithTags("me").RequireAuthorization().Produces(StatusCodes.Status204NoContent);
    }

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "email_taken" or "phone_taken" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        "category_not_optional" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "rate_limited" or "otp_rate_limited" => ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static string? ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
