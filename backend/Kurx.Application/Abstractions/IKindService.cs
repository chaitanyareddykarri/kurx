namespace Kurx.Application.Abstractions;

/// <summary>One of the 20 V3 Kinds (Event Architecture V3 §2) with the legacy type-name aliases that
/// resolve to it. Read-only registry projection for <c>GET /v1/kinds</c>.</summary>
public record KindView(string Slug, string Name, string GroupSlug, string GroupName, int Sort,
    IReadOnlyList<string> Aliases);

/// <summary>Read + resolution over the V3 Kind registry (Phase 1). Kinds are a closed, analysable
/// classification; this service never gates behaviour — it only lists the catalog and maps the legacy
/// taxonomy onto it. Alias→Kind resolution reads the <c>kind_aliases</c> table (data-driven), so a
/// mapping can be corrected in the database without a code change.</summary>
public interface IKindService
{
    /// <summary>The 20 Kinds in catalog order, each with its resolved aliases.</summary>
    Task<IReadOnlyList<KindView>> ListAsync(CancellationToken ct = default);

    /// <summary>Resolves the Kind slug for an event from its (optional) Type and (optional) Category via
    /// the alias table, with a category-level fallback. Returns null when nothing maps — kept nullable so
    /// Phase 1 stays strictly additive: an unmapped event is valid, just not yet kinded.</summary>
    Task<string?> ResolveKindSlugAsync(Guid? typeId, Guid? categoryId, CancellationToken ct = default);

    /// <summary>One-time (idempotent) backfill: stamps <c>KindSlug</c> on existing events that have none.
    /// Returns the number updated. Safe to re-run — only null-KindSlug rows are touched.</summary>
    Task<int> BackfillEventKindsAsync(CancellationToken ct = default);
}
