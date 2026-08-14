using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Infrastructure.Auth;
using Xunit;

namespace Kurx.Tests;

/// <summary>Password system (D-129) — factor 1. Real HTTP against kurx_test.
///
/// <para>Every test uses a distinct phone: the legacy OTP path caps requests per phone (D-005), so
/// sharing one would fail on rate limiting rather than on the behaviour under test.</para></summary>
public class PasswordSystemTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public PasswordSystemTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string Strong = "correct horse battery staple";
    private const string Strong2 = "an entirely different passphrase";

    /// <summary>OTP-logs in a fresh account and returns a client with the bearer token set — the
    /// migration starting point, an account that has no password yet.</summary>
    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        var req = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        // Assert here rather than letting LastOtpFor throw "sequence contains no matching element",
        // which hides whether the cause was rate limiting, validation, or something else entirely.
        Assert.True(req.IsSuccessStatusCode,
            $"OTP request for {phone} failed: {req.StatusCode} {await req.Content.ReadAsStringAsync()}");
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = await verify.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private static async Task<string?> ErrorOf(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out var e)
            ? e.GetString() : null;

    // ── Creation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_otp_era_account_starts_without_a_password_and_can_create_one()
    {
        var client = await LoginAsync("9500000001");

        var before = await (await client.GetAsync("/v1/auth/password/status")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(before.GetProperty("has_password").GetBoolean());

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong })).StatusCode);

        var after = await (await client.GetAsync("/v1/auth/password/status")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(after.GetProperty("has_password").GetBoolean());
    }

    [Fact]
    public async Task Setting_an_initial_password_twice_is_refused()
    {
        var client = await LoginAsync("9500000002");
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong });

        // Overwriting without proving the current password would let a stolen access token silently
        // take ownership of the account.
        var second = await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong2 });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("password_already_set", await ErrorOf(second));
    }

    [Fact]
    public async Task Password_endpoints_require_authentication()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/v1/auth/password/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/v1/auth/password/change",
                new { currentPassword = Strong, newPassword = Strong2 })).StatusCode);
    }

    // ── Policy (NIST 800-63B) ────────────────────────────────────────────────

    [Fact]
    public async Task A_password_shorter_than_the_minimum_is_refused()
    {
        var client = await LoginAsync("9500000003");
        var res = await client.PostAsJsonAsync("/v1/auth/password/set", new { password = "short1234" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("password_too_short", await ErrorOf(res));
    }

    [Fact]
    public async Task A_breached_password_is_refused_even_though_it_satisfies_length()
    {
        var client = await LoginAsync("9500000004");
        // 12 characters, digits and letters — it would pass any naive composition rule, which is
        // precisely why length alone is not the whole control.
        var res = await client.PostAsJsonAsync("/v1/auth/password/set", new { password = "password1234" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("password_breached", await ErrorOf(res));
    }

    [Fact]
    public async Task A_password_built_from_the_users_own_phone_is_refused()
    {
        var client = await LoginAsync("9500000005");
        var res = await client.PostAsJsonAsync("/v1/auth/password/set", new { password = "919500000005extra" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("password_contains_identifier", await ErrorOf(res));
    }

    [Fact]
    public void Policy_rejects_over_long_input_so_hashing_cannot_be_used_as_a_dos_lever()
    {
        var rejection = PasswordPolicy.Validate(new string('a', PasswordPolicy.MaxLength + 1), null, null, null);
        Assert.Equal(Kurx.Application.Abstractions.PasswordRejection.TooLong, rejection);
    }

    [Fact]
    public void Policy_accepts_a_long_passphrase_with_no_composition_requirements()
    {
        // NIST explicitly wants passphrases to work: no symbol, no digit, no uppercase.
        var rejection = PasswordPolicy.Validate("the quick brown fox jumps", "alice", "alice@example.com", "919500000099");
        Assert.Equal(Kurx.Application.Abstractions.PasswordRejection.None, rejection);
    }

    // ── Change ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Changing_a_password_requires_the_current_one()
    {
        var client = await LoginAsync("9500000006");
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong });

        var wrong = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = "not the current one", newPassword = Strong2 });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("invalid_credentials", await ErrorOf(wrong));

        var right = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong, newPassword = Strong2 });
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
    }

    [Fact]
    public async Task A_recently_used_password_cannot_be_set_again()
    {
        var client = await LoginAsync("9500000007");
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong });
        await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong, newPassword = Strong2 });

        // Going straight back to the password just abandoned would make "change" a no-op — the exact
        // case that matters after a suspected compromise.
        var back = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong2, newPassword = Strong });
        Assert.Equal(HttpStatusCode.BadRequest, back.StatusCode);
        Assert.Equal("password_reused", await ErrorOf(back));
    }

    [Fact]
    public async Task Changing_before_a_password_exists_is_refused()
    {
        var client = await LoginAsync("9500000008");
        var res = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong, newPassword = Strong2 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("password_not_set", await ErrorOf(res));
    }

    [Fact]
    public async Task A_new_password_must_also_satisfy_policy()
    {
        var client = await LoginAsync("9500000009");
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong });

        var res = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong, newPassword = "password1234" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("password_breached", await ErrorOf(res));
    }

    // ── Lockout ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Repeated_wrong_passwords_lock_the_account_and_the_correct_one_then_fails_too()
    {
        var client = await LoginAsync("9500000010");
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong });

        for (var i = 0; i < PasswordService.MaxFailedAttempts; i++)
        {
            var attempt = await client.PostAsJsonAsync("/v1/auth/password/change",
                new { currentPassword = $"wrong guess {i}", newPassword = Strong2 });
            Assert.NotEqual(HttpStatusCode.OK, attempt.StatusCode);
        }

        // The lockout must bite even for the *correct* password — otherwise it is decorative and an
        // attacker keeps learning whether each guess was right.
        var locked = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong, newPassword = Strong2 });
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        Assert.Equal("account_locked", await ErrorOf(locked));
    }

    [Fact]
    public async Task A_successful_change_clears_the_failure_run()
    {
        var client = await LoginAsync("9500000011");
        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = Strong });

        // Stop one short of the lockout threshold.
        for (var i = 0; i < PasswordService.MaxFailedAttempts - 1; i++)
            await client.PostAsJsonAsync("/v1/auth/password/change",
                new { currentPassword = $"wrong guess {i}", newPassword = Strong2 });

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = Strong, newPassword = Strong2 })).StatusCode);

        // Counter reset: a fresh run of failures must start from zero, so the next wrong guess is not
        // instantly a lockout for a user who has just proven ownership.
        var next = await client.PostAsJsonAsync("/v1/auth/password/change",
            new { currentPassword = "wrong again", newPassword = Strong });
        Assert.Equal(HttpStatusCode.Unauthorized, next.StatusCode);
        Assert.Equal("invalid_credentials", await ErrorOf(next));
    }
}
