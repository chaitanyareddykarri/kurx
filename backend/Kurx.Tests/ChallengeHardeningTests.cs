using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Kurx.Tests;

/// <summary>Dedicated negative + concurrency tests for the D-181 challenge hardening (owed since Phase 1):
/// a signature over the bare nonce must be rejected, three wrong match numbers must reject the challenge
/// outright, and two devices approving the same challenge must yield exactly one session.</summary>
public class ChallengeHardeningTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public ChallengeHardeningTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static string Sign(ECDsa key, string message) =>
        Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
    private static string SignMatched(ECDsa key, string nonce, int matchNumber) => Sign(key, $"{nonce}.{matchNumber:00}");

    private async Task<(HttpClient Client, Guid UserId, string Identifier)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var v = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        var body = await v.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        // Password-first (D-182) is the only route to a device-approval challenge since the identifier-only
        // `/v1/auth/login/start` was removed, so every test user needs one.
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = TestPassword });
        return (client, body.GetProperty("user_id").GetGuid(), "91" + phone);
    }

    /// <summary>Must not contain the account's own phone/username/email — the password policy rejects that.</summary>
    private const string TestPassword = "Tr0ubador&Staple!Horse";

    private static async Task<(Guid DeviceId, ECDsa Key)> EnrollDeviceAsync(HttpClient client)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var e = await (await client.PostAsJsonAsync("/v1/auth/devices/enroll", new { platform = "android", publicKeySpki = spki }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = e.GetProperty("device_id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId = e.GetProperty("challenge_id").GetGuid(), deviceId, signature = Sign(key, e.GetProperty("nonce").GetString()!) })).StatusCode);
        return (deviceId, key);
    }

    private async Task<(Guid ChallengeId, int MatchNumber, string Nonce)> StartLoginAsync(HttpClient device, string identifier, Guid challengeOwner)
    {
        var res = await _client.PostAsJsonAsync("/v1/auth/login/password",
            new { identifier, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var start = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("device_approval", start.GetProperty("next").GetString());
        var challengeId = start.GetProperty("challenge_id").GetGuid();
        var matchNumber = start.GetProperty("match_number").GetInt32();
        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var nonce = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId).GetProperty("nonce").GetString()!;
        return (challengeId, matchNumber, nonce);
    }

    private static async Task<string?> ErrorOf(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out var e) ? e.GetString() : null;

    // ── tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_approval_signed_over_the_bare_nonce_is_rejected()
    {
        var (device, userId, id) = await LoginAsync("9310000001");
        var (deviceId, key) = await EnrollDeviceAsync(device);
        var (challengeId, matchNumber, nonce) = await StartLoginAsync(device, id, userId);

        // Correct match number, but the signature is over the bare nonce (the pre-D-181 contract) — the
        // server verifies over "{nonce}.{NN}", so this must fail.
        var res = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature = Sign(key, nonce) });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_signature", await ErrorOf(res));
    }

    [Fact]
    public async Task Three_wrong_match_numbers_reject_the_challenge_outright()
    {
        var (device, userId, id) = await LoginAsync("9310000002");
        var (deviceId, key) = await EnrollDeviceAsync(device);
        var (challengeId, matchNumber, nonce) = await StartLoginAsync(device, id, userId);
        var wrong = matchNumber < 99 ? matchNumber + 1 : 10;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var r = await device.PostAsJsonAsync("/v1/auth/login/approve",
                new { challengeId, deviceId, matchNumber = wrong, signature = SignMatched(key, nonce, wrong) });
            Assert.Equal("invalid_match_number", await ErrorOf(r));
        }

        // The third wrong attempt rejects the whole challenge, not just the attempt.
        var third = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber = wrong, signature = SignMatched(key, nonce, wrong) });
        Assert.Equal("challenge_rejected", await ErrorOf(third));

        // Even the CORRECT code can no longer approve it.
        var correct = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature = SignMatched(key, nonce, matchNumber) });
        Assert.NotEqual(HttpStatusCode.OK, correct.StatusCode);
    }

    [Fact]
    public async Task Two_devices_approving_one_challenge_yield_exactly_one_success()
    {
        var (device, userId, id) = await LoginAsync("9310000003");
        var (deviceA, keyA) = await EnrollDeviceAsync(device);
        var (deviceB, keyB) = await EnrollDeviceAsync(device);
        var (challengeId, matchNumber, nonce) = await StartLoginAsync(device, id, userId);

        // Both trusted devices approve the same challenge concurrently, each with a valid signature.
        var approveA = device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId = deviceA, matchNumber, signature = SignMatched(keyA, nonce, matchNumber) });
        var approveB = device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId = deviceB, matchNumber, signature = SignMatched(keyB, nonce, matchNumber) });
        var results = await Task.WhenAll(approveA, approveB);

        // First approval wins: exactly one 200, the other told the challenge is already consumed.
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        var loser = results.Single(r => r.StatusCode != HttpStatusCode.OK);
        Assert.Equal("challenge_consumed", await ErrorOf(loser));
    }
}
