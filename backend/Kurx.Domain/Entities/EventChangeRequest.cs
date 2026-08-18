using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>D-388 — a host's proposed edit to an event that is already <b>live</b>, held for a reviewer
/// instead of applied.
///
/// <para><b>Why this exists.</b> Until D-388 the only guard on editing was
/// <c>EventStatusWorkflow.IsEditLocked</c> (the two review states) and D-363 §4's reopen (<c>Approved</c>
/// only). A <c>Published</c> event fell through both: its owner could rewrite the title, move the dates and
/// change the venue of an event people had already registered for, in one PATCH, with no reviewer, no
/// notification and no audit row. Reproduced over HTTP against the live API before this was written.</para>
///
/// <para><b>This is not a second event.</b> It holds the <i>proposed values</i> — a serialized
/// <c>UpdateEventInput</c>, the exact shape the one-and-only apply path already takes (D-191) — against the
/// event row it names. Approval replays it through <c>ApplyUpdateAsync</c>, so validation, the search
/// reindex, the §14.5 refund window and capability rematerialization all happen exactly as they do for a
/// draft edit. A duplicate event row would have needed every one of those re-implemented, and would have
/// had to answer "which one do registrations point at".</para>
///
/// <para><b><see cref="BaseVersion"/> is what makes approval safe.</b> It is the <c>Event.Version</c> the
/// host authored against. If the live event has moved since — another approved request, an admin emergency
/// edit — approving this one would silently overwrite that newer state, so it is refused with
/// <c>version_conflict</c> and the host reproposes from what the event is now. Rebasing was rejected: a
/// reviewer approved specific values in a specific context, and a merged result is something nobody
/// read.</para>
///
/// <para><b>Only PUBLIC events reach here.</b> A Private product is never reviewed (its host is its only
/// audience), so it keeps direct editing at every status.</para></summary>
public class EventChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The event whose live values this proposes to replace. A reference, never a copy.</summary>
    public Guid EventId { get; set; }

    /// <summary>The host who proposed it. Authorization is re-checked at approval time, not trusted from
    /// here — someone who has since lost management of the event must not have a pending request applied
    /// in their name.</summary>
    public Guid RequestedBy { get; set; }

    /// <summary>The <c>Event.Version</c> this was authored against. See the class remarks.</summary>
    public int BaseVersion { get; set; }

    /// <summary>The proposed values: a serialized <c>UpdateEventInput</c> carrying only the fields the host
    /// actually changed. jsonb, so it is valid at the database level rather than only at parse time.</summary>
    public string ProposedJson { get; set; } = "{}";

    /// <summary>A snapshot of the live values for exactly the proposed fields, taken when the request was
    /// created. The admin diff renders CURRENT from the live row, but this is what the host was looking at
    /// — without it, "what did they think they were changing" is unanswerable after the fact.</summary>
    public string PreviousJson { get; set; } = "{}";

    /// <summary>The host's explanation. Optional: a reviewer can read the diff, and demanding prose for a
    /// corrected typo is how a workflow gets routed around.</summary>
    public string? Reason { get; set; }

    public EventChangeRequestStatus Status { get; set; } = EventChangeRequestStatus.Pending;

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>An <see cref="EventReviewReason"/> name on a rejection — the same closed vocabulary the
    /// event review itself uses, never a free-form string.</summary>
    public string? ReviewReasonCode { get; set; }

    /// <summary>The reviewer's words, shown to the host. A refusal they cannot act on is a dead end.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>Set only on an approved request that actually landed, so "approved" and "applied" can never
    /// be assumed to be the same moment if the apply is ever made asynchronous.</summary>
    public DateTime? AppliedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
