using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.IdCards;

/// <summary>Storage keys for a generated ID card's artefacts (D-362), mirroring
/// <c>CertificateStorageKeys</c> so both document families are laid out the same way under an event.
///
/// <para><b>The size is part of the key (D-385).</b> A rendered badge is a raster at one physical size, so
/// one card issued at <c>lanyard</c> and later printed at <c>card</c> are two different artefacts. Keying
/// them together meant a stored lanyard raster was served for a CR80 request and drawn into a landscape
/// slot at the wrong aspect — the size selector silently stopped working the moment cards were issued.
/// With the size in the path a mismatched request simply misses and re-renders.</para></summary>
public static class IdCardStorageKeys
{
    public static string Pdf(Guid eventId, Guid cardId, string sizeKey) =>
        $"events/{eventId}/id-cards/{cardId:N}/{Size(sizeKey)}/card.pdf";

    public static string Png(Guid eventId, Guid cardId, string sizeKey) =>
        $"events/{eventId}/id-cards/{cardId:N}/{Size(sizeKey)}/card.png";

    /// <summary>Callers pass a <see cref="BadgeSize.Key"/>, which is already one of a fixed set — this is
    /// the belt on the braces, because the value lands in a storage path.</summary>
    private static string Size(string sizeKey) =>
        BadgeSize.FromKey(sizeKey)?.Key ?? BadgeSize.Lanyard.Key;
}

/// <summary>
/// Allocates the two public identifiers on an <c>IdCard</c> (D-331).
///
/// <para><b>Not the certificate allocator.</b> <c>ICertificateIdAllocator</c> is backed by a per-event
/// database sequence and a configurable pattern, because a certificate id is a formal record an
/// institution may need to reproduce years later. A card number is an operational label on a lanyard:
/// unique per issuing org, regenerated on reissue, and never referenced after the event. Borrowing the
/// sequence machinery would mean an event's certificate numbering and its badge numbering consumed the
/// same counter.</para>
/// </summary>
public static class IdCardCodes
{
    // Crockford-style base32 without I, L, O and U: those are what turn a code read aloud at a gate, or
    // typed off a printed badge, into the wrong code.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Ten characters, matching <c>Certificate.VerifyCode</c>'s shape so one verification
    /// convention covers both documents (D-331).</summary>
    public static string NewVerifyCode() => Random(10);

    /// <summary>Allocates the next card numbers for an org, as a batch.
    ///
    /// <para>Reads the current maximum once and counts up from it, rather than querying per card: a print
    /// run issues hundreds at a time and a round-trip each would dominate the operation. The
    /// <c>(OrgId, CardNumber)</c> unique index is still the authority — a concurrent run colliding is
    /// caught there and surfaces as a failed save rather than as two people holding the same number.</para>
    /// </summary>
    public static async Task<Queue<string>> AllocateCardNumbersAsync(
        KurxDbContext db, Guid orgId, int count, CancellationToken ct)
    {
        var existing = await db.IdCards.AsNoTracking()
            .Where(c => c.OrgId == orgId && c.CardNumber.StartsWith("KRX-"))
            .Select(c => c.CardNumber)
            .ToListAsync(ct);

        var highest = existing
            .Select(n => int.TryParse(n[4..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();

        var numbers = new Queue<string>(count);
        for (var i = 1; i <= count; i++) numbers.Enqueue($"KRX-{highest + i:D5}");
        return numbers;
    }

    private static string Random(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
