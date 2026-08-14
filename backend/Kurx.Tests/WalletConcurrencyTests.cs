using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>The projected <see cref="OrganizationWallet"/> is a CACHE of the append-only ledger, so
/// <c>wallet == SUM(ledger)</c> has to survive concurrency, not just the happy path.
///
/// <para><b>The gap these close.</b> <see cref="RefundLedgerTests"/> already asserts that invariant for a
/// single refund and for two concurrent refunds of the <i>same</i> order — but the same-order case is
/// guarded by the atomic Paid→Refunded claim, so it never exercised the wallet arithmetic itself. The
/// untested case was several orders sharing ONE wallet: there the mutation was a plain read-modify-write
/// (<c>wallet.CollectedPaise += x</c>), which EF compiles to <c>SET "CollectedPaise" = &lt;literal&gt;</c>.
/// Postgres READ COMMITTED does not prevent a lost update there — the second writer blocks on the row
/// lock, then overwrites with a value it computed before the first one landed.</para>
///
/// <para>Measured before the fix: six concurrent captures produced six correct ledger rows summing to
/// 60000 paise and a wallet crediting 10000 — five payments missing from the org's balance. Six concurrent
/// refunds netted the ledger to 0 and left 50000 paise of phantom, still-withdrawable funds. Both
/// directions are now in-SQL increments, evaluated by Postgres under the row lock.</para></summary>
public class WalletConcurrencyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    // users.phone is UNIQUE, so a random suffix would make the suite intermittently flaky. A monotonic
    // counter under a prefix no other test class uses keeps every seeded phone deterministic and distinct.
    private static int _phoneSeq;

    public WalletConcurrencyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Wallet Cat", Slug = "wallet-cat" };
            db.EventCategories.Add(cat);
            db.SaveChanges();
            _categoryId = cat.Id;
            _reset = true;
        }
    }

    private const long Amount = 10_000;
    private const int Orders = 6;
    private const long Total = Amount * Orders;

    [Fact]
    public async Task Concurrent_captures_in_one_org_credit_the_wallet_once_each()
    {
        var (orgId, userId) = NewOrg("Capture");
        var gatewayIds = Enumerable.Range(0, Orders).Select(_ => SeedPendingOrder(orgId, userId)).ToList();

        var results = await Task.WhenAll(gatewayIds.Select(async gid =>
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IOrderService>()
                .ConfirmPaymentAsync(gid, "pay_" + gid);
        }));
        Assert.All(results, r => Assert.True(r.Ok, r.Error));

        using var scope2 = _factory.Services.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<KurxDbContext>();
        var wallet = await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == orgId);

        Assert.Equal(Total, await LedgerSumAsync(db, orgId));
        Assert.Equal(Total, wallet.CollectedPaise + wallet.AvailablePaise);
        Assert.Equal(Total, wallet.LifetimeEarnedPaise);   // append-only counter, also lost updates before
    }

    [Fact]
    public async Task Concurrent_refunds_of_different_orders_in_one_org_debit_the_wallet_once_each()
    {
        var (orgId, userId) = NewOrg("Refund");
        var orderIds = Enumerable.Range(0, Orders).Select(_ => SeedPaidOrder(orgId, userId)).ToList();

        using (var s = _factory.Services.CreateScope())
        {
            var seeded = await s.ServiceProvider.GetRequiredService<KurxDbContext>()
                .OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == orgId);
            Assert.Equal(Total, seeded.CollectedPaise);
        }

        var results = await Task.WhenAll(orderIds.Select(async id =>
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IRefundService>()
                .RefundOrderAsync(id, "concurrency-test", null);
        }));
        Assert.All(results, r => Assert.True(r.Ok, r.Error));

        using var scope2 = _factory.Services.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<KurxDbContext>();
        var wallet = await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == orgId);

        Assert.Equal(0, await LedgerSumAsync(db, orgId));                 // 6 credits + 6 reversals
        Assert.Equal(0, wallet.CollectedPaise + wallet.AvailablePaise);   // cache agrees with the ledger
    }

    /// <summary>The detector that was missing. Inventory and registrations each had a reconciliation job
    /// proving their invariant; money did not — which is how the lost-update bug could have run in
    /// production with the ledger correct, the balance wrong, and no request ever failing.</summary>
    [Fact]
    public async Task Wallet_drift_is_detected_and_repairable()
    {
        var (orgId, userId) = NewOrg("Drift");
        SeedPaidOrder(orgId, userId);   // ledger +10000, wallet +10000 — in sync

        using (var scope = _factory.Services.CreateScope())
        {
            var wallets = scope.ServiceProvider.GetRequiredService<IWalletService>();
            Assert.Empty(await wallets.ReconcileAsync(orgId));
        }

        // Corrupt the cache exactly as a lost update would: ledger untouched, balance wrong.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.OrganizationWallets.Where(w => w.OrgId == orgId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.CollectedPaise, 3_000L));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var wallets = scope.ServiceProvider.GetRequiredService<IWalletService>();
            var drift = Assert.Single(await wallets.ReconcileAsync(orgId));
            Assert.Equal(3_000, drift.CachedPaise);
            Assert.Equal(Amount, drift.LedgerPaise);
            Assert.Equal(3_000 - Amount, drift.DeltaPaise);   // negative: the cache under-states

            Assert.Equal(1, await wallets.RepairAsync(orgId));
            Assert.Empty(await wallets.ReconcileAsync(orgId));
        }

        using var verify = _factory.Services.CreateScope();
        var wallet = await verify.ServiceProvider.GetRequiredService<KurxDbContext>()
            .OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == orgId);
        // Repair puts the correction on Collected, never Available — Available is what withdrawals pay
        // out against, so a repair must never be able to create a withdrawable balance.
        Assert.Equal(Amount, wallet.CollectedPaise);
        Assert.Equal(0, wallet.AvailablePaise);
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static Task<long> LedgerSumAsync(KurxDbContext db, Guid orgId) =>
        db.LedgerEntries.AsNoTracking().Where(l => l.OrgId == orgId).SumAsync(l => l.AmountPaise);

    private (Guid OrgId, Guid UserId) NewOrg(string label)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User { Phone = $"9192{Interlocked.Increment(ref _phoneSeq):D6}", Name = label + " Buyer" };
        db.Users.Add(user);
        db.SaveChanges();
        return (_factory.SeedVerifiedOrg(user.Id, $"{label} Org {Guid.NewGuid():N}"[..24]), user.Id);
    }

    /// <summary>An event + ticket type + order in the given org. Several of these share ONE wallet — the
    /// arrangement the same-order concurrency tests never produced.</summary>
    private (KurxDbContext Db, Event Ev, TicketType Tt, Order Order, IServiceScope Scope) NewOrder(
        Guid orgId, Guid userId, OrderStatus status)
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = userId,
            Title = "Wallet Fest " + suffix, Slug = "wallet-fest-" + suffix, ShortCode = suffix[..6].ToUpperInvariant(),
            Description = "Seeded for wallet concurrency tests.", CategoryId = _categoryId,
            VenueName = "Main Hall", City = "Vizag",
            StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(10).AddHours(3),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);

        var tt = new TicketType
        {
            EventId = ev.Id, Name = "General", PricePaise = Amount, Quantity = 100,
            Sold = status == OrderStatus.Paid ? 1 : 0,
            SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(9),
        };
        db.TicketTypes.Add(tt);

        var order = new Order
        {
            UserId = userId, EventId = ev.Id, TicketTypeId = tt.Id,
            Status = status, AmountPaise = Amount, RazorpayOrderId = "rzp_" + suffix,
        };
        db.Orders.Add(order);
        db.OrderItems.Add(new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = Amount });
        return (db, ev, tt, order, scope);
    }

    /// <summary>A Pending order with a gateway id, exactly as CreateOrderAsync leaves it before capture.</summary>
    private string SeedPendingOrder(Guid orgId, Guid userId)
    {
        var (db, _, _, order, scope) = NewOrder(orgId, userId, OrderStatus.Pending);
        using (scope) { db.SaveChanges(); }
        return order.RazorpayOrderId!;
    }

    /// <summary>The post-capture state: a Paid order, an issued ticket, a Collected ledger entry, and a
    /// wallet holding those funds.</summary>
    private Guid SeedPaidOrder(Guid orgId, Guid userId)
    {
        var (db, ev, _, order, scope) = NewOrder(orgId, userId, OrderStatus.Paid);
        using (scope)
        {
            var item = db.OrderItems.Local.First(oi => oi.OrderId == order.Id);
            db.Tickets.Add(new Ticket
            {
                OrderItemId = item.Id, EventId = ev.Id, UserId = userId,
                HmacSig = "seeded-signature", State = TicketState.Issued,
            });
            db.LedgerEntries.Add(new LedgerEntry
            {
                OrgId = orgId, EventId = ev.Id, AmountPaise = Amount,
                State = LedgerState.Collected, RefType = "payment", RefId = order.Id,
            });

            var wallet = db.OrganizationWallets.First(w => w.OrgId == orgId);
            wallet.CollectedPaise += Amount;
            wallet.LifetimeEarnedPaise += Amount;
            db.SaveChanges();
        }
        return order.Id;
    }
}
