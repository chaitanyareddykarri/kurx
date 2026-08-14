using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Api.Middleware;

namespace Kurx.Tests;

public class ExceptionHandlingTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public ExceptionHandlingTests(KurxApiFactory factory)
    {
        _factory = factory;
        // ResetDatabase() must run BEFORE CreateClient(): CreateClient starts the host (which
        // migrates kurx_test), and ResetDatabase then drops+recreates the DB and only re-migrates
        // if the host hasn't started yet. Reversed order leaves this class running against a
        // table-less kurx_test, 500-ing every DB-touching request. (Same fix AuthTests already has.)
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    [Fact]
    public async Task Correlation_id_is_generated_when_absent()
    {
        var res = await _client.GetAsync("/health");
        Assert.True(res.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task Correlation_id_is_echoed_back_when_provided()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add(CorrelationIdMiddleware.HeaderName, "test-correlation-42");
        var res = await _client.SendAsync(req);
        Assert.Equal("test-correlation-42", res.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Expected_error_responses_use_problem_details_shape()
    {
        var phone = "9200000001";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var res = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("title", out _));
        Assert.True(body.TryGetProperty("correlationId", out var correlationId));
        Assert.False(string.IsNullOrWhiteSpace(correlationId.GetString()));
        Assert.Equal("invalid_code", body.GetProperty("error").GetString());
    }
}
