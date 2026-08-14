using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>One-time account recovery codes (AM7, ADR-A4/A11). The escape hatch when every trusted device
/// is gone — without it, losing your phone means losing the account, which is exactly the failure mode a
/// device-bound auth platform creates. Codes are 64-bit CSPRNG values, SHA-256 at rest (no pepper needed:
/// unlike a 6-digit OTP they are not brute-forceable), single-use, and a new set invalidates the old one.
///
/// Redemption is two-factor on purpose: a leaked code sheet must not be a login. The holder must also pass
/// an OTP to the account's registered phone, so recovery needs something-you-have *and* the code.</summary>
public class RecoveryCodeService(KurxDbContext db, IOtpService otp, TokenService tokens,
    ITrustedBrowserService trustedBrowsers, ILogger<RecoveryCodeService> log) : IRecoveryCodeService
{
    public const int CodeCount = 10;

    public async Task<IReadOnlyList<string>> GenerateAsync(Guid userId, CancellationToken ct = default)
    {
        // Regenerating retires the previous sheet — otherwise a printout the user thought they'd replaced
        // would still open the account.
        await db.RecoveryCodes.Where(c => c.UserId == userId && c.UsedAt == null).ExecuteDeleteAsync(ct);

        var codes = new List<string>(CodeCount);
        for (var i = 0; i < CodeCount; i++)
        {
            var code = NewCode();
            codes.Add(code);
            db.RecoveryCodes.Add(new RecoveryCode { UserId = userId, CodeHash = HashCode(code) });
        }

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "recovery.codes_generated", Severity = "info",
            ContextJson = $"{{\"count\":{CodeCount}}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "recovery.codes_generated",
            Entity = "users", EntityId = userId,
        });
        await db.SaveChangesAsync(ct);
        return codes;
    }

    public Task<int> RemainingAsync(Guid userId, CancellationToken ct = default)
        => db.RecoveryCodes.CountAsync(c => c.UserId == userId && c.UsedAt == null, ct);

    public async Task StartAsync(string identifier, string? requestIp, CancellationToken ct = default)
    {
        var user = await AuthIdentifiers.ResolveUserAsync(db, identifier, ct);
        // Anti-enumeration: an unknown identifier is silently a no-op, same response either way.
        if (user?.Phone is not { Length: > 0 } phone) return;

        await otp.IssueAsync("+" + phone, OtpChannelPolicy.For(OtpPurpose.AccountRecovery),
            OtpPurpose.AccountRecovery, user.Id, requestIp, ct);
    }

    public async Task<RecoveryRedeemResult> RedeemAsync(string identifier, string otpCode, string recoveryCode,
        CancellationToken ct = default)
    {
        var user = await AuthIdentifiers.ResolveUserAsync(db, identifier, ct);
        if (user?.Phone is not { Length: > 0 } phone)
            return new RecoveryRedeemResult(false, "invalid_recovery");     // never says which part was wrong

        var otpResult = await otp.VerifyAsync("+" + phone, OtpChannelPolicy.For(OtpPurpose.AccountRecovery),
            OtpPurpose.AccountRecovery, otpCode, ct);
        if (!otpResult.Ok)
            return new RecoveryRedeemResult(false, "invalid_recovery");

        var hash = HashCode(recoveryCode);
        var stored = await db.RecoveryCodes.FirstOrDefaultAsync(
            c => c.UserId == user.Id && c.CodeHash == hash && c.UsedAt == null, ct);
        if (stored is null)
        {
            db.SecurityEvents.Add(new SecurityEvent
            {
                UserId = user.Id, Type = "recovery.code_rejected", Severity = "warning",
            });
            await db.SaveChangesAsync(ct);
            return new RecoveryRedeemResult(false, "invalid_recovery");
        }

        // A moderated account cannot be recovered back into service (D-060).
        if (user.BannedAt is not null || user.SuspendedAt is not null)
            return new RecoveryRedeemResult(false, user.BannedAt is not null ? "account_banned" : "account_suspended");

        var now = DateTime.UtcNow;
        var remaining = await RemainingAsync(user.Id, ct) - 1;   // counted before this one is marked used
        stored.UsedAt = now;

        // Recovery means the old devices may be lost or in someone else's hands: cut every existing session
        // and force every trusted device to re-enroll before it can approve anything again.
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        await db.AuthSessions.Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.RevokeReason, "account_recovered"), ct);
        await db.TrustedDevices
            .Where(d => d.UserId == user.Id && d.DeletedAt == null && d.LifecycleState == DeviceLifecycleState.Trusted)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LifecycleState, DeviceLifecycleState.Suspended)
                .SetProperty(d => d.StateChangedAt, now), ct);
        // Recovery re-secures the account, so a trusted browser must not remain a valid factor 2 either
        // (D-127). Staged into this transaction alongside the session/device teardown above.
        await trustedBrowsers.RevokeAllForCascadeAsync(user.Id, "recovery", ct);

        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (rawRefresh, refreshHash, refreshExpires) = tokens.CreateRefreshToken();
        // Deliberately a plain bearer session: the user has no trusted device left to constrain it to.
        // Enrolling a new device (AM2) is the next step the client drives them through.
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = refreshHash, ExpiresAt = refreshExpires });

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = user.Id, Type = "recovery.redeemed", Severity = "critical",
            ContextJson = $"{{\"remaining\":{remaining}}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = user.Id, Action = "auth.account_recovered",
            Entity = "users", EntityId = user.Id,
        });
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "recovery.redeemed",
            PayloadJson = $"{{\"userId\":\"{user.Id}\"}}",
            IdempotencyKey = $"recovery.redeemed:{stored.Id}",
        });
        await db.SaveChangesAsync(ct);

        log.LogWarning("Account recovered via recovery code for user {UserId}; all sessions revoked", user.Id);
        return new RecoveryRedeemResult(true,
            Tokens: new AuthTokens(access, accessExpires, rawRefresh, refreshExpires), UserId: user.Id);
    }

    public async Task<bool> ConsumeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var hash = HashCode(code);
        var stored = await db.RecoveryCodes.FirstOrDefaultAsync(
            c => c.UserId == userId && c.CodeHash == hash && c.UsedAt == null, ct);
        if (stored is null)
            return false;
        stored.UsedAt = DateTime.UtcNow;
        db.SecurityEvents.Add(new SecurityEvent { UserId = userId, Type = "recovery.code_consumed", Severity = "warning" });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>64 bits of entropy, rendered as two readable groups ("a1b2c3d4-e5f6a7b8").</summary>
    private static string NewCode()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..]}";
    }

    /// <summary>Normalizes away the cosmetic dashes/case so a user retyping a code cannot fail on format.</summary>
    private static string HashCode(string code)
        => TokenService.Sha256(new string(code.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant());
}
