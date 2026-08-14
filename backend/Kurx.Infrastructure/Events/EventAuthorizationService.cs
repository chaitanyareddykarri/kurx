using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M5 — institutional authorization for one event.
///
/// <para>Performs no authorization logic of its own: standing is resolved once through
/// <see cref="IEventAuthority"/> (D-269) and every branch is a threshold on it. Because "authorization"
/// (this evidence) and "authority" (that permission) share a word, each decision point below says which
/// one it means.</para>
///
/// <para>Decisions are recorded on the row itself and in the typed audit spine (D-102). They are
/// deliberately <b>not</b> written to <c>VerificationReview</c>: that store backs
/// <c>GetReviewHistoryAsync</c>, which filters on <c>SubjectType = Event</c>, so an authorization verdict
/// written there would appear in the event's own review history as though a reviewer had ruled on the
/// event. Two different decisions about two different subjects must not share one timeline.</para></summary>
public class EventAuthorizationService(
    KurxDbContext db,
    IEventAuthority authority,
    IStorage storage,
    IAuditWriter audit,
    Providers.UploadScanGate scanGate) : IEventAuthorizationService
{
    public async Task<ServiceResult<EventAuthorizationView>> SubmitAsync(Guid userId, Guid eventId, bool isAdmin,
        EventAuthorizationInput input, CancellationToken ct = default)
    {
        var (ev, error) = await ManageableEventAsync(userId, eventId, isAdmin, ct);
        if (error is not null) return ServiceResult<EventAuthorizationView>.Fail(error);

        // The same lock the event's own PATCH honours (D-266 M4). Evidence that moves while a reviewer is
        // reading it means they decide on something other than what they read — the exact failure the edit
        // lock exists to prevent, and there is no reason it should stop at the event's own columns.
        if (EventStatusWorkflow.IsEditLocked(ev!.Status))
            return ServiceResult<EventAuthorizationView>.Fail("event_under_review");

        var head = input.HeadName?.Trim();
        var designation = input.HeadDesignation?.Trim();
        var email = input.OfficialEmail?.Trim();
        var phone = input.OfficialPhone?.Trim();
        var role = input.RepresentativeRole?.Trim();
        if (string.IsNullOrWhiteSpace(head) || string.IsNullOrWhiteSpace(designation)
            || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone)
            || string.IsNullOrWhiteSpace(role))
            return ServiceResult<EventAuthorizationView>.Fail("authorization_fields_required");

        // The list is closed here or it is not closed at all: an unvalidated vocabulary is free text that
        // merely looks analysable, and a reviewer reading "Supreme Overlord" in a role field cannot tell
        // whether the platform vouched for that word.
        if (!RepresentativeRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            return ServiceResult<EventAuthorizationView>.Fail("representative_role_invalid");

        // A closed list that cannot express a real title pushes people into picking a wrong one, which is
        // worse for a reviewer than an open field. `Other` is the escape hatch and must carry the words.
        var roleOther = input.RepresentativeRoleOther?.Trim();
        if (role.Equals("Other", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(roleOther))
            return ServiceResult<EventAuthorizationView>.Fail("representative_role_other_required");

        // E.164 — the format every downstream sender expects. Enforced here rather than only at the edge
        // because a stored number the platform cannot dial is a signatory nobody can verify.
        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+[1-9]\d{7,14}$"))
            return ServiceResult<EventAuthorizationView>.Fail("official_phone_invalid");

        // A named Kurx account is a LINK, never a grant (see the entity remarks) — so the only check is
        // that the person exists. Naming them confers no authority over the event.
        if (input.RepresentativeUserId is { } repId
            && !await db.Users.AsNoTracking().AnyAsync(u => u.Id == repId, ct))
            return ServiceResult<EventAuthorizationView>.Fail("representative_user_not_found");

        var row = await db.EventAuthorizations.FirstOrDefaultAsync(a => a.EventId == eventId, ct);

        // Omitting a document means "keep the one on file", never "delete it". A client CANNOT resend a
        // document key: reads hand back presigned URLs and never keys, by design. Treating omission as
        // removal would mean correcting a typo in the signatory's phone silently destroyed the letter the
        // whole record exists to hold — and the supporting documents a reviewer may already have read.
        var letterhead = string.IsNullOrWhiteSpace(input.LetterheadDocumentKey)
            ? row?.LetterheadDocumentKey
            : input.LetterheadDocumentKey;

        // The letter on institutional letterhead is the substance of the claim. Without a document this is
        // a name typed into a form, which proves nothing and would put a reviewer's time on the line for it.
        if (string.IsNullOrWhiteSpace(letterhead))
            return ServiceResult<EventAuthorizationView>.Fail("letterhead_required");

        var signature = string.IsNullOrWhiteSpace(input.SignatureDocumentKey)
            ? row?.SignatureDocumentKey
            : input.SignatureDocumentKey;

        var docs = input.SupportingDocumentKeys is null
            ? ParseDocuments(row?.SupportingDocumentsJson ?? "[]")
            : input.SupportingDocumentKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Take(10).ToList();

        // D-338 — the letterhead is "the substance of the claim" per the guard above, and a reviewer opens
        // it. Only the keys this call is CLAIMING are scanned: `letterhead`, `signature` and `docs` fall
        // back to the values already on the row, and re-scanning those would re-refuse a submission whose
        // evidence was accepted before the scanner existed, on an edit that never touched a document.
        var claimed = new[]
        {
            input.LetterheadDocumentKey,
            input.SignatureDocumentKey,
        }.Concat(input.SupportingDocumentKeys ?? []);

        if (await scanGate.RejectAnyAsync(claimed, userId, "event_authorizations", eventId, ct) is { } scanError)
            return ServiceResult<EventAuthorizationView>.Fail(scanError);

        void Apply(EventAuthorization target)
        {
            target.SubmittedBy = userId;
            target.HeadName = head;
            target.HeadDesignation = designation;
            target.OfficialEmail = email;
            target.OfficialPhone = phone;
            target.RepresentativeRole = role;
            // Cleared when the role is not Other: a stale custom title beside a chosen one is two answers
            // to "what is their role", and a reviewer cannot tell which is current.
            target.RepresentativeRoleOther =
                role.Equals("Other", StringComparison.OrdinalIgnoreCase) ? roleOther : null;
            target.RepresentativeUserId = input.RepresentativeUserId;
            target.LetterheadDocumentKey = letterhead;
            target.SignatureDocumentKey = signature;
            target.SupportingDocumentsJson = JsonSerializer.Serialize(docs);
            target.UpdatedAt = DateTime.UtcNow;

            // Resubmitting clears the prior verdict. Keeping an Approved stamp on evidence that has since
            // been replaced would let an organiser swap the letter after approval and publish on the old
            // decision.
            target.Status = EventAuthorizationStatus.Submitted;
            target.ReviewerId = null;
            target.ReviewedAt = null;
            target.ReasonCode = null;
            target.Notes = null;
        }

        var isResubmission = row is not null;
        if (row is null)
        {
            // Claim the row in its own save BEFORE the audit entry is staged. Two managers pressing Save at
            // the same moment both see no row; the unique index on EventId decides which insert survives —
            // it is what keeps "is this event authorised?" a question with one answer — and the loser adopts
            // the winner rather than failing the organiser with a 500. Settling identity first is what stops
            // the audit entry below from naming a row that lost.
            row = new EventAuthorization { EventId = eventId, SubmittedBy = userId };
            Apply(row);
            db.EventAuthorizations.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.Entry(row).State = EntityState.Detached;
                var winner = await db.EventAuthorizations.FirstOrDefaultAsync(a => a.EventId == eventId, ct);
                if (winner is null) throw;   // not the race — a genuine failure, and it must not be swallowed
                row = winner;
            }
        }

        Apply(row);
        audit.Write(new AuditEvent("event_authorization.submitted", "event_authorizations", row.Id,
            ActorType: "user", ActorId: userId,
            After: new { event_id = eventId, resubmission = isResubmission }));

        await db.SaveChangesAsync(ct);
        return ServiceResult<EventAuthorizationView>.Success(await ProjectAsync(row, ct));
    }

    public async Task<ServiceResult<EventAuthorizationView?>> GetAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct = default)
    {
        var (_, error) = await ManageableEventAsync(userId, eventId, isAdmin, ct);
        if (error is not null) return ServiceResult<EventAuthorizationView?>.Fail(error);

        var row = await db.EventAuthorizations.AsNoTracking().FirstOrDefaultAsync(a => a.EventId == eventId, ct);
        // Nothing filed is a legitimate state, not a 404: most events never need an authorization, and a
        // client asking "is there one?" deserves an answer rather than an error.
        return ServiceResult<EventAuthorizationView?>.Success(row is null ? null : await ProjectAsync(row, ct));
    }

    public async Task<ServiceResult<PresignedUpload>> PresignDocumentAsync(Guid userId, Guid eventId, bool isAdmin,
        string contentType, long maxBytes, CancellationToken ct = default)
    {
        var (_, error) = await ManageableEventAsync(userId, eventId, isAdmin, ct);
        if (error is not null) return ServiceResult<PresignedUpload>.Fail(error);

        // Keyed under the event, so one event's evidence can never collide with another's and a leaked
        // key discloses nothing about where anyone else's documents live.
        var key = $"events/{eventId}/authorization/{Guid.NewGuid():N}";
        return ServiceResult<PresignedUpload>.Success(await storage.PresignPutAsync(key, contentType, maxBytes, ct));
    }

    public async Task<EventAuthorizationView?> GetForReviewAsync(Guid eventId, CancellationToken ct = default)
    {
        var row = await db.EventAuthorizations.AsNoTracking().FirstOrDefaultAsync(a => a.EventId == eventId, ct);
        return row is null ? null : await ProjectAsync(row, ct);
    }

    public async Task<ServiceResult<EventAuthorizationView>> ReviewAsync(Guid reviewerId, Guid eventId, string decision,
        string? reasonCode, string? notes, CancellationToken ct = default)
    {
        var row = await db.EventAuthorizations.FirstOrDefaultAsync(a => a.EventId == eventId, ct);
        if (row is null) return ServiceResult<EventAuthorizationView>.Fail("not_found");

        var target = decision.Trim().ToLowerInvariant() switch
        {
            "approve" => (EventAuthorizationStatus?)EventAuthorizationStatus.Approved,
            "reject" => EventAuthorizationStatus.Rejected,
            "request_changes" => EventAuthorizationStatus.ChangesRequested,
            _ => null,
        };
        if (target is not { } status) return ServiceResult<EventAuthorizationView>.Fail("invalid_decision");

        // Same rule and same closed vocabulary as the event review lifecycle (D-266 M4): a refusal with no
        // recorded reason is one the organiser cannot act on and the platform cannot analyse.
        if (status == EventAuthorizationStatus.Rejected
            && (string.IsNullOrWhiteSpace(reasonCode) || !Enum.TryParse<EventReviewReason>(reasonCode, out _)))
            return ServiceResult<EventAuthorizationView>.Fail("reason_code_required");

        // ChangesRequested exists so the organiser can act on it; without notes it says only "no".
        if (status == EventAuthorizationStatus.ChangesRequested && string.IsNullOrWhiteSpace(notes))
            return ServiceResult<EventAuthorizationView>.Fail("notes_required");

        var before = row.Status;
        row.Status = status;
        row.ReviewerId = reviewerId;
        row.ReviewedAt = DateTime.UtcNow;
        row.ReasonCode = status == EventAuthorizationStatus.Approved ? null : reasonCode;
        row.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        row.UpdatedAt = DateTime.UtcNow;

        // D-266 M5 — tell the organiser. A verdict they are never told about is one they can only find by
        // reopening the page and noticing a badge changed; on a rejection that means the event silently
        // cannot publish, and the reviewer's reason — the one thing that makes it fixable — is never
        // delivered. Sent to whoever FILED it, which is not necessarily the event's owner.
        var eventTitle = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => e.Title).FirstOrDefaultAsync(ct) ?? "your event";
        db.Notifications.Add(new Notification
        {
            UserId = row.SubmittedBy,
            Kind = status switch
            {
                EventAuthorizationStatus.Approved => "event_authorization_approved",
                EventAuthorizationStatus.Rejected => "event_authorization_rejected",
                _ => "event_authorization_changes_requested",
            },
            Title = status switch
            {
                EventAuthorizationStatus.Approved => "Authorization approved",
                EventAuthorizationStatus.Rejected => "Authorization rejected",
                _ => "Authorization needs changes",
            },
            Body = status switch
            {
                EventAuthorizationStatus.Approved =>
                    $"The authorization for {eventTitle} was approved. You can publish when the rest of the checklist is clear.",
                // The reason is the actionable part, so it rides in the body rather than being left in a
                // panel the organiser has to go looking for.
                EventAuthorizationStatus.Rejected =>
                    $"The authorization for {eventTitle} was rejected{(row.ReasonCode is null ? "" : $" ({row.ReasonCode})")}."
                    + (row.Notes is null ? "" : $" {row.Notes}"),
                _ => $"The authorization for {eventTitle} needs changes." + (row.Notes is null ? "" : $" {row.Notes}"),
            },
            DataJson = $"{{\"event_id\":\"{eventId}\",\"authorization_status\":\"{status}\"}}",
        });

        audit.Write(new AuditEvent("event_authorization.reviewed", "event_authorizations", row.Id,
            ActorType: "admin", ActorId: reviewerId,
            Before: new { status = before.ToString() },
            After: new { status = row.Status.ToString(), reason_code = row.ReasonCode }));

        await db.SaveChangesAsync(ct);
        return ServiceResult<EventAuthorizationView>.Success(await ProjectAsync(row, ct));
    }

    /// <summary>Resolve standing once (D-269) and answer both questions every caller here needs: does this
    /// event exist <i>to this caller</i>, and may they manage its content. A hidden event answers 404 and
    /// never 403 (D-018) — a 403 confirms the event exists to someone not entitled to know that.</summary>
    private async Task<(Event? Event, string? Error)> ManageableEventAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists || !access.HasStanding) return (null, "not_found");
        if (!access.Can(EventPermission.ManageContent)) return (null, "forbidden");

        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        return ev is null ? (null, "not_found") : (ev, null);
    }

    /// <summary>Keys become short-lived URLs here and nowhere else, so no call site can forget.</summary>
    private async Task<EventAuthorizationView> ProjectAsync(EventAuthorization row, CancellationToken ct)
    {
        var docKeys = ParseDocuments(row.SupportingDocumentsJson);
        var docUrls = new List<string>(docKeys.Count);
        foreach (var k in docKeys) docUrls.Add(await storage.PresignGetAsync(k, ct: ct));

        // Resolved here so neither console fetches a user per row. Null when the account is gone: the
        // decision and the filed evidence outlive the accounts that produced them.
        var reviewerName = row.ReviewerId is null ? null : await db.Users.AsNoTracking()
            .Where(u => u.Id == row.ReviewerId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        var repUsername = row.RepresentativeUserId is null ? null : await db.Users.AsNoTracking()
            .Where(u => u.Id == row.RepresentativeUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);

        return new EventAuthorizationView(
            row.EventId, row.HeadName, row.HeadDesignation, row.OfficialEmail, row.OfficialPhone,
            row.LetterheadDocumentKey is null ? null : await storage.PresignGetAsync(row.LetterheadDocumentKey, ct: ct),
            row.SignatureDocumentKey is null ? null : await storage.PresignGetAsync(row.SignatureDocumentKey, ct: ct),
            docUrls,
            row.Status.ToString(), row.ReviewerId, row.ReviewedAt, row.ReasonCode, row.Notes,
            row.CreatedAt, row.UpdatedAt,
            row.RepresentativeRole, row.RepresentativeRoleOther,
            row.RepresentativeUserId, repUsername, reviewerName);
    }

    private static List<string> ParseDocuments(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
