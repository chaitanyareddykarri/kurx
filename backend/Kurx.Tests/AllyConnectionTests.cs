using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-201 — Ally connections: the request/accept/decline/revoke state machine, canonical-pair
/// reactivation, the crossed-request auto-accept, authorization (404 for non-parties, 409 for wrong-state
/// actions on a party), the DB-level pair-order check constraint, and privacy (ShowAllies +
/// both-ProfilePublic gating on the public list). Real HTTP + kurx_test.</summary>
public class AllyConnectionTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public AllyConnectionTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9197{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private static Task<HttpResponseMessage> Request(HttpClient client, Guid targetUserId) =>
        client.PostAsJsonAsync("/v1/allies/requests", new { targetUserId });

    [Fact]
    public async Task Request_then_accept_becomes_an_accepted_connection_for_both_sides()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        var req = await Request(a, bId);
        Assert.Equal(HttpStatusCode.OK, req.StatusCode);
        var reqBody = await Json(req);
        Assert.Equal("Pending", reqBody.GetProperty("status").GetString());
        var connectionId = reqBody.GetProperty("id").GetGuid();

        var incoming = await Json(await b.GetAsync("/v1/allies/requests/incoming"));
        Assert.Contains(incoming.EnumerateArray(), r => r.GetProperty("id").GetGuid() == connectionId);

        var accept = await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal("Accepted", (await Json(accept)).GetProperty("status").GetString());

        var mineA = await Json(await a.GetAsync("/v1/me/allies"));
        var mineB = await Json(await b.GetAsync("/v1/me/allies"));
        Assert.Contains(mineA.EnumerateArray(), r => r.GetProperty("other_user_id").GetGuid() == bId);
        Assert.Contains(mineB.EnumerateArray(), r => r.GetProperty("other_user_id").GetGuid() == aId);
    }

    [Fact]
    public async Task Duplicate_outgoing_request_is_idempotent_one_row_for_the_pair()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (_, bId) = await LoginAsync(NextPhone());

        var first = await Json(await Request(a, bId));
        var second = await Json(await Request(a, bId));
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal("Pending", second.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Crossed_requests_auto_accept_instead_of_erroring()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        await Request(a, bId);   // A asks B first
        var crossed = await Json(await Request(b, aId));   // B asks A before responding to A's request
        Assert.Equal("Accepted", crossed.GetProperty("status").GetString());
        Assert.Equal("mutual_request", (await UnderlyingRowAsync(aId, bId)).ConnectedVia);
    }

    [Fact]
    public async Task Decline_then_re_request_reactivates_the_same_row_not_a_new_one()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());

        var req = await Json(await Request(a, bId));
        var connectionId = req.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsync($"/v1/allies/requests/{connectionId}/decline", null)).StatusCode);

        // A declined requester must wait out the anti-spam cooldown before re-asking (BUG-C). Age the
        // decline rather than sleeping — this test is about ROW REACTIVATION, not the cooldown, which
        // has its own coverage in AllyPolicyConflictTests.
        using (var cooldownScope = _factory.Services.CreateScope())
        {
            var cooldownDb = cooldownScope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var declined = await cooldownDb.AllyConnections.FirstAsync(c => c.Id == connectionId);
            declined.RespondedAt = DateTimeOffset.UtcNow.AddDays(-31);
            await cooldownDb.SaveChangesAsync();
        }

        var reReq = await Json(await Request(a, bId));
        Assert.Equal(connectionId, reReq.GetProperty("id").GetGuid());
        Assert.Equal("Pending", reReq.GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var rowCount = await db.AllyConnections.CountAsync(c =>
            (c.UserLowId == aId || c.UserHighId == aId) && (c.UserLowId == bId || c.UserHighId == bId));
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task Either_party_can_revoke_an_accepted_connection()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var connectionId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        var revoke = await b.DeleteAsync($"/v1/allies/{connectionId}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal("Revoked", (await UnderlyingRowAsync(aId, bId)).Status.ToString());
    }

    [Fact]
    public async Task Non_party_gets_404_not_403_on_accept()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var (stranger, _) = await LoginAsync(NextPhone());
        var connectionId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();

        var res = await stranger.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Requester_cannot_accept_their_own_pending_request()
    {
        var (a, _) = await LoginAsync(NextPhone());
        var (_, bId) = await LoginAsync(NextPhone());
        var connectionId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();

        var res = await a.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Cannot_ally_self()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var res = await Request(a, aId);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Db_check_constraint_rejects_a_row_with_the_pair_reversed()
    {
        var (_, aId) = await LoginAsync(NextPhone());
        var (_, bId) = await LoginAsync(NextPhone());
        var (low, high) = aId.CompareTo(bId) < 0 ? (aId, bId) : (bId, aId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.AllyConnections.Add(new AllyConnection
        {
            UserLowId = high, UserHighId = low,   // reversed on purpose
            RequesterId = aId, AddresseeId = bId,
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ShowAllies_false_hides_the_public_allies_list()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var connectionId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        var username = "allyprof" + Guid.NewGuid().ToString("N")[..10];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == aId);
            user.Username = username;
            user.ProfilePublic = true;
            user.ShowAllies = false;
            await db.SaveChangesAsync();
        }

        var pub = _factory.CreateClient();
        var allies = await Json(await pub.GetAsync($"/v1/public/users/{username}/allies"));
        Assert.Empty(allies.EnumerateArray());
    }

    [Fact]
    public async Task An_ally_is_hidden_from_the_public_list_if_the_other_party_is_not_public()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var connectionId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();
        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        var username = "allyprof" + Guid.NewGuid().ToString("N")[..10];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var userA = await db.Users.FirstAsync(u => u.Id == aId);
            userA.Username = username;
            userA.ProfilePublic = true;
            var userB = await db.Users.FirstAsync(u => u.Id == bId);
            userB.ProfilePublic = false;   // B opted out of a public profile entirely
            await db.SaveChangesAsync();
        }

        var pub = _factory.CreateClient();
        var allies = await Json(await pub.GetAsync($"/v1/public/users/{username}/allies"));
        Assert.Empty(allies.EnumerateArray());   // the relationship is mutual, but B's own privacy still gates display
    }

    // ── Notification wiring ──────────────────────────────────────────────────

    [Fact]
    public async Task Request_and_accept_each_notify_the_other_party_with_a_route()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var connectionId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var requested = db.Notifications.AsNoTracking().Single(n => n.UserId == bId && n.Kind == "ally.requested");
            Assert.Contains("route", requested.DataJson);
        }

        await b.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.True(db.Notifications.AsNoTracking().Any(n => n.UserId == aId && n.Kind == "ally.accepted"));
        }
    }

    [Fact]
    public async Task Withdrawing_a_pending_request_does_not_notify_but_removing_an_accepted_ally_does()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (b, bId) = await LoginAsync(NextPhone());
        var pendingId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();
        await a.DeleteAsync($"/v1/allies/{pendingId}");   // withdraw while still pending

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.False(db.Notifications.AsNoTracking().Any(n => n.UserId == bId && n.Kind == "ally.removed"));
        }

        var (c, cId) = await LoginAsync(NextPhone());
        var (d, dId) = await LoginAsync(NextPhone());
        var acceptedId = (await Json(await Request(c, dId))).GetProperty("id").GetGuid();
        await d.PostAsync($"/v1/allies/requests/{acceptedId}/accept", null);
        await c.DeleteAsync($"/v1/allies/{acceptedId}");   // remove an established connection

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.True(db.Notifications.AsNoTracking().Any(n => n.UserId == dId && n.Kind == "ally.removed"));
        }
    }

    // ── Batch status, mutual detail, suggestions ─────────────────────────────

    [Fact]
    public async Task Batch_status_returns_the_right_state_for_each_target_in_one_call()
    {
        var (a, aId) = await LoginAsync(NextPhone());
        var (_, acceptedId) = await LoginAsync(NextPhone());
        var (incomingClient, incomingId) = await LoginAsync(NextPhone());
        var (_, noneId) = await LoginAsync(NextPhone());

        var acceptedConn = (await Json(await Request(a, acceptedId))).GetProperty("id").GetGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var row = await db.AllyConnections.FirstAsync(c => c.Id == acceptedConn);
            row.Status = AllyStatus.Accepted;
            await db.SaveChangesAsync();
        }
        await Request(incomingClient, aId);   // incomingId -> a, so from a's perspective this is "pending_incoming"

        var res = await a.PostAsJsonAsync("/v1/me/allies/status-batch",
            new { userIds = new[] { acceptedId, incomingId, noneId } });
        var body = await Json(res);
        Assert.Equal("accepted", body.GetProperty(acceptedId.ToString()).GetString());
        Assert.Equal("pending_incoming", body.GetProperty(incomingId.ToString()).GetString());
        Assert.Equal("none", body.GetProperty(noneId.ToString()).GetString());
    }

    /// <summary>D-211 — closes a release-audit finding: `status-batch` previously accepted an
    /// unbounded `userIds` array, unlike its sibling `suggestions` endpoint which already clamps
    /// its `limit`. 200 (the exact cap) must still succeed; 201 must be rejected before it reaches
    /// the database.</summary>
    [Fact]
    public async Task Batch_status_rejects_an_oversized_target_list()
    {
        var (a, _) = await LoginAsync(NextPhone());

        var atCap = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();
        var okRes = await a.PostAsJsonAsync("/v1/me/allies/status-batch", new { userIds = atCap });
        Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);

        var overCap = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray();
        var rejectedRes = await a.PostAsJsonAsync("/v1/me/allies/status-batch", new { userIds = overCap });
        Assert.Equal(HttpStatusCode.BadRequest, rejectedRes.StatusCode);
    }

    [Fact]
    public async Task Mutual_detail_returns_shared_events_and_orgs_for_an_accepted_ally()
    {
        var owner = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner.Client, "Mutual Detail College " + Guid.NewGuid().ToString("N")[..6]);
        var (a, aId) = await LoginAsync(NextPhone());
        var (bClient, bId) = await LoginAsync(NextPhone());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.Memberships.AddRange(
                new Membership { OrgId = orgId, UserId = aId, Role = OrgRole.Staff, ShowOnProfile = true },
                new Membership { OrgId = orgId, UserId = bId, Role = OrgRole.Staff, ShowOnProfile = true });
            await db.SaveChangesAsync();
        }
        var connId = (await Json(await Request(a, bId))).GetProperty("id").GetGuid();
        await bClient.PostAsync($"/v1/allies/requests/{connId}/accept", null);

        var detail = await Json(await a.GetAsync($"/v1/me/allies/mutual/{bId}"));
        var orgs = detail.GetProperty("shared_orgs").EnumerateArray().ToList();
        Assert.Contains(orgs, o => o.GetProperty("id").GetGuid() == orgId);
    }

    /// <summary>D-211 — closes a release-audit finding: without an existing Accepted connection,
    /// mutual-detail must not disclose a stranger's shared events/orgs by GUID alone, regardless of
    /// that stranger's own privacy settings. Covers non-ally, private-profile-non-ally,
    /// public-profile-non-ally, and a nonexistent target — all must come back empty, not an error
    /// (maintains the existing 200-with-empty-arrays contract rather than leaking existence via a
    /// different status code).</summary>
    [Fact]
    public async Task Mutual_detail_is_empty_for_non_allies_regardless_of_the_targets_profile_visibility()
    {
        var owner = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner.Client, "Mutual Privacy College " + Guid.NewGuid().ToString("N")[..6]);
        var (a, aId) = await LoginAsync(NextPhone());
        var (_, publicStrangerId) = await LoginAsync(NextPhone());
        var (_, privateStrangerId) = await LoginAsync(NextPhone());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.Memberships.AddRange(
                new Membership { OrgId = orgId, UserId = aId, Role = OrgRole.Staff, ShowOnProfile = true },
                new Membership { OrgId = orgId, UserId = publicStrangerId, Role = OrgRole.Staff, ShowOnProfile = true },
                new Membership { OrgId = orgId, UserId = privateStrangerId, Role = OrgRole.Staff, ShowOnProfile = true });
            var pub = await db.Users.FirstAsync(u => u.Id == publicStrangerId);
            pub.ProfilePublic = true;
            var priv = await db.Users.FirstAsync(u => u.Id == privateStrangerId);
            priv.ProfilePublic = false;
            await db.SaveChangesAsync();
        }

        // Not allied with either — a shared org exists for both, but neither is disclosed.
        var againstPublic = await Json(await a.GetAsync($"/v1/me/allies/mutual/{publicStrangerId}"));
        Assert.Empty(againstPublic.GetProperty("shared_orgs").EnumerateArray());
        var againstPrivate = await Json(await a.GetAsync($"/v1/me/allies/mutual/{privateStrangerId}"));
        Assert.Empty(againstPrivate.GetProperty("shared_orgs").EnumerateArray());

        // A nonexistent user ID must also come back empty, not an error — no existence oracle.
        var againstNobody = await Json(await a.GetAsync($"/v1/me/allies/mutual/{Guid.NewGuid()}"));
        Assert.Empty(againstNobody.GetProperty("shared_orgs").EnumerateArray());
    }

    [Fact]
    public async Task Suggestions_rank_by_shared_signals_and_exclude_existing_connections()
    {
        var (owner, _) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Suggest College " + Guid.NewGuid().ToString("N")[..6]);
        var (a, aId) = await LoginAsync(NextPhone());
        var (_, strongId) = await LoginAsync(NextPhone());          // shares the org -> should be suggested
        var (alreadyClient, alreadyId) = await LoginAsync(NextPhone()); // already an ally -> must be excluded

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.Memberships.AddRange(
                new Membership { OrgId = orgId, UserId = aId, Role = OrgRole.Staff, ShowOnProfile = true },
                new Membership { OrgId = orgId, UserId = strongId, Role = OrgRole.Staff, ShowOnProfile = true },
                new Membership { OrgId = orgId, UserId = alreadyId, Role = OrgRole.Staff, ShowOnProfile = true });
            await db.SaveChangesAsync();
        }
        var connId = (await Json(await Request(a, alreadyId))).GetProperty("id").GetGuid();
        await alreadyClient.PostAsync($"/v1/allies/requests/{connId}/accept", null);

        var suggestions = await Json(await a.GetAsync("/v1/me/allies/suggestions"));
        var ids = suggestions.EnumerateArray().Select(s => s.GetProperty("user_id").GetGuid()).ToList();
        Assert.Contains(strongId, ids);
        Assert.DoesNotContain(alreadyId, ids);
    }

    private async Task<AllyConnection> UnderlyingRowAsync(Guid userA, Guid userB)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.AllyConnections.AsNoTracking().SingleAsync(c =>
            (c.UserLowId == userA || c.UserHighId == userA) && (c.UserLowId == userB || c.UserHighId == userB));
    }
}
