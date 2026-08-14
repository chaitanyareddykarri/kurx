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

/// <summary>Password reset ceremony (Phase 2C, D-127). INV-B: OTP alone never resets a password — a
/// recovery code or a device step-up is always the second factor. Real HTTP against an isolated database.</summary>
public class PasswordResetTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordResetTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string Old = "correct horse battery staple";
    private const string New = "an entirely different passphrase";

    private static string Sign(ECDsa key, string message) =>
        Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
    private static string SignMatched(ECDsa key, string nonce, int matchNumber) => Sign(key, $"{nonce}.{matchNumber:00}");

    private async Task<(HttpClient Client, Guid UserId, string Identifier, string Phone)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var v = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, v.StatusCode);
        var body = await v.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        return (client, body.GetProperty("user_id").GetGuid(), "91" + phone, phone);
    }

    private async Task<IReadOnlyList<string>> GenCodesAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecoveryCodeService>().GenerateAsync(userId);
    }

    private async Task<string> IssueBrowserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ITrustedBrowserService>()
            .IssueAsync(userId, new TrustedBrowserContext(Label: "Test"))).Token;
    }

    /// <summary>Reads the most recent OTP the user would have received (the reset OTP once /reset/start ran).</summary>
    private string ResetOtp(string phone) => _factory.WhatsApp.LastOtpFor(phone);

    private static async Task<string?> ErrorOf(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out var e) ? e.GetString() : null;

    /// <summary>Starts the ceremony and hands back what D-330 now returns: the approval a trusted device
    /// can sign off, and the transaction token that ties it to this reset.</summary>
    private async Task<JsonElement> StartResetAsync(string identifier)
    {
        var res = await _client.PostAsJsonAsync("/v1/auth/password/reset/start", new { identifier });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Otp_alone_can_never_reset_a_password()
    {
        var (_, _, id, phone) = await LoginAsync("9800000001");
        await StartResetAsync(id);
        var res = await _client.PostAsJsonAsync("/v1/auth/password/reset/complete",
            new { identifier = id, otpCode = ResetOtp(phone), newPassword = New });   // no recovery code, no step-up
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("second_factor_required", await ErrorOf(res));
    }

    [Fact]
    public async Task Otp_plus_a_recovery_code_resets_the_password_and_the_new_one_works()
    {
        var (client, userId, id, phone) = await LoginAsync("9800000002");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Old })).StatusCode);
        var codes = await GenCodesAsync(userId);

        await StartResetAsync(id);
        var reset = await _client.PostAsJsonAsync("/v1/auth/password/reset/complete",
            new { identifier = id, otpCode = ResetOtp(phone), newPassword = New, recoveryCode = codes[0] });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var access = (await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString();

        // The ceremony logged the user in.
        var me = _factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync("/v1/me")).StatusCode);

        // The NEW password works for a fresh login (with a browser cookie as factor 2); the OLD one does not.
        var cookie = await IssueBrowserAsync(userId);
        var login = _factory.CreateClient();
        login.DefaultRequestHeaders.Add("Cookie", $"{TrustedBrowserEndpoints.CookieName}={cookie}");
        Assert.Equal(HttpStatusCode.OK, (await login.PostAsJsonAsync("/v1/auth/login/password",
            new { identifier = id, password = New })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await login.PostAsJsonAsync("/v1/auth/login/password",
            new { identifier = id, password = Old })).StatusCode);
    }

    [Fact]
    public async Task A_reset_cascade_revokes_trusted_browsers()
    {
        var (client, userId, id, phone) = await LoginAsync("9800000003");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Old })).StatusCode);
        var codes = await GenCodesAsync(userId);
        var doomed = await IssueBrowserAsync(userId);

        await StartResetAsync(id);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/v1/auth/password/reset/complete",
            new { identifier = id, otpCode = ResetOtp(phone), newPassword = New, recoveryCode = codes[0] })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ITrustedBrowserService>().VerifyAsync(userId, doomed));
    }

    [Fact]
    public async Task A_wrong_otp_and_an_unknown_identifier_both_fail_the_same_way()
    {
        var (_, userId, id, _) = await LoginAsync("9800000004");
        var codes = await GenCodesAsync(userId);
        await StartResetAsync(id);

        var wrongOtp = await _client.PostAsJsonAsync("/v1/auth/password/reset/complete",
            new { identifier = id, otpCode = "000000", newPassword = New, recoveryCode = codes[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongOtp.StatusCode);
        Assert.Equal("invalid_reset", await ErrorOf(wrongOtp));

        var unknown = await _client.PostAsJsonAsync("/v1/auth/password/reset/complete",
            new { identifier = "no-such-user-xyz", otpCode = "000000", newPassword = New, recoveryCode = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("invalid_reset", await ErrorOf(unknown));
    }

    [Fact]
    public async Task A_reset_to_a_weak_password_is_rejected_by_policy()
    {
        var (_, userId, id, phone) = await LoginAsync("9800000005");
        var codes = await GenCodesAsync(userId);
        await StartResetAsync(id);
        var res = await _client.PostAsJsonAsync("/v1/auth/password/reset/complete",
            new { identifier = id, otpCode = ResetOtp(phone), newPassword = "short", recoveryCode = codes[0] });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("password_too_short", await ErrorOf(res));
    }

    [Fact]
    public async Task Otp_plus_a_device_step_up_resets_the_password()
    {
        var (client, userId, id, phone) = await LoginAsync("9800000006");

        // Enroll a trusted device.
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var e = await (await client.PostAsJsonAsync("/v1/auth/devices/enroll", new { platform = "android", publicKeySpki = spki }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = e.GetProperty("device_id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId = e.GetProperty("challenge_id").GetGuid(), deviceId, signature = Sign(key, e.GetProperty("nonce").GetString()!) })).StatusCode);

        // D-330. This used to read: perform any step-up, then reset with OTP alone, because the second
        // factor was the ambient "did this account step up recently". It is now an approval the device
        // gives to THIS reset by name — the same capability, no longer satisfiable by unrelated activity.
        var started = await StartResetAsync(id);
        var approvalId = started.GetProperty("approval_id").GetGuid();
        var match = started.GetProperty("match_number").GetInt32();
        var resetToken = started.GetProperty("reset_token").GetString()!;

        var pending = await client.GetFromJsonAsync<JsonElement>("/v1/auth/password/reset/pending");
        var nonce = pending.EnumerateArray()
            .Single(x => x.GetProperty("approval_id").GetGuid() == approvalId)
            .GetProperty("nonce").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/reset/approve",
            new { approvalId, deviceId, matchNumber = match, signature = SignMatched(key, nonce, match) })).StatusCode);

        var res = await _client.PostAsJsonAsync("/v1/auth/password/reset/complete",
            new { identifier = id, otpCode = ResetOtp(phone), newPassword = New, approvalId, resetToken });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotEqual(Guid.Empty, userId);
    }
}
