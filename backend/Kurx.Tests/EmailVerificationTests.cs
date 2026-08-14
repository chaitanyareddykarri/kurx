using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kurx.Tests;

/// <summary>Registration ceremony + email verification (Phase 2D). Real HTTP against an isolated database,
/// reading the emailed OTP through the capturing email sender.</summary>
public class EmailVerificationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public EmailVerificationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string Strong = "correct horse battery staple";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var v = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, v.StatusCode);
        var body = await v.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        return (client, body.GetProperty("user_id").GetGuid());
    }

    private static async Task<JsonElement> StatusAsync(HttpClient client)
        => await (await client.GetAsync("/v1/auth/registration/status")).Content.ReadFromJsonAsync<JsonElement>();

    private static string[] Remaining(JsonElement status)
        => status.GetProperty("remaining").EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static async Task<string?> ErrorOf(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out var e) ? e.GetString() : null;

    // ── tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_emailed_otp_verifies_the_address_and_the_status_reflects_it()
    {
        var (client, _) = await LoginAsync("9900000001");
        const string email = "person1@example.com";

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/email/verify/start", new { email })).StatusCode);
        var code = _factory.Email.LastOtpFor(email);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/email/verify/complete", new { email, code })).StatusCode);

        var status = await StatusAsync(client);
        Assert.True(status.GetProperty("email_verified").GetBoolean());
        Assert.Equal(email, status.GetProperty("email").GetString());
        Assert.DoesNotContain("verify_email", Remaining(status));
    }

    [Fact]
    public async Task A_wrong_code_does_not_verify_the_email()
    {
        var (client, _) = await LoginAsync("9900000002");
        const string email = "person2@example.com";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/email/verify/start", new { email })).StatusCode);

        var res = await client.PostAsJsonAsync("/v1/auth/email/verify/complete", new { email, code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_code", await ErrorOf(res));
        Assert.False((await StatusAsync(client)).GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    public async Task A_malformed_address_is_rejected()
    {
        var (client, _) = await LoginAsync("9900000003");
        var res = await client.PostAsJsonAsync("/v1/auth/email/verify/start", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_email", await ErrorOf(res));
    }

    [Fact]
    public async Task An_email_already_owned_by_another_account_is_rejected()
    {
        var (a, _) = await LoginAsync("9900000004");
        var (b, _) = await LoginAsync("9900000005");
        const string email = "shared@example.com";

        Assert.Equal(HttpStatusCode.OK, (await a.PostAsJsonAsync("/v1/auth/email/verify/start", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.PostAsJsonAsync("/v1/auth/email/verify/complete",
            new { email, code = _factory.Email.LastOtpFor(email) })).StatusCode);

        var res = await b.PostAsJsonAsync("/v1/auth/email/verify/start", new { email });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("email_taken", await ErrorOf(res));
    }

    [Fact]
    public async Task Registration_status_tracks_the_remaining_ceremony_steps()
    {
        var (client, _) = await LoginAsync("9900000006");

        var before = await StatusAsync(client);
        Assert.False(before.GetProperty("has_password").GetBoolean());
        Assert.True(before.GetProperty("phone_verified").GetBoolean());          // phone verified by the OTP login
        Assert.False(before.GetProperty("has_trusted_device").GetBoolean());
        Assert.Contains("create_password", Remaining(before));
        Assert.Contains("verify_email", Remaining(before));
        Assert.Contains("enroll_device", Remaining(before));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong })).StatusCode);

        var after = await StatusAsync(client);
        Assert.True(after.GetProperty("has_password").GetBoolean());
        Assert.DoesNotContain("create_password", Remaining(after));
    }
}
