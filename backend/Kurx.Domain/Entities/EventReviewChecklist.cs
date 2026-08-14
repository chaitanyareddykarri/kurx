namespace Kurx.Domain.Entities;

/// <summary>D-266 M7 — one reviewer's tick against one checklist item on one event.
///
/// <para><b>The item list is not stored.</b> It is a projection of
/// <c>PolicyResolver.ReviewerChecklist</c>, the same array the publish blockers come from, so a reviewer
/// can never be working through a list that has drifted from the rules actually gating the publish. This
/// table records only <i>which of those items a reviewer has ticked</i>. Storing the items themselves would
/// create a second source of truth and freeze it at the moment the event entered review — so a rule added
/// later, or an event edited into needing one, would be invisible to the person approving it.</para>
///
/// <para>Keyed per reviewer, not per event: two reviewers working the same queue item each keep their own
/// working state, and a release-and-reclaim does not silently inherit someone else's ticks.</para></summary>
public class EventReviewChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid ReviewerId { get; set; }

    /// <summary>The requirement key from <c>ReviewerChecklist</c> — e.g. <c>invitation_list_required</c>,
    /// <c>event_authorization_required</c>. A stable code, never display text: the copy is the client's and
    /// changing it must not orphan a reviewer's ticks.</summary>
    public string ItemKey { get; set; } = null!;

    public bool Checked { get; set; }
    public DateTime? CheckedAt { get; set; }
}
