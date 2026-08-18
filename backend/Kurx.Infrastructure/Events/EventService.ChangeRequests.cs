using System.Globalization;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Events;

/// <summary>D-388 — the change-request half of <see cref="EventService"/>.
///
/// <para>A partial rather than a service of its own: approving a change request IS an event update, so it
/// must run through <c>ApplyUpdateAsync</c> — D-191's one and only event-update implementation — and reuse
/// <c>CanManageEventAsync</c>. A separate class could reach neither without duplicating both, and a second
/// event-mutation path is exactly what D-191 exists to prevent.</para></summary>
public partial class EventService
{
    private static readonly JsonSerializerOptions ProposalJson = new(JsonSerializerDefaults.Web);

    public async Task<ServiceResult<EventChangeRequestView>> CreateChangeRequestAsync(Guid userId, Guid eventId,
        UpdateEventInput input, string? reason, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventChangeRequestView>.Fail("not_found");
        if (!await CanManageEventAsync(userId, ev, isAdmin: false, ct))
            return ServiceResult<EventChangeRequestView>.Fail("forbidden");

        // A change request exists because direct editing is refused. Where it is NOT refused — a draft, an
        // approved-but-unpublished event, a Private product — offering one too would be a second way to do
        // the same thing, and the host would have no way to tell which one their edit went through.
        if (!EventStatusWorkflow.IsLiveProtected(ev.Product, ev.Status))
            return ServiceResult<EventChangeRequestView>.Fail("not_live_protected");

        // `IsFeatured` is platform curation, never a host's to propose. Stripped rather than refused: the
        // web form posts the whole record, and rejecting the save over a field the host cannot even see
        // would be an unanswerable error. Everything else the host sent stands.
        var proposal = input with { IsFeatured = null };
        var changes = DescribeChanges(ev, proposal);
        if (changes.Count == 0) return ServiceResult<EventChangeRequestView>.Fail("no_changes");

        // Replace rather than add. The unique partial index makes a second pending row impossible anyway;
        // doing it here means the host's newer proposal wins instead of their save failing on a conflict
        // they cannot see or clear.
        var existing = await db.EventChangeRequests
            .FirstOrDefaultAsync(c => c.EventId == eventId && c.Status == EventChangeRequestStatus.Pending, ct);

        var previous = changes.ToDictionary(c => c.Field, c => c.Current);
        if (existing is null)
        {
            existing = new EventChangeRequest { EventId = eventId, RequestedBy = userId };
            db.EventChangeRequests.Add(existing);
        }
        else if (existing.RequestedBy != userId)
        {
            // Two managers of the same event. The later proposal replaces the earlier one and the record
            // says who owns it now — silently keeping the first author's name on someone else's values
            // would misattribute the decision a reviewer is about to make.
            existing.RequestedBy = userId;
        }

        existing.BaseVersion = ev.Version;
        existing.ProposedJson = JsonSerializer.Serialize(proposal, ProposalJson);
        existing.PreviousJson = JsonSerializer.Serialize(previous, ProposalJson);
        existing.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        existing.Status = EventChangeRequestStatus.Pending;
        // A replaced request carries no verdict: leaving a previous reviewer's rejection reason attached to
        // values they never saw would show the host a refusal of something else.
        existing.ReviewedBy = null;
        existing.ReviewedAt = null;
        existing.ReviewReasonCode = null;
        existing.ReviewNotes = null;
        existing.UpdatedAt = DateTime.UtcNow;

        audit.Write(new AuditEvent("event.change_request.submitted", "events", eventId, "user", userId,
            null, new { changeRequestId = existing.Id, baseVersion = ev.Version, fields = changes.Select(c => c.Field) }));
        await db.SaveChangesAsync(ct);

        log.LogInformation("Event {EventId} change request {ChangeRequestId} submitted against version {Version}",
            eventId, existing.Id, ev.Version);
        return ServiceResult<EventChangeRequestView>.Success(await ToViewAsync(existing, ev, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<EventChangeRequestView>>> ListChangeRequestsAsync(Guid userId,
        Guid eventId, bool isAdmin, bool isReviewer, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<IReadOnlyList<EventChangeRequestView>>.Fail("not_found");
        if (!isAdmin && !isReviewer && !await CanManageEventAsync(userId, ev, isAdmin: false, ct))
            return ServiceResult<IReadOnlyList<EventChangeRequestView>>.Fail("forbidden");

        var rows = await db.EventChangeRequests.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var views = new List<EventChangeRequestView>(rows.Count);
        foreach (var r in rows) views.Add(await ToViewAsync(r, ev, ct));
        return ServiceResult<IReadOnlyList<EventChangeRequestView>>.Success(views);
    }

    public async Task<ServiceResult<EventChangeRequestView>> WithdrawChangeRequestAsync(Guid userId, Guid eventId,
        Guid changeRequestId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventChangeRequestView>.Fail("not_found");
        if (!await CanManageEventAsync(userId, ev, isAdmin: false, ct))
            return ServiceResult<EventChangeRequestView>.Fail("forbidden");

        var cr = await db.EventChangeRequests.FirstOrDefaultAsync(c => c.Id == changeRequestId && c.EventId == eventId, ct);
        if (cr is null) return ServiceResult<EventChangeRequestView>.Fail("not_found");
        if (cr.Status != EventChangeRequestStatus.Pending)
            return ServiceResult<EventChangeRequestView>.Fail("change_request_decided");

        // Claimed in SQL against the status this request read, the same compare-and-swap the review
        // transitions use (D-266 M4): a reviewer may be deciding this very row, and withdrawing over their
        // verdict would erase a decision that has already been recorded.
        var moved = await db.EventChangeRequests
            .Where(c => c.Id == changeRequestId && c.Status == EventChangeRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, EventChangeRequestStatus.Withdrawn)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), ct);
        if (moved == 0) return ServiceResult<EventChangeRequestView>.Fail("change_request_decided");

        audit.Write(new AuditEvent("event.change_request.withdrawn", "events", eventId, "user", userId,
            null, new { changeRequestId }));
        await db.SaveChangesAsync(ct);
        // ExecuteUpdateAsync writes past the change tracker, so the tracked copy still reads Pending.
        await db.Entry(cr).ReloadAsync(ct);
        return ServiceResult<EventChangeRequestView>.Success(await ToViewAsync(cr, ev, ct));
    }

    public async Task<ServiceResult<EventChangeRequestView>> DecideChangeRequestAsync(Guid reviewerId, Guid eventId,
        Guid changeRequestId, bool approve, string? reasonCode, string? notes, bool isAdmin, bool isReviewer,
        CancellationToken ct = default)
    {
        // The verdict is a reviewer's. Checked before anything is read about the event so that a host
        // probing this route learns nothing about the queue.
        if (!isReviewer && !isAdmin) return ServiceResult<EventChangeRequestView>.Fail("reviewer_required");

        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<EventChangeRequestView>.Fail("not_found");

        var cr = await db.EventChangeRequests.FirstOrDefaultAsync(c => c.Id == changeRequestId && c.EventId == eventId, ct);
        if (cr is null) return ServiceResult<EventChangeRequestView>.Fail("not_found");
        if (cr.Status != EventChangeRequestStatus.Pending)
            return ServiceResult<EventChangeRequestView>.Fail("change_request_decided");

        // A host cannot approve their own proposal, even holding a reviewer role. Same rule the event
        // review already enforces on `approve_review`; stated again here because this is a second door to
        // the same act, and a rule written at one door is not a rule.
        if (cr.RequestedBy == reviewerId)
            return ServiceResult<EventChangeRequestView>.Fail("cannot_review_own_request");

        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(reasonCode)) return ServiceResult<EventChangeRequestView>.Fail("reason_required");
            if (!Enum.TryParse<EventReviewReason>(reasonCode, ignoreCase: true, out var parsed))
                return ServiceResult<EventChangeRequestView>.Fail("invalid_reason_code");

            var rejected = await ClaimDecisionAsync(changeRequestId, EventChangeRequestStatus.Rejected, reviewerId,
                parsed.ToString(), notes, ct);
            if (!rejected) return ServiceResult<EventChangeRequestView>.Fail("change_request_decided");

            // The live event is deliberately untouched on this path — not restored, not reverted, never
            // written to at all. That is the whole guarantee: a rejection cannot leave a mark on it.
            db.VerificationReviews.Add(new VerificationReview
            {
                SubjectType = VerificationSubjectType.Event,
                SubjectId = eventId,
                Decision = VerificationDecision.Reject,
                ReviewerId = reviewerId,
                ReasonCode = parsed.ToString(),
                Notes = notes,
            });
            audit.Write(new AuditEvent("event.change_request.rejected", "events", eventId, "admin", reviewerId,
                null, new { changeRequestId, reasonCode = parsed.ToString(), notes }));
            await db.SaveChangesAsync(ct);
            await db.Entry(cr).ReloadAsync(ct);
            log.LogInformation("Event {EventId} change request {ChangeRequestId} rejected ({Reason})", eventId, changeRequestId, parsed);
            return ServiceResult<EventChangeRequestView>.Success(await ToViewAsync(cr, ev, ct));
        }

        // ── Approval ────────────────────────────────────────────────────────────────────────────────
        // Everything from here is one transaction. A live event must never end up with the new title and
        // the old date because a validation failed halfway through the field groups.
        if (cr.BaseVersion != ev.Version)
            return ServiceResult<EventChangeRequestView>.Fail("version_conflict");

        // The proposer must still be entitled to change this event. A pending request outlives the seat it
        // was made from: someone removed from the org between proposing and the decision must not have
        // their values applied in their name because a reviewer clicked approve.
        if (!await CanManageEventAsync(cr.RequestedBy, ev, isAdmin: false, ct))
            return ServiceResult<EventChangeRequestView>.Fail("requester_no_longer_authorized");

        var input = JsonSerializer.Deserialize<UpdateEventInput>(cr.ProposedJson, ProposalJson);
        if (input is null) return ServiceResult<EventChangeRequestView>.Fail("invalid_proposal");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Claimed in SQL inside the transaction. Two reviewers pressing approve in the same moment both
        // pass every gate above; only one wins this, and the loser's transaction applies nothing.
        var claimed = await ClaimDecisionAsync(changeRequestId, EventChangeRequestStatus.Approved, reviewerId, null, notes, ct);
        if (!claimed)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<EventChangeRequestView>.Fail("change_request_decided");
        }

        // The one and only apply path (D-191). Validation, the D-265 field groups, the §14.5 refund window
        // and registrant notification, the capability rematerialization and the search reindex all come
        // from here — an apply written separately would have had to reproduce every one of them.
        //
        // Attributed to the REQUESTER, not the reviewer: the §14.5 material-change audit and refund window
        // record who changed the event, and that is the host. Who permitted it is the separate audit entry
        // written below.
        var applied = await ApplyUpdateAsync(ev, cr.RequestedBy, isAdmin: false, input, ct);
        if (!applied.Ok)
        {
            // Nothing partially written survives: the whole transaction goes, including the status claim,
            // so the request is still Pending and the reviewer sees why it could not be applied.
            await tx.RollbackAsync(ct);
            log.LogWarning("Event {EventId} change request {ChangeRequestId} approval refused by the update path: {Error}",
                eventId, changeRequestId, applied.Error);
            return ServiceResult<EventChangeRequestView>.Fail(applied.Error!);
        }

        await db.EventChangeRequests
            .Where(c => c.Id == changeRequestId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AppliedAt, DateTime.UtcNow), ct);

        db.VerificationReviews.Add(new VerificationReview
        {
            SubjectType = VerificationSubjectType.Event,
            SubjectId = eventId,
            Decision = VerificationDecision.Approve,
            ReviewerId = reviewerId,
            Notes = notes,
        });
        audit.Write(new AuditEvent("event.change_request.approved", "events", eventId, "admin", reviewerId,
            JsonSerializer.Deserialize<Dictionary<string, string?>>(cr.PreviousJson, ProposalJson),
            new { changeRequestId, requestedBy = cr.RequestedBy, baseVersion = cr.BaseVersion, newVersion = ev.Version }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        log.LogInformation("Event {EventId} change request {ChangeRequestId} approved: version {Base} to {New}",
            eventId, changeRequestId, cr.BaseVersion, ev.Version);
        await db.Entry(cr).ReloadAsync(ct);
        return ServiceResult<EventChangeRequestView>.Success(await ToViewAsync(cr, ev, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<EventChangeRequestView>>> ListPendingChangeRequestsAsync(int limit,
        CancellationToken ct = default)
    {
        var rows = await db.EventChangeRequests.AsNoTracking()
            .Where(c => c.Status == EventChangeRequestStatus.Pending)
            .OrderBy(c => c.CreatedAt)                     // oldest first: a queue, not a feed
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);
        if (rows.Count == 0)
            return ServiceResult<IReadOnlyList<EventChangeRequestView>>.Success([]);

        var eventIds = rows.Select(r => r.EventId).Distinct().ToList();
        var events = await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        var views = new List<EventChangeRequestView>(rows.Count);
        foreach (var r in rows)
            if (events.TryGetValue(r.EventId, out var ev))
                views.Add(await ToViewAsync(r, ev, ct));
        return ServiceResult<IReadOnlyList<EventChangeRequestView>>.Success(views);
    }

    /// <summary>Moves a pending request to its verdict, conditional on it still being pending IN SQL.
    /// Returns false when someone else decided it first.</summary>
    private async Task<bool> ClaimDecisionAsync(Guid changeRequestId, EventChangeRequestStatus to, Guid reviewerId,
        string? reasonCode, string? notes, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var moved = await db.EventChangeRequests
            .Where(c => c.Id == changeRequestId && c.Status == EventChangeRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, to)
                .SetProperty(c => c.ReviewedBy, (Guid?)reviewerId)
                .SetProperty(c => c.ReviewedAt, (DateTime?)now)
                .SetProperty(c => c.ReviewReasonCode, reasonCode)
                .SetProperty(c => c.ReviewNotes, notes)
                .SetProperty(c => c.UpdatedAt, now), ct);
        return moved > 0;
    }

    private async Task<EventChangeRequestView> ToViewAsync(EventChangeRequest cr, Event ev, CancellationToken ct)
    {
        var proposal = JsonSerializer.Deserialize<UpdateEventInput>(cr.ProposedJson, ProposalJson);

        // CURRENT is read from the live row, not from `PreviousJson`. A reviewer decides against what the
        // event IS, and the two disagreeing is precisely the staleness the version check refuses.
        IReadOnlyList<EventChangeField> changes = proposal is null ? [] : DescribeChanges(ev, proposal);

        var names = await db.Users.AsNoTracking()
            .Where(u => u.Id == cr.RequestedBy || (cr.ReviewedBy != null && u.Id == cr.ReviewedBy))
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(ct);

        return new EventChangeRequestView(
            cr.Id, cr.EventId, ev.Title, cr.RequestedBy,
            names.FirstOrDefault(n => n.Id == cr.RequestedBy)?.Name,
            cr.BaseVersion, ev.Version,
            // Only a PENDING request can be stale in a way that matters — a decided one is history, and
            // flagging it against a version it was never going to be applied at reads as a fault.
            cr.Status == EventChangeRequestStatus.Pending && cr.BaseVersion != ev.Version,
            changes, cr.Reason, cr.Status.ToString().ToLowerInvariant(),
            cr.ReviewedBy, names.FirstOrDefault(n => n.Id == cr.ReviewedBy)?.Name, cr.ReviewedAt,
            cr.ReviewReasonCode, cr.ReviewNotes, cr.CreatedAt, cr.UpdatedAt, cr.AppliedAt);
    }

    /*
     * The CURRENT-vs-PROPOSED diff a reviewer actually reads.
     *
     * Built here, on the server, rather than by each client: three surfaces formatting the same comparison
     * three ways is three chances to show a reviewer a difference that is not there (a timezone applied
     * twice, a null rendered as the string "null"). The label travels with the value for the same reason.
     *
     * Only fields whose value genuinely CHANGES are listed. The predicate that decides whether a change
     * request is needed tests presence in the payload (D-363's stated behaviour, so a form posting the
     * whole record still routes correctly); this tests equality, so the reviewer is not handed forty
     * unchanged rows to read past.
     */
    private static List<EventChangeField> DescribeChanges(Event ev, UpdateEventInput i)
    {
        var changes = new List<EventChangeField>();

        void Text(string field, string label, string? proposed, string? current)
        {
            if (proposed is null) return;
            var p = proposed.Trim();
            var c = (current ?? "").Trim();
            if (string.Equals(p, c, StringComparison.Ordinal)) return;
            changes.Add(new EventChangeField(field, label, c.Length == 0 ? null : c, p.Length == 0 ? null : p));
        }

        void When(string field, string label, DateTime? proposed, DateTime current)
        {
            if (proposed is null) return;
            var p = DateTime.SpecifyKind(proposed.Value, DateTimeKind.Utc);
            if (p == current) return;
            changes.Add(new EventChangeField(field, label, Stamp(current), Stamp(p)));
        }

        void Number(string field, string label, int? proposed, int? current)
        {
            if (proposed is null || proposed == current) return;
            changes.Add(new EventChangeField(field, label,
                current?.ToString(CultureInfo.InvariantCulture), proposed.Value.ToString(CultureInfo.InvariantCulture)));
        }

        Text("title", "Title", i.Title, ev.Title);
        Text("subtitle", "Subtitle", i.Subtitle, ev.Subtitle);
        Text("description", "Description", i.Description, ev.Description);
        Text("visibility", "Visibility", i.Visibility, ev.Visibility.ToString());
        Text("eventMode", "Delivery mode", i.EventMode, ev.EventMode.ToString());
        Text("onlineUrl", "Joining link", i.OnlineUrl, ev.OnlineUrl);
        Text("venueName", "Venue", i.VenueName, ev.VenueName);
        Text("venueAddress", "Venue address", i.VenueAddress, ev.VenueAddress);
        Text("city", "City", i.City, ev.City);
        Text("timezone", "Timezone", i.Timezone, ev.Timezone);
        When("startsAt", "Starts", i.StartsAt, ev.StartsAt);
        When("endsAt", "Ends", i.EndsAt, ev.EndsAt);
        Number("capacity", "Capacity", i.Capacity, ev.Capacity);

        // Taxonomy is ids, not display text. Named as changed without an inline label rather than rendered
        // as a raw GUID a reviewer cannot read — the console resolves them against the dossier it loads.
        if (i.CategoryId is { } cat && cat != ev.CategoryId)
            changes.Add(new EventChangeField("categoryId", "Category", ev.CategoryId.ToString(), cat.ToString()));
        if (i.TypeId is { } type && type != ev.TypeId)
            changes.Add(new EventChangeField("typeId", "Type", ev.TypeId?.ToString(), type.ToString()));
        if (i.AudienceLevelId is { } aud && aud != ev.AudienceLevelId)
            changes.Add(new EventChangeField("audienceLevelId", "Audience level", ev.AudienceLevelId?.ToString(), aud.ToString()));
        if (i.Eligibility is not null)
            changes.Add(new EventChangeField("eligibility", "Eligibility", DescribeEligibility(ev), Describe(i.Eligibility)));
        if (i.Legal is not null)
            changes.Add(new EventChangeField("legal", "Legal terms", "see event", "updated"));
        if (i.Commerce is not null)
            changes.Add(new EventChangeField("commerce", "Commercial terms", "see event", "updated"));

        return changes;

        static string Stamp(DateTime d) => d.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        static string Age(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "any";
        static string DescribeEligibility(Event e) => $"{Age(e.MinAge)}-{Age(e.MaxAge)}, {e.GenderRestriction}";
        static string Describe(EventEligibilityInput e) => $"{Age(e.MinAge)}-{Age(e.MaxAge)}, {e.GenderRestriction ?? "Any"}";
    }
}
