using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Events;

/// <summary>
/// Draft -&gt; (optional review) -&gt; Published -&gt; Closed -&gt; Archived, plus Unpublish (Published -&gt; Draft).
/// The review leg is D-266 M4's PendingReview/UnderReview/ChangesRequested/Approved/Rejected — see
/// docs/architecture/REVIEW_LIFECYCLE.md. Archived is terminal. Only the transitions listed here are valid.
/// </summary>
public static class EventStatusWorkflow
{
    /// <summary>Exposed read-only so tests can assert reachability over the whole table — that every review
    /// state is enterable and every non-terminal one has a way out. Checking those properties by listing
    /// transitions by hand would miss exactly the state someone forgot to wire.</summary>
    public static readonly IReadOnlyDictionary<string, (EventStatus From, EventStatus To)[]> Actions =
        new Dictionary<string, (EventStatus, EventStatus)[]>(StringComparer.OrdinalIgnoreCase)
        {
            // D-266 M4 Stage 4: the legacy action name, retargeted onto PendingReview. Kept rather than
            // deleted because clients still post it; it is now an alias for submit_for_review, so no
            // transition anywhere targets the retired InReview state.
            ["submit_review"] = [(EventStatus.Draft, EventStatus.PendingReview)],
            // The legacy one-step "approve & publish", preserved onto the new queue states. Removing it would
            // break the existing admin pending page and any client still posting it; the reviewed door
            // (publish_approved, from Approved only) is the M4 path, not a replacement for this one.
            ["publish"] = [(EventStatus.Draft, EventStatus.Published), (EventStatus.Approved, EventStatus.Published),
                          (EventStatus.PendingReview, EventStatus.Published), (EventStatus.UnderReview, EventStatus.Published)],
            // Legacy reject means "send it back to the organiser", which is Draft — distinct from the M4
            // reject_review, which is a formal rejection carrying a reason code. Both are kept because
            // they mean different things; collapsing them would lose the distinction.
            ["reject"] = [(EventStatus.PendingReview, EventStatus.Draft), (EventStatus.UnderReview, EventStatus.Draft)],
            ["unpublish"] = [(EventStatus.Published, EventStatus.Draft), (EventStatus.Scheduled, EventStatus.Draft)],
            ["close"] = [(EventStatus.Published, EventStatus.Closed)],
            // D-101 (M7): Cancelled is terminal and distinct from Closed ("the event happened"). There is
            // deliberately no path back to Published — a cancelled event is never resurrected; clone it.
            ["cancel"] = [(EventStatus.Draft, EventStatus.Cancelled), (EventStatus.PendingReview, EventStatus.Cancelled),
                          (EventStatus.UnderReview, EventStatus.Cancelled),
                          (EventStatus.Published, EventStatus.Cancelled), (EventStatus.Scheduled, EventStatus.Cancelled),
                          (EventStatus.Live, EventStatus.Cancelled)],
            ["archive"] = [(EventStatus.Draft, EventStatus.Archived), (EventStatus.Closed, EventStatus.Archived),
                           (EventStatus.Cancelled, EventStatus.Archived), (EventStatus.Completed, EventStatus.Archived)],
            // V3 §14.1 (Phase 14) — additive lifecycle: SCHEDULED (published to the workspace, registration not yet
            // open) → REGISTRATION_OPEN (= the authoritative Published) → LIVE → COMPLETED. The existing `publish`
            // (direct to Published) is preserved for backward compatibility; these are the granular §14.2-gated steps.
            ["schedule"] = [(EventStatus.Draft, EventStatus.Scheduled), (EventStatus.Approved, EventStatus.Scheduled)],
            ["open_registration"] = [(EventStatus.Scheduled, EventStatus.Published)],
            ["go_live"] = [(EventStatus.Published, EventStatus.Live)],
            ["complete"] = [(EventStatus.Published, EventStatus.Completed), (EventStatus.Live, EventStatus.Completed)],

            // ── D-266 M4 · review lifecycle ──────────────────────────────────────────────────
            // The path a Public product must take: Draft -> PendingReview -> UnderReview -> Approved
            // -> Published. `publish` from Draft survives only for Private products, which are never
            // reviewed; EventReviewPolicy.RequiresReview is what enforces the difference, so the table
            // stays a pure statement of which transitions exist.
            ["submit_for_review"] = [(EventStatus.Draft, EventStatus.PendingReview),
                                     (EventStatus.ChangesRequested, EventStatus.PendingReview),
                                     (EventStatus.Rejected, EventStatus.PendingReview)],
            // Organiser pulls it back out of the queue. Only before a reviewer has claimed it — once
            // someone is working on it, withdrawing would discard their in-flight review.
            ["withdraw"] = [(EventStatus.PendingReview, EventStatus.Draft)],
            // Reviewer claims the item. This is the transition the legacy single InReview state could
            // not express, and the reason the split exists.
            ["claim_review"] = [(EventStatus.PendingReview, EventStatus.UnderReview)],
            // Reviewer steps away without deciding; the item returns to the queue for someone else.
            ["release_review"] = [(EventStatus.UnderReview, EventStatus.PendingReview)],
            ["request_changes"] = [(EventStatus.UnderReview, EventStatus.ChangesRequested)],
            ["approve_review"] = [(EventStatus.UnderReview, EventStatus.Approved)],
            ["reject_review"] = [(EventStatus.UnderReview, EventStatus.Rejected)],
            // Approved is the only state a reviewed event publishes from.
            ["publish_approved"] = [(EventStatus.Approved, EventStatus.Published),
                                    (EventStatus.Approved, EventStatus.Scheduled)],
        };

    /// <summary>States in which the event is with a reviewer and the organiser may not edit it. A PATCH
    /// here returns 409: letting content change under a reviewer means they approve something other than
    /// what they read.</summary>
    public static bool IsEditLocked(EventStatus status) =>
        status is EventStatus.PendingReview or EventStatus.UnderReview;

    /// <summary>Actions only a reviewer may invoke. Authorization itself is D-269's
    /// <c>IEventAuthority</c>; this only says which actions are reviewer-scoped, so the two never
    /// duplicate each other.</summary>
    public static readonly string[] ReviewerActions =
        ["claim_review", "release_review", "request_changes", "approve_review", "reject_review"];

    /// <summary>Reviewer decisions that require a structured reason code — never a free-form string.</summary>
    public static readonly string[] ActionsRequiringReason = ["reject_review"];

    /// <summary>Reviewer decisions that require notes for the organiser to act on.</summary>
    public static readonly string[] ActionsRequiringNotes = ["request_changes"];

    /// <summary>The forward, gate-bearing lifecycle actions (§14.2), in lifecycle order — the publish checklist
    /// projects these against <see cref="TryGetTarget"/> + the event's validation gates.</summary>
    public static readonly string[] ForwardActions = ["schedule", "publish", "open_registration", "go_live", "complete"];

    public static bool IsKnownAction(string action) => Actions.ContainsKey(action);

    public static bool TryGetTarget(string action, EventStatus current, out EventStatus target)
    {
        target = default;
        if (!Actions.TryGetValue(action, out var transitions)) return false;
        foreach (var (from, to) in transitions)
        {
            if (from != current) continue;
            target = to;
            return true;
        }
        return false;
    }
}
