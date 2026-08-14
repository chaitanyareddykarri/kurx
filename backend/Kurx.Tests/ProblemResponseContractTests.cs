using System.Net.Http.Json;
using System.Text.Json;

namespace Kurx.Tests;

/// <summary>The published contract describes failure, not only success.
///
/// <para><b>The gap this closes.</b> D-313 gave 461 of 518 operations a described success body while the
/// whole API declared <b>five</b> error responses — against 634 error returns that all answer the same
/// RFC7807 shape. A generated client had typed success bodies and nothing at all for failure, which is
/// where most of its error-handling code lives. <c>ProblemResponseOperationFilter</c> derives those
/// responses from endpoint metadata rather than restating them on 600 routes.</para>
///
/// <para>Asserted against the <b>live</b> document served by the running test host, not the committed
/// <c>openapi.json</c>: the committed file is regenerated on demand, so reading it would test when
/// someone last ran the generator rather than what the filter does.</para></summary>
public class ProblemResponseContractTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public ProblemResponseContractTests(KurxApiFactory factory) => _factory = factory;

    private async Task<JsonElement> SpecAsync()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static JsonElement Operation(JsonElement spec, string path, string method) =>
        spec.GetProperty("paths").GetProperty(path).GetProperty(method);

    [Fact]
    public async Task An_authenticated_endpoint_declares_401()
    {
        var op = Operation(await SpecAsync(), "/v1/me", "get");

        Assert.True(op.GetProperty("responses").TryGetProperty("401", out var unauthorized),
            "/v1/me requires a token, so the contract has to say a 401 is possible.");
        // The body is described too — a status code with no shape still leaves the client guessing.
        Assert.True(unauthorized.TryGetProperty("content", out var content));
        Assert.True(content.TryGetProperty("application/problem+json", out _),
            "Errors answer application/problem+json, which is what the client parses.");
    }

    [Fact]
    public async Task An_anonymous_endpoint_does_not_claim_a_401_it_cannot_produce()
    {
        // Public reads take no token; describing a 401 on one would be fiction, and fiction in a contract
        // is worse than the silence it replaced.
        //
        // Found by path prefix rather than by a literal key: the exact spelling of a route ("/v1/events"
        // vs "/v1/events/") is Swashbuckle's business, and hard-coding one made this test fail with a
        // KeyNotFoundException that said nothing about the behaviour under test.
        var spec = await SpecAsync();

        var checkedAny = false;
        foreach (var path in spec.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith("/v1/public/", StringComparison.Ordinal)) continue;
            if (!path.Value.TryGetProperty("get", out var get)) continue;
            // A public-profile read is anonymous by design (D-201) — it is what an unauthenticated
            // visitor loads.
            Assert.False(get.GetProperty("responses").TryGetProperty("401", out _),
                $"GET {path.Name} takes no token, so it must not advertise a 401.");
            checkedAny = true;
        }

        Assert.True(checkedAny, "No /v1/public/* GET found — the anonymous surface should not be empty.");
    }

    [Fact]
    public async Task An_endpoint_with_a_body_declares_400()
    {
        var op = Operation(await SpecAsync(), "/v1/auth/otp/request", "post");

        Assert.True(op.GetProperty("responses").TryGetProperty("400", out _),
            "A route that accepts a body can reject it — malformed JSON and failed validation both 400.");
    }

    [Fact]
    public async Task A_policy_gated_endpoint_declares_403_as_well_as_401()
    {
        var spec = await SpecAsync();

        // Any KurxAdmin route: authenticated callers without the claim are refused, which is a different
        // outcome from having no token at all and deserves its own line in the contract.
        var found = false;
        foreach (var path in spec.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith("/v1/admin", StringComparison.Ordinal)) continue;
            foreach (var method in path.Value.EnumerateObject())
            {
                var responses = method.Value.GetProperty("responses");
                if (!responses.TryGetProperty("403", out _)) continue;
                Assert.True(responses.TryGetProperty("401", out _),
                    $"{method.Name.ToUpperInvariant()} {path.Name} declares 403 but not 401.");
                found = true;
                break;
            }
            if (found) break;
        }

        Assert.True(found, "No admin operation declared a 403 — the policy gate is not being described.");
    }

    /// <summary>The whole point, measured: the error surface is no longer five responses.</summary>
    [Fact]
    public async Task The_error_contract_covers_the_API_rather_than_a_handful_of_routes()
    {
        var spec = await SpecAsync();

        var errors = 0;
        foreach (var path in spec.GetProperty("paths").EnumerateObject())
            foreach (var method in path.Value.EnumerateObject())
                foreach (var response in method.Value.GetProperty("responses").EnumerateObject())
                    if (int.TryParse(response.Name, out var code) && code >= 400) errors++;

        // Before the filter: 5 across 518 operations. The floor is deliberately far below whatever it
        // measures today — this guards the "someone silently removed the filter" case, not a count.
        Assert.True(errors > 200, $"Only {errors} error responses are declared across the API.");
    }
}
