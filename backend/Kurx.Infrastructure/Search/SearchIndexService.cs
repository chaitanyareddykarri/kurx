using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kurx.Infrastructure.Search;

/// <summary>Maintains the V3 §15 discovery index (Phase 16). The projector is the ONLY writer of
/// <c>event_search_documents</c>: an event write enqueues a <c>search.reindex</c> outbox message in its own
/// transaction, and the outbox dispatcher calls <see cref="ProjectAsync"/> — never a synchronous dual-write from the
/// event path. Projection is idempotent and rebuilds each document from scratch, so an at-least-once redelivery is
/// harmless. Only Published + Public + not-deleted events are indexed, which is what makes the eligibility feed and
/// public discovery unable to leak an internal event's existence (404-not-403).</summary>
public class SearchIndexService(KurxDbContext db) : ISearchIndexService
{
    private const int VelocityWindowDays = 7;   // recent-view window that feeds the velocity ranking signal

    public async Task ProjectAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        // Discoverable candidates only. Anything else (draft, unlisted, internal, private, deleted, gone,
        // or admin-suspended/hidden — D-186) is removed from the index — so it can never surface in
        // discovery or the eligibility feed.
        // Canonical exposure rule (D-266 M3): anything not publicly visible is de-indexed. Using the shared
        // predicate rather than an inline comparison is what stops this drifting from the query-side guards
        // — the drift the Step 4 audit found, where an event was hidden from search and shown on profiles.
        if (ev is null || ev.Status != EventStatus.Published || !EventExposure.IsPubliclyVisible(ev)
            || ev.IsSuspended || ev.IsHidden)
        {
            await db.EventSearchDocuments.Where(d => d.EventId == eventId).ExecuteDeleteAsync(ct);
            return;
        }

        var tags = await db.EventTags.Where(t => t.EventId == eventId)
            .Join(db.Tags, et => et.TagId, t => t.Id, (et, t) => t.Name).ToListAsync(ct);
        var kindName = ev.KindSlug is null ? null
            : await db.EventKinds.Where(k => k.Slug == ev.KindSlug).Select(k => k.Name).FirstOrDefaultAsync(ct);
        var aliases = ev.KindSlug is null ? new List<string>()
            : await db.KindAliases.Where(a => a.KindSlug == ev.KindSlug).Select(a => a.Alias).ToListAsync(ct);
        var speakers = await db.EventSpeakers.Where(s => s.EventId == eventId)
            .Join(db.Speakers, es => es.SpeakerId, s => s.Id, (es, s) => s.Name).ToListAsync(ct);
        var orgName = await db.Organizations.Where(o => o.Id == ev.RepresentingOrgId).Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "";
        var unitName = ev.OrgUnitId is null ? "" :
            (await db.OrgUnits.Where(u => u.Id == ev.OrgUnitId).Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "");
        var seriesMode = ev.SeriesId is null ? null :
            (await db.EventSeries.Where(s => s.Id == ev.SeriesId).Select(s => (SeriesMode?)s.Mode).FirstOrDefaultAsync(ct))?.ToString();
        var isPaid = await db.TicketTypes.AnyAsync(t => t.EventId == eventId && t.PricePaise > 0 && t.DeletedAt == null, ct);
        var hasRule = await db.AudienceRules.AnyAsync(r => r.EventId == eventId, ct);

        var titleText = Join(ev.Title, ev.Subtitle);
        var bodyText = Join(ev.Description, string.Join(' ', tags), kindName, string.Join(' ', aliases),
            string.Join(' ', speakers), ev.VenueName, orgName, unitName, ev.City);
        var fuzzyText = Join(ev.Title, string.Join(' ', tags), kindName, string.Join(' ', aliases)).ToLowerInvariant();

        var doc = await db.EventSearchDocuments.FirstOrDefaultAsync(d => d.EventId == eventId, ct);
        var isNew = doc is null;
        doc ??= new EventSearchDocument { EventId = eventId };

        doc.TitleText = titleText;
        doc.BodyText = bodyText;
        doc.FuzzyText = fuzzyText;
        doc.OrgId = ev.RepresentingOrgId;
        doc.OrgUnitId = ev.OrgUnitId;
        doc.KindSlug = ev.KindSlug;
        doc.CategoryId = ev.CategoryId;
        doc.TypeId = ev.TypeId;
        doc.EventMode = ev.EventMode.ToString();
        doc.IsPaid = isPaid;
        doc.Language = ev.Language;
        doc.City = ev.City;
        doc.Country = ev.Country;
        doc.Lat = ev.Lat;
        doc.Lng = ev.Lng;
        doc.StartsAt = ev.StartsAt;
        doc.EndsAt = ev.EndsAt;
        doc.ListedStandalone = ev.ListedStandalone;
        doc.ParentEventId = ev.ParentEventId;
        doc.SeriesId = ev.SeriesId;
        doc.SeriesMode = seriesMode;
        doc.HasAudienceRule = hasRule;
        doc.IsFeatured = ev.IsFeatured;
        doc.RecentViewCount = await RecentViewsAsync(eventId, ct);
        doc.ConversionCount = await ConversionsAsync(eventId, ct);
        doc.EventCreatedAt = ev.CreatedAt;
        doc.IsSeriesPrimary = true;   // authoritative RECURRING collapse is set by RefreshSignalsAsync; non-recurring stays true
        doc.IndexedAt = DateTime.UtcNow;

        if (isNew) db.EventSearchDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        // A new/updated occurrence can change which occurrence a RECURRING series collapses to — recompute its primary.
        if (ev.SeriesId is not null && seriesMode == nameof(SeriesMode.Recurring))
            await RecomputeSeriesPrimaryAsync(ev.SeriesId.Value, ct);
    }

    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        var ids = await db.Events
            .Where(e => e.Status == EventStatus.Published && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed && e.DeletedAt == null
                && !db.EventSearchDocuments.Any(d => d.EventId == e.Id))
            .Select(e => e.Id).ToListAsync(ct);
        foreach (var id in ids) await ProjectAsync(id, ct);
        return ids.Count;
    }

    public async Task<int> RefreshSignalsAsync(CancellationToken ct = default)
    {
        // Set-based (M3): three statements total — no per-document queries, no full in-memory load — so the job scales
        // to a large index. Behaviour is identical to the per-document version.
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE event_search_documents d SET ""RecentViewCount"" = COALESCE((
                SELECT count(*) FROM event_views v
                WHERE v.""EventId"" = d.""EventId"" AND v.""ViewedAt"" >= now() - (@days * interval '1 day')), 0)",
            new NpgsqlParameter("days", VelocityWindowDays));   // velocity over the recent-view window
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE event_search_documents d SET ""ConversionCount"" = COALESCE((
                SELECT count(*) FROM registrations r
                WHERE r.""EventId"" = d.""EventId"" AND r.""State"" <> 'Cancelled'), 0)");   // conversion = active registrations
        // RECURRING one-listing (§13.2/§15): mark exactly one occurrence per series primary — next upcoming, else the
        // most recent past — with a single window-function update.
        await db.Database.ExecuteSqlRawAsync(@"
            WITH ranked AS (
                SELECT ""EventId"", row_number() OVER (
                    PARTITION BY ""SeriesId""
                    ORDER BY (""StartsAt"" >= now()) DESC,
                             CASE WHEN ""StartsAt"" >= now() THEN ""StartsAt"" END ASC NULLS LAST,
                             ""StartsAt"" DESC) AS rn
                FROM event_search_documents
                WHERE ""SeriesId"" IS NOT NULL AND ""SeriesMode"" = 'Recurring')
            UPDATE event_search_documents d SET ""IsSeriesPrimary"" = (r.rn = 1)
            FROM ranked r WHERE d.""EventId"" = r.""EventId""");
        return await db.EventSearchDocuments.CountAsync(ct);
    }

    // The RECURRING one-listing rule (§13.2/§15): exactly one occurrence is discoverable — the next upcoming one, or
    // the most recent past occurrence when none remain upcoming. Every other occurrence is collapsed out of discovery.
    private async Task RecomputeSeriesPrimaryAsync(Guid seriesId, CancellationToken ct)
    {
        var members = await db.EventSearchDocuments.Where(d => d.SeriesId == seriesId).ToListAsync(ct);
        if (members.Count == 0) return;
        var now = DateTime.UtcNow;
        var upcoming = members.Where(m => m.StartsAt >= now).OrderBy(m => m.StartsAt).FirstOrDefault();
        var primary = upcoming ?? members.OrderByDescending(m => m.StartsAt).First();
        foreach (var m in members) m.IsSeriesPrimary = m.EventId == primary.EventId;
        await db.SaveChangesAsync(ct);
    }

    private Task<int> RecentViewsAsync(Guid eventId, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-VelocityWindowDays);
        return db.EventViews.CountAsync(v => v.EventId == eventId && v.ViewedAt >= since, ct);
    }

    private Task<int> ConversionsAsync(Guid eventId, CancellationToken ct)
        => db.Registrations.CountAsync(r => r.EventId == eventId && r.State != RegistrationState.Cancelled, ct);

    private static string Join(params string?[] parts)
        => string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
