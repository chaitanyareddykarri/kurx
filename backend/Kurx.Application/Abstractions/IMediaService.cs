namespace Kurx.Application.Abstractions;

/// <summary>Event media (gallery/documents/poster/brochure/rules-PDF). The primary banner is a plain
/// Event.BannerKey field set via UpdateEventInput; everything else attaches here. Storage-provider-agnostic
/// via IStorage (D-011-style abstraction) so the backing provider can be swapped later.</summary>
public interface IMediaService
{
    /// <summary>Returns a presigned upload URL/key for the caller to PUT the file to directly.</summary>
    Task<ServiceResult<PresignedUpload>> PresignUploadAsync(Guid userId, Guid eventId, bool isAdmin,
        string contentType, long maxBytes, CancellationToken ct = default);

    /// <summary>Presigns an upload for an <b>org-level</b> credential (e.g. a verification letterhead, D-055).
    /// Keyed under <c>orgs/{orgId}/verification/*</c> so the credential is reused across the org's events and
    /// outlives any single event — unlike event media. Owner/Manager (or admin) of the org only.</summary>
    Task<ServiceResult<PresignedUpload>> PresignOrgDocAsync(Guid userId, Guid orgId, bool isAdmin,
        string contentType, long maxBytes, CancellationToken ct = default);

    /// <summary>Presigns an upload for <b>membership-claim</b> evidence (D-055 G4). Any authenticated user
    /// may upload proof for their own claim to an existing org (a claimant isn't a member yet, so there's no
    /// role check — only that the org exists). Keyed under <c>orgs/{orgId}/claims/{userId}/*</c>.</summary>
    Task<ServiceResult<PresignedUpload>> PresignClaimDocAsync(Guid userId, Guid orgId,
        string contentType, long maxBytes, CancellationToken ct = default);

    /// <summary>Presigns an upload for <b>representation-request</b> evidence (D-076). The institution does not
    /// exist yet (event-first, D-074), so this is user-scoped with no org — keyed under
    /// <c>representation-requests/{userId}/*</c>. The submit endpoint later records these keys against the
    /// placeholder org's <c>verification_documents</c>.</summary>
    Task<ServiceResult<PresignedUpload>> PresignRepresentationDocAsync(Guid userId,
        string contentType, long maxBytes, CancellationToken ct = default);

    /// <summary>Presigns an upload for the caller's own <b>profile image</b> (D-219) — avatar or cover.
    /// User-scoped with no org and no role check (you may always replace your own picture), keyed under
    /// <c>users/{userId}/{slot}/*</c>. <paramref name="slot"/> is "avatar" or "cover"; anything else is
    /// rejected. Unlike the document presigns, the content type is restricted to real image types — this
    /// key is rendered in an <c>img</c> tag on a public page, so accepting arbitrary types here would be
    /// a stored-content hazard rather than merely a bad upload. The resulting key is saved by the caller
    /// through <c>PATCH /v1/me/profile</c>, mirroring how event media confirms a key after upload.</summary>
    Task<ServiceResult<PresignedUpload>> PresignProfileImageAsync(Guid userId, string slot,
        string contentType, long maxBytes, CancellationToken ct = default);

    /// <summary>Attaches an already-uploaded object (by key) to the event as a media item.</summary>
    Task<ServiceResult<bool>> AttachAsync(Guid userId, Guid eventId, bool isAdmin, string kind, string key, string? caption, CancellationToken ct = default);

    Task<ServiceResult<bool>> RemoveAsync(Guid userId, Guid eventId, bool isAdmin, Guid mediaId, CancellationToken ct = default);
}
