using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

public class SupportingEntityTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public SupportingEntityTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                // ResetDatabase wipes what Program.cs's one-time startup seeding already inserted
                // during the factory's first boot; re-seed so tests that depend on system templates work.
                using var scope = factory.Services.CreateScope();
                Kurx.Infrastructure.Events.SystemTemplateSeeder.SeedAsync(
                    scope.ServiceProvider.GetRequiredService<KurxDbContext>()).GetAwaiter().GetResult();
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid OrgId)> OwnerWithOrgAsync(string phone, string orgName)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        var tokens = await Json(verify);
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());

        var orgId = _factory.SeedVerifiedOrgForClient(client, orgName);
        return (client, orgId);
    }

    [Fact]
    public async Task System_templates_are_seeded_and_public()
    {
        var anon = _factory.CreateClient();
        var templates = await Json(await anon.GetAsync("/v1/templates"));
        var names = templates.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("Conference", names);
        Assert.Contains("Workshop", names);
    }

    [Fact]
    public async Task Venue_crud_and_search_by_org()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9800000001", "Venue Org");
        var created = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/venues", new
        {
            name = "Grand Hall",
            address = "1 Main St",
            city = "Bengaluru",
            capacity = 500,
            hasParking = true,
            isAccessible = true,
        }));
        var venueId = created.GetProperty("id").GetGuid();
        Assert.Equal("Bengaluru", created.GetProperty("city").GetString());

        var list = await Json(await client.GetAsync($"/v1/orgs/{orgId}/venues"));
        Assert.Single(list.EnumerateArray());

        var updated = await Json(await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/venues/{venueId}", new { name = "Grand Hall", capacity = 600 }));
        Assert.Equal(600, updated.GetProperty("capacity").GetInt32());
    }

    [Fact]
    public async Task Event_can_use_a_saved_venue_and_denormalizes_city()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9800000002", "Linked Venue Org");
        var venue = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/venues", new { name = "Arena", city = "Mumbai" }));
        var venueId = venue.GetProperty("id").GetGuid();

        var categoryRes = await CreateCategoryAsync();

        var ev = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Arena Show",
            description = "desc",
            categoryId = categoryRes,
            venueId,
            startsAt = DateTime.UtcNow.AddDays(5),
            endsAt = DateTime.UtcNow.AddDays(5).AddHours(2),
        }));
        Assert.Equal("Mumbai", ev.GetProperty("venue").GetProperty("city").GetString());
        Assert.Equal(venueId, ev.GetProperty("venue").GetProperty("venue_id").GetGuid());
    }

    [Fact]
    public async Task Speaker_can_be_created_and_assigned_to_event_and_session()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9800000003", "Speaker Org");
        var categoryId = await CreateCategoryAsync();
        var ev = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Talk Night",
            description = "desc",
            categoryId,
            venueName = "Hall",
            startsAt = DateTime.UtcNow.AddDays(3),
            endsAt = DateTime.UtcNow.AddDays(3).AddHours(2),
        }));
        var eventId = ev.GetProperty("id").GetGuid();

        var session = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/sessions", new
        {
            title = "Opening Keynote",
            startsAt = DateTime.UtcNow.AddDays(3),
            endsAt = DateTime.UtcNow.AddDays(3).AddHours(1),
        }));
        var sessionId = session.GetProperty("id").GetGuid();

        var speaker = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/speakers", new { name = "Ada Lovelace", company = "Analytical Engines" }));
        var speakerId = speaker.GetProperty("id").GetGuid();

        var assign = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/speakers", new { speakerId, sessionId });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var eventSpeakers = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/speakers"));
        Assert.Single(eventSpeakers.EnumerateArray());

        var sessionsAfter = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/sessions"));
        var speakerIds = sessionsAfter.EnumerateArray().First().GetProperty("speaker_ids").EnumerateArray().ToList();
        Assert.Single(speakerIds);
    }

    [Fact]
    public async Task Sponsor_tiers_and_event_assignment()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9800000004", "Sponsor Org");
        var sponsor = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/sponsors", new { name = "Acme Corp", tier = "gold", priority = 1 }));
        Assert.Equal("gold", sponsor.GetProperty("tier").GetString());

        var categoryId = await CreateCategoryAsync();
        var ev = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Sponsored Show",
            description = "desc",
            categoryId,
            venueName = "Hall",
            startsAt = DateTime.UtcNow.AddDays(4),
            endsAt = DateTime.UtcNow.AddDays(4).AddHours(2),
        }));
        var eventId = ev.GetProperty("id").GetGuid();

        var assign = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors", new { sponsorId = sponsor.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var list = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/sponsors"));
        Assert.Single(list.EnumerateArray());
    }

    [Fact]
    public async Task Media_presign_attach_and_remove()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9800000005", "Media Org");
        var categoryId = await CreateCategoryAsync();
        var ev = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Media Event",
            description = "desc",
            categoryId,
            venueName = "Hall",
            startsAt = DateTime.UtcNow.AddDays(2),
            endsAt = DateTime.UtcNow.AddDays(2).AddHours(2),
        }));
        var eventId = ev.GetProperty("id").GetGuid();

        var presign = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/media/presign",
            new { contentType = "image/png", maxBytes = 1_000_000 }));
        var key = presign.GetProperty("key").GetString();

        var attach = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/media", new { kind = "gallery", key, caption = "Cover shot" });
        Assert.Equal(HttpStatusCode.OK, attach.StatusCode);

        var detail = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}"));
        var media = detail.GetProperty("media").EnumerateArray().ToList();
        Assert.Single(media);
        Assert.Equal("gallery", media[0].GetProperty("kind").GetString());

        var mediaId = media[0].GetProperty("id").GetGuid();
        var remove = await client.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/media/{mediaId}");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
    }

    [Fact]
    public async Task Category_admin_crud_requires_kurx_admin()
    {
        var (client, _) = await OwnerWithOrgAsync("9800000006", "Category Org");
        var res = await client.PostAsJsonAsync("/v1/categories", new { level = "Category", name = "Should Fail" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Tag_search_finds_tags_synced_from_events()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9800000007", "Tag Org");
        var categoryId = await CreateCategoryAsync();
        var created = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Tagged Event",
            description = "desc",
            categoryId,
            venueName = "Hall",
            tags = new[] { "Live Music", "Outdoor" },
            startsAt = DateTime.UtcNow.AddDays(6),
            endsAt = DateTime.UtcNow.AddDays(6).AddHours(2),
        }));
        Assert.Equal(2, created.GetProperty("tags").GetArrayLength());

        var anon = _factory.CreateClient();
        var search = await Json(await anon.GetAsync("/v1/tags?q=Live"));
        var names = search.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("Live Music", names);
    }

    private static int _categoryCounter;

    private async Task<Guid> CreateCategoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var name = $"Category {Interlocked.Increment(ref _categoryCounter)}";
        var category = new EventCategory
        {
            Level = CategoryLevel.Category,
            Name = name,
            Slug = name.ToLowerInvariant().Replace(" ", "-"),
        };
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }
}
