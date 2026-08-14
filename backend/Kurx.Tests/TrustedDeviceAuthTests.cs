using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Trusted-device authentication substrate (AM1/AM2/AM4, D-077..D-080): the hardened OTP
/// platform, device enrollment + lifecycle FSM, and push-approval login with its three security
/// properties (anti-enumeration, poll-token binding, single-issue). Additive surface — the legacy
/// OTP login covered by <see cref="AuthTests"/> is untouched.</summary>
public class TrustedDeviceAuthTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public TrustedDeviceAuthTests(KurxApiFactory factory)
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

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Legacy OTP login — the only way to mint a session today, so it bootstraps the
    /// authenticated clients that enroll devices.</summary>
    private async Task<(HttpClient Client, Guid UserId, string Phone)> LoginAsync(string phone)
    {
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());

        // Every test user gets a password at sign-up because password-first (D-182) is now the only way to
        // reach a device-approval login: the identifier-only `/v1/auth/login/start` was removed once no
        // client called it. Set before any device is enrolled — SetInitialAsync writes a credential and
        // does not cascade, but ordering it here keeps it clear that enrollment is never revoked by it.
        // Not asserted: a phone reused across tests already has one, and `password_already_set` is fine.
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = TestPassword });

        return (client, body.GetProperty("user_id").GetGuid(), "91" + phone);
    }

    /// <summary>Password is the only strong secret in these tests, so it must not collide with the policy
    /// check that rejects a password containing the account's own phone/username/email.</summary>
    private const string TestPassword = "Tr0ubador&Staple!Horse";

    /// <summary>Mints a device-approval login challenge the way the shipped clients do — factor 1 password,
    /// factor 2 the trusted device (D-182). The caller must already have enrolled a trusted device, which is
    /// what makes the backend answer <c>device_approval</c> rather than a session or a code list.
    /// Replaces the removed <c>/v1/auth/login/start</c>, which minted the same challenge from an identifier
    /// alone.</summary>
    private async Task<JsonElement> StartApprovalLoginAsync(string identifier)
    {
        var res = await _client.PostAsJsonAsync("/v1/auth/login/password",
            new { identifier, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("device_approval", body.GetProperty("next").GetString());
        return body;
    }

    private static (string Spki, ECDsa Key) NewDeviceKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), key);
    }

    private static string Sign(ECDsa key, string message) =>
        Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(message),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

    /// <summary>Signs a login or step-up challenge the way a real device must (Phase 3): the two-digit
    /// match number is part of the signed payload, so the signature attests to the digits the user
    /// confirmed and cannot be replayed against a challenge showing different ones. Enrollment has no
    /// match number and still signs the bare nonce.</summary>
    private static string SignMatched(ECDsa key, string nonce, int matchNumber) =>
        Sign(key, $"{nonce}.{matchNumber:00}");

    /// <summary>Step 1 of enrollment only: the device is registered but still PendingVerification.</summary>
    private static async Task<(Guid DeviceId, Guid ChallengeId, string Nonce, ECDsa Key)> BeginEnrollAsync(
        HttpClient client, string platform = "android")
    {
        var (spki, key) = NewDeviceKey();
        var res = await client.PostAsJsonAsync("/v1/auth/devices/enroll",
            new { name = "Test Device", platform, publicKeySpki = spki, alg = "ES256" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("device_id").GetGuid(), body.GetProperty("challenge_id").GetGuid(),
            body.GetProperty("nonce").GetString()!, key);
    }

    /// <summary>Both enrollment steps: the device proves possession and reaches Trusted.</summary>
    private static async Task<(Guid DeviceId, ECDsa Key)> EnrollTrustedAsync(HttpClient client)
    {
        var (deviceId, challengeId, nonce, key) = await BeginEnrollAsync(client);
        var verify = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature = Sign(key, nonce) });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return (deviceId, key);
    }

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider);
    }

    private Task<OtpIssueResult> IssueOtpAsync(string destination, OtpPurpose purpose,
        OtpChannel channel = OtpChannel.WhatsApp)
        => WithScopeAsync(sp => sp.GetRequiredService<IOtpService>()
            .IssueAsync(destination, channel, purpose, userId: null, requestIp: "127.0.0.1"));

    private Task<OtpVerifyResult> VerifyOtpAsync(string destination, OtpPurpose purpose, string code,
        OtpChannel channel = OtpChannel.WhatsApp)
        => WithScopeAsync(sp => sp.GetRequiredService<IOtpService>()
            .VerifyAsync(destination, channel, purpose, code));

    /// <summary>Shifts a destination's OTP rows back in time so cooldown/TTL windows can be crossed
    /// without sleeping in the test.</summary>
    private Task ShiftOtpClockAsync(string destination, TimeSpan by) => WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<KurxDbContext>();
        var rows = await db.OtpCodes.Where(o => o.Destination == destination).ToListAsync();
        foreach (var row in rows)
        {
            row.CreatedAt -= by;
            row.ExpiresAt -= by;
        }
        await db.SaveChangesAsync();
        return true;
    });

    // ══ AM1 — hardened OTP platform ══════════════════════════════════════════

    [Fact]
    public async Task Otp_issue_then_verify_succeeds_and_the_code_is_single_use()
    {
        const string dest = "+919100000001";
        Assert.True((await IssueOtpAsync(dest, OtpPurpose.Registration)).Ok);

        var code = _factory.WhatsApp.LastOtpFor(dest);
        var verified = await VerifyOtpAsync(dest, OtpPurpose.Registration, code);
        Assert.True(verified.Ok);
        Assert.NotNull(verified.OtpId);

        // Consumed: the same code cannot be replayed.
        var replay = await VerifyOtpAsync(dest, OtpPurpose.Registration, code);
        Assert.False(replay.Ok);
        Assert.Equal("otp_not_found", replay.Error);
    }

    [Fact]
    public async Task Otp_wrong_code_is_rejected_without_consuming_the_challenge()
    {
        const string dest = "+919100000002";
        await IssueOtpAsync(dest, OtpPurpose.Registration);
        var code = _factory.WhatsApp.LastOtpFor(dest);

        var wrong = await VerifyOtpAsync(dest, OtpPurpose.Registration, code == "111111" ? "222222" : "111111");
        Assert.False(wrong.Ok);
        Assert.Equal("invalid_code", wrong.Error);

        // The real code still works — a wrong guess burns an attempt, not the code.
        Assert.True((await VerifyOtpAsync(dest, OtpPurpose.Registration, code)).Ok);
    }

    [Fact]
    public async Task Otp_attempt_cap_locks_the_code_even_for_the_correct_value()
    {
        const string dest = "+919100000003";
        await IssueOtpAsync(dest, OtpPurpose.Registration);
        var code = _factory.WhatsApp.LastOtpFor(dest);
        var wrong = code == "111111" ? "222222" : "111111";

        for (var i = 0; i < 5; i++)
            Assert.Equal("invalid_code", (await VerifyOtpAsync(dest, OtpPurpose.Registration, wrong)).Error);

        var locked = await VerifyOtpAsync(dest, OtpPurpose.Registration, code);
        Assert.False(locked.Ok);
        Assert.Equal("too_many_attempts", locked.Error);
    }

    [Fact]
    public async Task Otp_resend_within_the_cooldown_is_refused_with_a_retry_after()
    {
        const string dest = "+919100000004";
        Assert.True((await IssueOtpAsync(dest, OtpPurpose.Registration)).Ok);

        var immediate = await IssueOtpAsync(dest, OtpPurpose.Registration);
        Assert.False(immediate.Ok);
        Assert.Equal("resend_cooldown", immediate.Error);
        Assert.InRange(immediate.RetryAfterSeconds ?? 0, 1, 30);

        // Past the cooldown, a resend is allowed again.
        await ShiftOtpClockAsync(dest, TimeSpan.FromSeconds(60));
        Assert.True((await IssueOtpAsync(dest, OtpPurpose.Registration)).Ok);
    }

    [Fact]
    public async Task Otp_fourth_issue_within_ten_minutes_is_rate_limited()
    {
        const string dest = "+919100000005";
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await IssueOtpAsync(dest, OtpPurpose.Registration)).Ok);
            // Clear the 30s resend cooldown while staying inside the 10-minute window.
            await ShiftOtpClockAsync(dest, TimeSpan.FromSeconds(60));
        }

        var limited = await IssueOtpAsync(dest, OtpPurpose.Registration);
        Assert.False(limited.Ok);
        Assert.Equal("rate_limited", limited.Error);
    }

    [Fact]
    public async Task Otp_past_its_ttl_no_longer_verifies()
    {
        const string dest = "+919100000006";
        await IssueOtpAsync(dest, OtpPurpose.Registration);
        var code = _factory.WhatsApp.LastOtpFor(dest);

        await ShiftOtpClockAsync(dest, TimeSpan.FromMinutes(10)); // TTL is 5 minutes
        var expired = await VerifyOtpAsync(dest, OtpPurpose.Registration, code);
        Assert.False(expired.Ok);
        Assert.Equal("otp_not_found", expired.Error);
    }

    [Fact]
    public async Task Otp_purposes_are_isolated_from_each_other()
    {
        const string dest = "+919100000007";
        await IssueOtpAsync(dest, OtpPurpose.Registration);
        var registrationCode = _factory.WhatsApp.LastOtpFor(dest);

        // A different purpose has its own cooldown and its own code — the registration code must not
        // satisfy an account-recovery challenge.
        Assert.True((await IssueOtpAsync(dest, OtpPurpose.AccountRecovery)).Ok);
        var wrongPurpose = await VerifyOtpAsync(dest, OtpPurpose.AccountRecovery, registrationCode);
        Assert.False(wrongPurpose.Ok);
        Assert.Equal("invalid_code", wrongPurpose.Error);
    }

    [Theory]
    [InlineData("9100000008", "invalid_phone")]     // not E.164 (ADR-A6 requires the leading '+')
    [InlineData("+91123", "invalid_phone")]         // below the E.164 length bound
    public async Task Otp_rejects_non_e164_destinations(string destination, string expectedError)
    {
        var result = await IssueOtpAsync(destination, OtpPurpose.Registration);
        Assert.False(result.Ok);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task Otp_rejects_a_malformed_email_destination()
    {
        var result = await IssueOtpAsync("not-an-email", OtpPurpose.EmailVerification, OtpChannel.Email);
        Assert.False(result.Ok);
        Assert.Equal("invalid_email", result.Error);
    }

    [Fact]
    public async Task Otp_issue_and_verify_are_audited_in_security_events()
    {
        const string dest = "+919100000009";
        await IssueOtpAsync(dest, OtpPurpose.Registration);
        var code = _factory.WhatsApp.LastOtpFor(dest);
        Assert.True((await VerifyOtpAsync(dest, OtpPurpose.Registration, code)).Ok);

        var types = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().SecurityEvents
            .AsNoTracking().Where(e => e.Type.StartsWith("otp.")).Select(e => e.Type).Distinct().ToListAsync());
        Assert.Contains("otp.issued", types);
        Assert.Contains("otp.verified", types);
    }

    // ══ AM2 — device enrollment + lifecycle FSM ══════════════════════════════

    [Fact]
    public async Task Device_endpoints_require_authentication()
    {
        var enroll = await _client.PostAsJsonAsync("/v1/auth/devices/enroll",
            new { platform = "android", publicKeySpki = "AAAA" });
        Assert.Equal(HttpStatusCode.Unauthorized, enroll.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/v1/auth/devices")).StatusCode);
    }

    [Fact]
    public async Task Device_enrollment_reaches_trusted_only_after_a_valid_signature()
    {
        var (client, _, _) = await LoginAsync("9100000101");
        var (deviceId, challengeId, nonce, key) = await BeginEnrollAsync(client);

        // Before proof of possession the device is not yet trusted.
        var pending = await (await client.GetAsync("/v1/auth/devices")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingVerification",
            pending.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == deviceId)
                .GetProperty("state").GetString());

        var verify = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature = Sign(key, nonce) });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var trusted = await (await client.GetAsync("/v1/auth/devices")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Trusted",
            trusted.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == deviceId)
                .GetProperty("state").GetString());
    }

    [Fact]
    public async Task Device_enrollment_with_a_signature_from_the_wrong_key_is_rejected()
    {
        var (client, _, _) = await LoginAsync("9100000102");
        var (deviceId, challengeId, nonce, _) = await BeginEnrollAsync(client);

        var (_, attackerKey) = NewDeviceKey();   // holds no private key matching the enrolled SPKI
        var res = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature = Sign(attackerKey, nonce) });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_signature",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        // The device stays untrusted.
        var devices = await (await client.GetAsync("/v1/auth/devices")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingVerification",
            devices.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == deviceId)
                .GetProperty("state").GetString());
    }

    [Fact]
    public async Task Device_enrollment_with_garbage_signature_bytes_fails_closed()
    {
        var (client, _, _) = await LoginAsync("9100000103");
        var (deviceId, challengeId, _, _) = await BeginEnrollAsync(client);

        var res = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature = "not-base64-at-all!!" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_signature",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Device_enrollment_challenge_is_single_use()
    {
        var (client, _, _) = await LoginAsync("9100000104");
        var (deviceId, challengeId, nonce, key) = await BeginEnrollAsync(client);
        var signature = Sign(key, nonce);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature })).StatusCode);

        // Replaying the identical signed challenge cannot re-run the transition.
        var replay = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("invalid_state",
            (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Device_enrollment_after_the_challenge_expires_is_rejected()
    {
        var (client, _, _) = await LoginAsync("9100000105");
        var (deviceId, challengeId, nonce, key) = await BeginEnrollAsync(client);

        await WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<KurxDbContext>();
            var challenge = await db.AuthChallenges.FirstAsync(c => c.Id == challengeId);
            challenge.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            return await db.SaveChangesAsync();
        });

        var res = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature = Sign(key, nonce) });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("challenge_expired",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_user_cannot_verify_or_revoke_another_users_device()
    {
        var (owner, _, _) = await LoginAsync("9100000106");
        var (attacker, _, _) = await LoginAsync("9100000107");
        var (deviceId, challengeId, nonce, key) = await BeginEnrollAsync(owner);

        // Hidden resource → 404, never 403 (D-018).
        var verify = await attacker.PostAsJsonAsync("/v1/auth/devices/enroll/verify",
            new { challengeId, deviceId, signature = Sign(key, nonce) });
        Assert.Equal(HttpStatusCode.NotFound, verify.StatusCode);

        var revoke = await attacker.PostAsJsonAsync($"/v1/auth/devices/{deviceId}/revoke", new { });
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);

        // The attacker's own device list never contains it.
        var devices = await (await attacker.GetAsync("/v1/auth/devices")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(devices.EnumerateArray(), d => d.GetProperty("id").GetGuid() == deviceId);
    }

    [Fact]
    public async Task Revoking_a_device_moves_it_to_revoked_and_revokes_its_credentials()
    {
        var (client, _, _) = await LoginAsync("9100000108");
        var (deviceId, _) = await EnrollTrustedAsync(client);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/v1/auth/devices/{deviceId}/revoke", new { })).StatusCode);

        var devices = await (await client.GetAsync("/v1/auth/devices")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Revoked",
            devices.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == deviceId)
                .GetProperty("state").GetString());

        var liveCredentials = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>()
            .DeviceCredentials.AsNoTracking()
            .CountAsync(c => c.TrustedDeviceId == deviceId && c.RevokedAt == null));
        Assert.Equal(0, liveCredentials);
    }

    [Fact]
    public async Task Revoking_an_unknown_device_is_a_404()
    {
        var (client, _, _) = await LoginAsync("9100000109");
        var res = await client.PostAsJsonAsync($"/v1/auth/devices/{Guid.NewGuid()}/revoke", new { });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ══ AM4 — push-approval login ════════════════════════════════════════════

    /// <summary>Drives the whole passwordless flow: web starts a login, the trusted device signs the
    /// pushed nonce, and the waiting web client exchanges its poll token for a device-bound session.</summary>
    [Fact]
    public async Task Push_approval_login_issues_a_working_device_bound_session()
    {
        var (device, userId, phone) = await LoginAsync("9100000201");
        var (deviceId, key) = await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(phone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();
        var pollToken = started.GetProperty("poll_token").GetString()!;

        // Nothing is issued until the device acts.
        var beforeApproval = await _client.PostAsJsonAsync("/v1/auth/login/status", new { challengeId, pollToken });
        Assert.Equal("pending",
            (await beforeApproval.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // The device app fetches the challenge it must sign.
        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var mine = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId);
        Assert.Equal(started.GetProperty("match_number").GetInt32(), mine.GetProperty("match_number").GetInt32());

        var matchNumber = mine.GetProperty("match_number").GetInt32();
        var approve = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber,
                signature = SignMatched(key, mine.GetProperty("nonce").GetString()!, matchNumber) });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var approved = await (await _client.PostAsJsonAsync("/v1/auth/login/status", new { challengeId, pollToken }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("approved", approved.GetProperty("status").GetString());
        Assert.Equal(userId, approved.GetProperty("user_id").GetGuid());

        // The minted access token is a real session, and it is bound to the approving device.
        var web = _factory.CreateClient();
        web.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", approved.GetProperty("access_token").GetString());
        var me = await web.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(userId, (await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        var session = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthSessions
            .AsNoTracking().SingleAsync(s => s.UserId == userId));
        Assert.Equal(deviceId, session.TrustedDeviceId);
    }

    // Security property 1 — anti-enumeration — no longer belongs here. It was asserted against the
    // identifier-only `/v1/auth/login/start`, which returned a decoy challenge so that an unknown
    // identifier and a real device-less account looked alike. That route is gone, and password-first has
    // no equivalent to shape: an unknown identifier and a wrong password both return one generic
    // `invalid_credentials`, which PasswordLoginTests covers directly. A device-less account that *passes*
    // the password is deliberately answered with `second_factor` (D-280) — telling a caller who already
    // proved factor 1 that their own account exists is not enumeration.

    /// <summary>Security property 2 — poll-token binding: knowing the challenge id is not enough to
    /// collect the tokens.</summary>
    [Fact]
    public async Task Login_status_with_the_wrong_poll_token_never_yields_tokens()
    {
        var (device, _, phone) = await LoginAsync("9100000203");
        var (deviceId, key) = await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(phone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();

        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var nonce = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId)
            .GetProperty("nonce").GetString()!;
        var matchNumber = started.GetProperty("match_number").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature = SignMatched(key, nonce, matchNumber) })).StatusCode);

        // Approved, but the thief holds the wrong poll token: indistinguishable from "still pending".
        var thief = await (await _client.PostAsJsonAsync("/v1/auth/login/status",
            new { challengeId, pollToken = "not-the-real-poll-token" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", thief.GetProperty("status").GetString());
        Assert.False(thief.TryGetProperty("access_token", out _));

        // The legitimate poller is unaffected and still collects the session.
        var real = await (await _client.PostAsJsonAsync("/v1/auth/login/status",
                new { challengeId, pollToken = started.GetProperty("poll_token").GetString() }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("approved", real.GetProperty("status").GetString());
    }

    /// <summary>Security property 3 — single issue: tokens are minted exactly once per approval.</summary>
    [Fact]
    public async Task Login_tokens_are_minted_once_and_a_second_poll_is_consumed()
    {
        var (device, userId, phone) = await LoginAsync("9100000204");
        var (deviceId, key) = await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(phone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();
        var pollToken = started.GetProperty("poll_token").GetString()!;

        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var nonce = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId)
            .GetProperty("nonce").GetString()!;
        var matchNumber = started.GetProperty("match_number").GetInt32();
        await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature = SignMatched(key, nonce, matchNumber) });

        var first = await (await _client.PostAsJsonAsync("/v1/auth/login/status", new { challengeId, pollToken }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("approved", first.GetProperty("status").GetString());

        var second = await (await _client.PostAsJsonAsync("/v1/auth/login/status", new { challengeId, pollToken }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("consumed", second.GetProperty("status").GetString());
        Assert.False(second.TryGetProperty("access_token", out _));

        // Exactly one session and one refresh token exist for this approval.
        var counts = await WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<KurxDbContext>();
            return (Sessions: await db.AuthSessions.CountAsync(s => s.UserId == userId),
                Refresh: await db.RefreshTokens.CountAsync(r => r.UserId == userId && r.SessionId != null));
        });
        Assert.Equal(1, counts.Sessions);
        Assert.Equal(1, counts.Refresh);
    }

    [Fact]
    public async Task Login_approval_signature_cannot_be_replayed()
    {
        var (device, _, phone) = await LoginAsync("9100000205");
        var (deviceId, key) = await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(phone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();
        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var matchNumber = started.GetProperty("match_number").GetInt32();
        var signature = SignMatched(key, pending.EnumerateArray()
            .Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId).GetProperty("nonce").GetString()!,
            matchNumber);

        Assert.Equal(HttpStatusCode.OK, (await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature })).StatusCode);

        var replay = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("challenge_consumed",
            (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Login_cannot_be_approved_by_an_untrusted_or_revoked_device()
    {
        var (device, _, phone) = await LoginAsync("9100000206");
        var (trustedId, trustedKey) = await EnrollTrustedAsync(device);
        var (pendingId, _, _, pendingKey) = await BeginEnrollAsync(device);   // never proved possession

        var started = await StartApprovalLoginAsync(phone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();
        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var nonce = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId)
            .GetProperty("nonce").GetString()!;

        var byPending = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId = pendingId, signature = Sign(pendingKey, nonce) });
        Assert.Equal(HttpStatusCode.BadRequest, byPending.StatusCode);
        Assert.Equal("device_not_trusted",
            (await byPending.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        // Same once the previously-trusted device is revoked.
        await device.PostAsJsonAsync($"/v1/auth/devices/{trustedId}/revoke", new { });
        var byRevoked = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId = trustedId, signature = Sign(trustedKey, nonce) });
        Assert.Equal(HttpStatusCode.BadRequest, byRevoked.StatusCode);
        Assert.Equal("device_not_trusted",
            (await byRevoked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_user_cannot_see_or_approve_another_users_login_challenge()
    {
        var (victim, _, victimPhone) = await LoginAsync("9100000207");
        await EnrollTrustedAsync(victim);
        var (attacker, _, _) = await LoginAsync("9100000208");
        var (attackerDeviceId, attackerKey) = await EnrollTrustedAsync(attacker);

        var started = await StartApprovalLoginAsync(victimPhone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();

        // The victim's challenge never appears in the attacker's pending list.
        var pending = await (await attacker.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(pending.EnumerateArray(), c => c.GetProperty("challenge_id").GetGuid() == challengeId);

        // Approving it with the attacker's own trusted device is a 404, not a 403.
        var nonce = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().Where(c => c.Id == challengeId).Select(c => c.Nonce).SingleAsync());
        var approve = await attacker.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId = attackerDeviceId, signature = Sign(attackerKey, nonce) });
        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await attacker.PostAsJsonAsync("/v1/auth/login/reject", new { challengeId })).StatusCode);
    }

    [Fact]
    public async Task Rejecting_a_login_stops_it_from_ever_issuing_tokens()
    {
        var (device, _, phone) = await LoginAsync("9100000209");
        var (deviceId, key) = await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(phone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();
        var pollToken = started.GetProperty("poll_token").GetString()!;

        Assert.Equal(HttpStatusCode.OK,
            (await device.PostAsJsonAsync("/v1/auth/login/reject", new { challengeId })).StatusCode);

        var status = await (await _client.PostAsJsonAsync("/v1/auth/login/status", new { challengeId, pollToken }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rejected", status.GetProperty("status").GetString());

        // A late signature cannot resurrect a rejected challenge.
        var nonce = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().Where(c => c.Id == challengeId).Select(c => c.Nonce).SingleAsync());
        var lateApprove = await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, signature = Sign(key, nonce) });
        Assert.Equal(HttpStatusCode.BadRequest, lateApprove.StatusCode);
        Assert.Equal("challenge_consumed",
            (await lateApprove.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task An_expired_login_challenge_reports_expired_and_issues_nothing()
    {
        var (device, _, phone) = await LoginAsync("9100000210");
        await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(phone);
        var challengeId = started.GetProperty("challenge_id").GetGuid();

        await WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<KurxDbContext>();
            var challenge = await db.AuthChallenges.FirstAsync(c => c.Id == challengeId);
            challenge.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            return await db.SaveChangesAsync();
        });

        var status = await (await _client.PostAsJsonAsync("/v1/auth/login/status",
                new { challengeId, pollToken = started.GetProperty("poll_token").GetString() }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("expired", status.GetProperty("status").GetString());
        Assert.False(status.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task Login_pending_approve_and_reject_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/v1/auth/login/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId = Guid.NewGuid(), deviceId = Guid.NewGuid(), signature = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/v1/auth/login/reject",
            new { challengeId = Guid.NewGuid() })).StatusCode);
    }

    // ══ AM5 — device-bound sessions, PoP refresh, family revocation ══════════

    private record DeviceSession(HttpClient Device, Guid DeviceId, ECDsa Key, Guid UserId, string RefreshToken);

    /// <summary>Runs the full push-approval login and returns the device-bound session it minted.</summary>
    private async Task<DeviceSession> DeviceLoginAsync(string phone)
    {
        var (device, userId, identifier) = await LoginAsync(phone);
        var (deviceId, key) = await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(identifier);
        var challengeId = started.GetProperty("challenge_id").GetGuid();
        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var nonce = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId)
            .GetProperty("nonce").GetString()!;
        var matchNumber = started.GetProperty("match_number").GetInt32();
        await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature = SignMatched(key, nonce, matchNumber) });

        var approved = await (await _client.PostAsJsonAsync("/v1/auth/login/status",
                new { challengeId, pollToken = started.GetProperty("poll_token").GetString() }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("approved", approved.GetProperty("status").GetString());
        return new DeviceSession(device, deviceId, key, userId, approved.GetProperty("refresh_token").GetString()!);
    }

    [Fact]
    public async Task A_device_bound_refresh_token_is_useless_without_the_device_signature()
    {
        var s = await DeviceLoginAsync("9100000301");

        // A stolen refresh string alone cannot rotate — this is the whole point of sender-constraining.
        var bare = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = s.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, bare.StatusCode);
        Assert.Equal("proof_required",
            (await bare.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        // Nor with a signature from some other key.
        var (_, attackerKey) = NewDeviceKey();
        var forged = await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(attackerKey, s.RefreshToken) });
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);

        // A failed proof must not revoke: the legitimate device still rotates successfully.
        var real = await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(s.Key, s.RefreshToken) });
        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
        var rotated = await real.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(s.RefreshToken, rotated.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task A_rotated_device_token_stays_sender_constrained_to_the_same_device()
    {
        var s = await DeviceLoginAsync("9100000302");

        var rotated = await (await _client.PostAsJsonAsync("/v1/auth/refresh",
                new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(s.Key, s.RefreshToken) }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var next = rotated.GetProperty("refresh_token").GetString()!;

        // The constraint survives rotation rather than decaying to a bearer token.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = next })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = next, deviceId = s.DeviceId, signature = Sign(s.Key, next) })).StatusCode);
    }

    [Fact]
    public async Task Legacy_bearer_refresh_tokens_still_rotate_without_a_proof()
    {
        // D-081 is additive: an OTP-login token carries no session, so it keeps the old bearer contract.
        var tokens = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "9100000304" });
        Assert.Equal(HttpStatusCode.OK, tokens.StatusCode);
        var code = _factory.WhatsApp.LastOtpFor("9100000304");
        var verify = await (await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone = "9100000304", code }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var refreshed = await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = verify.GetProperty("refresh_token").GetString() });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task Reuse_of_a_device_bound_token_revokes_only_that_session_family()
    {
        var s = await DeviceLoginAsync("9100000305");

        // The same user also holds a legacy OTP session, which must survive the blast (D-081).
        var legacy = await (await _client.PostAsJsonAsync("/v1/auth/otp/verify", new
        {
            phone = "9100000305",
            code = await IssueLegacyOtpAsync("9100000305"),
        })).Content.ReadFromJsonAsync<JsonElement>();
        var legacyRefresh = legacy.GetProperty("refresh_token").GetString();

        // Rotate once, then replay the spent token — classic theft signal.
        await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(s.Key, s.RefreshToken) });
        var reuse = await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(s.Key, s.RefreshToken) });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The device session is dead...
        var session = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthSessions
            .AsNoTracking().SingleAsync(x => x.UserId == s.UserId));
        Assert.NotNull(session.RevokedAt);
        Assert.Equal("reuse_detected", session.RevokeReason);

        // ...but the unrelated legacy session is untouched, which revoke-all would have killed.
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = legacyRefresh })).StatusCode);
    }

    /// <summary>Issues a legacy OTP for a phone that is already past its resend cooldown window.</summary>
    private async Task<string> IssueLegacyOtpAsync(string phone)
    {
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        return _factory.WhatsApp.LastOtpFor(phone);
    }

    [Fact]
    public async Task Your_devices_lists_the_live_session_with_its_device()
    {
        var s = await DeviceLoginAsync("9100000306");

        var sessions = await (await s.Device.GetAsync("/v1/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>();
        var row = sessions.EnumerateArray().Single();
        Assert.Equal(s.DeviceId, row.GetProperty("device_id").GetGuid());
        Assert.Equal("Test Device", row.GetProperty("device_name").GetString());
        Assert.Equal("android", row.GetProperty("platform").GetString());
    }

    [Fact]
    public async Task Revoking_a_session_remotely_kills_its_refresh_chain()
    {
        var s = await DeviceLoginAsync("9100000307");
        var sessions = await (await s.Device.GetAsync("/v1/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = sessions.EnumerateArray().Single().GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK,
            (await s.Device.PostAsJsonAsync($"/v1/auth/sessions/{sessionId}/revoke", new { })).StatusCode);

        // Even with a perfectly valid device proof, a revoked session cannot refresh.
        var afterRevoke = await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(s.Key, s.RefreshToken) });
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);

        Assert.Empty((await (await s.Device.GetAsync("/v1/auth/sessions"))
            .Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Revoking_a_device_signs_out_the_sessions_it_holds()
    {
        var s = await DeviceLoginAsync("9100000308");

        Assert.Equal(HttpStatusCode.OK,
            (await s.Device.PostAsJsonAsync($"/v1/auth/devices/{s.DeviceId}/revoke", new { })).StatusCode);

        // "Lost my phone" must actually sign the phone out, not just block re-enrollment.
        var refresh = await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(s.Key, s.RefreshToken) });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        var session = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthSessions
            .AsNoTracking().SingleAsync(x => x.UserId == s.UserId));
        Assert.Equal("device_revoked", session.RevokeReason);
    }

    [Fact]
    public async Task A_user_cannot_revoke_another_users_session()
    {
        var victim = await DeviceLoginAsync("9100000309");
        var (attacker, _, _) = await LoginAsync("9100000310");

        var sessions = await (await victim.Device.GetAsync("/v1/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = sessions.EnumerateArray().Single().GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await attacker.PostAsJsonAsync($"/v1/auth/sessions/{sessionId}/revoke", new { })).StatusCode);
        Assert.Empty((await (await attacker.GetAsync("/v1/auth/sessions"))
            .Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());

        // The victim's session is still alive.
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = victim.RefreshToken, deviceId = victim.DeviceId, signature = Sign(victim.Key, victim.RefreshToken) })).StatusCode);
    }

    [Fact]
    public async Task Revoking_an_unknown_or_already_revoked_session_is_handled()
    {
        var s = await DeviceLoginAsync("9100000311");
        Assert.Equal(HttpStatusCode.NotFound,
            (await s.Device.PostAsJsonAsync($"/v1/auth/sessions/{Guid.NewGuid()}/revoke", new { })).StatusCode);

        var sessions = await (await s.Device.GetAsync("/v1/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = sessions.EnumerateArray().Single().GetProperty("id").GetGuid();
        await s.Device.PostAsJsonAsync($"/v1/auth/sessions/{sessionId}/revoke", new { });

        var again = await s.Device.PostAsJsonAsync($"/v1/auth/sessions/{sessionId}/revoke", new { });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("already_revoked",
            (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Session_endpoints_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/v1/auth/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync($"/v1/auth/sessions/{Guid.NewGuid()}/revoke", new { })).StatusCode);
    }

    // ══ AM6 — step-up / assurance level ══════════════════════════════════════

    /// <summary>Runs a full step-up for an already-enrolled device.</summary>
    private static async Task<HttpResponseMessage> StepUpAsync(HttpClient client, Guid deviceId, ECDsa key)
    {
        var start = await client.PostAsJsonAsync("/v1/auth/step-up/start", new { action = "test" });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var body = await start.Content.ReadFromJsonAsync<JsonElement>();
        var matchNumber = body.GetProperty("match_number").GetInt32();
        return await client.PostAsJsonAsync("/v1/auth/step-up/verify", new
        {
            challengeId = body.GetProperty("challenge_id").GetGuid(),
            deviceId,
            matchNumber,
            signature = SignMatched(key, body.GetProperty("nonce").GetString()!, matchNumber),
        });
    }

    [Fact]
    public async Task Step_up_is_satisfied_only_after_a_valid_device_signature()
    {
        var (client, _, _) = await LoginAsync("9100000401");
        var (deviceId, key) = await EnrollTrustedAsync(client);

        var before = await (await client.GetAsync("/v1/auth/step-up/status")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(before.GetProperty("satisfied").GetBoolean());
        Assert.True(before.GetProperty("can_step_up").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await StepUpAsync(client, deviceId, key)).StatusCode);

        var after = await (await client.GetAsync("/v1/auth/step-up/status")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(after.GetProperty("satisfied").GetBoolean());
        Assert.NotNull(after.GetProperty("valid_until").GetString());
    }

    [Fact]
    public async Task Step_up_with_a_wrong_signature_does_not_satisfy_it()
    {
        var (client, _, _) = await LoginAsync("9100000402");
        var (deviceId, _) = await EnrollTrustedAsync(client);
        var (_, attackerKey) = NewDeviceKey();

        var start = await (await client.PostAsJsonAsync("/v1/auth/step-up/start", new { action = "test" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var matchNumber = start.GetProperty("match_number").GetInt32();
        var verify = await client.PostAsJsonAsync("/v1/auth/step-up/verify", new
        {
            challengeId = start.GetProperty("challenge_id").GetGuid(),
            deviceId,
            matchNumber,
            signature = SignMatched(attackerKey, start.GetProperty("nonce").GetString()!, matchNumber),
        });
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        Assert.Equal("invalid_signature",
            (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        var status = await (await client.GetAsync("/v1/auth/step-up/status")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(status.GetProperty("satisfied").GetBoolean());
    }

    [Fact]
    public async Task Step_up_is_impossible_without_a_trusted_device()
    {
        var (client, _, _) = await LoginAsync("9100000403");

        var start = await client.PostAsJsonAsync("/v1/auth/step-up/start", new { action = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Equal("no_trusted_device",
            (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        var status = await (await client.GetAsync("/v1/auth/step-up/status")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(status.GetProperty("can_step_up").GetBoolean());
    }

    [Fact]
    public async Task A_login_challenge_cannot_be_redeemed_as_a_step_up()
    {
        var s = await DeviceLoginAsync("9100000404");

        // Issue a fresh login challenge and try to pass it off as step-up proof.
        var started = await StartApprovalLoginAsync("919100000404");
        var challengeId = started.GetProperty("challenge_id").GetGuid();
        var nonce = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().Where(c => c.Id == challengeId).Select(c => c.Nonce).SingleAsync());

        var verify = await s.Device.PostAsJsonAsync("/v1/auth/step-up/verify",
            new { challengeId, deviceId = s.DeviceId, signature = Sign(s.Key, nonce) });
        Assert.Equal(HttpStatusCode.NotFound, verify.StatusCode);

        var status = await (await s.Device.GetAsync("/v1/auth/step-up/status")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(status.GetProperty("satisfied").GetBoolean());
    }

    [Fact]
    public async Task A_user_cannot_complete_another_users_step_up()
    {
        var (victim, _, _) = await LoginAsync("9100000405");
        var (victimDevice, victimKey) = await EnrollTrustedAsync(victim);
        var (attacker, _, _) = await LoginAsync("9100000406");
        await EnrollTrustedAsync(attacker);

        var start = await (await victim.PostAsJsonAsync("/v1/auth/step-up/start", new { action = "test" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var hijack = await attacker.PostAsJsonAsync("/v1/auth/step-up/verify", new
        {
            challengeId = start.GetProperty("challenge_id").GetGuid(),
            deviceId = victimDevice,
            signature = Sign(victimKey, start.GetProperty("nonce").GetString()!),
        });
        Assert.Equal(HttpStatusCode.NotFound, hijack.StatusCode);
    }

    [Fact]
    public async Task Step_up_endpoints_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/v1/auth/step-up/start", new { action = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/v1/auth/step-up/status")).StatusCode);
    }

    // ══ AM7 — account recovery codes ═════════════════════════════════════════

    private async Task<string[]> GenerateRecoveryCodesAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/v1/auth/recovery-codes", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes")
            .EnumerateArray().Select(c => c.GetString()!).ToArray();
    }

    /// <summary>Requests the recovery OTP and reads the code the user would have received.</summary>
    private async Task<string> RecoveryOtpAsync(string identifier, string e164)
    {
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync("/v1/auth/recovery/start", new { identifier })).StatusCode);
        return _factory.WhatsApp.LastOtpFor(e164);
    }

    [Fact]
    public async Task Recovery_codes_are_issued_once_and_counted()
    {
        var (client, _, _) = await LoginAsync("9100000501");
        var codes = await GenerateRecoveryCodesAsync(client);
        Assert.Equal(10, codes.Length);
        Assert.Equal(10, codes.Distinct().Count());

        var remaining = await (await client.GetAsync("/v1/auth/recovery-codes")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10, remaining.GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task Regenerating_recovery_codes_retires_the_previous_sheet()
    {
        var (client, _, _) = await LoginAsync("9100000502");
        var old = await GenerateRecoveryCodesAsync(client);
        await GenerateRecoveryCodesAsync(client);

        // A printout the user believes they replaced must not still open the account.
        var otp = await RecoveryOtpAsync("919100000502", "+919100000502");
        var redeem = await _client.PostAsJsonAsync("/v1/auth/recovery/redeem",
            new { identifier = "919100000502", otpCode = otp, recoveryCode = old[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, redeem.StatusCode);
    }

    [Fact]
    public async Task Recovery_needs_both_the_code_and_the_otp()
    {
        var (client, _, _) = await LoginAsync("9100000503");
        var codes = await GenerateRecoveryCodesAsync(client);
        const string identifier = "919100000503";

        // A leaked code sheet alone is not a login.
        var otp = await RecoveryOtpAsync(identifier, "+919100000503");
        var wrongOtp = await _client.PostAsJsonAsync("/v1/auth/recovery/redeem",
            new { identifier, otpCode = otp == "111111" ? "222222" : "111111", recoveryCode = codes[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongOtp.StatusCode);
        Assert.Equal("invalid_recovery",
            (await wrongOtp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        // Nor is the OTP alone — the same opaque error either way, so it never says which half was wrong.
        var freshOtp = await RecoveryOtpAsync(identifier, "+919100000503");
        var wrongCode = await _client.PostAsJsonAsync("/v1/auth/recovery/redeem",
            new { identifier, otpCode = freshOtp, recoveryCode = "deadbeef-deadbeef" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCode.StatusCode);
        Assert.Equal("invalid_recovery",
            (await wrongCode.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Recovery_signs_the_user_in_burns_the_code_and_cuts_every_old_session()
    {
        var s = await DeviceLoginAsync("9100000504");
        const string identifier = "919100000504";

        // With a trusted device present, minting codes is a step-up action (AM6).
        var blocked = await s.Device.PostAsJsonAsync("/v1/auth/recovery-codes", new { });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("step_up_required",
            (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        Assert.Equal(HttpStatusCode.OK, (await StepUpAsync(s.Device, s.DeviceId, s.Key)).StatusCode);
        var codes = await GenerateRecoveryCodesAsync(s.Device);

        var otp = await RecoveryOtpAsync(identifier, "+919100000504");
        var redeem = await _client.PostAsJsonAsync("/v1/auth/recovery/redeem",
            new { identifier, otpCode = otp, recoveryCode = codes[0] });
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        var recovered = await redeem.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(s.UserId, recovered.GetProperty("user_id").GetGuid());

        // The recovered session works...
        var web = _factory.CreateClient();
        web.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", recovered.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await web.GetAsync("/v1/me")).StatusCode);

        // ...the old device-bound session is dead, and the old device must re-enroll.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = s.RefreshToken, deviceId = s.DeviceId, signature = Sign(s.Key, s.RefreshToken) })).StatusCode);
        var device = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().TrustedDevices
            .AsNoTracking().SingleAsync(d => d.Id == s.DeviceId));
        Assert.Equal(DeviceLifecycleState.Suspended, device.LifecycleState);

        // The code is burned.
        var reuse = await _client.PostAsJsonAsync("/v1/auth/recovery/redeem",
            new { identifier, otpCode = await RecoveryOtpAsync(identifier, "+919100000504"), recoveryCode = codes[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Recovery_start_never_reveals_whether_an_account_exists()
    {
        var unknown = await _client.PostAsJsonAsync("/v1/auth/recovery/start", new { identifier = "919100009998" });
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.True((await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Recovery_code_endpoints_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/v1/auth/recovery-codes", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/v1/auth/recovery-codes")).StatusCode);
    }

    // ══ AM8 — outbox dispatch + risk engine ══════════════════════════════════

    [Fact]
    public async Task Security_critical_events_are_written_to_the_outbox_and_dispatched()
    {
        var s = await DeviceLoginAsync("9100000601");
        await s.Device.PostAsJsonAsync($"/v1/auth/devices/{s.DeviceId}/revoke", new { });

        // PayloadJson is jsonb, so it cannot be LIKE-matched in SQL — filter client-side.
        var mine = await OutboxForUserAsync(s.UserId);
        Assert.Contains("login.approved", mine.Select(m => m.Type));
        Assert.Contains("device.revoked", mine.Select(m => m.Type));

        // The dispatcher drains them at-least-once.
        await WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<OutboxDispatchJob>().RunAsync();
            return true;
        });

        var dispatched = await OutboxForUserAsync(s.UserId);
        Assert.All(dispatched, m => Assert.Equal(OutboxStatus.Dispatched, m.Status));
        Assert.All(dispatched, m => Assert.NotNull(m.DispatchedAt));
        // The idempotency key is what makes an at-least-once redelivery safe downstream.
        Assert.Equal(dispatched.Count, dispatched.Select(m => m.IdempotencyKey).Distinct().Count());
    }

    private async Task<List<OutboxMessage>> OutboxForUserAsync(Guid userId)
    {
        var all = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().OutboxMessages
            .AsNoTracking().ToListAsync());
        return all.Where(m => m.PayloadJson.Contains(userId.ToString())).ToList();
    }

    [Fact]
    public async Task The_risk_engine_denies_a_login_after_repeated_auth_failures()
    {
        var (client, userId, phone) = await LoginAsync("9100000602");
        await EnrollTrustedAsync(client);

        // A healthy account gets a real challenge.
        var healthy = await StartApprovalLoginAsync(phone);
        Assert.True(await ChallengeExistsAsync(healthy.GetProperty("challenge_id").GetGuid()));

        // Now the account looks under attack: a replayed refresh chain plus repeated bad signatures.
        await WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<KurxDbContext>();
            db.SecurityEvents.Add(new SecurityEvent { UserId = userId, Type = "refresh.reuse_detected", Severity = "critical" });
            for (var i = 0; i < 3; i++)
                db.SecurityEvents.Add(new SecurityEvent { UserId = userId, Type = "challenge.signature_invalid", Severity = "warning" });
            return await db.SaveChangesAsync();
        });

        // Password-first has no decoy to return, and needs none: a risk denial reports the same generic
        // failure as a wrong password or an unknown account, which is a stronger anti-enumeration answer
        // than a fabricated challenge. The property that must still hold is that a denied account gets no
        // challenge at all — the gate has to run before anything is minted, not after.
        var before = await LoginChallengeCountAsync(userId);
        var denied = await _client.PostAsJsonAsync("/v1/auth/login/password",
            new { identifier = phone, password = TestPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("invalid_credentials",
            (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Equal(before, await LoginChallengeCountAsync(userId));
    }

    private Task<int> LoginChallengeCountAsync(Guid userId)
        => WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().CountAsync(c => c.Purpose == AuthChallengePurpose.Login && c.UserId == userId));

    private Task<bool> ChallengeExistsAsync(Guid challengeId)
        => WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().AnyAsync(c => c.Id == challengeId));

    // ══ AM3 — WebAuthn / passkeys ════════════════════════════════════════════

    private record PasskeyCeremony(Guid ChallengeId, string Challenge);

    private static async Task<PasskeyCeremony> ReadCeremonyAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return new PasskeyCeremony(
            body.GetProperty("challenge_id").GetGuid(),
            body.GetProperty("options").GetProperty("challenge").GetString()!);
    }

    /// <summary>Registers a passkey for an authenticated client via the real ceremony.</summary>
    private static async Task<SoftwareAuthenticator> RegisterPasskeyAsync(HttpClient client, string? name = "My Passkey")
    {
        var authenticator = new SoftwareAuthenticator();
        var ceremony = await ReadCeremonyAsync(await client.PostAsJsonAsync("/v1/auth/passkeys/register/options", new { }));
        var register = await client.PostAsJsonAsync("/v1/auth/passkeys/register", new
        {
            challengeId = ceremony.ChallengeId,
            response = authenticator.AttestationResponse(ceremony.Challenge),
            deviceName = name,
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        return authenticator;
    }

    private async Task<HttpResponseMessage> PasskeyLoginAsync(SoftwareAuthenticator authenticator, string identifier, Guid userId)
    {
        var ceremony = await ReadCeremonyAsync(
            await _client.PostAsJsonAsync("/v1/auth/passkeys/login/options", new { identifier }));
        return await _client.PostAsJsonAsync("/v1/auth/passkeys/login", new
        {
            challengeId = ceremony.ChallengeId,
            response = authenticator.AssertionResponse(ceremony.Challenge, userId),
        });
    }

    [Fact]
    public async Task A_passkey_can_be_registered_and_then_used_to_sign_in()
    {
        var (client, userId, identifier) = await LoginAsync("9100000701");
        var authenticator = await RegisterPasskeyAsync(client);

        // It enrolls as a trusted device, so everything built on that aggregate applies to it.
        var passkeys = await (await client.GetAsync("/v1/auth/passkeys")).Content.ReadFromJsonAsync<JsonElement>();
        var registered = passkeys.EnumerateArray().Single();
        Assert.Equal("My Passkey", registered.GetProperty("name").GetString());
        Assert.Equal("Trusted", registered.GetProperty("state").GetString());

        var login = await PasskeyLoginAsync(authenticator, identifier, userId);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, tokens.GetProperty("user_id").GetGuid());

        // The minted session is real and device-bound.
        var web = _factory.CreateClient();
        web.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await web.GetAsync("/v1/me")).StatusCode);

        var session = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthSessions
            .AsNoTracking().SingleAsync(s => s.UserId == userId));
        Assert.NotNull(session.TrustedDeviceId);

        // Documented AM3 limitation (D-086): a WebAuthn authenticator cannot sign an arbitrary refresh
        // token, so a passkey session is device-bound but NOT sender-constrained. Pinned so that if the
        // behavior ever changes, it changes deliberately.
        var refresh = await _client.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = tokens.GetProperty("refresh_token").GetString() });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Passkey_sign_in_increments_the_signature_counter()
    {
        var (client, userId, identifier) = await LoginAsync("9100000702");
        var authenticator = await RegisterPasskeyAsync(client);

        Assert.Equal(HttpStatusCode.OK, (await PasskeyLoginAsync(authenticator, identifier, userId)).StatusCode);
        var afterFirst = await StoredCounterAsync(userId);
        Assert.Equal(HttpStatusCode.OK, (await PasskeyLoginAsync(authenticator, identifier, userId)).StatusCode);
        var afterSecond = await StoredCounterAsync(userId);

        // Persisting the counter is what makes clone detection possible at all.
        Assert.True(afterSecond > afterFirst, $"counter did not advance: {afterFirst} → {afterSecond}");
    }

    [Fact]
    public async Task A_cloned_authenticator_with_a_rewound_counter_is_rejected()
    {
        var (client, userId, identifier) = await LoginAsync("9100000703");
        var authenticator = await RegisterPasskeyAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await PasskeyLoginAsync(authenticator, identifier, userId)).StatusCode);

        // A cloned key replays an older counter value — the signal WebAuthn exists to catch.
        authenticator.ForceSignCount(0);
        var cloned = await PasskeyLoginAsync(authenticator, identifier, userId);
        Assert.Equal(HttpStatusCode.Unauthorized, cloned.StatusCode);
    }

    [Fact]
    public async Task A_passkey_assertion_signed_by_the_wrong_key_is_rejected()
    {
        var (client, userId, identifier) = await LoginAsync("9100000704");
        await RegisterPasskeyAsync(client);

        // Same credential id shape, different key pair: the signature must not verify.
        var impostor = new SoftwareAuthenticator();
        var login = await PasskeyLoginAsync(impostor, identifier, userId);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task A_passkey_registration_challenge_is_single_use_and_owner_scoped()
    {
        var (owner, _, _) = await LoginAsync("9100000705");
        var (attacker, _, _) = await LoginAsync("9100000706");

        var authenticator = new SoftwareAuthenticator();
        var ceremony = await ReadCeremonyAsync(await owner.PostAsJsonAsync("/v1/auth/passkeys/register/options", new { }));
        var attestation = authenticator.AttestationResponse(ceremony.Challenge);

        // Another user cannot complete someone else's ceremony — 404, not 403 (D-018).
        var hijack = await attacker.PostAsJsonAsync("/v1/auth/passkeys/register",
            new { challengeId = ceremony.ChallengeId, response = attestation, deviceName = "x" });
        Assert.Equal(HttpStatusCode.NotFound, hijack.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/v1/auth/passkeys/register",
            new { challengeId = ceremony.ChallengeId, response = attestation, deviceName = "x" })).StatusCode);

        // Replaying the completed ceremony is refused.
        var replay = await owner.PostAsJsonAsync("/v1/auth/passkeys/register",
            new { challengeId = ceremony.ChallengeId, response = attestation, deviceName = "x" });
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
    }

    [Fact]
    public async Task A_malformed_attestation_is_refused_and_burns_the_ceremony()
    {
        var (client, _, _) = await LoginAsync("9100000707");
        var ceremony = await ReadCeremonyAsync(await client.PostAsJsonAsync("/v1/auth/passkeys/register/options", new { }));

        var garbage = JsonDocument.Parse("""{"id":"AAAA","rawId":"AAAA","type":"public-key","response":{"attestationObject":"AAAA","clientDataJSON":"AAAA"}}""").RootElement;
        var res = await client.PostAsJsonAsync("/v1/auth/passkeys/register",
            new { challengeId = ceremony.ChallengeId, response = garbage, deviceName = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_attestation",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        Assert.Empty((await (await client.GetAsync("/v1/auth/passkeys"))
            .Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task Passkey_login_options_never_reveal_whether_an_account_exists()
    {
        var (_, deviceLessUserId, deviceLessIdentifier) = await LoginAsync("9100000708");

        foreach (var identifier in new[] { "919100009997", deviceLessIdentifier })
        {
            var res = await _client.PostAsJsonAsync("/v1/auth/passkeys/login/options", new { identifier });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<JsonElement>();

            // Well-formed options either way — just nothing that can satisfy them.
            Assert.NotEqual(Guid.Empty, body.GetProperty("challenge_id").GetGuid());
            Assert.False(string.IsNullOrEmpty(body.GetProperty("options").GetProperty("challenge").GetString()));
            Assert.False(await ChallengeExistsAsync(body.GetProperty("challenge_id").GetGuid()));
        }

        var persisted = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().CountAsync(c => c.UserId == deviceLessUserId));
        Assert.Equal(0, persisted);
    }

    [Fact]
    public async Task A_revoked_passkey_can_no_longer_sign_in()
    {
        var (client, userId, identifier) = await LoginAsync("9100000709");
        var authenticator = await RegisterPasskeyAsync(client);

        var passkeys = await (await client.GetAsync("/v1/auth/passkeys")).Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = passkeys.EnumerateArray().Single().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/v1/auth/devices/{deviceId}/revoke", new { })).StatusCode);

        // Revocation goes through the shared trusted-device lifecycle, so it covers this rail too.
        var login = await PasskeyLoginAsync(authenticator, identifier, userId);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Passkey_login_with_an_unknown_challenge_is_rejected()
    {
        var (client, userId, _) = await LoginAsync("9100000710");
        var authenticator = await RegisterPasskeyAsync(client);

        var login = await _client.PostAsJsonAsync("/v1/auth/passkeys/login", new
        {
            challengeId = Guid.NewGuid(),
            response = authenticator.AssertionResponse(SoftwareAuthenticator.Base64Url(new byte[32]), userId),
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    // ══ D-098 — the two credential rails must not share challenges ═══════════

    [Fact]
    public async Task A_passkey_login_ceremony_never_appears_as_an_approvable_push_request()
    {
        var (device, userId, identifier) = await LoginAsync("9100000801");
        await EnrollTrustedAsync(device);                       // gives the user a trusted device
        await RegisterPasskeyAsync(device);

        // Start a passkey sign-in. Its challenge lives in the same table as push-approval logins.
        var ceremony = await ReadCeremonyAsync(
            await _client.PostAsJsonAsync("/v1/auth/passkeys/login/options", new { identifier }));

        // It must NOT surface in the device-approval list: a trusted device approving it would
        // consume the challenge and kill the user's in-flight passkey sign-in.
        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(pending.EnumerateArray(),
            c => c.GetProperty("challenge_id").GetGuid() == ceremony.ChallengeId);

        var purpose = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().Where(c => c.Id == ceremony.ChallengeId).Select(c => c.Purpose).SingleAsync());
        Assert.Equal(AuthChallengePurpose.PasskeyLogin, purpose);
    }

    [Fact]
    public async Task A_device_key_enrollment_challenge_cannot_be_spent_on_the_passkey_rail()
    {
        var (client, _, _) = await LoginAsync("9100000802");
        var (_, deviceChallengeId, _, _) = await BeginEnrollAsync(client);

        // Feeding a device-key challenge to the passkey verifier previously reached
        // CredentialCreateOptions.FromJson on non-WebAuthn context JSON and threw an unhandled
        // JsonException (a 500). It must be a clean refusal.
        var authenticator = new SoftwareAuthenticator();
        var res = await client.PostAsJsonAsync("/v1/auth/passkeys/register", new
        {
            challengeId = deviceChallengeId,
            response = authenticator.AttestationResponse(SoftwareAuthenticator.Base64Url(new byte[32])),
            deviceName = "x",
        });

        Assert.NotEqual(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_passkey_registration_challenge_cannot_complete_a_device_key_enrollment()
    {
        var (client, _, _) = await LoginAsync("9100000803");

        // A pending device-key device whose private key we control.
        var (pendingDeviceId, _, _, pendingKey) = await BeginEnrollAsync(client);

        // A passkey registration ceremony for the same user.
        var ceremony = await ReadCeremonyAsync(
            await client.PostAsJsonAsync("/v1/auth/passkeys/register/options", new { }));
        var nonce = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().Where(c => c.Id == ceremony.ChallengeId).Select(c => c.Nonce).SingleAsync());

        // Signing the passkey ceremony's nonce must not trust the device-key device.
        var res = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify", new
        {
            challengeId = ceremony.ChallengeId,
            deviceId = pendingDeviceId,
            signature = Sign(pendingKey, nonce),
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("challenge_not_found",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        var devices = await (await client.GetAsync("/v1/auth/devices")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingVerification",
            devices.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == pendingDeviceId)
                .GetProperty("state").GetString());
    }

    [Fact]
    public async Task Passkey_management_endpoints_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/v1/auth/passkeys")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/v1/auth/passkeys/register/options", new { })).StatusCode);
    }

    // ══ D-088 — challenge purpose binding ════════════════════════════════════

    [Fact]
    public async Task A_login_challenge_cannot_be_spent_completing_a_device_enrollment()
    {
        var (client, _, identifier) = await LoginAsync("9100000901");
        await EnrollTrustedAsync(client);                       // gives the user a trusted device

        // A second device begins enrolling, so it is PendingVerification and holds a key we control.
        var (pendingDeviceId, _, _, pendingKey) = await BeginEnrollAsync(client);

        // Meanwhile a login challenge is outstanding for the same user.
        var started = await StartApprovalLoginAsync(identifier);
        var loginChallengeId = started.GetProperty("challenge_id").GetGuid();
        var loginNonce = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().Where(c => c.Id == loginChallengeId).Select(c => c.Nonce).SingleAsync());

        // Signing the login nonce must not complete the enrollment: purpose is bound centrally.
        var res = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify", new
        {
            challengeId = loginChallengeId,
            deviceId = pendingDeviceId,
            signature = Sign(pendingKey, loginNonce),
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("challenge_not_found",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        // The device stayed untrusted and the login challenge was not consumed.
        var devices = await (await client.GetAsync("/v1/auth/devices")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingVerification",
            devices.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == pendingDeviceId)
                .GetProperty("state").GetString());
        var stillPending = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().AuthChallenges
            .AsNoTracking().Where(c => c.Id == loginChallengeId).Select(c => c.Status).SingleAsync());
        Assert.Equal(AuthChallengeStatus.Pending, stillPending);
    }

    // ══ D-089 — canonical E.164 phone identity ═══════════════════════════════

    [Fact]
    public async Task Registration_dual_writes_the_canonical_phone_representations()
    {
        var (_, userId, _) = await LoginAsync("9100000902");

        var user = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().Users
            .AsNoTracking().SingleAsync(u => u.Id == userId));
        Assert.Equal("919100000902", user.Phone);           // legacy column untouched
        Assert.Equal("+919100000902", user.PhoneE164);      // canonical alongside it
        Assert.Equal("IN", user.CountryCode);
        Assert.False(string.IsNullOrWhiteSpace(user.PhoneNational));
    }

    [Fact]
    public async Task A_phone_change_keeps_both_representations_in_step()
    {
        var (client, userId, _) = await LoginAsync("9100000903");

        const string newPhone = "9100000904";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = newPhone });
        var code = _factory.WhatsApp.LastOtpFor(newPhone);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/v1/me/phone/verify", new { phone = newPhone, code })).StatusCode);

        var user = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().Users
            .AsNoTracking().SingleAsync(u => u.Id == userId));
        Assert.Equal("919100000904", user.Phone);
        Assert.Equal("+919100000904", user.PhoneE164);      // dual-write, never drifting apart
    }

    [Fact]
    public async Task Authentication_dual_reads_a_row_that_has_not_been_backfilled_yet()
    {
        var (device, userId, _) = await LoginAsync("9100000905");
        await EnrollTrustedAsync(device);

        // Simulate a legacy row the backfill has not reached: canonical columns null, legacy intact.
        await WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<KurxDbContext>();
            var u = await db.Users.SingleAsync(x => x.Id == userId);
            u.PhoneE164 = null; u.CountryCode = null; u.PhoneNational = null;
            return await db.SaveChangesAsync();
        });

        // Login still resolves, via the legacy fallback — this is what makes the migration zero-downtime.
        var started = await StartApprovalLoginAsync("919100000905");
        Assert.True(await ChallengeExistsAsync(started.GetProperty("challenge_id").GetGuid()));
    }

    [Fact]
    public async Task Authentication_resolves_the_same_account_from_any_phone_format()
    {
        var (device, _, _) = await LoginAsync("9100000906");
        await EnrollTrustedAsync(device);

        // E.164, bare digits, and a spaced international form must all reach the same account.
        foreach (var identifier in new[] { "+919100000906", "919100000906", "+91 91000 00906" })
        {
            var started = await StartApprovalLoginAsync(identifier);
            Assert.True(await ChallengeExistsAsync(started.GetProperty("challenge_id").GetGuid()),
                $"identifier {identifier} did not resolve to the account");
        }
    }

    [Fact]
    public async Task The_backfill_converts_legacy_rows_and_refuses_to_guess_at_bad_ones()
    {
        var (_, goodUserId, _) = await LoginAsync("9100000907");
        var brokenUserId = Guid.NewGuid();

        await WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<KurxDbContext>();
            var good = await db.Users.SingleAsync(u => u.Id == goodUserId);
            good.PhoneE164 = null; good.CountryCode = null; good.PhoneNational = null;
            // A legacy row that cannot be a real number in any region.
            db.Users.Add(new User { Id = brokenUserId, Phone = "000000000000", Name = "" });
            return await db.SaveChangesAsync();
        });

        var report = await WithScopeAsync(sp => sp.GetRequiredService<PhoneE164BackfillJob>().BackfillAsync());

        var rows = await WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().Users
            .AsNoTracking().Where(u => u.Id == goodUserId || u.Id == brokenUserId)
            .Select(u => new { u.Id, u.Phone, u.PhoneE164, u.CountryCode }).ToListAsync());

        var converted = rows.Single(r => r.Id == goodUserId);
        Assert.Equal("+919100000907", converted.PhoneE164);
        Assert.Equal("IN", converted.CountryCode);

        // The unconvertible row is left exactly as it was, never guessed at.
        var untouched = rows.Single(r => r.Id == brokenUserId);
        Assert.Null(untouched.PhoneE164);
        Assert.Equal("000000000000", untouched.Phone);
        Assert.True(report.Failed >= 1, "the unconvertible row should be reported as a failure");
    }

    [Fact]
    public async Task The_backfill_is_idempotent()
    {
        await LoginAsync("9100000908");
        var first = await WithScopeAsync(sp => sp.GetRequiredService<PhoneE164BackfillJob>().BackfillAsync());
        var second = await WithScopeAsync(sp => sp.GetRequiredService<PhoneE164BackfillJob>().BackfillAsync());

        // Re-running must not re-convert anything already done, so a partial run can safely resume.
        Assert.Equal(0, second.Converted);
        Assert.True(second.Scanned <= first.Scanned);
    }

    [Theory]
    [InlineData("+14155552671", "US")]      // never assume +91
    [InlineData("+442071838750", "GB")]
    [InlineData("+81312345678", "JP")]
    public void The_canonicalizer_supports_every_country_not_just_india(string e164, string expectedRegion)
    {
        Assert.True(PhoneCanonicalizer.TryParse(e164, region: null, out var parsed));
        Assert.Equal(e164, parsed.E164);
        Assert.Equal(expectedRegion, parsed.CountryCode);
    }

    [Fact]
    public void A_national_number_without_a_region_is_refused_rather_than_guessed()
    {
        // The old "10 digits ⇒ India" assumption is exactly what this replaces: with no region there is
        // no honest answer, and guessing routes an OTP to a stranger in another country.
        Assert.False(PhoneCanonicalizer.TryParse("9876543210", region: null, out _));

        // With a region stated, it parses — and to that region, not India.
        Assert.True(PhoneCanonicalizer.TryParse("4155552671", "US", out var us));
        Assert.Equal("+14155552671", us.E164);
        Assert.Equal("US", us.CountryCode);
    }

    [Fact]
    public void An_invalid_number_is_rejected_even_with_a_valid_region()
    {
        Assert.False(PhoneCanonicalizer.TryParse("12345", "IN", out _));
        Assert.False(PhoneCanonicalizer.TryParse("", "IN", out _));
        Assert.False(PhoneCanonicalizer.TryParse("not-a-number", "IN", out _));
    }

    // ══ AM9 — realtime login status ══════════════════════════════════════════

    private HubConnection LoginHubConnection()
        => new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/login"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

    [Fact]
    public async Task The_waiting_client_is_pushed_the_login_status_without_polling()
    {
        var (device, _, identifier) = await LoginAsync("9100000801");
        var (deviceId, key) = await EnrollTrustedAsync(device);

        var started = await StartApprovalLoginAsync(identifier);
        var challengeId = started.GetProperty("challenge_id").GetGuid();

        // The login hub is deliberately anonymous — the browser has no session yet.
        await using var connection = LoginHubConnection();
        await connection.StartAsync();

        var pushed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("login_status", payload =>
            pushed.TrySetResult(payload.GetProperty("status").GetString()!));
        await connection.InvokeAsync("Watch", challengeId, started.GetProperty("poll_token").GetString());

        var pending = await (await device.GetAsync("/v1/auth/login/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var mine = pending.EnumerateArray().Single(c => c.GetProperty("challenge_id").GetGuid() == challengeId);
        var nonce = mine.GetProperty("nonce").GetString()!;
        var matchNumber = mine.GetProperty("match_number").GetInt32();
        // Login approval now requires the two-digit match number, signed into the payload (Phase 3,
        // Amendment A/C). Signing the bare nonce with no match number is rejected, so the approval must
        // carry it for the "approved" status to reach the waiting client.
        await device.PostAsJsonAsync("/v1/auth/login/approve",
            new { challengeId, deviceId, matchNumber, signature = SignMatched(key, nonce, matchNumber) });

        var status = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("approved", status);
    }

    [Fact]
    public async Task Watching_a_login_requires_the_poll_token()
    {
        var (device, _, identifier) = await LoginAsync("9100000802");
        await EnrollTrustedAsync(device);
        var started = await StartApprovalLoginAsync(identifier);
        var challengeId = started.GetProperty("challenge_id").GetGuid();

        await using var connection = LoginHubConnection();
        await connection.StartAsync();

        // Knowing the challenge id is not enough to subscribe — same rule as collecting tokens (D-080).
        var ex = await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("Watch", challengeId, "not-the-poll-token"));
        Assert.Contains("not_authorized", ex.Message);

        // The real poll token is accepted.
        await connection.InvokeAsync("Watch", challengeId, started.GetProperty("poll_token").GetString());
    }

    [Fact]
    public async Task A_rejected_login_is_pushed_to_the_waiting_client()
    {
        var (device, _, identifier) = await LoginAsync("9100000803");
        await EnrollTrustedAsync(device);
        var started = await StartApprovalLoginAsync(identifier);
        var challengeId = started.GetProperty("challenge_id").GetGuid();

        await using var connection = LoginHubConnection();
        await connection.StartAsync();
        var pushed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("login_status", payload =>
            pushed.TrySetResult(payload.GetProperty("status").GetString()!));
        await connection.InvokeAsync("Watch", challengeId, started.GetProperty("poll_token").GetString());

        await device.PostAsJsonAsync("/v1/auth/login/reject", new { challengeId });

        Assert.Equal("rejected", await pushed.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private Task<long> StoredCounterAsync(Guid userId)
        => WithScopeAsync(sp => sp.GetRequiredService<KurxDbContext>().DeviceCredentials
            .AsNoTracking()
            .Where(c => c.CredentialType == DeviceCredentialType.WebAuthn
                        && sp.GetRequiredService<KurxDbContext>().TrustedDevices
                            .Any(d => d.Id == c.TrustedDeviceId && d.UserId == userId))
            .Select(c => c.SignatureCounter)
            .FirstAsync());
}
