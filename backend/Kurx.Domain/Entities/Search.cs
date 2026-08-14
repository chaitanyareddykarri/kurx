namespace Kurx.Domain.Entities;

/// <summary>The denormalised discovery document for one event (V3 §15, Phase 16). One row per Published + Public
/// event, rebuilt by the outbox-fed projector — never dual-written from the event path. The projector fills the plain
/// text + typed columns; Postgres derives the weighted <c>tsvector</c> (a generated column added in the migration) and
/// the trigram index over <see cref="FuzzyText"/>, so full-text and typo-tolerant search hit an index rather than the
/// events table. Signal columns (<see cref="RecentViewCount"/>, <see cref="ConversionCount"/>, <see cref="IsSeriesPrimary"/>)
/// are refreshed asynchronously; they replace the raw <c>ViewCount DESC</c> ranking. No new classification field is
/// introduced — <c>topics</c>/<c>channel</c> (§12.1) are deferred to a future classification phase.</summary>
public class EventSearchDocument
{
    public Guid EventId { get; set; }                  // = Event.Id (PK, FK, cascade)

    // Weighted full-text source — the migration derives a stored tsvector: TitleText → weight A, BodyText → weight B.
    public string TitleText { get; set; } = "";        // title + subtitle
    public string BodyText { get; set; } = "";         // description + tags + kind name + kind aliases + speakers + venue + org + unit
    public string FuzzyText { get; set; } = "";        // lower-cased title + tags + kind + aliases — trigram fuzzy/partial matching

    // Typed filter columns (§12.3: anything filtered is a real column) — denormalised from the event.
    public Guid OrgId { get; set; }
    public Guid? OrgUnitId { get; set; }
    public string? KindSlug { get; set; }

    // The human-facing taxonomy (D-188/D-189), distinct from the behavioural KindSlug beside it. These two
    // existed as a filter before this index did, and Phase 16 moved discovery onto the index WITHOUT them —
    // so `?categoryId=` silently matched everything for every caller, which §12.3's own rule ("anything
    // filtered is a real column") would have caught had it been applied. Both are carried because the
    // filter has always accepted either level: an id may name a category or one of its types (D-299).
    public Guid CategoryId { get; set; }
    public Guid? TypeId { get; set; }
    public string EventMode { get; set; } = "Offline"; // Offline | Online | Hybrid
    public bool IsPaid { get; set; }
    public string Language { get; set; } = "en";
    public string City { get; set; } = "";
    public string Country { get; set; } = "IN";
    public double? Lat { get; set; }                   // proximity ranking (only when the query supplies a location)
    public double? Lng { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    // Discovery-collapse fields (§3.4 / §13.2) — reuse the Phase-12 model, no new concepts.
    public bool ListedStandalone { get; set; } = true; // a sub-event surfaces standalone only on opt-in
    public Guid? ParentEventId { get; set; }
    public Guid? SeriesId { get; set; }
    public string? SeriesMode { get; set; }            // Recurring | Editions (null when the event is not in a series)
    public bool IsSeriesPrimary { get; set; } = true;  // the single occurrence a RECURRING series collapses to (next upcoming)

    public bool HasAudienceRule { get; set; }          // fast pre-filter for the eligibility-aware feed
    public bool IsFeatured { get; set; }               // admin curation (§15 featured feed)

    // Deterministic ranking signals (refreshed asynchronously — never a synchronous write on the read path).
    public int RecentViewCount { get; set; }           // EventView stream over the velocity window
    public int ConversionCount { get; set; }           // authoritative registrations for the event
    public DateTime EventCreatedAt { get; set; }       // recency (creation)
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}
