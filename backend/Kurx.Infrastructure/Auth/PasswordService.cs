using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Password lifecycle — factor 1 (D-126, D-129). Owns policy enforcement, hashing, reuse
/// history and account lockout. Deliberately knows nothing about sessions: proving a password is one
/// step of signing in, never the whole of it.</summary>
public class PasswordService(KurxDbContext db, IPasswordHasher hasher, IAuthTelemetry telemetry,
    ITrustedBrowserService trustedBrowsers)
    : IPasswordService
{
    /// <summary>Consecutive failures before the account locks. OWASP suggests 5–10; 10 is chosen because
    /// lockout is a denial-of-service lever against a *known* account, and the trusted-device factor
    /// already means a guessed password alone does not grant access.</summary>
    public const int MaxFailedAttempts = 10;

    /// <summary>Fixed window rather than escalating backoff: predictable for a locked-out legitimate
    /// user, and long enough that online guessing is hopeless against a 12-character minimum.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Task<bool> HasPasswordAsync(Guid userId, CancellationToken ct = default)
        => db.UserCredentials.AsNoTracking().AnyAsync(c => c.UserId == userId, ct);

    public PasswordRejection ValidatePolicy(string password, string? username, string? email, string? phone)
        => PasswordPolicy.Validate(password, username, email, phone);

    public async Task<ServiceResult<bool>> SetInitialAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return new ServiceResult<bool>(false, "not_found");

        if (await db.UserCredentials.AnyAsync(c => c.UserId == userId, ct))
            return new ServiceResult<bool>(false, "password_already_set");

        var rejection = ValidatePolicy(password, user.Username, user.Email, user.PhoneE164 ?? user.Phone);
        if (rejection != PasswordRejection.None)
            return new ServiceResult<bool>(false, RejectionCode(rejection));

        var hash = hasher.Hash(password);
        db.UserCredentials.Add(new UserCredential { UserId = userId, PasswordHash = hash });
        db.PasswordHistories.Add(new PasswordHistory { UserId = userId, PasswordHash = hash });
        WriteEvent(userId, "password.created", "info");

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
        {
            // Lost a race to set the first password. The unique index on UserId is what makes this
            // safe: the other request won and the account has exactly one credential, not two.
            return new ServiceResult<bool>(false, "password_already_set");
        }

        telemetry.RecordAuthAttempt("password", "created");
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task<ServiceResult<bool>> ChangeAsync(Guid userId, string currentPassword, string newPassword,
        CancellationToken ct = default)
    {
        var credential = await db.UserCredentials.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (credential is null)
            return new ServiceResult<bool>(false, "password_not_set");

        // Knowledge of the current password is required even though the caller already holds a session:
        // it is what stops a stolen access token from silently taking ownership of the account.
        var current = await VerifyAsync(userId, currentPassword, ct);
        if (current.Locked)
            return new ServiceResult<bool>(false, "account_locked");
        if (!current.Ok)
            return new ServiceResult<bool>(false, "invalid_credentials");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var rejection = ValidatePolicy(newPassword, user.Username, user.Email, user.PhoneE164 ?? user.Phone);
        if (rejection != PasswordRejection.None)
            return new ServiceResult<bool>(false, RejectionCode(rejection));

        if (await IsReusedAsync(userId, newPassword, ct))
            return new ServiceResult<bool>(false, RejectionCode(PasswordRejection.Reused));

        var hash = hasher.Hash(newPassword);
        credential.PasswordHash = hash;
        credential.Algorithm = "argon2id";
        credential.UpdatedAt = DateTime.UtcNow;
        // A successful change clears any lockout: the legitimate owner has just proven knowledge of the
        // current password, so leaving them locked out would punish the victim of a guessing attempt.
        credential.FailedAttempts = 0;
        credential.LockedUntil = null;

        db.PasswordHistories.Add(new PasswordHistory { UserId = userId, PasswordHash = hash });
        await PruneHistoryAsync(userId, ct);
        WriteEvent(userId, "password.changed", "warning");
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "auth.password_changed",
            Entity = "users", EntityId = userId,
        });
        // Same transaction as the change (ADR-AM16): a "your password changed" alert that can be lost
        // is exactly the alert a victim needs when it was not them.
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "password.changed",
            PayloadJson = $"{{\"userId\":\"{userId}\"}}",
            IdempotencyKey = $"password.changed:{userId}:{DateTime.UtcNow.Ticks}",
        });

        // A password change is a re-secure event: drop every trusted browser so a factor-2 cookie a thief
        // planted cannot survive the owner reclaiming the account (D-127, INV-A). Staged into this same
        // transaction, so the cascade commits with the change or not at all.
        await trustedBrowsers.RevokeAllForCascadeAsync(userId, "password_changed", ct);

        await db.SaveChangesAsync(ct);
        telemetry.RecordAuthAttempt("password", "changed");
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task<ServiceResult<bool>> ResetAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return new ServiceResult<bool>(false, "not_found");

        var rejection = ValidatePolicy(newPassword, user.Username, user.Email, user.PhoneE164 ?? user.Phone);
        if (rejection != PasswordRejection.None)
            return new ServiceResult<bool>(false, RejectionCode(rejection));
        if (await IsReusedAsync(userId, newPassword, ct))
            return new ServiceResult<bool>(false, RejectionCode(PasswordRejection.Reused));

        var hash = hasher.Hash(newPassword);
        var credential = await db.UserCredentials.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (credential is null)
        {
            // A user who never set a password can still land here via the reset ceremony; upsert one.
            db.UserCredentials.Add(new UserCredential { UserId = userId, PasswordHash = hash });
        }
        else
        {
            credential.PasswordHash = hash;
            credential.Algorithm = "argon2id";
            credential.UpdatedAt = DateTime.UtcNow;
            credential.FailedAttempts = 0;
            credential.LockedUntil = null;
        }
        db.PasswordHistories.Add(new PasswordHistory { UserId = userId, PasswordHash = hash });
        await PruneHistoryAsync(userId, ct);

        // A reset re-secures the account: drop every trusted browser and revoke every session so the reset
        // forces a fresh login (INV-B) and a cookie or refresh token a thief planted cannot survive it.
        var now = DateTime.UtcNow;
        await trustedBrowsers.RevokeAllForCascadeAsync(userId, "password_reset", ct);
        await db.AuthSessions.Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokeReason, "password_reset"), ct);
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

        WriteEvent(userId, "password.reset", "critical");
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "auth.password_reset", Entity = "users", EntityId = userId,
        });
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "password.reset",
            PayloadJson = $"{{\"userId\":\"{userId}\"}}",
            IdempotencyKey = $"password.reset:{userId}:{now.Ticks}",
        });
        await db.SaveChangesAsync(ct);
        telemetry.RecordAuthAttempt("password", "reset");
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task<PasswordCheck> VerifyAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var credential = await db.UserCredentials.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (credential is null)
            return new PasswordCheck(false, false, null);   // no password set reads as "wrong password"

        var now = DateTime.UtcNow;
        if (credential.LockedUntil is { } until && until > now)
        {
            // Do not verify while locked — otherwise the lockout is decorative and an attacker keeps
            // learning whether each guess was right.
            telemetry.RecordAuthAttempt("password", "locked");
            return new PasswordCheck(false, true, until);
        }

        var result = hasher.Verify(password, credential.PasswordHash);
        if (!result.Ok)
        {
            credential.FailedAttempts++;
            credential.LastFailedAt = now;
            if (credential.FailedAttempts >= MaxFailedAttempts)
            {
                credential.LockedUntil = now.Add(LockoutDuration);
                credential.FailedAttempts = 0;      // window consumed; next lockout needs a fresh run
                WriteEvent(userId, "password.locked_out", "critical");
                telemetry.RecordSecurityEvent("password.locked_out", "critical");
            }
            await db.SaveChangesAsync(ct);
            telemetry.RecordAuthAttempt("password", "failed");
            return new PasswordCheck(false, credential.LockedUntil is not null, credential.LockedUntil);
        }

        // Correct password: clear the failure run and opportunistically upgrade the hash if policy cost
        // has risen since it was written — this is the only moment the plaintext is available.
        credential.FailedAttempts = 0;
        credential.LockedUntil = null;
        credential.LastSuccessfulAt = now;
        if (result.NeedsRehash)
        {
            credential.PasswordHash = hasher.Hash(password);
            credential.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        telemetry.RecordAuthAttempt("password", "success");
        return new PasswordCheck(true, false, null);
    }

    /// <summary>A throwaway Argon2id hash, computed once, that a login attempt for a non-existent account
    /// is verified against. Verifying it costs the same ~50 ms as a real attempt, so an attacker cannot
    /// tell "no such user" from "wrong password" by timing. Lazily built with the injected hasher so tests
    /// that substitute a cheap hasher stay cheap.</summary>
    private static string? _dummyHash;

    public async Task<PasswordCheck> VerifyForLoginAsync(Guid? userId, string password, CancellationToken ct = default)
    {
        if (userId is Guid uid && await db.UserCredentials.AsNoTracking().AnyAsync(c => c.UserId == uid, ct))
            return await VerifyAsync(uid, password, ct);

        // No account, or an account with no password set yet: burn the same work as a real verification so
        // the timing is identical, then report the same generic failure.
        _dummyHash ??= hasher.Hash("kurx-login-timing-equalizer");
        hasher.Verify(string.IsNullOrEmpty(password) ? "\0" : password, _dummyHash);
        return new PasswordCheck(false, false, null);
    }

    /// <summary>Reuse is detected by verifying the candidate against each stored hash. Comparing hashes
    /// directly would be meaningless — every entry has its own salt.</summary>
    private async Task<bool> IsReusedAsync(Guid userId, string candidate, CancellationToken ct)
    {
        var recent = await db.PasswordHistories.AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(PasswordHistory.RetainedPerUser)
            .Select(h => h.PasswordHash)
            .ToListAsync(ct);

        return recent.Any(h => hasher.Verify(candidate, h).Ok);
    }

    private async Task PruneHistoryAsync(Guid userId, CancellationToken ct)
    {
        // Keep the window bounded so the table cannot grow without limit, and so a very old password
        // becomes acceptable again rather than being barred forever.
        var keep = await db.PasswordHistories.AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(PasswordHistory.RetainedPerUser - 1)      // -1 leaves room for the row being added
            .Select(h => h.Id)
            .ToListAsync(ct);

        await db.PasswordHistories
            .Where(h => h.UserId == userId && !keep.Contains(h.Id))
            .ExecuteDeleteAsync(ct);
    }

    private void WriteEvent(Guid userId, string type, string severity) =>
        db.SecurityEvents.Add(new SecurityEvent { UserId = userId, Type = type, Severity = severity });

    /// <summary>Stable machine-readable codes; the UI maps these to copy. Policy failures are specific
    /// because the user must be able to act on them — unlike authentication failures, which never are.</summary>
    private static string RejectionCode(PasswordRejection r) => r switch
    {
        PasswordRejection.TooShort => "password_too_short",
        PasswordRejection.TooLong => "password_too_long",
        PasswordRejection.Breached => "password_breached",
        PasswordRejection.ContainsIdentifier => "password_contains_identifier",
        PasswordRejection.Reused => "password_reused",
        PasswordRejection.SameAsCurrent => "password_reused",
        _ => "password_invalid",
    };
}
