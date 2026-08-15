using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// How the person named on a certificate reaches it (D-344, Phase 10).
///
/// <para><b>The token is the credential, and it is never stored.</b> Only a SHA-256 hash is kept, so a
/// dump of this table cannot be turned back into working links — the same reason a password is not stored
/// either. The raw value exists in exactly one response, and afterwards the platform cannot reproduce it.</para>
///
/// <para><b>Linking is by verified email only.</b> Matching on an unverified address would let anyone type
/// a stranger's email into their profile and collect that stranger's certificates.</para>
/// </summary>
public class CertificateParticipantService(
    KurxDbContext db,
    IEventAuthority authority,
    IStorage storage,
    ICertificateVerificationLinks links,
    ICertificateAnalyticsService analytics,
    ILogger<CertificateParticipantService> log) : ICertificateParticipantService
{
    /// <summary>256 bits from a cryptographic RNG. This is the entire authorisation for the link, so it
    /// has to be unguessable in the same sense a session token is.</summary>
    private const int TokenBytes = 32;

    /// <summary>How long a download link lives. Minutes: it is handed to the browser that just asked, and
    /// a URL that outlives the page it was rendered on is a credential nobody is tracking.</summary>
    private static readonly TimeSpan DownloadTtl = TimeSpan.FromMinutes(15);

    // ── Capability links ────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateAccessLinkView>> CreateAccessLinkAsync(
        Guid userId, Guid recipientId, bool isAdmin, CancellationToken ct = default)
    {
        var (recipient, error) = await LoadRecipientAsync(userId, recipientId, isAdmin, ct);
        if (error is not null) return ServiceResult<CertificateAccessLinkView>.Fail(error);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var link = new CertificateAccessLink
        {
            RecipientId = recipient!.Id,
            TokenHash = Hash(token),
            CreatedByUserId = userId,
        };
        db.CertificateAccessLinks.Add(link);
        await db.SaveChangesAsync(ct);

        // The one and only time the raw token leaves this method. Deliberately not logged.
        return ServiceResult<CertificateAccessLinkView>.Success(new CertificateAccessLinkView(
            link.Id, link.RecipientId, links.AccessUrl(token), false, link.CreatedAt, null, null));
    }

    public async Task<ServiceResult<bool>> RevokeAccessLinkAsync(
        Guid userId, Guid linkId, bool isAdmin, CancellationToken ct = default)
    {
        var link = await db.CertificateAccessLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);
        if (link is null) return ServiceResult<bool>.Fail("not_found");

        var (_, error) = await LoadRecipientAsync(userId, link.RecipientId, isAdmin, ct);
        if (error is not null) return ServiceResult<bool>.Fail(error);

        if (link.RevokedAt is not null) return ServiceResult<bool>.Success(true);

        link.RevokedAt = DateTime.UtcNow;
        link.RevokedByUserId = userId;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Certificate access link {LinkId} revoked by {UserId}", link.Id, userId);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<CertificateAccessLinkView>>> ListAccessLinksAsync(
        Guid userId, Guid recipientId, bool isAdmin, CancellationToken ct = default)
    {
        var (_, error) = await LoadRecipientAsync(userId, recipientId, isAdmin, ct);
        if (error is not null)
            return ServiceResult<IReadOnlyList<CertificateAccessLinkView>>.Fail(error);

        var rows = await db.CertificateAccessLinks.AsNoTracking()
            .Where(l => l.RecipientId == recipientId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        // Url is null on every one of these. A link that has been minted cannot be shown again, only
        // replaced — which is what "the token is never stored" actually means in practice.
        return ServiceResult<IReadOnlyList<CertificateAccessLinkView>>.Success(rows
            .Select(l => new CertificateAccessLinkView(
                l.Id, l.RecipientId, null, l.RevokedAt is not null, l.CreatedAt, l.LastAccessedAt, l.RevokedAt))
            .ToList());
    }

    public async Task<ServiceResult<ParticipantCertificatesView>> ResolveAccessAsync(
        string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ServiceResult<ParticipantCertificatesView>.Fail("not_found");

        var link = await db.CertificateAccessLinks
            .FirstOrDefaultAsync(l => l.TokenHash == Hash(token), ct);

        // A revoked link and a token that never existed are the same answer. Distinguishing them would
        // confirm to whoever is guessing that they had found a real one.
        if (link is null || link.RevokedAt is not null)
            return ServiceResult<ParticipantCertificatesView>.Fail("not_found");

        var recipient = await db.CertificateRecipients.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == link.RecipientId, ct);
        if (recipient is null) return ServiceResult<ParticipantCertificatesView>.Fail("not_found");

        link.LastAccessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var view = await ProjectAsync(recipient.FullName, [recipient.Id], ct);

        // The holder opened their certificates. Recorded per certificate, with nothing about who they are
        // — an organiser needs to know theirs are being collected, not to watch a particular person.
        foreach (var certificate in view.Certificates)
            await analytics.RecordAsync(certificate.CertificateId, "Viewed", ct);

        return ServiceResult<ParticipantCertificatesView>.Success(view);
    }

    // ── Account holders ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ParticipantCertificatesView>> ListMineAsync(
        Guid userId, CancellationToken ct = default)
    {
        // Claim anything waiting on this account's verified address first, so a certificate issued before
        // they signed up appears the moment they look rather than after some later backfill.
        await LinkByVerifiedEmailAsync(userId, ct);

        var recipients = await db.CertificateRecipients.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => new { r.Id, r.FullName })
            .ToListAsync(ct);

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);

        return ServiceResult<ParticipantCertificatesView>.Success(await ProjectAsync(
            recipients.FirstOrDefault()?.FullName ?? user ?? "",
            recipients.Select(r => r.Id).ToList(), ct));
    }

    public async Task<int> LinkByVerifiedEmailAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.EmailVerifiedAt })
            .FirstOrDefaultAsync(ct);

        // The whole rule. An unverified address is a claim, not a proof, and acting on it would let
        // anyone collect a stranger's certificates by typing their email into a profile field.
        if (user?.Email is null || user.EmailVerifiedAt is null) return 0;

        var normalized = user.Email.Trim().ToLowerInvariant();
        if (normalized.Length == 0) return 0;

        var unclaimed = await db.CertificateRecipients
            .Where(r => r.NormalizedEmail == normalized && r.UserId == null)
            .ToListAsync(ct);

        if (unclaimed.Count == 0) return 0;

        var linkedAt = DateTime.UtcNow;
        foreach (var recipient in unclaimed)
        {
            recipient.UserId = userId;
            // Kept separate from UserId so "linked at signup" and "linked by a later backfill" stay
            // distinguishable.
            recipient.LinkedAt = linkedAt;
        }
        await db.SaveChangesAsync(ct);

        log.LogInformation("Linked {Count} certificate recipient(s) to user {UserId} by verified email",
            unclaimed.Count, userId);
        return unclaimed.Count;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private async Task<(CertificateRecipient? Recipient, string? Error)> LoadRecipientAsync(
        Guid userId, Guid recipientId, bool isAdmin, CancellationToken ct)
    {
        var recipient = await db.CertificateRecipients.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == recipientId, ct);
        if (recipient is null) return (null, "not_found");

        var access = await authority.ResolveAsync(userId, recipient.EventId, isAdmin, ct);
        // D-018: a recipient on an event the caller may not see is indistinguishable from one that does
        // not exist.
        if (!access.EventExists || !access.Can(EventPermission.ManageContent)) return (null, "not_found");

        return (recipient, null);
    }

    /// <summary>Everything these recipient rows were issued, newest first.</summary>
    private async Task<ParticipantCertificatesView> ProjectAsync(
        string recipientName, IReadOnlyList<Guid> recipientIds, CancellationToken ct)
    {
        if (recipientIds.Count == 0) return new ParticipantCertificatesView(recipientName, []);

        var certificates = await db.IssuedCertificates.AsNoTracking()
            .Where(c => recipientIds.Contains(c.RecipientId))
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync(ct);

        var eventIds = certificates.Select(c => c.EventId).Distinct().ToList();
        var titles = await db.Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Title, ct);

        var views = new List<ParticipantCertificateView>(certificates.Count);
        foreach (var certificate in certificates)
        {
            var live = certificate.Status == IssuedCertificateStatus.Issued;

            string? pdf = null, png = null;
            // A withdrawn or replaced certificate is still listed — hiding it would leave the holder
            // unable to find out what happened to it — but no fresh download is minted for one the
            // platform publicly calls invalid.
            if (live)
            {
                pdf = await PresignAsync(certificate.PdfStorageKey, ct);
                png = await PresignAsync(certificate.PngStorageKey, ct);
            }

            string? replacedBy = null;
            if (certificate.Status == IssuedCertificateStatus.Superseded)
                replacedBy = await db.IssuedCertificates.AsNoTracking()
                    .Where(c => c.SupersedesCertificateId == certificate.Id)
                    .Select(c => c.CertificateId)
                    .FirstOrDefaultAsync(ct);

            string? reason = null;
            if (!live)
                reason = await db.CertificateRevocations.AsNoTracking()
                    .Where(r => r.CertificateId == certificate.Id)
                    .OrderByDescending(r => r.RevokedAt)
                    .Select(r => r.Reason)
                    .FirstOrDefaultAsync(ct);

            views.Add(new ParticipantCertificateView(
                certificate.CertificateId,
                titles.TryGetValue(certificate.EventId, out var title) ? title : "",
                certificate.Status.ToString().ToLowerInvariant(),
                certificate.IssuedAt,
                links.VerificationUrl(certificate.CertificateId),
                pdf, png, replacedBy, reason));
        }

        return new ParticipantCertificatesView(recipientName, views);
    }

    private async Task<string?> PresignAsync(string? key, CancellationToken ct)
    {
        if (key is null) return null;
        try
        {
            return await storage.ExistsAsync(key, ct)
                ? await storage.PresignGetAsync(key, DownloadTtl, ct)
                : null;
        }
        catch (Exception ex)
        {
            // A download that cannot be linked is a missing button, not a failed page. The certificate is
            // still shown, and still verifiable.
            log.LogWarning(ex, "Could not presign certificate file {Key}", key);
            return null;
        }
    }
}
