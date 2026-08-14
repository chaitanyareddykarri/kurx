using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Api.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>Security Center backend (Phase 2E): the aggregate overview, the user's own activity feed, and
/// "sign out everywhere". Real HTTP against an isolated database.</summary>
public class SecurityCenterTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public SecurityCenterTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string Pw = "correct horse battery staple";

    private static string Sign(ECDsa key, string message) =>
        Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

    private async Task<(HttpClient Client, Guid UserId, string Identifier)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var v = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        var body = await v.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        return (client, body.GetProperty("user_id").GetGuid(), "91" + phone);
    }

    private async Task<string> IssueBrowserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ITrustedBrowserService>()
            .IssueAsync(userId, new TrustedBrowserContext(Label: "Test"))).Token;
    }

    private async Task<bool> BrowserValidAsync(Guid userId, string token)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITrustedBrowserService>().VerifyAsync(userId, token);
    }

    private static async Task EnrollDeviceAsync(HttpClient client)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var e = await (await client.PostAsJsonAsync("/v1/auth/devices/enroll", new { platform = "android", publicKeySpki = spki }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify", new
        {
            challengeId = e.GetProperty("challenge_id").GetGuid(),
            deviceId = e.GetProperty("device_id").GetGuid(),
            signature = Sign(key, e.GetProperty("nonce").GetString()!),
        })).StatusCode);
    }

    private static async Task<JsonElement> OverviewAsync(HttpClient client)
        => await (await client.GetAsync("/v1/auth/security-center")).Content.ReadFromJsonAsync<JsonElement>();

    // ── tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_overview_reflects_the_users_factors_and_credentials()
    {
        var (client, userId, _) = await LoginAsync("9210000001");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Pw })).StatusCode);
        await IssueBrowserAsync(userId);
        await EnrollDeviceAsync(client);

        var o = await OverviewAsync(client);
        Assert.True(o.GetProperty("has_password").GetBoolean());
        Assert.True(o.GetProperty("phone_verified").GetBoolean());
        Assert.Equal(1, o.GetProperty("trusted_browsers").GetInt32());
        Assert.Equal(1, o.GetProperty("trusted_devices").GetInt32());
        Assert.True(o.GetProperty("can_step_up").GetBoolean());
    }

    [Fact]
    public async Task The_activity_feed_returns_the_users_own_security_events()
    {
        var (client, _, _) = await LoginAsync("9210000002");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Pw })).StatusCode);

        var activity = await (await client.GetAsync("/v1/auth/security-center/activity")).Content.ReadFromJsonAsync<JsonElement>();
        var types = activity.EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToArray();
        Assert.Contains("password.created", types);
    }

    [Fact]
    public async Task Sign_out_everywhere_revokes_sessions_and_trusted_browsers()
    {
        var (client, userId, id) = await LoginAsync("9210000003");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Pw })).StatusCode);

        // Create a real session via the password + trusted-browser fast path.
        var cookie = await IssueBrowserAsync(userId);
        var login = _factory.CreateClient();
        login.DefaultRequestHeaders.Add("Cookie", $"{TrustedBrowserEndpoints.CookieName}={cookie}");
        var session = await login.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        Assert.True((await OverviewAsync(client)).GetProperty("active_sessions").GetInt32() >= 1);
        var doomed = await IssueBrowserAsync(userId);

        var res = await client.PostAsync("/v1/auth/security-center/sign-out-all", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revoked").GetInt32() >= 1);

        Assert.False(await BrowserValidAsync(userId, doomed));            // browsers forgotten
        Assert.Equal(0, (await OverviewAsync(client)).GetProperty("active_sessions").GetInt32());   // sessions gone
    }
}
