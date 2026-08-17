using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Kurx.Tests;

public class AnalyticsAndGamificationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public AnalyticsAndGamificationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string JwtSecret = "dev-only-secret-change-me-0123456789abcdef";
    private const string Issuer = "kurx";
    private const string Audience = "kurx-app";

    private static string TokenFor(Guid userId, params (string Type, string Value)[] extra)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(extra.Select(c => new Claim(c.Type, c.Value)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var jwt = new JwtSecurityToken(Issuer, Audience, claims,
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private async Task<Guid> CreateUserAsync(string? username = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User
        {
            Phone = $"9197{Random.Shared.Next(100000, 999999)}",
            Name = "V2 Test User",
            Username = username
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> CreateOrgAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var org = new Organization
        {
            Name = "V2 Org " + Guid.NewGuid().ToString("N")[..8],
            Slug = "v2org" + Guid.NewGuid().ToString("N")[..6],
            VerificationStatus = OrgVerificationStatus.Verified
        };
        db.Organizations.Add(org);
        db.Memberships.Add(new Membership { OrgId = org.Id, UserId = userId, Role = OrgRole.Owner });
        await db.SaveChangesAsync();
        return org.Id;
    }

    private HttpClient ClientFor(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Can_register_list_and_delete_devices()
    {
        var userId = await CreateUserAsync();
        var token = TokenFor(userId);
        var client = ClientFor(token);

        // 1. Register device
        var regRes = await client.PostAsJsonAsync("/v1/devices/register", new
        {
            fcmToken = "test-fcm-token-123",
            platform = "android",
            deviceName = "Pixel 8 Pro",
            appVersion = "2.4.1"
        });
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);

        // 2. List devices
        var listRes = await client.GetAsync("/v1/me/devices");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var devices = await TestJson.ReadAsync<List<DeviceView>>(listRes);
        Assert.NotNull(devices);
        Assert.Single(devices);
        Assert.Equal("test-fcm-token-123", devices[0].FcmToken);
        Assert.Equal("android", devices[0].Platform);
        Assert.Equal("Pixel 8 Pro", devices[0].DeviceName);
        Assert.Equal("2.4.1", devices[0].AppVersion);

        // 3. Delete device
        var delRes = await client.DeleteAsync($"/v1/devices/{devices[0].Id}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        // 4. Verify deleted
        var listEmptyRes = await client.GetAsync("/v1/me/devices");
        var emptyDevices = await TestJson.ReadAsync<List<DeviceView>>(listEmptyRes);
        Assert.NotNull(emptyDevices);
        Assert.Empty(emptyDevices);
    }

    /// <summary>Exercises `MeNotificationEndpoints` (`/v1/me/notifications*`) — the current-generation
    /// notification surface. The old `/v1/notifications*` family (`NotificationEndpoints.cs`) was
    /// removed as dead code (D-212): no client anywhere ever called it, and its doc comment confirmed
    /// `MeNotificationEndpoints` was already its intended replacement. The one capability the old file
    /// had that the new one didn't — deleting a single notification — was ported over rather than
    /// dropped; step 6 below is that new route's only test coverage.</summary>
    [Fact]
    public async Task Can_list_count_read_and_clear_notifications()
    {
        var userId = await CreateUserAsync();
        var token = TokenFor(userId);
        var client = ClientFor(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await svc.NotifyAsync(userId, "TicketPurchased", "Ticket Issued", "You bought a ticket.");
            await svc.NotifyAsync(userId, "EventReminder", "Reminder", "Event starts tomorrow.");
        }

        // 1. List + unread count come back together
        var listRes = await client.GetAsync("/v1/me/notifications");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var feed = await TestJson.ReadAsync<MeNotificationFeed>(listRes);
        Assert.NotNull(feed);
        Assert.Equal(2, feed.unread_count);
        Assert.Equal(2, feed.items.Count);

        // 2. Mark one read
        var readRes = await client.PostAsync($"/v1/me/notifications/{feed.items[0].id}/read", null);
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);

        // 3. Verify unread count is 1
        var afterOneRes = await client.GetAsync("/v1/me/notifications");
        var afterOne = await TestJson.ReadAsync<MeNotificationFeed>(afterOneRes);
        Assert.NotNull(afterOne);
        Assert.Equal(1, afterOne.unread_count);

        // 4. Read all
        var readAllRes = await client.PostAsync("/v1/me/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, readAllRes.StatusCode);

        // 5. Verify unread count is 0
        var afterAllRes = await client.GetAsync("/v1/me/notifications");
        var afterAll = await TestJson.ReadAsync<MeNotificationFeed>(afterAllRes);
        Assert.NotNull(afterAll);
        Assert.Equal(0, afterAll.unread_count);

        // 6. Delete one — the capability ported from the removed old endpoint file.
        var deleteRes = await client.DeleteAsync($"/v1/me/notifications/{afterAll.items[0].id}");
        Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);
        var afterDeleteRes = await client.GetAsync("/v1/me/notifications");
        var afterDelete = await TestJson.ReadAsync<MeNotificationFeed>(afterDeleteRes);
        Assert.NotNull(afterDelete);
        Assert.Single(afterDelete.items);
    }

    [Fact]
    public async Task Admin_can_broadcast_notifications()
    {
        var adminId = await CreateUserAsync();
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.PlatformRoles.Add(new PlatformRoleAssignment { UserId = adminId, Role = PlatformRole.SuperAdmin });
            await db.SaveChangesAsync();
        }

        var adminToken = TokenFor(adminId, ("kurx_admin", "true"));
        var client = ClientFor(adminToken);

        var broadcastRes = await client.PostAsJsonAsync("/v1/admin/notifications/broadcast", new
        {
            title = "Global Announcement",
            message = "Maintenance tomorrow at 2:00 AM UTC",
            dataJson = "{\"maintenance\": true}"
        });
        Assert.Equal(HttpStatusCode.OK, broadcastRes.StatusCode);
    }

    [Fact]
    public async Task Can_query_analytics_and_export_csv()
    {
        var userId = await CreateUserAsync();
        var orgId = await CreateOrgAsync(userId);
        var token = TokenFor(userId);
        var client = ClientFor(token);

        // 1. Fetch org analytics
        var res = await client.GetAsync($"/v1/orgs/{orgId}/analytics");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // 2. Create Event to query event analytics
        Guid eventId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var category = db.EventCategories.First();
            var ev = new Event
            {
                RepresentingOrgId = orgId,
                Title = "Analytics Test Event",
                Slug = "analytics-event-" + Guid.NewGuid().ToString("N")[..8],
                ShortCode = Guid.NewGuid().ToString("N")[..8],
                CategoryId = category.Id,
                Status = EventStatus.Published,
                CreatedBy = userId
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;
        }
        // V3 §16 (Phase 17): no more daily-rollup seeding step — sales/attendance/revenue/export all read
        // leaf facts at request time, so an empty-but-valid response is exactly what's expected here.

        // 3. Query sales timeline
        var salesRes = await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics/sales");
        Assert.Equal(HttpStatusCode.OK, salesRes.StatusCode);

        // 4. Query attendance
        var attRes = await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics/attendance");
        Assert.Equal(HttpStatusCode.OK, attRes.StatusCode);

        // 5. Query revenue
        var revRes = await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics/revenue");
        Assert.Equal(HttpStatusCode.OK, revRes.StatusCode);

        // 6. Export CSV
        var exportRes = await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics/export");
        Assert.Equal(HttpStatusCode.OK, exportRes.StatusCode);
        Assert.Equal("text/csv", exportRes.Content.Headers.ContentType?.MediaType);
        var csvString = await exportRes.Content.ReadAsStringAsync();
        Assert.Contains("Date,Views,UniqueVisitors", csvString);
    }

    [Fact]
    public async Task Can_award_points_and_apply_referral()
    {
        var referrerId = await CreateUserAsync("referrerUser");
        var refereeId = await CreateUserAsync();

        var refereeToken = TokenFor(refereeId);
        var client = ClientFor(refereeToken);

        // Apply referral code
        var refRes = await client.PostAsJsonAsync("/v1/referrals/apply", new { referralCode = "referrerUser" });
        Assert.Equal(HttpStatusCode.OK, refRes.StatusCode);

        // Verify points history
        var pointsRes = await client.GetAsync("/v1/me/points");
        Assert.Equal(HttpStatusCode.OK, pointsRes.StatusCode);
        var pointsSummary = await TestJson.ReadAsync<PointsSummary>(pointsRes);
        Assert.NotNull(pointsSummary);
        Assert.Equal(50, pointsSummary.TotalPoints);
        Assert.Single(pointsSummary.History);
        Assert.Equal("Referral Signup", pointsSummary.History[0].Source);
    }

    /// <summary>D-368 — the public organization leaderboard ranks real organizations only.
    ///
    /// <para>It grouped published events by <c>RepresentingOrgId</c> with no scope filter, so a
    /// self-representation row (D-268 persistence, named after the person who created the event) could be
    /// published on <c>GET /v1/gamification/leaderboards/organizations</c> as though it were an
    /// institution — and consume one of the 100 ranked slots doing it. The surface had no test at all,
    /// which is how it survived D-353.</para></summary>
    [Fact]
    public async Task Org_leaderboard_ranks_real_organizations_and_never_self_representation_rows()
    {
        var userId = await CreateUserAsync();
        var realOrgId = await CreateOrgAsync(userId);

        Guid personalOrgId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var personal = new Organization
            {
                Name = "D368 Self Row " + Guid.NewGuid().ToString("N")[..6],
                Slug = "self-" + Guid.NewGuid().ToString("N")[..12],
                IsPersonal = true,
            };
            personal.CanonicalOrgId = personal.Id;
            db.Organizations.Add(personal);
            await db.SaveChangesAsync();
            personalOrgId = personal.Id;

            // One published event each, so both would rank identically without the scope filter — the
            // personal row is excluded because of what it IS, never because it scored lower.
            var category = db.EventCategories.First();
            foreach (var orgId in new[] { realOrgId, personalOrgId })
                db.Events.Add(new Event
                {
                    RepresentingOrgId = orgId,
                    Title = "D368 Leaderboard Event",
                    Slug = "d368-lb-" + Guid.NewGuid().ToString("N")[..8],
                    ShortCode = Guid.NewGuid().ToString("N")[..8],
                    CategoryId = category.Id,
                    Status = EventStatus.Published,
                    CreatedBy = userId,
                });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IGamificationService>();
            await svc.RefreshLeaderboardsAsync();
            var board = await svc.GetOrgLeaderboardAsync(100);

            Assert.Contains(board, e => e.OrgId == realOrgId);
            Assert.DoesNotContain(board, e => e.OrgId == personalOrgId);
        }

        // The row itself is untouched — this is a ranking rule, not a deletion.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.True(db.Organizations.Any(o => o.Id == personalOrgId && o.IsPersonal));
        }
    }

    private class MeNotificationFeed
    {
        public List<MeNotificationItem> items { get; set; } = new();
        public int unread_count { get; set; }
    }

    private class MeNotificationItem
    {
        public Guid id { get; set; }
    }
}
