using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-367 — `teams` is enforced by the domain, not only described by the engine.
///
/// <para>Web reverts `participation` when the Type stops supporting teams, and Flutter does the same on
/// rebuild. That is two implementations of one rule, neither of which is the one that matters: the API
/// accepted <c>registrationMode: "Group"</c> whatever the archetype said, so the invariant held only for
/// as long as every client kept its half of the bargain. **Every test here goes over HTTP**, which is the
/// only way to prove a rule survives a client that does not know it.</para>
///
/// <para>The matrix is the authority, not this file: `teams` is Optional for <c>competitive</c> and absent
/// for <c>conference</c> in <c>archetype_capability_defaults</c>, and the last test here changes a row and
/// watches the answer change with it.</para></summary>
public class TeamCapabilityEnforcementTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static Guid _teamTypeId;      // archetype: competitive — `teams` Optional
    private static Guid _soloTypeId;      // archetype: conference  — `teams` absent ⇒ Unsupported
    private static readonly object ResetLock = new();
    private static bool _reset;

    public TeamCapabilityEnforcementTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Cap", Slug = "cap-enforce" };
            db.EventCategories.Add(cat);
            // Two Types under one Category, differing ONLY in archetype — so every assertion below is
            // about the capability matrix and nothing else.
            var teamType = new EventCategory
            {
                Level = CategoryLevel.Type, ParentId = cat.Id, Name = "Cap Hackathon",
                Slug = "cap-hackathon", ArchetypeSlug = "competitive",
            };
            var soloType = new EventCategory
            {
                Level = CategoryLevel.Type, ParentId = cat.Id, Name = "Cap Conference",
                Slug = "cap-conference", ArchetypeSlug = "conference",
            };
            db.EventCategories.AddRange(teamType, soloType);
            db.SaveChanges();
            _categoryId = cat.Id;
            _teamTypeId = teamType.Id;
            _soloTypeId = soloType.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone, code = _factory.WhatsApp.LastOtpFor(phone) }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return c;
    }

    /// <summary>An event of the given Type, or of none at all when <paramref name="typeId"/> is null —
    /// the archetype-less case, which resolves every capability to Unsupported by design.</summary>
    private async Task<(Guid OrgId, Guid EventId)> EventAsync(HttpClient owner, string seed, Guid? typeId)
    {
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Cap Org {seed}", "Company");
        var id = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = $"Cap Event {seed}", description = "Capability enforcement under test.",
            categoryId = _categoryId, typeId,
            venueName = "Arena", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();
        return (orgId, id);
    }

    /// <summary><paramref name="competition"/> matters: `TeamService.SyncPolicyAsync` creates a
    /// `TeamPolicy` only for a competition ticket type, so the test that asserts a refused Type change
    /// leaves the policy alone has to create one that HAS a policy.</summary>
    private static object Ticket(string mode, int? groupMin = null, int? groupMax = null,
        object[]? tiers = null, bool competition = false) => new
    {
        name = mode == "Group" ? "Team Entry" : "General",
        pricePaise = 0,
        pricingUnit = mode == "Group" ? "PerGroup" : "PerTicket",
        registrationMode = mode,
        groupMin, groupMax,
        quantity = 100,
        saleStarts = DateTime.UtcNow.AddDays(-1),
        saleEnds = DateTime.UtcNow.AddDays(30),
        perUserLimit = 5, isAllAccess = false, isCompetition = competition,
        priceTiers = tiers,
    };

    private Task<HttpResponseMessage> PostTicket(HttpClient c, Guid orgId, Guid eventId, object body) =>
        c.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", body);

    // ── Creation ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_team_capable_archetype_accepts_a_team_ticket()
    {
        var owner = await LoginAsync("9950000001");
        var (orgId, eventId) = await EventAsync(owner, "ok", _teamTypeId);

        var res = await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.Equal("Group", (await Json(res)).GetProperty("registration_mode").GetString());
    }

    /// <summary><b>The bypass test.</b> No client is involved: this is the raw API call a script, a stale
    /// build or a curl one-liner would make, and it is the only thing that proves the rule exists at the
    /// server boundary rather than in two UIs.</summary>
    [Fact]
    public async Task A_non_team_archetype_refuses_a_team_ticket_even_when_the_API_is_called_directly()
    {
        var owner = await LoginAsync("9950000002");
        var (orgId, eventId) = await EventAsync(owner, "no", _soloTypeId);

        var res = await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5));
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("teams_not_supported", (await Json(res)).GetProperty("error").GetString());

        // Nothing was written — a refusal that leaves a row behind is not a refusal.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Empty(await db.TicketTypes.AsNoTracking().Where(t => t.EventId == eventId).ToListAsync());
    }

    /// <summary>An event with no Type has no archetype, and `CapabilityResolver.StateOf` answers
    /// Unsupported for that deliberately. Enforcement inherits it: an event that has not said what kind of
    /// thing it is has not said it can have teams.</summary>
    [Fact]
    public async Task An_archetype_less_event_refuses_a_team_ticket()
    {
        var owner = await LoginAsync("9950000003");
        var (orgId, eventId) = await EventAsync(owner, "none", typeId: null);

        var res = await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5));
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("teams_not_supported", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>The rule is about TEAMS and nothing else — an individual ticket is unaffected on every
    /// archetype, including the ones that support teams and the ones that have no archetype at all.</summary>
    [Theory]
    [InlineData("team")]
    [InlineData("solo")]
    [InlineData("none")]
    public async Task An_individual_ticket_is_accepted_everywhere(string shape)
    {
        var owner = await LoginAsync($"995000001{shape.Length}");
        var typeId = shape switch { "team" => _teamTypeId, "solo" => _soloTypeId, _ => (Guid?)null };
        var (orgId, eventId) = await EventAsync(owner, $"solo-{shape}", typeId);

        var res = await PostTicket(owner, orgId, eventId, Ticket("Individual"));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    // ── Update ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_individual_ticket_may_become_a_team_ticket_where_teams_are_supported()
    {
        var owner = await LoginAsync("9950000020");
        var (orgId, eventId) = await EventAsync(owner, "becomes", _teamTypeId);
        var ttId = (await Json(await PostTicket(owner, orgId, eventId, Ticket("Individual")))).GetProperty("id").GetGuid();

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}",
            Ticket("Group", 2, 5));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    /// <summary>The second bypass shape: not creating a team ticket, but turning an existing one into
    /// one. Same refusal, because the invariant is about the state that results, not the verb used.</summary>
    [Fact]
    public async Task An_individual_ticket_may_not_become_a_team_ticket_where_teams_are_not_supported()
    {
        var owner = await LoginAsync("9950000021");
        var (orgId, eventId) = await EventAsync(owner, "cannot-become", _soloTypeId);
        var ttId = (await Json(await PostTicket(owner, orgId, eventId, Ticket("Individual")))).GetProperty("id").GetGuid();

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}",
            Ticket("Group", 2, 5));
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("teams_not_supported", (await Json(res)).GetProperty("error").GetString());

        // And it is still what it was — a refused change must not half-apply.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var stored = await db.TicketTypes.AsNoTracking().FirstAsync(t => t.Id == ttId);
        Assert.Equal(RegistrationMode.Individual, stored.RegistrationMode);
    }

    /// <summary><b>The legacy-data rule.</b> A ticket already stored as Group stays editable even where
    /// the matrix now says teams are unsupported — otherwise an admin editing capability data would
    /// strand rows that were legal when written, leaving an organiser unable to fix or reprice a ticket
    /// that may already have sold. The invariant is "no NEW invalid state", not "punish old state".</summary>
    [Fact]
    public async Task An_existing_team_ticket_stays_editable_after_its_archetype_stops_supporting_teams()
    {
        var owner = await LoginAsync("9950000022");
        var (orgId, eventId) = await EventAsync(owner, "legacy", _teamTypeId);
        var ttId = (await Json(await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5)))).GetProperty("id").GetGuid();

        // The archetype loses `teams` under the ticket's feet — the admin console can do exactly this.
        await SetTeamsRuleAsync("competitive", CapabilityRule.Unsupported);

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}", new
        {
            name = "Renamed Team Entry", pricePaise = 0, pricingUnit = "PerGroup", registrationMode = "Group",
            groupMin = 2, groupMax = 5, quantity = 120,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 5, isAllAccess = false, isCompetition = false,
        });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        // Restored, because the matrix is shared state and the next test must see it as seeded.
        await SetTeamsRuleAsync("competitive", CapabilityRule.Optional);
    }

    // ── The Type change ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_type_change_between_two_team_capable_archetypes_is_allowed()
    {
        var owner = await LoginAsync("9950000030");
        var (orgId, eventId) = await EventAsync(owner, "team-to-team", _teamTypeId);
        await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5));

        // A second competitive Type — same archetype, so `teams` is supported on both sides.
        Guid otherTeamType;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var t = new EventCategory
            {
                Level = CategoryLevel.Type, ParentId = _categoryId, Name = "Cap Tournament",
                Slug = "cap-tournament", ArchetypeSlug = "tournament",   // `teams` Optional here too
            };
            db.EventCategories.Add(t);
            await db.SaveChangesAsync();
            otherTeamType = t.Id;
        }

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { typeId = otherTeamType });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    /// <summary><b>The third bypass shape, and the one that would have destroyed data.</b> Converting the
    /// ticket instead would delete a TeamPolicy, its roster rules and its D-366 price bands as a side
    /// effect of a dropdown. Refused, and — asserted below — refused ATOMICALLY.</summary>
    [Fact]
    public async Task A_type_change_to_a_non_team_archetype_is_refused_while_a_team_ticket_exists()
    {
        var owner = await LoginAsync("9950000031");
        var (orgId, eventId) = await EventAsync(owner, "conflict", _teamTypeId);
        var ttId = (await Json(await PostTicket(owner, orgId, eventId,
            Ticket("Group", 2, 5, [new { minSize = 2, maxSize = 5, pricePaise = 25_000 }],
                competition: true)))).GetProperty("id").GetGuid();

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { typeId = _soloTypeId });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("type_conflicts_with_team_ticket", (await Json(res)).GetProperty("error").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // NOTHING moved: not the Type, not the archetype, not the ticket, not its bands, not its policy.
        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
        Assert.Equal(_teamTypeId, ev.TypeId);
        Assert.Equal("competitive", ev.ArchetypeSlug);

        var tt = await db.TicketTypes.AsNoTracking().FirstAsync(t => t.Id == ttId);
        Assert.Equal(RegistrationMode.Group, tt.RegistrationMode);
        Assert.Equal(2, tt.GroupMin);
        Assert.Equal(5, tt.GroupMax);

        Assert.Single(await db.TicketPriceTiers.AsNoTracking().Where(b => b.TicketTypeId == ttId).ToListAsync());
        Assert.NotNull(await db.TeamPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.TicketTypeId == ttId));
    }

    /// <summary>The same Type change is fine when nothing on the event claims teams — the rule guards a
    /// team registration, not the archetype in the abstract.</summary>
    [Fact]
    public async Task A_type_change_to_a_non_team_archetype_is_allowed_when_no_team_ticket_exists()
    {
        var owner = await LoginAsync("9950000032");
        var (orgId, eventId) = await EventAsync(owner, "no-conflict", _teamTypeId);
        await PostTicket(owner, orgId, eventId, Ticket("Individual"));

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { typeId = _soloTypeId });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    // ── The matrix is the authority ──────────────────────────────────────────────────

    /// <summary>The rule reads the persisted matrix the admin console owns (D-188), so an admin enabling
    /// `teams` for an archetype makes team events legal there with no deployment. Hardcoding
    /// "competitive has teams" anywhere in the enforcement path would make this test impossible to
    /// write — which is why it is the last one here.</summary>
    [Fact]
    public async Task Enabling_teams_for_an_archetype_makes_a_team_ticket_legal_without_a_code_change()
    {
        var owner = await LoginAsync("9950000040");
        var (orgId, eventId) = await EventAsync(owner, "matrix", _soloTypeId);

        // Refused as seeded — `conference` has no `teams` row.
        var before = await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5));
        Assert.Equal("teams_not_supported", (await Json(before)).GetProperty("error").GetString());

        // The console's own move: flip the existing cell. The seeder writes an explicit Unsupported row
        // for every pair (D-266 M2's inversion), so there is a row here to change rather than add.
        await SetTeamsRuleAsync("conference", CapabilityRule.Optional);

        var after = await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5));
        Assert.True(after.IsSuccessStatusCode, await after.Content.ReadAsStringAsync());

        await SetTeamsRuleAsync("conference", CapabilityRule.Unsupported);   // back as seeded
    }

    /// <summary>Sets one cell of the persisted matrix, the way the admin console does. Shared state, so
    /// every test that touches it puts it back.</summary>
    private async Task SetTeamsRuleAsync(string archetypeSlug, CapabilityRule rule)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await db.ArchetypeCapabilityDefaults
            .Where(d => d.ArchetypeSlug == archetypeSlug && d.CapabilitySlug == "teams")
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Rule, rule));
    }

    // ── D-366 must survive D-367 ─────────────────────────────────────────────────────

    /// <summary>The bands still work on a team-capable archetype: created, kept through an update that
    /// says nothing about them, and resolving per size at checkout is covered by
    /// <c>TeamSizePricingTests</c>. This is the seam between the two decisions, asserted where they meet.</summary>
    [Fact]
    public async Task Price_bands_still_work_on_a_team_capable_event()
    {
        var owner = await LoginAsync("9950000050");
        var (orgId, eventId) = await EventAsync(owner, "bands", _teamTypeId);

        var created = await PostTicket(owner, orgId, eventId, Ticket("Group", 2, 5,
        [
            new { minSize = 2, maxSize = 3, pricePaise = 25_000 },
            new { minSize = 4, maxSize = 5, pricePaise = 40_000 },
        ]));
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var ttId = (await Json(created)).GetProperty("id").GetGuid();

        // An update that never mentions bands leaves them alone (the audit fix), and the ticket is still
        // a team ticket, so D-367's update check does not fire on it.
        var patched = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}", new
        {
            name = "Renamed", pricePaise = 0, pricingUnit = "PerGroup", registrationMode = "Group",
            groupMin = 2, groupMax = 5, quantity = 100,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 5, isAllAccess = false, isCompetition = false,
        });
        Assert.True(patched.IsSuccessStatusCode, await patched.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(2, await db.TicketPriceTiers.AsNoTracking().CountAsync(b => b.TicketTypeId == ttId));
    }
}
