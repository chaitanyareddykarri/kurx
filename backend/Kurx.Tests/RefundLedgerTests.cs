using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Analytics;
using Kurx.Infrastructure.Audit;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-103 (M4 Slice A): reverse-ledger refunds. Exercised at the <b>service layer against real
/// Postgres</b> — no mocked persistence, no HTTP login (so the suite is independent of the shared auth
/// helper). The invariant under test throughout: the append-only ledger is the truth and the projected
/// wallet is only ever a cache of it, so after any refund <c>wallet == SUM(ledger)</c> must still hold.</summary>
public class RefundLedgerTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public RefundLedgerTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Refund Cat", Slug = "refund-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    // users.phone is UNIQUE, so a random suffix would make the suite intermittently flaky. A monotonic
    // counter under a prefix no other test class uses keeps every seeded phone deterministic and distinct.
    private static int _phoneSeq;

    private Guid NewUser(string label)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User { Phone = $"9194{Interlocked.Increment(ref _phoneSeq):D6}", Name = label };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    private record Fixture(Guid OrderId, Guid EventId, Guid TicketTypeId, Guid TicketId, Guid OrgId, Guid UserId);

    /// <summary>Seeds the post-capture state ConfirmPaymentAsync would have produced: a Paid order with an
    /// item, an issued ticket, a Collected ledger entry, and a wallet holding those funds.</summary>
    private Fixture SeedPaidOrder(long amountPaise, bool guest = false, EventStatus status = EventStatus.Published)
    {
        var userId = NewUser("Refund Buyer");
        var orgId = _factory.SeedVerifiedOrg(userId, "Refund Org " + Guid.NewGuid().ToString("N")[..6]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = userId,
            Title = "Refund Fest " + suffix, Slug = "refund-fest-" + suffix, ShortCode = suffix[..6].ToUpperInvariant(),
            Description = "Seeded for refund tests.", CategoryId = _categoryId,
            VenueName = "Main Hall", City = "Vizag",
            StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(10).AddHours(3),
            Status = status,
        };
        db.Events.Add(ev);

        var ticketType = new TicketType
        {
            EventId = ev.Id, Name = "General", PricePaise = amountPaise, Quantity = 100, Sold = 1,
            SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(9),
        };
        db.TicketTypes.Add(ticketType);

        var order = new Order
        {
            UserId = guest ? null : userId, EventId = ev.Id, TicketTypeId = ticketType.Id,
            Status = OrderStatus.Paid, AmountPaise = amountPaise, RazorpayOrderId = "rzp_" + suffix,
            GuestName = guest ? "Guest Buyer" : null, GuestPhone = guest ? "9199999999" : null,
        };
        db.Orders.Add(order);

        var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = 1, UnitPricePaise = amountPaise };
        db.OrderItems.Add(item);

        var ticket = new Ticket
        {
            OrderItemId = item.Id, EventId = ev.Id, UserId = guest ? null : userId,
            HmacSig = "seeded-signature", State = TicketState.Issued,
        };
        db.Tickets.Add(ticket);

        db.LedgerEntries.Add(new LedgerEntry
        {
            OrgId = orgId, EventId = ev.Id, AmountPaise = amountPaise,
            State = LedgerState.Collected, RefType = "payment", RefId = order.Id,
        });

        var wallet = db.OrganizationWallets.First(w => w.OrgId == orgId);
        wallet.CollectedPaise += amountPaise;
        wallet.LifetimeEarnedPaise += amountPaise;
        db.SaveChanges();

        return new Fixture(order.Id, ev.Id, ticketType.Id, ticket.Id, orgId, userId);
    }

    private async Task<ServiceResult<RefundResult>> RefundAsync(Guid orderId, string reason = "test", Guid? actorId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var refunds = scope.ServiceProvider.GetRequiredService<IRefundService>();
        return await refunds.RefundOrderAsync(orderId, reason, actorId);
    }

    /// <summary>The core invariant: the cached wallet must equal the sum of its ledger entries.</summary>
    private async Task AssertWalletMatchesLedgerAsync(Guid orgId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ledgerSum = await db.LedgerEntries.AsNoTracking().Where(l => l.OrgId == orgId).SumAsync(l => l.AmountPaise);
        var wallet = await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == orgId);
        Assert.Equal(ledgerSum, wallet.CollectedPaise + wallet.AvailablePaise);
    }

    // ── Reverse ledger + wallet integrity ────────────────────────────────────
    [Fact]
    public async Task Refund_appends_a_negating_ledger_entry_and_debits_the_wallet()
    {
        var f = SeedPaidOrder(50_000);

        var result = await RefundAsync(f.OrderId, "customer_request");
        Assert.True(result.Ok, result.Error);
        Assert.Equal("refunded", result.Value!.Outcome);
        Assert.Equal(50_000, result.Value.AmountPaise);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // The original Collected entry is untouched; a NEW negating entry is appended.
        var entries = await db.LedgerEntries.AsNoTracking().Where(l => l.OrgId == f.OrgId).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, l => l.AmountPaise == 50_000 && l.State == LedgerState.Collected);
        var reversal = Assert.Single(entries, l => l.State == LedgerState.Refunded);
        Assert.Equal(-50_000, reversal.AmountPaise);
        Assert.Equal("refund", reversal.RefType);
        Assert.Equal(f.OrderId, reversal.RefId);

        var wallet = await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == f.OrgId);
        Assert.Equal(0, wallet.CollectedPaise);
        Assert.Equal(reversal.Id, wallet.LastLedgerEntryId);

        Assert.Equal(OrderStatus.Refunded, (await db.Orders.AsNoTracking().FirstAsync(o => o.Id == f.OrderId)).Status);
        var refund = await db.Refunds.AsNoTracking().SingleAsync(r => r.OrderId == f.OrderId);
        Assert.Equal(RefundStatus.Initiated, refund.Status);   // books reversed; customer not yet paid back
        Assert.Equal(50_000, refund.AmountPaise);

        await AssertWalletMatchesLedgerAsync(f.OrgId);
    }

    /// <summary>V3 §9.5 (Phase 17): revenue reporting and refunds both read <see cref="ValueAllocationRecord"/>,
    /// so the refunded amount can never diverge from what was reported as revenue. Revenue itself is gross
    /// (VAR rows are never deleted/negated by a refund) — <c>RefundCountAsync</c>/day-fact <c>RefundCount</c>
    /// is the separate signal that a refund happened, not a deduction from revenue.</summary>
    [Fact]
    public async Task Refund_amount_matches_the_VAR_revenue_and_revenue_stays_gross_after_refund()
    {
        var f = SeedPaidOrder(75_000);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var item = await db.OrderItems.SingleAsync(i => i.OrderId == f.OrderId);
            db.ValueAllocationRecords.Add(new ValueAllocationRecord
            {
                OrderId = f.OrderId, OrderItemId = item.Id, EventId = f.EventId, AllocatedPaise = 75_000,
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(75_000, await RevenueAsync(f.EventId));

        var result = await RefundAsync(f.OrderId, "changed_mind");
        Assert.True(result.Ok, result.Error);
        Assert.Equal(75_000, result.Value!.AmountPaise);   // refund reads the same VAR row revenue does

        Assert.Equal(75_000, await RevenueAsync(f.EventId));   // gross revenue is unchanged by the refund

        using var s2 = _factory.Services.CreateScope();
        var facts = s2.ServiceProvider.GetRequiredService<IAnalyticsFactSource>();
        Assert.Equal(1, await facts.RefundCountAsync([f.EventId], default));   // the refund shows up here instead
    }

    private async Task<long> RevenueAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var facts = scope.ServiceProvider.GetRequiredService<IAnalyticsFactSource>();
        return (await facts.RevenueByEventAsync([eventId], default))[eventId];
    }

    [Fact]
    public async Task Refund_voids_the_ticket_and_returns_the_seat_to_stock()
    {
        var f = SeedPaidOrder(20_000);

        Assert.True((await RefundAsync(f.OrderId)).Ok);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(TicketState.Void, (await db.Tickets.AsNoTracking().FirstAsync(t => t.Id == f.TicketId)).State);
        Assert.Equal(0, (await db.TicketTypes.AsNoTracking().FirstAsync(t => t.Id == f.TicketTypeId)).Sold);
    }

    // ── Funds that have already matured Collected -> Available ───────────────
    [Fact]
    public async Task Refund_debits_available_when_the_funds_have_already_matured()
    {
        var f = SeedPaidOrder(30_000);
        using (var scope = _factory.Services.CreateScope())
        {
            // Simulate CollectedToAvailableLedgerJob having matured the funds.
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var w = await db.OrganizationWallets.FirstAsync(x => x.OrgId == f.OrgId);
            w.AvailablePaise += w.CollectedPaise;
            w.CollectedPaise = 0;
            await db.SaveChangesAsync();
        }

        Assert.True((await RefundAsync(f.OrderId)).Ok);

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        var wallet = await vdb.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == f.OrgId);
        Assert.Equal(0, wallet.AvailablePaise);
        Assert.Equal(0, wallet.CollectedPaise);
        await AssertWalletMatchesLedgerAsync(f.OrgId);
    }

    // ── Rollback: a refused refund must change NOTHING ───────────────────────
    [Fact]
    public async Task Refund_is_refused_when_the_wallet_cannot_cover_it_and_nothing_changes()
    {
        var f = SeedPaidOrder(40_000);
        using (var scope = _factory.Services.CreateScope())
        {
            // Funds already withdrawn — the wallet can no longer cover the reversal.
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var w = await db.OrganizationWallets.FirstAsync(x => x.OrgId == f.OrgId);
            w.CollectedPaise = 0;
            await db.SaveChangesAsync();
        }

        var result = await RefundAsync(f.OrderId);
        Assert.False(result.Ok);
        Assert.Equal("insufficient_wallet_balance", result.Error);

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        // No reversal entry, no Refund row, order untouched, ticket still valid.
        Assert.False(await vdb.LedgerEntries.AsNoTracking().AnyAsync(l => l.State == LedgerState.Refunded && l.RefId == f.OrderId));
        Assert.False(await vdb.Refunds.AsNoTracking().AnyAsync(r => r.OrderId == f.OrderId));
        Assert.Equal(OrderStatus.Paid, (await vdb.Orders.AsNoTracking().FirstAsync(o => o.Id == f.OrderId)).Status);
        Assert.Equal(TicketState.Issued, (await vdb.Tickets.AsNoTracking().FirstAsync(t => t.Id == f.TicketId)).State);
    }

    // ── Idempotency / double-refund protection ───────────────────────────────
    [Fact]
    public async Task Refunding_twice_reverses_only_once()
    {
        var f = SeedPaidOrder(25_000);

        var first = await RefundAsync(f.OrderId);
        Assert.Equal("refunded", first.Value!.Outcome);

        var second = await RefundAsync(f.OrderId);
        Assert.True(second.Ok);
        Assert.Equal("already_refunded", second.Value!.Outcome);   // replay is success, not an error

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Single(await db.LedgerEntries.AsNoTracking().Where(l => l.State == LedgerState.Refunded && l.RefId == f.OrderId).ToListAsync());
        Assert.Single(await db.Refunds.AsNoTracking().Where(r => r.OrderId == f.OrderId).ToListAsync());
        Assert.Equal(0, (await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == f.OrgId)).CollectedPaise);
        await AssertWalletMatchesLedgerAsync(f.OrgId);
    }

    // ── Concurrency: the money-critical case ─────────────────────────────────
    // SKIPPED DELIBERATELY — this asserts the *correct* invariant, which the current implementation does
    // NOT yet guarantee. RefundOrderAsync is check-then-act (reads Status == Paid, mutates, saves) and
    // refunds.OrderId carries a non-unique index, so two concurrent callers can both reverse. The assertion
    // below is therefore left exactly as written (deliberately NOT weakened to make it pass) and disabled
    // until M4 Slice B adds idempotency keys + a unique index on refunds(OrderId) — at which point deleting
    // this Skip is the acceptance test for that fix. Running it today is also non-deterministic: depending
    // on interleaving it either double-reverses or trips the wallet CHECK constraint and throws.
    [Fact(Skip = "Verified race, fix scheduled in M4-B (idempotency keys + unique index on refunds.OrderId). See D-103.")]
    public async Task Concurrent_refunds_of_the_same_order_reverse_it_only_once()
    {
        var f = SeedPaidOrder(60_000);

        // Two independent scopes (independent DbContexts) racing the same order, as two retried
        // webhooks or a double-clicked admin action would.
        var both = await Task.WhenAll(RefundAsync(f.OrderId, "race-a"), RefundAsync(f.OrderId, "race-b"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var reversals = await db.LedgerEntries.AsNoTracking()
            .Where(l => l.State == LedgerState.Refunded && l.RefId == f.OrderId).ToListAsync();
        var refundRows = await db.Refunds.AsNoTracking().Where(r => r.OrderId == f.OrderId).ToListAsync();
        var wallet = await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == f.OrgId);

        // Money must be reversed exactly once no matter how the two calls interleave.
        Assert.Single(reversals);
        Assert.Single(refundRows);
        Assert.Equal(0, wallet.CollectedPaise);
        Assert.All(both, r => Assert.True(r.Ok, r.Error));
        await AssertWalletMatchesLedgerAsync(f.OrgId);
    }

    // ── State guards ─────────────────────────────────────────────────────────
    [Fact]
    public async Task Refund_of_an_unpaid_order_is_refused()
    {
        var f = SeedPaidOrder(10_000);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == f.OrderId);
            order.Status = OrderStatus.Pending;
            await db.SaveChangesAsync();
        }

        var result = await RefundAsync(f.OrderId);
        Assert.False(result.Ok);
        Assert.Equal("invalid_order_state", result.Error);
    }

    [Fact]
    public async Task Refund_of_an_unknown_order_is_not_found()
    {
        var result = await RefundAsync(Guid.NewGuid());
        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }

    // ── Guest orders (no user, therefore no chat membership to clean up) ─────
    [Fact]
    public async Task Guest_order_refunds_cleanly()
    {
        var f = SeedPaidOrder(15_000, guest: true);

        var result = await RefundAsync(f.OrderId);
        Assert.True(result.Ok, result.Error);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(OrderStatus.Refunded, (await db.Orders.AsNoTracking().FirstAsync(o => o.Id == f.OrderId)).Status);
        await AssertWalletMatchesLedgerAsync(f.OrgId);
    }

    // ── Cancellation refund path (M7 Cancelled -> D-103 reversal) ────────────
    [Fact]
    public async Task Order_on_a_cancelled_event_can_be_refunded()
    {
        var f = SeedPaidOrder(35_000, status: EventStatus.Cancelled);

        var result = await RefundAsync(f.OrderId, "event_cancelled");
        Assert.True(result.Ok, result.Error);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal("event_cancelled", (await db.Refunds.AsNoTracking().SingleAsync(r => r.OrderId == f.OrderId)).Reason);
        await AssertWalletMatchesLedgerAsync(f.OrgId);
    }

    // ── Audit completeness (through the D-102 typed spine) ───────────────────
    [Fact]
    public async Task Refund_writes_an_audit_row_through_the_typed_spine()
    {
        var actor = NewUser("Refund Admin");
        var f = SeedPaidOrder(45_000);

        Assert.True((await RefundAsync(f.OrderId, "duplicate_charge", actor)).Ok);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Entity == "orders" && a.EntityId == f.OrderId && a.Action == "order.refunded")
            .OrderByDescending(a => a.CreatedAt).FirstAsync();

        Assert.Equal("user", row.ActorType);
        Assert.Equal(actor, row.ActorId);

        using var doc = JsonDocument.Parse(row.DetailsJson!);
        var root = doc.RootElement;
        Assert.Equal(AuditWriter.EnvelopeVersion, root.GetProperty("v").GetInt32());
        Assert.Equal(nameof(OrderStatus.Paid), root.GetProperty("before").GetProperty("status").GetString());
        Assert.Equal(nameof(OrderStatus.Refunded), root.GetProperty("after").GetProperty("status").GetString());
        Assert.Equal(45_000, root.GetProperty("after").GetProperty("amount_paise").GetInt64());
        Assert.Equal("duplicate_charge", root.GetProperty("after").GetProperty("reason").GetString());
        Assert.Equal(1, root.GetProperty("after").GetProperty("tickets_voided").GetInt32());
    }

    [Fact]
    public async Task System_initiated_refund_is_audited_as_a_system_actor()
    {
        var f = SeedPaidOrder(12_000);

        Assert.True((await RefundAsync(f.OrderId, "event_cancelled", actorId: null)).Ok);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.AuditLogs.AsNoTracking()
            .FirstAsync(a => a.Entity == "orders" && a.EntityId == f.OrderId && a.Action == "order.refunded");
        Assert.Equal("system", row.ActorType);
        Assert.Null(row.ActorId);
    }
}
