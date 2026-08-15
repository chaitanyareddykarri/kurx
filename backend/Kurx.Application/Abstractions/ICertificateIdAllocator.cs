namespace Kurx.Application.Abstractions;

/// <summary>
/// Hands out the next certificate id for an event (D-344).
///
/// <para><b>Why this is its own boundary.</b> Allocation is the one part of issuing a certificate that
/// cannot be done correctly in application code: it is a contended counter, and every obvious
/// implementation — read the sequence, add one, write it back — issues the same id twice the moment two
/// generations overlap. Putting it behind an interface keeps that single dangerous operation in one
/// reviewed place instead of inlined into whatever service happens to need an id.</para>
///
/// <para><b>The contract is stronger than "returns a string".</b> An implementation must guarantee that
/// two concurrent callers, in separate transactions, never receive the same value — and it must do so
/// without serialising all certificate generation behind a lock held in the API process.</para>
///
/// <para>The database's unique index on <c>issued_certificates.CertificateId</c> remains the final
/// guarantee regardless. This interface exists to make collisions impossible in practice; the index is
/// what makes them impossible in fact.</para>
/// </summary>
public interface ICertificateIdAllocator
{
    /// <summary>Allocates the next certificate id for <paramref name="eventId"/>, creating the event's
    /// id rule with platform defaults if it has none yet.
    ///
    /// <para>Each call consumes a sequence value. A caller that allocates and then abandons the work
    /// leaves a gap — deliberately: reusing a skipped number would mean an id could refer to two
    /// different certificates across time, and gaps are cheaper than that ambiguity.</para></summary>
    /// <param name="issuedAt">Stamped into date tokens in the configured pattern. Passed in rather than
    /// read from the clock so an id's date component matches the certificate's own issue time.</param>
    Task<CertificateIdAllocation> AllocateAsync(Guid eventId, DateTime issuedAt, CancellationToken ct = default);
}

/// <param name="CertificateId">The formatted, public identifier — e.g. <c>CERT-2026-00001</c>.</param>
/// <param name="Sequence">The raw sequence value consumed, kept for diagnostics: "which number did this
/// certificate get" is unanswerable from the formatted string once a pattern has been changed.</param>
public sealed record CertificateIdAllocation(string CertificateId, long Sequence);
