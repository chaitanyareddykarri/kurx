using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kurx.Tests;

/// <summary>The OTP attempt cap and single-use guarantee must hold under parallelism, not just in
/// sequence.
///
/// <para><b>The gap this closes.</b> <c>OtpService.VerifyAsync</c> read <c>VerifyAttempts</c>, compared
/// it to <c>MaxVerifyAttempts</c>, then incremented in memory and saved. Sequential tests pass against
/// that, because each request observes the previous one's write. Parallel requests do not: they all read
/// the same count and all write count+1, so the counter advanced roughly once per BATCH instead of once
/// per guess. A 5-attempt cap over a 6-digit keyspace only bounds anything if every guess is actually
/// counted — with enough concurrency it stopped bounding at all, and the cap is the ONLY thing standing
/// between an attacker and 10^6 tries.</para>
///
/// <para><c>Consumed</c> was assigned the same way, so two callers presenting the same correct code at
/// once could both be told they succeeded — one OTP, two sessions.</para>
///
/// <para>Both are now conditional <c>ExecuteUpdateAsync</c> claims, the shape
/// <c>ChallengeService.VerifyMatchNumberAsync</c> already uses for its own match-number cap.</para></summary>
public class OtpVerifyConcurrencyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public OtpVerifyConcurrencyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    [Fact]
    public async Task Parallel_wrong_guesses_cannot_outrun_the_attempt_cap()
    {
        const string phone = "9212000001";
        var correct = await RequestOtpAsync(phone);

        // Twelve at once against a cap of five. Before the fix these mostly shared one read of the
        // counter, so it landed far below the cap and the code stayed guessable afterwards.
        var wrong = WrongCodeFor(correct);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            _factory.CreateClient().PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code = wrong })));

        // The cap is spent, so even the CORRECT code must now be refused. This is the assertion that
        // fails on the old implementation: the counter had not really reached five, so this returned 200.
        var afterCap = await _factory.CreateClient()
            .PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code = correct });
        Assert.Equal(HttpStatusCode.Unauthorized, afterCap.StatusCode);

        var problem = await afterCap.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("too_many_attempts", problem.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Two_parallel_verifications_of_one_code_succeed_exactly_once()
    {
        const string phone = "9212000002";
        var correct = await RequestOtpAsync(phone);

        var responses = await Task.WhenAll(
            _factory.CreateClient().PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code = correct }),
            _factory.CreateClient().PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code = correct }));

        // Single-use: one session, not two. The loser is refused because the code is already spent.
        Assert.Single(responses, r => r.IsSuccessStatusCode);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
    }

    private async Task<string> RequestOtpAsync(string phone)
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return _factory.WhatsApp.LastOtpFor(phone);
    }

    /// <summary>Any six digits that are not the real code — derived from it so it can never collide.</summary>
    private static string WrongCodeFor(string correct)
    {
        var shifted = (int.Parse(correct) + 1) % 1_000_000;
        return shifted.ToString("D6");
    }
}
