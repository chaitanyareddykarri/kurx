using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class MediaService(KurxDbContext db, IEventAuthority authority, IStorage storage,
    Providers.UploadScanGate scanGate) : IMediaService
{
    public async Task<ServiceResult<PresignedUpload>> PresignUploadAsync(Guid userId, Guid eventId, bool isAdmin,
        string contentType, long maxBytes, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<PresignedUpload>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<PresignedUpload>.Fail("forbidden");

        var key = $"events/{eventId}/media/{Guid.NewGuid():N}";
        var upload = await storage.PresignPutAsync(key, contentType, maxBytes, ct);
        return ServiceResult<PresignedUpload>.Success(upload);
    }

    public async Task<ServiceResult<PresignedUpload>> PresignOrgDocAsync(Guid userId, Guid orgId, bool isAdmin,
        string contentType, long maxBytes, CancellationToken ct = default)
    {
        // Hide existence from non-members (404-not-403, D-018) — matching org verification submit, the
        // sibling that consumes these keys. A member who lacks Owner/Manager still gets a 403.
        var org = await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct);
        if (!org.IsMember && !isAdmin) return ServiceResult<PresignedUpload>.Fail("not_found");
        if (!org.CanManage) return ServiceResult<PresignedUpload>.Fail("forbidden");

        var key = $"orgs/{orgId}/verification/{Guid.NewGuid():N}";
        var upload = await storage.PresignPutAsync(key, contentType, maxBytes, ct);
        return ServiceResult<PresignedUpload>.Success(upload);
    }

    public async Task<ServiceResult<PresignedUpload>> PresignClaimDocAsync(Guid userId, Guid orgId,
        string contentType, long maxBytes, CancellationToken ct = default)
    {
        // Any authenticated user may upload proof for their OWN membership claim to an existing org, so
        // there's no role check here (a claimant isn't a member yet) — only that the org exists. Keyed
        // per-user so one claimant's evidence can't collide with another's.
        var exists = await db.Organizations.AsNoTracking().AnyAsync(o => o.Id == orgId && o.DeletedAt == null, ct);
        if (!exists) return ServiceResult<PresignedUpload>.Fail("not_found");

        var key = $"orgs/{orgId}/claims/{userId}/{Guid.NewGuid():N}";
        var upload = await storage.PresignPutAsync(key, contentType, maxBytes, ct);
        return ServiceResult<PresignedUpload>.Success(upload);
    }

    public async Task<ServiceResult<PresignedUpload>> PresignRepresentationDocAsync(Guid userId,
        string contentType, long maxBytes, CancellationToken ct = default)
    {
        // Evidence for a NOT-yet-existing institution (event-first, D-076): no org to key against and no role
        // to check — any authenticated user may file a representation request. Keyed per-user; the key is saved
        // onto the placeholder org's verification_documents when SubmitRepresentationRequestAsync runs.
        var key = $"representation-requests/{userId}/{Guid.NewGuid():N}";
        var upload = await storage.PresignPutAsync(key, contentType, maxBytes, ct);
        return ServiceResult<PresignedUpload>.Success(upload);
    }

    /// <summary>The two profile-image slots. A closed set rather than free text: the slot becomes part of
    /// the storage key, so an unchecked value would let a caller write outside its own prefix.</summary>
    private static readonly string[] ProfileImageSlots = ["avatar", "cover"];

    /// <summary>Image types accepted for a profile picture. An allowlist, not a blocklist — this key is
    /// rendered directly in the clients, so anything not a real raster image is rejected at the boundary.</summary>
    private static readonly string[] ProfileImageContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif", "image/avif"];

    public async Task<ServiceResult<PresignedUpload>> PresignProfileImageAsync(Guid userId, string slot,
        string contentType, long maxBytes, CancellationToken ct = default)
    {
        if (!ProfileImageSlots.Contains(slot, StringComparer.OrdinalIgnoreCase))
            return ServiceResult<PresignedUpload>.Fail("invalid_slot");
        if (!ProfileImageContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return ServiceResult<PresignedUpload>.Fail("invalid_content_type");

        // No role check by design — a user may always replace their own picture. Keyed per-user per-slot so
        // one caller can never presign into another's prefix; the key is persisted via PATCH /v1/me/profile.
        var key = $"users/{userId}/{slot.ToLowerInvariant()}/{Guid.NewGuid():N}";
        var upload = await storage.PresignPutAsync(key, contentType, maxBytes, ct);
        return ServiceResult<PresignedUpload>.Success(upload);
    }

    public async Task<ServiceResult<bool>> AttachAsync(Guid userId, Guid eventId, bool isAdmin, string kind, string key, string? caption, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");
        if (!Enum.TryParse<MediaKind>(kind, true, out var mediaKind)) return ServiceResult<bool>.Fail("invalid_kind");
        if (string.IsNullOrWhiteSpace(key)) return ServiceResult<bool>.Fail("invalid_key");

        // D-338 — attach is where the server first claims these bytes, so it is where they are scanned.
        // Presign cannot do it: nothing has been uploaded yet at that point.
        if (await scanGate.RejectAsync(key, userId, "event_media", eventId, ct) is { } scanError)
            return ServiceResult<bool>.Fail(scanError);

        var sort = await db.EventMedia.Where(m => m.EventId == eventId && m.Kind == mediaKind).CountAsync(ct);
        db.EventMedia.Add(new EventMedia { EventId = eventId, Kind = mediaKind, Key = key, Caption = caption ?? "", Sort = sort });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> RemoveAsync(Guid userId, Guid eventId, bool isAdmin, Guid mediaId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");

        var media = await db.EventMedia.FirstOrDefaultAsync(m => m.Id == mediaId && m.EventId == eventId, ct);
        if (media is null) return ServiceResult<bool>.Fail("not_found");
        db.EventMedia.Remove(media);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }
}
