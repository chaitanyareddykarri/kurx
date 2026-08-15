using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Withdrawing a certificate, and correcting one (D-344, Phase 9).
///
/// <para><b>Nothing is edited in place.</b> The original row keeps its certificate id, its field values,
/// its signature and its rendered files exactly as issued — only <see cref="IssuedCertificate.Status"/>
/// moves. Editing the values would leave the copy in someone's inbox disagreeing with the platform and
/// would break a signature made over the old values, which is precisely the failure the signature exists
/// to detect.</para>
///
/// <para>A correction is therefore a NEW certificate, with its own id, its own signature and its own
/// rendered files, linked to the original in both directions.</para>
/// </summary>
public class CertificateRevocationService(
    KurxDbContext db,
    IEventAuthority authority,
    ICertificateIssuingService issuing,
    ILogger<CertificateRevocationService> log) : ICertificateRevocationService
{
    /// <summary>Long enough to say what happened, short enough to stay a reason rather than a report.</summary>
    private const int MaxReasonLength = 500;

    // ── Revoke ──────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateLineageView>> RevokeAsync(
        Guid userId, Guid certificateRowId, string reason, bool isAdmin, CancellationToken ct = default)
    {
        var (certificate, error) = await LoadAsync(userId, certificateRowId, isAdmin, ct);
        if (error is not null) return Fail(error);

        var stated = (reason ?? "").Trim();
        // The platform will publicly say this certificate should not be honoured. A public claim with no
        // reason behind it is not one the holder or a verifier can do anything with.
        if (stated.Length is < 1 or > MaxReasonLength) return Fail("invalid_reason");

        if (certificate!.Status != IssuedCertificateStatus.Issued)
            return Fail(certificate.Status == IssuedCertificateStatus.Revoked
                ? "already_revoked"
                : "certificate_superseded");

        certificate.Status = IssuedCertificateStatus.Revoked;
        db.CertificateRevocations.Add(new CertificateRevocation
        {
            CertificateId = certificate.Id,
            Reason = stated,
            RevokedByUserId = userId,
            // Null: revoked outright, with nothing issued in its place. That is what distinguishes this
            // from a correction, and it is what the verification page reads to know which to say.
            ReplacementCertificateId = null,
        });
        await db.SaveChangesAsync(ct);

        log.LogInformation("Certificate {CertificateId} revoked by {UserId}", certificate.CertificateId, userId);
        return await ProjectAsync(certificate, ct);
    }

    public async Task<ServiceResult<CertificateBatchRevocationResult>> RevokeBatchAsync(
        Guid userId, Guid batchId, string reason, bool isAdmin, CancellationToken ct = default)
    {
        var batch = await db.CertificateBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return ServiceResult<CertificateBatchRevocationResult>.Fail("not_found");

        var access = await authority.ResolveAsync(userId, batch.EventId, isAdmin, ct);
        if (!access.EventExists || !access.Can(EventPermission.ManageContent))
            return ServiceResult<CertificateBatchRevocationResult>.Fail("not_found");

        var stated = (reason ?? "").Trim();
        if (stated.Length is < 1 or > MaxReasonLength)
            return ServiceResult<CertificateBatchRevocationResult>.Fail("invalid_reason");

        var certificates = await db.IssuedCertificates
            .Where(c => c.BatchId == batchId)
            .ToListAsync(ct);

        var revokedAt = DateTime.UtcNow;
        int revoked = 0, skipped = 0;

        foreach (var certificate in certificates)
        {
            // Already revoked or already superseded. Counted rather than failed, so re-running a bulk
            // revocation after a partial one is safe.
            if (certificate.Status != IssuedCertificateStatus.Issued) { skipped++; continue; }

            certificate.Status = IssuedCertificateStatus.Revoked;
            db.CertificateRevocations.Add(new CertificateRevocation
            {
                CertificateId = certificate.Id,
                Reason = stated,
                RevokedByUserId = userId,
                RevokedAt = revokedAt,
            });
            revoked++;
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Certificate batch {BatchId}: {Revoked} revoked, {Skipped} already not live",
            batchId, revoked, skipped);

        return ServiceResult<CertificateBatchRevocationResult>.Success(
            new CertificateBatchRevocationResult(revoked, skipped));
    }

    // ── Reissue ─────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateLineageView>> ReissueAsync(
        Guid userId, Guid certificateRowId, CertificateCorrection correction, bool isAdmin,
        CancellationToken ct = default)
    {
        var (original, error) = await LoadAsync(userId, certificateRowId, isAdmin, ct);
        if (error is not null) return Fail(error);

        var stated = (correction?.Reason ?? "").Trim();
        if (stated.Length is < 1 or > MaxReasonLength) return Fail("invalid_reason");

        // Only a live certificate can be corrected. A superseded one has already been replaced — correct
        // its replacement instead, or the chain forks and "which one is live" stops having one answer.
        if (original!.Status != IssuedCertificateStatus.Issued)
            return Fail(original.Status == IssuedCertificateStatus.Revoked
                ? "certificate_revoked"
                : "certificate_superseded");

        var recipient = await db.CertificateRecipients.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == original.RecipientId, ct);
        if (recipient is null) return Fail("recipient_not_found");

        var prepared = await issuing.PrepareAsync(original.TemplateId, ct);
        if (!prepared.Ok) return Fail(prepared.Error!);

        // Carried over from the certificate being replaced, then overridden. Correcting one misspelled
        // word must not mean restating the whole row — and anything not restated has to come out
        // identical, or a correction quietly changes fields nobody touched.
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in ReadValues(original)) values[key] = value;
        if (correction!.Values is not null)
            foreach (var (key, value) in correction.Values) values[key] = value;

        var name = string.IsNullOrWhiteSpace(correction.RecipientName)
            ? recipient.FullName
            : correction.RecipientName.Trim();
        values["participant_name"] = name;

        // The replacement is a real certificate: its own id, its own signature over its own values, its
        // own rendered files. Not a copy of the original with a field swapped.
        var reissued = await issuing.IssuePreparedAsync(
            prepared.Value!, original.RecipientId, batchId: null, name, values, ct);
        if (!reissued.Ok) return Fail(reissued.Error!);

        var replacement = await db.IssuedCertificates
            .FirstAsync(c => c.CertificateId == reissued.Value!.CertificateId, ct);

        // Linked in both directions: the replacement records what it replaced, and the original's
        // revocation row records what replaced it. Either end can be followed to the other, which is what
        // the verification page needs whichever id someone happens to hold.
        replacement.SupersedesCertificateId = original.Id;

        var tracked = await db.IssuedCertificates.FirstAsync(c => c.Id == original.Id, ct);
        tracked.Status = IssuedCertificateStatus.Superseded;

        db.CertificateRevocations.Add(new CertificateRevocation
        {
            CertificateId = original.Id,
            Reason = stated,
            RevokedByUserId = userId,
            ReplacementCertificateId = replacement.Id,
        });

        await db.SaveChangesAsync(ct);

        log.LogInformation("Certificate {Original} superseded by {Replacement}",
            original.CertificateId, replacement.CertificateId);

        return await ProjectAsync(replacement, ct);
    }

    // ── Lineage ─────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<CertificateLineageView>>> LineageAsync(
        Guid userId, Guid certificateRowId, bool isAdmin, CancellationToken ct = default)
    {
        var (certificate, error) = await LoadAsync(userId, certificateRowId, isAdmin, ct);
        if (error is not null)
            return ServiceResult<IReadOnlyList<CertificateLineageView>>.Fail(error);

        // Walk back to the original, then forward through every replacement. Bounded rather than looping
        // until it runs out: a cycle should be impossible, and an impossible thing that hangs a request is
        // worse than one that stops.
        const int MaxChain = 50;

        var root = certificate!;
        for (var i = 0; i < MaxChain && root.SupersedesCertificateId is Guid previous; i++)
        {
            var earlier = await db.IssuedCertificates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == previous, ct);
            if (earlier is null) break;
            root = earlier;
        }

        var chain = new List<IssuedCertificate> { root };
        for (var i = 0; i < MaxChain; i++)
        {
            var current = chain[^1].Id;
            var next = await db.IssuedCertificates.AsNoTracking()
                .FirstOrDefaultAsync(c => c.SupersedesCertificateId == current, ct);
            if (next is null) break;
            chain.Add(next);
        }

        var views = new List<CertificateLineageView>(chain.Count);
        foreach (var link in chain) views.Add((await ProjectAsync(link, ct)).Value!);

        return ServiceResult<IReadOnlyList<CertificateLineageView>>.Success(views);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static ServiceResult<CertificateLineageView> Fail(string error) =>
        ServiceResult<CertificateLineageView>.Fail(error);

    private async Task<(IssuedCertificate? Certificate, string? Error)> LoadAsync(
        Guid userId, Guid certificateRowId, bool isAdmin, CancellationToken ct)
    {
        var certificate = await db.IssuedCertificates.FirstOrDefaultAsync(c => c.Id == certificateRowId, ct);
        if (certificate is null) return (null, "not_found");

        var access = await authority.ResolveAsync(userId, certificate.EventId, isAdmin, ct);
        // D-018: a certificate on an event the caller may not see is indistinguishable from one that does
        // not exist. Revocation is a destructive act, so this is the check that matters most here.
        if (!access.EventExists || !access.Can(EventPermission.ManageContent)) return (null, "not_found");

        return (certificate, null);
    }

    /// <summary>The values the certificate was rendered with. A malformed snapshot yields an empty set
    /// rather than throwing — a correction should still be possible on a row whose json went bad.</summary>
    private static IReadOnlyDictionary<string, string> ReadValues(IssuedCertificate certificate)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(certificate.FieldValuesJson)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private async Task<ServiceResult<CertificateLineageView>> ProjectAsync(
        IssuedCertificate certificate, CancellationToken ct)
    {
        string? supersedes = null;
        if (certificate.SupersedesCertificateId is Guid previous)
            supersedes = await db.IssuedCertificates.AsNoTracking()
                .Where(c => c.Id == previous).Select(c => c.CertificateId).FirstOrDefaultAsync(ct);

        var supersededBy = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.SupersedesCertificateId == certificate.Id)
            .Select(c => c.CertificateId)
            .FirstOrDefaultAsync(ct);

        var revocation = await db.CertificateRevocations.AsNoTracking()
            .Where(r => r.CertificateId == certificate.Id)
            .OrderByDescending(r => r.RevokedAt)
            .FirstOrDefaultAsync(ct);

        var name = ReadValues(certificate).TryGetValue("participant_name", out var value)
            ? value
            : await db.CertificateRecipients.AsNoTracking()
                .Where(r => r.Id == certificate.RecipientId).Select(r => r.FullName).FirstOrDefaultAsync(ct)
              ?? "";

        return ServiceResult<CertificateLineageView>.Success(new CertificateLineageView(
            certificate.Id,
            certificate.CertificateId,
            name,
            certificate.Status.ToString().ToLowerInvariant(),
            certificate.IssuedAt,
            supersedes,
            supersededBy,
            revocation?.Reason,
            revocation?.RevokedAt));
    }
}
