namespace Kurx.Application.Abstractions;

/// <summary>EventSeries (V3 §13.2, Phase 12) — LINEAGE, one entity two modes. RECURRING (same content, an RFC-5545
/// rrule + exception dates; each occurrence is its own Event with independent inventory and its own timezone) and
/// EDITIONS (same brand, different content; each member Event carries an ordinal/label). Followers and brand assets
/// live on the Series. Additive over the approved event model — an event's `SeriesId` links it to at most one series
/// (§3.4 rule 5, series never nest). Organiser actions reuse the org role check (Owner/Manager/Representative).</summary>
public interface ISeriesService
{
    Task<ServiceResult<SeriesView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, SeriesInput input, CancellationToken ct = default);
    Task<ServiceResult<SeriesView>> UpdateAsync(Guid userId, Guid seriesId, bool isAdmin, SeriesInput input, CancellationToken ct = default);
    Task<ServiceResult<SeriesView>> GetAsync(Guid seriesId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<SeriesView>>> ListForOrgAsync(Guid orgId, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid seriesId, bool isAdmin, CancellationToken ct = default);

    // Membership (an event belongs to ≤1 series).
    Task<ServiceResult<SeriesMemberView>> AttachEventAsync(Guid userId, Guid seriesId, bool isAdmin, SeriesMemberInput input, CancellationToken ct = default);
    Task<ServiceResult<bool>> DetachEventAsync(Guid userId, Guid seriesId, Guid eventId, bool isAdmin, CancellationToken ct = default);
    Task<IReadOnlyList<SeriesMemberView>> ListMembersAsync(Guid seriesId, Guid? viewerUserId, bool isAdmin, CancellationToken ct = default);

    // Followers (carried across members).
    Task<ServiceResult<bool>> FollowAsync(Guid userId, Guid seriesId, CancellationToken ct = default);
    Task<ServiceResult<bool>> UnfollowAsync(Guid userId, Guid seriesId, CancellationToken ct = default);
}

public record SeriesInput(string? Name, string? Mode, string? Description, string? BannerKey, string? BrandAssetsJson,
    string? Rrule, IReadOnlyList<string>? ExceptionDates);

public record SeriesView(Guid Id, Guid OrgId, string Name, string Slug, string Mode, string Description, string? BannerKey,
    string? BrandAssetsJson, string? Rrule, string? ExceptionDatesJson, int FollowerCount, int MemberCount, DateTime CreatedAt);

public record SeriesMemberInput(Guid EventId, int? EditionOrdinal, string? EditionLabel);

public record SeriesMemberView(Guid EventId, string Title, string Slug, int? EditionOrdinal, string? EditionLabel,
    DateTime StartsAt, string Timezone, string Status);
