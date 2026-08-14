using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Idempotently seeds system-wide (OrgId=null, EventId=null) design templates so
/// CertificateService (D-035) has something to render against out of the box. Runs once at
/// startup, after migrations — safe to call on every boot since it only inserts missing names.</summary>
public static class DesignTemplateSeeder
{
    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        var existingNames = await db.DesignTemplates
            .Where(t => t.OrgId == null && t.EventId == null)
            .Select(t => t.Name)
            .ToListAsync(ct);
        var existing = new HashSet<string>(existingNames, StringComparer.Ordinal);

        var systemTemplates = new (string Name, TemplateKind Kind, string BaseLayout, string AccentColor)[]
        {
            ("Classic Certificate", TemplateKind.Certificate, "classic-certificate", "#0F172A"),
            ("Modern Certificate", TemplateKind.Certificate, "modern-certificate", "#FF5A3C"),
            ("Classic Invite", TemplateKind.Invite, "classic-invite", "#FF5A3C"),
        };

        foreach (var t in systemTemplates)
        {
            if (existing.Contains(t.Name)) continue;
            db.DesignTemplates.Add(new DesignTemplate
            {
                OrgId = null,
                EventId = null,
                Kind = t.Kind,
                Mode = TemplateMode.System,
                Name = t.Name,
                BaseLayout = t.BaseLayout,
                PlacementsJson = "[]",
                AccentColor = t.AccentColor,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
