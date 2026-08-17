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
            // nowhere else — and a timestamp finer than the database can hold verifies on the machine that
            // signed it and nowhere else either. See Storable.
            $"iat{PairSeparator}{Storable(issuedAt):O}",
        };

        // Ordinal sort, so the same set of values always produces the same string regardless of the
        // dictionary's internal ordering or the machine's culture.
        foreach (var (key, value) in values.OrderBy(v => v.Key, StringComparer.Ordinal))
            parts.Add($"f:{key}{PairSeparator}{value}");

        return string.Join(FieldSeparator, parts);
    }

    /// <summary>The instant at the precision that survives being stored.
    ///
    /// <para>Signing happens over a <see cref="DateTime"/> still in memory; verification rebuilds the
    /// payload from the row that was written. PostgreSQL <c>timestamptz</c> keeps <b>microseconds</b> and
    /// .NET ticks are 100 nanoseconds, so anything finer is silently truncated on the way in — and the two
    /// payloads differ by the digits that were dropped. The signature then fails, and a genuine
    /// certificate reports as tampered: the worst possible way to be wrong, arriving at the person holding
    /// a real document.</para>
    ///
    /// <para>It hid because the clock differs by platform. On macOS <c>DateTime.UtcNow</c> is already
    /// microsecond-granular, so every value round-trips exactly and the suite passes; on Linux — CI, and
    /// every deployed container — roughly nine in ten carry sub-microsecond ticks, so most certificates
    /// signed there could never verify. It was a production defect that only CI could see.</para>
    ///
    /// <para>Truncation, not rounding, because that is what PostgreSQL does with the digits it cannot
    /// keep. Applied here rather than at issuance so both halves normalise identically no matter which
    /// caller built the payload, and so the format string is untouched — a value that was already whole
    /// microseconds is unchanged, which is what keeps certificates issued before this fix verifying.</para></summary>
    private static DateTime Storable(DateTime issuedAt)
    {
        var utc = issuedAt.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
    }
}
