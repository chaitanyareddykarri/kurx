using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>The competition group entity (V3 §6.2) — the ONLY group entity, existing only where competition does.
/// Additive in Phase 10: the purchase <see cref="Group"/> stays as a legacy compatibility mirror. A Team survives
/// the event (it is a result record), carries identity, and is disqualifiable / mergeable / splittable. Every state
/// transition is audited; disqualification requires a reason + actor. Team registration as a purchase subject
/// (drawing a team-slot, §6.5) is deferred to the competitive-purchase phase — this phase is formation only.</summary>
public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    /// <summary>The competition ticket type the team forms under (its TeamPolicy lives here). Reuses the existing
    /// TicketType (no parallel product) — the ticket type's <c>IsCompetition</c> gates team formation.</summary>
    public Guid TicketTypeId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;               // unique per event
    public string? LogoUrl { get; set; }
    public string? Tagline { get; set; }
    public Guid? DeclaredOrgUnitId { get; set; }            // §4.4 team eligibility (applies_to) uses this
    public TeamState State { get; set; } = TeamState.Forming;
    /// <summary>The Team's own Registration when it registers as a unit (§6.5). Null this phase — the team-slot
    /// purchase flow is deferred; the field exists so the later competitive-purchase phase can link without a
    /// migration.</summary>
    public Guid? RegistrationId { get; set; }
    /// <summary>Tombstone pointer: after a merge/split, the retired team references the surviving team (§6.4), so
    /// history and submissions stay traceable rather than being silently discarded.</summary>
    public Guid? MergedIntoTeamId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A person's membership in a team (V3 §6.2). Substitution is an <b>edge, not a delete</b>:
/// <see cref="ReplacedByMembershipId"/> preserves who competed in which stage — required for result integrity and
/// certificates.</summary>
public class TeamMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid? PersonId { get; set; }                     // null until an email/phone invite is claimed
    public TeamRole Role { get; set; } = TeamRole.Member;
    public TeamMembershipState State { get; set; } = TeamMembershipState.Active;
    public Guid? ReplacedByMembershipId { get; set; }       // substitution edge
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
}

/// <summary>An outstanding invitation to join a team (V3 §6.2) — to a known person, or an email/phone.</summary>
public class TeamInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid? InviteePersonId { get; set; }
    public string? InviteeEmail { get; set; }
    public string? InviteePhone { get; set; }
    public string Token { get; set; } = null!;              // unique bearer token embedded in the invite link
    public TeamRole Role { get; set; } = TeamRole.Member;
    public TeamInviteState State { get; set; } = TeamInviteState.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A person's request to join a team (V3 §6.2), decided by captain or organiser per the policy.</summary>
public class TeamJoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid PersonId { get; set; }
    public string? Message { get; set; }
    public TeamJoinRequestState State { get; set; } = TeamJoinRequestState.Pending;
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Configuration of the <c>teams</c> capability (V3 §6.3), one per competition ticket type (like the
/// registration policy / Pass are per ticket type). Organiser-owned; derived on create with sensible defaults.</summary>
public class TeamPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }                  // unique — one policy per competition ticket type
    public int MinSize { get; set; } = 1;
    public int MaxSize { get; set; } = 4;
    public TeamFormationMode FormationMode { get; set; } = TeamFormationMode.Open;
    public TeamJoinApproval JoinApproval { get; set; } = TeamJoinApproval.Captain;
    public DateTime? LockAt { get; set; }                   // after: no join / leave / rename
    public DateTime? NameEditUntil { get; set; }
    public DateTime? RosterEditUntil { get; set; }
    public DateTime? MentorEditUntil { get; set; }
    public int MaxTeamsPerPersonInEvent { get; set; } = 1;
    public bool UnlimitedTeamsPerTree { get; set; }         // max_teams_per_person_across_tree: 1 (false) | unlimited (true)
    public bool AllowSoloAsTeam { get; set; }
    public bool AllowCrossOrgMembers { get; set; } = true;  // gates §4.4 applies_to
    public int SubstitutesAllowed { get; set; }
    public DateTime? SubstitutionDeadline { get; set; }
    public IncompleteTeamPolicy IncompleteTeamPolicy { get; set; } = IncompleteTeamPolicy.BlockAtLock;
    public string? WaitlistConfigJson { get; set; }         // { enabled, ordering: fcfs | score | random }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
