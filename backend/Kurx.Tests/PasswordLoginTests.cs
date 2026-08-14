using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Api.Endpoints;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>Password-first login orchestration (Phase 2B, architecture §5): factor 1 = password, factor 2 =
/// a trusted-browser cookie or a trusted-device approval. Real HTTP against kurx_test.</summary>
public class PasswordLoginTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordLoginTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string Pw = "correct horse battery staple";

    private static string Sign(ECDsa key, string message) =>
        Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
    private static string SignMatched(ECDsa key, string nonce, int matchNumber) => Sign(key, $"{nonce}.{matchNumber:00}");

    /// <summary>OTP-login a fresh account and return an authenticated client, the user id, and the login
    /// identifier (E.164 digits) that resolves back to it.</summary>
    private async Task<(HttpClient Client, Guid UserId, string Identifier)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var v = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, v.StatusCode);
        var body = await v.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        return (client, body.GetProperty("user_id").GetGuid(), "91" + phone);
    }

    private static async Task SetPasswordAsync(HttpClient client)
        => Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Pw })).StatusCode);

    /// <summary>Full trusted-device enrollment for an authenticated client: register the key, sign the
    /// enrollment nonce (bare — enrollment carries no match number), verify → Trusted.</summary>
    private static async Task<(Guid DeviceId, ECDsa Key)> EnrollDeviceAsync(HttpClient client)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var e = await (await client.PostAsJsonAsync("/v1/auth/devices/enroll",
            new { platform = "android", publicKeySpki = spki })).Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = e.GetProperty("device_id").GetGuid();
        var challengeId = e.GetProperty("challenge_id").GetGuid();
        var nonce = e.GetProperty("nonce").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature = Sign(key, nonce) })).StatusCode);
        return (deviceId, key);
    }

    private async Task<string> IssueBrowserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ITrustedBrowserService>();
        return (await svc.IssueAsync(userId, new TrustedBrowserContext(Label: "Test"))).Token;
    }

    private static async Task<string?> ErrorOf(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out var e) ? e.GetString() : null;

    // ── tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_wrong_password_and_an_unknown_identifier_report_the_same_generic_failure()
    {
        await LoginAsync("9700000001");   // account exists but has no password yet
        var noPw = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = "919700000001", password = Pw });
        Assert.Equal(HttpStatusCode.Unauthorized, noPw.StatusCode);
        Assert.Equal("invalid_credentials", await ErrorOf(noPw));

        var unknown = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = "no-such-user-xyz", password = Pw });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("invalid_credentials", await ErrorOf(unknown));
    }

    /// <summary>D-280 changed this deliberately. A correct password with no trusted device used to be a dead
    /// end (<c>no_second_factor</c>) — an account with a password and no enrolled device could not log in at
    /// all, which on mobile is the common case rather than an edge case. It now offers the factors the
    /// account actually has.</summary>
    [Fact]
    public async Task A_correct_password_with_no_device_offers_the_factors_the_account_does_have()
    {
        var (client, _, id) = await LoginAsync("9700000002");
        await SetPasswordAsync(client);

        var res = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("second_factor", body.GetProperty("next").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("challenge_id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("poll_token").GetString()));

        // This account was created by phone OTP, so SMS is available and email is not (never verified).
        var methods = body.GetProperty("methods").EnumerateArray()
            .Select(m => m.GetProperty("method").GetString()).ToList();
        Assert.Contains("sms_otp", methods);
        Assert.DoesNotContain("email_otp", methods);
        Assert.DoesNotContain("trusted_device", methods);
    }

    /// <summary>The whole point of D-280: the offered code actually completes the login.</summary>
    [Fact]
    public async Task A_code_second_factor_completes_the_login_and_the_session_works()
    {
        const string phone = "9700000021";
        var (client, userId, id) = await LoginAsync(phone);
        await SetPasswordAsync(client);

        var start = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        var startBody = await start.Content.ReadFromJsonAsync<JsonElement>();
        var challengeId = startBody.GetProperty("challenge_id").GetGuid();
        var pollToken = startBody.GetProperty("poll_token").GetString();

        var send = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send",
            new { challengeId, pollToken, method = "sms_otp" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        var verify = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/verify",
            new { challengeId, pollToken, code = _factory.Phone.LastOtpFor(phone) });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var access = (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString();

        var me = _factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var meRes = await me.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        Assert.Equal(userId, (await meRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    /// <summary>The continuation token is what proves this caller passed the password step. Knowing a
    /// challenge id must not be enough — otherwise anyone who observed one could collect a session.</summary>
    [Fact]
    public async Task A_second_factor_challenge_cannot_be_driven_without_its_continuation_token()
    {
        const string phone = "9700000022";
        var (client, _, id) = await LoginAsync(phone);
        await SetPasswordAsync(client);

        var start = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        var challengeId = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("challenge_id").GetGuid();

        var send = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send",
            new { challengeId, pollToken = "not-the-real-token", method = "sms_otp" });
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
    }

    /// <summary>Verifies an email on an authenticated client, so the account can later be offered
    /// <c>email_otp</c> as a second factor (D-282).</summary>
    private async Task VerifyEmailAsync(HttpClient client, string email)
    {
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/v1/auth/email/verify/start", new { email })).StatusCode);
        var code = _factory.Email.LastOtpFor(email);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/v1/auth/email/verify/complete", new { email, code })).StatusCode);
    }

    /// <summary>Starts a password login and returns the challenge the client must drive.</summary>
    private async Task<(Guid ChallengeId, string PollToken, List<string> Methods)> StartSecondFactorAsync(string identifier)
    {
        var res = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier, password = Pw });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("second_factor", body.GetProperty("next").GetString());
        return (body.GetProperty("challenge_id").GetGuid(),
                body.GetProperty("poll_token").GetString()!,
                body.GetProperty("methods").EnumerateArray().Select(m => m.GetProperty("method").GetString()!).ToList());
    }

    /// <summary>F4 — the email second factor end to end. Previously only negative assertions existed, so a
    /// headline path of this release had never actually been executed.</summary>
    [Fact]
    public async Task Email_otp_second_factor_completes_the_login_and_the_session_works()
    {
        const string email = "second.factor@example.com";
        var (client, userId, id) = await LoginAsync("9700000031");
        await SetPasswordAsync(client);
        await VerifyEmailAsync(client, email);

        var (challengeId, pollToken, methods) = await StartSecondFactorAsync(id);
        Assert.Contains("email_otp", methods);

        var send = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send",
            new { challengeId, pollToken, method = "email_otp" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        var verify = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/verify",
            new { challengeId, pollToken, code = _factory.Email.LastOtpFor(email) });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var access = (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString();

        var me = _factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var meRes = await me.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        Assert.Equal(userId, (await meRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    /// <summary>F4 — cross-purpose isolation, direction 1. A code minted to *verify* an address must not
    /// authenticate a login: <c>EmailVerification</c> and <c>EmailLogin</c> are distinct purposes precisely
    /// so this cannot happen (D-282), and the purpose is part of the lookup key.</summary>
    [Fact]
    public async Task An_email_verification_code_cannot_be_redeemed_as_a_login_second_factor()
    {
        const string email = "crosspurpose.a@example.com";
        var (client, _, id) = await LoginAsync("9700000032");
        await SetPasswordAsync(client);
        await VerifyEmailAsync(client, email);

        // Mint a fresh *verification* code, then try to spend it on the login ceremony.
        //
        // Issued through the service rather than the endpoint deliberately: the endpoint enforces a 30s
        // per-destination resend spacing, and this address was verified moments ago, so a second HTTP start
        // would be refused for a reason that has nothing to do with what is under test. The cooldown is
        // covered by its own tests; here it is scaffolding.
        using (var scope = _factory.Services.CreateScope())
        {
            var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
            var issued = await otp.IssueAsync(email, OtpChannel.Email, OtpPurpose.EmailVerification,
                null, null, default, enforceResendCooldown: false);
            Assert.True(issued.Ok, $"scaffolding failed to mint a verification code: {issued.Error}");
        }
        var verificationCode = _factory.Email.LastOtpFor(email);

        var (challengeId, pollToken, _) = await StartSecondFactorAsync(id);
        await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send",
            new { challengeId, pollToken, method = "email_otp" });

        var attempt = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/verify",
            new { challengeId, pollToken, code = verificationCode });
        Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
    }

    /// <summary>F4 — cross-purpose isolation, direction 2. A login code must not satisfy address
    /// verification either; the isolation has to hold both ways or it is not isolation.</summary>
    [Fact]
    public async Task A_login_code_cannot_be_redeemed_as_an_email_verification()
    {
        const string email = "crosspurpose.b@example.com";
        var (client, _, id) = await LoginAsync("9700000033");
        await SetPasswordAsync(client);
        await VerifyEmailAsync(client, email);

        var (challengeId, pollToken, _) = await StartSecondFactorAsync(id);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send",
            new { challengeId, pollToken, method = "email_otp" })).StatusCode);
        var loginCode = _factory.Email.LastOtpFor(email);

        var attempt = await client.PostAsJsonAsync("/v1/auth/email/verify/complete",
            new { email, code = loginCode });
        Assert.NotEqual(HttpStatusCode.OK, attempt.StatusCode);
    }

    /// <summary>F5 — the code must belong to the challenge that asked for it. A code minted by the legacy
    /// passwordless login endpoint is genuinely this user's and genuinely valid, and must still be refused
    /// here: this step proves participation in *this* login, not mere possession of the phone.</summary>
    [Fact]
    public async Task A_code_from_another_ceremony_does_not_satisfy_the_second_factor()
    {
        const string phone = "9700000034";
        var (client, _, id) = await LoginAsync(phone);
        await SetPasswordAsync(client);

        var (challengeId, pollToken, _) = await StartSecondFactorAsync(id);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send",
            new { challengeId, pollToken, method = "sms_otp" })).StatusCode);

        // A different ceremony mints its own code to the same number, after this challenge's code.
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone })).StatusCode);
        var otherCeremonyCode = _factory.Phone.LastOtpFor(phone);

        var attempt = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/verify",
            new { challengeId, pollToken, code = otherCeremonyCode });
        Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
    }

    /// <summary>F5 — verification is refused outright when no code was ever requested for this challenge,
    /// rather than falling through to "any live code for this destination".</summary>
    [Fact]
    public async Task Verifying_without_first_requesting_a_code_is_refused()
    {
        const string phone = "9700000035";
        var (client, _, id) = await LoginAsync(phone);
        await SetPasswordAsync(client);
        var loginCode = _factory.Phone.LastOtpFor(phone);   // a live code exists for this number

        var (challengeId, pollToken, _) = await StartSecondFactorAsync(id);

        // No /second-factor/send for this challenge — so there is nothing bound, and the live code is not it.
        var attempt = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/verify",
            new { challengeId, pollToken, code = loginCode });
        Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
    }

    /// <summary>F3 — "remember this browser" must work on the OTP path exactly as it does on the device
    /// path. It was silently ignored here, which is a broken promise the user cannot detect.</summary>
    [Fact]
    public async Task Remember_browser_on_the_otp_path_sets_the_cookie()
    {
        const string phone = "9700000036";
        var (client, _, id) = await LoginAsync(phone);
        await SetPasswordAsync(client);

        var start = await _client.PostAsJsonAsync("/v1/auth/login/password",
            new { identifier = id, password = Pw, rememberBrowser = true });
        var body = await start.Content.ReadFromJsonAsync<JsonElement>();
        var challengeId = body.GetProperty("challenge_id").GetGuid();
        var pollToken = body.GetProperty("poll_token").GetString();

        await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send",
            new { challengeId, pollToken, method = "sms_otp" });
        var verify = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/verify",
            new { challengeId, pollToken, code = _factory.Phone.LastOtpFor(phone) });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Contains(verify.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith(TrustedBrowserEndpoints.CookieName, StringComparison.Ordinal));
    }

    /// <summary>F2 — the second-factor window must outlive the 5-minute OTP, or a user on a slow carrier
    /// holds a valid code the server will no longer accept.</summary>
    [Fact]
    public async Task The_second_factor_challenge_outlives_the_otp_it_waits_for()
    {
        var (client, _, id) = await LoginAsync("9700000037");
        await SetPasswordAsync(client);

        var res = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        var expiresAt = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expires_at").GetDateTime();

        var otpTtl = Kurx.Infrastructure.Auth.OtpService.Ttl;
        var window = expiresAt - DateTime.UtcNow;
        Assert.True(window > otpTtl,
            $"the challenge window ({window}) must exceed the OTP TTL ({otpTtl}); "
            + "otherwise the challenge dies while a valid code is still in flight");
    }

    /// <summary>A login code must never reach an address nobody proved they own (D-282), so an account with
    /// no verified email is not offered the method and cannot request it either.</summary>
    [Fact]
    public async Task Email_otp_is_not_offered_or_sendable_without_a_verified_address()
    {
        var (client, _, id) = await LoginAsync("9700000023");
        await SetPasswordAsync(client);

        var start = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        var body = await start.Content.ReadFromJsonAsync<JsonElement>();
        var methods = body.GetProperty("methods").EnumerateArray()
            .Select(m => m.GetProperty("method").GetString()).ToList();
        Assert.DoesNotContain("email_otp", methods);

        // Asking anyway is refused rather than silently downgraded to a channel the account does have.
        var send = await _client.PostAsJsonAsync("/v1/auth/login/second-factor/send", new
        {
            challengeId = body.GetProperty("challenge_id").GetGuid(),
            pollToken = body.GetProperty("poll_token").GetString(),
            method = "email_otp",
        });
        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
        Assert.Equal("method_unavailable", await ErrorOf(send));
    }

    [Fact]
    public async Task Password_plus_a_trusted_browser_cookie_issues_a_working_session()
    {
        var (client, userId, id) = await LoginAsync("9700000003");
        await SetPasswordAsync(client);
        var token = await IssueBrowserAsync(userId);

        var loginClient = _factory.CreateClient();
        loginClient.DefaultRequestHeaders.Add("Cookie", $"{TrustedBrowserEndpoints.CookieName}={token}");
        var res = await loginClient.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var access = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString();

        var me = _factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var meRes = await me.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        Assert.Equal(userId, (await meRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_wrong_cookie_does_not_satisfy_factor_two()
    {
        var (client, _, id) = await LoginAsync("9700000006");
        await SetPasswordAsync(client);

        var loginClient = _factory.CreateClient();
        loginClient.DefaultRequestHeaders.Add("Cookie", $"{TrustedBrowserEndpoints.CookieName}=not-a-real-token");
        var res = await loginClient.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });

        // The bogus cookie satisfies nothing, so the login is NOT completed: it falls through to the
        // second-factor step (D-280) instead of issuing a session. Asserting on the absence of tokens is the
        // load-bearing part — the cookie must never be mistaken for factor 2.
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("second_factor", body.GetProperty("next").GetString());
        Assert.False(body.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task A_locked_account_returns_423_regardless_of_the_second_factor()
    {
        var (client, userId, id) = await LoginAsync("9700000004");
        await SetPasswordAsync(client);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.UserCredentials.Where(c => c.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LockedUntil, DateTime.UtcNow.AddMinutes(10)));
        }
        var res = await _client.PostAsJsonAsync("/v1/auth/login/password", new { identifier = id, password = Pw });
        Assert.Equal(HttpStatusCode.Locked, res.StatusCode);
    }

    [Fact]
    public async Task Password_then_device_approval_issues_a_session_and_remember_trusts_the_browser()
    {
        var (client, userId, id) = await LoginAsync("9700000005");
        await SetPasswordAsync(client);
        var (deviceId, key) = await EnrollDeviceAsync(client);

        var browser = _factory.CreateClient();
        var start = await (await browser.PostAsJsonAsync("/v1/auth/login/password",
            new { identifier = id, password = Pw, rememberBrowser = true })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("device_approval", start.GetProperty("next").GetString());
        var challengeId = start.GetProperty("challenge_id").GetGuid();
        var pollToken = start.GetProperty("poll_token").GetString();

        var pending = await (await client.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var mine = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId);
        var nonce = mine.GetProperty("nonce").GetString()!;
        var matchNumber = mine.GetProperty("match_number").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature = SignMatched(key, nonce, matchNumber) })).StatusCode);

        var status = await (await browser.PostAsJsonAsync("/v1/auth/login/status", new { challengeId, pollToken }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("approved", status.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(status.GetProperty("access_token").GetString()));

        // "Remember this browser" → a trusted-browser row now exists for the user.
        using var scope = _factory.Services.CreateScope();
        var db2 = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db2.TrustedBrowsers.AnyAsync(b => b.UserId == userId && b.RevokedAt == null));
    }
}
