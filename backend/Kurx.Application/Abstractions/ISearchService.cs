namespace Kurx.Application.Abstractions;

/// <summary>The single canonical discovery implementation (V3 §15, Phase 16) backing every public discovery endpoint.
/// Reads the outbox-fed <c>event_search_documents</c> index — Postgres FTS + trigram for text (incl. retired kind
/// names as aliases), typed columns for filters, and a deterministic ranking (recency · velocity · conversion ·
/// proximity, replacing raw <c>ViewCount DESC</c>). Applies the discovery-collapse rules (a Festival is one card;
/// a RECURRING series is one listing) and preserves the 404-not-403 visibility invariant — only Published + Public
/// events are indexed, so an internal event can never surface here. Affinity/personalisation is deferred.</summary>
public interface ISearchService
{
    /// <summary>Ranked search + filter over the index. When <c>filter.Q</c> is set, full-text + trigram + alias
    /// matching drives relevance; otherwise the deterministic ranking orders the filtered set.</summary>
    Task<(IReadOnlyList<EventSummary> Items, int Total)> SearchAsync(EventListFilter filter, CancellationToken ct = default);

    Task<IReadOnlyList<EventSummary>> UpcomingAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<EventSummary>> TrendingAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<EventSummary>> FeaturedAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<EventSummary>> LatestAsync(int limit, CancellationToken ct = default);

    /// <summary>Events related to <paramref name="eventId"/> — shared kind / org / unit + text similarity, ranked.</summary>
    Task<IReadOnlyList<EventSummary>> RelatedAsync(Guid eventId, int limit, CancellationToken ct = default);

    /// <summary>The eligibility-aware "events you can attend" feed (§15): Published + Public events whose audience rule
    /// the user actually satisfies (open events included; rule-gated events the user fails removed), ranked. Internal
    /// events are never indexed, so this can never leak their existence (404-not-403 preserved).</summary>
    Task<IReadOnlyList<EventSummary>> EligibleForAsync(Guid userId, int limit, CancellationToken ct = default);
}
