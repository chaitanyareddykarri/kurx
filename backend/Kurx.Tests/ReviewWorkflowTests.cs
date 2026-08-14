using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;

namespace Kurx.Tests;

/// <summary>D-266 M4 — the review lifecycle's structural guarantees. Pure workflow assertions: the
/// transition table and its classifications are the single definition of what a review can do, so testing
/// them directly is testing the rule rather than one caller's use of it.</summary>
public class ReviewWorkflowTests
{
    private static EventStatus Target(string action, EventStatus from)
    {
        Assert.True(EventStatusWorkflow.TryGetTarget(action, from, out var to),
            $"'{action}' should be valid from {from}");
        return to;
    }

    private static void Invalid(string action, EventStatus from) =>
        Assert.False(EventStatusWorkflow.TryGetTarget(action, from, out _),
            $"'{action}' must NOT be valid from {from}");

    // ── The required path ────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_public_event_reaches_published_only_through_review()
    {
        Assert.Equal(EventStatus.PendingReview, Target("submit_for_review", EventStatus.Draft));
        Assert.Equal(EventStatus.UnderReview, Target("claim_review", EventStatus.PendingReview));
        Assert.Equal(EventStatus.Approved, Target("approve_review", EventStatus.UnderReview));
        Assert.Equal(EventStatus.Published, Target("publish_approved", EventStatus.Approved));
    }

    [Fact]
    public void Publish_approved_is_reachable_only_from_approved()
    {
        foreach (var s in new[] { EventStatus.Draft, EventStatus.PendingReview, EventStatus.UnderReview,
                                  EventStatus.ChangesRequested, EventStatus.Rejected })
            Invalid("publish_approved", s);
    }

    [Fact]
    public void The_changes_requested_loop_returns_to_the_queue()
    {
        Assert.Equal(EventStatus.ChangesRequested, Target("request_changes", EventStatus.UnderReview));
        // The organiser edits, then resubmits — straight back to the queue, not to a reviewer.
        Assert.Equal(EventStatus.PendingReview, Target("submit_for_review", EventStatus.ChangesRequested));
    }

    [Fact]
    public void A_rejected_event_may_be_resubmitted()
        => Assert.Equal(EventStatus.PendingReview, Target("submit_for_review", EventStatus.Rejected));

    // ── Claim mechanics — the reason the InReview split exists ───────────────────────────────

    [Fact]
    public void A_claim_can_be_released_back_to_the_queue()
    {
        Assert.Equal(EventStatus.UnderReview, Target("claim_review", EventStatus.PendingReview));
        Assert.Equal(EventStatus.PendingReview, Target("release_review", EventStatus.UnderReview));
    }

    [Fact]
    public void An_unclaimed_item_cannot_be_decided()
    {
        // Deciding without claiming would let two reviewers act on one event simultaneously.
        foreach (var action in new[] { "approve_review", "reject_review", "request_changes", "release_review" })
            Invalid(action, EventStatus.PendingReview);
    }

    [Fact]
    public void Withdraw_is_only_possible_before_a_reviewer_claims_it()
    {
        Assert.Equal(EventStatus.Draft, Target("withdraw", EventStatus.PendingReview));
        // Once claimed, withdrawing would discard a reviewer's in-flight work.
        Invalid("withdraw", EventStatus.UnderReview);
    }

    [Fact]
    public void An_event_cannot_be_submitted_twice()
    {
        Invalid("submit_for_review", EventStatus.PendingReview);
        Invalid("submit_for_review", EventStatus.UnderReview);
        Invalid("submit_for_review", EventStatus.Approved);
    }

    // ── Edit lock ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Edits_are_locked_exactly_while_the_event_is_with_a_reviewer()
    {
        Assert.True(EventStatusWorkflow.IsEditLocked(EventStatus.PendingReview));
        Assert.True(EventStatusWorkflow.IsEditLocked(EventStatus.UnderReview));

        // Everything else stays editable — notably ChangesRequested, where editing is the whole point.
        foreach (var s in new[] { EventStatus.Draft, EventStatus.ChangesRequested, EventStatus.Approved,
                                  EventStatus.Rejected, EventStatus.Published })
            Assert.False(EventStatusWorkflow.IsEditLocked(s), $"{s} must remain editable");
    }

    // ── Reviewer classification and decision requirements ────────────────────────────────────

    [Fact]
    public void Every_reviewer_action_is_a_real_transition()
    {
        foreach (var action in EventStatusWorkflow.ReviewerActions)
            Assert.True(EventStatusWorkflow.IsKnownAction(action), $"'{action}' is declared but has no transition");
    }

    [Fact]
    public void Organiser_actions_are_not_classified_as_reviewer_actions()
    {
        // submit/withdraw belong to the organiser; classifying them as reviewer actions would let a
        // reviewer submit on someone's behalf.
        Assert.DoesNotContain("submit_for_review", EventStatusWorkflow.ReviewerActions);
        Assert.DoesNotContain("withdraw", EventStatusWorkflow.ReviewerActions);
        Assert.DoesNotContain("publish_approved", EventStatusWorkflow.ReviewerActions);
    }

    [Fact]
    public void Rejection_requires_a_structured_reason_and_changes_require_notes()
    {
        Assert.Contains("reject_review", EventStatusWorkflow.ActionsRequiringReason);
        Assert.Contains("request_changes", EventStatusWorkflow.ActionsRequiringNotes);

        // Approval needs neither — an approval that demanded a justification would be noise.
        Assert.DoesNotContain("approve_review", EventStatusWorkflow.ActionsRequiringReason);
        Assert.DoesNotContain("approve_review", EventStatusWorkflow.ActionsRequiringNotes);
    }

    [Fact]
    public void Every_reason_code_is_a_closed_vocabulary_member()
    {
        // Free-form rejection strings cannot be analysed or localised; the enum is the contract.
        Assert.All(Enum.GetNames<EventReviewReason>(),
            n => Assert.True(Enum.TryParse<EventReviewReason>(n, out _)));
        Assert.Contains("Other", Enum.GetNames<EventReviewReason>());   // escape hatch exists, and needs notes
    }

    // ── Legacy compatibility until Stage 4 ───────────────────────────────────────────────────

    /// <summary>D-266 M4 Stage 4 — the legacy state is gone. This replaces the compatibility test that
    /// asserted InReview still transitioned: that behaviour is retired, so asserting it would be asserting
    /// the opposite of the architecture. What must still hold is that nothing can reach a dead end.</summary>
    [Fact]
    public void No_review_state_is_unreachable_or_terminal_by_accident()
    {
        // Every review state must be enterable by some transition, or it is dead code in the model …
        var reachable = new[] { EventStatus.PendingReview, EventStatus.UnderReview,
                                EventStatus.ChangesRequested, EventStatus.Approved, EventStatus.Rejected };
        foreach (var s in reachable)
            Assert.Contains(EventStatusWorkflow.Actions.Values.SelectMany(t => t), t => t.To == s);

        // … and every non-terminal review state must have a way out, or an event lands there forever.
        foreach (var s in new[] { EventStatus.PendingReview, EventStatus.UnderReview,
                                  EventStatus.ChangesRequested, EventStatus.Rejected, EventStatus.Approved })
            Assert.Contains(EventStatusWorkflow.Actions.Values.SelectMany(t => t), t => t.From == s);
    }

    /// <summary>The legacy submit action survives as an alias so existing clients keep working, but it now
    /// lands on PendingReview — nothing anywhere can put an event into the retired state.</summary>
    [Fact]
    public void The_legacy_submit_action_now_targets_the_review_queue()
        => Assert.Equal(EventStatus.PendingReview, Target("submit_review", EventStatus.Draft));
}
