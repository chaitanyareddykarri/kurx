using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Public certificate verification (D-344, Phase 5).
///
/// <para><b>An infrastructure failure is never reported as invalidity.</b> Every unexpected exception
/// becomes <see cref="CertificateVerificationOutcome.Unavailable"/>, because this page answers "is this
/// credential real" to someone who may be deciding whether to hire a person — and answering "no" because
/// a database connection dropped is the worst outcome this surface can produce.</para>
///
/// <para><b>What is disclosed is only what the certificate already shows.</b> Anyone can reach this with
/// an id, so the response carries the fields printed on the document and nothing else: no email, no batch,
/// no template internals, and no spreadsheet column that was never placed on the certificate.</para>
/// </summary>
public class CertificateVerificationService(
    KurxDbContext db,
    ICertificateSigner signer,
    IStorage storage,
    ICertificateAnalyticsService analytics,
    ILogger<CertificateVerificationService> log) : ICertificateVerificationService
{
    /// <summary>What a verifier is shown. Everything else in the snapshot stays private — a spreadsheet
    /// may have carried an employee number or a personal email that the design never printed, and an
    /// unauthenticated endpoint must not become a way to read it back.</summary>
    private static readonly string[] PublicKeys =
    [
        "participant_name", "event_name", "event_date", "start_date", "end_date",
        "achievement", "role", "team_name", "organization", "organizer_name", "venue", "issue_date",
    ];

    /// <summary>Deliberately short. The preview lets a verifier compare what they are holding against what
    /// was issued; it is not a distribution channel, and a long-lived link posted anywhere becomes one.</summary>
    private static readonly TimeSpan PreviewTtl = TimeSpan.FromMinutes(10);

    public async Task<CertificateVerificationResult> VerifyAsync(
        string certificateId, CancellationToken ct = default)
    {
        try
        {
            var id = (certificateId ?? "").Trim();
            // Bounded before touching the database: an unbounded string is a free scan on a public,
            // unauthenticated endpoint.
            if (id.Length is < 1 or > 64) return NotFound();

            var certificate = await db.IssuedCertificates.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CertificateId == id, ct);
            if (certificate is null) return NotFound();

            var values = Deserialize(certificate.FieldValuesJson);

            // Rebuilt from the stored row, not from anything the caller sent — that is what makes the
            // signature a check on the record rather than on the request.
            var payload = CertificateCanonicalPayload.Build(
                certificate.CertificateId, certificate.EventId, certificate.TemplateId,
                certificate.TemplateVersion, certificate.IssuedAt, values);

            var compromised = false;
            if (certificate.SignatureKeyId is { Length: > 0 } keyId && certificate.Signature is { Length: > 0 } sig)
            {
                var verdict = await signer.VerifyAsync(keyId, payload, sig, ct);
                compromised = verdict.KeyCompromised;

                // A signature that does not match means the row was altered after issue. The only
                // outcome that says "do not trust this".
                if (verdict.KeyKnown && !verdict.Matches) return Tampered(certificate.CertificateId);

                // The key is gone entirely. We cannot judge, so we do not — reporting an unknown key as
                // tampering would condemn a certificate on the strength of our own key management.
                if (!verdict.KeyKnown)
                {
                    log.LogWarning("Certificate {CertificateId} references unknown signing key {KeyId}",
                        certificate.CertificateId, keyId);
                    return new CertificateVerificationResult(CertificateVerificationOutcome.Unavailable);
                }
            }

            var details = values
                .Where(v => PublicKeys.Contains(v.Key, StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(v.Value))
                .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

            var preview = certificate.PngStorageKey is null
                ? null
                : await storage.PresignGetAsync(certificate.PngStorageKey, PreviewTtl, ct);

            // A revoked or superseded certificate still returns its details. Someone checking one needs
            // to learn that it existed and what happened to it — "not found" would imply it was never
            // real, which is a different and false claim.
            if (certificate.Status == IssuedCertificateStatus.Revoked)
            {
                var revocation = await db.CertificateRevocations.AsNoTracking()
                    .Where(r => r.CertificateId == certificate.Id)
                    .OrderByDescending(r => r.RevokedAt)
                    .FirstOrDefaultAsync(ct);

                string? replacement = null;
                if (revocation?.ReplacementCertificateId is Guid replacementId)
                    replacement = await db.IssuedCertificates.AsNoTracking()
                        .Where(c => c.Id == replacementId).Select(c => c.CertificateId).FirstOrDefaultAsync(ct);

                return new CertificateVerificationResult(
                    CertificateVerificationOutcome.Revoked, certificate.CertificateId, details,
                    certificate.IssuedAt, compromised,
                    revocation?.Reason, revocation?.RevokedAt, replacement, preview);
            }

            if (certificate.Status == IssuedCertificateStatus.Superseded)
            {
                // Which certificate replaced this one — the entire point of superseding rather than
                // revoking. Someone holding the old id is told where the live one is instead of being left
                // with "this is not current" and nowhere to go.
                var replacedBy = await db.IssuedCertificates.AsNoTracking()
                    .Where(c => c.SupersedesCertificateId == certificate.Id)
                    .Select(c => c.CertificateId)
                    .FirstOrDefaultAsync(ct);

                var correction = await db.CertificateRevocations.AsNoTracking()
                    .Where(r => r.CertificateId == certificate.Id)
                    .OrderByDescending(r => r.RevokedAt)
                    .FirstOrDefaultAsync(ct);

                return new CertificateVerificationResult(
                    CertificateVerificationOutcome.Superseded, certificate.CertificateId, details,
                    certificate.IssuedAt, compromised,
                    correction?.Reason, correction?.RevokedAt, replacedBy, preview);
            }

            // Counted only once the certificate resolved to something real. A miss records nothing —
            // otherwise the counter measures guessing rather than use.
            await analytics.RecordAsync(certificate.CertificateId, "Verified", ct);

            return new CertificateVerificationResult(
                CertificateVerificationOutcome.Valid, certificate.CertificateId, details,
                certificate.IssuedAt, compromised, PreviewUrl: preview);
        }
        catch (Exception ex)
        {
            // The load-bearing catch. Anything unexpected — a dropped connection, a storage timeout, a
            // malformed snapshot — is "we could not check", never "this is not valid".
            log.LogError(ex, "Certificate verification failed for {CertificateId}", certificateId);
            return new CertificateVerificationResult(CertificateVerificationOutcome.Unavailable);
        }
    }

    /// <summary>Uniform for a malformed id and an unknown one, so the endpoint cannot be used to learn
    /// which ids exist. Certificate ids are sequential and therefore guessable by design — the mitigation
    /// is a minimal response and rate limiting, not pretending they are secret.</summary>
    private static CertificateVerificationResult NotFound() =>
        new(CertificateVerificationOutcome.NotFound);

    private static CertificateVerificationResult Tampered(string certificateId) =>
        new(CertificateVerificationOutcome.Tampered, certificateId);

    private static Dictionary<string, string> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
