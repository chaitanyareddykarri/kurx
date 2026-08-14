namespace Kurx.Application.Abstractions;

/// <summary>The audience / eligibility subsystem (V3 §4.4, Phase 5): an organiser sets who may register
/// (an <c>AudienceRule</c>), members carry the attributes rules match on, and eligibility is evaluated
/// server-side — DENY BY DEFAULT when a rule exists, open when none does. Enforced at registration (order
/// creation) and re-checked at admission (the gate flags, never silently voids).</summary>
public interface IAudienceService
{
    /// <summary>The event's rule (manage authz). Value is null when the event is open (no rule).</summary>
    Task<ServiceResult<AudienceRuleView?>> GetRuleAsync(Guid actorUserId, Guid orgId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Create or replace the event's audience rule (Owner/Manager/Representative or admin).</summary>
    Task<ServiceResult<AudienceRuleView>> SetRuleAsync(Guid actorUserId, Guid orgId, Guid eventId, bool isAdmin, AudienceRuleInput input, CancellationToken ct = default);

    /// <summary>Remove the event's audience rule, reopening registration.</summary>
    Task<ServiceResult<bool>> DeleteRuleAsync(Guid actorUserId, Guid orgId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Is this user (null = guest) allowed to register for the event? Pure, side-effect-free —
    /// callers log the consequential decision. No rule ⇒ allowed.</summary>
    Task<EligibilityResult> EvaluateAsync(Guid? userId, Guid eventId, CancellationToken ct = default);

    /// <summary>Set a member's audience attributes (cohort_year, section, …) and source (Owner/Manager or admin).</summary>
    Task<ServiceResult<bool>> SetMemberAttributesAsync(Guid actorUserId, Guid orgId, Guid membershipId, bool isAdmin, MemberAttributesInput input, CancellationToken ct = default);
}

public record AudienceRuleInput(
    IReadOnlyList<Guid>? UnitSubtreeIn,
    IReadOnlyList<string>? RoleIn,
    IReadOnlyList<int>? CohortYearIn,
    IReadOnlyDictionary<string, string>? AttributeMatches,
    bool RequireVerified,
    bool ExternalOrgsAllowed,
    string? AppliesTo,
    bool GuestsAllowed,
    int GuestPerRegistrantCap,
    bool GuestsRequireApproval);

public record AudienceRuleView(
    Guid EventId,
    IReadOnlyList<Guid> UnitSubtreeIn,
    IReadOnlyList<string> RoleIn,
    IReadOnlyList<int> CohortYearIn,
    IReadOnlyDictionary<string, string> AttributeMatches,
    bool RequireVerified,
    bool ExternalOrgsAllowed,
    string AppliesTo,
    bool GuestsAllowed,
    int GuestPerRegistrantCap,
    bool GuestsRequireApproval);

public record MemberAttributesInput(int? CohortYear, IReadOnlyDictionary<string, string>? Attributes, string? Source);

/// <summary>Eligibility outcome. <see cref="Reason"/> is a stable code when denied, null when allowed.</summary>
public record EligibilityResult(bool Allowed, string? Reason);
