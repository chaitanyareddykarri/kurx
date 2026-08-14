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

/// <summary>V3 §14 (Phase 14) — Lifecycle, gates & approvals. Additive over the approved lifecycle: Published stays the
/// authoritative registration-open state; Scheduled/Live/Completed + their §14.2 gates are added; §14.3 approval chains
/// gate publishing; §14.5 material change opens a refund window + notifies + audits. No money-path change.</summary>
public class LifecycleApprovalTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public LifecycleApprovalTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private IApprovalService Approvals(IServiceScope s) => s.ServiceProvider.GetRequiredService<IApprovalService>();
    private IEventService Events(IServiceScope s) => s.ServiceProvider.GetRequiredService<IEventService>();

    // ── §14.3 Approval chains ─────────────────────────────────────────────────

    [Fact]
    public async Task Approval_chain_gates_scheduling_until_the_step_is_approved()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014001", "Approval Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, approverId) = await LoginAsync("9700014002");
        var (_, strangerId) = await LoginAsync("9700014003");
        var unitId = await OrgUnitAsync(eventId);

        using var scope = _factory.Services.CreateScope();
        var svc = Approvals(scope);
        var events = Events(scope);
        Assert.True((await svc.CreateChainAsync(ownerId, unitId, false, new ApprovalChainInput("Advisor", "Sequential",
            [new ApprovalStepInput(0, null, approverId, "Always", null, 48, 24)]))).Ok);

        // The internal chain gates publishing (§14.2/§14.3) — order: chain → platform review → published.
        Assert.Equal("approval_pending", (await events.TransitionAsync(ownerId, eventId, false, false, "schedule")).Error);

        var req = (await svc.GetRequestAsync(ownerId, eventId, false)).Value!;
        var decisionId = req.Decisions.Single().Id;
        Assert.Equal("forbidden", (await svc.DecideAsync(strangerId, decisionId, false, "approve", null)).Error);   // not the approver
        Assert.True((await svc.DecideAsync(approverId, decisionId, false, "approve", "ok")).Ok);
        Assert.Equal("Approved", (await svc.GetRequestAsync(ownerId, eventId, false)).Value!.State);

        var scheduled = await events.TransitionAsync(ownerId, eventId, false, false, "schedule");
        Assert.True(scheduled.Ok, scheduled.Error);
        Assert.Equal("Scheduled", scheduled.Value!.Status);
    }

    [Fact]
    public async Task Bypass_requires_an_owner_and_is_recorded()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014004", "Bypass Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, approverId) = await LoginAsync("9700014005");
        var (_, memberId) = await LoginAsync("9700014006");
        var unitId = await OrgUnitAsync(eventId);

        using var scope = _factory.Services.CreateScope();
        var svc = Approvals(scope);
        Assert.True((await svc.CreateChainAsync(ownerId, unitId, false, new ApprovalChainInput("Chain", "Sequential",
            [new ApprovalStepInput(0, null, approverId, "Always", null, null, null)]))).Ok);
        await Events(scope).TransitionAsync(ownerId, eventId, false, false, "schedule");   // materialises the request
        var decisionId = (await svc.GetRequestAsync(ownerId, eventId, false)).Value!.Decisions.Single().Id;

        Assert.Equal("forbidden", (await svc.DecideAsync(memberId, decisionId, false, "bypass", "no")).Error);   // non-owner can't bypass
        Assert.True((await svc.DecideAsync(ownerId, decisionId, false, "bypass", "advisor unavailable")).Ok);     // owner may
        var req = (await svc.GetRequestAsync(ownerId, eventId, false)).Value!;
        Assert.Equal("Bypassed", req.Decisions.Single().State);
        Assert.Equal("Approved", req.State);   // all applicable steps settled ⇒ request complete
    }

    // ── §14.1/§14.2 Lifecycle + gates ─────────────────────────────────────────

    [Fact]
    public async Task Go_live_requires_staff_then_complete_progresses_the_lifecycle()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014010", "Lifecycle Org");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);   // Published (registration open) — Published semantics unchanged

        using var scope = _factory.Services.CreateScope();
        var events = Events(scope);
        Assert.Equal("no_staff_assigned", (await events.TransitionAsync(ownerId, eventId, false, false, "go_live")).Error);   // §14.2 go-live gate
        await SeedStaffAsync(eventId, orgId, ownerId);
        var live = await events.TransitionAsync(ownerId, eventId, false, false, "go_live");
        Assert.True(live.Ok, live.Error);
        Assert.Equal("Live", live.Value!.Status);
        var done = await events.TransitionAsync(ownerId, eventId, false, false, "complete");
        Assert.True(done.Ok, done.Error);
        Assert.Equal("Completed", done.Value!.Status);
    }

    [Fact]
    public async Task Open_registration_gate_requires_a_pass_and_pool()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014011", "OpenReg Org");
        var eventId = await CreateEventAsync(owner, orgId);   // draft, no ticket type / pool yet

        using var scope = _factory.Services.CreateScope();
        var events = Events(scope);
        Assert.True((await events.TransitionAsync(ownerId, eventId, false, false, "schedule")).Ok);   // no chain ⇒ schedules
        Assert.Equal("no_pass", (await events.TransitionAsync(ownerId, eventId, false, false, "open_registration")).Error);
    }

    // ── §14.5 Material change ─────────────────────────────────────────────────

    [Fact]
    public async Task Material_change_opens_a_refund_window_and_notifies_registrants()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014020", "Material Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (registrant, registrantId) = await LoginAsync("9700014021");
        Assert.Equal(HttpStatusCode.OK, (await registrant.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var events = Events(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var before = await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
        Assert.Null(before.RefundWindowEndsAt);

        var upd = await events.UpdateAsync(ownerId, eventId, false, MoveDate(before.StartsAt.AddDays(3), before.EndsAt.AddDays(3)));
        Assert.True(upd.Ok, upd.Error);

        var after = await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
        Assert.NotNull(after.RefundWindowEndsAt);   // §14.5 window opened
        Assert.True(await db.Notifications.AsNoTracking().AnyAsync(n => n.UserId == registrantId && n.Kind == "event_material_change"));
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(a => a.Entity == "events" && a.EntityId == eventId && a.Action == "event.material_change"));
    }

    // ── Review-fix regressions (H1 · H2 · M1 · M4) ───────────────────────────

    [Fact]   // H1 — a condition (if_paid) that becomes true AFTER the request exists must add its required approval step.
    public async Task Condition_becoming_true_after_the_request_adds_the_missing_step()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014030", "H1 Org");
        var eventId = await CreateEventAsync(owner, orgId);   // free event
        var (_, alwaysApprover) = await LoginAsync("9700014031");
        var (_, paidApprover) = await LoginAsync("9700014032");
        var unitId = await OrgUnitAsync(eventId);

        using var scope = _factory.Services.CreateScope();
        var svc = Approvals(scope);
        var events = Events(scope);
        Assert.True((await svc.CreateChainAsync(ownerId, unitId, false, new ApprovalChainInput("Chain", "Parallel",
            [new ApprovalStepInput(0, null, alwaysApprover, "Always", null, null, null),
             new ApprovalStepInput(1, null, paidApprover, "IfPaid", null, null, null)]))).Ok);

        // While free, only the Always step is materialised; approving it completes the request.
        await events.TransitionAsync(ownerId, eventId, false, false, "schedule");
        var d0 = (await svc.GetRequestAsync(ownerId, eventId, false)).Value!.Decisions.Single();   // just the Always step
        Assert.True((await svc.DecideAsync(alwaysApprover, d0.Id, false, "approve", null)).Ok);
        Assert.True((await events.TransitionAsync(ownerId, eventId, false, false, "schedule")).Ok);   // schedules (approved)

        // Make it PAID after approval → the if_paid step must now be required (no silent bypass).
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Paid", pricePaise = 50000L, pricingUnit = "PerTicket", registrationMode = "Individual",
            quantity = 50, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 10, isAllAccess = false,
        })).StatusCode);
        var req = (await svc.GetRequestAsync(ownerId, eventId, false)).Value!;
        Assert.Equal("Pending", req.State);                          // re-opened
        Assert.Equal(2, req.Decisions.Count);                        // the if_paid step was added
        var paidDecision = req.Decisions.Single(d => d.Condition == "IfPaid");
        Assert.Equal("Pending", paidDecision.State);
        Assert.True((await svc.DecideAsync(paidApprover, paidDecision.Id, false, "approve", null)).Ok);
        Assert.Equal("Approved", (await svc.GetRequestAsync(ownerId, eventId, false)).Value!.State);
    }

    [Fact]   // H2 — reject → fix → resubmit restarts the approval cycle (history in the audit spine preserved).
    public async Task Rejected_request_can_be_resubmitted()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014040", "H2 Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, approverId) = await LoginAsync("9700014041");
        var unitId = await OrgUnitAsync(eventId);

        using var scope = _factory.Services.CreateScope();
        var svc = Approvals(scope);
        var events = Events(scope);
        Assert.True((await svc.CreateChainAsync(ownerId, unitId, false, new ApprovalChainInput("Chain", "Sequential",
            [new ApprovalStepInput(0, null, approverId, "Always", null, null, null)]))).Ok);
        await events.TransitionAsync(ownerId, eventId, false, false, "schedule");
        var decisionId = (await svc.GetRequestAsync(ownerId, eventId, false)).Value!.Decisions.Single().Id;
        Assert.True((await svc.DecideAsync(approverId, decisionId, false, "reject", "fix the venue")).Ok);
        Assert.Equal("approval_pending", (await events.TransitionAsync(ownerId, eventId, false, false, "schedule")).Error);   // rejected blocks

        var resub = await svc.ResubmitAsync(ownerId, eventId, false);   // organiser fixes + resubmits
        Assert.True(resub.Ok, resub.Error);
        Assert.Equal("Pending", resub.Value!.State);
        var freshDecisionId = resub.Value!.Decisions.Single().Id;
        Assert.NotEqual(decisionId, freshDecisionId);   // a fresh decision, not the rejected one
        Assert.True((await svc.DecideAsync(approverId, freshDecisionId, false, "approve", null)).Ok);
        Assert.True((await events.TransitionAsync(ownerId, eventId, false, false, "schedule")).Ok);   // now publishes

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(a => a.Action == "approval.resubmit"));   // resubmit audited
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(a => a.Action == "approval.reject"));     // the rejection history remains in the spine
    }

    [Fact]   // M4 — cancelling a sub-event (parent continues) opens a refund window + notifies its registrants.
    public async Task Sub_event_cancellation_is_a_material_change()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700014050", "M4 Org");
        var parentId = await CreateEventAsync(owner, orgId);
        var (subId, subTtId) = await PublishFreeSubEventAsync(owner, orgId, parentId);
        var (registrant, registrantId) = await LoginAsync("9700014051");
        Assert.Equal(HttpStatusCode.OK, (await registrant.PostAsJsonAsync($"/v1/events/{subId}/orders", new { ticketTypeId = subTtId })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        Assert.True((await Events(scope).TransitionAsync(ownerId, subId, false, false, "cancel")).Ok);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var sub = await db.Events.AsNoTracking().FirstAsync(e => e.Id == subId);
        Assert.NotNull(sub.RefundWindowEndsAt);   // §14.5 window opened on the sub-event
        Assert.True(await db.Notifications.AsNoTracking().AnyAsync(n => n.UserId == registrantId && n.Kind == "event_material_change"));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeSubEventAsync(HttpClient owner, Guid orgId, Guid parentId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Sub " + Guid.NewGuid().ToString("N")[..6], description = "sub", categoryId = catId, typeId, parentEventId = parentId,
            venueName = "Room", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(1),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var subId = (await Json(res)).GetProperty("id").GetGuid();
        var tt = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{subId}/ticket-types", new
        {
            name = "GA", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            quantity = 100, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        var ttId = (await Json(tt)).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(subId);   // D-266 M5 — fixture needs a published sub-event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{subId}/transition", new { action = "publish" });
        return (subId, ttId);
    }

    private static UpdateEventInput MoveDate(DateTime start, DateTime end) =>
        new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, start, end, null, null, null, null, null, null, null, null, null, null);

    private async Task<Guid> OrgUnitAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return (await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.OrgUnitId).FirstAsync())!.Value;
    }

    private async Task SeedStaffAsync(Guid eventId, Guid orgId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.EventAssignments.Add(new EventAssignment { EventId = eventId, OrgId = orgId, UserId = userId, Role = "staff", Status = AssignmentStatus.Accepted });
        await db.SaveChangesAsync();
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "GA", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            quantity = 100, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ttId = (await Json(res)).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        var pub = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, pub.StatusCode);
        return (eventId, ttId);
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Ev " + Guid.NewGuid().ToString("N")[..6], description = "a real description", categoryId = catId, typeId,
            venueName = "Main Hall", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var id = (await Json(res)).GetProperty("id").GetGuid();
        // D-266 M5: this class is about the APPROVAL CHAIN and the §14.2 lifecycle gates. On the schedule
        // leg the policy engine is consulted first — deliberately, so /policy-requirements and the gate
        // agree — which means an unauthorized event reports `event_authorization_required` and the
        // `approval_pending` these cases assert on never surfaces. Authorizing here keeps each test
        // measuring the gate it names.
        _factory.SeedApprovedEventAuthorization(id);
        return id;
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
