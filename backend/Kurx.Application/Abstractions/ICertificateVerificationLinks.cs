namespace Kurx.Application.Abstractions;

/// <summary>
/// Where a certificate's QR points (D-355, Phase 5).
///
/// <para>The public verification origin is configuration, not a constant: a self-hosted deployment, a
/// staging environment and production all publish on different hosts, and a hardcoded URL would print the
/// wrong address permanently onto every certificate — the one part of a certificate that cannot be fixed
/// after it is issued.</para>
///
/// <para>Its own boundary rather than an inline config read, so exactly one place decides the shape of a
/// verification URL and the QR, the emailed link and the shareable link cannot drift apart.</para>
/// </summary>
public interface ICertificateVerificationLinks
{
    /// <summary>The page a verifier lands on for this certificate.</summary>
    string VerificationUrl(string certificateId);

    /// <summary>The page a participant with no account lands on to reach their own certificates
    /// (D-355, Phase 10). Same origin as verification, and built here for the same reason — the emailed
    /// link and the one an organiser copies out of the dashboard must be the same link.</summary>
    string AccessUrl(string token);
}
