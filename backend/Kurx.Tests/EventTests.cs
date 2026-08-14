using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kurx.Tests;

public class EventTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public EventTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Music", Slug = "music" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
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

    private object ValidCreateBody(string title = "Indie Night") => new
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

    [Fact]
    public async Task Owner_sees_event_analytics_summary()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000191", "Analytics Org");
        var ev = await Json(await client.CreateEventAsync(orgId, ValidCreateBody("Analytics Show")));
        var eventId = ev.GetProperty("id").GetGuid();

        var res = await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var a = await Json(res);
        Assert.Equal(eventId, a.GetProperty("event_id").GetGuid());
        Assert.Equal(0, a.GetProperty("view_count").GetInt32());
        Assert.Equal(0, a.GetProperty("ticket_types").GetInt32());
        Assert.Equal(0, a.GetProperty("tickets_issued").GetInt32());
        Assert.Equal(0, a.GetProperty("checked_in").GetInt32());
        Assert.Equal(0, a.GetProperty("orders_paid").GetInt32());
        Assert.Equal(0L, a.GetProperty("gross_paise").GetInt64());
    }

    [Fact]
    public async Task Non_member_gets_not_found_for_analytics()
    {
        var (owner, orgId) = await OwnerWithOrgAsync("9700000192", "Analytics Owner Org");
        var ev = await Json(await owner.CreateEventAsync(orgId, ValidCreateBody("Hidden Analytics")));
        var eventId = ev.GetProperty("id").GetGuid();

        // A member of a *different* org must get 404 (existence hidden), never 403.
        var (stranger, _) = await OwnerWithOrgAsync("9700000193", "Stranger Org");
        var res = await stranger.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_event_in_draft()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000001", "Music Org");
        var res = await client.CreateEventAsync(orgId, ValidCreateBody());
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var ev = await Json(res);
        Assert.Equal("draft", ev.GetProperty("status").GetString());
        Assert.Equal("Indie Night", ev.GetProperty("title").GetString());
        Assert.Equal("Chennai", ev.GetProperty("venue").GetProperty("city").GetString());

        var shortCode = ev.GetProperty("short_code").GetString();
        Assert.False(string.IsNullOrWhiteSpace(shortCode));
        Assert.Equal(6, shortCode!.Length);
    }

    [Fact]
    public async Task Staff_cannot_create_event()
    {
        var (owner, orgId) = await OwnerWithOrgAsync("9700000002", "Staff Test Org");
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/members", new { phone = "9700000003", role = "staff" });

        var staffClient = _factory.CreateClient();
        await staffClient.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "9700000003" });
        var code = _factory.WhatsApp.LastOtpFor("9700000003");
        var verify = await staffClient.PostAsJsonAsync("/v1/auth/otp/verify", new { phone = "9700000003", code });
        var tokens = await Json(verify);
        staffClient.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());

        var res = await staffClient.CreateEventAsync(orgId, ValidCreateBody());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Empty_title_is_rejected_by_validation()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000004", "Validation Org");
        var body = new
        {
            title = "",
            categoryId = _categoryId,
            startsAt = DateTime.UtcNow.AddDays(1),
            endsAt = DateTime.UtcNow.AddDays(1).AddHours(1),
        };
        var res = await client.CreateEventAsync(orgId, body);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invalid_category_is_rejected_by_service()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000005", "Bad Category Org");
        var body = new
        {
            title = "Some Event",
            categoryId = Guid.NewGuid(), // well-formed but doesn't exist
            startsAt = DateTime.UtcNow.AddDays(1),
            endsAt = DateTime.UtcNow.AddDays(1).AddHours(1),
        };
        var res = await client.CreateEventAsync(orgId, body);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_category", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Publish_requires_description_and_venue_then_status_workflow_proceeds()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000006", "Workflow Org");

        var incomplete = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Bare Event",
            categoryId = _categoryId,
            startsAt = DateTime.UtcNow.AddDays(1),
            endsAt = DateTime.UtcNow.AddDays(1).AddHours(1),
        }));
        var eventId = incomplete.GetProperty("id").GetGuid();
        // D-266 M5: authorize the event so the READINESS gate is what this test observes. Without it the
        // publish is refused earlier, for institutional authorization, and the assertions below would be
        // measuring a different rule than the one they name.
        _factory.SeedApprovedEventAuthorization(eventId);

        var failedPublish = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.BadRequest, failedPublish.StatusCode);
        Assert.Equal("missing_description", (await Json(failedPublish)).GetProperty("error").GetString());

        await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { description = "Now complete.", venueName = "Hall A" });

        var published = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("published", (await Json(published)).GetProperty("status").GetString());

        var closed = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "close" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal("closed", (await Json(closed)).GetProperty("status").GetString());

        var archived = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "archive" });
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Equal("archived", (await Json(archived)).GetProperty("status").GetString());

        // Archived is terminal: publishing again is not a valid transition.
        var invalid = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        Assert.Equal("invalid_transition", (await Json(invalid)).GetProperty("error").GetString());
    }

    /// <summary>D-299. An Online event has no venue by definition, and the publish readiness gate demanded
    /// one unconditionally — so an online event could never be published at all, through the one door the
    /// wizards and the whole review lifecycle use. The V3 §14.2 Scheduled gate beside it already accepted
    /// an OnlineUrl; this asserts the two now agree.</summary>
    [Fact]
    public async Task An_online_event_publishes_on_its_joining_link_and_needs_no_venue()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000031", "Online Publish Org");

        var created = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Remote Only Summit",
            description = "An event that exists solely online.",
            categoryId = _categoryId,
            eventMode = "online",
            onlineUrl = "https://meet.example.com/summit",
            startsAt = DateTime.UtcNow.AddDays(3),
            endsAt = DateTime.UtcNow.AddDays(3).AddHours(2),
        }));
        var eventId = created.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);

        var published = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("published", (await Json(published)).GetProperty("status").GetString());
    }

    /// <summary>The other half of the gate. <c>ValidateMode</c> refuses an Online event with no OnlineUrl on
    /// BOTH create and update, so this state cannot be reached through the API at all — the row is written
    /// directly here, which is the only way it could arise (a migration, a backfill, an admin tool). The
    /// branch is kept rather than dropped because the sibling §14.2 gate carries the same one, and a publish
    /// gate that trusts an invariant enforced elsewhere is how the venue bug happened in the first place.</summary>
    [Fact]
    public async Task An_online_event_whose_link_was_lost_is_refused_by_name_not_for_a_venue()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000032", "Online Refuse Org");

        var created = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Linkless Remote Summit",
            description = "An online event with nowhere to go.",
            categoryId = _categoryId,
            eventMode = "online",
            onlineUrl = "https://meet.example.com/gone",
            startsAt = DateTime.UtcNow.AddDays(3),
            endsAt = DateTime.UtcNow.AddDays(3).AddHours(2),
        }));
        var eventId = created.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db.Events.FirstAsync(e => e.Id == eventId);
            ev.OnlineUrl = null;
            await db.SaveChangesAsync();
        }

        var refused = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        // Names the missing link, not a venue an online event is never going to have.
        Assert.Equal("missing_online_url", (await Json(refused)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Unpublish_returns_event_to_draft()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000007", "Unpublish Org");
        var created = await Json(await client.CreateEventAsync(orgId, ValidCreateBody()));
        var eventId = created.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — this test is about unpublish

        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        var unpublished = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "unpublish" });
        Assert.Equal(HttpStatusCode.OK, unpublished.StatusCode);
        Assert.Equal("draft", (await Json(unpublished)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Draft_can_be_deleted_but_published_cannot()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000008", "Delete Org");
        var draft = await Json(await client.CreateEventAsync(orgId, ValidCreateBody()));
        var draftId = draft.GetProperty("id").GetGuid();

        var deleteDraft = await client.DeleteAsync($"/v1/orgs/{orgId}/events/{draftId}");
        Assert.Equal(HttpStatusCode.OK, deleteDraft.StatusCode);

        var published = await Json(await client.CreateEventAsync(orgId, ValidCreateBody("Published Event")));
        var publishedId = published.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(publishedId);   // D-266 M5 — this test is about deletion
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{publishedId}/transition", new { action = "publish" });

        var deletePublished = await client.DeleteAsync($"/v1/orgs/{orgId}/events/{publishedId}");
        Assert.Equal(HttpStatusCode.Conflict, deletePublished.StatusCode);
        Assert.Equal("not_draft", (await Json(deletePublished)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Public_search_only_shows_published_events()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000009", "Search Org");
        var draft = await Json(await client.CreateEventAsync(orgId, ValidCreateBody("Hidden Draft")));
        var published = await Json(await client.CreateEventAsync(orgId, ValidCreateBody("Visible Show")));
        _factory.SeedApprovedEventAuthorization(published.GetProperty("id").GetGuid());   // D-266 M5 — about search
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{published.GetProperty("id").GetGuid()}/transition", new { action = "publish" });

        // V3 §15 (Phase 16): discovery is the outbox-fed index — drain the reindex message before searching.
        using (var s = _factory.Services.CreateScope()) await s.ServiceProvider.GetRequiredService<OutboxDispatchJob>().RunAsync();

        var anon = _factory.CreateClient();
        var search = await Json(await anon.GetAsync("/v1/events?q=Show"));
        var titles = search.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();
        Assert.Contains("Visible Show", titles);
        Assert.DoesNotContain("Hidden Draft", titles);

        var draftBySlug = await anon.GetAsync($"/v1/events/{draft.GetProperty("slug").GetString()}");
        Assert.Equal(HttpStatusCode.NotFound, draftBySlug.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_manage_events_in_orgs_they_do_not_belong_to()
    {
        var (owner, orgId) = await OwnerWithOrgAsync("9700000010", "Admin Target Org");
        var created = await Json(await owner.CreateEventAsync(orgId, ValidCreateBody()));
        var eventId = created.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — this test is about admin authority

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", await AdminTokenAsync());

        var res = await adminClient.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private const string JwtSecret = "dev-only-secret-change-me-0123456789abcdef";
    private const string Issuer = "kurx";
    private const string Audience = "kurx-app";

    // Platform admin authority is now a real, live-read grant in platform_roles (M2, D-040) — no
    // longer a forged "kurx_admin" token claim. Seed a real user + SuperAdmin grant, then mint a token
    // for that user; PlatformRoleClaimsTransformation resolves the role from the DB per request.
    private async Task<string> AdminTokenAsync()
    {
        Guid adminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var admin = new User { Phone = $"9198{Random.Shared.Next(100000, 999999)}", Name = "Platform Admin" };
            db.Users.Add(admin);
            db.PlatformRoles.Add(new PlatformRoleAssignment { UserId = admin.Id, Role = PlatformRole.SuperAdmin });
            await db.SaveChangesAsync();
            adminId = admin.Id;
        }
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, adminId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var jwt = new JwtSecurityToken(Issuer, Audience, claims,
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
