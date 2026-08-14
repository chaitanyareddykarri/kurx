namespace Kurx.Application.Abstractions;

/// <summary>One version-row of a Template family (V3 §13.1). <see cref="RootTemplateId"/> is the stable identity
/// events reference; a family's rows share it and differ by <see cref="Version"/>. Declarative only — ConfigJson
/// carries the capability preset + defaults; it never carries dates/slug/status/inventory/financials.</summary>
public record TemplateView(
    Guid Id, Guid RootTemplateId, int Version, string State, string Scope,
    Guid? OrgId, Guid? OrgUnitId, Guid? OwnerUserId,
    string Name, string Slug, string Description, string? KindSlug,
    string ConfigJson, string DefaultSectionsJson, bool IsSystem,
    DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>Create input for a new template family (v1 Draft). Scope drives the ownership column and the authority
/// check: Platform→admin, Org/Unit→org manager, Personal→any authenticated user (owner = caller).</summary>
public record TemplateInput(
    string Scope, Guid? OrgId, Guid? OrgUnitId,
    string Name, string? Description, string? KindSlug,
    string? ConfigJson, string? DefaultSectionsJson);

/// <summary>Authoritative Template system (V3 §13.1, activated Phase 15). Scoped (PLATFORM|ORG|UNIT|PERSONAL,
/// most-specific wins) + versioned (a Published version is immutable). Every write validates ConfigJson against the
/// closed capability registry (§11) through <see cref="ValidateConfigAsync"/> — the ONE pipeline a future AI
/// generator would reuse: unknown/disabled capability slugs and invalid config/workspace declarations are rejected,
/// never repaired. A template is declarative config only; it never carries runtime, inventory, or financial state.</summary>
public interface ITemplateService
{
    /// <summary>Create a new template family (v1, Draft). ConfigJson validated against the registry.</summary>
    Task<ServiceResult<TemplateView>> CreateAsync(Guid userId, bool isAdmin, TemplateInput input, CancellationToken ct = default);

    /// <summary>Edit a Draft version in place (a Published version is immutable). Re-validates ConfigJson.</summary>
    Task<ServiceResult<TemplateView>> UpdateAsync(Guid userId, Guid templateId, bool isAdmin,
        string? name, string? description, string? kindSlug, string? configJson, string? defaultSectionsJson, CancellationToken ct = default);

    /// <summary>Draft → Published (immutable from here). The version events snapshot from.</summary>
    Task<ServiceResult<TemplateView>> PublishAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Open a new editable Draft (Version+1, same family) copied from the given version.</summary>
    Task<ServiceResult<TemplateView>> NewVersionAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Retire a version (→ Archived); it stays for events that already snapshot from it.</summary>
    Task<ServiceResult<TemplateView>> ArchiveAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Duplicate a version into a NEW family (fresh root, v1 Draft). Scope/ownership follow the caller.</summary>
    Task<ServiceResult<TemplateView>> CloneAsync(Guid userId, Guid templateId, bool isAdmin, string? newName, CancellationToken ct = default);

    /// <summary>Hard-delete a Draft family never referenced by an event. System/in-use families are refused.</summary>
    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Templates visible in a context, most-specific-first (Personal → Unit → Org → Platform), latest
    /// Published version per family. All-null = Platform templates only (the public catalog).</summary>
    Task<IReadOnlyList<TemplateView>> ListForContextAsync(Guid? orgId, Guid? orgUnitId, Guid? ownerUserId, CancellationToken ct = default);

    /// <summary>Snapshot-apply the latest Published version of a family to an event (§13.1): upsert its capability
    /// preset onto <c>event_capabilities</c>, apply declarative defaults (timezone), and record
    /// <c>created_from_template_version</c>. Returns the applied version. Never creates inventory/financial rows.</summary>
    Task<ServiceResult<int>> ApplyToEventAsync(Guid eventId, Guid rootTemplateId, CancellationToken ct = default);

    /// <summary>Validate a template ConfigJson against the closed capability registry — the shared pipeline. Returns
    /// a specific error code (unknown_capability / capability_disabled / invalid_capability_state /
    /// invalid_workspace_declaration / invalid_config_json) on the first violation, or Success.</summary>
    Task<ServiceResult<bool>> ValidateConfigAsync(string configJson, CancellationToken ct = default);
}
