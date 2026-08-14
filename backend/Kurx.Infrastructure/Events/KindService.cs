using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Read + resolution over the V3 Kind registry (Phase 1). Resolution reads the <c>kind_aliases</c>
/// table so the 145→20 mapping is correctable in the database without a code change; the category-level
/// fallback (for events with no Type) is the only static map and never affects the alias count.</summary>
public class KindService(KurxDbContext db) : IKindService
{
    public async Task<IReadOnlyList<KindView>> ListAsync(CancellationToken ct = default)
    {
        var kinds = await db.EventKinds.AsNoTracking().OrderBy(k => k.Sort).ToListAsync(ct);
        var aliasesByKind = (await db.KindAliases.AsNoTracking()
                .Select(a => new { a.KindSlug, a.Alias }).ToListAsync(ct))
            .GroupBy(a => a.KindSlug)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Alias).OrderBy(n => n).ToList());

        return kinds.Select(k => new KindView(k.Slug, k.Name, k.GroupSlug, k.GroupName, k.Sort,
            aliasesByKind.GetValueOrDefault(k.Slug, Array.Empty<string>()))).ToList();
    }

    public async Task<string?> ResolveKindSlugAsync(Guid? typeId, Guid? categoryId, CancellationToken ct = default)
    {
        if (typeId is not null)
        {
            var typeSlug = await db.EventCategories.AsNoTracking()
                .Where(c => c.Id == typeId).Select(c => c.Slug).FirstOrDefaultAsync(ct);
            if (typeSlug is not null)
            {
                var kind = await db.KindAliases.AsNoTracking()
                    .Where(a => a.NormalizedAlias == typeSlug).Select(a => a.KindSlug).FirstOrDefaultAsync(ct);
                if (kind is not null) return kind;
            }
        }

        if (categoryId is not null)
        {
            var catSlug = await db.EventCategories.AsNoTracking()
                .Where(c => c.Id == categoryId).Select(c => c.Slug).FirstOrDefaultAsync(ct);
            if (catSlug is not null && KindCatalog.CategorySlugToKind.TryGetValue(catSlug, out var kind))
                return kind;
        }

        return null;
    }

    public async Task<int> BackfillEventKindsAsync(CancellationToken ct = default)
    {
        var events = await db.Events.Where(e => e.KindSlug == null).ToListAsync(ct);
        if (events.Count == 0) return 0;

        var updated = 0;
        foreach (var ev in events)
        {
            var kind = await ResolveKindSlugAsync(ev.TypeId, ev.CategoryId, ct);
            if (kind is null) continue;
            ev.KindSlug = kind;
            updated++;
        }
        if (updated > 0) await db.SaveChangesAsync(ct);
        return updated;
    }
}
