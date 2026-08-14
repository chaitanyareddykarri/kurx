namespace Kurx.Application.Abstractions;

/// <summary>Internal approval chains (V3 §14.3, Phase 14). A chain lives on an <see cref="OrgUnit"/> and is inherited
/// down the tree; an event resolves the chain on its owning unit or the nearest ancestor. Publishing is gated on the
/// chain being complete (order: internal chain → platform review → published, §14.3/§14.4). Steps carry a condition
/// (only-if-paid/external/budget/minors) + SLA/escalation metadata (the auto-escalation timer is a later phase). Bypass
/// is first-class and always audited. Additive over the existing lifecycle; the paid-event trust gate is preserved.</summary>
public interface IApprovalService
{
    // Chain configuration (org Owner/Manager on the unit's org).
    Task<ServiceResult<ApprovalChainView>> CreateChainAsync(Guid actorId, Guid orgUnitId, bool isAdmin, ApprovalChainInput input, CancellationToken ct = default);
    Task<ServiceResult<ApprovalChainView>> UpdateChainAsync(Guid actorId, Guid chainId, bool isAdmin, ApprovalChainInput input, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeleteChainAsync(Guid actorId, Guid chainId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ApprovalChainView>>> ListForOrgUnitAsync(Guid actorId, Guid orgUnitId, bool isAdmin, CancellationToken ct = default);

    // Per-event approval workflow.
    Task<ServiceResult<ApprovalRequestView>> GetRequestAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<ApprovalRequestView>> DecideAsync(Guid actorId, Guid stepDecisionId, bool isAdmin, string action, string? note, CancellationToken ct = default);   // approve | reject | bypass
    Task<ServiceResult<ApprovalRequestView>> ResubmitAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default);   // recover a rejected request into a fresh cycle

    // Publish gate (called by the lifecycle). Resolves the effective chain, ensures a request exists, and reports
    // whether approval is complete. No effective chain ⇒ complete (backward-compatible: unchained events are unaffected).
    Task<bool> EnsureAndCheckCompleteAsync(Guid eventId, CancellationToken ct = default);

    // Read-only twin of the gate: evaluates the SAME completeness rule without materialising the request or any
    // decisions. For the workspace publish-checklist projection, which must never mutate approval state.
    Task<bool> IsCompleteAsync(Guid eventId, CancellationToken ct = default);
}

public record ApprovalChainInput(string? Name, string? Mode, IReadOnlyList<ApprovalStepInput>? Steps);

public record ApprovalStepInput(int Sort, string? ApproverRole, Guid? ApproverUserId, string? Condition,
    long? ConditionParamPaise, int? SlaHours, int? EscalationAfterHours);

public record ApprovalChainView(Guid Id, Guid OrgUnitId, string Name, string Mode, IReadOnlyList<ApprovalStepView> Steps);

public record ApprovalStepView(Guid Id, int Sort, string? ApproverRole, Guid? ApproverUserId, string Condition,
    long? ConditionParamPaise, int? SlaHours, int? EscalationAfterHours);

public record ApprovalRequestView(Guid Id, Guid EventId, Guid ChainId, string State, IReadOnlyList<ApprovalDecisionView> Decisions);

public record ApprovalDecisionView(Guid Id, Guid StepId, int Sort, string State, Guid? DecidedBy, DateTime? DecidedAt,
    string? Note, string? ApproverRole, Guid? ApproverUserId, string Condition);
