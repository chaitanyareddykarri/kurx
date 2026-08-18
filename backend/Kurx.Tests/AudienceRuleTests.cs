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

/// <summary>V3 §4.4 (Phase 5) — audience rules + member attributes + server-side eligibility. DENY BY DEFAULT
/// when a rule exists, open (backward compatible) when none does; enforced at registration and re-checked at
/// admission (flag, never void). Rules are evaluated against org memberships (staff today; richer affiliate
/// memberships arrive with a later import).</summary>
public class AudienceRuleTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public AudienceRuleTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    // ── Backward compatibility ──────────────────────────────────────────────

    [Fact]
    public async Task No_rule_keeps_registration_open_for_anyone()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002001", "Open Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        var (stranger, _) = await LoginAsync("9700002002");
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(stranger, eventId, ttId)).StatusCode);

        var elig = await Json(await stranger.GetAsync($"/v1/events/{eventId}/eligibility"));
        Assert.True(elig.GetProperty("allowed").GetBoolean());
    }

    // ── Rule management authz + validation ──────────────────────────────────

    [Fact]
    public async Task Setting_a_rule_requires_a_manage_role()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002003", "Managed Org");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);

        var (stranger, _) = await LoginAsync("9700002004");
        Assert.Equal(HttpStatusCode.Forbidden, (await SetRuleAsync(stranger, orgId, eventId, new { externalOrgsAllowed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = true })).StatusCode);

        var got = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/audience"));
        Assert.True(got.GetProperty("external_orgs_allowed").GetBoolean());
    }

    [Fact]
    public async Task Rule_validation_rejects_bad_input()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002005", "Validate Org");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);

        Assert.Equal(HttpStatusCode.BadRequest, (await SetRuleAsync(owner, orgId, eventId, new { roleIn = new[] { "Wizard" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetRuleAsync(owner, orgId, eventId, new { appliesTo = "Nonsense" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetRuleAsync(owner, orgId, eventId, new { unitSubtreeIn = new[] { Guid.NewGuid() } })).StatusCode);
    }

    // ── Predicate coverage ──────────────────────────────────────────────────

    [Fact]
    public async Task External_orgs_allowed_controls_non_members()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002006", "External Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (stranger, _) = await LoginAsync("9700002007");

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(stranger, eventId, ttId)).StatusCode);

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = true });
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(stranger, eventId, ttId)).StatusCode);
    }

    [Fact]
    public async Task Role_in_gates_by_membership_role()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002008", "Role Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        // The owner is an Owner-role member; a rule allowing only Staff excludes them.
        await SetRuleAsync(owner, orgId, eventId, new { roleIn = new[] { "Staff" } });
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);

        await SetRuleAsync(owner, orgId, eventId, new { roleIn = new[] { "Owner", "Manager" } });
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);
    }

    [Fact]
    public async Task Cohort_year_in_gates_by_member_attributes()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002009", "Cohort Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var membershipId = await OwnerMembershipIdAsync(orgId);

        await SetRuleAsync(owner, orgId, eventId, new { cohortYearIn = new[] { 2026 } });
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);   // no cohort set yet

        Assert.Equal(HttpStatusCode.OK,
            (await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/members/{membershipId}/attributes", new { cohortYear = 2026 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);

        // A different cohort is rejected.
        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/members/{membershipId}/attributes", new { cohortYear = 2025 });
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);
    }

    [Fact]
    public async Task Require_verified_gates_unverified_members()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002010", "Verified Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        await SetRuleAsync(owner, orgId, eventId, new { requireVerified = true });
        await SetMembershipAsync(orgId, verified: false, source: "Imported");
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);   // unverified → denied

        await SetMembershipAsync(orgId, verified: true, source: "Imported");
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);          // verified + not self-declared → allowed
    }

    [Fact]
    public async Task Require_verified_rejects_self_declared_even_when_verified()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002018", "Self Declared Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        await SetRuleAsync(owner, orgId, eventId, new { requireVerified = true });

        // V3 §4.3: require_verified rejects SELF_DECLARED, even if the seat is marked verified.
        await SetMembershipAsync(orgId, verified: true, source: "SelfDeclared");
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);

        await SetMembershipAsync(orgId, verified: true, source: "Invited");
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);
    }

    [Fact]
    public async Task Guests_allowed_controls_guest_checkout()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002011", "Guest Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        await SetRuleAsync(owner, orgId, eventId, new { guestsAllowed = false, externalOrgsAllowed = true });
        Assert.Equal(HttpStatusCode.Forbidden, (await GuestOrderAsync(eventId, ttId)).StatusCode);

        await SetRuleAsync(owner, orgId, eventId, new { guestsAllowed = true });
        Assert.Equal(HttpStatusCode.OK, (await GuestOrderAsync(eventId, ttId)).StatusCode);
    }

    [Fact]
    public async Task Unit_subtree_in_matches_root_but_not_a_child_unit()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002012", "Unit Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        Guid rootId, childId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var root = await db.OrgUnits.SingleAsync(u => u.OrgId == orgId && u.ParentId == null);
            var child = new Kurx.Domain.Entities.OrgUnit { OrgId = orgId, ParentId = root.Id, Kind = "department", Name = "CSE" };
            child.Path = $"{root.Path}{child.Id}/";
            db.OrgUnits.Add(child);
            await db.SaveChangesAsync();
            rootId = root.Id; childId = child.Id;
        }

        // The member's effective unit is the org root → in the root subtree, not in a child subtree.
        await SetRuleAsync(owner, orgId, eventId, new { unitSubtreeIn = new[] { rootId } });
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);

        await SetRuleAsync(owner, orgId, eventId, new { unitSubtreeIn = new[] { childId } });
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);
    }

    // ── Gate behaviour + logging + lifecycle ────────────────────────────────

    [Fact]
    public async Task Register_denied_is_logged_and_creates_no_order()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002013", "Denied Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (stranger, strangerId) = await LoginAsync("9700002014");

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(stranger, eventId, ttId)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(0, await db.Orders.CountAsync(o => o.EventId == eventId && o.UserId == strangerId));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "audience.register_denied" && a.EntityId == eventId));
    }

    [Fact]
    public async Task Admission_flags_but_still_admits_a_now_ineligible_holder()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700002015", "Gate Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        // Owner registers while eligible (rule allows Owner).
        await SetRuleAsync(owner, orgId, eventId, new { roleIn = new[] { "Owner" } });
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);

        Guid ticketCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            ticketCode = await db.Tickets.Where(t => t.EventId == eventId && t.UserId == ownerId).Select(t => t.Code).FirstAsync();
        }

        // Revoke eligibility, then scan: admitted, but flagged (never silently voided).
        await SetRuleAsync(owner, orgId, eventId, new { roleIn = new[] { "Staff" } });
        var scan = await Json(await owner.PostAsJsonAsync($"/v1/gate/{eventId}/scan", new { ticketCode }));
        Assert.True(scan.GetProperty("admitted").GetBoolean());
        Assert.Equal("role_not_eligible", scan.GetProperty("eligibility_flag").GetString());
    }

    [Fact]
    public async Task Deleting_a_rule_reopens_registration()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002016", "Reopen Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (stranger, _) = await LoginAsync("9700002017");

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await FreeOrderAsync(stranger, eventId, ttId)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await DeleteRuleAsync(owner, orgId, eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(stranger, eventId, ttId)).StatusCode);
    }

    // ── No-bypass: every ticket-issuing path runs the same gate ──────────────

    [Fact]
    public async Task Join_group_path_is_gated_by_audience()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002020", "Group Join Org");
        var (eventId, ttId) = await PublishGroupEventAsync(owner, orgId);
        await owner.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 3 });   // owner opens the group
        var joinCode = await GroupJoinCodeAsync(eventId);

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = false });

        var (_, strangerId) = await LoginAsync("9700002021");
        using var scope = _factory.Services.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var res = await orders.JoinGroupAsync(strangerId, new JoinGroupInput(joinCode, "Stranger", null));
        Assert.False(res.Ok);
        Assert.Equal("not_eligible", res.Error);                    // same decision as CreateOrderAsync
    }

    [Fact]
    public async Task Accept_group_invitation_path_is_gated_by_audience()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700002022", "Group Invite Org");
        var (eventId, ttId) = await PublishGroupEventAsync(owner, orgId);
        var order = await Json(await owner.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 3 }));
        var groupId = await GroupIdAsync(eventId);

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = false });

        var (_, strangerId) = await LoginAsync("9700002023");
        var token = "inv" + Guid.NewGuid().ToString("N")[..9];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.EventInvitations.Add(new EventInvitation
        {
            EventId = eventId, InvitedBy = ownerId, GroupId = groupId, Name = "Stranger",
            Phone = null, Channel = InvitationChannel.WhatsApp, InviteToken = token,
            Status = InvitationStatus.Active, RsvpStatus = InvitationRsvpStatus.None,
        });
        await db.SaveChangesAsync();

        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var res = await orders.AcceptGroupInvitationAsync(strangerId, token, null);
        Assert.False(res.Ok);
        Assert.Equal("not_eligible", res.Error);
    }

    [Fact]
    public async Task Ticket_transfer_claim_is_gated_by_audience()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700002024", "Transfer Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(owner, eventId, ttId)).StatusCode);   // owner (member) registers, ungated

        Guid ticketId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            ticketId = await db.Tickets.Where(t => t.EventId == eventId && t.UserId == ownerId).Select(t => t.Id).FirstAsync();
        }

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = false });
        const string strangerPhone = "9700002025";
        var (_, strangerId) = await LoginAsync(strangerPhone);

        using var scope2 = _factory.Services.CreateScope();
        var transfers = scope2.ServiceProvider.GetRequiredService<ITicketTransferService>();
        var init = await transfers.InitiateAsync(ownerId, ticketId, strangerPhone);
        Assert.True(init.Ok);
        var claim = await transfers.ClaimAsync(strangerId, strangerPhone, init.Value!.TransferCode);
        Assert.False(claim.Ok);                                     // the claimant is re-evaluated
        Assert.Equal("not_eligible", claim.Error);
    }

    [Fact]
    public async Task Eligible_member_can_still_join_a_group()   // regression: the gate must not over-block
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002026", "Group OK Org");
        var (eventId, ttId) = await PublishGroupEventAsync(owner, orgId);
        await owner.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 3 });
        var joinCode = await GroupJoinCodeAsync(eventId);

        await SetRuleAsync(owner, orgId, eventId, new { externalOrgsAllowed = true });   // anyone eligible
        var (_, joinerId) = await LoginAsync("9700002027");
        using var scope = _factory.Services.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var res = await orders.JoinGroupAsync(joinerId, new JoinGroupInput(joinCode, "Joiner", null));
        Assert.True(res.Ok);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /*
     * D-388 — this suite is about how a rule is EVALUATED, not about when one may be written.
     *
     * Every fixture here publishes the event first, because registration (the thing being gated) only
     * happens on a live one. D-388 then froze the audience rule on a live PUBLIC event, so fourteen tests
     * began failing on `change_request_required` — none of them because their subject had changed, all of
     * them because their SETUP now collided with a rule about a different question.
     *
     * The rule is written with the event momentarily back in `Draft`, then the status is restored. The
     * real endpoint still runs — its authorization, its validation and its role/JSON canonicalisation are
     * all still under test, which seeding the row directly would have thrown away, and which is exactly
     * where this suite has caught defects before. Nothing is weakened: only the incidental dependency on
     * "the event happened to be live while the rule was written" is removed.
     *
     * `Setting_a_rule_requires_a_manage_role` and `Rule_validation_rejects_bad_input` are unaffected:
     * both assert refusals (403/400) reached before any status gate, and both go through this helper.
     *
     * When a change request grows to carry eligibility (D-388's follow-up), this helper is what should be
     * replaced by the proposal flow — not the assertions below it.
     */
    private async Task<HttpResponseMessage> SetRuleAsync(HttpClient client, Guid orgId, Guid eventId, object body)
    {
        var restore = await SuspendLiveStatusAsync(eventId);
        try { return await client.PutAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/audience", body); }
        finally { await RestoreStatusAsync(eventId, restore); }
    }

    /// <summary>The delete leg, through the same suspension as <see cref="SetRuleAsync"/> and for the same
    /// reason — removing a rule opens a live event to everyone, which D-388 freezes just as firmly as
    /// setting one. The test below is about what deletion does to REGISTRATION, not about when it may
    /// happen; the guard itself is asserted in <c>Setting_a_rule_on_a_live_event_needs_approval</c>.</summary>
    private async Task<HttpResponseMessage> DeleteRuleAsync(HttpClient client, Guid orgId, Guid eventId)
    {
        var restore = await SuspendLiveStatusAsync(eventId);
        try { return await client.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/audience"); }
        finally { await RestoreStatusAsync(eventId, restore); }
    }

    /// <summary>Puts a live event back in <c>Draft</c> for the duration of one write, returning the status
    /// it had. Null when it was not live-protected, in which case nothing is restored.</summary>
    private async Task<EventStatus?> SuspendLiveStatusAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null || !Kurx.Infrastructure.Events.EventStatusWorkflow.IsLiveProtected(ev.Product, ev.Status))
            return null;
        var was = ev.Status;
        ev.Status = EventStatus.Draft;
        await db.SaveChangesAsync();
        return was;
    }

    private async Task RestoreStatusAsync(Guid eventId, EventStatus? status)
    {
        if (status is not { } s) return;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.FirstAsync(e => e.Id == eventId);
        ev.Status = s;
        await db.SaveChangesAsync();
    }

    /// <summary>D-388 — the guard itself, asserted here rather than only in
    /// <c>EventChangeRequestTests</c>: this is the suite that owns the audience endpoint, and a guard on
    /// it that only a distant test knows about is one nobody maintaining this file would see.</summary>
    [Fact]
    public async Task Setting_a_rule_on_a_live_event_needs_approval()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700002099", "Live Rule Org");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);

        // Straight at the endpoint, with no status suspension — the real organiser path.
        var res = await owner.PutAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/audience",
            new { externalOrgsAllowed = false });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("change_request_required", (await Json(res)).GetProperty("error").GetString());

        // And nothing was written: a refused rule must not silently gate registration.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.AudienceRules.AnyAsync(r => r.EventId == eventId));
    }

    private async Task<string> GroupJoinCodeAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Groups.Where(g => g.EventId == eventId).Select(g => g.JoinCode).FirstAsync();
    }

    private async Task<Guid> GroupIdAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Groups.Where(g => g.EventId == eventId).Select(g => g.Id).FirstAsync();
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishGroupEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Grp " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        }))).GetProperty("id").GetGuid();

        var ttRes = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Team", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Group",
            groupMin = 2, groupMax = 5, quantity = 500,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        var ttId = (await Json(ttRes)).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private Task<HttpResponseMessage> FreeOrderAsync(HttpClient client, Guid eventId, Guid ttId)
        => client.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

    private Task<HttpResponseMessage> GuestOrderAsync(Guid eventId, Guid ttId)
        => _factory.CreateClient().PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, guestName = "Walk In", guestPhone = "9700009999" });

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Aud " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        }))).GetProperty("id").GetGuid();

        var ttRes = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Free", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            groupMin = (int?)null, groupMax = (int?)null, quantity = 500,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        var ttId = (await Json(ttRes)).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private async Task<Guid> OwnerMembershipIdAsync(Guid orgId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Memberships.Where(m => m.OrgId == orgId && m.Role == OrgRole.Owner).Select(m => m.Id).FirstAsync();
    }

    private async Task SetMembershipAsync(Guid orgId, bool verified, string source)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE memberships SET \"IsVerified\" = {verified}, \"Source\" = {source} WHERE \"OrgId\" = {orgId} AND \"Role\" = 'Owner'");
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
