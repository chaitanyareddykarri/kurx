using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M2 — writes the D12 archetype × capability matrix into
/// <c>archetype_capability_defaults</c>, one row per (archetype, capability) pair including the
/// <see cref="CapabilityRule.Unsupported"/> cells.
///
/// <para>Storing the negatives is deliberate. <see cref="KindCapabilityDefault"/> stored only the positive
/// cells and read absence as "available but off", which is why the old model could never say "a Workshop
/// may never enable Leaderboard". A complete grid also means the admin console can show the matrix, and a
/// capability added to the catalog without a matrix decision shows up as an explicit Unsupported row rather
/// than silently defaulting to permitted.</para>
///
/// <para>Idempotent, and it re-syncs: a row whose rule no longer matches the catalog is corrected rather
/// than left stale, because the code matrix is the transcription of D12 and D12 is frozen. Rows for an
/// archetype or capability that no longer exists are deleted.</para></summary>
public static class ArchetypeCapabilitySeeder
{
    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        var archetypes = CapabilityCatalog.ArchetypeDefaults.Keys.ToList();
        var capabilities = CapabilityCatalog.Capabilities.Select(c => c.Slug).ToList();

        var want = new Dictionary<(string, string), CapabilityRule>();
        foreach (var a in archetypes)
        {
            var d = CapabilityCatalog.ArchetypeDefaults[a];
            foreach (var c in capabilities)
            {
                var rule = d.Required.Contains(c, StringComparer.Ordinal) ? CapabilityRule.Required
                         : d.Optional.Contains(c, StringComparer.Ordinal) ? CapabilityRule.Optional
                         : CapabilityRule.Unsupported;
                want[(a, c)] = rule;
            }
        }

        var existing = await db.ArchetypeCapabilityDefaults.ToListAsync(ct);
        var byKey = existing.ToDictionary(r => (r.ArchetypeSlug, r.CapabilitySlug));
        var dirty = false;

        foreach (var ((a, c), rule) in want)
        {
            if (byKey.TryGetValue((a, c), out var row))
            {
                if (row.Rule == rule) continue;
                row.Rule = rule;                        // re-sync: D12 is the source of truth, not the row
                dirty = true;
            }
            else
            {
                db.ArchetypeCapabilityDefaults.Add(new ArchetypeCapabilityDefault
                {
                    ArchetypeSlug = a, CapabilitySlug = c, Rule = rule,
                });
                dirty = true;
            }
        }

        // Drop rows for archetypes/capabilities that no longer exist, so the grid never carries orphans.
        foreach (var row in existing)
            if (!want.ContainsKey((row.ArchetypeSlug, row.CapabilitySlug)))
            {
                db.ArchetypeCapabilityDefaults.Remove(row);
                dirty = true;
            }

        if (dirty) await db.SaveChangesAsync(ct);
    }
}
