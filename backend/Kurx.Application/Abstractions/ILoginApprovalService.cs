namespace Kurx.Application.Abstractions;

public record LoginStatusResult(string Status, AuthTokens? Tokens = null, Guid? UserId = null,
    string? TrustedBrowserToken = null, DateTime? TrustedBrowserExpiresAt = null);
public record PendingLoginView(Guid ChallengeId, string Nonce, int? MatchNumber, string? ContextJson, DateTime ExpiresAt);

/// <summary>Which client is signing in. The available second factors are the same either way; only their
/// <b>ranking</b> differs (D-283), because "a trusted device" means something different on each surface.
///
/// <para>On the web the approving phone is a genuinely separate object, so device approval is the strongest
/// available factor and ranks first. On a handset the trusted device is usually the same device that is
/// signing in — which is not a second factor at all — so a code ranks first there.</para></summary>
public enum AuthSurface { Web, Mobile, Admin }

/// <summary>One second factor this account can actually use, as decided by the backend (D-283).
///
/// <para>Only <b>available</b> methods are returned — there is no "present but disabled" variant, so a client
/// cannot render a greyed-out button and leak the distinction through its UI.</para></summary>
/// <param name="Method">Stable machine key the client switches on: <c>trusted_device</c>, <c>passkey</c>,
/// <c>sms_otp</c>, <c>email_otp</c>, <c>recovery_code</c>. Never parse <paramref name="Label"/>.</param>
/// <param name="Rank">Display order, lowest first. Server-decided so all three clients agree.</param>
/// <param name="Label">Human text, server-supplied so copy cannot drift between clients.</param>
/// <param name="Hint">Masked destination or device name, or null. Safe to disclose: the caller has already
/// proven the password.</param>
public record SecondFactorMethod(string Method, int Rank, string Label, string? Hint);

/// <summary>Outcome of password-first login (Phase 2B). <c>Outcome</c> is one of <c>session</c> (factor 2 was a
/// valid trusted-browser cookie → tokens issued now), <c>device_approval</c> (a challenge was pushed to the
/// user's trusted devices → the client waits on <c>/status</c>), <c>second_factor</c> (D-280 — the account's
/// available factors are in <see cref="Methods"/> and the client picks one), or <c>failed</c>. A non-null
/// <see cref="TrustedBrowserToken"/> means the caller should set the trusted-browser cookie.</summary>
public record PasswordLoginResult(
    string Outcome,
    string? Error = null,
    AuthTokens? Tokens = null,
    Guid? UserId = null,
    Guid? ChallengeId = null,
    string? PollToken = null,
    int? MatchNumber = null,
    DateTime? ExpiresAt = null,
    string? TrustedBrowserToken = null,
    DateTime? TrustedBrowserExpiresAt = null,
    IReadOnlyList<SecondFactorMethod>? Methods = null);

/// <summary>Trusted-device push-approval login (AM4, ADR-A3/A9) — the passwordless, OTP-free sign-in.
/// Web starts a login, the server pushes a signed-nonce challenge to the user's trusted devices over FCM,
/// the device signs it after biometric unlock, and the waiting web client exchanges its poll token for a
/// device-bound session. Tokens are minted exactly once, only to the holder of the poll token, and only
/// after a valid device signature.</summary>
public interface ILoginApprovalService
{
    /// <summary>Password-first login orchestration (Phase 2B, architecture §5). Verifies factor 1 (password,
    /// timing-equalized), then satisfies factor 2 by a valid trusted-browser cookie (→ immediate session) or
    /// a trusted-device push approval (→ waiting screen). A correct password with neither factor available
    /// returns <c>no_second_factor</c> rather than a challenge nothing can approve. Never leaks whether the
    /// identifier exists, by shape or timing.</summary>
    Task<PasswordLoginResult> PasswordLoginAsync(string identifier, string password, string? trustedBrowserCookie,
        bool rememberBrowser, TrustedBrowserContext browserContext, string? ip, string? userAgent,
        AuthSurface surface = AuthSurface.Web, CancellationToken ct = default);

    /// <summary>Delivers a one-time code for a second factor the client picked from
    /// <see cref="PasswordLoginResult.Methods"/> (D-280). The continuation token proves this caller is the one
    /// that passed the password step — knowing a challenge id is not enough.
    ///
    /// <para>The method must be one this account actually has; asking for <c>email_otp</c> without a verified
    /// address is refused rather than silently downgraded, because a login code must never reach an address
    /// nobody has proven they own (D-282).</para></summary>
    Task<ServiceResult<bool>> SendSecondFactorCodeAsync(Guid challengeId, string continuationToken, string method,
        string? ip, CancellationToken ct = default);

    /// <summary>Verifies the code and mints the session, consuming the challenge in the same transaction so it
    /// cannot yield two sessions. Returns the same shape as the device-approval poll, so a client that already
    /// handles that path needs no second result contract.</summary>
    Task<LoginStatusResult> VerifySecondFactorCodeAsync(Guid challengeId, string continuationToken, string code,
        CancellationToken ct = default);

    /// <summary>Pending login challenges for the authenticated user — how the app obtains the nonce to sign.</summary>
    Task<IReadOnlyList<PendingLoginView>> ListPendingAsync(Guid userId, CancellationToken ct = default);

    Task<ServiceResult<bool>> ApproveAsync(Guid userId, Guid challengeId, Guid deviceId, string signatureBase64,
        int? matchNumber, CancellationToken ct = default);

    Task<ServiceResult<bool>> RejectAsync(Guid userId, Guid challengeId, CancellationToken ct = default);

    /// <summary>Waiting-screen poll. Requires the poll token issued by <see cref="PasswordLoginAsync"/>;
    /// mints the session on the first poll after approval, then marks the challenge consumed.</summary>
    Task<LoginStatusResult> StatusAsync(Guid challengeId, string pollToken, CancellationToken ct = default);

    /// <summary>Whether a caller holding <paramref name="pollToken"/> may subscribe to this login's realtime
    /// status (AM9). Same binding rule as <see cref="StatusAsync"/> — the hub is anonymous, so the poll
    /// token is what authorizes the subscription.</summary>
    Task<bool> CanWatchAsync(Guid challengeId, string pollToken, CancellationToken ct = default);
}
