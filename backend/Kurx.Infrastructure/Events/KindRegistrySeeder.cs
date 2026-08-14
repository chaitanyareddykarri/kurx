using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Idempotently seeds the V3 Kind registry (Event Architecture V3 §2, Phase 1): the 20 Kinds
/// into <c>event_kinds</c> and the 145 legacy taxonomy Type names into <c>kind_aliases</c>. Runs at
/// startup AFTER <see cref="EventTaxonomySeeder"/> — the aliases derive from the seeded Type rows so an
/// alias slug always matches the taxonomy slug it backfills from. Safe on every boot: it only inserts
/// missing slugs, so a re-run adds nothing and a manual DB correction is never overwritten.</summary>
public static class KindRegistrySeeder
{
    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        // 1) The 20 Kinds (closed catalog, V3 §2).
        var existingKinds = (await db.EventKinds.Select(k => k.Slug).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < KindCatalog.Kinds.Length; i++)
        {
            var k = KindCatalog.Kinds[i];
            if (existingKinds.Contains(k.Slug)) continue;
            db.EventKinds.Add(new EventKind
            {
                Slug = k.Slug, Name = k.Name, GroupSlug = k.GroupSlug, GroupName = k.GroupName, Sort = i,
            });
        }
        await db.SaveChangesAsync(ct);

        // 2) The 145 aliases — one per seeded taxonomy Type row, mapped by its exact name. The alias's
        //    NormalizedAlias is the Type's slug, so KindService can resolve an event's KindSlug from its
        //    TypeId's slug via this table (data-driven, correctable without code).
        var types = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Type)
            .Select(c => new { c.Name, c.Slug })
            .ToListAsync(ct);
        var existingAliases = (await db.KindAliases.Select(a => a.NormalizedAlias).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var t in types)
        {
            if (existingAliases.Contains(t.Slug)) continue;
            // Every seeded Type name is covered by the catalog; an unmapped name is skipped (it would
            // then be caught by KindRegistryTests, which asserts the exact 145 count).
            if (!KindCatalog.TypeNameToKind.TryGetValue(t.Name, out var kindSlug)) continue;
            db.KindAliases.Add(new KindAlias { Alias = t.Name, NormalizedAlias = t.Slug, KindSlug = kindSlug });
        }
        await db.SaveChangesAsync(ct);
    }
}
