namespace Kurx.Application.Abstractions;

/// <summary>A registry entry (V3 §11) — an event capability's metadata. Distinct from trust capabilities
/// (organizer permissions) and D-116 workspace-capabilities (org-role UI contract).</summary>
public record CapabilityView(string Slug, string Name, string GroupSlug, bool IsUniversal,
    string? WorkspaceTab, IReadOnlyList<string> DependsOn, IReadOnlyList<string> AvailableModes, int Sort);

/// <summary>A capability resolved to an effective <c>state</c> (required|on|off) for a Kind or an event.</summary>
public record ResolvedCapability(string Slug, string Name, string GroupSlug, string State, string? WorkspaceTab);

/// <summary>Read + resolution over the V3 Capability registry (Phase 2). Resolution reads the seeded tables
/// (data-driven, correctable without code). It never gates engine behaviour — it lists the catalog, resolves
/// a Kind's defaults, and materializes an event's effective capability set from Kind × mode × dependencies.</summary>
public interface ICapabilityService
{
    /// <summary>The ~45 capabilities in catalog order.</summary>
    Task<IReadOnlyList<CapabilityView>> ListRegistryAsync(CancellationToken ct = default);

    /// <summary>Effective default state of every capability for a Kind: universal → required, §19 matrix
    /// (R → required, ● → on), everything else → off; dependencies resolved. Mode is not applied here.</summary>
    /// <summary>D-266 M2 — what an archetype supports, before any one event's configuration. Replaces
    /// GetForKindAsync: Kind resolved through the name-matched alias chain and could not express
    /// "unsupported", which is the answer this has to be able to give.</summary>
    Task<IReadOnlyList<ResolvedCapability>> GetForArchetypeAsync(string archetypeSlug, string? mode = null,
        CancellationToken ct = default);

    /// <summary>Effective capability set for one event: its Kind's defaults, gated by the event's mode
    /// (§11.3), with dependencies resolved (§11.4). Empty if the event does not exist.</summary>
    Task<IReadOnlyList<ResolvedCapability>> GetForEventAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Idempotently materialize the non-OFF <c>event_capabilities</c> rows for an event from its
    /// Kind (the "presets applied to events" step, V3 roadmap Phase 2). Re-materializes on Kind/mode change.</summary>
    Task MaterializeForEventAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>One-time backfill: materialize capabilities for kinded events that have no rows yet.
    /// Returns the number of events materialized. Safe to re-run.</summary>
    Task<int> BackfillEventCapabilitiesAsync(CancellationToken ct = default);
}
