using System.Linq.Expressions;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kurx.Infrastructure.Search;

/// <summary>The single canonical discovery implementation (V3 §15, Phase 16). Reads the outbox-fed
/// <c>event_search_documents</c> index: raw SQL over the stored <c>search_vector</c> (FTS, GIN-indexed) + trigram on
/// <c>FuzzyText</c> for the text path, LINQ over the typed columns for the filter/feed paths. Ranking is deterministic
/// — recency (start + creation), velocity (windowed views), conversion (registrations), proximity (haversine, only
/// when the caller supplies a location) — replacing raw <c>ViewCount DESC</c>; affinity is deferred. Collapse rules
/// (§3.4/§13.2) drop non-standalone sub-events and every RECURRING occurrence but the primary one. Summaries are read
/// from the authoritative events row, so the index never serves a stale title/slug.</summary>
public class SearchService(KurxDbContext db, IAudienceService audience, IStorage storage) : ISearchService
{
    /// <summary>Presigns the banner on each card. A storage key is not fetchable, so a client handed only
    /// the key renders nothing — which is what every event card did (D-302). Presigning is HMAC over the
    /// key, not a round-trip, and the page is capped at 50, so this stays cheap.</summary>
    internal static async Task<IReadOnlyList<EventSummary>> WithBannerUrlsAsync(
        IStorage storage, List<EventSummary> items, CancellationToken ct)
    {
        for (var i = 0; i < items.Count; i++)
            if (!string.IsNullOrWhiteSpace(items[i].BannerKey))
                items[i] = items[i] with { BannerUrl = await storage.PresignGetAsync(items[i].BannerKey!, null, ct) };
        return items;
    }

    public async Task<(IReadOnlyList<EventSummary> Items, int Total)> SearchAsync(EventListFilter filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize, 1, 50);
        // One ranked query for both the text and no-text cases (M4): recency + velocity + conversion + proximity always
        // apply; the FTS/trigram term contributes only when `q` is present. So `?lat=&lng=` ranks by distance with or
        // without a query — every discovery path shares the same deterministic model.
        var (ids, total) = await RankedIdsAsync(filter, (page - 1) * size, size, ct);
        return (await SummariesAsync(ids, ct), total);
    }

    public async Task<IReadOnlyList<EventSummary>> UpcomingAsync(int limit, CancellationToken ct = default)
    {
        var ids = await BaseDiscoverable().Where(d => d.StartsAt > DateTime.UtcNow)
            .OrderBy(d => d.StartsAt).Take(Clamp(limit)).Select(d => d.EventId).ToListAsync(ct);
        return await SummariesAsync(ids, ct);
    }

    public async Task<IReadOnlyList<EventSummary>> LatestAsync(int limit, CancellationToken ct = default)
    {
        var ids = await BaseDiscoverable()
            .OrderByDescending(d => d.EventCreatedAt).Take(Clamp(limit)).Select(d => d.EventId).ToListAsync(ct);
        return await SummariesAsync(ids, ct);
    }

    public async Task<IReadOnlyList<EventSummary>> FeaturedAsync(int limit, CancellationToken ct = default)
    {
        // Curation stays the IsFeatured flag; the index carries it so featured also respects the collapse rules.
        var ids = await BaseDiscoverable().Where(d => d.IsFeatured)
            .OrderBy(d => d.StartsAt).Take(Clamp(limit)).Select(d => d.EventId).ToListAsync(ct);
        return await SummariesAsync(ids, ct);
    }

    public async Task<IReadOnlyList<EventSummary>> TrendingAsync(int limit, CancellationToken ct = default)
    {
        // The same deterministic ranking (velocity + conversion + recency) — replaces raw ViewCount DESC.
        var (ids, _) = await RankedIdsAsync(Blank(), 0, Clamp(limit), ct);
        return await SummariesAsync(ids, ct);
    }

    public async Task<IReadOnlyList<EventSummary>> RelatedAsync(Guid eventId, int limit, CancellationToken ct = default)
    {
        var seed = await db.EventSearchDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.EventId == eventId, ct);
        if (seed is null) return [];
        // Shared kind / org / unit; ranked by how much they overlap, then the deterministic signals.
        var ids = await BaseDiscoverable().Where(d => d.EventId != eventId
                && (d.KindSlug == seed.KindSlug || d.OrgId == seed.OrgId || d.OrgUnitId == seed.OrgUnitId))
            .OrderByDescending(d => (d.KindSlug == seed.KindSlug ? 2 : 0) + (d.OrgId == seed.OrgId ? 2 : 0) + (d.OrgUnitId == seed.OrgUnitId ? 1 : 0))
            .ThenByDescending(d => d.RecentViewCount * 2 + d.ConversionCount * 3)
            .ThenBy(d => d.StartsAt)
            .Take(Clamp(limit)).Select(d => d.EventId).ToListAsync(ct);
        return await SummariesAsync(ids, ct);
    }

    public async Task<IReadOnlyList<EventSummary>> EligibleForAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var take = Clamp(limit);
        // Rank a candidate window with the same model, then keep those the user may actually register for: open events
        // (no rule) pass directly; a rule-gated event is included only when the audience rule allows this user. Internal
        // events are never indexed, so this can never leak their existence (404-not-403).
        var (candidates, _) = await RankedIdsAsync(Blank(), 0, take * 4, ct);
        var hasRule = await db.EventSearchDocuments.AsNoTracking().Where(d => candidates.Contains(d.EventId))
            .Select(d => new { d.EventId, d.HasAudienceRule }).ToDictionaryAsync(x => x.EventId, x => x.HasAudienceRule, ct);
        var allowed = new List<Guid>(take);
        foreach (var id in candidates)   // preserve rank order
        {
            if (!hasRule.GetValueOrDefault(id)) allowed.Add(id);
            else if ((await audience.EvaluateAsync(userId, id, ct)).Allowed) allowed.Add(id);
            if (allowed.Count >= take) break;
        }
        return await SummariesAsync(allowed, ct);
    }

    // ── shared query building ──────────────────────────────────────────────────────
    // Every discovery path starts here: the index holds only Published + Public events; drop non-standalone sub-events
    // (§3.4 rule 2) and every RECURRING occurrence but the primary one (§13.2 — the one-listing collapse).
    private IQueryable<EventSearchDocument> BaseDiscoverable()
        => db.EventSearchDocuments.AsNoTracking().Where(d => d.ListedStandalone
            && (d.SeriesId == null || d.SeriesMode != nameof(SeriesMode.Recurring) || d.IsSeriesPrimary));

    // An all-null filter — the ranked feed/browse inputs (Trending, EligibleFor) share the one ranked query.
    private static EventListFilter Blank() => new(null, null, null, null, null, null, null, null, null, 1, 50);

    // The one canonical ranked query (M4). WHERE = the collapse base + optional filters + (when `q` is present) an
    // index-usable text disjunct: `@@` on the tsvector GIN, `%` and `<%` on the trgm GIN — a BitmapOr of GIN scans,
    // never a seq scan. Score = (when `q`) ts_rank + similarity, ALWAYS recency + velocity + conversion, and proximity
    // (haversine) whenever a location is supplied — so text search, feeds, and browse all rank identically.
    private async Task<(List<Guid> Ids, int Total)> RankedIdsAsync(EventListFilter f, int offset, int size, CancellationToken ct)
    {
        var hasQ = !string.IsNullOrWhiteSpace(f.Q);
        var ps = new List<NpgsqlParameter>();
        var where = new StringBuilder(@"d.""ListedStandalone"" = true
            AND (d.""SeriesId"" IS NULL OR d.""SeriesMode"" <> 'Recurring' OR d.""IsSeriesPrimary"" = true)");
        if (hasQ)
        {
            where.Append(@" AND (d.search_vector @@ websearch_to_tsquery('english', @q)
                 OR d.""FuzzyText"" % @qraw OR @qraw <% d.""FuzzyText"")");
            ps.Add(new("q", f.Q!));
            ps.Add(new("qraw", f.Q!.ToLowerInvariant()));
        }

        void Filter(string clause, string name, object val) { where.Append(" AND ").Append(clause); ps.Add(new(name, val)); }
        if (f.OrgId is { } org) Filter(@"d.""OrgId"" = @org", "org", org);
        // Either level matches, preserving the pre-index semantics exactly (the old predicate was
        // `e.CategoryId == catId || e.TypeId == catId`): callers pass a category id from the browse rail
        // and a type id from the wizard's own taxonomy, and both must select the same events (D-299).
        if (f.CategoryId is { } cat) Filter(@"(d.""CategoryId"" = @cat OR d.""TypeId"" = @cat)", "cat", cat);
        if (!string.IsNullOrWhiteSpace(f.Kind)) Filter(@"d.""KindSlug"" = @kind", "kind", f.Kind);
        if (!string.IsNullOrWhiteSpace(f.Mode)) Filter(@"lower(d.""EventMode"") = lower(@mode)", "mode", f.Mode);
        if (!string.IsNullOrWhiteSpace(f.Language)) Filter(@"d.""Language"" = @lang", "lang", f.Language);
        if (!string.IsNullOrWhiteSpace(f.City)) Filter(@"lower(d.""City"") = lower(@city)", "city", f.City);
        if (f.Price == "free") where.Append(@" AND d.""IsPaid"" = false");
        else if (f.Price == "paid") where.Append(@" AND d.""IsPaid"" = true");
        if (f.DateFrom is { } from) Filter(@"d.""StartsAt"" >= @from", "from", from);
        if (f.DateTo is { } to) Filter(@"d.""StartsAt"" <= @to", "to", to);

        // Location: proximity is a null-safe score BOOST (never drops no-coordinate events); a supplied RadiusKm adds a
        // hard bounding-box filter (applied on the count too so paging is correct) — consistent across q and no-q (L1).
        var hasLoc = f is { Lat: not null, Lng: not null };
        if (hasLoc && f.RadiusKm is { } r)
        {
            var dLat = r / 111.0;
            var dLng = r / (111.0 * Math.Max(0.01, Math.Cos(f.Lat!.Value * Math.PI / 180)));
            Filter(@"d.""Lat"" IS NOT NULL AND d.""Lng"" IS NOT NULL AND d.""Lat"" BETWEEN @latLo AND @latHi AND d.""Lng"" BETWEEN @lngLo AND @lngHi", "latLo", f.Lat!.Value - dLat);
            ps.Add(new("latHi", f.Lat!.Value + dLat)); ps.Add(new("lngLo", f.Lng!.Value - dLng)); ps.Add(new("lngHi", f.Lng!.Value + dLng));
        }
        var proximity = hasLoc
            ? @"CASE WHEN d.""Lat"" IS NOT NULL AND d.""Lng"" IS NOT NULL THEN
                exp(- (6371 * acos(LEAST(1, GREATEST(-1,
                cos(radians(@lat)) * cos(radians(d.""Lat"")) * cos(radians(d.""Lng"") - radians(@lng))
                + sin(radians(@lat)) * sin(radians(d.""Lat"")))))) / 50.0) ELSE 0 END"
            : "0";

        var textScore = hasQ
            ? @"3 * ts_rank(d.search_vector, websearch_to_tsquery('english', @q)) + 2 * COALESCE(similarity(d.""FuzzyText"", @qraw), 0) + "
            : "";
        var score = $@"{textScore}1 * exp(- GREATEST(0, extract(epoch from (now() - d.""EventCreatedAt"")) / 86400) / 30.0)
            + 0.5 * ln(1 + d.""RecentViewCount"")
            + 0.8 * ln(1 + d.""ConversionCount"")
            + 1.5 * {proximity}";

        // `Sort` is an explicit caller override of the default deterministic ranking above — the same
        // literal date_asc/date_desc/newest/popular vocabulary the pre-D-184 discovery accepted. Honored
        // verbatim when present; null/unrecognized keeps the score-based default (was silently dropped
        // when discovery cut over to this one canonical query — M4 — restored here, found by client
        // integration verification for V3 Phase 16's client-completion pass).
        var orderBy = f.Sort switch
        {
            "date_asc" => @"d.""StartsAt"" ASC",
            "date_desc" => @"d.""StartsAt"" DESC",
            "newest" => @"d.""EventCreatedAt"" DESC",
            _ => $@"({score}) DESC, d.""StartsAt""",
        };

        var whereSql = where.ToString();
        // A read transaction so `SET LOCAL` scopes the word-similarity threshold (0.35 — good typo recall) to this
        // search only, never leaking onto the pooled connection. Only needed when `q` (the `<%` operator) is present.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (hasQ) await db.Database.ExecuteSqlRawAsync("SET LOCAL pg_trgm.word_similarity_threshold = 0.35", ct);
        // EF1002 is suppressed deliberately: every caller-supplied value (q, filters, location) is bound via a
        // NpgsqlParameter — only our own trusted column names / clause fragments are concatenated into the SQL string.
#pragma warning disable EF1002
        var total = (await db.Database.SqlQueryRaw<int>(
            $@"SELECT count(*)::int AS ""Value"" FROM event_search_documents d WHERE {whereSql}",
            ps.Select(Clone).ToArray()).ToListAsync(ct)).First();

        var idsParams = ps.Select(Clone).ToList();
        if (hasLoc) { idsParams.Add(new("lat", f.Lat!.Value)); idsParams.Add(new("lng", f.Lng!.Value)); }
        idsParams.Add(new("lim", size));
        idsParams.Add(new("off", offset));
        var ids = await db.Database.SqlQueryRaw<Guid>(
            $@"SELECT d.""EventId"" AS ""Value"" FROM event_search_documents d WHERE {whereSql}
               ORDER BY {orderBy} LIMIT @lim OFFSET @off",
            idsParams.ToArray()).ToListAsync(ct);
#pragma warning restore EF1002
        await tx.CommitAsync(ct);
        return (ids, total);
    }

    // A parameter can't be reused across two commands — clone for each execution.
    private static NpgsqlParameter Clone(NpgsqlParameter p) => new(p.ParameterName, p.Value);

    private async Task<IReadOnlyList<EventSummary>> SummariesAsync(IReadOnlyList<Guid> orderedIds, CancellationToken ct)
    {
        if (orderedIds.Count == 0) return [];
        var byId = await db.Events.AsNoTracking().Where(e => orderedIds.Contains(e.Id))
            .Select(SummaryProjection(db)).ToDictionaryAsync(s => s.Id, ct);
        var ordered = orderedIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();   // preserve rank order
        return await WithBannerUrlsAsync(storage, ordered, ct);
    }

    /// <summary>The one card projection, shared with <c>SocialService</c> so a saved event and a searched
    /// event are never two different shapes (D-302). Category and price are correlated subqueries rather
    /// than reads off the search index: the index is a *ranking* structure and can lag, while a price shown
    /// on a card has to be the price. The page is capped at 50 rows, so this is 50 index seeks, not a scan.
    /// </summary>
    internal static Expression<Func<Event, EventSummary>> SummaryProjection(KurxDbContext db) => e =>
        new EventSummary(e.Id, e.RepresentingOrgId, e.ParentEventId, e.Title, e.Slug, e.ShortCode, e.Subtitle, e.BannerKey,
            e.StartsAt, e.EndsAt, e.Status.ToString(), e.Visibility.ToString(), e.VenueName, e.City,
            e.EventMode.ToString(),
            db.EventCategories.Where(c => c.Id == e.CategoryId).Select(c => c.Name).FirstOrDefault(),
            // "From" price: the cheapest ticket a buyer could actually take, so a free tier makes the card
            // read Free even when a premium tier exists. Null means no ticket type at all — which a client
            // must render as "Registration not open", never as free.
            db.TicketTypes.Where(t => t.EventId == e.Id && t.DeletedAt == null)
                .Select(t => (long?)t.PricePaise).Min(),
            e.SettlementCurrency,
            e.IsFeatured);

    private static int Clamp(int limit) => Math.Clamp(limit, 1, 50);
}
