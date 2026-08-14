using System.Net;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// The presigned-URL round trip (D-302).
///
/// <para><b>Why this had to be an integration test.</b> The defect it locks down was not in the signing
/// or in the verification — those agreed with each other perfectly, which is exactly why a unit test
/// over <c>LocalDiskStorage</c> would have passed. It was in the step between them: the URL is built by
/// the provider, and the key is bound back out of it by <b>ASP.NET routing</b>, which does not decode
/// <c>%2F</c> into a path separator. <c>Uri.EscapeDataString</c> over the whole key turned every
/// <c>/</c> into <c>%2F</c>, so the receiver verified a signature over a key that no longer matched
/// the one that was signed, and every multi-segment key — which is every key the product issues —
/// answered <b>403</b>.</para>
///
/// <para>Nothing caught it for the same reason it is easy to miss: the provider generates and verifies
/// its own signature, so it is self-consistent in isolation. <b>No test had ever fetched a URL this
/// code produced.</b> That is the gap this file closes, and the assertion that matters is simply
/// "GET the URL we handed out and do not get 403".</para>
/// </summary>
public class StoragePresignTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public StoragePresignTests(KurxApiFactory factory) => _factory = factory;

    /// <summary>A multi-segment key is the whole point: <c>events/…</c>, <c>avatars/…</c>,
    /// <c>chat/…</c> — the product has no single-segment keys, which is why "some presigns worked"
    /// was never a symptom anyone saw.</summary>
    private const string Key = "events/presign-roundtrip/banner one.jpg";

    private static readonly byte[] Content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];

    [Fact]
    public async Task PresignedGet_url_is_actually_fetchable()
    {
        var client = _factory.CreateClient();
        var storage = _factory.Services.GetRequiredService<IStorage>();

        await storage.PutAsync(Key, Content, "image/png");
        var url = await storage.PresignGetAsync(Key);

        // The provider emits an absolute URL against its configured public base; the test client is
        // bound to the in-memory host, so only the path+query is meaningful here.
        var pathAndQuery = new Uri(url).PathAndQuery;
        var res = await client.GetAsync(pathAndQuery);

        // 403 is the regression: signature verified against a key the router handed back differently.
        Assert.NotEqual(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(Content, await res.Content.ReadAsByteArrayAsync());
    }

    /// <summary>The signature is the credential (D-110), so a tampered one must fail closed. Without
    /// this, "make the URL fetchable" could be satisfied by not checking the signature at all — which
    /// would turn a 403 bug into an open read primitive over every stored object.</summary>
    [Fact]
    public async Task Tampered_signature_is_refused()
    {
        var client = _factory.CreateClient();
        var storage = _factory.Services.GetRequiredService<IStorage>();

        await storage.PutAsync(Key, Content, "image/png");
        var url = await storage.PresignGetAsync(Key);
        var pathAndQuery = new Uri(url).PathAndQuery;

        // Flip the last character of the signature rather than dropping it: a missing `sig` could be
        // rejected by a different branch than a wrong one.
        var tampered = pathAndQuery[..^1] + (pathAndQuery[^1] == 'a' ? 'b' : 'a');

        var res = await client.GetAsync(tampered);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>A key whose segments need escaping must still round-trip — the space in <see cref="Key"/>
    /// is deliberate. Escaping per segment is only correct if the separators stay literal AND the
    /// contents stay escaped; this fails if someone "fixes" the 403 by not escaping at all.</summary>
    [Fact]
    public async Task Key_segments_are_still_escaped()
    {
        var storage = _factory.Services.GetRequiredService<IStorage>();
        var url = await storage.PresignGetAsync(Key);

        Assert.Contains("events/presign-roundtrip/", url);   // separators literal
        Assert.DoesNotContain("banner one.jpg", url);        // the space escaped
        Assert.DoesNotContain("%2F", url);                   // the regression, stated directly
    }
}
