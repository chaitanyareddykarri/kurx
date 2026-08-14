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

/// <summary>D-199: the HTTP surface over the pre-existing <c>IRefundService</c> (D-103/D-198). Real HTTP /
/// real <c>kurx_test</c>. <c>RefundOrderAsync</c>'s own money-correctness invariants are already covered by
/// <see cref="RefundLedgerTests"/> at the service layer — these tests cover what's new: authorization
/// (Owner/Finance/FinanceOps/SuperAdmin allowed, Representative/Support/unrelated-attendee blocked), the
/// read endpoints, and idempotency observed through the HTTP layer.</summary>
public class RefundEndpointsTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public RefundEndpointsTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Refund Ep Cat", Slug = "refund-ep-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private string NextPhone() => $"9195{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<HttpClient> PlatformStaffAsync(PlatformRole role)
    {
        var (client, userId) = await LoginAsync(NextPhone());
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>().GrantAsync(userId, role, grantedBy: null);
        return client;
    }

    // D-203 (D-201 review finding): SecondMemberUserId lives on the Fixture record itself now, not a
    // side-channel mutable instance field — removes the temporal coupling where a caller had to read
    // _secondMemberUserId immediately after each SeedPaidOrderAsync call, before a later call could
    // overwrite it.
    private record Fixture(Guid OrderId, Guid EventId, Guid OrgId, Guid BuyerUserId, Guid TicketId, Guid? SecondMemberUserId = null);

    /// <summary>Seeds the post-capture state ConfirmPaymentAsync would have produced — a Paid order, issued
    /// ticket, Collected ledger entry, funded wallet — with a distinct buyer and org owner, plus an optional
    /// second org member at <paramref name="secondMemberRole"/> (for the Finance/Representative authorization cases).</summary>
    private async Task<Fixture> SeedPaidOrderAsync(long amountPaise, OrgRole? secondMemberRole = null)
    {
        var (_, ownerId) = await LoginAsync(NextPhone());
        var (_, buyerId) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrg(ownerId, "Refund Ep Org " + Guid.NewGuid().ToString("N")[..6]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Guid? secondMemberUserId = null;
        if (secondMemberRole is { } role)
        {
            var (_, secondUserId) = await LoginAsync(NextPhone());
            db.Memberships.Add(new Membership { OrgId = orgId, UserId = secondUserId, Role = role, IsVerified = true });
            secondMemberUserId = secondUserId;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = ownerId,
            Title = "Refund Ep Fest " + suffix, Slug = "refund-ep-fest-" + suffix, ShortCode = suffix[..6].ToUpperInvariant(),
            Description = "Seeded for refund endpoint tests.", CategoryId = _categoryId,
            VenueName = "Main Hall", City = "Vizag",
            StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(10).AddHours(3),
            Status = EventStatus.Published,
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
            UserId = buyerId, EventId = ev.Id, TicketTypeId = ticketType.Id,
            Status = OrderStatus.Paid, AmountPaise = amountPaise, RazorpayOrderId = "rzp_" + suffix,
        };
        db.Orders.Add(order);

        var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = 1, UnitPricePaise = amountPaise };
        db.OrderItems.Add(item);

        var ticket = new Ticket
        {
            OrderItemId = item.Id, EventId = ev.Id, UserId = buyerId, HmacSig = "seeded-signature", State = TicketState.Issued,
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

        return new Fixture(order.Id, ev.Id, orgId, buyerId, ticket.Id, secondMemberUserId);
    }

    private async Task<HttpClient> ClientForAsync(Guid userId)
    {
        // Re-derive a session for an already-seeded user id via the users table's phone (test-only shortcut:
        // log in fresh and reuse the DB row already created for that user by SeedPaidOrderAsync).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var phone = await db.Users.Where(u => u.Id == userId).Select(u => u.Phone).FirstAsync();
        var (client, _) = await LoginAsync(phone);
        return client;
    }

    [Fact]
    public async Task Org_Owner_can_refund_a_paid_order_wallet_ledger_and_ticket_all_update()
    {
        var f = await SeedPaidOrderAsync(50_000);
        var owner = await OwnerClientForOrgAsync(f.OrgId);

        var res = await owner.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "customer request" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await Json(res);
        Assert.Equal("refunded", body.GetProperty("outcome").GetString());
        Assert.Equal(50_000, body.GetProperty("amount_paise").GetInt64());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == f.OrderId);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        var ticket = await db.Tickets.AsNoTracking().FirstAsync(t => t.Id == f.TicketId);
        Assert.Equal(TicketState.Void, ticket.State);
        var wallet = await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == f.OrgId);
        Assert.Equal(0, wallet.CollectedPaise);
        var reversal = await db.LedgerEntries.AsNoTracking().FirstAsync(l => l.OrgId == f.OrgId && l.State == LedgerState.Refunded);
        Assert.Equal(-50_000, reversal.AmountPaise);
        var audit = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.Action == "order.refunded" && a.EntityId == f.OrderId);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task Duplicate_refund_is_idempotent_wallet_is_not_double_debited()
    {
        var f = await SeedPaidOrderAsync(40_000);
        var owner = await OwnerClientForOrgAsync(f.OrgId);

        var first = await owner.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "test" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await owner.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "test again" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("already_refunded", (await Json(second)).GetProperty("outcome").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var wallet = await db.OrganizationWallets.AsNoTracking().FirstAsync(w => w.OrgId == f.OrgId);
        Assert.Equal(0, wallet.CollectedPaise);   // debited exactly once, not twice
        Assert.Equal(1, await db.Refunds.CountAsync(r => r.OrderId == f.OrderId));
    }

    [Fact]
    public async Task Org_Finance_role_can_refund_but_Representative_cannot()
    {
        var f = await SeedPaidOrderAsync(30_000, secondMemberRole: OrgRole.Representative);
        var representative = await ClientForAsync(f.SecondMemberUserId!.Value);
        var repRes = await representative.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "test" });
        Assert.Equal(HttpStatusCode.Forbidden, repRes.StatusCode);

        var f2 = await SeedPaidOrderAsync(30_000, secondMemberRole: OrgRole.Finance);
        var finance = await ClientForAsync(f2.SecondMemberUserId!.Value);
        var financeRes = await finance.PostAsJsonAsync($"/v1/orders/{f2.OrderId}/refund", new { reason = "test" });
        Assert.Equal(HttpStatusCode.OK, financeRes.StatusCode);
    }

    [Fact]
    public async Task Attendee_cannot_initiate_their_own_refund_but_can_view_it_after_staff_processes_it()
    {
        var f = await SeedPaidOrderAsync(20_000);
        var attendee = await ClientForAsync(f.BuyerUserId);

        var selfRefund = await attendee.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "I want a refund" });
        Assert.Equal(HttpStatusCode.Forbidden, selfRefund.StatusCode);

        var beforeGet = await attendee.GetAsync($"/v1/orders/{f.OrderId}/refund");
        Assert.Equal(HttpStatusCode.NotFound, beforeGet.StatusCode);   // no refund exists yet

        var owner = await OwnerClientForOrgAsync(f.OrgId);
        await owner.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "staff-processed" });

        var afterGet = await attendee.GetAsync($"/v1/orders/{f.OrderId}/refund");
        Assert.Equal(HttpStatusCode.OK, afterGet.StatusCode);
        Assert.Equal("initiated", (await Json(afterGet)).GetProperty("status").GetString());

        var myRefunds = await Json(await attendee.GetAsync("/v1/refunds"));
        Assert.Contains(myRefunds.EnumerateArray(), r => r.GetProperty("order_id").GetGuid() == f.OrderId);
    }

    [Fact]
    public async Task Support_is_blocked_from_refunding_or_viewing_an_unrelated_order()
    {
        var f = await SeedPaidOrderAsync(15_000);
        var support = await PlatformStaffAsync(PlatformRole.Support);

        var postRes = await support.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "test" });
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);

        // D-018: an order Support can't see must look identical to one that doesn't exist — 404, not 403.
        var getRes = await support.GetAsync($"/v1/orders/{f.OrderId}/refund");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        var adminList = await support.GetAsync("/v1/admin/refunds");
        Assert.Equal(HttpStatusCode.Forbidden, adminList.StatusCode);
    }

    [Fact]
    public async Task FinanceOps_and_SuperAdmin_can_refund_any_orgs_order()
    {
        var f1 = await SeedPaidOrderAsync(10_000);
        var financeOps = await PlatformStaffAsync(PlatformRole.FinanceOps);
        var res1 = await financeOps.PostAsJsonAsync($"/v1/orders/{f1.OrderId}/refund", new { reason = "finance ops override" });
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        var f2 = await SeedPaidOrderAsync(10_000);
        var superAdmin = await PlatformStaffAsync(PlatformRole.SuperAdmin);
        var res2 = await superAdmin.PostAsJsonAsync($"/v1/orders/{f2.OrderId}/refund", new { reason = "super admin override" });
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
    }

    [Fact]
    public async Task Admin_refund_list_is_FinanceOps_gated_and_unauthenticated_is_401()
    {
        var f = await SeedPaidOrderAsync(60_000);
        var owner = await OwnerClientForOrgAsync(f.OrgId);
        await owner.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "test" });

        var financeOps = await PlatformStaffAsync(PlatformRole.FinanceOps);
        var listRes = await financeOps.GetAsync("/v1/admin/refunds?status=initiated&limit=5");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var body = await Json(listRes);
        Assert.Contains(body.GetProperty("items").EnumerateArray(), r => r.GetProperty("order_id").GetGuid() == f.OrderId);

        var anon = _factory.CreateClient();
        var anonRes = await anon.GetAsync("/v1/admin/refunds");
        Assert.Equal(HttpStatusCode.Unauthorized, anonRes.StatusCode);
    }

    [Fact]
    public async Task Refund_for_a_nonexistent_order_is_404()
    {
        var financeOps = await PlatformStaffAsync(PlatformRole.FinanceOps);
        var res = await financeOps.PostAsJsonAsync($"/v1/orders/{Guid.NewGuid()}/refund", new { reason = "test" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // D-203 (D-201 review finding): the GET counterpart of the test above — a non-staff caller (a plain
    // normal-user, unrelated to the order in every way) hitting a truly nonexistent order id. GetForOrderAsync's
    // `order is null` check runs first, unconditionally, before any authorization branch — so this is
    // "not_found" for every caller, staff or not. Matches the current implementation; no logic changed.
    [Fact]
    public async Task Get_refund_for_a_nonexistent_order_is_404_for_a_non_staff_caller()
    {
        var (normalUser, _) = await LoginAsync(NextPhone());
        var res = await normalUser.GetAsync($"/v1/orders/{Guid.NewGuid()}/refund");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // D-202 (D-201 review finding): RequestRefundBodyValidator — matches ModerationActionBodyValidator's
    // NotEmpty().MaximumLength(500) shape. Runs before authorization/service logic, so a FinanceOps caller
    // (who would otherwise be allowed through) is enough to isolate the validator itself.
    [Fact]
    public async Task Empty_reason_is_rejected_by_the_validator()
    {
        var f = await SeedPaidOrderAsync(10_000);
        var financeOps = await PlatformStaffAsync(PlatformRole.FinanceOps);

        var res = await financeOps.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await Json(res)).GetProperty("error").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == f.OrderId);
        Assert.Equal(OrderStatus.Paid, order.Status);   // rejected before the service ever ran — no refund happened
    }

    [Fact]
    public async Task Reason_over_500_characters_is_rejected_by_the_validator()
    {
        var f = await SeedPaidOrderAsync(10_000);
        var financeOps = await PlatformStaffAsync(PlatformRole.FinanceOps);

        var res = await financeOps.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = new string('x', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Reason_at_exactly_500_characters_is_accepted()
    {
        var f = await SeedPaidOrderAsync(10_000);
        var financeOps = await PlatformStaffAsync(PlatformRole.FinanceOps);

        var res = await financeOps.PostAsJsonAsync($"/v1/orders/{f.OrderId}/refund", new { reason = new string('x', 500) });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // D-202 (D-201 review finding): the docs/api/README.md correction — a not-yet-Paid order is
    // invalid_order_state (400), never not_found. Also confirms the RefundOrderAsync branch (untouched
    // since D-199) is reachable and correctly mapped by RefundEndpoints.Fail()'s default case.
    [Fact]
    public async Task Refunding_an_order_that_was_never_Paid_is_400_invalid_order_state_not_404()
    {
        var (_, buyerId) = await LoginAsync(NextPhone());
        var (_, ownerId) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrg(ownerId, "Refund Ep Pending Org " + Guid.NewGuid().ToString("N")[..6]);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = ownerId,
                Title = "Refund Ep Pending Fest " + suffix, Slug = "refund-ep-pending-fest-" + suffix,
                ShortCode = suffix[..6].ToUpperInvariant(), Description = "Seeded for refund endpoint tests.",
                CategoryId = _categoryId, VenueName = "Main Hall", City = "Vizag",
                StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(10).AddHours(3),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            var ticketType = new TicketType
            {
                EventId = ev.Id, Name = "General", PricePaise = 10_000, Quantity = 100, Sold = 1,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(9),
            };
            db.TicketTypes.Add(ticketType);
            db.Orders.Add(new Order
            {
                Id = _pendingOrderId, UserId = buyerId, EventId = ev.Id, TicketTypeId = ticketType.Id,
                Status = OrderStatus.Pending, AmountPaise = 10_000, RazorpayOrderId = "rzp_" + suffix,
            });
            db.SaveChanges();
        }

        var owner = await ClientForAsync(ownerId);
        var res = await owner.PostAsJsonAsync($"/v1/orders/{_pendingOrderId}/refund", new { reason = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_order_state", (await Json(res)).GetProperty("error").GetString());
    }

    private readonly Guid _pendingOrderId = Guid.NewGuid();

    private async Task<HttpClient> OwnerClientForOrgAsync(Guid orgId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ownerId = await db.Memberships.Where(m => m.OrgId == orgId && m.Role == OrgRole.Owner).Select(m => m.UserId).FirstAsync();
        return await ClientForAsync(ownerId);
    }
}
