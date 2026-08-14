using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain;
using Kurx.Domain.Entities;

namespace Kurx.Tests;

/// <summary>First-time-user onboarding: what a brand-new account must supply before it is finished, and
/// what a returning one must never be asked for again.
///
/// <para><b>The gaps these close.</b> Onboarding completion was decided by two hand-copied expressions —
/// <c>/v1/me</c> tested <c>Name == ""</c> while <c>/v1/auth/registration/status</c> tested
/// <c>IsNullOrWhiteSpace(Name)</c> — so a whitespace name was onboarded according to one endpoint and not
/// the other. Neither consulted the credential store, so an account with no password reported itself
/// fully set up, and the client's registration flow read that as licence to skip the password step
/// entirely. Date of birth was collected nowhere at all, despite events carrying MinAge/MaxAge
/// eligibility that has nothing else to read.</para></summary>
public class OnboardingCompletionTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public OnboardingCompletionTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    /// <param name="phone">Exactly what a client would send — spacing and '+' included.</param>
    /// <param name="lookupDigits">The bare national digits to find the captured code by. Needed because
    /// the capture helper matches on the last ten CHARACTERS of what the provider was handed, which a
    /// spaced or '+'-prefixed input does not line up with.</param>
    private async Task<(HttpClient Client, Guid UserId)> SignInAsync(string phone, string? lookupDigits = null)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(lookupDigits ?? phone);
        var response = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private static async Task<JsonElement> MeAsync(HttpClient client) =>
        await (await client.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> StatusAsync(HttpClient client) =>
        await (await client.GetAsync("/v1/auth/registration/status"))
            .Content.ReadFromJsonAsync<JsonElement>();

    private static string[] Remaining(JsonElement status) =>
        status.GetProperty("remaining").EnumerateArray().Select(e => e.GetString()!).ToArray();

    // ── TEST 1 / 5 / 10: the new account, and what it still owes ────────────────────────────────

    [Fact]
    public async Task A_brand_new_account_needs_profile_and_password_before_it_is_onboarded()
    {
        var (client, _) = await SignInAsync("9100000001");

        var me = await MeAsync(client);
        Assert.True(me.GetProperty("needs_onboarding").GetBoolean());

        // Blocking steps first, and both present: the password step used to be listed after the
        // skippable email one, and was not reflected in needs_onboarding at all.
        var remaining = Remaining(await StatusAsync(client));
        Assert.Equal("complete_profile", remaining[0]);
        Assert.Equal("create_password", remaining[1]);
    }

    [Fact]
    public async Task Onboarding_finishes_only_once_profile_AND_password_are_both_set()
    {
        var (client, _) = await SignInAsync("9100000002");

        var profile = await client.PatchAsJsonAsync("/v1/me/profile", new
        {
            name = "Asha Rao",
            username = "asharao",
            dateOfBirth = "1998-04-11",
        });
        profile.EnsureSuccessStatusCode();

        // Profile alone is NOT enough — this is the state that used to report itself complete and
        // let the client walk the user past the password step into the app.
        Assert.True((await MeAsync(client)).GetProperty("needs_onboarding").GetBoolean());
        Assert.Contains("create_password", Remaining(await StatusAsync(client)));

        var password = await client.PostAsJsonAsync("/v1/auth/password/set",
            new { password = "correct-horse-battery" });
        password.EnsureSuccessStatusCode();

        var me = await MeAsync(client);
        Assert.False(me.GetProperty("needs_onboarding").GetBoolean());
        Assert.Equal("1998-04-11", me.GetProperty("date_of_birth").GetString());
        // Written on insert since the beginning, but served to nobody until now.
        Assert.True(me.GetProperty("created_at").GetDateTime() > DateTime.UtcNow.AddMinutes(-10));
        // Email and device stay outstanding but must never block: neither can be completed by a user
        // who mistyped an address or is on a handset that cannot enrol.
        Assert.DoesNotContain("complete_profile", Remaining(await StatusAsync(client)));
        Assert.DoesNotContain("create_password", Remaining(await StatusAsync(client)));
    }

    [Fact]
    public async Task A_profile_without_a_date_of_birth_is_still_incomplete()
    {
        var (client, _) = await SignInAsync("9100000003");

        var response = await client.PatchAsJsonAsync("/v1/me/profile",
            new { name = "No Birthday", username = "nobirthday" });
        response.EnsureSuccessStatusCode();

        Assert.Contains("complete_profile", Remaining(await StatusAsync(client)));
        Assert.True((await MeAsync(client)).GetProperty("needs_onboarding").GetBoolean());
    }

    [Fact]
    public async Task A_whitespace_name_does_not_count_as_a_name()
    {
        var (client, _) = await SignInAsync("9100000004");

        await client.PatchAsJsonAsync("/v1/me/profile",
            new { name = "   ", username = "spacename", dateOfBirth = "1996-01-02" });

        // The two endpoints disagreed here before: `/v1/me` tested Name == "" and called this done.
        Assert.True((await MeAsync(client)).GetProperty("needs_onboarding").GetBoolean());
        Assert.True((await StatusAsync(client)).GetProperty("needs_onboarding").GetBoolean());
    }

    // ── TEST 12: date-of-birth validation is the server's call ──────────────────────────────────

    [Theory]
    [InlineData("2035-01-01")]                       // the future
    [InlineData("1850-01-01")]                       // implausible
    public async Task An_impossible_date_of_birth_is_rejected(string dateOfBirth)
    {
        var (client, _) = await SignInAsync("910000001" + dateOfBirth[2]);

        var response = await client.PatchAsJsonAsync("/v1/me/profile",
            new { name = "Edge Case", username = "edge" + dateOfBirth[..4], dateOfBirth });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_applicant_under_the_minimum_age_is_refused()
    {
        var (client, _) = await SignInAsync("9100000007");
        var tooYoung = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-Onboarding.MinimumAgeYears + 1);

        var response = await client.PatchAsJsonAsync("/v1/me/profile", new
        {
            name = "Too Young",
            username = "tooyoung",
            dateOfBirth = tooYoung.ToString("yyyy-MM-dd"),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("under_minimum_age", problem.GetProperty("error").GetString());
    }

    /// <summary>The profile write surface is bounded server-side, not merely in each client's form.
    ///
    /// <para>`PATCH /v1/me/profile` ran no validator at all, so `name`/`headline`/`bio` were unbounded
    /// while mobile's onboarding capped bio at 300 in the UI. A limit enforced only where it is
    /// convenient is not a limit — the same shape as the password policy that read 8 on the client and
    /// 12 on the server.</para></summary>
    [Fact]
    public async Task An_oversized_bio_is_refused_by_the_server_not_just_by_the_form()
    {
        var (client, _) = await SignInAsync("9100000030");

        var response = await client.PatchAsJsonAsync("/v1/me/profile", new
        {
            name = "Verbose Person",
            username = "verbose",
            dateOfBirth = "1991-11-11",
            bio = new string('x', 2001),        // one past the limit
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_bio_within_the_limit_is_accepted()
    {
        var (client, _) = await SignInAsync("9100000031");

        var response = await client.PatchAsJsonAsync("/v1/me/profile", new
        {
            name = "Concise Person",
            username = "concise",
            dateOfBirth = "1991-12-12",
            bio = new string('x', 2000),
        });

        response.EnsureSuccessStatusCode();
    }

    // ── TEST 2 / 11: an existing phone signs in; it never forks a second account ─────────────────

    [Fact]
    public async Task An_existing_phone_signs_in_to_the_same_account_rather_than_creating_another()
    {
        const string phone = "9100000008";
        var (first, firstUserId) = await SignInAsync(phone);
        await first.PatchAsJsonAsync("/v1/me/profile",
            new { name = "Returning User", username = "returning", dateOfBirth = "1994-07-19" });
        await first.PostAsJsonAsync("/v1/auth/password/set", new { password = "correct-horse-battery" });

        // The same number again is a LOGIN, not a second registration. Sending a code to a known
        // number is the design (D-011/D-037): one combined ceremony, so the API never has to answer
        // "does this number have an account" to an anonymous caller.
        var (second, secondUserId) = await SignInAsync(phone);

        Assert.Equal(firstUserId, secondUserId);
        // …and the returning user is asked for nothing again.
        Assert.False((await MeAsync(second)).GetProperty("needs_onboarding").GetBoolean());
        Assert.Equal("Returning User", (await MeAsync(second)).GetProperty("name").GetString());
    }

    // ── TEST 9: one canonical phone identity ────────────────────────────────────────────────────

    // A number per case, not one shared across all three: each case signs in twice, and six requests
    // to a single destination would trip the 3-per-10-minutes OTP cap and leave a stale code behind.
    [Theory]
    [InlineData("9100000021", "+919100000021")]
    [InlineData("9100000022", "919100000022")]
    [InlineData("9100000023", "+91 91000 00023")]
    public async Task Equivalent_phone_spellings_resolve_to_one_account(string bare, string equivalent)
    {
        var (_, canonicalUserId) = await SignInAsync(bare);
        var (_, sameUserId) = await SignInAsync(equivalent, lookupDigits: bare);

        Assert.Equal(canonicalUserId, sameUserId);
    }

    // ── TEST 8: username uniqueness is the database's job ───────────────────────────────────────

    [Fact]
    public async Task A_taken_username_is_refused_with_a_reason_the_client_can_show()
    {
        var (owner, _) = await SignInAsync("9100000010");
        (await owner.PatchAsJsonAsync("/v1/me/profile",
            new { name = "First Claimer", username = "contested", dateOfBirth = "1993-03-03" }))
            .EnsureSuccessStatusCode();

        var (rival, _) = await SignInAsync("9100000011");
        var response = await rival.PatchAsJsonAsync("/v1/me/profile",
            new { name = "Second Claimer", username = "contested", dateOfBirth = "1993-03-04" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("username_taken", problem.GetProperty("error").GetString());
    }

    // ── TEST 10: the code is the gate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_wrong_code_creates_no_account()
    {
        const string phone = "9100000012";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });

        var response = await client.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone, code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── The rule itself, without a round trip ───────────────────────────────────────────────────

    [Fact]
    public void The_completion_rule_requires_name_username_date_of_birth_and_password()
    {
        var complete = new User
        {
            Phone = "919100000099",
            Name = "Complete Person",
            Username = "complete",
            DateOfBirth = new DateOnly(1995, 6, 15),
        };

        Assert.False(Onboarding.IsIncomplete(complete, hasPassword: true));
        Assert.True(Onboarding.IsIncomplete(complete, hasPassword: false));

        Assert.True(Onboarding.IsIncomplete(
            new User { Phone = "9", Name = " ", Username = "u", DateOfBirth = new DateOnly(1995, 1, 1) }, true));
        Assert.True(Onboarding.IsIncomplete(
            new User { Phone = "9", Name = "N", Username = null, DateOfBirth = new DateOnly(1995, 1, 1) }, true));
        Assert.True(Onboarding.IsIncomplete(
            new User { Phone = "9", Name = "N", Username = "u", DateOfBirth = null }, true));
    }

    /// <summary>The date-of-birth and password requirements bind the accounts created under them, and
    /// nobody else. A password is required in the account-CREATION flow; applying it backwards would
    /// lock an established user out of the app they are already using, over a rule that did not exist
    /// when they signed up.</summary>
    [Fact]
    public void An_account_created_before_the_rule_is_not_retroactively_blocked()
    {
        User legacy(DateTime createdAt) => new()
        {
            Phone = "919100000098",
            Name = "Established User",
            Username = "established",
            CreatedAt = createdAt,
            DateOfBirth = null,          // never asked for one
        };

        var before = legacy(Onboarding.RequirementsEffectiveFrom.AddDays(-1));
        Assert.False(Onboarding.IsIncomplete(before, hasPassword: false));

        // Created after the cutoff, so both new requirements apply.
        var after = legacy(Onboarding.RequirementsEffectiveFrom.AddMinutes(1));
        Assert.True(Onboarding.IsIncomplete(after, hasPassword: false));

        // Identity, however, has been required of every account since D-037 — grandfathering does not
        // reach back past that.
        var noUsername = legacy(Onboarding.RequirementsEffectiveFrom.AddDays(-1));
        noUsername.Username = null;
        Assert.True(Onboarding.IsIncomplete(noUsername, hasPassword: true));
    }

    [Fact]
    public void A_grandfathered_account_is_still_told_what_it_is_missing()
    {
        var legacy = new User
        {
            Phone = "919100000097",
            Name = "Established User",
            Username = "established2",
            CreatedAt = Onboarding.RequirementsEffectiveFrom.AddDays(-30),
        };

        // Unblocked, but not pretended complete: Security settings has to be able to offer these.
        Assert.False(Onboarding.IsIncomplete(legacy, hasPassword: false));
        var remaining = Onboarding.Remaining(legacy, hasPassword: false, hasTrustedDevice: false);
        Assert.Contains("complete_profile", remaining);
        Assert.Contains("create_password", remaining);
    }

    [Fact]
    public void Age_is_counted_in_completed_years_not_calendar_years()
    {
        var today = new DateOnly(2026, 8, 9);

        // Birthday still to come this year — 17, not 18.
        Assert.Equal(17, Onboarding.AgeOn(new DateOnly(2008, 12, 25), today));
        Assert.Equal(18, Onboarding.AgeOn(new DateOnly(2008, 8, 9), today));   // today
        Assert.Equal(17, Onboarding.AgeOn(new DateOnly(2008, 8, 10), today));  // tomorrow
    }
}
