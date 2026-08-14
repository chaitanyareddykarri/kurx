using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>V3 §11 — a platform-governed event Capability (Event Architecture V3, Phase 2). A capability is
/// an event *feature* (registration, teams, scoring, certificates…), distinct from trust capabilities
/// (organizer permissions, M7) and the D-116 workspace-capabilities (org-role UI contract). Seeded (~45).
/// <see cref="DependsOnJson"/> is the ONLY permitted coupling (§11.4, a shallow DAG); config schemas and
/// the full <c>provides</c> contract land as each subsystem is built in later phases.</summary>
public class Capability
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = null!;            // stable id, e.g. "scoring"
    public string Name { get; set; } = null!;            // display, e.g. "Scoring"
    public string GroupSlug { get; set; } = null!;       // universal | structure | people | commerce | output
    /// <summary>V3 §19 — universal to every Kind (Registration/Check-in/Announcements/…): always REQUIRED.</summary>
    public bool IsUniversal { get; set; }
    /// <summary>The workspace tab this capability renders (V3 §11.1 provides.workspace_tab); drives the
    /// generated workspace in Phase 15. Nullable — a few capabilities have no tab of their own.</summary>
    public string? WorkspaceTab { get; set; }
    /// <summary>JSON array of capability slugs this one requires (V3 §11.4 shallow DAG). Turning this on
    /// pulls its dependencies to at least ON.</summary>
    public string DependsOnJson { get; set; } = "[]";
    /// <summary>JSON array of event modes this capability is available in (V3 §11.3 mode-gating). Default
    /// all three; a few (seat map, route &amp; timing, travel) are excluded from Online.</summary>
    public string AvailableModesJson { get; set; } = "[\"Offline\",\"Online\",\"Hybrid\"]";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>V3 §11.1 — Kind × Capability default (the §19 matrix). Only REQUIRED and ON cells are stored;
/// an absent row means OFF (available, off by default). Universal capabilities are not stored per-kind —
/// they are REQUIRED for every Kind via <see cref="Capability.IsUniversal"/>.</summary>
/// <summary><b>Retired by D-266 M2 — the table stays, empty, on purpose.</b>
///
/// <para><see cref="ArchetypeCapabilityDefault"/> replaced it and <c>CapabilityRegistrySeeder</c> deletes
/// every row on boot, so it holds no truth and cannot become a second authority. It is kept rather than
/// dropped for one reason: <see cref="EventKind"/> itself is archived-not-dropped because live events still
/// carry a <c>KindSlug</c>, and dropping this table would make the Kind axis unrollbackable while those
/// references exist. Retire the table in the same change that retires <c>Event.KindSlug</c> — not before,
/// and not separately.</para>
///
/// <para>Nothing reads this type. If you find a caller, that is the defect.</para></summary>
public class KindCapabilityDefault
{
    public string KindSlug { get; set; } = null!;         // → EventKind.Slug
    public string CapabilitySlug { get; set; } = null!;   // → Capability.Slug
    public CapabilityState State { get; set; }            // Required | On (never Off/Locked here)
}

/// <summary>D-266 M2 — one cell of the D12 archetype x capability matrix, persisted. Supersedes
/// <see cref="KindCapabilityDefault"/>, whose rows were reached through the name-matched KindAlias chain
/// D-188 flags as fragile.
///
/// <para>Unlike the Kind table, <b>every</b> cell is stored, including <see cref="CapabilityRule.Unsupported"/>.
/// The Kind table stored only non-OFF rows and treated absence as "available but off", which cannot express
/// "a Workshop may never enable Leaderboard". Storing the negative cells is the point.</para></summary>
public class ArchetypeCapabilityDefault
{
    public string ArchetypeSlug { get; set; } = null!;    // -> EventArchetype.Slug
    public string CapabilitySlug { get; set; } = null!;   // -> Capability.Slug
    public CapabilityRule Rule { get; set; }
}

/// <summary>V3 §11.1 — the resolved capability state for one event (materialized from its Kind defaults +
/// mode gating + dependency resolution). Only non-OFF states are stored; OFF is the absence of a row.
/// <see cref="LockedReason"/>/<see cref="ConfigJson"/> support the LOCKED state and per-event config that
/// later phases populate.</summary>
public class EventCapability
{
    public Guid EventId { get; set; }
    public string CapabilitySlug { get; set; } = null!;   // → Capability.Slug
    public CapabilityState State { get; set; }            // Required | On | Locked (never Off — Off = no row)
    public string? ConfigJson { get; set; }               // per-event capability config (later phases)
    public string? LockedReason { get; set; }             // set when State = Locked (V3 §11.2)
}
