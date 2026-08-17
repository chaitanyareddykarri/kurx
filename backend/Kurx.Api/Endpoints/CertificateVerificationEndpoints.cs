using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>
/// Public certificate verification (D-355, Phase 5).
///
/// <para><b>Deliberately anonymous.</b> This is what a QR resolves to and what a shared link opens. Anyone
/// holding a printed certificate must be able to check it without an account — requiring one would defeat
/// the purpose of a verifiable credential.</para>
///
/// <para>Rate limited per IP under its own policy rather than the global anonymous cap: certificate ids
/// are sequential and therefore enumerable by design, so the mitigation is a low per-IP ceiling and a
/// minimal response body, not pretending the ids are secret.</para>
/// </summary>
public static class CertificateVerificationEndpoints
{
    public static void MapCertificateVerificationEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/verify/{certificateId}", async (
            string certificateId, ICertificateVerificationService svc, CancellationToken ct) =>
        {
            var result = await svc.VerifyAsync(certificateId, ct);

            return result.Outcome switch
            {
                // 404 for an unknown id, so the shape of the response matches its meaning.
                CertificateVerificationOutcome.NotFound => Results.NotFound(result),

                // 503, NOT 200-with-invalid and NOT 404. A verifier must be able to tell "we could not
                // check" from "this is not real" — and a caching layer must not store the former.
                CertificateVerificationOutcome.Unavailable => Results.Json(result, statusCode: 503),

                // Everything else is a real answer about a real record, including revoked, superseded and
                // tampered. The body carries which.
                _ => Results.Ok(result),
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting("verify")
        .WithTags("certificate-verification")
        .WithSummary("Verify a certificate by its public id")
        .Produces<CertificateVerificationResult>();
    }
}
