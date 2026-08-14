namespace Kurx.Application.Abstractions;

public record TrustedBrowserView(Guid Id, string? Label, string? Browser, string? OperatingSystem,
    string? Ip, string? ApproxLocation, bool IsCurrent, DateTime CreatedAt, DateTime? LastUsedAt, DateTime ExpiresAt);

/// <summary>Request context captured when a browser is trusted — supplied by the login flow (Phase 2B),
/// which owns <c>HttpContext</c>. All fields are forensic and optional.</summary>
public record TrustedBrowserContext(string? Label = null, string? Browser = null, string? OperatingSystem = null,
    string? UserAgent = null, string? Ip = null, string? ApproxLocation = null);

/// <summary>The raw cookie token — handed to the browser exactly once — and its expiry.</summary>
public record TrustedBrowserIssued(string Token, DateTime ExpiresAt);

/// <summary>Trusted browsers (D-127, Phase 2A). A browser the user chose to trust satisfies <b>factor 2
/// only</b> — it never bypasses the password (<b>INV-A</b>); a cookie that skipped the password would be a
/// bearer credential for the whole account. Only the SHA-256 of the opaque cookie token is stored, so a
/// database disclosure yields nothing replayable — the same reasoning as refresh tokens. The login flow
/// (Phase 2B) issues the cookie and calls <see cref="VerifyAsync"/>; this service owns the lifecycle and
/// the cascade-revocation invariant.</summary>
public interface ITrustedBrowserService
{
    Task<TrustedBrowserIssued> IssueAsync(Guid userId, TrustedBrowserContext context, CancellationToken ct = default);

    /// <summary>True iff the presented raw token matches an active (unexpired, unrevoked) trusted browser
    /// for the user; touches <c>LastUsedAt</c> on a hit. Factor 2 only — the caller still enforces the
    /// password.</summary>
    Task<bool> VerifyAsync(Guid userId, string? rawToken, CancellationToken ct = default);

    Task<IReadOnlyList<TrustedBrowserView>> ListAsync(Guid userId, string? currentRawToken = null, CancellationToken ct = default);

    /// <summary>Revokes one browser the caller owns. Unknown id is 404, not 403 (D-018).</summary>
    Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>Cascade on a re-secure event (password change / reset / recovery): revoke every trusted
    /// browser and stage a security event. Deliberately does <b>not</b> call SaveChanges — it composes into
    /// the caller's transaction, so the cascade cannot be recorded without the cause that triggered it.</summary>
    Task RevokeAllForCascadeAsync(Guid userId, string reason, CancellationToken ct = default);
}
