namespace Kurx.Application.Abstractions;

/// <summary>
/// Withdrawing a certificate, and correcting one (D-355, Phase 9).
///
/// <para><b>Nothing is ever edited in place.</b> A certificate is a signed claim that someone already
/// holds a copy of and may already have shown to an employer. Changing its values would leave that copy
/// disagreeing with the platform, and would invalidate a signature made over the old values. So a
/// correction is a NEW certificate with its own id and its own signature, and the original stays exactly
/// as it was issued — only its <i>status</i> moves.</para>
///
/// <para>The two operations are deliberately different claims, and the verification page says which:
/// <b>revoked</b> means this should not be honoured, <b>superseded</b> means a corrected one exists and
/// here it is. Collapsing them would tell someone holding a merely-misspelled certificate that theirs was
/// withdrawn.</para>
/// </summary>
public interface ICertificateRevocationService
{
    /// <summary>Withdraws a certificate outright. Permanent, and requires a stated reason — the platform
    /// will publicly say this certificate should not be honoured, and a public claim with no reason behind
    /// it is not one anyone can act on.</summary>
    Task<ServiceResult<CertificateLineageView>> RevokeAsync(
        Guid userId, Guid certificateRowId, string reason, bool isAdmin, CancellationToken ct = default);

    /// <summary>Withdraws every live certificate in a run. The case this exists for is "all two hundred
    /// have the wrong date", where revoking them one at a time is not a real option.</summary>
    Task<ServiceResult<CertificateBatchRevocationResult>> RevokeBatchAsync(
        Guid userId, Guid batchId, string reason, bool isAdmin, CancellationToken ct = default);

    /// <summary>Issues a corrected replacement. The original becomes <c>Superseded</c> — not revoked — and
    /// the two are linked in both directions so either one can be followed to the other.</summary>
    Task<ServiceResult<CertificateLineageView>> ReissueAsync(
        Guid userId, Guid certificateRowId, CertificateCorrection correction, bool isAdmin,
        CancellationToken ct = default);

    /// <summary>The full chain a certificate belongs to, oldest first. What a support conversation
    /// actually needs: "which of these three is the live one, and why did the others change".</summary>
    Task<ServiceResult<IReadOnlyList<CertificateLineageView>>> LineageAsync(
        Guid userId, Guid certificateRowId, bool isAdmin, CancellationToken ct = default);
}

/// <param name="RecipientName">The corrected spelling, or null to keep the original's.</param>
/// <param name="Values">Field values to override. Anything omitted is carried over from the certificate
/// being replaced, so correcting one misspelled word does not mean restating the whole row.</param>
/// <param name="Reason">Why it was corrected. Recorded against the original.</param>
public sealed record CertificateCorrection(
    string? RecipientName,
    IReadOnlyDictionary<string, string>? Values,
    string Reason);

/// <param name="Supersedes">The certificate this one replaced, if any.</param>
/// <param name="SupersededBy">The certificate that replaced this one, if any.</param>
/// <param name="RevocationReason">Stated when it was revoked or corrected; null while it is live.</param>
public sealed record CertificateLineageView(
    Guid Id,
    string CertificateId,
    string RecipientName,
    string Status,
    DateTime IssuedAt,
    string? Supersedes,
    string? SupersededBy,
    string? RevocationReason,
    DateTime? RevokedAt);

/// <param name="Skipped">Certificates that were already revoked or superseded. Counted rather than
/// treated as failures — re-running a bulk revocation must be safe.</param>
public sealed record CertificateBatchRevocationResult(int Revoked, int Skipped);
