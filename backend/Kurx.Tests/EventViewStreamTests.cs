using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-130: the public event-detail read appends to the <c>event_views</c> stream instead of
/// incrementing <c>events.ViewCount</c> inline, and the nightly rollup derives real traffic figures from
/// that stream (they used to be fabricated with <c>Random.Shared</c>).</summary>
public class EventViewStreamTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public EventViewStreamTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Views", Slug = "views-cat" };
            db.EventCategories.Add(category);
            db.SaveChanges();
            _categoryId = category.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid OrgId)> OwnerWithOrgAsync(string phone, string orgName)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, _factory.SeedVerifiedOrgForClient(client, orgName));
    }

    private object CreateBody(string title) => new
    {
        title,
        description = "A great show.",
        categoryId = _categoryId,
        venueName = "The Venue",
        venueAddress = "123 Main St",
        city = "Chennai",
        startsAt = DateTime.UtcNow.AddDays(10),
        endsAt = DateTime.UtcNow.AddDays(10).AddHours(3),
    };

    /// <summary>Creates an event and returns (eventId, slug), published unless told otherwise.</summary>
    private async Task<(Guid Id, string Slug)> SeedEventAsync(HttpClient client, Guid orgId, string title, bool publish = true)
    {
        var created = await Json(await client.CreateEventAsync(orgId, CreateBody(title)));
        var id = created.GetProperty("id").GetGuid();
        var slug = created.GetProperty("slug").GetString()!;
        if (publish)
        {
            _factory.SeedApprovedEventAuthorization(id);   // D-266 M5 — fixture needs a published event
            await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        }
        return (id, slug);
    }

    private async Task<T> WithDbAsync<T>(Func<KurxDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<KurxDbContext>());
    }

    [Fact]
    public async Task Public_detail_read_appends_a_view_and_leaves_ViewCount_untouched()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9740000001", "View Stream Org");
        var (eventId, slug) = await SeedEventAsync(client, orgId, "Streamed Show");

        var anon = _factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await anon.GetAsync($"/v1/events/{slug}")).StatusCode);

        var views = await WithDbAsync(db => db.EventViews.CountAsync(v => v.EventId == eventId));
        Assert.Equal(1, views);

        // The counter is now derived by the nightly rollup, never written on the read path.
        var viewCount = await WithDbAsync(db => db.Events.Where(e => e.Id == eventId).Select(e => e.ViewCount).FirstAsync());
        Assert.Equal(0, viewCount);
    }

    [Fact]
    public async Task Recorded_view_stores_no_raw_ip_or_user_agent()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9740000002", "Privacy Org");
        var (eventId, slug) = await SeedEventAsync(client, orgId, "Privacy Show");

        var anon = _factory.CreateClient();
        anon.DefaultRequestHeaders.UserAgent.ParseAdd("KurxTestAgent/9.9");
        await anon.GetAsync($"/v1/events/{slug}");

        var key = await WithDbAsync(db => db.EventViews.Where(v => v.EventId == eventId).Select(v => v.VisitorKey).FirstAsync());
        Assert.Equal(64, key.Length);                       // SHA-256 hex
        Assert.DoesNotContain("KurxTestAgent", key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".", key);                    // no dotted-quad IP leaked into the key
    }

    [Fact]
    public async Task Unpublished_event_records_no_view()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9740000003", "Draft View Org");
        var (eventId, slug) = await SeedEventAsync(client, orgId, "Draft Show", publish: false);

        // An org member may read their own draft by slug — but a draft is not public traffic.
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync($"/v1/events/{slug}")).StatusCode);

        Assert.Equal(0, await WithDbAsync(db => db.EventViews.CountAsync(v => v.EventId == eventId)));
    }

    [Fact]
    public async Task Organizer_dashboard_read_records_no_view()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9740000004", "Dashboard Org");
        var (eventId, _) = await SeedEventAsync(client, orgId, "Dashboard Show");

        await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}");

        Assert.Equal(0, await WithDbAsync(db => db.EventViews.CountAsync(v => v.EventId == eventId)));
    }

    // V3 §16 (Phase 17): the daily rollup job (EventAnalyticsDaily/OrganizationAnalytics, AggregateDailyStatsAsync)
    // is retired — analytics reads leaf facts (EventViews here) at request time instead. These three tests
    // used to exercise that job directly; rewritten to assert the same real-traffic/dedup/zero-traffic
    // behavior against the read-time path (IAnalyticsService.GetOrgAnalyticsAsync), which is what's left.
    // The old "rerun doesn't double-count" test is dropped: with no write-on-read and no accumulator, that
    // whole bug class is now structurally impossible rather than merely tested-for.
    [Fact]
    public async Task Org_analytics_reports_real_views_and_deduplicates_unique_visitors()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9740000005", "Rollup Org");
        var (eventId, _) = await SeedEventAsync(client, orgId, "Rollup Show");
        var ownerId = await WithDbAsync(db => db.Memberships.Where(m => m.OrgId == orgId && m.Role == OrgRole.Owner).Select(m => m.UserId).FirstAsync());

        var today = DateTime.UtcNow.Date;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Three views from two distinct visitors.
            db.EventViews.AddRange(
                new EventView { EventId = eventId, VisitorKey = "visitor-a", ViewedAt = today.AddHours(1) },
                new EventView { EventId = eventId, VisitorKey = "visitor-a", ViewedAt = today.AddHours(2) },
                new EventView { EventId = eventId, VisitorKey = "visitor-b", ViewedAt = today.AddHours(3) });
            await db.SaveChangesAsync();
        }

        using var readScope = _factory.Services.CreateScope();
        var result = await readScope.ServiceProvider.GetRequiredService<IAnalyticsService>()
            .GetOrgAnalyticsAsync(ownerId, orgId);
        Assert.True(result.Ok);
        Assert.Equal(3, result.Value!.Views);
        Assert.Equal(2, result.Value.UniqueVisitors);
    }

    [Fact]
    public async Task Org_analytics_reports_zero_traffic_when_there_are_no_views()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9740000006", "Zero Traffic Org");
        await SeedEventAsync(client, orgId, "Unwatched Show");
        var ownerId = await WithDbAsync(db => db.Memberships.Where(m => m.OrgId == orgId && m.Role == OrgRole.Owner).Select(m => m.UserId).FirstAsync());

        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAnalyticsService>()
            .GetOrgAnalyticsAsync(ownerId, orgId);

        // Previously the nightly job could report Random.Shared traffic for an event nobody visited.
        Assert.True(result.Ok);
        Assert.Equal(0, result.Value!.Views);
        Assert.Equal(0, result.Value.UniqueVisitors);
    }
}
