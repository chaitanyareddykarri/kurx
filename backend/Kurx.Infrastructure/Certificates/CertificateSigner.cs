using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// ES256 certificate signing (D-344, Phase 5).
///
/// <para><b>Reuses the platform's key protection, not its key lifecycle.</b> The private half is wrapped
/// by the existing <see cref="ISigningKeyProtector"/> — so on a production deployment it is sealed by KMS
/// and a stolen database dump is inert — but the keys live in the certificate module's own table, because
/// JWT keys are retired and discarded within minutes while a certificate must verify for years.</para>
///
/// <para><b>The active key is created on first use</b> rather than by a separate provisioning step. A
/// platform that cannot sign until someone remembers to run a command is a platform that issues its first
/// unsigned certificates in production.</para>
/// </summary>
public class CertificateSigner(KurxDbContext db, ISigningKeyProtector protector) : ICertificateSigner
{
    private const string Algorithm = "ES256";

    public async Task<CertificateSignature> SignAsync(string canonicalPayload, CancellationToken ct = default)
    {
        var key = await GetOrCreateActiveKeyAsync(ct);

        var pkcs8 = Convert.FromBase64String(
            await protector.UnprotectAsync(key.ProtectedPrivateKey!, ct));

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
        CryptographicOperations.ZeroMemory(pkcs8);

        // IEEE P1363 fixed-width, which is what ES256 means. The .NET default for SignData is DER, and
        // mixing the two would verify locally and fail against anything that expects the JOSE convention.
        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(canonicalPayload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return new CertificateSignature(key.KeyId, Convert.ToBase64String(signature));
    }

    public async Task<CertificateSignatureVerdict> VerifyAsync(
        string keyId, string canonicalPayload, string signature, CancellationToken ct = default)
    {
        var key = await db.CertificateSigningKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyId == keyId, ct);

        // No such key. NOT a mismatch: we cannot judge this certificate, and reporting "does not match"
        // would call a possibly-genuine document a forgery.
        if (key is null) return new CertificateSignatureVerdict(false, KeyKnown: false, KeyCompromised: false);

        var compromised = key.State == CertificateSigningKeyState.Compromised;

        bool matches;
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key.PublicKeySpki), out _);
            matches = ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(canonicalPayload),
                Convert.FromBase64String(signature),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception)
        {
            // Malformed base64 or an unparseable key. A verification that cannot be performed is not a
            // failed verification — the caller distinguishes the two, and reports neither as "invalid".
            return new CertificateSignatureVerdict(false, KeyKnown: true, KeyCompromised: compromised);
        }

        return new CertificateSignatureVerdict(matches, KeyKnown: true, KeyCompromised: compromised);
    }

    /// <summary>The current signing key, generating one if the platform has none.
    ///
    /// <para>Concurrency: two simultaneous first-issues could both generate a key. That is harmless —
    /// both are valid, both are retained, and each certificate records which one signed it. Serialising
    /// key creation to avoid a spare row would be a lock on the issuance path for no benefit.</para></summary>
    private async Task<CertificateSigningKey> GetOrCreateActiveKeyAsync(CancellationToken ct)
    {
        var existing = await db.CertificateSigningKeys
            .Where(k => k.State == CertificateSigningKeyState.Active && k.ProtectedPrivateKey != null)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pkcs8 = ecdsa.ExportPkcs8PrivateKey();

        var key = new CertificateSigningKey
        {
            // Time-ordered and random: the id appears on every certificate this key signs, so it must be
            // stable and unguessable, and ordering makes a key list read chronologically.
            KeyId = $"cert-{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}",
            Algorithm = Algorithm,
            PublicKeySpki = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
            ProtectedPrivateKey = await protector.ProtectAsync(Convert.ToBase64String(pkcs8), ct),
            ProtectionScheme = protector.SchemeId,
            State = CertificateSigningKeyState.Active,
        };
        CryptographicOperations.ZeroMemory(pkcs8);

        db.CertificateSigningKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return key;
    }
}
