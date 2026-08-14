using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

public class AuthTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public AuthTests(KurxApiFactory factory)
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

    private async Task<JsonElement> LoginAsync(string phone)
    {
        var req = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        Assert.Equal(HttpStatusCode.OK, req.StatusCode);
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return await verify.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Health_reports_dependency_checks()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        var checks = body.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Contains("postgres", checks);
        Assert.Contains("storage", checks);
    }

    [Fact]
    public async Task Otp_login_creates_user_and_issues_tokens()
    {
        var tokens = await LoginAsync("9000000001");
        Assert.True(tokens.GetProperty("is_new_user").GetBoolean());
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("refresh_token").GetString()));

        // Second login with the same phone is an existing user.
        var again = await LoginAsync("9000000001");
        Assert.False(again.GetProperty("is_new_user").GetBoolean());
        Assert.Equal(tokens.GetProperty("user_id").GetGuid(), again.GetProperty("user_id").GetGuid());
    }

    [Fact]
    public async Task Wrong_code_is_rejected_and_five_attempts_lock_the_otp()
    {
        var phone = "9000000002";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var wrong = code == "111111" ? "222222" : "111111";

        for (var i = 0; i < 5; i++)
        {
            var res = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code = wrong });
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        // Correct code no longer works: attempt cap reached.
        var locked = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        var body = await locked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("too_many_attempts", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Fourth_otp_request_within_10_minutes_is_rate_limited()
    {
        var phone = "9000000003";
        for (var i = 0; i < 3; i++)
        {
            var ok = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        var limited = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Me_requires_auth_and_returns_profile()
    {
        var anonymous = await _client.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var tokens = await LoginAsync("9000000004");
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var me = await res.Content.ReadFromJsonAsync<JsonElement>();
        // Canonical E.164, matching /security-center and /registration/status. This endpoint used to be the
        // only one returning the raw legacy digits, so the API answered the same question two ways.
        Assert.Equal("+919000000004", me.GetProperty("phone").GetString());
        Assert.True(me.GetProperty("needs_onboarding").GetBoolean());
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_all_sessions()
    {
        var tokens = await LoginAsync("9000000005");
        var refresh1 = tokens.GetProperty("refresh_token").GetString();

        var rotated = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh1 });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var tokens2 = await rotated.Content.ReadFromJsonAsync<JsonElement>();
        var refresh2 = tokens2.GetProperty("refresh_token").GetString();
        Assert.NotEqual(refresh1, refresh2);

        // Reusing the rotated token fails and revokes the whole chain, including refresh2.
        var reuse = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh1 });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        var afterReuse = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh2 });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var tokens = await LoginAsync("9000000006");
        var refresh = tokens.GetProperty("refresh_token").GetString();

        var logout = await _client.PostAsJsonAsync("/v1/auth/logout", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var res = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private HttpClient AuthedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>Each requirement is added in turn and the flag only clears on the last one — so a
    /// regression that drops any single one of them fails here, naming which.
    ///
    /// <para>Was <c>Needs_onboarding_requires_both_name_and_username</c>. D-311 added date of birth and a
    /// password to the rule: name + username described an account that could neither be age-checked
    /// against an event's MinAge nor signed into from a device that cannot receive the SMS.</para></summary>
    [Fact]
    public async Task Needs_onboarding_requires_name_username_date_of_birth_and_password()
    {
        var tokens = await LoginAsync("9000000010");
        var client = AuthedClient(tokens.GetProperty("access_token").GetString()!);

        var afterLogin = await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(afterLogin.GetProperty("needs_onboarding").GetBoolean());

        // Name alone (the pre-D-037 onboarding contract) is no longer sufficient.
        await client.PatchAsJsonAsync("/v1/me/profile", new { name = "Legacy User" });
        var afterName = await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(afterName.GetProperty("needs_onboarding").GetBoolean());

        // Nor name + username (the D-037 contract).
        await client.PatchAsJsonAsync("/v1/me/profile", new { username = "onboarded_user_10" });
        var afterUsername = await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(afterUsername.GetProperty("needs_onboarding").GetBoolean());

        await client.PatchAsJsonAsync("/v1/me/profile", new { dateOfBirth = "1997-02-14" });
        var afterDob = await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(afterDob.GetProperty("needs_onboarding").GetBoolean());   // still no password

        await client.PostAsJsonAsync("/v1/auth/password/set", new { password = "correct-horse-battery" });
        var afterPassword = await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(afterPassword.GetProperty("needs_onboarding").GetBoolean());
    }

    [Fact]
    public async Task Username_availability_reports_expected_statuses()
    {
        var tokens = await LoginAsync("9000000011");
        var client = AuthedClient(tokens.GetProperty("access_token").GetString()!);
        await client.PatchAsJsonAsync("/v1/me/profile", new { username = "claimed_handle_11" });

        var available = await (await _client.GetAsync("/v1/usernames/availability?username=brand_new_handle_11"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("available", available.GetProperty("status").GetString());

        var taken = await (await _client.GetAsync("/v1/usernames/availability?username=claimed_handle_11"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unavailable", taken.GetProperty("status").GetString());

        var reserved = await (await _client.GetAsync("/v1/usernames/availability?username=admin"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("reserved", reserved.GetProperty("status").GetString());

        var invalid = await (await _client.GetAsync("/v1/usernames/availability?username=ab"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid", invalid.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Username_inside_reclaim_hold_is_unavailable_and_rejected_on_patch()
    {
        const string held = "held_handle_11";
        var releaser = await LoginAsync("9000000021");
        var releaserId = releaser.GetProperty("user_id").GetGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.UsernameHistory.Add(new UsernameHistory
            {
                Username = held,
                UserId = releaserId,
                ReleasedAt = DateTime.UtcNow.AddDays(-10), // inside the 30-day hold
            });
            await db.SaveChangesAsync();
        }

        var availability = await (await _client.GetAsync($"/v1/usernames/availability?username={held}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unavailable", availability.GetProperty("status").GetString());

        var tokens = await LoginAsync("9000000012");
        var client = AuthedClient(tokens.GetProperty("access_token").GetString()!);
        var patch = await client.PatchAsJsonAsync("/v1/me/profile", new { username = held });
        Assert.Equal(HttpStatusCode.Conflict, patch.StatusCode);
    }

    [Fact]
    public async Task Username_outside_reclaim_hold_is_available_again()
    {
        const string released = "released_handle_11";
        var releaser = await LoginAsync("9000000022");
        var releaserId = releaser.GetProperty("user_id").GetGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.UsernameHistory.Add(new UsernameHistory
            {
                Username = released,
                UserId = releaserId,
                ReleasedAt = DateTime.UtcNow.AddDays(-40), // past the 30-day hold
            });
            await db.SaveChangesAsync();
        }

        var availability = await (await _client.GetAsync($"/v1/usernames/availability?username={released}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("available", availability.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Phone_change_updates_existing_account_and_preserves_identity()
    {
        var tokens = await LoginAsync("9000000013");
        var userId = tokens.GetProperty("user_id").GetGuid();
        var oldRefresh = tokens.GetProperty("refresh_token").GetString();
        var client = AuthedClient(tokens.GetProperty("access_token").GetString()!);

        const string newPhone = "9000000014";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = newPhone });
        var code = _factory.WhatsApp.LastOtpFor(newPhone);

        var verify = await client.PostAsJsonAsync("/v1/me/phone/verify", new { phone = newPhone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verifyBody = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(verifyBody.GetProperty("access_token").GetString()));
        var newRefresh = verifyBody.GetProperty("refresh_token").GetString();
        Assert.NotEqual(oldRefresh, newRefresh);

        var authedWithNew = AuthedClient(verifyBody.GetProperty("access_token").GetString()!);
        var me = await (await authedWithNew.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, me.GetProperty("id").GetGuid());
        Assert.Equal("+919000000014", me.GetProperty("phone").GetString());

        // Check the freshly-issued token first: reusing the revoked old one below correctly
        // triggers D-014's separate reuse-detection sweep (revokes everything again, as theft
        // protection), which would otherwise kill the new token before we get to assert on it.
        var newRefreshResult = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = newRefresh });
        Assert.Equal(HttpStatusCode.OK, newRefreshResult.StatusCode);

        // The device's pre-change session is revoked (D-038).
        var oldRefreshResult = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefreshResult.StatusCode);
    }

    [Fact]
    public async Task Phone_change_revokes_every_other_active_session()
    {
        const string phone = "9000000031";
        var sessionA = await LoginAsync(phone); // two independent OTP logins => two simultaneously-active sessions
        var sessionB = await LoginAsync(phone);
        var refreshA = sessionA.GetProperty("refresh_token").GetString();
        var refreshB = sessionB.GetProperty("refresh_token").GetString();

        var client = AuthedClient(sessionB.GetProperty("access_token").GetString()!);
        const string newPhone = "9000000032";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = newPhone });
        var code = _factory.WhatsApp.LastOtpFor(newPhone);
        var verify = await client.PostAsJsonAsync("/v1/me/phone/verify", new { phone = newPhone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        // Check the freshly-issued token first — reusing a revoked token below correctly
        // triggers D-014's own reuse-detection sweep, which would otherwise revoke this one too.
        var verifyBody = await verify.Content.ReadFromJsonAsync<JsonElement>();
        var newRefresh = verifyBody.GetProperty("refresh_token").GetString();
        var newRefreshResult = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = newRefresh });
        Assert.Equal(HttpStatusCode.OK, newRefreshResult.StatusCode);

        var refreshAResult = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refreshA });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAResult.StatusCode);
        var refreshBResult = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refreshB });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshBResult.StatusCode);
    }

    [Fact]
    public async Task Phone_change_to_an_already_registered_number_is_rejected()
    {
        var other = await LoginAsync("9000000015");
        var otherPhone = "919000000015";

        var tokens = await LoginAsync("9000000016");
        var client = AuthedClient(tokens.GetProperty("access_token").GetString()!);

        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = otherPhone });
        var code = _factory.WhatsApp.LastOtpFor(otherPhone);

        var verify = await client.PostAsJsonAsync("/v1/me/phone/verify", new { phone = otherPhone, code });
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("phone_already_registered", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Concurrent_phone_change_to_the_same_number_never_500s_and_only_one_wins()
    {
        var tokensA = await LoginAsync("9000000017");
        var tokensC = await LoginAsync("9000000018");
        var clientA = AuthedClient(tokensA.GetProperty("access_token").GetString()!);
        var clientC = AuthedClient(tokensC.GetProperty("access_token").GetString()!);

        const string contested = "9000000019";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = contested });
        var code = _factory.WhatsApp.LastOtpFor(contested);

        var results = await Task.WhenAll(
            clientA.PostAsJsonAsync("/v1/me/phone/verify", new { phone = contested, code }),
            clientC.PostAsJsonAsync("/v1/me/phone/verify", new { phone = contested, code }));

        Assert.All(results, r => Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Concurrent_registration_for_a_new_phone_merges_into_one_account()
    {
        const string phone = "9000000020";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();
        var results = await Task.WhenAll(
            client1.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }),
            client2.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));

        // An OTP is single-use, so exactly one of the two redeems it (D-261). This previously asserted
        // that BOTH returned 200, which was only ever true when the race happened to interleave a
        // certain way — it passed one CI run and failed the next on identical test code. The property
        // worth pinning is the one in this test's name: the contested phone resolves to ONE account,
        // never two. The winner proves that; the loser is refused because the code is already spent.
        var winner = Assert.Single(results.Where(r => r.IsSuccessStatusCode));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Unauthorized);

        var body = await winner.Content.ReadFromJsonAsync<JsonElement>();
        var userId = body.GetProperty("user_id").GetGuid();

        // Re-authenticating the same phone lands on the SAME account — the merge guarantee, verified
        // without depending on how the two racers interleaved.
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var again = await (await _client.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone, code = _factory.WhatsApp.LastOtpFor(phone) })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, again.GetProperty("user_id").GetGuid());
    }

    [Fact]
    public async Task Two_users_with_null_email_do_not_conflict()
    {
        var a = await LoginAsync("9000000040");
        var b = await LoginAsync("9000000041");
        Assert.NotEqual(a.GetProperty("user_id").GetGuid(), b.GetProperty("user_id").GetGuid());
        // Both users were created with Email == null (D-037/D-038); the filtered unique
        // index only applies WHERE "Email" IS NOT NULL, so no conflict is expected here.
    }

    [Fact]
    public async Task Email_uniqueness_is_case_insensitive_at_the_database_level()
    {
        var userA = await LoginAsync("9000000042");
        var userB = await LoginAsync("9000000043");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var a = await db.Users.FirstAsync(u => u.Id == userA.GetProperty("user_id").GetGuid());
        a.Email = "Test@Example.com";
        await db.SaveChangesAsync();

        var b = await db.Users.FirstAsync(u => u.Id == userB.GetProperty("user_id").GetGuid());
        b.Email = "test@example.com"; // same mailbox, different case
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
