using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>D-330 — the reset second factor is an approval bound to <b>one</b> reset, not an ambient
/// "this account stepped up recently".
///
/// <para>The scenario these exist to close: an attacker who has SIM-swapped the number receives the reset
/// OTP. Under the old rule the second factor was <c>StepUpService.StatusAsync(userId).Satisfied</c> — a
/// fact about the <i>account</i>, true whenever the victim had done anything sensitive in the preceding
/// 300 s, and readable by an anonymous caller. The attacker did not need to possess anything the victim
/// had; they only needed to arrive while the victim was active.</para>
///
/// <para>These tests also pin the assurance ladder that must survive the fix: OTP login is a shortcut to a
/// low-assurance session, ordinary reads work on it, and only sensitive actions demand a step-up.</para></summary>
public class PasswordResetApprovalBindingTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _anon;

    public PasswordResetApprovalBindingTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
        _anon = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string NewPassword = "an entirely different passphrase";

    // ── harness ─────────────────────────────────────────────────────────────

    private static (string Spki, ECDsa Key) NewDeviceKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), key);
    }

    private static string Sign(ECDsa key, string message) =>
        Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(message),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

    private static string SignMatched(ECDsa key, string nonce, int matchNumber) => Sign(key, $"{nonce}.{matchNumber:00}");

    /// <summary>The "shortcut login": an OTP to the registered number buys a session with no step-up.</summary>
    private async Task<(HttpClient Client, Guid UserId, string Identifier, string Phone)> ShortcutLoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var v = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, v.StatusCode);
        var body = await v.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        return (client, body.GetProperty("user_id").GetGuid(), "91" + phone, phone);
    }

    private static async Task<(Guid DeviceId, ECDsa Key)> EnrollTrustedDeviceAsync(HttpClient client)
    {
        var (spki, key) = NewDeviceKey();
        var res = await client.PostAsJsonAsync("/v1/auth/devices/enroll",
            new { name = "Test Device", platform = "android", publicKeySpki = spki, alg = "ES256" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var deviceId = body.GetProperty("device_id").GetGuid();
        var verify = await client.PostAsJsonAsync("/v1/auth/devices/enroll/verify", new
        {
            challengeId = body.GetProperty("challenge_id").GetGuid(),
            deviceId,
            signature = Sign(key, body.GetProperty("nonce").GetString()!),
        });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return (deviceId, key);
    }

    private record Started(Guid ApprovalId, int MatchNumber, string ResetToken);

    private async Task<Started> StartResetAsync(string identifier)
    {
        var res = await _anon.PostAsJsonAsync("/v1/auth/password/reset/start", new { identifier });
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"start failed: {res.StatusCode} {raw}");
        var body = JsonDocument.Parse(raw).RootElement;
        foreach (var key in new[] { "approval_id", "match_number", "reset_token" })
            Assert.True(body.TryGetProperty(key, out _), $"missing '{key}' in start response: {raw}");
        return new Started(body.GetProperty("approval_id").GetGuid(), body.GetProperty("match_number").GetInt32(),
            body.GetProperty("reset_token").GetString()!);
    }

    private string ResetOtp(string phone) => _factory.WhatsApp.LastOtpFor(phone);

    /// <summary>Approves a pending reset from the signed-in device, reading the nonce from /pending — the
    /// same two calls a real trusted device makes.</summary>
    private static async Task<HttpResponseMessage> ApproveAsync(HttpClient device, Guid approvalId, Guid deviceId,
        ECDsa key, int matchNumber)
    {
        var pending = await device.GetFromJsonAsync<JsonElement>("/v1/auth/password/reset/pending");
        var row = pending.EnumerateArray().Single(x => x.GetProperty("approval_id").GetGuid() == approvalId);
        return await device.PostAsJsonAsync("/v1/auth/password/reset/approve", new
        {
            approvalId,
            deviceId,
            signature = SignMatched(key, row.GetProperty("nonce").GetString()!, matchNumber),
            matchNumber,
        });
    }

    private Task<HttpResponseMessage> CompleteAsync(string identifier, string otp, Started? approval = null,
        string? recoveryCode = null, Guid? approvalIdOverride = null, string? resetTokenOverride = null)
        => _anon.PostAsJsonAsync("/v1/auth/password/reset/complete", new
        {
            identifier,
            otpCode = otp,
            newPassword = NewPassword,
            recoveryCode,
            approvalId = approvalIdOverride ?? approval?.ApprovalId,
            resetToken = resetTokenOverride ?? approval?.ResetToken,
        });

    private static async Task<string?> ErrorOf(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out var e) ? e.GetString() : null;

    private async Task<T> WithDbAsync<T>(Func<KurxDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<KurxDbContext>());
    }

    // ── 1-3: the assurance ladder the fix must not disturb ───────────────────

    [Fact]
    public async Task Shortcut_login_issues_a_session_without_demanding_a_step_up()
    {
        var (client, userId, _, _) = await ShortcutLoginAsync("9700000101");

        Assert.NotEqual(Guid.Empty, userId);
        // No step-up was performed, and none was demanded to get here.
        var status = await client.GetFromJsonAsync<JsonElement>("/v1/auth/step-up/status");
        Assert.False(status.GetProperty("satisfied").GetBoolean());
    }

    [Fact]
    public async Task Ordinary_reads_work_on_a_shortcut_login_session()
    {
        var (client, _, _, _) = await ShortcutLoginAsync("9700000102");

        var me = await client.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_sensitive_action_still_demands_a_step_up()
    {
        var (client, _, _, _) = await ShortcutLoginAsync("9700000103");
        await EnrollTrustedDeviceAsync(client);      // the guard exempts users who cannot step up at all

        // Minting recovery codes is step-up gated (AM6) — a shortcut session alone must not print new
        // ways back into the account.
        var mint = await client.PostAsJsonAsync("/v1/auth/recovery-codes", new { });
        Assert.Equal("step_up_required", await ErrorOf(mint));
    }

    // ── 4-8: the binding ─────────────────────────────────────────────────────

    [Fact]
    public async Task An_approval_minted_for_one_account_cannot_reset_another()
    {
        // Victim starts a reset and approves it on their own device.
        var (victimDevice, _, victimId, victimPhone) = await ShortcutLoginAsync("9700000104");
        var (victimDeviceId, victimKey) = await EnrollTrustedDeviceAsync(victimDevice);
        var victimReset = await StartResetAsync(victimId);
        Assert.Equal(HttpStatusCode.OK,
            (await ApproveAsync(victimDevice, victimReset.ApprovalId, victimDeviceId, victimKey, victimReset.MatchNumber)).StatusCode);

        // The attacker holds account B's OTP and tries to spend account A's approval.
        var (_, _, attackerId, attackerPhone) = await ShortcutLoginAsync("9700000105");
        await StartResetAsync(attackerId);

        var stolen = await CompleteAsync(attackerId, ResetOtp(attackerPhone), victimReset);
        Assert.Equal("second_factor_required", await ErrorOf(stolen));

        // And the victim's approval is still unspent — the failed attempt did not burn it.
        var status = await WithDbAsync(db => db.AuthChallenges.AsNoTracking()
            .Where(c => c.Id == victimReset.ApprovalId).Select(c => c.Status).FirstAsync());
        Assert.Equal(AuthChallengeStatus.Approved, status);
        Assert.NotEqual(string.Empty, victimPhone);
    }

    [Fact]
    public async Task A_recent_unrelated_step_up_does_not_satisfy_a_reset()
    {
        // This is the SIM-swap scenario in full. The user is signed in and performs a genuine step-up on
        // their trusted device, exactly as they would when visiting the Security Center.
        var (client, userId, identifier, phone) = await ShortcutLoginAsync("9700000106");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(client);

        var start = await client.PostAsJsonAsync("/v1/auth/step-up/start", new { action = "recovery-codes" });
        var challenge = await start.Content.ReadFromJsonAsync<JsonElement>();
        var verify = await client.PostAsJsonAsync("/v1/auth/step-up/verify", new
        {
            challengeId = challenge.GetProperty("challenge_id").GetGuid(),
            deviceId,
            signature = SignMatched(key, challenge.GetProperty("nonce").GetString()!, challenge.GetProperty("match_number").GetInt32()),
            matchNumber = challenge.GetProperty("match_number").GetInt32(),
        });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var satisfied = await client.GetFromJsonAsync<JsonElement>("/v1/auth/step-up/status");
        Assert.True(satisfied.GetProperty("satisfied").GetBoolean());   // the ambient fact the old rule read

        // The attacker, holding only the OTP, starts a reset and supplies no approval. Under the old rule
        // the satisfied step-up above would have carried this request. It must not.
        await StartResetAsync(identifier);
        var hijack = await CompleteAsync(identifier, ResetOtp(phone));
        Assert.Equal("second_factor_required", await ErrorOf(hijack));
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task A_step_up_approval_is_not_redeemable_as_a_reset_approval()
    {
        // Purpose binding (D-088): the two ceremonies must not share currency even for the same account.
        var (client, _, identifier, phone) = await ShortcutLoginAsync("9700000107");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(client);

        var start = await client.PostAsJsonAsync("/v1/auth/step-up/start", new { action = "x" });
        var challenge = await start.Content.ReadFromJsonAsync<JsonElement>();
        var stepUpId = challenge.GetProperty("challenge_id").GetGuid();
        await client.PostAsJsonAsync("/v1/auth/step-up/verify", new
        {
            challengeId = stepUpId,
            deviceId,
            signature = SignMatched(key, challenge.GetProperty("nonce").GetString()!, challenge.GetProperty("match_number").GetInt32()),
            matchNumber = challenge.GetProperty("match_number").GetInt32(),
        });

        var started = await StartResetAsync(identifier);
        // Presented with this ceremony's own token, so only the purpose mismatch can refuse it.
        var res = await CompleteAsync(identifier, ResetOtp(phone),
            approvalIdOverride: stepUpId, resetTokenOverride: started.ResetToken);
        Assert.Equal("second_factor_required", await ErrorOf(res));
    }

    [Fact]
    public async Task An_approval_cannot_be_replayed_against_a_second_reset()
    {
        var (device, _, identifier, phone) = await ShortcutLoginAsync("9700000108");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(device);

        var first = await StartResetAsync(identifier);
        Assert.Equal(HttpStatusCode.OK,
            (await ApproveAsync(device, first.ApprovalId, deviceId, key, first.MatchNumber)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(identifier, ResetOtp(phone), first)).StatusCode);

        // Spent exactly once, and left in a state no later completion can match.
        var afterSpend = await WithDbAsync(db => db.AuthChallenges.AsNoTracking()
            .Where(c => c.Id == first.ApprovalId).Select(c => c.Status).FirstAsync());
        Assert.Equal(AuthChallengeStatus.Consumed, afterSpend);

        // A second ceremony, replaying the approval AND the token the first one already spent. The
        // assertion is "not accepted" rather than a specific error: a fresh reset within the OTP resend
        // window may not mint a new code, and asserting the exact refusal would then be testing the
        // cooldown instead of the replay guard.
        await StartResetAsync(identifier);
        var replay = await CompleteAsync(identifier, ResetOtp(phone), first);
        Assert.NotEqual(HttpStatusCode.OK, replay.StatusCode);
    }

    [Fact]
    public async Task A_concurrent_reset_cannot_spend_an_approval_belonging_to_another_reset()
    {
        // The sharpest form of the attack, and the one that account-scoping alone does NOT stop. The owner
        // legitimately resets and approves on their phone, leaving an Approved row. At the same moment the
        // SIM-swap attacker runs their own reset for the same account and holds a valid OTP. They must not
        // be able to reach across and spend the owner's approval.
        var (device, _, identifier, phone) = await ShortcutLoginAsync("9700000109");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(device);

        var owner = await StartResetAsync(identifier);
        Assert.Equal(HttpStatusCode.OK,
            (await ApproveAsync(device, owner.ApprovalId, deviceId, key, owner.MatchNumber)).StatusCode);

        // The attacker's own ceremony: their own token, and the OTP now in flight. They never saw the
        // owner's reset token, so this is everything they could possibly hold.
        var attacker = await StartResetAsync(identifier);
        Assert.NotEqual(owner.ApprovalId, attacker.ApprovalId);
        Assert.NotEqual(owner.ResetToken, attacker.ResetToken);

        var hijack = await CompleteAsync(identifier, ResetOtp(phone),
            approvalIdOverride: owner.ApprovalId, resetTokenOverride: attacker.ResetToken);
        Assert.Equal("second_factor_required", await ErrorOf(hijack));

        // Unspent — a failed hijack must not burn the owner's approval either.
        var status = await WithDbAsync(db => db.AuthChallenges.AsNoTracking()
            .Where(c => c.Id == owner.ApprovalId).Select(c => c.Status).FirstAsync());
        Assert.Equal(AuthChallengeStatus.Approved, status);
    }

    [Fact]
    public async Task An_approval_without_its_reset_token_is_refused()
    {
        var (device, _, identifier, phone) = await ShortcutLoginAsync("9700000117");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(device);

        var start = await StartResetAsync(identifier);
        Assert.Equal(HttpStatusCode.OK,
            (await ApproveAsync(device, start.ApprovalId, deviceId, key, start.MatchNumber)).StatusCode);

        var res = await CompleteAsync(identifier, ResetOtp(phone), approvalIdOverride: start.ApprovalId);
        Assert.Equal("second_factor_required", await ErrorOf(res));
    }

    [Fact]
    public async Task An_expired_approval_cannot_authorize_a_reset()
    {
        var (device, _, identifier, phone) = await ShortcutLoginAsync("9700000110");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(device);

        var start = await StartResetAsync(identifier);
        Assert.Equal(HttpStatusCode.OK,
            (await ApproveAsync(device, start.ApprovalId, deviceId, key, start.MatchNumber)).StatusCode);

        // Age it past its five-minute ceiling. Approved but expired must be worth nothing.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.AuthChallenges.Where(c => c.Id == start.ApprovalId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
        }

        var res = await CompleteAsync(identifier, ResetOtp(phone), start);
        Assert.Equal("second_factor_required", await ErrorOf(res));
    }

    [Fact]
    public async Task Two_concurrent_completions_cannot_both_spend_one_approval()
    {
        var (device, _, identifier, phone) = await ShortcutLoginAsync("9700000111");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(device);

        var start = await StartResetAsync(identifier);
        Assert.Equal(HttpStatusCode.OK,
            (await ApproveAsync(device, start.ApprovalId, deviceId, key, start.MatchNumber)).StatusCode);

        var otp = ResetOtp(phone);
        // Fired together on purpose: a read-then-write would let both observe Approved and both proceed,
        // spending a single-use factor twice. The conditional UPDATE is what makes that impossible.
        var both = await Task.WhenAll(
            CompleteAsync(identifier, otp, start),
            CompleteAsync(identifier, otp, start));

        Assert.True(both.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1,
            "two concurrent completions both succeeded against one approval");

        var status = await WithDbAsync(db => db.AuthChallenges.AsNoTracking()
            .Where(c => c.Id == start.ApprovalId).Select(c => c.Status).FirstAsync());
        Assert.NotEqual(AuthChallengeStatus.Approved, status);   // spent exactly once, never left reusable
    }

    // ── 9-10: the flows that must keep working ───────────────────────────────

    [Fact]
    public async Task Signed_in_elsewhere_the_owner_can_approve_their_own_reset_and_it_succeeds()
    {
        // The legitimate case the ambient step-up was reaching for, now done properly: the phone is still
        // signed in, and it approves *this* reset by name.
        var (device, _, identifier, phone) = await ShortcutLoginAsync("9700000112");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(device);

        var start = await StartResetAsync(identifier);

        var pending = await device.GetFromJsonAsync<JsonElement>("/v1/auth/password/reset/pending");
        Assert.Single(pending.EnumerateArray());     // the device can see the reset waiting on it

        Assert.Equal(HttpStatusCode.OK,
            (await ApproveAsync(device, start.ApprovalId, deviceId, key, start.MatchNumber)).StatusCode);

        var done = await CompleteAsync(identifier, ResetOtp(phone), start);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        // The ceremony logged the user in, and the new password works.
        var body = await done.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("access_token").GetString()));

        var login = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/password/login",
            new { identifier, password = NewPassword });
        Assert.NotEqual(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Approving_requires_the_match_number_shown_in_the_starting_browser()
    {
        // The control that survives a careless tap: the digits live only in the browser that started the
        // reset. An approver who cannot see them cannot approve, which is precisely the SIM-swap attacker's
        // victim being pushed an approval for a reset they did not start.
        var (device, _, identifier, _) = await ShortcutLoginAsync("9700000113");
        var (deviceId, key) = await EnrollTrustedDeviceAsync(device);
        var start = await StartResetAsync(identifier);

        var wrong = start.MatchNumber == 99 ? 10 : start.MatchNumber + 1;
        var res = await ApproveAsync(device, start.ApprovalId, deviceId, key, wrong);
        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);

        var status = await WithDbAsync(db => db.AuthChallenges.AsNoTracking()
            .Where(c => c.Id == start.ApprovalId).Select(c => c.Status).FirstAsync());
        Assert.NotEqual(AuthChallengeStatus.Approved, status);
    }

    [Fact]
    public async Task A_locked_out_user_with_no_device_still_resets_with_a_recovery_code()
    {
        var (client, userId, identifier, phone) = await ShortcutLoginAsync("9700000114");
        // Codes are step-up gated, so mint them through the service the way the other reset tests do.
        IReadOnlyList<string> codes;
        using (var scope = _factory.Services.CreateScope())
            codes = await scope.ServiceProvider
                .GetRequiredService<Kurx.Application.Abstractions.IRecoveryCodeService>().GenerateAsync(userId);

        await StartResetAsync(identifier);
        var res = await CompleteAsync(identifier, ResetOtp(phone), recoveryCode: codes[0]);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        client.Dispose();
    }

    [Fact]
    public async Task With_neither_factor_the_refusal_names_the_missing_one()
    {
        var (_, _, identifier, phone) = await ShortcutLoginAsync("9700000115");
        await StartResetAsync(identifier);

        var res = await CompleteAsync(identifier, ResetOtp(phone));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("second_factor_required", await ErrorOf(res));
    }

    [Fact]
    public async Task Start_does_not_reveal_whether_the_account_exists()
    {
        var real = await _anon.PostAsJsonAsync("/v1/auth/password/reset/start", new { identifier = "919700000116" });
        var fake = await _anon.PostAsJsonAsync("/v1/auth/password/reset/start", new { identifier = "919000000000" });

        Assert.Equal(real.StatusCode, fake.StatusCode);
        var a = await real.Content.ReadFromJsonAsync<JsonElement>();
        var b = await fake.Content.ReadFromJsonAsync<JsonElement>();
        // Same shape, both populated: a decoy is indistinguishable from a real approval.
        Assert.Equal(
            a.EnumerateObject().Select(p => p.Name).OrderBy(n => n),
            b.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        Assert.NotEqual(Guid.Empty, b.GetProperty("approval_id").GetGuid());
    }
}
