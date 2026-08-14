using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>D-266 M5 — the organiser's evidence that the institution an event names has consented to
/// being represented by it.
///
/// <para><b>Not <c>IEventAuthority</c> (D-269), despite the shared word.</b> The authority service answers
/// <i>may this caller act on this event</i> — a live permission resolved per request, never stored. This
/// answers <i>has the represented institution authorised this event</i> — reviewed evidence that outlives
/// the request. Neither substitutes for the other: full manage authority over an event still does not let
/// anyone publish it in a college's name without the college's letter.</para>
///
/// <para><b>It belongs to the event, not to the organization.</b> A head of department authorises one
/// event, not every future event anyone files under the institution; hanging it off the organization would
/// silently turn a single signed letter into a standing licence. Organization-level trust is a separate,
/// already-built thing (<c>Organization.VerificationStatus</c>, D-044) and this neither replaces nor
/// duplicates it — an event under a verified college still needs its own authorization.</para></summary>
public class EventAuthorization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>One row per event. A second would make "is this event authorised?" ambiguous, which is the
    /// only question the row exists to answer.</summary>
    public Guid EventId { get; set; }

    /// <summary>Who filed it — deliberately distinct from the event's owner (<c>Event.CreatedBy</c>). A
    /// manager may file on the owner's behalf, and who produced the evidence is part of the evidence.</summary>
    public Guid SubmittedBy { get; set; }

    public string HeadName { get; set; } = null!;
    public string HeadDesignation { get; set; } = null!;
    public string OfficialEmail { get; set; } = null!;

    /// <summary>Required. A signatory who cannot be reached is not a verifiable one — the reviewer's only
    /// independent check on a letter is contacting the person who supposedly signed it.</summary>
    public string OfficialPhone { get; set; } = null!;

    /// <summary>The signatory's role from the platform's vocabulary (<c>Principal</c>, <c>Dean</c>,
    /// <c>HR Manager</c>, …) or <c>Other</c>. A closed list makes "who authorises institutional events"
    /// analysable; free text alone would make it unanswerable.</summary>
    public string RepresentativeRole { get; set; } = null!;

    /// <summary>The typed role when <see cref="RepresentativeRole"/> is <c>Other</c>. Required in that
    /// case and meaningless otherwise — a closed list that cannot express a real title would push people
    /// into choosing a wrong one, which is worse than an open field.</summary>
    public string? RepresentativeRoleOther { get; set; }

    /// <summary>Optional link to the signatory's Kurx account, when they have one.
    ///
    /// <para><b>A link, never a grant.</b> Naming a user here gives them no authority over the event and
    /// changes nothing about their account: it only lets a reviewer see the signatory is a known person
    /// rather than a name typed into a form. Authority remains <c>IEventAuthority</c>'s alone (D-269).</para></summary>
    public Guid? RepresentativeUserId { get; set; }

    /// <summary>Storage keys, never URLs — presigned on read, the same discipline every other evidence
    /// document on the platform follows. A stored URL either expires inside the database or never expires
    /// at all, and both are wrong for a document proving institutional consent.</summary>
    public string? LetterheadDocumentKey { get; set; }
    public string? SignatureDocumentKey { get; set; }

    /// <summary>jsonb <c>string[]</c> of additional storage keys. A list of opaque keys with no per-item
    /// state of its own does not earn a table.</summary>
    public string SupportingDocumentsJson { get; set; } = "[]";

    public EventAuthorizationStatus Status { get; set; } = EventAuthorizationStatus.Submitted;

    /// <summary>The platform reviewer who decided. Null until a decision is recorded.</summary>
    public Guid? ReviewerId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>An <see cref="EventReviewReason"/> name. The event review lifecycle's closed vocabulary is
    /// reused rather than duplicated: refusing an authorization is the same KIND of fact as refusing an
    /// event, and two parallel reason enums would drift the first time either gained a value.</summary>
    public string? ReasonCode { get; set; }

    /// <summary>Reviewer note. Organiser-facing on <c>ChangesRequested</c> — that state exists so they can
    /// act on it, and a request to change something without saying what is not one.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
