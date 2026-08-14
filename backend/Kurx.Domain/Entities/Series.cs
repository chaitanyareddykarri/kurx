using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>An EventSeries (V3 §13.2, Phase 12) — LINEAGE, one entity with two modes. RECURRING = the same content run
/// again (an <c>rrule</c> + <c>exception_dates</c>; each occurrence is its own <see cref="Event"/> with independent
/// inventory/cancellation and its own timezone). EDITIONS = the same brand, different content (each member Event carries
/// an ordinal/label). Followers, brand assets and canonical SEO authority live here and carry across members. <b>Lineage
/// never nests</b> (§3.4): a Series contains Events; an Event belongs to at most one Series; editions are siblings.</summary>
public class EventSeries
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;                  // unique per org — canonical series URL
    public SeriesMode Mode { get; set; }
    public string Description { get; set; } = "";
    public string? BannerKey { get; set; }
    public string? BrandAssetsJson { get; set; }               // brand assets carried across members
    public string? Rrule { get; set; }                         // RECURRING only — RFC 5545 recurrence rule
    public string? ExceptionDatesJson { get; set; }            // RECURRING only — jsonb [ISO date] skipped occurrences
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

/// <summary>A user following an EventSeries (V3 §13.2 — followers live on the Series and carry across editions).
/// Mirrors <see cref="OrganizationFollower"/>.</summary>
public class EventSeriesFollower
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
