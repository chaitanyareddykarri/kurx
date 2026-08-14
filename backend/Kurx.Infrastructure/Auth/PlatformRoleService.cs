using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Live platform-role reads/writes against <c>platform_roles</c> (M2, D-040).
/// No caching yet: the lookup is a single index seek on (UserId) returning ≤5 rows (0 for the vast
/// majority of users), consistent with the codebase's existing live-authz reads (OrgService.RoleAsync).
/// A short-TTL cache is a documented future perf optimization, not needed at current scale.</summary>
public class PlatformRoleService(KurxDbContext db) : IPlatformRoleService
{
    public async Task<IReadOnlySet<PlatformRole>> GetRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var roles = await db.PlatformRoles.AsNoTracking()
            .Where(r => r.UserId == userId && (r.ExpiresAt == null || r.ExpiresAt > now))
            .Select(r => r.Role)
            .ToListAsync(ct);
        return roles.ToHashSet();
    }

    public async Task<bool> HasRoleAsync(Guid userId, PlatformRole role, CancellationToken ct = default)
    {
        var roles = await GetRolesAsync(userId, ct);
        return roles.Contains(PlatformRole.SuperAdmin) || roles.Contains(role);
    }

    public async Task GrantAsync(Guid userId, PlatformRole role, Guid? grantedBy, DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        var existing = await db.PlatformRoles.FirstOrDefaultAsync(r => r.UserId == userId && r.Role == role, ct);
        if (existing is not null)
        {
            existing.GrantedBy = grantedBy;
            existing.GrantedAt = DateTime.UtcNow;
            existing.ExpiresAt = expiresAt;
        }
        else
        {
            db.PlatformRoles.Add(new PlatformRoleAssignment
            {
                UserId = userId, Role = role, GrantedBy = grantedBy, ExpiresAt = expiresAt,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(Guid userId, PlatformRole role, CancellationToken ct = default)
        => await db.PlatformRoles.Where(r => r.UserId == userId && r.Role == role).ExecuteDeleteAsync(ct);
}
