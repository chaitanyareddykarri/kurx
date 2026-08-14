using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Idempotently seeds the platform participant-role registry (V3 §5.3, Phase 6) into
/// <c>participant_roles</c>. Runs at startup; inserts only missing platform slugs, so a re-run adds nothing
/// and a manual correction is never overwritten.</summary>
public static class ParticipantRoleSeeder
{
    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        var existing = (await db.ParticipantRoles.Where(r => r.OrgId == null).Select(r => r.Slug).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < ParticipantRoleCatalog.Roles.Length; i++)
        {
            var r = ParticipantRoleCatalog.Roles[i];
            if (existing.Contains(r.Slug)) continue;
            db.ParticipantRoles.Add(new ParticipantRole
            {
                Slug = r.Slug, Name = r.Name, Class = r.Class,
                CountsTowardCapacity = r.CountsTowardCapacity, InventorySegment = r.InventorySegment,
                IsPublic = r.IsPublic,
                DefaultPermissionsJson = r.DefaultPermissions.Length > 0 ? JsonSerializer.Serialize(r.DefaultPermissions) : null,
                OrgId = null, Sort = i,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
