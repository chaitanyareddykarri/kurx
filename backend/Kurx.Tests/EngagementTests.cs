using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Attendee engagement (D-064): saved events, org follows, and event reviews. Real HTTP/kurx_test.</summary>
public class EngagementTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public EngagementTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Engage Cat", Slug = "engage-cat" };
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

    // A published, public, FREE event (free events publish directly, no review) + its free ticket type.
    private async Task<(Guid OrgId, Guid EventId, Guid TicketTypeId)> PublishedFreeEventAsync(HttpClient owner)
    {
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Engage Org " + Guid.NewGuid().ToString("N")[..6], "Company");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Free Fest " + Guid.NewGuid().ToString("N")[..6],
            description = "A free event.", categoryId = _categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(10), endsAt = DateTime.UtcNow.AddDays(10).AddHours(3),
        }))).GetProperty("id").GetGuid();

        Guid ticketTypeId;
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
            ticketTypeId = tt.Id;
        }

        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (orgId, eventId, ticketTypeId);
    }

    [Fact]
    public async Task Save_then_list_then_unsave_an_event()
    {
        var owner = await LoginAsync("9910000001");
        var (_, eventId, _) = await PublishedFreeEventAsync(owner);
        var user = await LoginAsync("9910000002");

        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync($"/v1/events/{eventId}/save", null)).StatusCode);

        var saved = await Json(await user.GetAsync("/v1/me/saved"));
        Assert.Contains(saved.EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);

        await user.DeleteAsync($"/v1/events/{eventId}/save");
        var after = await Json(await user.GetAsync("/v1/me/saved"));
        Assert.DoesNotContain(after.EnumerateArray(), e => e.GetProperty("id").GetGuid() == eventId);
    }

    [Fact]
    public async Task Saving_a_nonexistent_event_is_404()
    {
        var user = await LoginAsync("9910000003");
        Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsync($"/v1/events/{Guid.NewGuid()}/save", null)).StatusCode);
    }

    [Fact]
    public async Task Follow_then_list_then_unfollow_an_org()
    {
        var owner = await LoginAsync("9910000004");
        var (orgId, _, _) = await PublishedFreeEventAsync(owner);
        var user = await LoginAsync("9910000005");

        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync($"/v1/orgs/{orgId}/follow", null)).StatusCode);
        var following = await Json(await user.GetAsync("/v1/me/following"));
        Assert.Contains(following.EnumerateArray(), o => o.GetProperty("org_id").GetGuid() == orgId);

        await user.DeleteAsync($"/v1/orgs/{orgId}/follow");
        var after = await Json(await user.GetAsync("/v1/me/following"));
        Assert.DoesNotContain(after.EnumerateArray(), o => o.GetProperty("org_id").GetGuid() == orgId);
    }

    [Fact]
    public async Task A_ticket_holder_can_review_but_a_non_attendee_cannot()
    {
        var owner = await LoginAsync("9910000006");
        var (_, eventId, ticketTypeId) = await PublishedFreeEventAsync(owner);

        // A non-attendee is refused.
        var stranger = await LoginAsync("9910000007");
        var refused = await stranger.PostAsJsonAsync($"/v1/events/{eventId}/reviews", new { rating = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("review_requires_ticket", (await Json(refused)).GetProperty("error").GetString());

        // An attendee (free ticket issued on order) can review, and it shows up with the summary.
        var attendee = await LoginAsync("9910000008");
        await attendee.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId });
        var posted = await attendee.PostAsJsonAsync($"/v1/events/{eventId}/reviews",
            new { rating = 4, title = "Good", body = "Enjoyed it" });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.True((await Json(posted)).GetProperty("is_verified").GetBoolean());

        var list = await Json(await _factory.CreateClient().GetAsync($"/v1/events/{eventId}/reviews"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(4.0, list.GetProperty("summary").GetProperty("average").GetDouble());
    }

    [Fact]
    public async Task An_invalid_rating_is_rejected()
    {
        var owner = await LoginAsync("9910000009");
        var (_, eventId, ticketTypeId) = await PublishedFreeEventAsync(owner);
        var attendee = await LoginAsync("9910000010");
        await attendee.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId });

        var res = await attendee.PostAsJsonAsync($"/v1/events/{eventId}/reviews", new { rating = 9 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_rating", (await Json(res)).GetProperty("error").GetString());
    }
}
