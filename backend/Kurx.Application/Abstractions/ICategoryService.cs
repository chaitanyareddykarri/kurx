namespace Kurx.Application.Abstractions;

public record CategoryView(Guid Id, Guid? ParentId, string Level, string Name, string Slug, int Sort,
    bool IsVisible,
    /// <summary>"Public"/"Private" on a Type node; null on Audience/Category nodes and on Types an admin
    /// has not classified. The event's <c>Product</c> is derived from this at create (D-266 M1), so the
    /// Create-Event gate offers only the Types matching the chosen product class — it filters, and never
    /// overrides the derivation (D-305/D-307). Null is treated as **Public** by every consumer, matching
    /// <c>ResolveArchetypeAsync</c>'s own fallback.
    ///
    /// <para>This is the PUBLIC projection and the one `/v1/categories` actually serves. The richer
    /// <see cref="AdminCategoryView"/> carries it too; putting it only there is a mistake that ships a
    /// field no client can see.</para></summary>
    string? ProductClass = null);

/// <summary>D-188 (Platform Taxonomy Management) — the admin-only, richer projection of a taxonomy node:
/// everything <see cref="CategoryView"/> has, plus lifecycle/metadata/audit fields and a real usage count.
/// <c>RegistrationsCount</c>/<c>AttendeesCount</c>/<c>RevenuePaise</c>/<c>ViewsCount</c>/<c>FavoritesCount</c>/
/// <c>IsTrending</c> are deliberate extension points (round 2 refinement 6) — always <c>null</c> today; each
/// would need its own join (Orders/Tickets for revenue, the EventView stream for views, a time-windowed
/// comparison for trending) that this pass does not add, so every taxonomy screen load doesn't pay for
/// aggregates nobody asked to see yet. <c>UsageCount</c> is the one real, always-populated count.</summary>
public record AdminCategoryView(
    Guid Id, Guid? ParentId, string Level, string Name, string Slug, int Sort, bool IsVisible,
    string Status, string? Description, string? IconKey, string? Color, string? Badge, string? SearchKeywords,
    int Version, DateTime CreatedAt, DateTime UpdatedAt, Guid? CreatedBy, Guid? UpdatedBy,
    int UsageCount,
    int? RegistrationsCount = null, int? AttendeesCount = null, long? RevenuePaise = null,
    int? ViewsCount = null, int? FavoritesCount = null, bool? IsTrending = null,
    /// <summary>"Public" or "Private" on a Type node; null on Audience/Category nodes and on Types an
    /// admin has not classified. The event's <c>Product</c> is derived from this at create (D-266 M1),
    /// so the Create-Event gate uses it to offer only the Types that match the chosen product class —
    /// the gate filters, it never overrides the derivation (D-305). Optional and trailing so every
    /// existing construction site compiles unchanged.</summary>
    string? ProductClass = null);

/// <summary>One node in a taxonomy export/import payload — keyed by <c>Slug</c> (never <c>Id</c>, which is
/// environment-specific) so an export from one Kurx environment can be merged into another. <c>ParentSlug</c>
/// is null for Audience-level nodes.</summary>
public record TaxonomyExportNode(string Slug, string? ParentSlug, string Level, string Name, int Sort,
    bool IsVisible, string? Description, string? IconKey, string? Color, string? Badge, string? SearchKeywords);

public record TaxonomyExport(IReadOnlyList<TaxonomyExportNode> Nodes, DateTime ExportedAt);

/// <summary>D-188 refinement round 2 #1 — Preview → Validate → Apply, never a destructive replace.
/// <c>Conflicts</c> are warnings (the node is skipped, the rest of the import still proceeds); any
/// <c>Errors</c> entry makes <see cref="ICategoryService.ApplyImportAsync"/> refuse to run at all.</summary>
public record ImportPreview(IReadOnlyList<TaxonomyExportNode> ToCreate, IReadOnlyList<TaxonomyExportNode> ToUpdate,
    IReadOnlyList<string> Conflicts, IReadOnlyList<string> Errors);

public record ImportResult(int Created, int Updated, int Skipped);

public record ReorderItem(Guid Id, int Sort);

/// <summary>A capability slug's state for one Type-level taxonomy node — <c>State</c> is <c>"off"</c> when
/// no <see cref="Kurx.Domain.Entities.CategoryCapabilityDefault"/> row exists for it.</summary>
public record TypeCapabilityView(string Slug, string Name, string GroupSlug, string State);

/// <summary>Platform-wide taxonomy (Audience/Category/Type levels). Shared across all orgs, so only a
/// platform admin (KurxAdmin) may create/edit/delete — everyone can read. D-188: fully admin-manageable —
/// lifecycle (Active/Disabled/Archived, independent of the display-only <c>IsVisible</c>), rich metadata,
/// audit trail, reorder, duplicate (Type-only), reparent (Type-only), merge-only import/export, and a
/// Type-level capability bridge (<see cref="Kurx.Domain.Entities.CategoryCapabilityDefault"/>) that reuses
/// the existing <see cref="ICapabilityService"/> catalog rather than inventing new capability names.</summary>
public interface ICategoryService
{
    Task<ServiceResult<CategoryView>> CreateAsync(string level, string name, Guid? parentId, int sort, bool isVisible,
        string? description, string? iconKey, string? color, string? badge, string? searchKeywords,
        Guid? actorId, CancellationToken ct = default);

    /// <summary><paramref name="parentId"/> only applies to a <c>Level == Type</c> node (reparent to another
    /// Category) — ignored otherwise. Slug is never touched, for any level (D-188 refinement round 2 #2).</summary>
    Task<ServiceResult<CategoryView>> UpdateAsync(Guid id, string? name, int? sort, bool? isVisible,
        string? description, string? iconKey, string? color, string? badge, string? searchKeywords,
        Guid? parentId, Guid? actorId, CancellationToken ct = default);

    /// <summary>Unchanged guard behaviour (still refuses when in-use or has-children) — now also audits.</summary>
    Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid? actorId, CancellationToken ct = default);

    Task<ServiceResult<CategoryView>> DisableAsync(Guid id, Guid? actorId, CancellationToken ct = default);
    Task<ServiceResult<CategoryView>> EnableAsync(Guid id, Guid? actorId, CancellationToken ct = default);
    Task<ServiceResult<CategoryView>> ArchiveAsync(Guid id, Guid? actorId, CancellationToken ct = default);
    /// <summary>Archived restores to Disabled, never straight to Active — a deliberate two-step.</summary>
    Task<ServiceResult<CategoryView>> RestoreAsync(Guid id, Guid? actorId, CancellationToken ct = default);
    Task<ServiceResult<CategoryView>> SetVisibleAsync(Guid id, bool visible, Guid? actorId, CancellationToken ct = default);

    /// <summary>Type-level only — fails <c>invalid_level</c> otherwise.</summary>
    Task<ServiceResult<CategoryView>> DuplicateAsync(Guid id, Guid? actorId, CancellationToken ct = default);

    /// <summary>Every id must share the same <paramref name="parentId"/> (null reorders the top-level
    /// Audience rows) or the whole batch fails <c>parent_mismatch</c> — never a partial reorder.</summary>
    Task<ServiceResult<bool>> ReorderAsync(Guid? parentId, IReadOnlyList<ReorderItem> order, Guid? actorId, CancellationToken ct = default);

    Task<IReadOnlyList<CategoryView>> ListAsync(string? level, string? q, bool includeHidden, CancellationToken ct = default);

    /// <summary>The enriched admin listing — every status/visibility, usage counts, full metadata.</summary>
    Task<IReadOnlyList<AdminCategoryView>> ListForAdminAsync(string? level, string? q, CancellationToken ct = default);

    /// <summary>One grouped query per FK column (<c>CategoryId</c>/<c>TypeId</c>/<c>AudienceLevelId</c>),
    /// never per-row — same batch-aggregate shape as <c>EventService.EventStatsAsync</c>.</summary>
    Task<IReadOnlyDictionary<Guid, int>> UsageCountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    Task<TaxonomyExport> ExportAsync(CancellationToken ct = default);
    Task<ImportPreview> PreviewImportAsync(TaxonomyExport import, CancellationToken ct = default);
    /// <summary>Refuses to run (returns null) if <see cref="PreviewImportAsync"/> on the same payload would
    /// report any <c>Errors</c> — creates/updates only, matched by slug, never deletes anything.</summary>
    Task<ImportResult?> ApplyImportAsync(TaxonomyExport import, Guid? actorId, CancellationToken ct = default);

    /// <summary>The full ~45-capability registry with this Type's current state overlaid.</summary>
    Task<ServiceResult<IReadOnlyList<TypeCapabilityView>>> GetTypeCapabilitiesAsync(Guid typeId, CancellationToken ct = default);
    /// <summary><paramref name="states"/> is the complete desired set — any capability not listed is turned
    /// off (its row removed), matching how <c>KindCapabilityDefault</c> already treats "off" as "no row."</summary>
    Task<ServiceResult<bool>> SetTypeCapabilitiesAsync(Guid typeId, IReadOnlyList<(string Slug, string State)> states,
        Guid? actorId, CancellationToken ct = default);
}
