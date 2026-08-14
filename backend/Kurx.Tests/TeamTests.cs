using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §6 (Phase 10) — the Team subsystem: the only group entity, and only where competition exists.
/// Formation (create/roster/invite/join-request/substitute), organiser lifecycle + merge/split, and TeamPolicy.
/// Additive — the purchase Group flow and the Phase-9 money path are untouched.</summary>
public class TeamTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public TeamTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private ITeamService Svc(IServiceScope s) => s.ServiceProvider.GetRequiredService<ITeamService>();

    // ── TeamPolicy ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creating_a_competition_ticket_type_syncs_a_team_policy()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006001", "Team Policy Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 2, max: 4);

        using var scope = _factory.Services.CreateScope();
        var pol = await scope.ServiceProvider.GetRequiredService<KurxDbContext>().TeamPolicies.SingleAsync(p => p.TicketTypeId == ttId);
        Assert.Equal(2, pol.MinSize);
        Assert.Equal(4, pol.MaxSize);
        Assert.Equal(TeamFormationMode.InviteOnly, pol.FormationMode);
    }

    [Fact]
    public async Task Non_competition_ticket_type_has_no_team_policy_and_rejects_teams()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006002", "No Comp Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, userId) = await LoginAsync("9700006003");

        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<KurxDbContext>().TeamPolicies.AnyAsync(p => p.TicketTypeId == ttId));
        var r = await Svc(scope).CreateTeamAsync(userId, eventId, ttId, new TeamInput("Nope"));
        Assert.False(r.Ok);
        Assert.Equal("not_a_competition", r.Error);
    }

    [Fact]
    public async Task Set_policy_requires_manage_and_validates()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700006004", "Set Policy Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId);
        var (_, strangerId) = await LoginAsync("9700006005");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        Assert.False((await svc.SetPolicyAsync(strangerId, eventId, ttId, false, EmptyPolicy() with { MinSize = 3 }, default)).Ok);   // forbidden
        Assert.False((await svc.SetPolicyAsync(ownerId, eventId, ttId, false, EmptyPolicy() with { MinSize = 5, MaxSize = 2 }, default)).Ok);   // invalid_size
        var ok = await svc.SetPolicyAsync(ownerId, eventId, ttId, false, EmptyPolicy() with { JoinApproval = "Organiser", SubstitutesAllowed = 2 }, default);
        Assert.True(ok.Ok);
        Assert.Equal("Organiser", ok.Value!.JoinApproval);
        Assert.Equal(2, ok.Value!.SubstitutesAllowed);
    }

    // ── Formation ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_team_makes_the_creator_captain()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006010", "Create Team Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId);
        var (_, captainId) = await LoginAsync("9700006011");

        using var scope = _factory.Services.CreateScope();
        var r = await Svc(scope).CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Alpha Squad"));
        Assert.True(r.Ok);
        Assert.Equal("Forming", r.Value!.State);
        Assert.Equal("alpha-squad", r.Value!.Slug);
        var captain = Assert.Single(r.Value!.Members);
        Assert.Equal(captainId, captain.PersonId);
        Assert.Equal("Captain", captain.Role);
    }

    [Fact]
    public async Task Invite_and_accept_adds_a_member_and_completes_the_team()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006012", "Invite Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 2, max: 4);
        var (_, captainId) = await LoginAsync("9700006013");
        var (_, memberId) = await LoginAsync("9700006014");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Beta"))).Value!;
        var invite = await svc.InviteAsync(captainId, team.Id, new TeamInviteInput(PersonId: memberId));
        Assert.True(invite.Ok);
        var accepted = await svc.AcceptInviteAsync(memberId, invite.Value!.Token);
        Assert.True(accepted.Ok);
        Assert.Equal(2, accepted.Value!.ActiveMemberCount);
        Assert.Equal("Complete", accepted.Value!.State);   // reached min_size (2)
    }

    [Fact]
    public async Task Join_request_is_decided_by_the_captain()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700006015", "Join Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 2, max: 4);
        var (_, captainId) = await LoginAsync("9700006016");
        var (_, joinerId) = await LoginAsync("9700006017");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        await svc.SetPolicyAsync(ownerId, eventId, ttId, false, EmptyPolicy() with { FormationMode = "Open", JoinApproval = "Captain" }, default);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Gamma"))).Value!;

        var req = await svc.RequestJoinAsync(joinerId, team.Id, "let me in");
        Assert.True(req.Ok);
        Assert.Equal("Pending", req.Value!.State);
        Assert.True((await svc.DecideJoinRequestAsync(captainId, req.Value!.Id, approve: true, isAdmin: false)).Ok);
        Assert.Equal(2, (await svc.GetTeamAsync(team.Id)).Value!.ActiveMemberCount);
    }

    [Fact]
    public async Task Team_fills_at_max_size()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006018", "Full Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 2);
        var (_, captainId) = await LoginAsync("9700006019");
        var (_, m1) = await LoginAsync("9700006020");
        var (_, m2) = await LoginAsync("9700006021");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Delta"))).Value!;
        var i1 = await svc.InviteAsync(captainId, team.Id, new TeamInviteInput(PersonId: m1));
        Assert.True((await svc.AcceptInviteAsync(m1, i1.Value!.Token)).Ok);   // now 2/2 (full)
        var i2 = await svc.InviteAsync(captainId, team.Id, new TeamInviteInput(PersonId: m2));
        Assert.False(i2.Ok);   // team already full
        Assert.Equal("team_full", i2.Error);
    }

    [Fact]
    public async Task A_person_cannot_hold_two_teams_in_one_event()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006022", "One Team Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId);
        var (_, personId) = await LoginAsync("9700006023");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        Assert.True((await svc.CreateTeamAsync(personId, eventId, ttId, new TeamInput("First"))).Ok);
        var second = await svc.CreateTeamAsync(personId, eventId, ttId, new TeamInput("Second"));
        Assert.False(second.Ok);
        Assert.Equal("team_limit_reached", second.Error);
    }

    [Fact]
    public async Task Leave_and_remove_close_memberships_as_history()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006024", "Leave Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 4);
        var (_, captainId) = await LoginAsync("9700006025");
        var (_, m1) = await LoginAsync("9700006026");
        var (_, m2) = await LoginAsync("9700006027");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Epsilon"))).Value!;
        foreach (var m in new[] { m1, m2 })
            Assert.True((await svc.AcceptInviteAsync(m, (await svc.InviteAsync(captainId, team.Id, new TeamInviteInput(PersonId: m))).Value!.Token)).Ok);

        Assert.True((await svc.LeaveAsync(m1, team.Id)).Ok);                 // member leaves
        var m2Membership = (await svc.GetTeamAsync(team.Id)).Value!.Members.Single(x => x.PersonId == m2).MembershipId;
        Assert.True((await svc.RemoveMemberAsync(captainId, team.Id, m2Membership, false)).Ok);   // captain removes m2
        var after = (await svc.GetTeamAsync(team.Id)).Value!;
        Assert.Equal(1, after.ActiveMemberCount);                           // only the captain remains active
        Assert.Contains(after.Members, x => x.PersonId == m1 && x.State == "Left");
        Assert.Contains(after.Members, x => x.PersonId == m2 && x.State == "Removed");
    }

    [Fact]
    public async Task Substitution_is_an_edge_not_a_delete()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700006028", "Sub Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 4);
        var (_, captainId) = await LoginAsync("9700006029");
        var (_, outId) = await LoginAsync("9700006030");
        var (_, inId) = await LoginAsync("9700006031");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        await svc.SetPolicyAsync(ownerId, eventId, ttId, false, EmptyPolicy() with { SubstitutesAllowed = 1 }, default);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Zeta"))).Value!;
        Assert.True((await svc.AcceptInviteAsync(outId, (await svc.InviteAsync(captainId, team.Id, new TeamInviteInput(PersonId: outId))).Value!.Token)).Ok);
        var outMembership = (await svc.GetTeamAsync(team.Id)).Value!.Members.Single(x => x.PersonId == outId).MembershipId;

        var subbed = await svc.SubstituteAsync(captainId, team.Id, outMembership, inId, false);
        Assert.True(subbed.Ok);
        var members = subbed.Value!.Members;
        var outRow = members.Single(x => x.PersonId == outId);
        Assert.Equal("Replaced", outRow.State);
        Assert.NotNull(outRow.ReplacedByMembershipId);                       // the edge is preserved (history)
        Assert.Contains(members, x => x.PersonId == inId && x.State == "Active");
        Assert.Equal(2, subbed.Value!.ActiveMemberCount);                    // captain + the substitute
    }

    // ── Lifecycle + merge/split ────────────────────────────────────────────────

    [Fact]
    public async Task Organiser_locks_then_disqualifies_with_a_reason()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700006032", "Lifecycle Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 4);
        var (_, captainId) = await LoginAsync("9700006033");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Eta"))).Value!;

        Assert.False((await svc.TransitionAsync(captainId, team.Id, false, "lock", null)).Ok);   // non-organiser forbidden
        Assert.True((await svc.TransitionAsync(ownerId, team.Id, false, "lock", null)).Ok);
        Assert.False((await svc.InviteAsync(captainId, team.Id, new TeamInviteInput(PersonId: captainId))).Ok);   // locked → no invites
        Assert.False((await svc.TransitionAsync(ownerId, team.Id, false, "disqualify", null)).Ok);   // reason required
        var dq = await svc.TransitionAsync(ownerId, team.Id, false, "disqualify", "cheating");
        Assert.True(dq.Ok);
        Assert.Equal("Disqualified", dq.Value!.State);
    }

    [Fact]
    public async Task Merge_moves_members_and_tombstones_the_second_team()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700006034", "Merge Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 4);
        var (_, capA) = await LoginAsync("9700006035");
        var (_, capB) = await LoginAsync("9700006036");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var a = (await svc.CreateTeamAsync(capA, eventId, ttId, new TeamInput("Team A"))).Value!;
        var b = (await svc.CreateTeamAsync(capB, eventId, ttId, new TeamInput("Team B"))).Value!;

        var merged = await svc.MergeAsync(ownerId, a.Id, b.Id, false);
        Assert.True(merged.Ok);
        Assert.Equal(2, merged.Value!.ActiveMemberCount);                    // both captains now in A (B's captain demoted)
        Assert.Contains(merged.Value!.Members, m => m.PersonId == capB && m.Role == "Member");
        var bAfter = (await svc.GetTeamAsync(b.Id)).Value!;
        Assert.Equal(a.Id, bAfter.MergedIntoTeamId);                         // B is a tombstone referencing A
    }

    [Fact]
    public async Task Split_creates_a_new_team_from_the_moved_members()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700006037", "Split Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 6);
        var (_, captainId) = await LoginAsync("9700006038");
        var (_, m1) = await LoginAsync("9700006039");
        var (_, m2) = await LoginAsync("9700006040");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Whole"))).Value!;
        foreach (var m in new[] { m1, m2 })
            Assert.True((await svc.AcceptInviteAsync(m, (await svc.InviteAsync(captainId, team.Id, new TeamInviteInput(PersonId: m))).Value!.Token)).Ok);

        var split = await svc.SplitAsync(ownerId, team.Id, [m1, m2], "Splinter", false);
        Assert.True(split.Ok);
        Assert.Equal(1, split.Value!.Original.ActiveMemberCount);           // captain stays on the original
        Assert.Equal(2, split.Value!.NewTeam.ActiveMemberCount);            // m1 + m2 on the new team
        Assert.Equal("Splinter", split.Value!.NewTeam.Name);
    }

    [Fact]
    public async Task Merge_is_forbidden_once_a_team_is_competing()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700006041", "Late Merge Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 4);
        var (_, capA) = await LoginAsync("9700006042");
        var (_, capB) = await LoginAsync("9700006043");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var a = (await svc.CreateTeamAsync(capA, eventId, ttId, new TeamInput("A2"))).Value!;
        var b = (await svc.CreateTeamAsync(capB, eventId, ttId, new TeamInput("B2"))).Value!;
        await svc.TransitionAsync(ownerId, a.Id, false, "lock", null);
        await svc.TransitionAsync(ownerId, a.Id, false, "compete", null);   // A is now Competing

        var merge = await svc.MergeAsync(ownerId, a.Id, b.Id, false);
        Assert.False(merge.Ok);
        Assert.Equal("merge_after_lock_forbidden", merge.Error);
    }

    [Fact]
    public async Task Non_captain_cannot_invite()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006044", "Authz Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId);
        var (_, captainId) = await LoginAsync("9700006045");
        var (_, strangerId) = await LoginAsync("9700006046");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var team = (await svc.CreateTeamAsync(captainId, eventId, ttId, new TeamInput("Guarded"))).Value!;
        var r = await svc.InviteAsync(strangerId, team.Id, new TeamInviteInput(PersonId: strangerId));
        Assert.False(r.Ok);
        Assert.Equal("forbidden", r.Error);
    }

    [Fact]
    public async Task Create_team_endpoint_works_over_http()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700006047", "Http Team Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId);
        var (captain, _) = await LoginAsync("9700006048");

        var res = await captain.PostAsJsonAsync($"/v1/events/{eventId}/teams?ticketTypeId={ttId}", new { name = "Endpoint Team" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var team = await Json(res);
        Assert.Equal("Forming", team.GetProperty("state").GetString());

        var mine = (await Json(await captain.GetAsync("/v1/me/teams"))).EnumerateArray().ToList();
        Assert.Single(mine);
        Assert.Equal(team.GetProperty("id").GetGuid(), mine[0].GetProperty("id").GetGuid());
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static TeamPolicyInput EmptyPolicy() => new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishCompetitionAsync(HttpClient owner, Guid orgId, int min = 2, int max = 4)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Comp", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Group",
            groupMin = min, groupMax = max, quantity = 200,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50,
            isAllAccess = false, isCompetition = true,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ttId = (await Json(res)).GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Free", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            groupMin = (int?)null, groupMax = (int?)null, quantity = 100,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        var ttId = (await Json(res)).GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Team " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<(HttpClient Client, Guid OrgId, Guid UserId)> LoginOrgAsync(string phone, string name)
    {
        var (client, userId) = await LoginAsync(phone);
        return (client, _factory.SeedVerifiedOrgForClient(client, name), userId);
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug).Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.ParentId!.Value, t.Id);
    }
}
