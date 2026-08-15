using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Builds verification URLs from the configured public origin (D-355, Phase 5).
///
/// <para><c>CERTIFICATE_VERIFICATION_BASE_URL</c> is the browser-facing web origin, which is deliberately
/// NOT <c>KURX_API_BASE</c>: that is the API's address, used for presigned storage URLs, and a QR pointing
/// at an API host would take a verifier to JSON.</para>
/// </summary>
public class CertificateVerificationLinks(IConfiguration config) : ICertificateVerificationLinks
{
    private readonly string _base =
        (config["CERTIFICATE_VERIFICATION_BASE_URL"] ?? "http://localhost:3000").TrimEnd('/');

    public string VerificationUrl(string certificateId) =>
        $"{_base}/verify/{Uri.EscapeDataString(certificateId)}";

    public string AccessUrl(string token) =>
        $"{_base}/certificates/{Uri.EscapeDataString(token)}";
}
