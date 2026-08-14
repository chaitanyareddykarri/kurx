using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-229 — regression tests for every Critical and High finding from the production audit.
///
/// <para>Each of these encodes a defect that <b>shipped with a green test suite</b>. The suite proved
/// the backend contract and proved nothing about whether the numbers agreed with each other, whether
/// a hidden membership actually hid anything, or whether a shared cache could cross-serve a private
/// view. These are the tests that would have caught them.</para></summary>
public class ProfileAuditRegressionTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileAuditRegressionTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9204{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync()
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<string> ClaimAsync(Guid userId)
    {
        var username = "aud" + Guid.NewGuid().ToString("N")[..15];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Username = username;
        user.ProfilePublic = true;
        await db.SaveChangesAsync();
        return username;
    }

    /// <summary>Seeds a finished public event owned by a new org, optionally making the user a member
    /// (which is what makes them its organizer).</summary>
    private async Task<(Guid EventId, Guid OrgId)> SeedOrganizedEventAsync(
        KurxDbContext db, Guid userId, bool member, bool showOnProfile = true, int daysAgo = 30)
    {
        var creator = new User
        {
            Phone = "9199" + Random.Shared.Next(1000000, 9999999),
            Name = "Aud Creator",
        };
        db.Users.Add(creator);
        var org = new Organization
        {
            Name = "Aud Org " + Guid.NewGuid().ToString("N")[..6],
            Slug = "audorg" + Guid.NewGuid().ToString("N")[..10],
        };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "aud-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Aud Cat", Slug = "aud-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = creator.Id,
            Title = "Aud Event " + Guid.NewGuid().ToString("N")[..5],
            Slug = "audev-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = EventVisibility.Listed,
            StartsAt = DateTime.UtcNow.AddDays(-daysAgo), EndsAt = DateTime.UtcNow.AddDays(-daysAgo + 1),
        };
        db.Events.Add(ev);

        if (member)
        {
            db.Memberships.Add(new Membership
            {
                UserId = userId, OrgId = org.Id, Role = OrgRole.Manager,
                ShowOnProfile = showOnProfile, CreatedAt = DateTime.UtcNow.AddDays(-60),
            });
        }
        await db.SaveChangesAsync();
        return (ev.Id, org.Id);
    }

    // ── C2 · one canonical definition per metric ─────────────────────────────

    /// <summary>The audit's worst finding: three surfaces each had their own "events organized"
    /// definition and could display three different numbers on one page. They must now agree exactly,
    /// because they all read <c>ProfileCounts</c>.</summary>
    [Fact]
    public async Task Events_organized_is_identical_across_root_stats_metrics_and_experience()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 3; i++) await SeedOrganizedEventAsync(db, userId, member: true, daysAgo: 30 + i);
            // A future event must not inflate any of the three — the old metrics/experience engines
            // omitted the time filter and counted it while the root profile did not.
            await SeedOrganizedEventAsync(db, userId, member: true, daysAgo: -30);
        }

        var anon = _factory.CreateClient();
        var root = await Json(await anon.GetAsync($"/v1/public/users/{username}"));
        var metrics = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));
        var experience = await Json(await anon.GetAsync($"/v1/public/users/{username}/experience"));

        var fromRoot = root.GetProperty("stats").GetProperty("events_conducted").GetInt32();
        var fromMetrics = metrics.GetProperty("events_organized").GetInt32();
        var fromExperience = experience.GetProperty("events_organized").GetInt32();

        Assert.Equal(3, fromRoot);
        Assert.Equal(fromRoot, fromMetrics);
        Assert.Equal(fromRoot, fromExperience);
    }

    // ── H1 · a hidden membership contributes to nothing ──────────────────────

    /// <summary>Hiding a membership must remove its events from every count, not just from the
    /// organizations list. The cached root summary used to ignore <c>ShowOnProfile</c> entirely, so
    /// the privacy control silently half-worked.</summary>
    [Fact]
    public async Task A_hidden_membership_contributes_to_no_count_anywhere()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await SeedOrganizedEventAsync(db, userId, member: true, showOnProfile: true);
            await SeedOrganizedEventAsync(db, userId, member: true, showOnProfile: false);
        }

        var anon = _factory.CreateClient();
        var root = await Json(await anon.GetAsync($"/v1/public/users/{username}"));
        var metrics = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));

        // Two organized events exist; only the visible membership's may be counted.
        Assert.Equal(1, root.GetProperty("stats").GetProperty("events_conducted").GetInt32());
        Assert.Equal(1, metrics.GetProperty("events_organized").GetInt32());
        Assert.Equal(1, metrics.GetProperty("organizations").GetInt32());
        Assert.Single(root.GetProperty("organizations").EnumerateArray());
    }

    // ── H2 · hidden is null, never zero ──────────────────────────────────────

    [Fact]
    public async Task Every_hidden_count_is_null_and_never_zero()
    {
        var (client, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await SeedOrganizedEventAsync(db, userId, member: true);
        }

        await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string>
            {
                ["events"] = "only_me",
                ["organizations"] = "only_me",
                ["certificates"] = "only_me",
                ["network"] = "only_me",
                ["achievements"] = "only_me",
            },
        });

        var anon = _factory.CreateClient();
        var metrics = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));
        foreach (var key in new[]
                 {
                     "events_organized", "events_participated", "certificates", "organizations",
                     "verified_organizations", "ally_count", "competitions_entered", "speaker_sessions",
                 })
        {
            Assert.Equal(JsonValueKind.Null, metrics.GetProperty(key).ValueKind);
        }

        var root = await Json(await anon.GetAsync($"/v1/public/users/{username}"));
        var stats = root.GetProperty("stats");
        foreach (var key in new[] { "events_conducted", "certificates_count", "ally_count" })
        {
            Assert.Equal(JsonValueKind.Null, stats.GetProperty(key).ValueKind);
        }
        Assert.Equal(JsonValueKind.Null,
            root.GetProperty("verification").GetProperty("verified_certificates").ValueKind);
    }

    // ── C3 · viewer-aware responses must not be shared-cacheable ─────────────

    /// <summary>Every route under the public-profile group became viewer-dependent in D-221. Without
    /// these headers a CDN or reverse proxy may store one viewer's entitled view and serve it to
    /// another — a disclosure path no amount of correct backend logic prevents.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("/metrics")]
    [InlineData("/experience")]
    [InlineData("/journey")]
    [InlineData("/contributions")]
    [InlineData("/certificates")]
    [InlineData("/timeline")]
    [InlineData("/resume")]
    public async Task Viewer_aware_responses_forbid_shared_caching(string suffix)
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/v1/public/users/{username}{suffix}");

        var cacheControl = res.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.Private, $"{suffix} is shared-cacheable");
        Assert.True(cacheControl.NoStore, $"{suffix} may be written to a cache");
        Assert.Contains("Authorization", res.Headers.Vary);
    }

    // ── C1 parity · an authenticated viewer sees more than an anonymous one ──

    /// <summary>The behaviour the web client could not exercise until D-229, because it sent no token.
    /// This asserts the contract both clients must produce: the same authenticated viewer gets the
    /// same answer, and it differs from the anonymous one.</summary>
    [Fact]
    public async Task An_authenticated_ally_sees_a_connections_tier_section_that_anonymous_cannot()
    {
        var (owner, ownerId) = await LoginAsync();
        var (ally, allyId) = await LoginAsync();
        var username = await ClaimAsync(ownerId);
        await ClaimAsync(allyId);

        var connectionId = (await Json(await ally.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = ownerId })))
            .GetProperty("id").GetGuid();
        await owner.PostAsync($"/v1/allies/requests/{connectionId}/accept", null);

        await owner.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { ["metrics"] = "connections" },
        });

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await anon.GetAsync($"/v1/public/users/{username}/metrics")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ally.GetAsync($"/v1/public/users/{username}/metrics")).StatusCode);
    }

    // ── H3 · every section is user-configurable ──────────────────────────────

    /// <summary>A section with no visibility control is a section published without consent. The
    /// resolver exhaustiveness test did not catch <c>metrics</c> and <c>contributions</c> being absent
    /// from both clients' privacy screens, because it only checked the resolver.</summary>
    [Fact]
    public async Task Every_profile_section_can_be_set_by_the_user()
    {
        var (client, _) = await LoginAsync();

        foreach (var section in Enum.GetValues<ProfileSection>())
        {
            var key = Kurx.Infrastructure.Users.ProfileVisibilityResolver.SectionKey(section);
            var res = await client.PatchAsJsonAsync("/v1/me/privacy", new
            {
                sections = new Dictionary<string, string> { [key] = "only_me" },
            });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var settings = (await Json(res)).GetProperty("sections");
            Assert.Equal("only_me", settings.GetProperty(key).GetString());
        }
    }

    /// <summary>The client-facing section list is served by the API so the two cannot drift — the web
    /// and Flutter privacy screens both build their UI from <see cref="ProfileSection"/>'s keys.</summary>
    [Fact]
    public async Task The_privacy_payload_exposes_every_section_key()
    {
        var (client, _) = await LoginAsync();
        var me = await Json(await client.GetAsync("/v1/me"));
        var sections = me.GetProperty("privacy").GetProperty("sections");

        foreach (var section in Enum.GetValues<ProfileSection>())
        {
            var key = Kurx.Infrastructure.Users.ProfileVisibilityResolver.SectionKey(section);
            Assert.True(sections.TryGetProperty(key, out _), $"section '{key}' is not offered to the user");
        }
    }
}
