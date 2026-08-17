namespace Kurx.Application.Abstractions;

/// <summary>
/// Public certificate verification (D-355, Phase 5).
///
/// <para><b>No authentication.</b> This page exists so a stranger holding a printed certificate can check
/// it. Requiring an account would defeat the entire purpose.</para>
///
/// <para><b>The outcome states are not collapsible.</b> "This certificate is not valid" and "we could not
/// check right now" are different sentences with different consequences: reporting an outage as invalidity
/// tells someone a genuine credential is forged, on the strength of a timeout. That is the single worst
/// failure this surface can have, and <see cref="CertificateVerificationOutcome.Unavailable"/> exists to
/// prevent it.</para>
/// </summary>
public interface ICertificateVerificationService
{
    /// <param name="certificateId">As printed on the certificate, or as carried by its QR.</param>
    Task<CertificateVerificationResult> VerifyAsync(string certificateId, CancellationToken ct = default);
}

public enum CertificateVerificationOutcome
{
    /// <summary>Issued by the platform, signature intact, not revoked.</summary>
    Valid,

    /// <summary>Genuinely issued, then withdrawn. Still reported with its details, because a verifier
    /// asking about a revoked certificate needs to know it existed and was revoked — not that it was
    /// never real.</summary>
    Revoked,

    /// <summary>Superseded by a corrected reissue. Genuine, but no longer the current document.</summary>
    Superseded,

    /// <summary>No certificate with that id. Uniform for a malformed id and an unknown one, so the page
    /// cannot be used to probe which ids exist.</summary>
    NotFound,

    /// <summary>The record exists but its signature does not match its contents — the row was altered
    /// after issue. The only outcome that means "do not trust this".</summary>
    Tampered,

    /// <summary>Verification could not be completed. NEVER rendered as "not valid".</summary>
    Unavailable,
}

/// <param name="SigningKeyCompromised">The key that signed this was later declared compromised. The
/// certificate remains <see cref="CertificateVerificationOutcome.Valid"/> — it really was issued — and the
/// page shows a clear warning beside it. Calling a genuine certificate invalid because of an operational
/// incident on our side would be the wrong lie.</param>
/// <param name="Details">Only what the certificate itself displays. Deliberately not the recipient's
/// email, the batch, the template, or any spreadsheet column that was never placed on the document.</param>
/// <param name="PreviewUrl">Short-lived presigned URL for the certificate image, so a verifier can compare
/// what they are holding against what was issued.</param>
public sealed record CertificateVerificationResult(
    CertificateVerificationOutcome Outcome,
    string? CertificateId = null,
    IReadOnlyDictionary<string, string>? Details = null,
    DateTime? IssuedAt = null,
    bool SigningKeyCompromised = false,
    string? RevocationReason = null,
    DateTime? RevokedAt = null,
    string? ReplacementCertificateId = null,
    string? PreviewUrl = null);
