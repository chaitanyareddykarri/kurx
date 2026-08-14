using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>The organizer events-management table reads GET /v1/orgs/{orgId}/events, which is enriched
/// with per-event stats (tickets sold / checked-in / revenue / category / paid). These assert the
/// aggregation, pagination and authorization of that org-scoped projection.</summary>
public class OrgEventListTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static Guid _categoryId;

    public OrgEventListTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Sports", Slug = "sports" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> UserAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", verify.GetProperty("access_token").GetString());
        return (client, verify.GetProperty("user_id").GetGuid());
    }

    private async Task<(HttpClient Client, Guid UserId, Guid OrgId)> OwnerAsync(string phone, string orgName)
    {
        var (client, uid) = await UserAsync(phone);
        var orgId = _factory.SeedVerifiedOrg(uid, orgName, OrgRole.Owner, status: OrgVerificationStatus.Verified);
        return (client, uid, orgId);
    }

    private static Event NewEvent(Guid orgId, Guid userId, string title, Guid categoryId) => new()
    {
        RepresentingOrgId = orgId,
        CreatedBy = userId,
        Title = title,
        Slug = "e-" + Guid.NewGuid().ToString("N"),
        // 8 hex chars (not the usual 6) — this helper seeds up to 1000 rows in one loop (see
        // Scales_to_a_thousand_events_without_loading_all_rows) and 6 chars' ~16.7M keyspace gave a real
        // birthday-collision chance against the unique index; negligible at 8.
        ShortCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
        CategoryId = categoryId,
        StartsAt = DateTime.UtcNow.AddDays(3),
        EndsAt = DateTime.UtcNow.AddDays(3).AddHours(3),
        Status = EventStatus.Published,
        Visibility = EventVisibility.Listed
    };

    [Fact]
    public async Task Aggregates_tickets_checkins_revenue_and_category()
    {
        var (client, uid, orgId) = await OwnerAsync("9720000001", "Stats Org");
        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = NewEvent(orgId, uid, "Championship", _categoryId);
            db.Events.Add(ev);
            var tt = new TicketType { EventId = ev.Id, Name = "GA", PricePaise = 50000, Quantity = 100, SaleStarts = DateTime.UtcNow, SaleEnds = DateTime.UtcNow.AddDays(2) };
            db.TicketTypes.Add(tt);

            // Revenue: two Paid orders (75000) + one Pending (must be excluded). V3 §9.5 (Phase 17): revenue
            // is read from ValueAllocationRecord, not Orders.AmountPaise directly — seed the VAR rows the
            // real purchase flow (EventRegistrationService) would write, one per order item, matching its
            // own AllocatedPaise = UnitPricePaise * Qty.
            var paidOrder1 = new Order { EventId = ev.Id, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = 50000 };
            var paidOrder2 = new Order { EventId = ev.Id, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = 25000 };
            db.Orders.Add(paidOrder1);
            db.Orders.Add(paidOrder2);
            db.Orders.Add(new Order { EventId = ev.Id, TicketTypeId = tt.Id, Status = OrderStatus.Pending, AmountPaise = 99999 });
            var paidItem1 = new OrderItem { OrderId = paidOrder1.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = 50000 };
            var paidItem2 = new OrderItem { OrderId = paidOrder2.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = 25000 };
            db.OrderItems.Add(paidItem1);
            db.OrderItems.Add(paidItem2);
            db.ValueAllocationRecords.Add(new ValueAllocationRecord { OrderId = paidOrder1.Id, OrderItemId = paidItem1.Id, EventId = ev.Id, AllocatedPaise = 50000 });
            db.ValueAllocationRecords.Add(new ValueAllocationRecord { OrderId = paidOrder2.Id, OrderItemId = paidItem2.Id, EventId = ev.Id, AllocatedPaise = 25000 });

            var order = new Order { EventId = ev.Id, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = 0 };
            db.Orders.Add(order);
            var item = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 3, UnitPricePaise = 50000 };
            db.OrderItems.Add(item);
            // 3 tickets issued, 2 checked in.
            db.Tickets.Add(new Ticket { OrderItemId = item.Id, EventId = ev.Id, HmacSig = "x", CheckedInAt = DateTime.UtcNow });
            db.Tickets.Add(new Ticket { OrderItemId = item.Id, EventId = ev.Id, HmacSig = "x", CheckedInAt = DateTime.UtcNow });
            db.Tickets.Add(new Ticket { OrderItemId = item.Id, EventId = ev.Id, HmacSig = "x" });
            db.SaveChanges();
            eventId = ev.Id;
        }

        var body = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events"));
        var row = body.GetProperty("items").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == eventId);

        Assert.Equal(3, row.GetProperty("tickets_sold").GetInt32());
        Assert.Equal(2, row.GetProperty("checked_in").GetInt32());
        Assert.Equal(75000L, row.GetProperty("revenue_paise").GetInt64());
        Assert.True(row.GetProperty("is_paid").GetBoolean());
        Assert.Equal("Sports", row.GetProperty("category_name").GetString());
    }

    [Fact]
    public async Task Free_event_reports_not_paid_and_zero_stats()
    {
        var (client, uid, orgId) = await OwnerAsync("9720000002", "Free Org");
        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = NewEvent(orgId, uid, "Free Meetup", _categoryId);
            db.Events.Add(ev);
            db.SaveChanges();
            eventId = ev.Id;
        }

        var body = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events"));
        var row = body.GetProperty("items").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == eventId);

        Assert.False(row.GetProperty("is_paid").GetBoolean());
        Assert.Equal(0, row.GetProperty("tickets_sold").GetInt32());
        Assert.Equal(0, row.GetProperty("checked_in").GetInt32());
        Assert.Equal(0L, row.GetProperty("revenue_paise").GetInt64());
    }

    [Fact]
    public async Task Paginates_with_total()
    {
        var (client, uid, orgId) = await OwnerAsync("9720000003", "Paginate Org");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 25; i++) db.Events.Add(NewEvent(orgId, uid, $"E{i}", _categoryId));
            db.SaveChanges();
        }

        var p1 = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events?page=1&pageSize=10"));
        Assert.Equal(25, p1.GetProperty("total").GetInt32());
        Assert.Equal(10, p1.GetProperty("items").GetArrayLength());

        var p3 = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events?page=3&pageSize=10"));
        Assert.Equal(5, p3.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Non_member_is_forbidden()
    {
        var (_, ownerId) = await UserAsync("9720000004");
        var orgId = _factory.SeedVerifiedOrg(ownerId, "Private Org", OrgRole.Owner, status: OrgVerificationStatus.Verified);

        var (outsider, _) = await UserAsync("9720000005");
        var res = await outsider.GetAsync($"/v1/orgs/{orgId}/events");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_unauthorized()
    {
        var (_, ownerId) = await UserAsync("9720000006");
        var orgId = _factory.SeedVerifiedOrg(ownerId, "Auth Org", OrgRole.Owner, status: OrgVerificationStatus.Verified);

        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/v1/orgs/{orgId}/events");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Scales_to_a_thousand_events_without_loading_all_rows()
    {
        var (client, uid, orgId) = await OwnerAsync("9720000007", "Scale Org");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var batch = new List<Event>();
            for (var i = 0; i < 1000; i++) batch.Add(NewEvent(orgId, uid, $"S{i}", _categoryId));
            db.Events.AddRange(batch);
            db.SaveChanges();
        }

        // pageSize is clamped server-side (≤ 50), so a 1000-event org still materialises only one page of
        // stats — per-request work stays bounded (no N+1, no full-table scan for the aggregates).
        var body = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events?page=1&pageSize=20"));
        Assert.Equal(1000, body.GetProperty("total").GetInt32());
        Assert.Equal(20, body.GetProperty("items").GetArrayLength());

        // An over-large pageSize is clamped to 50, never 1000.
        var big = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events?page=1&pageSize=1000"));
        Assert.Equal(50, big.GetProperty("items").GetArrayLength());
    }
}
