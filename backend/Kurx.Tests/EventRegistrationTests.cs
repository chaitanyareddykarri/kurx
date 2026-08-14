using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Analytics;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §7 (Phase 8) — the registration → admission → credential layer as an additive dual-write shadow
/// of the authoritative Order/Ticket. One five-axis policy per ticket type; each order projects a Registration,
/// an Admission per ticket, and one Credential per person per event TREE (§7.2); reconciliation proves the shadow
/// matches the authority and repairs drift. Order/Ticket stay authoritative (cut-over is Phase 9).</summary>
public class EventRegistrationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public EventRegistrationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    // ── Policy derivation ────────────────────────────────────────────────────

    [Fact]
    public async Task Free_individual_ticket_type_derives_an_open_contact_policy()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005001", "Policy Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var pol = await db.RegistrationPolicies.SingleAsync(p => p.TicketTypeId == ttId);
        Assert.Equal(RegistrationSubject.Person, pol.Subject);
        Assert.Equal(PaymentPolicy.Free, pol.Payment);
        Assert.Equal(IdentityRequirement.Contact, pol.IdentityRequirement);   // free individual ⇒ guest-checkout allowed
        Assert.Contains("Open", pol.GatesJson);
    }

    [Fact]
    public async Task Paid_ticket_type_derives_a_self_pay_account_policy()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700005002", "Paid Policy Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700005003");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var pol = await db.RegistrationPolicies.SingleAsync(p => p.TicketTypeId == ttId);
        Assert.Equal(PaymentPolicy.Self, pol.Payment);
        Assert.Equal(IdentityRequirement.Account, pol.IdentityRequirement);
    }

    // ── The registration → admission → credential chain ────────────────────────

    [Fact]
    public async Task Free_order_projects_registration_admission_and_credential()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005004", "Chain Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, buyerId) = await LoginAsync("9700005005");

        var orderId = (await Json(await FreeOrderAsync(buyer, eventId, ttId))).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var reg = await db.Registrations.SingleAsync(r => r.OrderId == orderId);
        Assert.Equal(RegistrationSubject.Person, reg.SubjectType);
        Assert.Equal(buyerId, reg.SubjectId);
        var adm = await db.Admissions.SingleAsync(a => a.RegistrationId == reg.Id);
        Assert.Equal(AdmissionState.Active, adm.State);
        Assert.Equal(buyerId, adm.PersonId);
        Assert.NotNull(adm.PoolId);                                            // linked to the Phase-7 pool
        Assert.Equal(1, await db.Credentials.CountAsync(c => c.EventId == eventId && c.PersonId == buyerId && c.State == CredentialState.Active));
    }

    /// <summary>D-336 — the resolve-or-insert in <c>MirrorOrderCoreAsync</c> is a read-then-insert with THREE
    /// concurrent producers for one order: the in-transaction money path, <c>DataBackfillJob</c>'s convergence
    /// pass and <c>RegistrationReconciliationJob</c>'s repair. Two of them both read null and both insert;
    /// <c>IX_registrations_OrderId</c> refuses the loser with 23505, which surfaced as an unhandled
    /// <c>DbUpdateException</c> — a 500 where §17.1 promises an idempotent no-op, and on a gateway webhook a 500
    /// is answered with a retry.
    ///
    /// <para>The registration is deleted first because that is exactly the state <c>BackfillAsync</c> selects on
    /// (<c>!db.Registrations.Any(r => r.OrderId == o.Id)</c>) — an order whose projection has not landed yet. It
    /// then fires the projector concurrently, which is what a backfill overlapping a live capture does.</para>
    ///
    /// <para>Without the transaction-scoped advisory lock this throws; the unique index still prevents a second
    /// row, so the assertion that matters is that nobody gets an exception AND exactly one row exists — the money
    /// was never at risk, the error shape was.</para></summary>
    [Fact]
    public async Task Concurrent_projections_of_one_order_insert_exactly_one_registration()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005040", "Projection Race Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9700005041");

        var orderId = (await Json(await FreeOrderAsync(buyer, eventId, ttId))).GetProperty("id").GetGuid();

        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Admissions hang off the registration, so they go with it — this is the un-projected state, not a
            // half-projected one.
            var reg = await db.Registrations.SingleAsync(r => r.OrderId == orderId);
            db.Admissions.RemoveRange(db.Admissions.Where(a => a.RegistrationId == reg.Id));
            db.Registrations.Remove(reg);
            await db.SaveChangesAsync();
        }

        // Each on its own scope and its own transaction, exactly as MirrorOrderAsync runs for the backfill and
        // the reconciliation job.
        var projections = Enumerable.Range(0, 6).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IEventRegistrationService>()
                .MirrorOrderAsync(orderId);
        });

        // No 23505 escapes to any caller.
        await Task.WhenAll(projections);

        using var check = _factory.Services.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await verify.Registrations.CountAsync(r => r.OrderId == orderId));
    }

    [Fact]
    public async Task Paid_capture_projects_a_confirmed_registration()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700005006", "Paid Chain Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700005007");
        var (buyer, buyerId) = await LoginAsync("9700005008");

        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_reg_1");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var reg = await db.Registrations.SingleAsync(r => r.OrderId == orderId);
        Assert.Equal(RegistrationState.Confirmed, reg.State);
        Assert.Equal(1, await db.Admissions.CountAsync(a => a.RegistrationId == reg.Id && a.State == AdmissionState.Active));
    }

    [Fact]
    public async Task Group_order_is_one_registration_with_an_admission_per_member()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005009", "Group Reg Org");
        var (eventId, ttId) = await PublishGroupEventAsync(owner, orgId);
        var order = await Json(await owner.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 3 }));
        var orderId = order.GetProperty("id").GetGuid();

        string joinCode;
        using (var scope = _factory.Services.CreateScope())
            joinCode = await scope.ServiceProvider.GetRequiredService<KurxDbContext>().Groups.Where(g => g.EventId == eventId).Select(g => g.JoinCode).FirstAsync();

        var (_, memberId) = await LoginAsync("9700005010");
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IOrderService>().JoinGroupAsync(memberId, new JoinGroupInput(joinCode, "M", null));

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        var reg = await db.Registrations.SingleAsync(r => r.OrderId == orderId);   // ONE registration for the party
        Assert.Equal(2, await db.Admissions.CountAsync(a => a.RegistrationId == reg.Id));   // leader + member
    }

    [Fact]
    public async Task Refund_cancels_the_registration_voids_admissions_and_revokes_credentials()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700005011", "Refund Reg Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700005012");
        var (buyer, buyerId) = await LoginAsync("9700005013");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_reg_ref");

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "test", null);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        var reg = await db.Registrations.SingleAsync(r => r.OrderId == orderId);
        Assert.Equal(RegistrationState.Cancelled, reg.State);
        Assert.All(await db.Admissions.Where(a => a.RegistrationId == reg.Id).ToListAsync(), a => Assert.Equal(AdmissionState.Void, a.State));
        Assert.Equal(CredentialState.Revoked, await db.Credentials.Where(c => c.EventId == eventId && c.PersonId == buyerId).Select(c => c.State).SingleAsync());
    }

    [Fact]
    public async Task Same_person_two_orders_share_one_credential()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005014", "Dedup Cred Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, buyerId) = await LoginAsync("9700005015");

        await FreeOrderAsync(buyer, eventId, ttId);
        await FreeOrderAsync(buyer, eventId, ttId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(2, await db.Registrations.CountAsync(r => r.EventId == eventId && r.SubjectId == buyerId));
        Assert.Equal(2, await db.Admissions.CountAsync(a => a.EventId == eventId && a.PersonId == buyerId));
        Assert.Equal(1, await db.Credentials.CountAsync(c => c.EventId == eventId && c.PersonId == buyerId));   // ONE per person per event tree (§7.2)
    }

    [Fact]
    public async Task Guest_order_projects_a_contactless_registration()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005016", "Guest Reg Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        var guest = _factory.CreateClient();
        var order = await Json(await guest.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, guestName = "Walk In", guestPhone = "9700009991" }));
        var orderId = order.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var reg = await db.Registrations.SingleAsync(r => r.OrderId == orderId);
        Assert.Null(reg.SubjectId);                                            // no account (CONTACT identity)
        var adm = await db.Admissions.SingleAsync(a => a.RegistrationId == reg.Id);
        Assert.Null(adm.PersonId);
        Assert.NotNull(adm.CredentialId);                                      // guest gets a per-admission credential
    }

    // ── Reconciliation, policy authz, backfill ─────────────────────────────────

    [Fact]
    public async Task Reconciliation_detects_shadow_drift()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005017", "Recon Reg Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9700005018");
        await FreeOrderAsync(buyer, eventId, ttId);

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Empty(await svc.ReconcileAsync(eventId));

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM registrations WHERE \"EventId\" = {eventId}");
        var drift = await svc.ReconcileAsync(eventId);
        Assert.Single(drift);
        Assert.Equal(1, drift[0].Orders);
        Assert.Equal(0, drift[0].Registrations);
    }

    [Fact]
    public async Task Set_policy_requires_manage_and_validates_then_updates_axes()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005019", "Set Policy Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (stranger, _) = await LoginAsync("9700005020");

        var url = $"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/registration-policy";
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PatchAsJsonAsync(url, new { gate = "Approval" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync(url, new { gates = new[] { "Nonsense" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync(url, new { subject = "Alien" })).StatusCode);

        var ok = await Json(await owner.PatchAsJsonAsync(url, new { gates = new[] { "Approval", "Prerequisite" }, allocation = "Ranked", payment = "Sponsored" }));
        Assert.Equal("Ranked", ok.GetProperty("allocation").GetString());
        Assert.Equal("Sponsored", ok.GetProperty("payment").GetString());
        Assert.Equal(2, ok.GetProperty("gates").GetArrayLength());
    }

    [Fact]
    public async Task Get_registrations_lists_the_event_shadow()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005021", "List Reg Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9700005022");
        await FreeOrderAsync(buyer, eventId, ttId);

        var rows = (await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/registrations"))).EnumerateArray().ToList();
        Assert.Single(rows);
        Assert.Equal(1, rows[0].GetProperty("admission_count").GetInt32());
    }

    [Fact]
    public async Task Backfill_is_idempotent_and_projects_orphaned_orders()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005023", "Backfill Reg Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9700005024");
        var orderId = (await Json(await FreeOrderAsync(buyer, eventId, ttId))).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Orphan the order (delete its shadow), then backfill.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM registrations WHERE \"OrderId\" = {orderId}");
        Assert.True(await svc.BackfillAsync() >= 1);
        Assert.Equal(1, await db.Registrations.CountAsync(r => r.OrderId == orderId));

        Assert.Equal(0, await svc.BackfillAsync());   // repeated run projects nothing new
    }

    // ── Adversarial: concurrency, retry, recovery, event-tree ──────────────────

    [Fact]
    public async Task Concurrent_orders_for_one_person_heal_to_a_single_credential()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005025", "Concurrent Cred Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, buyerId) = await LoginAsync("9700005026");

        // Two orders for the SAME person fire concurrently — their post-commit projections race on the
        // per-person credential; the unique index makes one projection's SaveChanges fail (and roll back).
        var responses = await Task.WhenAll(FreeOrderAsync(buyer, eventId, ttId), FreeOrderAsync(buyer, eventId, ttId));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));   // both orders commit regardless of the race

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Order/Ticket stay authoritative: two orders, two live tickets, whatever the shadow did.
        Assert.Equal(2, await db.Orders.CountAsync(o => o.EventId == eventId && o.UserId == buyerId));
        Assert.Equal(2, await db.Tickets.CountAsync(t => t.EventId == eventId && t.UserId == buyerId && t.State != TicketState.Void));

        await svc.RepairAsync(eventId);   // reconciliation heals whatever the race dropped

        Assert.Empty(await svc.ReconcileAsync(eventId));   // shadow back in sync with the authority
        var creds = await db.Credentials.AsNoTracking().Where(c => c.EventId == eventId && c.PersonId == buyerId).ToListAsync();
        Assert.Single(creds);   // exactly ONE credential per person per event tree, even under the race
        var admCreds = await db.Admissions.AsNoTracking().Where(a => a.EventId == eventId && a.PersonId == buyerId).Select(a => a.CredentialId).ToListAsync();
        Assert.Equal(2, admCreds.Count);
        Assert.All(admCreds, cid => Assert.Equal(creds[0].Id, cid));   // both admissions linked to it
    }

    [Fact]
    public async Task Duplicate_projection_is_idempotent()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005027", "Retry Proj Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, buyerId) = await LoginAsync("9700005028");
        var orderId = (await Json(await FreeOrderAsync(buyer, eventId, ttId))).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await svc.MirrorOrderAsync(orderId);   // retry / duplicate delivery of the same order
        await svc.MirrorOrderAsync(orderId);

        Assert.Equal(1, await db.Registrations.CountAsync(r => r.OrderId == orderId));
        var reg = await db.Registrations.SingleAsync(r => r.OrderId == orderId);
        Assert.Equal(1, await db.Admissions.CountAsync(a => a.RegistrationId == reg.Id));
        Assert.Equal(1, await db.Credentials.CountAsync(c => c.EventId == eventId && c.PersonId == buyerId));
    }

    [Fact]
    public async Task Reconciliation_repairs_a_missing_projection()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005029", "Repair Proj Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, buyerId) = await LoginAsync("9700005030");
        var orderId = (await Json(await FreeOrderAsync(buyer, eventId, ttId))).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Simulate a projection that never landed (cascades the admission away too).
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM registrations WHERE \"OrderId\" = {orderId}");
        Assert.Single(await svc.ReconcileAsync(eventId));

        var repaired = await svc.RepairAsync(eventId);
        Assert.True(repaired >= 1);
        Assert.Empty(await svc.ReconcileAsync(eventId));
        Assert.Equal(1, await db.Registrations.CountAsync(r => r.OrderId == orderId));
        Assert.Equal(1, await db.Credentials.CountAsync(c => c.EventId == eventId && c.PersonId == buyerId && c.State == CredentialState.Active));
    }

    [Fact]
    public async Task Concurrent_refunds_leave_a_consistent_revoked_shadow()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700005031", "Concurrent Refund Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700005032");
        var (buyer, buyerId) = await LoginAsync("9700005033");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_conc_ref");

        async Task Refund()
        {
            using var s = _factory.Services.CreateScope();
            // A genuine refund-side concurrency clash is Phase-6's concern; this test asserts the Phase-8 shadow
            // stays consistent whichever call wins, so tolerate that and check the authoritative outcome below.
            try { await s.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "test", null); }
            catch (DbUpdateConcurrencyException) { }
        }
        await Task.WhenAll(Refund(), Refund());   // replay-safe refund; the shadow must not double-void or drift

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(OrderStatus.Refunded, await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync());   // authority refunded exactly once
        await svc.RepairAsync(eventId);

        Assert.Empty(await svc.ReconcileAsync(eventId));
        Assert.Equal(RegistrationState.Cancelled, await db.Registrations.Where(r => r.OrderId == orderId).Select(r => r.State).SingleAsync());
        Assert.Equal(CredentialState.Revoked, await db.Credentials.Where(c => c.EventId == eventId && c.PersonId == buyerId).Select(c => c.State).SingleAsync());
    }

    [Fact]
    public async Task One_credential_spans_a_fest_and_its_sub_event()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005034", "Event Tree Org");
        var (festId, festTt) = await PublishFreeEventAsync(owner, orgId);
        var (subId, subTt) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, buyerId) = await LoginAsync("9700005035");

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // Make the second event a sub-event of the fest (one-level tree, per the Event model).
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET \"ParentEventId\" = {festId} WHERE \"Id\" = {subId}");

        await FreeOrderAsync(buyer, festId, festTt);
        await FreeOrderAsync(buyer, subId, subTt);

        // ONE credential for the person across the whole tree (§7.2), keyed on the root (fest).
        var creds = await db.Credentials.AsNoTracking().Where(c => c.PersonId == buyerId).ToListAsync();
        Assert.Single(creds);
        Assert.Equal(festId, creds[0].EventId);
        var admCreds = await db.Admissions.AsNoTracking()
            .Where(a => a.PersonId == buyerId && (a.EventId == festId || a.EventId == subId))
            .Select(a => a.CredentialId).ToListAsync();
        Assert.Equal(2, admCreds.Count);                                       // one admission in the fest, one in the sub-event
        Assert.All(admCreds, cid => Assert.Equal(creds[0].Id, cid));           // both point at the single tree credential
    }

    // ── Phase 17 (V3 §16): distinct-attendance rollup ───────────────────────────

    /// <summary>A person checked into two sub-events of the same parent counts once, never twice — the fact
    /// source dedups by <c>PersonId</c>, not by admission/ticket row (§16).</summary>
    [Fact]
    public async Task Distinct_checkin_count_dedupes_a_person_across_sub_events_of_the_same_parent()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005050", "Dedup Org");
        var (festId, festTt) = await PublishFreeEventAsync(owner, orgId);
        var (subAId, subATt) = await PublishFreeEventAsync(owner, orgId);
        var (subBId, subBTt) = await PublishFreeEventAsync(owner, orgId);
        var (buyer1, buyer1Id) = await LoginAsync("9700005051");
        var (buyer2, buyer2Id) = await LoginAsync("9700005052");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET \"ParentEventId\" = {festId} WHERE \"Id\" = {subAId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET \"ParentEventId\" = {festId} WHERE \"Id\" = {subBId}");

        // buyer1 attends all three (fest + both sub-events); buyer2 attends only subA.
        await FreeOrderAsync(buyer1, festId, festTt);
        await FreeOrderAsync(buyer1, subAId, subATt);
        await FreeOrderAsync(buyer1, subBId, subBTt);
        await FreeOrderAsync(buyer2, subAId, subATt);

        // No HTTP check-in route in this suite's scope — set the one real leaf-fact timestamp
        // DistinctCheckedInAsync/CheckedInByEventAsync read directly.
        var tickets = await db.Tickets
            .Where(t => (t.UserId == buyer1Id || t.UserId == buyer2Id) && (t.EventId == festId || t.EventId == subAId || t.EventId == subBId))
            .ToListAsync();
        Assert.Equal(4, tickets.Count);
        foreach (var t in tickets) t.CheckedInAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var facts = scope.ServiceProvider.GetRequiredService<IAnalyticsFactSource>();
        var eventIds = new[] { festId, subAId, subBId };
        Assert.Equal(2, await facts.DistinctCheckedInAsync(eventIds, default));                       // 2 real people
        Assert.Equal(4, (await facts.CheckedInByEventAsync(eventIds, default)).Values.Sum());          // 4 per-event check-ins, undeduped
    }

    // ── Phase 9: authority cut-over — Pass, VAR, authoritative admission ───────

    [Fact]
    public async Task Creating_a_ticket_type_creates_a_pass_and_single_admission_right()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005040", "Pass Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var pass = await db.Passes.SingleAsync(p => p.TicketTypeId == ttId);
        Assert.Equal(eventId, pass.EventId);
        var right = await db.AdmissionRights.SingleAsync(r => r.PassId == pass.Id);
        Assert.Equal(AdmissionScope.Single, right.Scope);   // Phase 9 Option A: SINGLE only
        Assert.Equal(eventId, right.EventId);
        Assert.Equal(1, right.Uses);
    }

    [Fact]
    public async Task Free_order_creates_the_admission_in_the_same_transaction()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005041", "Authoritative Adm Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, buyerId) = await LoginAsync("9700005042");

        await FreeOrderAsync(buyer, eventId, ttId);

        // Authoritative (§6.1/§17.1): the admission exists the instant the order returns — no background job, no
        // eventual consistency — and it reconciles against the pool's authoritative Consumed.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var adm = await db.Admissions.SingleAsync(a => a.EventId == eventId && a.PersonId == buyerId);
        Assert.Equal(AdmissionState.Active, adm.State);
        Assert.NotNull(adm.PoolId);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IInventoryService>().ReconcileAsync(eventId));
    }

    [Fact]
    public async Task Paid_order_records_a_var_line_equal_to_the_price()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700005043", "VAR Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700005044");
        var (buyer, _) = await LoginAsync("9700005045");
        var orderId = (await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }))).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var vars = await db.ValueAllocationRecords.Where(v => v.OrderId == orderId).ToListAsync();
        Assert.Single(vars);                               // one line per order item (single-scope)
        Assert.Equal(eventId, vars[0].EventId);
        Assert.Equal(50000, vars[0].AllocatedPaise);       // the full price — the paid ticket is priced 50000
        Assert.Equal("list_price", vars[0].Basis);
    }

    [Fact]
    public async Task Refund_amount_derives_from_the_var()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700005046", "VAR Refund Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700005047");
        var (buyer, _) = await LoginAsync("9700005048");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_var_ref");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var varSum = await db.ValueAllocationRecords.Where(v => v.OrderId == orderId).SumAsync(v => v.AllocatedPaise);

        var result = await scope.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "test", null);
        Assert.True(result.Ok);
        Assert.Equal(varSum, result.Value!.AmountPaise);   // the refund reads the VAR, never recomputes (§9.5)
        Assert.Equal(varSum, await db.Refunds.Where(r => r.OrderId == orderId).Select(r => r.AmountPaise).SingleAsync());
    }

    [Fact]
    public async Task Backfill_creates_passes_and_var_for_pre_phase9_orders()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700005049", "P9 Backfill Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9700005050");
        var orderId = (await Json(await FreeOrderAsync(buyer, eventId, ttId))).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEventRegistrationService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Simulate pre-Phase-9 data: strip the Pass/AdmissionRight and the VAR the live path created.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM passes WHERE \"EventId\" = {eventId}");   // cascades admission_rights
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM var_lines WHERE \"OrderId\" = {orderId}");

        await svc.BackfillAsync();
        Assert.Equal(1, await db.Passes.CountAsync(p => p.TicketTypeId == ttId));
        Assert.Equal(1, await db.AdmissionRights.CountAsync(r => r.EventId == eventId));
        Assert.Equal(1, await db.ValueAllocationRecords.CountAsync(v => v.OrderId == orderId));
        Assert.Equal(0, await svc.BackfillAsync());   // idempotent
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> FreeOrderAsync(HttpClient client, Guid eventId, Guid ttId)
        => client.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, 0, "Individual", null, null);
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishGroupEventAsync(HttpClient owner, Guid orgId)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, 0, "Group", 2, 5);
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private async Task<(HttpClient Owner, Guid OrgId, Guid OwnerId)> PaidOwnerAsync(string phone, string name)
    {
        var (owner, ownerId) = await LoginAsync(phone);
        await owner.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Owner" });
        await owner.PostAsJsonAsync("/v1/me/identity/bank", new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Owner" });
        return (owner, _factory.SeedVerifiedOrgForClient(owner, name, "Company"), ownerId);
    }

    private async Task<(Guid EventId, Guid TicketTypeId, HttpClient Reviewer)> PublishPaidEventAsync(HttpClient owner, Guid orgId, string reviewerPhone)
    {
        var reviewer = await ReviewerAsync(reviewerPhone);
        var eventId = await CreateEventAsync(owner, orgId);
        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, 50000, "Individual", null, null);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId, reviewer);
    }

    private async Task<Guid> CreateTicketTypeAsync(HttpClient owner, Guid orgId, Guid eventId, long pricePaise, string mode, int? min, int? max)
    {
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "T", pricePaise, pricingUnit = "PerTicket", registrationMode = mode,
            groupMin = min, groupMax = max, quantity = 200,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Reg " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> CaptureAsync(string razorpayOrderId, string paymentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay")
        {
            Content = JsonContent.Create(new { order_id = razorpayOrderId, payment_id = paymentId }),
        };
        req.Headers.Add("X-Razorpay-Signature", "mock-signature");
        return await _factory.CreateClient().SendAsync(req);
    }

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>().GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
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
