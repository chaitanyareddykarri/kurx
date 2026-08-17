using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Count-based limits — <c>PerUserLimit</c> and a group's <c>OrderItem.Qty</c> capacity — cannot be
/// expressed as a unique index, so nothing backstops them if the read and the write are not serialised.
/// Both were read BEFORE the order transaction and acted on after it opened, so two simultaneous requests
/// each saw "one slot left" and each took it. They are now evaluated inside the transaction under
/// transaction-scoped advisory locks, the same guard <c>SeatBlockService</c> uses for its reassign limit.
///
/// <para>Also pins <c>currency</c> on the order payload (V3 §9.1): orders have always been stored with the
/// event's settlement currency, but no client was ever sent it, so every surface hardcoded ₹.</para></summary>
public class OrderLimitConcurrencyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public OrderLimitConcurrencyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    [Fact]
    public async Task Concurrent_orders_from_one_buyer_cannot_exceed_PerUserLimit()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9320000001", "Limit Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, quantity: 100, perUserLimit: 1);
        var (buyer, buyerId) = await LoginAsync("9320000002");

        // One buyer, PerUserLimit of 1, two simultaneous orders. Exactly one may be issued a ticket.
        var results = await Task.WhenAll(
            buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }),
            buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var issued = await (from t in db.Tickets.AsNoTracking()
                            join oi in db.OrderItems.AsNoTracking() on t.OrderItemId equals oi.Id
                            where t.UserId == buyerId && oi.TicketTypeId == ttId && t.State != TicketState.Void
                            select t.Id).CountAsync();
        Assert.Equal(1, issued);
    }

    [Fact]
    public async Task Concurrent_joins_cannot_overfill_a_group()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9320000010", "Group Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, quantity: 100, perUserLimit: 50,
            registrationMode: "Group", groupMin: 2, groupMax: 3);
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });

        // A group sized 2: the leader takes one slot, so exactly ONE more member fits.
        var (leader, _) = await LoginAsync("9320000011");
        var order = await Json(await leader.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 2 }));
        var joinCode = order.GetProperty("join_code").GetString();

        var (a, _) = await LoginAsync("9320000012");
        var (b, _) = await LoginAsync("9320000013");
        var joins = await Task.WhenAll(
            a.PostAsJsonAsync("/v1/groups/join", new { joinCode }),
            b.PostAsJsonAsync("/v1/groups/join", new { joinCode }));

        Assert.Single(joins, r => r.StatusCode == HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var groupId = order.GetProperty("group_id").GetGuid();
        Assert.Equal(2, await db.GroupMembers.AsNoTracking().CountAsync(m => m.GroupId == groupId));
    }

    [Fact]
    public async Task Order_payload_carries_the_events_settlement_currency()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9320000020", "Currency Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9320000021");

        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));

        // amount_paise alone is ambiguous; the client cannot pick a symbol without this.
        Assert.Equal("INR", order.GetProperty("currency").GetString());
    }

    /// <summary>D-245: `web/lib/api.ts` parses this list with a strict schema, and it had drifted — it
    /// required an `order_id` the payload never carried, so zod threw and the caller's `.catch(() =&gt; [])`
    /// showed every buyer an empty My Tickets page. Pins the keys that surface actually reads.</summary>
    [Fact]
    public async Task My_orders_list_carries_the_fields_a_ticket_card_renders()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9320000030", "List Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (buyer, _) = await LoginAsync("9320000031");
        await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

        var order = (await Json(await buyer.GetAsync("/v1/orders"))).EnumerateArray().Single();

        Assert.False(string.IsNullOrWhiteSpace(order.GetProperty("event_title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(order.GetProperty("event_slug").GetString()));
        Assert.Equal("Free", order.GetProperty("ticket_type").GetString());
        Assert.Equal("INR", order.GetProperty("currency").GetString());
        Assert.Single(order.GetProperty("tickets").EnumerateArray());
        // The key that never existed. Its absence is the contract, so assert it stays absent.
        Assert.False(order.TryGetProperty("order_id", out _));
    }

    [Fact]
    public async Task My_orders_list_honours_page_and_pageSize()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9320000040", "Paging Org");
        var (buyer, _) = await LoginAsync("9320000041");
        for (var i = 0; i < 3; i++)
        {
            var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
            await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        }

        Assert.Equal(3, (await Json(await buyer.GetAsync("/v1/orders"))).GetArrayLength());
        Assert.Equal(2, (await Json(await buyer.GetAsync("/v1/orders?page=0&pageSize=2"))).GetArrayLength());
        Assert.Equal(1, (await Json(await buyer.GetAsync("/v1/orders?page=1&pageSize=2"))).GetArrayLength());
        // Over-large pageSize clamps to the cap rather than 400-ing or returning everything unbounded.
        Assert.Equal(3, (await Json(await buyer.GetAsync("/v1/orders?page=0&pageSize=99999"))).GetArrayLength());
    }

    // ── helpers (mirroring InventoryTests' HTTP-level setup) ─────────────────
    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>();

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
        return (client, _factory.SeedVerifiedOrgForClient(client, name + " " + Guid.NewGuid().ToString("N")[..6]), userId);
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var cat = await db.EventCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == "limit-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Limit Cat", Slug = "limit-cat" };
            db.EventCategories.Add(cat);
            await db.SaveChangesAsync();
        }
        var type = await db.EventCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == "limit-type");
        if (type is null)
        {
            // D-367 — this suite creates GROUP ticket types, which are only legal where the archetype
            // supports `teams`. An unclassified Type carries no archetype at all, which resolves every
            // capability to Unsupported — a Type existing is not the same as a Type saying something.
            type = new EventCategory
            {
                Level = CategoryLevel.Type, Name = "Limit Type", Slug = "limit-type", ParentId = cat.Id,
                ArchetypeSlug = "competitive",
            };
            db.EventCategories.Add(type);
            await db.SaveChangesAsync();
        }
        return (cat.Id, type.Id);
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync();
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Lim " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTicketTypeAsync(HttpClient owner, Guid orgId, Guid eventId, int quantity,
        int perUserLimit, string registrationMode = "Individual", int? groupMin = null, int? groupMax = null)
    {
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Free", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode,
            groupMin, groupMax, quantity,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId,
        int quantity = 100, int perUserLimit = 50)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var ttId = await CreateTicketTypeAsync(owner, orgId, eventId, quantity, perUserLimit);
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }
}
