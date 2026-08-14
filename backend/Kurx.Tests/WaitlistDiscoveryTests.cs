using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Waitlist (A3) + free/paid discovery filter (A7 partial), D-064. Real HTTP/kurx_test.</summary>
public class WaitlistDiscoveryTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public WaitlistDiscoveryTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "WD Cat", Slug = "wd-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<(Guid OrgId, Guid EventId, Guid TicketTypeId)> PublishedFreeEventAsync(HttpClient owner, int quantity)
    {
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "WD Org " + Guid.NewGuid().ToString("N")[..6], "Company");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "WD Fest " + Guid.NewGuid().ToString("N")[..6],
            description = "x", categoryId = _categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(9), endsAt = DateTime.UtcNow.AddDays(9).AddHours(3),
        }))).GetProperty("id").GetGuid();

        Guid ttId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var tt = new TicketType
            {
                EventId = eventId, Name = "Free", PricePaise = 0, Quantity = quantity,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30),
            };
            db.TicketTypes.Add(tt);
            await db.SaveChangesAsync();
            ttId = tt.Id;
        }
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (orgId, eventId, ttId);
    }

    [Fact]
    public async Task Join_the_waitlist_when_sold_out_then_leave()
    {
        var owner = await LoginAsync("9880000001");
        var (_, eventId, ttId) = await PublishedFreeEventAsync(owner, quantity: 1);

        var buyer = await LoginAsync("9880000002");        // takes the single ticket → sold out
        await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

        var waiter = await LoginAsync("9880000003");
        Assert.Equal(HttpStatusCode.OK,
            (await waiter.PostAsync($"/v1/events/{eventId}/ticket-types/{ttId}/waitlist", null)).StatusCode);

        var mine = await Json(await waiter.GetAsync("/v1/me/waitlist"));
        Assert.Contains(mine.EnumerateArray(), w => w.GetProperty("ticket_type_id").GetGuid() == ttId);

        await waiter.DeleteAsync($"/v1/events/{eventId}/ticket-types/{ttId}/waitlist");
        var after = await Json(await waiter.GetAsync("/v1/me/waitlist"));
        Assert.DoesNotContain(after.EnumerateArray(), w => w.GetProperty("ticket_type_id").GetGuid() == ttId);
    }

    [Fact]
    public async Task Joining_the_waitlist_when_tickets_are_available_is_rejected()
    {
        var owner = await LoginAsync("9880000004");
        var (_, eventId, ttId) = await PublishedFreeEventAsync(owner, quantity: 100);   // not sold out
        var waiter = await LoginAsync("9880000005");

        var res = await waiter.PostAsync($"/v1/events/{eventId}/ticket-types/{ttId}/waitlist", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("tickets_available", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Price_filter_partitions_free_and_paid_events()
    {
        var owner = await LoginAsync("9880000006");
        var (orgId, eventId, _) = await PublishedFreeEventAsync(owner, quantity: 50);
        var pub = _factory.CreateClient();
        await DrainOutboxAsync();   // V3 §15 (Phase 16): project into the discovery index before filtering

        // Initially free (only a free ticket type).
        var free1 = await Json(await pub.GetAsync($"/v1/events?price=free&orgId={orgId}"));
        Assert.Contains(free1.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);
        var paid1 = await Json(await pub.GetAsync($"/v1/events?price=paid&orgId={orgId}"));
        Assert.DoesNotContain(paid1.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);

        // Add a priced ticket type → now "paid".
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.TicketTypes.Add(new TicketType
            {
                EventId = eventId, Name = "VIP", PricePaise = 50000, Quantity = 10,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30),
            });
            await db.SaveChangesAsync();
        }
        await ReindexAsync(eventId);   // direct DB change → project so the index's IsPaid flag is current

        var paid2 = await Json(await pub.GetAsync($"/v1/events?price=paid&orgId={orgId}"));
        Assert.Contains(paid2.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);
        var free2 = await Json(await pub.GetAsync($"/v1/events?price=free&orgId={orgId}"));
        Assert.DoesNotContain(free2.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);
    }

    [Fact]
    public async Task Online_event_requires_a_url_and_is_filterable_by_mode()
    {
        var owner = await LoginAsync("9880000007");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Mode Org " + Guid.NewGuid().ToString("N")[..6], "Company");

        // Online without a URL is refused.
        var bad = await owner.CreateEventAsync(orgId, new
        {
            title = "Bad Online", description = "x", categoryId = _categoryId, venueName = "Virtual", city = "Online",
            startsAt = DateTime.UtcNow.AddDays(4), endsAt = DateTime.UtcNow.AddDays(4).AddHours(2), eventMode = "online",
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("online_url_required", (await Json(bad)).GetProperty("error").GetString());

        // Online with a URL: detail reflects the mode; after publish it's filterable by ?mode=online.
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Online " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = _categoryId,
            venueName = "Virtual", city = "Online",
            startsAt = DateTime.UtcNow.AddDays(4), endsAt = DateTime.UtcNow.AddDays(4).AddHours(2),
            eventMode = "online", onlineUrl = "https://meet.example/x",
        }))).GetProperty("id").GetGuid();

        var detail = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}"));
        Assert.Equal("online", detail.GetProperty("event_mode").GetString());
        Assert.Equal("https://meet.example/x", detail.GetProperty("online_url").GetString());

        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — this test is about discovery
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        await DrainOutboxAsync();   // V3 §15 (Phase 16): project into the discovery index before filtering
        var pub = _factory.CreateClient();
        var res = await Json(await pub.GetAsync($"/v1/events?mode=online&orgId={orgId}"));
        Assert.Contains(res.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);
    }

    // V3 §15 (Phase 16): discovery is served by the outbox-fed index — drain the reindex messages (or project a
    // directly-mutated event) before asserting on discovery results.
    private async Task DrainOutboxAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatchJob>().RunAsync();
    }

    private async Task ReindexAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISearchIndexService>().ProjectAsync(eventId);
    }
}
