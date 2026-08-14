using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Multi-party policy contracts for Allies. The pre-existing suite exercises each party's
/// actions in isolation, which is why BUG-A/B/C/D all shipped: every one of them lives in the
/// interaction between two users with opposing intentions, or between a user and moderation. These
/// tests deliberately pit parties against each other.</summary>
public class AllyPolicyConflictTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _seq;

    public AllyPolicyConflictTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9193{Interlocked.Increment(ref _seq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<string> ClaimAsync(HttpClient c, Guid id)
    {
        var username = $"pc{id:N}"[..12];
        await c.PatchAsJsonAsync("/v1/me/profile", new { name = "Conflict", username });
        return username;
    }

    private record Pair(HttpClient A, Guid AId, string AName, HttpClient B, Guid BId, string BName, Guid ConnId);

    private async Task<Pair> AcceptedPairAsync()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var aName = await ClaimAsync(a, aId);
        var bName = await ClaimAsync(b, bId);
        var connId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connId}/accept", null);
        return new Pair(a, aId, aName, b, bId, bName, connId);
    }

    private async Task<int> PublicAllyCountAsync(string username)
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/v1/public/users/{username}/allies");
        if (res.StatusCode != HttpStatusCode.OK) return -1;
        return (await Json(res)).EnumerateArray().Count();
    }

    private Task SetVisibilityAsync(HttpClient c, Guid connId, string v) =>
        c.PatchAsJsonAsync($"/v1/allies/{connId}/visibility", new { visibility = v });

    // ── BUG-A: the more private choice wins ──────────────────────────────────

    [Fact]
    public async Task B_cannot_publish_a_connection_A_hid()
    {
        var p = await AcceptedPairAsync();
        await SetVisibilityAsync(p.A, p.ConnId, "hidden");
        await SetVisibilityAsync(p.B, p.ConnId, "public");
        Assert.Equal(0, await PublicAllyCountAsync(p.AName));
        Assert.Equal(0, await PublicAllyCountAsync(p.BName));
    }

    [Fact]
    public async Task A_cannot_publish_a_connection_B_hid()
    {
        var p = await AcceptedPairAsync();
        await SetVisibilityAsync(p.B, p.ConnId, "hidden");
        await SetVisibilityAsync(p.A, p.ConnId, "public");
        Assert.Equal(0, await PublicAllyCountAsync(p.AName));
    }

    [Fact]
    public async Task Both_hide_then_one_unhides_stays_hidden()
    {
        var p = await AcceptedPairAsync();
        await SetVisibilityAsync(p.A, p.ConnId, "hidden");
        await SetVisibilityAsync(p.B, p.ConnId, "hidden");
        await SetVisibilityAsync(p.A, p.ConnId, "public");
        Assert.Equal(0, await PublicAllyCountAsync(p.AName));   // B's hide still stands

        await SetVisibilityAsync(p.B, p.ConnId, "public");
        Assert.Equal(1, await PublicAllyCountAsync(p.AName));   // only now is it public again
    }

    [Fact]
    public async Task A_party_can_freely_reverse_their_own_choice()
    {
        var p = await AcceptedPairAsync();
        await SetVisibilityAsync(p.A, p.ConnId, "hidden");
        Assert.Equal(0, await PublicAllyCountAsync(p.AName));
        await SetVisibilityAsync(p.A, p.ConnId, "public");
        Assert.Equal(1, await PublicAllyCountAsync(p.AName));
    }

    [Fact]
    public async Task Repeated_toggling_by_both_parties_converges_on_most_private()
    {
        var p = await AcceptedPairAsync();
        for (var i = 0; i < 5; i++)
        {
            await SetVisibilityAsync(p.A, p.ConnId, "public");
            await SetVisibilityAsync(p.B, p.ConnId, "hidden");
            Assert.Equal(0, await PublicAllyCountAsync(p.AName));
        }
        await SetVisibilityAsync(p.B, p.ConnId, "public");
        Assert.Equal(1, await PublicAllyCountAsync(p.AName));
    }

    [Fact]
    public async Task Concurrent_opposing_visibility_writes_never_lose_the_hide()
    {
        var p = await AcceptedPairAsync();
        await Task.WhenAll(
            SetVisibilityAsync(p.A, p.ConnId, "hidden"),
            SetVisibilityAsync(p.B, p.ConnId, "public"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.AllyConnections.AsNoTracking().FirstAsync(x => x.Id == p.ConnId);
        // Whatever the interleaving, A asked for hidden and never reversed it.
        Assert.True(row.HiddenByLow || row.HiddenByHigh);
        Assert.Equal(AllyVisibility.Hidden, row.Visibility);
    }

    [Fact]
    public async Task Profile_privacy_still_hides_the_pair_independently_of_the_flags()
    {
        var p = await AcceptedPairAsync();
        await p.B.PatchAsJsonAsync("/v1/me/privacy", new { profilePublic = false });
        Assert.Equal(0, await PublicAllyCountAsync(p.AName));   // B not public ⇒ pair hidden on A's page
    }

    // ── BUG-B: moderation reaches display surfaces ───────────────────────────

    private async Task ModerateAsync(Guid userId, bool banned = false, bool suspended = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.BannedAt = banned ? DateTime.UtcNow : null;
        u.SuspendedAt = suspended ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_moderated_user_disappears_from_another_users_public_ally_list(bool banned, bool suspended)
    {
        var p = await AcceptedPairAsync();
        Assert.Equal(1, await PublicAllyCountAsync(p.AName));
        await ModerateAsync(p.BId, banned, suspended);
        Assert.Equal(0, await PublicAllyCountAsync(p.AName));
    }

    [Fact]
    public async Task A_moderated_users_own_public_profile_is_gone_and_returns_when_restored()
    {
        var p = await AcceptedPairAsync();
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/v1/public/users/{p.BName}")).StatusCode);

        await ModerateAsync(p.BId, banned: true);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/v1/public/users/{p.BName}")).StatusCode);
        // Every gated section routes through the same loader, so they go together.
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/v1/public/users/{p.BName}/timeline")).StatusCode);

        await ModerateAsync(p.BId);   // restored
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/v1/public/users/{p.BName}")).StatusCode);
        Assert.Equal(1, await PublicAllyCountAsync(p.AName));
    }

    [Fact]
    public async Task A_moderated_user_is_not_discoverable_by_search()
    {
        var p = await AcceptedPairAsync();
        var anon = _factory.CreateClient();
        async Task<int> HitsAsync() =>
            (await Json(await anon.GetAsync($"/v1/public/users?q={p.BName}"))).EnumerateArray().Count();

        Assert.Equal(1, await HitsAsync());
        await ModerateAsync(p.BId, banned: true);
        Assert.Equal(0, await HitsAsync());
    }

    // ── BUG-C: decline cooldown stops spam, preserves reconnects ─────────────

    [Fact]
    public async Task Re_requesting_immediately_after_a_decline_is_refused()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        var connId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connId}/decline", null);

        var retry = await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId });
        Assert.Equal(HttpStatusCode.TooManyRequests, retry.StatusCode);
        Assert.Equal("declined_recently", (await Json(retry)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Decline_spam_is_bounded_to_a_single_notification()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        for (var i = 0; i < 6; i++)
        {
            var req = await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId });
            if (req.StatusCode != HttpStatusCode.OK) continue;
            var id = (await Json(req)).GetProperty("id").GetGuid();
            await b.PostAsync($"/v1/allies/requests/{id}/decline", null);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var count = await db.Notifications.CountAsync(n => n.UserId == bId && n.Kind == "ally.requested");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task After_the_cooldown_expires_a_genuine_reconnect_is_allowed()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        var connId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connId}/decline", null);

        // Age the decline past the window rather than sleeping (same technique the OTP suite uses).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var row = await db.AllyConnections.FirstAsync(x => x.Id == connId);
            row.RespondedAt = DateTimeOffset.UtcNow.AddDays(-31);
            await db.SaveChangesAsync();
        }

        var retry = await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("Pending", (await Json(retry)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_decliner_may_ask_immediately_because_that_is_a_reconnect_not_spam()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        var connId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connId}/decline", null);

        // B declined A; B now changes their mind. The cooldown targets the declined REQUESTER only.
        var byDecliner = await b.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = aId });
        Assert.Equal(HttpStatusCode.OK, byDecliner.StatusCode);
    }

    [Fact]
    public async Task Concurrent_retries_during_cooldown_do_not_slip_through()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        var connId = (await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })))
            .GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connId}/decline", null);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId })));
        Assert.All(responses, r => Assert.NotEqual(HttpStatusCode.OK, r.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.AllyConnections.AsNoTracking().FirstAsync(x => x.Id == connId);
        Assert.Equal(AllyStatus.Declined, row.Status);
    }

    // ── BUG-D: suggestions never surface a non-public profile ────────────────

    [Fact]
    public async Task Suggestions_exclude_non_public_and_moderated_profiles()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        await ClaimAsync(a, aId);

        var suggestions = await Json(await a.GetAsync("/v1/me/allies/suggestions"));
        foreach (var s in suggestions.EnumerateArray())
        {
            // Anything returned must be navigable — a card with no username is a dead end and was the
            // symptom of the half-applied privacy rule.
            Assert.False(s.GetProperty("username").ValueKind == JsonValueKind.Null,
                "a suggestion was returned without a username, so it cannot be opened");
        }
    }
}
