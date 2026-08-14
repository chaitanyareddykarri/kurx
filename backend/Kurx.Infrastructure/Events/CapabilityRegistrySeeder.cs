using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Idempotently seeds the V3 Capability registry (Event Architecture V3 §11 + §19, Phase 2): the
/// ~45 capabilities into <c>capabilities</c> and the Kind×Capability default matrix into
/// <c>kind_capability_defaults</c>. Runs at startup AFTER <see cref="KindRegistrySeeder"/> (the defaults
/// key on Kind slugs). Safe on every boot — inserts only missing rows, so a re-run adds nothing and a
/// manual DB correction is never overwritten.</summary>
public static class CapabilityRegistrySeeder
{
    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        // 1) The ~45 capabilities (closed catalog, V3 §11/§19).
        var existing = (await db.Capabilities.Select(c => c.Slug).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < CapabilityCatalog.Capabilities.Length; i++)
        {
            var c = CapabilityCatalog.Capabilities[i];
            if (existing.Contains(c.Slug)) continue;
            db.Capabilities.Add(new Capability
            {
                Slug = c.Slug, Name = c.Name, GroupSlug = c.Group, IsUniversal = c.Universal,
                WorkspaceTab = c.WorkspaceTab,
                DependsOnJson = JsonSerializer.Serialize(c.DependsOn),
                AvailableModesJson = JsonSerializer.Serialize(c.AvailableModes),
                Sort = i,
            });
        }
        await db.SaveChangesAsync(ct);

        // 2) D-266 M2: the Kind x capability defaults are retired. The archetype matrix
        // (archetype_capability_defaults) is the only capability authority now, so leaving these rows
        // behind would be a second, stale source of truth for the same question.
        //
        // Also drop capability rows whose slug is no longer an event capability. The 27 removed in M2 were
        // Registration / Ticketing / Invitation / Scheduling / Eligibility / Finance / Infrastructure
        // concerns; their subsystems own them, and a leftover row here would let a client believe the
        // capability engine still governs them.
        await db.KindCapabilityDefaults.ExecuteDeleteAsync(ct);

        var live = CapabilityCatalog.Capabilities.Select(c => c.Slug).ToList();
        await db.Capabilities.Where(c => !live.Contains(c.Slug)).ExecuteDeleteAsync(ct);
        await db.EventCapabilities.Where(c => !live.Contains(c.CapabilitySlug)).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
    }
}
