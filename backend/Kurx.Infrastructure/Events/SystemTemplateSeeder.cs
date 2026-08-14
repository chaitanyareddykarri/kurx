using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Idempotently seeds the system event templates named in the Phase 2 spec. Runs once at
/// startup, after migrations — safe to call on every boot since it only inserts missing slugs.</summary>
public static class SystemTemplateSeeder
{
    private static readonly string[] Names =
    [
        "Conference", "Workshop", "Hackathon", "Wedding", "Birthday",
        "Sports Tournament", "Concert", "Seminar", "Meetup", "Webinar", "Festival",
    ];

    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        var existingSlugs = await db.EventTemplates.Where(t => t.IsSystem).Select(t => t.Slug).ToListAsync(ct);
        var existing = new HashSet<string>(existingSlugs, StringComparer.Ordinal);

        foreach (var name in Names)
        {
            var slug = Slugify(name);
            if (existing.Contains(slug)) continue;
            var template = new EventTemplate
            {
                Name = name,
                Slug = slug,
                Description = $"{name} template.",
                DefaultSectionsJson = "[]",
                IsSystem = true,
                OrgId = null,
                // V3 §13.1 (Phase 15): system templates are Platform-scoped and immediately usable (Published v1).
                // RootTemplateId = self so the (RootTemplateId, Version) family key is unique per seeded row.
                Scope = TemplateScope.Platform,
                State = TemplateState.Published,
            };
            template.RootTemplateId = template.Id;
            db.EventTemplates.Add(template);
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Slugify(string s) => s.Trim().ToLowerInvariant().Replace(" ", "-");
}
