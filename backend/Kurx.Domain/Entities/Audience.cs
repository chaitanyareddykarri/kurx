using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>Who may REGISTER for an event (V3 §4.4 [B15], Phase 5). <b>DENY BY DEFAULT</b>: when a rule
/// exists, a user is eligible only if they satisfy every <i>specified</i> predicate condition through a
/// membership in the event's org, or qualify as an allowed external / guest. No rule ⇒ the event is open
/// (backward compatible). Ownership (<see cref="Event.OrgUnitId"/>) and audience are always separate (§4.4):
/// a unit may own an event open to everyone, or own one restricted to a different unit. Evaluated
/// server-side; the consequential decisions (register-deny, admission-flag) are logged by their callers.</summary>
public class AudienceRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }                   // exactly one rule per event

    // ── Predicate ─ each condition restricts only when non-empty (a conjunction of the specified ones) ──
    public string? UnitSubtreeInJson { get; set; }      // jsonb Guid[] — org_unit subtree roots the member must sit under
    public string? RoleInJson { get; set; }             // jsonb string[] — allowed OrgRole names
    public string? CohortYearInJson { get; set; }       // jsonb int[] — allowed cohort (joining) years
    public string? AttributeMatchesJson { get; set; }   // jsonb { key: value } — all must equal the member's attributes
    public bool RequireVerified { get; set; }           // the membership must be verified (§4.3)
    public bool ExternalOrgsAllowed { get; set; }       // a non-member may still register

    public AudienceAppliesTo AppliesTo { get; set; } = AudienceAppliesTo.EveryMember;  // team semantics arrive with Phase 10

    // ── Guests (§4.4). aggregate_pool_id is deferred to Phase 7 (inventory pools) ──
    public bool GuestsAllowed { get; set; }
    public int GuestPerRegistrantCap { get; set; }
    public bool GuestsRequireApproval { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
