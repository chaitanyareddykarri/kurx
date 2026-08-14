using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §9.1 (Phase 3) — the money currency dimension. Additive `currency` columns default to INR;
/// an event binds its settlement currency from its Org and exposes it at the edge; clones inherit it.
/// Existing paise amounts (D-004) are untouched.</summary>
public class MoneyCurrencyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public MoneyCurrencyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public void Money_value_type_is_amount_minor_plus_currency()
    {
        var m = Money.Inr(500);
        Assert.Equal(500, m.AmountMinor);
        Assert.Equal("INR", m.Currency);
        Assert.Equal("INR", Money.DefaultCurrency);
        Assert.Equal(new Money(250, "USD"), new Money(250, "USD"));   // value equality
    }

    [Fact]
    public async Task Money_bearing_rows_default_to_inr()
    {
        var (_, orgId) = await LoginOrgAsync("9700000921", "Currency Default Org");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.Equal("INR", await db.Organizations.Where(o => o.Id == orgId).Select(o => o.SettlementCurrency).SingleAsync());
        Assert.Equal("INR", await db.OrganizationWallets.Where(w => w.OrgId == orgId).Select(w => w.Currency).SingleAsync());
        Assert.Equal("INR", await db.PayoutSchedules.Where(p => p.OrgId == orgId).Select(p => p.Currency).SingleAsync());
    }

    [Fact]
    public async Task Event_binds_settlement_currency_from_org_and_clone_inherits()
    {
        var (client, orgId) = await LoginOrgAsync("9700000922", "USD Org");
        // Make the org settle in USD; the event must bind that (§3.5 — currency is a bound field).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var org = await db.Organizations.SingleAsync(o => o.Id == orgId);
            org.SettlementCurrency = "USD";
            await db.SaveChangesAsync();
        }

        var (catId, typeId) = await TaxonAsync("hackathon");
        var body = new
        {
            title = "Priced", description = "x", categoryId = catId, typeId, venueName = "V", city = "C",
            startsAt = DateTime.UtcNow.AddDays(15), endsAt = DateTime.UtcNow.AddDays(16),
        };
        var ev = await Json(await client.CreateEventAsync(orgId, body));
        var eventId = ev.GetProperty("id").GetGuid();

        Assert.Equal("USD", ev.GetProperty("settlement_currency").GetString());   // bound + displayed at the edge
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.Equal("USD", await db.Events.Where(e => e.Id == eventId).Select(e => e.SettlementCurrency).SingleAsync());
        }

        // A clone (same org) inherits the settlement currency.
        var clone = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/clone", new { title = "Clone" }));
        Assert.Equal("USD", clone.GetProperty("settlement_currency").GetString());
    }

    [Fact]
    public async Task Event_detail_exposes_settlement_currency()
    {
        var (client, orgId) = await LoginOrgAsync("9700000923", "INR Event Org");
        var (catId, typeId) = await TaxonAsync("hackathon");
        var body = new
        {
            title = "Inr Event", description = "x", categoryId = catId, typeId, venueName = "V", city = "C",
            startsAt = DateTime.UtcNow.AddDays(15), endsAt = DateTime.UtcNow.AddDays(16),
        };
        var created = await Json(await client.CreateEventAsync(orgId, body));
        var got = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{created.GetProperty("id").GetGuid()}"));
        Assert.Equal("INR", got.GetProperty("settlement_currency").GetString());
    }

    private async Task<(HttpClient Client, Guid OrgId)> LoginOrgAsync(string phone, string name)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await Json(verify)).GetProperty("access_token").GetString());
        return (client, _factory.SeedVerifiedOrgForClient(client, name));
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug)
            .Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.ParentId!.Value, t.Id);
    }

    // ── Priority-1 fix: currency coherence across the whole money chain ──

    [Fact]
    public void IsValidCurrency_accepts_iso4217_shape_and_rejects_malformed()
    {
        Assert.True(Money.IsValidCurrency("INR"));
        Assert.True(Money.IsValidCurrency("USD"));
        Assert.False(Money.IsValidCurrency("inr"));   // lowercase
        Assert.False(Money.IsValidCurrency("US"));    // too short
        Assert.False(Money.IsValidCurrency("USDT"));  // too long
        Assert.False(Money.IsValidCurrency("12A"));   // non-alpha
        Assert.False(Money.IsValidCurrency(""));
        Assert.False(Money.IsValidCurrency(null));
    }

    [Fact]
    public async Task Invalid_org_currency_falls_back_to_inr_on_bind()
    {
        var (client, orgId) = await LoginOrgAsync("9700000936", "Bad Currency Org");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var org = await db.Organizations.SingleAsync(o => o.Id == orgId);
            org.SettlementCurrency = "usd";   // malformed (lowercase) — must never propagate onto an event
            await db.SaveChangesAsync();
        }
        var (catId, typeId) = await TaxonAsync("hackathon");
        var ev = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Fallback " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(15), endsAt = DateTime.UtcNow.AddDays(16),
        }));
        Assert.Equal("INR", ev.GetProperty("settlement_currency").GetString());   // rejected → INR default
    }

    [Fact]
    public async Task Free_order_and_ticket_carry_the_default_inr_currency()
    {
        var (client, orgId) = await LoginOrgAsync("9700000934", "INR Free Org");
        var (catId, typeId) = await TaxonAsync("hackathon");
        var eventId = (await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Free INR " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        }))).GetProperty("id").GetGuid();
        var ttId = await CreateTicketTypeAsync(client, orgId, eventId, pricePaise: 0);
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });

        var (buyer, _) = await LoginAsync("9700000935");
        var orderId = (await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId })))
            .GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal("INR", await db.TicketTypes.Where(t => t.Id == ttId).Select(t => t.Currency).SingleAsync());
        Assert.Equal("INR", await db.Orders.Where(o => o.Id == orderId).Select(o => o.Currency).SingleAsync());
        Assert.Equal("INR", await db.OrderItems.Where(i => i.OrderId == orderId).Select(i => i.Currency).FirstAsync());
    }

    [Fact]
    public async Task End_to_end_currency_is_coherent_across_the_money_chain()
    {
        var reviewer = await ReviewerAsync("9700000931");
        var (owner, _) = await LoginAsync("9700000932");

        // Owner becomes paid-verified (identity + bank).
        await owner.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Owner" });
        await owner.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Owner" });

        var orgId = _factory.SeedVerifiedOrgForClient(owner, "USD Chain " + Guid.NewGuid().ToString("N")[..6], "Company");

        // Settle the org in USD BEFORE the event exists, so the event binds USD.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var org = await db.Organizations.SingleAsync(o => o.Id == orgId);
            org.SettlementCurrency = "USD";
            await db.SaveChangesAsync();
        }

        var (catId, typeId) = await TaxonAsync("hackathon");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "USD Chain " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "Hall", city = "Vizag", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();

        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, pricePaise: 50000);   // via TicketTypeService → USD
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });

        var (buyer, _) = await LoginAsync("9700000933");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var orderId = order.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_usd_1")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRefundService>().RefundOrderAsync(orderId, "test", null);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Organization → Event → TicketType → Order → OrderItem → Refund → Ledger all carry USD.
            Assert.Equal("USD", await db.Organizations.Where(o => o.Id == orgId).Select(o => o.SettlementCurrency).SingleAsync());
            Assert.Equal("USD", await db.Events.Where(e => e.Id == eventId).Select(e => e.SettlementCurrency).SingleAsync());
            Assert.Equal("USD", await db.TicketTypes.Where(t => t.Id == ttId).Select(t => t.Currency).SingleAsync());
            Assert.Equal("USD", await db.Orders.Where(o => o.Id == orderId).Select(o => o.Currency).SingleAsync());
            Assert.Equal("USD", await db.OrderItems.Where(i => i.OrderId == orderId).Select(i => i.Currency).FirstAsync());
            Assert.Equal("USD", await db.Refunds.Where(r => r.OrderId == orderId).Select(r => r.Currency).FirstAsync());
            var ledgerCurrencies = await db.LedgerEntries.Where(l => l.EventId == eventId)
                .Select(l => l.Currency).Distinct().ToListAsync();
            Assert.Equal(new[] { "USD" }, ledgerCurrencies);   // both Collected and the Refunded reversal are USD
        }
    }

    private async Task<Guid> CreateTicketTypeAsync(HttpClient owner, Guid orgId, Guid eventId, long pricePaise)
    {
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "General", pricePaise, pricingUnit = "PerTicket", registrationMode = "Individual",
            groupMin = (int?)null, groupMax = (int?)null, quantity = 100,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 5, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
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

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
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
}
