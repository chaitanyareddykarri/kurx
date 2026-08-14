namespace Kurx.Application.Abstractions;

/// <summary>A GitHub-style "security" summary for the signed-in user (Phase 2E): what factors and channels
/// are set, and how many of each credential exists. The individual management surfaces (password, email,
/// phone, trusted browsers/devices, passkeys, recovery codes, sessions) each have their own endpoints; this
/// aggregates their counts so the Security Center screen needs one call.</summary>
public record SecurityOverview(
    bool HasPassword, string? Email, bool EmailVerified, string? Phone, bool PhoneVerified,
    int TrustedBrowsers, int TrustedDevices, int Passkeys, int ActiveSessions, int RecoveryCodesRemaining,
    bool StepUpSatisfied, bool CanStepUp);

/// <summary>One entry in the user's own recent security history — their `security_events`, which are theirs
/// to see (unlike the operator-facing forensics feed).</summary>
public record SecurityActivityItem(string Type, string Severity, string? Context, DateTime CreatedAt);

/// <summary>Security Center backend (Phase 2E). Read-side aggregation over the existing auth tables plus the
/// "sign out everywhere" action. Introduces no new credential mechanism.</summary>
public interface ISecurityCenterService
{
    Task<SecurityOverview> GetOverviewAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<SecurityActivityItem>> GetActivityAsync(Guid userId, int limit = 50, CancellationToken ct = default);

    /// <summary>Signs the user out of every session and forgets every trusted browser (the panic button).
    /// Returns how many sessions were revoked.</summary>
    Task<int> SignOutEverywhereAsync(Guid userId, CancellationToken ct = default);
}
