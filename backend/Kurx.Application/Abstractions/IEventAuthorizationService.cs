namespace Kurx.Application.Abstractions;

/// <summary>D-266 M5 — what an organiser files to prove the institution their event names has authorised
/// it. Document fields are storage <b>keys</b>: the client uploads through a presigned PUT and sends back
/// the key it was handed, so this API never carries file bytes.</summary>
public record EventAuthorizationInput(
    string HeadName, string HeadDesignation, string OfficialEmail, string OfficialPhone,
    string? LetterheadDocumentKey, string? SignatureDocumentKey,
    IReadOnlyList<string>? SupportingDocumentKeys,
    // D-266 M5 representative details. Role is required and comes from a closed vocabulary;
    // RepresentativeRoleOther carries the words when it is `Other`. RepresentativeUserId is a LINK to a
    // Kurx account, never a grant of authority — D-269 owns that and this changes nothing about it.
    string RepresentativeRole = "", string? RepresentativeRoleOther = null,
    Guid? RepresentativeUserId = null);

/// <summary>D-266 M5 — the closed role vocabulary. Exposed so every client renders the same list and a
/// reviewer sees a comparable answer across institutions; `Other` is the escape hatch that keeps a closed
/// list from forcing a wrong choice.</summary>
public static class RepresentativeRoles
{
    public static readonly string[] All =
    [
        "Principal", "Vice Principal", "Dean", "HOD", "Professor", "Faculty Advisor",
        "Event Coordinator", "Placement Officer", "Student Affairs Officer",
        "HR Manager", "Founder", "CEO", "Director", "Secretary", "President",
        "Club Coordinator", "Other",
    ];
}

/// <summary>One event's authorization as a client sees it.
///
/// <para>Documents come back as <b>presigned URLs, not keys</b>. A key is an internal storage address;
/// handing one to a client invites it to construct paths of its own, and a client that constructs paths
/// eventually constructs someone else's. The URLs are short-lived by construction.</para></summary>
public record EventAuthorizationView(
    Guid EventId, string HeadName, string HeadDesignation, string OfficialEmail, string? OfficialPhone,
    string? LetterheadUrl, string? SignatureUrl, IReadOnlyList<string> SupportingDocumentUrls,
    string Status, Guid? ReviewerId, DateTime? ReviewedAt, string? ReasonCode, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt,
    // D-266 M5. ReviewerName and RepresentativeUsername are resolved server-side so neither console has
    // to fetch a user per row; both are null when the account no longer exists — the decision and the
    // filed evidence are still retained.
    string RepresentativeRole = "", string? RepresentativeRoleOther = null,
    Guid? RepresentativeUserId = null, string? RepresentativeUsername = null,
    string? ReviewerName = null);

/// <summary>D-266 M5 — institutional authorization for one event.
///
/// <para><b>Distinct from <see cref="IEventAuthority"/> (D-269).</b> That resolves who may act; this
/// records whether the represented institution consented. The publish gate needs both, and neither is a
/// substitute for the other.</para></summary>
public interface IEventAuthorizationService
{
    /// <summary>Create or replace this event's authorization. A resubmission returns the row to
    /// <c>Submitted</c> and clears the previous decision: evidence that changed after a reviewer read it
    /// has not been reviewed, and leaving the old verdict attached would claim it had.</summary>
    Task<ServiceResult<EventAuthorizationView>> SubmitAsync(Guid userId, Guid eventId, bool isAdmin,
        EventAuthorizationInput input, CancellationToken ct = default);

    /// <summary>The event's authorization, or a null Value when none has been filed. "None filed" is a
    /// legitimate state rather than an error — most events never need one.</summary>
    Task<ServiceResult<EventAuthorizationView?>> GetAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct = default);

    /// <summary>A presigned PUT for one authorization document, keyed under the event.</summary>
    Task<ServiceResult<PresignedUpload>> PresignDocumentAsync(Guid userId, Guid eventId, bool isAdmin,
        string contentType, long maxBytes, CancellationToken ct = default);

    /// <summary>The reviewer's read. Takes no caller: the endpoint's <c>VerificationReviewer</c> policy is
    /// the authorization, exactly as <c>GetReviewHistoryAsync</c> already works — a platform reviewer holds
    /// no standing on the event itself and would 404 through the organiser path.</summary>
    Task<EventAuthorizationView?> GetForReviewAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Reviewer decision. <paramref name="decision"/> is <c>approve</c> | <c>reject</c> |
    /// <c>request_changes</c> — the same vocabulary organization verification already uses, so a reviewer
    /// learns one set of words for the platform rather than one per surface.</summary>
    Task<ServiceResult<EventAuthorizationView>> ReviewAsync(Guid reviewerId, Guid eventId, string decision,
        string? reasonCode, string? notes, CancellationToken ct = default);
}
