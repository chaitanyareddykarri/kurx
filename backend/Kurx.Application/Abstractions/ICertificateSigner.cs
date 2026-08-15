namespace Kurx.Application.Abstractions;

/// <summary>
/// Signs and verifies certificates (D-355, Phase 5).
///
/// <para><b>What the signature is actually for.</b> Verification is online, against our own database, so
/// the signature does not prove the certificate exists — the row does. What it proves is that the row has
/// not been <i>altered</i>: a directly-edited <c>issued_certificates</c> record fails verification instead
/// of verifying as genuine. That is a real and worthwhile guarantee, and it is a narrower one than
/// "offline-verifiable", which this deliberately is not.</para>
///
/// <para><b>Three properties, all covered by one signature.</b> That the platform issued it (only the
/// platform holds the private key); that the payload is untouched (the signature covers every field the
/// verification page displays); and that it corresponds to THIS certificate id (the id is inside the
/// signed payload, so a signature cannot be transplanted from one certificate to another).</para>
/// </summary>
public interface ICertificateSigner
{
    /// <summary>Signs a canonical payload with the current active key.</summary>
    Task<CertificateSignature> SignAsync(string canonicalPayload, CancellationToken ct = default);

    /// <summary>Checks a signature against the key that produced it.
    ///
    /// <para>Returns a verdict rather than a bool because "the signature does not match" and "the key that
    /// signed this was later compromised" are different answers with different consequences, and
    /// collapsing them would make a genuine certificate look forged.</para></summary>
    Task<CertificateSignatureVerdict> VerifyAsync(
        string keyId, string canonicalPayload, string signature, CancellationToken ct = default);
}

public sealed record CertificateSignature(string KeyId, string Signature);

/// <param name="Matches">Whether the signature is cryptographically valid for the payload.</param>
/// <param name="KeyKnown">False when no key with that id exists. Distinct from a mismatch: an unknown key
/// means we cannot judge, not that the certificate is fake.</param>
/// <param name="KeyCompromised">The key was later declared compromised. The certificate is still reported
/// valid — it really was issued by the platform — with a warning alongside it.</param>
public sealed record CertificateSignatureVerdict(bool Matches, bool KeyKnown, bool KeyCompromised);

/// <summary>
/// Builds the exact bytes that get signed (D-355, Phase 5).
///
/// <para><b>Determinism is the whole requirement.</b> The payload is rebuilt from the stored row at every
/// verification, so any variation — dictionary ordering, culture-dependent date formatting, a changed
/// separator — makes every existing certificate fail to verify. That failure would look exactly like
/// tampering, which is the worst possible way to be wrong.</para>
///
/// <para>Values are sorted by ordinal key and joined with characters that cannot appear in a key name, so
/// two different value sets cannot serialise to the same string. Dates are round-trip ISO-8601 in UTC,
/// never a local or culture-formatted rendering.</para>
/// </summary>
public static class CertificateCanonicalPayload
{
    /// <summary>Bumped only if the payload format changes. Recorded in the payload itself so a future
    /// format can be verified alongside this one instead of invalidating every certificate ever issued.</summary>
    public const string Version = "1";

    private const char PairSeparator = '';   // unit separator — cannot occur in a field key or value
    private const char FieldSeparator = '';  // record separator

    public static string Build(
        string certificateId,
        Guid eventId,
        Guid templateId,
        int templateVersion,
        DateTime issuedAt,
        IReadOnlyDictionary<string, string> values)
    {
        var parts = new List<string>
        {
            $"v{PairSeparator}{Version}",
            $"cid{PairSeparator}{certificateId}",
            $"eid{PairSeparator}{eventId:D}",
            $"tid{PairSeparator}{templateId:D}",
            $"tv{PairSeparator}{templateVersion}",
            // Round-trip UTC. A culture-formatted date would verify on the machine that signed it and
            // nowhere else.
            $"iat{PairSeparator}{issuedAt.ToUniversalTime():O}",
        };

        // Ordinal sort, so the same set of values always produces the same string regardless of the
        // dictionary's internal ordering or the machine's culture.
        foreach (var (key, value) in values.OrderBy(v => v.Key, StringComparer.Ordinal))
            parts.Add($"f:{key}{PairSeparator}{value}");

        return string.Join(FieldSeparator, parts);
    }
}
