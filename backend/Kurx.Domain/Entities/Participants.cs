using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>The participant-role registry (V3 §5.1/§5.3, Phase 6). One model replacing V2's three overlapping
/// systems (EventAssignment.Role free-text, capability people-lists, org RBAC): capabilities REFERENCE these
/// roles, they never own their own people tables. <see cref="Class"/> is one of 7 hardcoded values (capacity
/// and permission logic branch on it); <see cref="Slug"/> is the platform registry, org-extensible later
/// (<see cref="OrgId"/> null = platform role). <see cref="CountsTowardCapacity"/> and
/// <see cref="InventorySegment"/> are role properties (§5.2 — Volunteers/Media consume no attendee inventory;
/// Competitors do; VIPs consume the VIP segment).</summary>
public class ParticipantRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = null!;               // "judge", "volunteer", … (unique among platform roles)
    public string Name { get; set; } = null!;
    public ParticipantClass Class { get; set; }
    /// <summary>jsonb string[] of event-scoped permission keys the role grants (§5.4), e.g. ["participants:manage"].</summary>
    public string? DefaultPermissionsJson { get; set; }
    /// <summary>jsonb string[] of default credential access zones (§5.2) — mostly consumed by later phases.</summary>
    public string? DefaultAccessZonesJson { get; set; }
    public bool IsPublic { get; set; }
    public bool CountsTowardCapacity { get; set; }
    public string? InventorySegment { get; set; }           // "general" | "vip" | null (no inventory)
    /// <summary>null = platform role; a value marks an org-extended role (write path is a later phase).</summary>
    public Guid? OrgId { get; set; }
    public int Sort { get; set; }
}

/// <summary>A person/team/org-unit participating in an event in some capacity (V3 §5.1, Phase 6).
/// PARTICIPATION only — permission is derived from the role (§5.4) and credential is separate (§5.2). Replaces
/// (and is backfilled from) V2's <c>EventAssignment</c>, which is kept as the legacy shadow during the
/// strangler window. Team subjects are stored-for-Phase-10; <see cref="ScopeJson"/> holds whole-event/sub-event
/// scope (stages/agenda-item scope is honoured as those entities land).</summary>
public class EventParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public ParticipantSubjectType SubjectType { get; set; } = ParticipantSubjectType.Person;
    public Guid SubjectId { get; set; }                     // user id / team id / org_unit id per SubjectType
    public string RoleSlug { get; set; } = null!;           // → ParticipantRole.Slug
    public string? CustomLabel { get; set; }                // free text (§5.3), never a permission/capacity input
    public ParticipantState State { get; set; } = ParticipantState.Invited;
    /// <summary>jsonb: { "whole_event": bool, "sub_events": [guid], "stages": [guid], "agenda_items": [guid] }.
    /// null ⇒ whole event. Stages/agenda scoping is inert until those entities exist (Phases 11/12).</summary>
    public string? ScopeJson { get; set; }
    public ParticipantVisibility Visibility { get; set; } = ParticipantVisibility.Internal;
    public Guid? InvitedBy { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
