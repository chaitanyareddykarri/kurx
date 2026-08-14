using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kurx.Tests;

public class ValidationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public ValidationTests(KurxApiFactory factory)
    {
        _factory = factory;
        // ResetDatabase() must run BEFORE CreateClient() (see ExceptionHandlingTests for the full
        // rationale): reversed order leaves this class running against a table-less kurx_test.
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoggedInClientAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        var tokens = await Json(verify);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    [Fact]
    public async Task Empty_phone_on_otp_request_is_rejected_before_reaching_the_service()
    {
        var res = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await Json(res);
        Assert.Equal("validation_failed", body.GetProperty("error").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("Phone", out _));
    }

    [Fact]
    public async Task Non_six_digit_otp_code_is_rejected_by_validation()
    {
        var res = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone = "9300000001", code = "12" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Empty_org_name_is_rejected()
    {
        var client = await LoggedInClientAsync("9300000002");
        var res = await client.PostAsJsonAsync("/v1/orgs/", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invalid_role_on_add_member_is_rejected_by_validation()
    {
        var client = await LoggedInClientAsync("9300000003");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Validation Org");

        var res = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/members", new { phone = "9300000004", role = "bogus" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Malformed_ifsc_on_bank_kyc_is_rejected_by_validation()
    {
        var client = await LoggedInClientAsync("9300000005");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Kyc Validation Org");

        var res = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/kyc/bank", new
        {
            legalName = "Org",
            accountNumber = "123456",
            ifsc = "not-an-ifsc",
            holderName = "Org",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await Json(res)).GetProperty("error").GetString());
    }
}
