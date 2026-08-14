namespace Kurx.Application.Abstractions;

/// <summary>D-266 M3 — one event's resolved policy model. Every field is a projection of a single
/// resolution, so <see cref="PublishBlockers"/> and <see cref="ReviewerChecklist"/> can never disagree
/// about whether the event is publishable.</summary>
/// <param name="AllowedPolicies">From the Type's stored allow-list, narrowed by product. Runtime truth.</param>
/// <param name="RegistrationGates">Derived from <paramref name="SelectedPolicy"/>. Never independently set.</param>
/// <param name="PublishBlockers">Empty means publishable, as far as policy is concerned.</param>
public record PolicyRequirementsView(
    Guid EventId,
    string Product,
    string SelectedPolicy,
    IReadOnlyList<string> AllowedPolicies,
    IReadOnlyList<string> RegistrationGates,
    string IdentityRequirement,
    IReadOnlyList<string> RegistrationRequirements,
    IReadOnlyList<string> PublishBlockers,
    IReadOnlyList<string> ReviewerChecklist,
    bool IsValid);

/// <summary>Resolves what an event is <i>allowed</i> to do. Distinct from <c>ICapabilityService</c>, which
/// resolves what it <i>can</i> do, and from <c>IEventAuthority</c> (D-269), which resolves <i>who</i> may
/// act. None of the three consults the others.</summary>
public interface IEventPolicyService
{
    Task<ServiceResult<PolicyRequirementsView>> GetForEventAsync(Guid eventId, CancellationToken ct = default);
}

/// <summary>D-266 M7 — one item on the reviewer's checklist, with this reviewer's tick.
///
/// <para><see cref="Key"/> is the stable requirement code from <c>ReviewerChecklist</c>; the copy a console
/// renders is the client's. <see cref="Blocking"/> distinguishes a rule that would refuse the publish
/// outright from one the reviewer merely has to confirm — both must be ticked to approve, but only the
/// first also stops the publish on its own.</para></summary>
public record ReviewChecklistItemView(string Key, bool Checked, DateTime? CheckedAt, bool Blocking);

/// <summary>D-266 M7 — the reviewer's checklist for one event. <see cref="IsComplete"/> is what gates
/// <c>approve_review</c>.</summary>
public record ReviewChecklistView(Guid EventId, IReadOnlyList<ReviewChecklistItemView> Items, bool IsComplete);

/// <summary>D-266 M7 — the reviewer's working checklist.
///
/// <para><b>The item list is derived, never stored.</b> It projects
/// <c>PolicyResolver.ReviewerChecklist</c> — the same array the publish blockers come from — so a reviewer
/// cannot work through a list that has drifted from the rules actually gating the publish. Only the ticks
/// are persisted.</para></summary>
public interface IEventReviewChecklistService
{
    /// <summary>The live checklist for this event, merged with the caller's ticks. Reviewer-gated at the
    /// endpoint; performs no event authority check, exactly like the rest of the admin review surface.</summary>
    Task<ServiceResult<ReviewChecklistView>> GetAsync(Guid reviewerId, Guid eventId, CancellationToken ct = default);

    /// <summary>Tick or untick one item. <c>unknown_checklist_item</c> when the key is not on the event's
    /// current checklist — a tick against an item that does not apply would count towards completeness
    /// without ever having been read.</summary>
    Task<ServiceResult<ReviewChecklistView>> SetAsync(Guid reviewerId, Guid eventId, string itemKey, bool @checked,
        CancellationToken ct = default);

    /// <summary>Whether this reviewer has ticked every item. The gate <c>approve_review</c> consults.</summary>
    Task<bool> IsCompleteAsync(Guid reviewerId, Guid eventId, CancellationToken ct = default);
}
