using Kurx.Application.Abstractions;
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
            // D-363 §2: `Approved` was absent, so an organiser who had been approved and then decided not
            // to run the event had no way to say so — the only exits were forward.
            ["cancel"] = [(EventStatus.Draft, EventStatus.Cancelled), (EventStatus.PendingReview, EventStatus.Cancelled),
                          (EventStatus.UnderReview, EventStatus.Cancelled), (EventStatus.Approved, EventStatus.Cancelled),
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
            //
            // D-363 §1: also from `Approved`. An approved event has never been public, so it can hold no
            // orders and nothing is lost by returning it to `Draft` — which is where its creator can edit
            // and resubmit. Without this, deciding to rework an approved event was a dead end. It is the
            // same word for the same act (take it back out of the review process), so it is the same
            // action rather than a second one meaning the same thing.
            ["withdraw"] = [(EventStatus.PendingReview, EventStatus.Draft),
                            (EventStatus.Approved, EventStatus.Draft)],
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

    /*
     * D-363 §4 — approval binds to what was reviewed.
     *
     * `IsEditLocked` covered the review states only, so an APPROVED event was fully editable with no
     * re-review: a creator could be approved, then change the title, the dates, the venue and the
     * eligibility rules, and publish something no reviewer ever saw. The approval was real; what it
     * applied to was not.
     *
     * The split is between edits that change WHAT WAS ASSESSED and edits that dress it. A reviewer
     * reads the title, when and where it runs, who may attend, what it costs and what it commits the
     * platform to. They do not read the promo video. So the tagline, banner, FAQ, rules text, contact
     * details and registration windows stay free — forcing a fresh review to fix a typo in the FAQ
     * would make re-review something organisers route around rather than respect.
     *
     * Two things are LOCKED after approval rather than re-reviewed, and both are locked by absence
     * rather than by a check here: `EventProduct` (Public/Private) and the representing organization
     * are not fields on `UpdateEventInput` at all. Changing either invalidates the eligibility gate the
     * event was created under (D-323), which is a different act from editing it. `TypeId` IS here and
     * derives Product (D-266 M1), which is why it counts as material below.
     */
    public static bool RequiresFreshReview(UpdateEventInput i) =>
        // What it is, and who is on the hook for it.
        i.Title is not null || i.CategoryId is not null || i.TypeId is not null
        || i.AudienceLevelId is not null || i.TemplateId is not null
        // When and where it runs. Timezone counts: it moves the effective times without touching them.
        || i.StartsAt is not null || i.EndsAt is not null || i.Timezone is not null
        || i.VenueId is not null || i.VenueName is not null || i.VenueAddress is not null
        || i.City is not null || i.EventMode is not null || i.OnlineUrl is not null
        || i.Location is not null
        // How many, who may come, and what it commits us to.
        || i.Capacity is not null || i.Visibility is not null
        || i.Eligibility is not null || i.Legal is not null || i.Commerce is not null;

    // Free by construction — anything not named above: subtitle, description prose, tags, banner, all of
    // `Content` (tagline, short description, logo, thumbnail, promo video, rules, FAQ), contact details,
    // website, socials, `Schedule` (registration/check-in windows), language, standalone listing.

    /*
     * D-388 — a LIVE event's substance is not the host's to change.
     *
     * D-363 §4 (`RequiresFreshReview` above) covered `Approved`, which is reviewed but NOT public: nobody
     * has seen it, it can hold no orders, so applying an edit and returning it to the queue loses nothing.
     * `Published`/`Scheduled`/`Live` are a different situation entirely and had NO guard at all — the
     * owner of an event people had already registered for could retitle it and move its dates in one
     * PATCH, with no reviewer, no notification and no audit row. Reproduced over HTTP before this existed.
     *
     * So the same edit takes a different route depending on where the event stands:
     *
     *   Draft / ChangesRequested / Rejected → applied directly
     *   PendingReview / UnderReview         → refused, `event_under_review` (unchanged)
     *   Approved                            → applied, event returns to the queue (D-363 §4, unchanged)
     *   Published / Scheduled / Live        → refused, `change_request_required`  ← this
     *
     * PUBLIC ONLY. A Private product is never reviewed — its host is its only audience — so it keeps
     * direct editing at every status, and putting an admin between a family and their own wedding page
     * would be an absurdity, not a safeguard.
     */

    /// <summary>Whether this event's protected substance is frozen behind an approved change request:
    /// a Public product that is publicly live. The one definition — <c>EventService</c>,
    /// <c>TicketTypeService</c>, <c>AudienceService</c> and <c>EventAuthorizationService</c> all call it,
    /// because §4's lesson was that a guard on the one path you thought of holds only until someone adds
    /// a second.</summary>
    public static bool IsLiveProtected(EventProduct product, EventStatus status) =>
        product == EventProduct.Public
        && status is EventStatus.Published or EventStatus.Scheduled or EventStatus.Live;

    /// <summary>The protected set for a LIVE event: everything a reviewer assessed
    /// (<see cref="RequiresFreshReview"/>) plus the two public-facing prose fields.
    ///
    /// <para><b>Subtitle and Description are protected here and free before publication</b>, which is not
    /// an inconsistency. Before an event is public, prose is draft copy and a re-review to fix a typo is a
    /// rule organisers route around. After it is public, the description is the single most abusable field
    /// on the listing: it is what an attendee read before paying, and swapping it out is a bait-and-switch
    /// no other guard would catch. Tagline, short description, rules, FAQ, banner, contact details and the
    /// registration windows stay free at every status — presentation and operations, not the offer.</para></summary>
    public static bool RequiresApprovalWhenLive(UpdateEventInput i) =>
        RequiresFreshReview(i) || i.Subtitle is not null || i.Description is not null;

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
