using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Admin verification console (M12, D-051): org merge (fresh duplicates only), blacklist, and
/// the cross-subject verification audit trail — gated by the VerificationReviewer platform role.
/// Real HTTP/kurx_test. (The admin/ Next.js UI is Pending Stitch UI.)</summary>
public class AdminConsoleTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public AdminConsoleTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var c = new EventCategory { Level = CategoryLevel.Category, Name = "Admin Cat", Slug = "admin-cat" };
                db.EventCategories.Add(c);
                db.SaveChanges();
                _categoryId = c.Id;
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

    // D-075: institutions are no longer self-minted; seed the post-approval end state directly.
    private Task<Guid> CreateOrgAsync(HttpClient client, string name)
        => Task.FromResult(_factory.SeedVerifiedOrgForClient(client, name));

    [Fact]
    public async Task Merge_fresh_duplicate_repoints_members_and_records_an_alias()
    {
        var reviewer = await ReviewerAsync("9970000001");
        var (owner1, _) = await LoginAsync("9970000002");
        var canonicalId = await CreateOrgAsync(owner1, "Canonical Tech University");
        var (owner2, owner2Id) = await LoginAsync("9970000003");
        var dupId = await CreateOrgAsync(owner2, "Duplicate Tech Institute");

        var res = await reviewer.PostAsJsonAsync("/v1/admin/orgs/merge", new { duplicateOrgId = dupId, canonicalOrgId = canonicalId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var dup = await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == dupId);
            Assert.NotNull(dup.DeletedAt);
            Assert.Equal(canonicalId, dup.CanonicalOrgId);
            // The duplicate's owner is now a member of the canonical org.
            Assert.True(await db.Memberships.AnyAsync(m => m.OrgId == canonicalId && m.UserId == owner2Id));
        }

        // The duplicate's name now resolves to the canonical org (alias).
        var search = await Json(await owner1.GetAsync("/v1/orgs/search?q=Duplicate Tech Institute"));
        Assert.Equal(canonicalId, search.EnumerateArray().First().GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Merge_is_blocked_when_the_duplicate_has_events()
    {
        var reviewer = await ReviewerAsync("9970000004");
        var (owner1, _) = await LoginAsync("9970000005");
        var canonicalId = await CreateOrgAsync(owner1, "Keeper Org");
        var (owner2, _) = await LoginAsync("9970000006");
        var dupId = await CreateOrgAsync(owner2, "Has Events Org");
        await owner2.CreateEventAsync(dupId, new
        {
            title = "Some Event", description = "d", categoryId = _categoryId, venueName = "H", city = "V",
            startsAt = DateTime.UtcNow.AddDays(5), endsAt = DateTime.UtcNow.AddDays(5).AddHours(2),
        });

        var res = await reviewer.PostAsJsonAsync("/v1/admin/orgs/merge", new { duplicateOrgId = dupId, canonicalOrgId = canonicalId });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("cannot_merge_has_events", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Reviewer_can_blacklist_an_org()
    {
        var reviewer = await ReviewerAsync("9970000007");
        var (owner, _) = await LoginAsync("9970000008");
        var orgId = await CreateOrgAsync(owner, "Impersonator Org");
        var res = await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/blacklist", new { reason = "impersonation" });
        Assert.Equal("blacklisted", (await Json(res)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Verification_history_returns_the_review_trail()
    {
        var reviewer = await ReviewerAsync("9970000009");
        var (owner, _) = await LoginAsync("9970000010");
        // D-075: seed an *Unverified* org so the real submit → review flow below writes the audit trail.
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Audited Org", status: OrgVerificationStatus.Unverified);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/verification/submit",
            new { documents = new[] { new { docType = "registration_cert", storageKey = "k" } } });
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review", new { decision = "approve", reasonCode = "ok" });

        var h = await Json(await reviewer.GetAsync($"/v1/admin/verifications/organization/{orgId}/history"));
        Assert.Equal("organization", h.GetProperty("subject_type").GetString());
        var reviews = h.GetProperty("reviews").EnumerateArray().ToList();
        Assert.Contains(reviews, r => r.GetProperty("decision").GetString() == "approve");
        Assert.Single(h.GetProperty("documents").EnumerateArray());
    }

    [Fact]
    public async Task Non_reviewer_cannot_merge_or_view_history()
    {
        var (user, _) = await LoginAsync("9970000011");
        var merge = await user.PostAsJsonAsync("/v1/admin/orgs/merge", new { duplicateOrgId = Guid.NewGuid(), canonicalOrgId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, merge.StatusCode);
        var history = await user.GetAsync($"/v1/admin/verifications/organization/{Guid.NewGuid()}/history");
        Assert.Equal(HttpStatusCode.Forbidden, history.StatusCode);
    }
}
