using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <summary>Live resolution of a user's platform roles from <c>platform_roles</c> (M2, D-040).
/// Always queried per request — platform authority is never carried in a JWT — so a revoked role
/// stops working on the next request. Grant/Revoke are used by the admin console (M12) and tests.</summary>
public interface IPlatformRoleService
{
    /// <summary>Active (non-expired) platform roles for the user. Empty for the vast majority of users.</summary>
    Task<IReadOnlySet<PlatformRole>> GetRolesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>True if the user holds <paramref name="role"/> or SuperAdmin (which implies all).</summary>
    Task<bool> HasRoleAsync(Guid userId, PlatformRole role, CancellationToken ct = default);

    Task GrantAsync(Guid userId, PlatformRole role, Guid? grantedBy, DateTime? expiresAt = null, CancellationToken ct = default);
    Task RevokeAsync(Guid userId, PlatformRole role, CancellationToken ct = default);
}
