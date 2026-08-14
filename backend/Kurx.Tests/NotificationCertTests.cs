using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>In-app notifications + device registration (A6) and the certificate roster (A8), D-064. Real HTTP/kurx_test.</summary>
public class NotificationCertTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public NotificationCertTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "NC Cat", Slug = "nc-cat" };
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

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (client, t.GetProperty("user_id").GetGuid());
    }

    private async Task<(Guid OrgId, Guid EventId, Guid TicketTypeId)> PublishedFreeEventAsync(HttpClient owner)
    {
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "NC Org " + Guid.NewGuid().ToString("N")[..6], "Company");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "NC Fest " + Guid.NewGuid().ToString("N")[..6],
            description = "x", categoryId = _categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(8), endsAt = DateTime.UtcNow.AddDays(8).AddHours(3),
        }))).GetProperty("id").GetGuid();

        Guid ttId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var tt = new TicketType
            {
                EventId = eventId, Name = "Free", PricePaise = 0, Quantity = 100,
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
    public async Task Register_device_and_read_the_notification_feed()
    {
        var (user, uid) = await LoginAsync("9890000001");
        Assert.Equal(HttpStatusCode.OK,
            (await user.PostAsJsonAsync("/v1/me/devices", new { fcmToken = "tok-" + Guid.NewGuid().ToString("N"), platform = "android" })).StatusCode);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<INotificationService>()
                .NotifyAsync(uid, "reminder", "Event soon", "Starts tomorrow");

        var list = await Json(await user.GetAsync("/v1/me/notifications"));
        Assert.Equal(1, list.GetProperty("unread_count").GetInt32());
        var notifId = list.GetProperty("items")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync($"/v1/me/notifications/{notifId}/read", null)).StatusCode);
        var after = await Json(await user.GetAsync("/v1/me/notifications"));
        Assert.Equal(0, after.GetProperty("unread_count").GetInt32());
    }

    [Fact]
    public async Task Owner_generates_and_lists_the_cert_roster_but_a_stranger_cannot()
    {
        var (owner, _) = await LoginAsync("9890000002");
        var (_, eventId, ttId) = await PublishedFreeEventAsync(owner);
        var (attendee, _) = await LoginAsync("9890000003");
        await attendee.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

        var gen = await Json(await owner.PostAsync($"/v1/events/{eventId}/certificates/generate", null));
        Assert.True(gen.GetProperty("generated").GetInt32() >= 1);

        var roster = await Json(await owner.GetAsync($"/v1/events/{eventId}/certificates"));
        Assert.True(roster.GetArrayLength() >= 1);

        var (stranger, _) = await LoginAsync("9890000004");
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/v1/events/{eventId}/certificates")).StatusCode);
    }
}
