namespace Kurx.Application.Abstractions;

/// <summary>
/// Turning a stored key into something a client can actually fetch.
///
/// <para><b>The bug this closes.</b> <see cref="IStorage.PresignGetAsync"/> returns
/// <c>{base}/v1/storage/{key}?exp=…&amp;sig=…</c>; a bare key is not fetchable by anyone. Every
/// projection that carried <c>avatar_key</c> and nothing else therefore rendered nothing — a client
/// feeding the key to <c>&lt;img src&gt;</c> resolves it against its OWN origin and takes a 404, so
/// every avatar on every surface silently fell back to initials. That is D-302's finding ("a storage
/// key is not a URL"), which swept the event projections and left the person-shaped ones behind.</para>
///
/// <para><b>Why an extension rather than a method per service.</b> Eleven services build a view with a
/// person's avatar on it. A private <c>PresignOrNull</c> in each is eleven copies of one rule, free to
/// disagree about the null case — and the null case is the load-bearing one.</para>
/// </summary>
public static class StorageUrls
{
    /// <summary>A fetchable URL for <paramref name="key"/>, or <c>null</c> when there is no key.
    ///
    /// <para><b>Null in, null out.</b> An account with no picture must never receive a URL to nothing:
    /// every client tests the URL for presence to decide between the image and the initials fallback,
    /// so a non-null URL pointing at a missing object renders a broken image where initials belong.</para>
    ///
    /// <para>Presigning is an HMAC over the key, not a round-trip to storage, so calling this per row of
    /// a page is cheap — the same reasoning <c>SearchService.WithBannerUrlsAsync</c> records for event
    /// cards. It still cannot run inside an EF projection: translate the query first, then presign over
    /// the materialised rows.</para></summary>
    public static async Task<string?> PresignOrNullAsync(
        this IStorage storage, string? key, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(key) ? null : await storage.PresignGetAsync(key, null, ct);
}
