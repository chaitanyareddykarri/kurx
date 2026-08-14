using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kurx.Infrastructure.Auth;

/// <summary>Trusted browsers — factor 2 only (D-127, INV-A). A trusted browser skips the trusted-device
/// approval for a bounded window; it never skips the password. Only the SHA-256 of the opaque cookie token
/// is stored (<see cref="TokenService.Sha256"/>), so a database disclosure yields nothing replayable — the
/// same posture as refresh tokens. The login flow (Phase 2B) issues the cookie and calls
/// <see cref="VerifyAsync"/>; this service owns the lifecycle and the cascade-revocation invariant.</summary>
public class TrustedBrowserService(KurxDbContext db, IConfiguration config) : ITrustedBrowserService
{
    private int TtlDays => int.TryParse(config["AUTH_TRUSTED_BROWSER_TTL_DAYS"], out var v) && v > 0 ? v : 30;

    public async Task<TrustedBrowserIssued> IssueAsync(Guid userId, TrustedBrowserContext ctx, CancellationToken ct = default)
    {
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        var expires = DateTime.UtcNow.AddDays(TtlDays);
        db.TrustedBrowsers.Add(new TrustedBrowser
        {
            UserId = userId,
            TokenHash = TokenService.Sha256(raw),
            Label = ctx.Label,
            Browser = ctx.Browser,
            OperatingSystem = ctx.OperatingSystem,
            UserAgent = ctx.UserAgent,
            Ip = ctx.Ip,
            ApproxLocation = ctx.ApproxLocation,
            ExpiresAt = expires,
        });
        db.SecurityEvents.Add(new SecurityEvent { UserId = userId, Type = "trusted_browser.trusted", Severity = "info" });
        await db.SaveChangesAsync(ct);
        return new TrustedBrowserIssued(raw, expires);
    }

    public async Task<bool> VerifyAsync(Guid userId, string? rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawToken)) return false;
        var hash = TokenService.Sha256(rawToken);
        var now = DateTime.UtcNow;
        // Lookup is by the token's own SHA-256 (a 256-bit value), the same shape as refresh-token
        // verification: an equality lookup on a high-entropy hash, so app-side timing carries no signal.
        var browser = await db.TrustedBrowsers.FirstOrDefaultAsync(
            b => b.UserId == userId && b.TokenHash == hash && b.RevokedAt == null && b.ExpiresAt > now, ct);
        if (browser is null) return false;
        browser.LastUsedAt = now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TrustedBrowserView>> ListAsync(Guid userId, string? currentRawToken = null,
        CancellationToken ct = default)
    {
        var currentHash = string.IsNullOrEmpty(currentRawToken) ? null : TokenService.Sha256(currentRawToken);
        var now = DateTime.UtcNow;
        var rows = await db.TrustedBrowsers.AsNoTracking()
            .Where(b => b.UserId == userId && b.RevokedAt == null && b.ExpiresAt > now)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id, b.Label, b.Browser, b.OperatingSystem, b.Ip, b.ApproxLocation,
                b.TokenHash, b.CreatedAt, b.LastUsedAt, b.ExpiresAt,
            })
            .ToListAsync(ct);
        return rows
            .Select(b => new TrustedBrowserView(b.Id, b.Label, b.Browser, b.OperatingSystem, b.Ip, b.ApproxLocation,
                currentHash != null && b.TokenHash == currentHash, b.CreatedAt, b.LastUsedAt, b.ExpiresAt))
            .ToList();
    }

    public async Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        // Scoped by userId so one user can never revoke another's browser; unknown id is 404, not 403 (D-018).
        var browser = await db.TrustedBrowsers.FirstOrDefaultAsync(
            b => b.Id == id && b.UserId == userId && b.RevokedAt == null, ct);
        if (browser is null)
            return new ServiceResult<bool>(false, "not_found");

        browser.RevokedAt = DateTime.UtcNow;
        browser.RevokeReason = "user_revoked";
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId, Type = "trusted_browser.revoked", Severity = "warning",
            ContextJson = $"{{\"trustedBrowserId\":\"{id}\"}}",
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "trusted_browser.revoked",
            Entity = "trusted_browsers", EntityId = id,
        });
        await db.SaveChangesAsync(ct);
        return new ServiceResult<bool>(true, Value: true);
    }

    public async Task RevokeAllForCascadeAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var count = await db.TrustedBrowsers
            .Where(b => b.UserId == userId && b.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.RevokedAt, now)
                .SetProperty(b => b.RevokeReason, reason), ct);
        // No SaveChanges by design: the security event is staged into the caller's transaction (the
        // password change / reset / recovery), so it is recorded only if its cause is.
        if (count > 0)
            db.SecurityEvents.Add(new SecurityEvent
            {
                UserId = userId, Type = "trusted_browser.cascade_revoked", Severity = "warning",
                ContextJson = $"{{\"reason\":\"{reason}\",\"count\":{count}}}",
            });
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
