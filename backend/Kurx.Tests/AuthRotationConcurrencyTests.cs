using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kurx.Tests;

/// <summary>Refresh-token rotation is SINGLE-USE (D-009/D-014/D-240): one token mints at most one new
/// chain, no matter how the callers interleave.
///
/// <para><b>The gap this closes.</b> <c>AuthTests.Refresh_rotates_and_reuse_revokes_all_sessions</c>
/// already covers rotation and reuse, but sequentially — it presents the old token only after the new
/// one has been issued, which is the path the <c>RevokedAt</c> read at the top of
/// <c>AuthService.RefreshAsync</c> already handled. The untested path was two callers presenting the
/// SAME token at once: both passed that read before either wrote, and both minted a chain. Measured
/// before the fix: two parallel <c>POST /v1/auth/refresh</c> calls with one token BOTH returned 200,
/// so a stolen token could be forked into a parallel live session and reuse detection never fired —
/// defeating the entire point of rotation.</para>
///
/// <para>The loser is refused but its family is deliberately NOT revoked (D-240): a client firing two
/// refreshes at once is ordinary behaviour, not proof of theft. The sequential-reuse test above still
/// pins the revoke-everything response for real reuse.</para></summary>
public class AuthRotationConcurrencyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public AuthRotationConcurrencyTests(KurxApiFactory factory)
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
    public async Task Two_parallel_refreshes_of_one_token_mint_exactly_one_chain()
    {
        var refresh = await LoginAsync("9211000001");

        var responses = await Task.WhenAll(
            _factory.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh }),
            _factory.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh }));

        var winners = responses.Where(r => r.IsSuccessStatusCode).ToList();
        Assert.Single(winners);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);

        // The winner's chain is live and still rotatable — losing the race must not have collaterally
        // killed the session the winner just took ownership of.
        var issued = await winners[0].Content.ReadFromJsonAsync<JsonElement>();
        var next = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = issued.GetProperty("refresh_token").GetString() });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Losing_a_rotation_race_does_not_revoke_the_session_family()
    {
        var refresh = await LoginAsync("9211000002");

        var responses = await Task.WhenAll(
            _factory.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh }),
            _factory.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh }));

        var winner = Assert.Single(responses.Where(r => r.IsSuccessStatusCode));
        var issued = await winner.Content.ReadFromJsonAsync<JsonElement>();

        // D-240: the race loser is refused, never treated as theft. If it had triggered reuse detection
        // the whole family would be revoked and this access token would already be worthless.
        var me = _factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", issued.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync("/v1/me")).StatusCode);
    }

    private async Task<string> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var login = await (await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return login.GetProperty("refresh_token").GetString()!;
    }
}
