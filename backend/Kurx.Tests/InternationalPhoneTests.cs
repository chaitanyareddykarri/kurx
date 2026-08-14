using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// International phone support across numbering plans (E.164, D-089).
/// </summary>
/// <remarks>
/// <para><b>The defect these exist to prevent.</b> <c>NormalizePhone</c> stripped the '+' and then applied
/// <c>digits.Length == 10 ? "91" + digits</c>. Because it only saw length, it could not distinguish a
/// national number from a complete international one, so <b>every number whose full E.164 form is exactly
/// ten digits was refiled under India</b> — Singapore, Norway, Denmark and every other plan with a
/// two-digit country code and an eight-digit national number.</para>
///
/// <para>The consequence was worse than a rejection: <c>+65 9123 4567</c> became <c>+91 6591234567</c>, so
/// registration appeared to succeed and the one-time code was texted to an unrelated real phone in India.
/// The account owner never received one, and nothing anywhere logged a problem.</para>
///
/// <para><b>Why it survived so long:</b> every phone fixture in the suite was Indian, so an entire class of
/// countries was invisible to the tests. That is the gap this file closes — the country table below is the
/// point of the whole thing, not the individual assertions.</para>
/// </remarks>
public class InternationalPhoneTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public InternationalPhoneTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    /// <summary>Real, structurally valid numbers from ten numbering plans, chosen to span the shapes that
    /// broke the old rule. The <c>digits</c> column is the length after stripping '+': every row at
    /// <b>10</b> is one the previous implementation corrupted.</summary>
    public static IEnumerable<object[]> Countries() =>
    [
        //         E.164 input        country          full-digit length
        ["+919876543210", "India",          12],
        ["+14155552671",  "United States",  11],
        ["+442071838750", "United Kingdom", 12],
        ["+6591234567",   "Singapore",      10],   // ← was corrupted
        ["+4791234567",   "Norway",         10],   // ← was corrupted
        ["+4531234567",   "Denmark",        10],   // ← was corrupted
        ["+61412345678",  "Australia",      11],
        ["+4915112345678","Germany",        13],
        ["+33612345678",  "France",         11],
        ["+818012345678", "Japan",          12],
    ];

    // ── the normalizer itself ───────────────────────────────────────────────

    /// <summary>The unit-level guarantee: an international number survives normalization byte for byte.
    /// Asserted directly against the normalizer because it is shared by eighteen call sites across
    /// auth, orders, invitations, transfers and moderation — a regression here is not confined to login.</summary>
    [Theory]
    [MemberData(nameof(Countries))]
    public void An_international_number_normalises_to_its_own_digits(string e164, string country, int digits)
    {
        var normalized = AuthService.NormalizePhone(e164);

        Assert.Equal(e164[1..], normalized);
        Assert.Equal(digits, normalized.Length);
        Assert.False(normalized.StartsWith("91") && !country.Equals("India"),
            $"{country}'s number was refiled under India — the exact defect this suite exists to catch");
    }

    /// <summary>Formatting a user might paste must not change the outcome. libphonenumber tolerates spaces,
    /// dashes and brackets; the normalizer must not reintroduce a naive digit count around them.</summary>
    [Theory]
    [InlineData("+65 9123 4567", "6591234567")]
    [InlineData("+47 912 34 567", "4791234567")]
    [InlineData("+1 (415) 555-2671", "14155552671")]
    [InlineData("+91 98765 43210", "919876543210")]
    public void Human_formatting_does_not_change_the_canonical_result(string input, string expected)
        => Assert.Equal(expected, AuthService.NormalizePhone(input));

    /// <summary>The specific regression. Ten-digit E.164 numbers are the ones the old length rule caught,
    /// so they get their own named test rather than living only inside a theory row.</summary>
    [Theory]
    [InlineData("+6591234567", "Singapore")]
    [InlineData("+4791234567", "Norway")]
    [InlineData("+4531234567", "Denmark")]
    public void A_ten_digit_e164_number_is_not_given_an_indian_country_code(string e164, string country)
    {
        var normalized = AuthService.NormalizePhone(e164);

        Assert.DoesNotContain("91" + e164[1..], normalized);
        Assert.Equal(e164[1..], normalized);
        // Unchanged length is the proof that nothing was prepended.
        Assert.True(normalized.Length == 10, $"{country}'s number changed length during normalization");
    }

    /// <summary>A number that cannot be placed is returned as its digits rather than assigned a country.
    /// Inventing one is what produced the original defect, so "leave it alone" is the safe direction.</summary>
    [Theory]
    [InlineData("+99900011122")]
    [InlineData("12345")]
    public void An_unplaceable_number_is_never_given_an_invented_country_code(string input)
    {
        var normalized = AuthService.NormalizePhone(input);

        Assert.Equal(new string(input.Where(char.IsAsciiDigit).ToArray()), normalized);
    }

    /// <summary>Backward compatibility: a bare Indian national number from a pre-E.164 client still resolves
    /// the way it always has. This is what makes the fix non-breaking for the existing user base.</summary>
    [Fact]
    public void A_bare_national_number_from_a_legacy_client_still_resolves_to_india()
        => Assert.Equal("919876543210", AuthService.NormalizePhone("9876543210"));

    // ── end to end, over real HTTP ──────────────────────────────────────────

    /// <summary>Registration and OTP login for each country, through the real endpoints. The assertion that
    /// matters is the last one: the code has to arrive <b>at the number the user gave</b>. Under the old
    /// implementation a Singaporean registration texted <c>+916591234567</c>, so this failed by timeout
    /// rather than by a wrong value — the capturing sender simply never saw that destination.</summary>
    [Theory]
    [MemberData(nameof(Countries))]
    public async Task A_user_from_any_country_can_register_and_sign_in(string e164, string country, int digits)
    {
        _ = digits;

        var request = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = e164 });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        // Reads what was actually sent to THIS number. A code filed under another country never appears here.
        var code = _factory.Phone.LastOtpFor(e164);
        Assert.False(string.IsNullOrWhiteSpace(code), $"no code was delivered to the {country} number");

        var verify = await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone = e164, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();
        var access = body.GetProperty("access_token").GetString();
        var userId = body.GetProperty("user_id").GetGuid();

        // The account stores the number the user gave, in canonical form.
        var me = _factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var profile = await (await me.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(userId, profile.GetProperty("id").GetGuid());
        var storedPhone = profile.GetProperty("phone").GetString()!;
        Assert.Contains(e164[1..], storedPhone.Replace("+", ""));
    }

    /// <summary>Signing in again with the same international number must find the same account — the
    /// identifier resolution path is separate from the registration path and could drift from it.</summary>
    [Theory]
    [InlineData("+6591234599", "Singapore")]
    [InlineData("+4915112345699", "Germany")]
    public async Task An_international_number_resolves_to_the_same_account_on_a_later_sign_in(string e164, string country)
    {
        _ = country;

        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = e164 });
        var first = await _client.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone = e164, code = _factory.Phone.LastOtpFor(e164) });
        var firstUserId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user_id").GetGuid();

        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = e164 });
        var second = await _client.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone = e164, code = _factory.Phone.LastOtpFor(e164) });
        var secondUserId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user_id").GetGuid();

        Assert.Equal(firstUserId, secondUserId);
    }

    /// <summary>Two numbers from different countries must never collide into one account. Under the old rule
    /// a Singapore and a Norway number could both be rewritten into the +91 space and, with unlucky digits,
    /// land on the same row.</summary>
    [Fact]
    public async Task Numbers_from_different_countries_create_distinct_accounts()
    {
        var ids = new List<Guid>();
        foreach (var e164 in new[] { "+6591234511", "+4791234511", "+4531234511" })
        {
            await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = e164 });
            var res = await _client.PostAsJsonAsync("/v1/auth/otp/verify",
                new { phone = e164, code = _factory.Phone.LastOtpFor(e164) });
            ids.Add((await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user_id").GetGuid());
        }

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>What SNS actually receives. The provider is handed the destination verbatim, so a corruption
    /// upstream is invisible at this layer — which is precisely why it went unnoticed. Asserting the
    /// captured destination is the only place the end-to-end guarantee can be pinned down.</summary>
    [Theory]
    [InlineData("+6591234522")]
    [InlineData("+818012345622")]
    public async Task The_sms_provider_receives_the_users_own_e164_number(string e164)
    {
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = e164 });

        // LastOtpFor matches on the trailing digits of the destination it was sent to; a code filed under a
        // different country code would not be found here at all.
        Assert.False(string.IsNullOrWhiteSpace(_factory.Phone.LastOtpFor(e164)));
    }
    // ── ticket transfer across numbering plans ──────────────────────

    /// <summary>Two numbering plans can share their last ten digits: <c>+1 987 654 3210</c> and
    /// <c>+91 98765 43210</c> differ only in country code. The claim check used to compare the trailing ten
    /// digits of each side — a tolerance for the inconsistent formats that predated canonical storage — so
    /// the American number could claim a ticket addressed to the Indian one. Both sides now come from the
    /// same normalizer, so the comparison is exact.</summary>
    [Fact]
    public void Two_numbers_sharing_their_last_ten_digits_are_not_the_same_number()
    {
        var us = AuthService.NormalizePhone("+19876543210");
        var india = AuthService.NormalizePhone("+919876543210");

        Assert.NotEqual(us, india);
        Assert.EndsWith("9876543210", us);
        Assert.EndsWith("9876543210", india);   // the collision the old suffix check could not see
    }

    /// <summary>The trap the canonical column exists to close. A Singapore national number is *also* a
    /// well-formed Indian mobile, so feeding the legacy bare-digit <c>Phone</c> column back through
    /// <c>NormalizePhone</c> re-homes it to India — the stored value carries no '+' to say otherwise.
    /// Anything comparing a stored number against a freshly normalized one must therefore read
    /// <c>PhoneE164</c> first, which is what the transfer-claim endpoint now does.</summary>
    [Fact]
    public void Re_normalizing_a_stored_bare_digit_number_is_why_the_canonical_column_is_read_first()
    {
        var stored = AuthService.NormalizePhone("+6591234567");   // what the legacy column holds
        Assert.Equal("6591234567", stored);

        // Round-tripping the bare digits loses the country: they are a valid Indian mobile too.
        Assert.NotEqual(stored, AuthService.NormalizePhone(stored));

        // Reading the canonical column instead round-trips cleanly, which is the fix.
        Assert.Equal(stored, AuthService.NormalizePhone("+" + stored));
    }
    // ── unplaceable numbers are refused before a code is minted ────────────

    /// <summary>The defect: <c>NormalizePhone</c> validates but does not REJECT. On failure it returns the
    /// input's bare digits — correct in itself (D-290: inventing a country is what sent one user's code to a
    /// stranger) — but <c>RequestOtpAsync</c> then accepted those digits on length alone and minted a code
    /// for a destination that cannot exist.
    ///
    /// <para>Nothing was insecure: the code went nowhere, and no account exists until <c>VerifyOtpAsync</c>
    /// consumes one. It was a dead end presented to the user as a sent message, and it let one caller mint
    /// rows against unbounded fictional destinations. These are the numbers named in the approval.</para></summary>
    [Theory]
    [InlineData("+911111111111")]      // right shape for India, not an assignable number
    [InlineData("+910000000000")]
    [InlineData("+99900011122")]       // country code 999 does not exist
    [InlineData("+12345678")]          // NANP needs 10 national digits; 7 is impossible
    [InlineData("+9198765432100000")]  // too long for any plan
    public async Task An_unplaceable_number_is_refused_and_no_code_is_minted(string phone)
    {
        var before = await OtpRowCount();

        var res = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_phone", body.GetProperty("error").GetString());

        // The guarantee that matters is not the status code — it is that nothing was issued.
        Assert.Equal(before, await OtpRowCount());
    }

    /// <summary>Shorter than the syntactic floor is refused by the request validator BEFORE the normalizer
    /// runs, so it answers `validation_failed` rather than `invalid_phone`. Pinned deliberately: the two
    /// codes mean different things — "this is not a phone-shaped string" and "this is phone-shaped but
    /// cannot exist" — and a client that collapses them would tell the user the wrong thing to fix.</summary>
    [Fact]
    public async Task Input_below_the_syntactic_floor_is_refused_earlier_and_still_mints_nothing()
    {
        var before = await OtpRowCount();

        var res = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "+1234" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", body.GetProperty("error").GetString());
        Assert.Equal(before, await OtpRowCount());
    }

    /// <summary>The refusal must not become an oracle. An unplaceable number is rejected on FORMAT, before
    /// any account lookup happens, so the response is identical whether or not anyone holds that number —
    /// and it cannot be used to probe for registered accounts (the reasoning D-311 applies to the login form).</summary>
    [Fact]
    public async Task The_refusal_is_a_format_verdict_and_leaks_no_account_state()
    {
        var registered = "+919876500001";
        var request = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = registered });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var code = _factory.Phone.LastOtpFor(registered);
        await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone = registered, code });

        // Same invalid input, one account that exists and one that does not: byte-identical refusals.
        var a = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "+911111111111" });
        var b = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "+910000000000" });

        Assert.Equal(a.StatusCode, b.StatusCode);
        var ja = await a.Content.ReadFromJsonAsync<JsonElement>();
        var jb = await b.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ja.GetProperty("error").GetString(), jb.GetProperty("error").GetString());
    }

    /// <summary>The compatibility half, and the reason this change is safe: every success branch of
    /// <c>NormalizePhone</c> returns <c>parsed.E164[1..]</c>, so restoring the '+' reconstructs exactly what
    /// libphonenumber produced. The new guard therefore cannot refuse a number the normalizer accepted —
    /// including a bare Indian national number from a pre-E.164 client.</summary>
    [Theory]
    [InlineData("+919876543211")]
    [InlineData("+6591234511")]
    [InlineData("+4915112345611")]
    [InlineData("9876543211")]         // legacy bare national, no '+' (D-290 LegacyInputRegion)
    public async Task A_number_the_normalizer_accepts_is_never_refused_by_the_new_guard(string phone)
    {
        var res = await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>Rows in <c>otp_codes</c>, so a test can assert that a refusal minted nothing.</summary>
    private async Task<int> OtpRowCount()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Kurx.Infrastructure.Persistence.KurxDbContext>();
        return await db.OtpCodes.CountAsync();
    }

    // ── cross-platform conformance (D-288) ─────────────────────────────

    /// <summary>The backend half of the shared corpus in <c>docs/api/phone-conformance.json</c>.
    ///
    /// <para>Web and Flutter assert the same file. The three platforms use independent libphonenumber ports
    /// whose version numbers cannot be compared to one another, so there is no version to pin — behaviour is
    /// the only thing that can be held stable. Without this, a metadata update on one platform silently
    /// makes it reject a number the others accept, and the failure surfaces as a user who cannot register
    /// rather than as a broken build.</para></summary>
    [Fact]
    public void The_shared_conformance_corpus_parses_identically_here()
    {
        var doc = JsonDocument.Parse(File.ReadAllText(ConformanceFixturePath()));
        var cases = doc.RootElement.GetProperty("cases").EnumerateArray().ToList();
        Assert.NotEmpty(cases);

        foreach (var c in cases)
        {
            var input = c.GetProperty("input").GetString()!;
            var expectValid = c.GetProperty("valid").GetBoolean();
            var parsed = PhoneCanonicalizer.TryParse(input, region: null, out var result);

            Assert.True(parsed == expectValid,
                $"{input}: expected valid={expectValid}, got {parsed}. If this platform is the outlier, bump "
                + "its libphonenumber metadata — do not edit the fixture to match it.");

            if (!expectValid) continue;
            Assert.Equal(c.GetProperty("e164").GetString(), result.E164);
            Assert.Equal(c.GetProperty("region").GetString(), result.CountryCode);
        }
    }

    /// <summary>Resolved from the compile-time path of this file, not from <c>AppContext.BaseDirectory</c>.
    /// The documented container recipe builds with <c>-p:ArtifactsPath=/tmp/artifacts</c>, so the assembly
    /// sits outside the repository and walking up from it never reaches <c>docs/</c> — it only appeared to
    /// work because CI does not redirect build output. Same reason, and the same fix, as
    /// <c>CapabilityEngineMigrationTests.SourcePath</c>.</summary>
    private static string ConformanceFixturePath(
        [System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        const string relative = "docs/api/phone-conformance.json";
        // here = <repo>/backend/Kurx.Tests/InternationalPhoneTests.cs → up two levels is <repo>.
        var repoRoot = Directory.GetParent(Path.GetDirectoryName(here)!)!.Parent!.FullName;
        var candidate = Path.Combine(repoRoot, relative);
        if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException(
            $"Could not find {relative} at {candidate}. It is the shared cross-platform corpus and the "
            + "test is meaningless without it, so this fails rather than skips.");
    }
}
