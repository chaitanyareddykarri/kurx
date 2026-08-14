using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kurx.Tests;

/// <summary>Guards the API-contract hardening pass: a response type introduced to replace an anonymous
/// object must serialize to the <b>same bytes</b> the anonymous object did.
///
/// <para>These assert over HTTP, on the raw response string, deliberately. The risk being guarded is a
/// serialization-level one — <c>SnakeCaseResponseConverter</c> selects response types <b>by namespace</b>,
/// so a DTO declared outside <c>Kurx.Application.Abstractions</c> silently emits camelCase and every client
/// breaks with a green build. Deserializing into a typed object here would hide exactly that: a
/// case-insensitive read succeeds against either spelling.</para></summary>
public class ResponseContractTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public ResponseContractTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private async Task<string> LoginAsync(string phone)
    {
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return (await verify.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
    }

    /// <summary>80 endpoints across 34 files answered <c>Results.Ok(new { ok = true })</c>; they now answer
    /// the shared <c>OperationAck</c>. <c>/v1/auth/otp/request</c> is one of them and is anonymous, so it
    /// exercises the real serializer pipeline with no login first.
    ///
    /// <para>The assertion is on the exact body, not on a parsed property: <c>{"Ok":true}</c> and
    /// <c>{"ok":true}</c> both parse, and only one of them is the contract three clients were written
    /// against.</para></summary>
    [Fact]
    public async Task OperationAck_serializes_exactly_as_the_anonymous_shape_it_replaced()
    {
        var response = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "9000004242" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"ok\":true}", await response.Content.ReadAsStringAsync());
    }

    /// <summary>A 204 endpoint must keep answering 204 with a genuinely empty body. Stage A declared 32 of
    /// these in OpenAPI — they had all been advertised as <c>200</c>, which was simply false — so this
    /// proves the declaration described reality rather than changing it.
    ///
    /// <para>Unblock is the right probe because it is unconditionally 204 by design (see
    /// <c>AccountEndpoints</c>): it needs no prior block and no such target user, so a failure here is a
    /// contract regression and never a domain precondition.</para></summary>
    [Fact]
    public async Task A_204_endpoint_still_answers_204_with_an_empty_body()
    {
        var token = await LoginAsync("9000004243");

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/me/blocks/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    /// <summary>File downloads must be described as raw binary, never base64.
    ///
    /// <para><c>byte[]</c> renders as <c>format: byte</c>, which in OpenAPI 3.0 means base64-encoded; these
    /// endpoints stream the bytes themselves. A generated client would base64-decode a CSV and get garbage.
    /// <c>BinaryResponseSchemaFilter</c> corrects it, and this reads the <b>real generated document</b> off
    /// the running host rather than re-implementing the rule.</para>
    ///
    /// <para>The two assertions are deliberately different in kind: the first is the rule, the second is a
    /// vacuity guard. Without it this test would still pass if every download endpoint were deleted, or if
    /// the filter silently stopped matching anything — the exact failure mode that made the client-contract
    /// gate report a clean pass for months (D-303).</para></summary>
    [Fact]
    public async Task File_downloads_are_described_as_binary_not_base64()
    {
        using var doc = JsonDocument.Parse(await _client.GetStringAsync("/swagger/v1/swagger.json"));

        var offenders = new List<string>();
        var binary = new List<string>();

        foreach (var path in doc.RootElement.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            if (!operation.Value.TryGetProperty("responses", out var responses)) continue;
            foreach (var response in responses.EnumerateObject())
            {
                if (!response.Value.TryGetProperty("content", out var content)) continue;
                foreach (var media in content.EnumerateObject())
                {
                    if (media.Name.Contains("json", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!media.Value.TryGetProperty("schema", out var schema)) continue;
                    if (!schema.TryGetProperty("format", out var format)) continue;

                    var where = $"{operation.Name.ToUpperInvariant()} {path.Name} [{media.Name}]";
                    if (format.GetString() == "byte") offenders.Add(where);
                    if (format.GetString() == "binary") binary.Add(where);
                }
            }
        }

        Assert.Empty(offenders);
        Assert.NotEmpty(binary);
    }
}
