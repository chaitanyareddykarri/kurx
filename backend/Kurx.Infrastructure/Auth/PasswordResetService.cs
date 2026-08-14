using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Password reset ceremony (Phase 2C, D-127). Enforces INV-B: OTP is one factor and never enough
/// on its own — completion also requires a recovery code or a trusted device that approved <b>this</b>
/// reset. Reuses the OTP, recovery-code, challenge and password services rather than reimplementing any of
/// them.
///
/// <para><b>D-330 — why the second factor is a challenge and not a step-up.</b> This used to accept
/// <c>stepUp.StatusAsync(user.Id).Satisfied</c>: "did this account complete any step-up in the last 300
/// seconds". Every other caller of that method is authenticated, so the account scope matches the caller;
/// this endpoint is anonymous, so it did not. An attacker who had SIM-swapped the number held the OTP, and
/// the second factor was contributed by the victim's own unrelated activity. The approval below names the
/// account, names the reset, dies in five minutes, and is consumed exactly once.</para></summary>
public class PasswordResetService(KurxDbContext db, IOtpService otp, IPasswordService passwords,
    IRecoveryCodeService recovery, IChallengeService challenges, TokenService tokens) : IPasswordResetService
{
    public async Task<PasswordResetStart> StartAsync(string identifier, string? requestIp, CancellationToken ct = default)
    {
        var user = await AuthIdentifiers.ResolveUserAsync(db, identifier, ct);
        // Anti-enumeration: an unknown identifier gets a well-formed response backed by nothing. It cannot
        // be completed — no OTP was sent and no challenge exists — but it is indistinguishable from a real
        // one, which is the property the bare ack used to provide and must not lose.
        if (user?.Phone is not { Length: > 0 } phone)
            return Decoy();

        await otp.IssueAsync("+" + phone, OtpChannelPolicy.For(OtpPurpose.PasswordReset), OtpPurpose.PasswordReset,
            user.Id, requestIp, ct);

        // No trusted device means no approver, so opening a challenge would advertise a route that cannot
        // be walked. Such an account resets with a recovery code or not at all — the documented posture.
        var approvable = await db.TrustedDevices.AnyAsync(
            d => d.UserId == user.Id && d.DeletedAt == null && d.LifecycleState == DeviceLifecycleState.Trusted, ct);
        if (!approvable)
            return Decoy();

        // The transaction secret. Only its hash is persisted, so a database reader cannot present it, and
        // it never reaches the approving device — /pending deliberately does not return ContextJson.
        var resetToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        var issued = await challenges.IssueAsync(user.Id, AuthChallengePurpose.PasswordReset,
            $"{{\"ip\":{Json(requestIp)},\"rth\":\"{Sha256(resetToken)}\"}}", ct);
        return new PasswordResetStart(issued.ChallengeId, issued.MatchNumber, issued.ExpiresAt, resetToken);
    }

    /// <summary>A response shaped exactly like a real one and backed by nothing. The id is random rather
    /// than empty so it cannot be told apart by inspection, and it matches no row, so presenting it at
    /// completion fails the same way an expired approval does.</summary>
    private static PasswordResetStart Decoy() => new(
        Guid.NewGuid(), RandomNumberGenerator.GetInt32(10, 100), DateTime.UtcNow.AddSeconds(300),
        Base64Url(RandomNumberGenerator.GetBytes(32)));

    private static string Sha256(string value) =>
        Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Json(string? value) =>
        value is null ? "null" : System.Text.Json.JsonSerializer.Serialize(value);

    public async Task<PasswordResetResult> CompleteAsync(string identifier, string otpCode, string newPassword,
        string? recoveryCode, Guid? approvalId, string? resetToken, CancellationToken ct = default)
    {
        var user = await AuthIdentifiers.ResolveUserAsync(db, identifier, ct);
        if (user?.Phone is not { Length: > 0 } phone)
            return new PasswordResetResult(false, "invalid_reset");   // never says which part was wrong

        // Factor 1 — OTP to the registered channel.
        var otpResult = await otp.VerifyAsync("+" + phone, OtpChannelPolicy.For(OtpPurpose.PasswordReset),
            OtpPurpose.PasswordReset, otpCode, ct);
        if (!otpResult.Ok)
            return new PasswordResetResult(false, "invalid_reset");

        // Factor 2 — INV-B. A recovery code if supplied, otherwise a device approval bound to this reset.
        var secondFactor = !string.IsNullOrWhiteSpace(recoveryCode)
            ? await recovery.ConsumeAsync(user.Id, recoveryCode, ct)
            : approvalId is { } id && await ConsumeApprovalAsync(user.Id, id, resetToken, ct);
        if (!secondFactor)
            return new PasswordResetResult(false, "second_factor_required");

        // A moderated account cannot be reset back into service (D-060).
        if (user.BannedAt is not null || user.SuspendedAt is not null)
            return new PasswordResetResult(false, user.BannedAt is not null ? "account_banned" : "account_suspended");

        // Sets the password and cascade-revokes every browser + session (force re-login).
        var set = await passwords.ResetAsync(user.Id, newPassword, ct);
        if (!set.Ok)
            return new PasswordResetResult(false, set.Error);

        // The user proved OTP + a second factor, so mint a fresh session (the old ones were just revoked).
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExpires) = tokens.CreateRefreshToken();
        var session = new AuthSession { UserId = user.Id, TrustedDeviceId = null };
        db.AuthSessions.Add(session);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = refreshHash, ExpiresAt = refreshExpires, SessionId = session.Id,
        });
        await db.SaveChangesAsync(ct);

        return new PasswordResetResult(true,
            Tokens: new AuthTokens(access, accessExpires, rawRefresh, refreshExpires), UserId: user.Id);
    }

    /// <summary>Spends an approved reset challenge, or refuses. Every clause is load-bearing:
    /// <c>Id</c> and <c>UserId</c> together mean an approval minted for account A cannot reset account B;
    /// <c>Purpose</c> means a step-up or login approval is not a reset approval (D-088); <c>Approved</c>
    /// means an unapproved or already-spent challenge is worthless; <c>ExpiresAt</c> bounds it to five
    /// minutes.
    ///
    /// <para>It is one conditional UPDATE rather than a read-then-write for the same reason challenge
    /// approval is: two completions racing on one approval would otherwise both read <c>Approved</c> and
    /// both proceed, spending a single-use factor twice. Postgres serialises the row, so exactly one
    /// caller sees a row affected and every other attempt — concurrent or replayed later — sees zero.</para></summary>
    private async Task<bool> ConsumeApprovalAsync(Guid userId, Guid approvalId, string? resetToken, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Transaction binding, checked before the spend. Account scope alone is not enough: an owner who
        // legitimately approved their own reset leaves an Approved row, and a concurrent reset started by
        // an attacker for the same account would otherwise spend it. The token was handed only to the
        // browser that called /start, so presenting the pair proves this completion continues that
        // ceremony. Compared in fixed time against the stored digest, never against a stored secret.
        if (string.IsNullOrWhiteSpace(resetToken))
            return false;
        var contextJson = await db.AuthChallenges.AsNoTracking()
            .Where(c => c.Id == approvalId && c.UserId == userId
                        && c.Purpose == AuthChallengePurpose.PasswordReset)
            .Select(c => c.ContextJson)
            .FirstOrDefaultAsync(ct);
        if (contextJson is null || !TokenMatches(contextJson, resetToken))
            return false;

        var spent = await db.AuthChallenges
            .Where(c => c.Id == approvalId
                        && c.UserId == userId
                        && c.Purpose == AuthChallengePurpose.PasswordReset
                        && c.Status == AuthChallengeStatus.Approved
                        && c.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, AuthChallengeStatus.Consumed), ct);

        if (spent == 0)
            return false;

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "password_reset.approval_consumed", Severity = "info",
            ContextJson = $"{{\"approvalId\":\"{approvalId}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Constant-time compare of the presented token against the stored digest.</summary>
    private static bool TokenMatches(string contextJson, string presented)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("rth", out var stored) || stored.GetString() is not { } digest)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(digest), System.Text.Encoding.UTF8.GetBytes(Sha256(presented)));
    }

    public async Task<IReadOnlyList<PendingResetView>> ListPendingAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // ContextJson is deliberately NOT returned: it carries the reset-token digest, and the approving
        // device has no use for it. The device needs only what it must sign and display.
        return await db.AuthChallenges.AsNoTracking()
            .Where(c => c.UserId == userId && c.Purpose == AuthChallengePurpose.PasswordReset
                        && c.Status == AuthChallengeStatus.Pending && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PendingResetView(c.Id, c.Nonce, c.MatchNumber, null, c.ExpiresAt))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<bool>> ApproveAsync(Guid userId, Guid approvalId, Guid deviceId,
        string signatureBase64, int? matchNumber, CancellationToken ct = default)
    {
        var challenge = await db.AuthChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == approvalId, ct);
        // Scoped to the caller and to PasswordReset: account A's device cannot approve account B's reset,
        // and a login or step-up challenge is not redeemable here.
        if (challenge is null || challenge.UserId != userId || challenge.Purpose != AuthChallengePurpose.PasswordReset)
            return new ServiceResult<bool>(false, "not_found");

        var device = await db.TrustedDevices.AsNoTracking().FirstOrDefaultAsync(
            d => d.Id == deviceId && d.UserId == userId && d.DeletedAt == null, ct);
        if (device is null)
            return new ServiceResult<bool>(false, "not_found");
        if (device.LifecycleState != DeviceLifecycleState.Trusted)
            return new ServiceResult<bool>(false, "device_not_trusted");

        // Verifies the ES256 signature and the match number, and flips Pending → Approved exactly once.
        // The match number is what binds the approval to the browser that started this reset.
        var result = await challenges.VerifySignatureAsync(approvalId, deviceId, signatureBase64,
            AuthChallengePurpose.PasswordReset, matchNumber, ct);
        if (!result.Ok)
            return new ServiceResult<bool>(false, result.Error);

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "password_reset.approved", Severity = "warning",
            ContextJson = $"{{\"deviceId\":\"{deviceId}\",\"approvalId\":\"{approvalId}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return new ServiceResult<bool>(true, Value: true);
    }
}
