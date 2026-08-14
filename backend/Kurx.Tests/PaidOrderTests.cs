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

/// <summary>Paid order + ledger write-path (M10, D-049). A paid ticket type creates a Pending order +
/// gateway order (mock IPaymentGateway); the Razorpay webhook confirms capture → issues the ticket and
/// writes LedgerEntry(Collected) + updates the OrganizationWallet cache (D-028). Payments are gated live
/// (organizer paid-verified + org verified, M8). Real HTTP/kurx_test.</summary>
public class PaidOrderTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public PaidOrderTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Paid Cat", Slug = "paid-cat" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;
    private const long Price = 50000;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
    }

    /// <summary>Full setup: a verified org + paid-verified owner + a Published paid event with an on-sale
    /// priced ticket type. Returns the org id, event id, and ticket-type id.</summary>
    private async Task<(Guid OrgId, Guid EventId, Guid TicketTypeId)> PublishedPaidEventAsync(HttpClient owner, HttpClient reviewer)
    {
        // Owner becomes paid-verified.
        await owner.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Owner" });
        await owner.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Owner" });

        // D-075: the owner files a representation request; approval makes them a verified rep of a Verified org.
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, "Paid Org " + Guid.NewGuid().ToString("N")[..6], "Company");

        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Paid Gala " + Guid.NewGuid().ToString("N")[..6],
            description = "A ticketed gala.", categoryId = _categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();

        Guid ticketTypeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var tt = new TicketType
            {
                EventId = eventId, Name = "General", PricePaise = Price, Quantity = 100,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30),
            };
            db.TicketTypes.Add(tt);
            await db.SaveChangesAsync();
            ticketTypeId = tt.Id;
        }

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (orgId, eventId, ticketTypeId);
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

    [Fact]
    public async Task Paid_order_creates_a_pending_order_with_a_gateway_order()
    {
        var reviewer = await ReviewerAsync("9960000001");
        var (owner, _) = await LoginAsync("9960000002");
        var (_, eventId, ttId) = await PublishedPaidEventAsync(owner, reviewer);

        var (buyer, _) = await LoginAsync("9960000003");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        Assert.Equal("pending", order.GetProperty("status").GetString());
        Assert.Equal(Price, order.GetProperty("amount_paise").GetInt64());
        Assert.False(string.IsNullOrEmpty(order.GetProperty("razorpay_order_id").GetString()));
        Assert.Equal(0, order.GetProperty("tickets").GetArrayLength());   // no ticket until capture
    }

    [Fact]
    public async Task Webhook_capture_issues_the_ticket_and_writes_the_collected_ledger()
    {
        var reviewer = await ReviewerAsync("9960000004");
        var (owner, _) = await LoginAsync("9960000005");
        var (orgId, eventId, ttId) = await PublishedPaidEventAsync(owner, reviewer);

        var (buyer, buyerId) = await LoginAsync("9960000006");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var rzpOrderId = order.GetProperty("razorpay_order_id").GetString()!;

        var capture = await CaptureAsync(rzpOrderId, "pay_test_1");
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var orderId = order.GetProperty("id").GetGuid();

        Assert.Equal(OrderStatus.Paid, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status);
        Assert.True(await db.Tickets.AsNoTracking().AnyAsync(t => t.UserId == buyerId && t.EventId == eventId));

        var wallet = await db.OrganizationWallets.AsNoTracking().SingleAsync(w => w.OrgId == orgId);
        Assert.Equal(Price, wallet.CollectedPaise);
        Assert.Equal(Price, wallet.LifetimeEarnedPaise);

        var ledger = await db.LedgerEntries.AsNoTracking()
            .SingleOrDefaultAsync(l => l.EventId == eventId && l.State == LedgerState.Collected);
        Assert.NotNull(ledger);
        Assert.Equal(Price, ledger!.AmountPaise);
    }

    [Fact]
    public async Task Webhook_capture_is_idempotent()
    {
        var reviewer = await ReviewerAsync("9960000007");
        var (owner, _) = await LoginAsync("9960000008");
        var (orgId, eventId, ttId) = await PublishedPaidEventAsync(owner, reviewer);

        var (buyer, _) = await LoginAsync("9960000009");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var rzpOrderId = order.GetProperty("razorpay_order_id").GetString()!;

        await CaptureAsync(rzpOrderId, "pay_test_2");
        await CaptureAsync(rzpOrderId, "pay_test_2");   // re-delivered webhook

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var orderId = order.GetProperty("id").GetGuid();
        // Exactly one ticket and one collected ledger entry despite two webhook deliveries.
        Assert.Equal(1, await db.Tickets.CountAsync(t => t.EventId == eventId));
        Assert.Equal(1, await db.LedgerEntries.CountAsync(l => l.EventId == eventId && l.State == LedgerState.Collected));
        Assert.Equal(Price, (await db.OrganizationWallets.AsNoTracking().SingleAsync(w => w.OrgId == orgId)).CollectedPaise);
    }

    [Fact]
    public async Task Suspending_the_org_blocks_new_paid_orders_immediately()
    {
        var reviewer = await ReviewerAsync("9960000010");
        var (owner, _) = await LoginAsync("9960000011");
        var (orgId, eventId, ttId) = await PublishedPaidEventAsync(owner, reviewer);

        // Org suspended after publish → the live payment gate must block new orders.
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/suspend", new { reason = "fraud" });

        var (buyer, _) = await LoginAsync("9960000012");
        var res = await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("payments_not_enabled", (await Json(res)).GetProperty("error").GetString());
    }
}
