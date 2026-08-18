using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §8 (Phase 7) — inventory pools as an additive dual-write shadow of the scalar Quantity/Sold.
/// One general pool per TicketType; Consumed tracks Sold at every sale/refund/hold-expiry; the waitlist points
/// at a pool; reconciliation proves consumed == active admissions. The scalar stays the oversell authority.</summary>
public class InventoryTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public InventoryTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    // ── Pool lifecycle & dual-write ──────────────────────────────────────────

    [Fact]
    public async Task Ticket_type_creation_mints_a_general_pool()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004001", "Pool Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 50);

        var pool = await PoolAsync(ttId);
        Assert.Equal("General", pool.Segment.ToString());
        Assert.Equal("InPerson", pool.Channel.ToString());
        Assert.Equal("PersonSlot", pool.Unit.ToString());
        Assert.Equal(50, pool.Total);
        Assert.Equal(0, pool.Consumed);
    }

    [Fact]
    public async Task Free_order_dual_writes_pool_consumed()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004002", "Free Pool Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9700004003");

        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(buyer, eventId, ttId)).StatusCode);

        var pool = await PoolAsync(ttId);
        Assert.Equal(1, pool.Consumed);
        Assert.Equal(await SoldAsync(ttId), pool.Consumed);   // mirrors the scalar exactly
    }

    [Fact]
    public async Task Refund_returns_inventory_to_the_pool()
    {
        var (owner, orgId, ownerId) = await PaidOwnerAsync("9700004004", "Refund Pool Org");
        var (eventId, ttId, reviewer) = await PublishPaidEventAsync(owner, orgId, "9700004005");
        var (buyer, _) = await LoginAsync("9700004006");

        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_inv_1");
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "test", null);

        var pool = await PoolAsync(ttId);
        Assert.Equal(0, pool.Consumed);                        // inventory returned on refund (§8)
        Assert.Equal(await SoldAsync(ttId), pool.Consumed);
    }

    [Fact]
    public async Task Paid_hold_dual_writes_and_hold_expiry_returns_inventory()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004007", "Hold Pool Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004008");
        var (buyer, _) = await LoginAsync("9700004009");

        await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });   // pending order → HOLD, no consume
        var held = await PoolAsync(ttId);
        Assert.Equal(1, held.Held);            // Phase 9: the paid pending order holds the seat
        Assert.Equal(0, held.Consumed);

        // Expire the hold and run the sweeper — the held seat returns to the pool.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE seat_holds SET \"ExpiresAt\" = {DateTime.UtcNow.AddMinutes(-1)} WHERE \"Status\" = 'Active'");
            await scope.ServiceProvider.GetRequiredService<ExpireSeatHoldsJob>().RunAsync(CancellationToken.None);
        }

        var pool = await PoolAsync(ttId);
        Assert.Equal(0, pool.Held);            // hold released
        Assert.Equal(0, pool.Consumed);
    }

    // ── Reconciliation, waitlist, policy, read ───────────────────────────────

    [Fact]
    public async Task Reconciliation_detects_drift_and_reports_none_when_in_sync()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004010", "Recon Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9700004011");
        await FreeOrderAsync(buyer, eventId, ttId);

        using var scope = _factory.Services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.Empty(await inventory.ReconcileAsync(eventId));   // Consumed(1) == active admissions(1) → no drift

        // Corrupt the authoritative pool directly; reconciliation must surface it against the active admissions.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory_pools SET \"Consumed\" = 5 WHERE \"TicketTypeId\" = {ttId}");
        var drift = await inventory.ReconcileAsync(eventId);
        Assert.Single(drift);
        Assert.Equal(5, drift[0].Consumed);
        Assert.Equal(1, drift[0].ActiveAdmissions);   // Phase 9 invariant: Consumed == count(active admissions)
    }

    [Fact]
    public async Task Paid_held_order_does_not_report_reconciliation_drift()
    {
        // Phase 9: a held-but-uncaptured paid order sits in Held (not Consumed) and has NO admission yet.
        // Reconciliation (Consumed == active admissions) sees 0 == 0 and correctly reports no drift.
        var (owner, orgId, _) = await PaidOwnerAsync("9700004060", "Held Recon Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004061");
        var (buyer, _) = await LoginAsync("9700004062");

        await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });   // pending + hold, no ticket

        using var scope = _factory.Services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var pool = await PoolAsync(ttId);
        Assert.Equal(1, pool.Held);        // the seat is reserved, not consumed
        Assert.Equal(0, pool.Consumed);
        Assert.Empty(await inventory.ReconcileAsync(eventId));   // Consumed(0) == active admissions(0) — no false drift
    }

    [Fact]
    public async Task Concurrent_purchases_keep_pool_and_scalar_synced()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004063", "Concurrent Buy Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 100);
        var buyers = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => LoginAsync($"970000407{i}")));

        await Task.WhenAll(buyers.Select(b => FreeOrderAsync(b.Client, eventId, ttId)));

        // Phase 9: the pool's authoritative Consumed is race-free (conditional decrement) — exactly 5, no lost
        // updates. It reconciles against the admissions, not the legacy Sold mirror (which may undercount).
        var pool = await PoolAsync(ttId);
        Assert.Equal(5, pool.Consumed);
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IInventoryService>().ReconcileAsync(eventId));
    }

    [Fact]
    public async Task Concurrent_refunds_keep_pool_and_scalar_synced()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004075", "Concurrent Refund Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004076");
        var orderIds = new List<Guid>();
        foreach (var phone in new[] { "9700004077", "9700004078" })
        {
            var (buyer, _) = await LoginAsync(phone);
            var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
            orderIds.Add(order.GetProperty("id").GetGuid());
            await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_" + phone);
        }
        Assert.Equal(2, (await PoolAsync(ttId)).Consumed);

        await Task.WhenAll(orderIds.Select(async id =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(id, "test", null);
        }));

        // Phase 9: the conditional release makes concurrent refunds atomic — both seats return, Consumed lands
        // on exactly 0 (no lost decrement), and the pool reconciles against the (now zero) active admissions.
        var pool = await PoolAsync(ttId);
        Assert.Equal(0, pool.Consumed);
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IInventoryService>().ReconcileAsync(eventId));
    }

    [Fact]
    public async Task Duplicate_refund_is_safe_and_keeps_sync()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004080", "Dup Refund Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004081");
        var (buyer, _) = await LoginAsync("9700004082");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_dup");

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "test", null);
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "test", null);   // duplicate

        var pool = await PoolAsync(ttId);
        Assert.Equal(0, pool.Consumed);                         // decremented once, not twice
        Assert.Equal(await SoldAsync(ttId), pool.Consumed);
    }

    [Fact]
    public async Task Duplicate_hold_expiry_is_safe_and_keeps_sync()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004083", "Dup Expiry Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004084");
        var (buyer, _) = await LoginAsync("9700004085");
        await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE seat_holds SET \"ExpiresAt\" = {DateTime.UtcNow.AddMinutes(-1)} WHERE \"Status\" = 'Active'");
        }
        for (var i = 0; i < 2; i++)   // the second sweep finds no active holds
            using (var scope = _factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ExpireSeatHoldsJob>().RunAsync(CancellationToken.None);

        var pool = await PoolAsync(ttId);
        Assert.Equal(0, pool.Consumed);                         // released once, not twice
        Assert.Equal(await SoldAsync(ttId), pool.Consumed);
    }

    [Fact]
    public async Task A_failed_order_changes_neither_scalar_nor_pool()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004086", "Failed Order Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 1);
        var (buyer1, _) = await LoginAsync("9700004087");
        var (buyer2, _) = await LoginAsync("9700004088");

        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(buyer1, eventId, ttId)).StatusCode);   // sells out (qty 1)
        var sold = await SoldAsync(ttId);
        var consumed = (await PoolAsync(ttId)).Consumed;

        Assert.NotEqual(HttpStatusCode.OK, (await FreeOrderAsync(buyer2, eventId, ttId)).StatusCode);  // sold out → fails
        Assert.Equal(sold, await SoldAsync(ttId));              // the failed order touched neither counter
        Assert.Equal(consumed, (await PoolAsync(ttId)).Consumed);
    }

    [Fact]
    public async Task Backfill_is_idempotent_and_creates_missing_pools()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004089", "Backfill Idem Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 40);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inventory_pools WHERE \"TicketTypeId\" = {ttId}");   // simulate a legacy type
        Assert.True(await inventory.BackfillPoolsAsync() >= 1);
        var pool = await db.InventoryPools.AsNoTracking().SingleAsync(p => p.TicketTypeId == ttId && p.Segment == InventorySegment.General);
        Assert.Equal(40, pool.Total);

        Assert.Equal(0, await inventory.BackfillPoolsAsync());   // repeated run creates nothing
    }

    [Fact]
    public async Task Waitlist_join_points_to_the_general_pool()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004012", "Waitlist Pool Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 1);
        var (buyer, _) = await LoginAsync("9700004013");
        var (waiter, waiterId) = await LoginAsync("9700004014");

        Assert.Equal(HttpStatusCode.OK, (await FreeOrderAsync(buyer, eventId, ttId)).StatusCode);   // sells out (qty 1)

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWaitlistService>().JoinAsync(waiterId, eventId, ttId);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var poolId = await db.InventoryPools.Where(p => p.TicketTypeId == ttId && p.Segment == InventorySegment.General).Select(p => p.Id).SingleAsync();
        var entryPool = await db.TicketWaitlists.Where(w => w.TicketTypeId == ttId && w.UserId == waiterId).Select(w => w.PoolId).SingleAsync();
        Assert.Equal(poolId, entryPool);
    }

    [Fact]
    public async Task Set_policy_requires_manage_and_validates()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004015", "Policy Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (stranger, _) = await LoginAsync("9700004016");

        var url = $"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/inventory";
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PatchAsJsonAsync(url, new { oversellAllowance = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync(url, new { noShowPolicy = "Nonsense" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync(url, new { oversellAllowance = -1 })).StatusCode);

        var ok = await Json(await owner.PatchAsJsonAsync(url, new { oversellAllowance = 10, noShowPolicy = "ReleaseAfter", noShowReleaseMinutes = 30 }));
        Assert.Equal(10, ok.GetProperty("oversell_allowance").GetInt32());
        Assert.Equal("ReleaseAfter", ok.GetProperty("no_show_policy").GetString());
    }

    [Fact]
    public async Task Get_inventory_returns_pools_with_availability()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004017", "Read Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 20);
        var (buyer, _) = await LoginAsync("9700004018");
        await FreeOrderAsync(buyer, eventId, ttId);

        var pools = (await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/inventory"))).EnumerateArray().ToList();
        var pool = pools.Single(p => p.GetProperty("ticket_type_id").GetGuid() == ttId);
        Assert.Equal(20, pool.GetProperty("total").GetInt32());
        Assert.Equal(1, pool.GetProperty("consumed").GetInt32());
        Assert.Equal(19, pool.GetProperty("available").GetInt32());
    }

    [Fact]
    public async Task Updating_ticket_type_quantity_syncs_pool_total()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004019", "Quantity Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 30);

        // D-388 — the subject here is the POOL staying in step with the ticket's quantity, not when a
        // ticket type may be edited. `PublishFreeEventAsync` publishes because inventory only matters on
        // a live event, so the update runs with the status momentarily suspended and the real PATCH —
        // including the pool-sync side effect being asserted — still executes.
        var res = await _factory.WithLiveStatusSuspendedAsync(eventId, () =>
            owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}", new
        {
            name = "Free", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            groupMin = (int?)null, groupMax = (int?)null, quantity = 75,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        }));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(75, (await PoolAsync(ttId)).Total);
    }

    // ── Phase 9: §17.1 authoritative money path ───────────────────────────────

    [Fact]
    public async Task Oversell_is_prevented_under_concurrency()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004200", "Oversell Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 1);
        var buyers = await Task.WhenAll(Enumerable.Range(1, 6).Select(i => LoginAsync($"97000042{i:D2}")));

        var results = await Task.WhenAll(buyers.Select(b => FreeOrderAsync(b.Client, eventId, ttId)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));   // exactly one seat — no oversell
        var pool = await PoolAsync(ttId);
        Assert.Equal(1, pool.Consumed);
        Assert.True(pool.Consumed <= pool.Total + pool.OversellAllowance);        // the §17.1 invariant holds
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IInventoryService>().ReconcileAsync(eventId));
    }

    [Fact]
    public async Task Oversell_allowance_permits_exactly_that_many_extra()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004240", "Allowance Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 1);
        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/inventory", new { oversellAllowance = 1 });
        var buyers = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => LoginAsync($"97000042{50 + i}")));

        var results = await Task.WhenAll(buyers.Select(b => FreeOrderAsync(b.Client, eventId, ttId)));

        Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.OK));   // total(1) + oversell_allowance(1)
        Assert.Equal(2, (await PoolAsync(ttId)).Consumed);
    }

    [Fact]
    public async Task Client_idempotency_key_dedupes_a_retried_order()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004210", "Idem Order Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 50);
        var (buyer, _) = await LoginAsync("9700004211");

        async Task<JsonElement> Order(string key)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/events/{eventId}/orders")
            { Content = JsonContent.Create(new { ticketTypeId = ttId }) };
            req.Headers.Add("Idempotency-Key", key);
            return await Json(await buyer.SendAsync(req));
        }
        var first = await Order("retry-abc");
        var second = await Order("retry-abc");   // same key → the SAME order, no second seat consumed

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);
    }

    [Fact]
    public async Task Duplicate_capture_issues_one_ticket_and_consumes_once()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004220", "Dup Cap Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004221");
        var (buyer, _) = await LoginAsync("9700004222");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var rzp = order.GetProperty("razorpay_order_id").GetString()!;
        var orderId = order.GetProperty("id").GetGuid();

        await CaptureAsync(rzp, "pay_dupcap");
        await CaptureAsync(rzp, "pay_dupcap");   // re-delivered webhook — must be a no-op

        var pool = await PoolAsync(ttId);
        Assert.Equal(1, pool.Consumed);
        Assert.Equal(0, pool.Held);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var oiIds = await db.OrderItems.Where(oi => oi.OrderId == orderId).Select(oi => oi.Id).ToListAsync();
        Assert.Equal(1, await db.Tickets.CountAsync(t => oiIds.Contains(t.OrderItemId)));
        Assert.Equal(1, await db.Admissions.CountAsync(a => a.EventId == eventId && a.State != Domain.Enums.AdmissionState.Void));
    }

    [Fact]
    public async Task Concurrent_captures_consume_once()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004225", "Conc Cap Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004226");
        var (buyer, _) = await LoginAsync("9700004227");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var rzp = order.GetProperty("razorpay_order_id").GetString()!;

        await Task.WhenAll(CaptureAsync(rzp, "pay_conc_a"), CaptureAsync(rzp, "pay_conc_b"));   // race — only one wins

        var pool = await PoolAsync(ttId);
        Assert.Equal(1, pool.Consumed);   // the atomic Pending→Paid claim means only one capture converts the hold
        Assert.Equal(0, pool.Held);
    }

    [Fact]
    public async Task Conditional_decrement_locks_pools_in_deterministic_order()
    {
        // Two pools; two concurrent tasks consume BOTH, each passing them in the OPPOSITE input order. The service
        // sorts by pool id before locking, so the acquisitions never deadlock and both tasks complete.
        var (owner, orgId, _) = await LoginOrgAsync("9700004230", "Lock Order Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var ttA = await CreateTicketTypeAsync(owner, orgId, eventId, 0, 100);
        var ttB = await CreateTicketTypeAsync(owner, orgId, eventId, 0, 100);
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });

        var poolA = (await PoolAsync(ttA)).Id;
        var poolB = (await PoolAsync(ttB)).Id;

        async Task ConsumeBoth(PoolDraw[] draws)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            await using var tx = await db.Database.BeginTransactionAsync();
            Assert.True(await inv.TryConsumeManyAsync(draws));
            await tx.CommitAsync();
        }

        await Task.WhenAll(
            ConsumeBoth([new PoolDraw(poolA, 1), new PoolDraw(poolB, 1)]),
            ConsumeBoth([new PoolDraw(poolB, 1), new PoolDraw(poolA, 1)]));   // opposite order — no deadlock

        Assert.Equal(2, (await PoolAsync(ttA)).Consumed);
        Assert.Equal(2, (await PoolAsync(ttB)).Consumed);
    }

    // ── Phase 9 review-fixes: idempotency scoping, authority reads, repair, late capture ──

    private async Task<JsonElement> OrderWithKeyAsync(HttpClient c, Guid eventId, Guid ttId, string key, object? extra = null)
    {
        object body = extra is null ? new { ticketTypeId = ttId } : Merge(ttId, extra);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/events/{eventId}/orders") { Content = JsonContent.Create(body) };
        req.Headers.Add("Idempotency-Key", key);
        return await Json(await c.SendAsync(req));
    }
    private static object Merge(Guid ttId, object extra)
    {
        var d = new Dictionary<string, object?> { ["ticketTypeId"] = ttId };
        foreach (var p in extra.GetType().GetProperties()) d[p.Name] = p.GetValue(extra);
        return d;
    }

    [Fact]
    public async Task Idempotency_key_is_scoped_per_authenticated_user()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004300", "Idem Scope Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 50);
        var (userA, _) = await LoginAsync("9700004301");
        var (userB, _) = await LoginAsync("9700004302");

        var a = await OrderWithKeyAsync(userA, eventId, ttId, "SHARED-KEY");
        var b = await OrderWithKeyAsync(userB, eventId, ttId, "SHARED-KEY");   // same key, DIFFERENT user

        Assert.NotEqual(a.GetProperty("id").GetGuid(), b.GetProperty("id").GetGuid());   // B gets its OWN order, never A's
        var a2 = await OrderWithKeyAsync(userA, eventId, ttId, "SHARED-KEY");             // A's retry replays A's order
        Assert.Equal(a.GetProperty("id").GetGuid(), a2.GetProperty("id").GetGuid());
        Assert.Equal(2, (await PoolAsync(ttId)).Consumed);                                // two distinct callers → two seats
    }

    [Fact]
    public async Task Idempotency_does_not_leak_a_guest_order_or_token_to_another_caller()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004310", "Guest Isol Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 50);

        var guest = _factory.CreateClient();
        var g1 = await OrderWithKeyAsync(guest, eventId, ttId, "GKEY", new { guestName = "Gigi", guestPhone = "9700009990" });
        var guestId = g1.GetProperty("id").GetGuid();
        Assert.False(string.IsNullOrEmpty(g1.GetProperty("guest_access_token").GetString()));

        // An authenticated user replaying the SAME key gets their own order — never the guest's order or token.
        var (userB, _) = await LoginAsync("9700004311");
        var b = await OrderWithKeyAsync(userB, eventId, ttId, "GKEY");
        Assert.NotEqual(guestId, b.GetProperty("id").GetGuid());
        Assert.True(b.TryGetProperty("guest_access_token", out var tok) == false || tok.ValueKind == JsonValueKind.Null);

        // The guest's own retry (same phone + key) replays the guest's order (with its token).
        var g2 = await OrderWithKeyAsync(guest, eventId, ttId, "GKEY", new { guestName = "Gigi", guestPhone = "9700009990" });
        Assert.Equal(guestId, g2.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Availability_reads_come_from_the_pool_not_the_legacy_scalar()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004320", "Authority Read Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 2);
        var (buyer, _) = await LoginAsync("9700004321");
        await FreeOrderAsync(buyer, eventId, ttId);   // pool Consumed=1; legacy Sold=1

        // Force a divergence: the pool says sold out, the legacy scalar still says 1 sold.
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<KurxDbContext>().Database
            .ExecuteSqlInterpolatedAsync($"UPDATE inventory_pools SET \"Consumed\" = 2 WHERE \"TicketTypeId\" = {ttId}");

        // Public availability reflects the POOL (0), not Quantity − Sold (which would be 1).
        var pub = await scope.ServiceProvider.GetRequiredService<ITicketTypeService>().ListPublicAsync(eventId);
        Assert.Equal(0, pub.Value!.Single(t => t.Id == ttId).Available);

        // Waitlist join is now allowed because the POOL is sold out.
        var (_, waiterId) = await LoginAsync("9700004322");
        Assert.True((await scope.ServiceProvider.GetRequiredService<IWaitlistService>().JoinAsync(waiterId, eventId, ttId)).Ok);
    }

    [Fact]
    public async Task Inventory_reconciliation_repairs_consumed_to_active_admissions()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700004330", "Inv Repair Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 50);
        var (buyer, _) = await LoginAsync("9700004331");
        await FreeOrderAsync(buyer, eventId, ttId);   // Consumed=1, one active admission

        using var scope = _factory.Services.CreateScope();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory_pools SET \"Consumed\" = 9 WHERE \"TicketTypeId\" = {ttId}");
        Assert.Single(await inv.ReconcileAsync(eventId));

        Assert.Equal(1, await inv.RepairAsync(eventId));       // heals the one drifting pool
        Assert.Empty(await inv.ReconcileAsync(eventId));
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);     // set to the active-admission ground truth
    }

    [Fact]
    public async Task Late_capture_after_hold_expiry_still_consumes_and_stays_in_sync()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004340", "Late Cap Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004341");
        var (buyer, _) = await LoginAsync("9700004342");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var rzp = order.GetProperty("razorpay_order_id").GetString()!;

        // Expire + release the hold BEFORE the capture arrives (a genuinely late capture).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE seat_holds SET \"ExpiresAt\" = {DateTime.UtcNow.AddMinutes(-1)} WHERE \"Status\" = 'Active'");
            await scope.ServiceProvider.GetRequiredService<ExpireSeatHoldsJob>().RunAsync(CancellationToken.None);
        }
        Assert.Equal(0, (await PoolAsync(ttId)).Held);

        await CaptureAsync(rzp, "pay_late");   // honoured: the seat is consumed unconditionally

        var pool = await PoolAsync(ttId);
        Assert.Equal(1, pool.Consumed);
        using var s = _factory.Services.CreateScope();
        Assert.Empty(await s.ServiceProvider.GetRequiredService<IInventoryService>().ReconcileAsync(eventId));   // Consumed == active admissions
    }

    [Fact]
    public async Task Concurrent_full_refunds_create_exactly_one_refund_row()
    {
        var (owner, orgId, _) = await PaidOwnerAsync("9700004350", "One Refund Org");
        var (eventId, ttId, _) = await PublishPaidEventAsync(owner, orgId, "9700004351");
        var (buyer, _) = await LoginAsync("9700004352");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_one_ref");

        async Task Refund()
        {
            using var s = _factory.Services.CreateScope();
            try { await s.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "t", null); }
            catch (DbUpdateException) { }
        }
        await Task.WhenAll(Refund(), Refund());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.Refunds.CountAsync(r => r.OrderId == orderId));   // atomic status claim, not a unique index
        Assert.Equal(0, (await PoolAsync(ttId)).Consumed);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<Domain.Entities.InventoryPool> PoolAsync(Guid ttId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.InventoryPools.AsNoTracking().SingleAsync(p => p.TicketTypeId == ttId && p.Segment == InventorySegment.General);
    }

    private async Task<int> SoldAsync(Guid ttId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.TicketTypes.Where(t => t.Id == ttId).Select(t => t.Sold).SingleAsync();
    }

    private Task<HttpResponseMessage> FreeOrderAsync(HttpClient client, Guid eventId, Guid ttId)
        => client.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId, int quantity = 100)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, 0, quantity);
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
        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, 50000, 100);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId, reviewer);
    }

    private async Task<Guid> CreateTicketTypeAsync(HttpClient owner, Guid orgId, Guid eventId, long pricePaise, int quantity)
    {
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Free", pricePaise, pricingUnit = "PerTicket", registrationMode = "Individual",
            groupMin = (int?)null, groupMax = (int?)null, quantity,
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
            title = "Inv " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
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
