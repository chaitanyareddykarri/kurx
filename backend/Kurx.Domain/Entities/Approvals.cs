using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>An internal ApprovalChain (V3 §14.3, Phase 14) attached to an <see cref="OrgUnit"/> and <b>inherited down
/// the tree</b> — an event uses the chain on its owning unit, or the nearest ancestor unit that declares one. Gates
/// publishing: the chain must complete before <c>Publish → Scheduled</c> (order: internal chain → platform review →
/// published). Additive; the paid-event platform-review gate (§14.4) is preserved separately.</summary>
public class ApprovalChain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgUnitId { get; set; }
    public string Name { get; set; } = null!;
    public ApprovalMode Mode { get; set; } = ApprovalMode.Sequential;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

/// <summary>One step of an <see cref="ApprovalChain"/> (§14.3). The approver is a role OR a specific user; the step
/// applies only when its <see cref="Condition"/> matches the event (e.g. IfPaid on a paid event). SLA/escalation are
/// stored metadata — the auto-escalation timer is a later phase, but the schema needs no change to add it.</summary>
public class ApprovalStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChainId { get; set; }
    public int Sort { get; set; }
    public OrgRole? ApproverRole { get; set; }          // approve as a role within the org…
    public Guid? ApproverUserId { get; set; }           // …or a named user
    public ApprovalCondition Condition { get; set; } = ApprovalCondition.Always;
    public long? ConditionParamPaise { get; set; }      // IfBudgetGt threshold (money is paise, D-004)
    public int? SlaHours { get; set; }                  // metadata (§14.3) — timer deferred
    public int? EscalationAfterHours { get; set; }      // metadata (§14.3) — timer deferred
}

/// <summary>The per-event instance of an approval run (§14.3) — created when an organiser first attempts to schedule/
/// publish under a chain. Holds the applicable steps' decisions; the request is Approved once every applicable step is
/// Approved or Bypassed, which the publish gate requires.</summary>
public class ApprovalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid ChainId { get; set; }
    public ApprovalRequestState State { get; set; } = ApprovalRequestState.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}

/// <summary>One applicable step's decision within an <see cref="ApprovalRequest"/> (§14.3). Bypass is a first-class
/// state and always audited (never a silent skip).</summary>
public class ApprovalStepDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid StepId { get; set; }
    public int Sort { get; set; }                       // copied from the step, for ordered (SEQUENTIAL) evaluation
    public ApprovalStepState State { get; set; } = ApprovalStepState.Pending;
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Note { get; set; }
}
