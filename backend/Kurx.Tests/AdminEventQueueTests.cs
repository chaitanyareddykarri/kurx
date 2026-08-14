using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Admin event-approval queue (M8, D-057): lists events awaiting review across all orgs; a reviewer
/// approves (publish) or rejects them via the existing transition. Reviewer-gated. Real HTTP/kurx_test.</summary>
public class AdminEventQueueTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public AdminEventQueueTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Appr Cat", Slug = "appr-cat" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
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

    // A paid event left in the review queue: verified org + paid-capable owner + priced ticket + submit_review.
    private async Task<(Guid OrgId, Guid EventId)> InReviewPaidEventAsync(HttpClient owner, HttpClient reviewer)
    {
        await owner.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Owner" });
        await owner.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Owner" });

        // D-075: seed a Verified org the owner manages (institutions are no longer self-minted then verified via HTTP here).
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Appr Org " + Guid.NewGuid().ToString("N")[..6], "Company");

        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Appr Gala " + Guid.NewGuid().ToString("N")[..6],
            description = "A ticketed gala.", categoryId = _categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.TicketTypes.Add(new TicketType
            {
                EventId = eventId, Name = "General", PricePaise = 50000, Quantity = 100,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30),
            });
            await db.SaveChangesAsync();
        }

        // D-266 M5 — the reviewer must be able to publish this queued event; institutional authorization is
        // a separate gate and this fixture is about the review queue.
        _factory.SeedApprovedEventAuthorization(eventId);

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        return (orgId, eventId);
    }

    [Fact]
    public async Task Pending_queue_lists_an_event_in_review_then_drops_it_after_approval()
    {
        var reviewer = await ReviewerAsync("9950000001");
        var (owner, _) = await LoginAsync("9950000002");
        var (orgId, eventId) = await InReviewPaidEventAsync(owner, reviewer);

        var pending = await Json(await reviewer.GetAsync("/v1/admin/events/pending"));
        Assert.Contains(pending.EnumerateArray(), e => e.GetProperty("event_id").GetGuid() == eventId);

        // Approve via the existing transition — a reviewer can drive any org's event (M8).
        Assert.Equal(HttpStatusCode.OK,
            (await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" })).StatusCode);

        var after = await Json(await reviewer.GetAsync("/v1/admin/events/pending"));
        Assert.DoesNotContain(after.EnumerateArray(), e => e.GetProperty("event_id").GetGuid() == eventId);
    }

    [Fact]
    public async Task A_non_reviewer_cannot_see_the_pending_queue()
    {
        var (user, _) = await LoginAsync("9950000003");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/v1/admin/events/pending")).StatusCode);
    }

    [Fact]
    public async Task Admin_event_search_lists_an_event_and_feature_toggles_it()
    {
        var reviewer = await ReviewerAsync("9950000010");
        var (owner, _) = await LoginAsync("9950000011");
        var (_, eventId) = await InReviewPaidEventAsync(owner, reviewer);

        // D-187: GET /v1/admin/events now returns {items, total} (real pagination) rather than a bare
        // array — the old shape hard-capped at 100 rows with no way to reach row 101.
        var list = await Json(await reviewer.GetAsync("/v1/admin/events"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), e => e.GetProperty("event_id").GetGuid() == eventId);

        var featured = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/feature", new { }));
        Assert.True(featured.GetProperty("is_featured").GetBoolean());

        var unfeatured = await Json(await reviewer.PostAsJsonAsync($"/v1/admin/events/{eventId}/unfeature", new { }));
        Assert.False(unfeatured.GetProperty("is_featured").GetBoolean());
    }

    [Fact]
    public async Task A_non_reviewer_cannot_search_all_events()
    {
        var (user, _) = await LoginAsync("9950000012");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/v1/admin/events")).StatusCode);
    }
}
