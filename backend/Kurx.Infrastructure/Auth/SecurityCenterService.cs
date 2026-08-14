using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Security Center backend (Phase 2E). Aggregates the existing auth tables for the overview screen,
/// serves the user their own recent security history, and implements "sign out everywhere". Reuses the
/// password, recovery and step-up services — no new credential mechanism.</summary>
public class SecurityCenterService(KurxDbContext db, IPasswordService passwords, IRecoveryCodeService recovery,
    IStepUpService stepUp) : ISecurityCenterService
{
    public async Task<SecurityOverview> GetOverviewAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var now = DateTime.UtcNow;

        var hasPassword = await passwords.HasPasswordAsync(userId, ct);
        var browsers = await db.TrustedBrowsers.CountAsync(b => b.UserId == userId && b.RevokedAt == null && b.ExpiresAt > now, ct);
        var devices = await db.TrustedDevices.CountAsync(
            d => d.UserId == userId && d.DeletedAt == null && d.LifecycleState == DeviceLifecycleState.Trusted, ct);
        var deviceIds = db.TrustedDevices.Where(d => d.UserId == userId && d.DeletedAt == null).Select(d => d.Id);
        var passkeys = await db.DeviceCredentials.CountAsync(
            c => c.RevokedAt == null && c.CredentialType == DeviceCredentialType.WebAuthn && deviceIds.Contains(c.TrustedDeviceId), ct);
        var sessions = await db.AuthSessions.CountAsync(s => s.UserId == userId && s.RevokedAt == null, ct);
        var recoveryRemaining = await recovery.RemainingAsync(userId, ct);
        var stepUpStatus = await stepUp.StatusAsync(userId, ct);
        var canStepUp = await stepUp.CanStepUpAsync(userId, ct);

        var phone = user.PhoneE164 ?? (string.IsNullOrEmpty(user.Phone) ? null : user.Phone);
        return new SecurityOverview(hasPassword, user.Email, user.EmailVerifiedAt is not null, phone, phone is not null,
            browsers, devices, passkeys, sessions, recoveryRemaining, stepUpStatus.Satisfied, canStepUp);
    }

    public async Task<IReadOnlyList<SecurityActivityItem>> GetActivityAsync(Guid userId, int limit = 50, CancellationToken ct = default)
    {
        var rows = await db.SecurityEvents.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(e => new { e.Type, e.Severity, e.ContextJson, e.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(e => new SecurityActivityItem(e.Type, e.Severity, e.ContextJson, e.CreatedAt)).ToList();
    }

    public async Task<int> SignOutEverywhereAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var count = await db.AuthSessions.Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokeReason, "user_signed_out_all"), ct);
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        await db.TrustedBrowsers.Where(b => b.UserId == userId && b.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.RevokedAt, now).SetProperty(b => b.RevokeReason, "user_signed_out_all"), ct);

        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "session.sign_out_all", Severity = "warning", ContextJson = $"{{\"sessions\":{count}}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "auth.sign_out_all", Entity = "users", EntityId = userId,
        });
        await db.SaveChangesAsync(ct);
        return count;
    }
}
