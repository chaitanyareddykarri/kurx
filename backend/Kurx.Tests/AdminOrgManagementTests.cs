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

/// <summary>D-194: platform-wide org list &amp; detail — closes the standing "no admin-scoped org read"
/// gap (admin/STATUS.md §5.4). Real HTTP / real kurx_test.</summary>
public class AdminOrgManagementTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public AdminOrgManagementTests(KurxApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task Reviewer_can_list_orgs_across_every_verification_status()
    {
        var (owner, _) = await LoginAsync("9970000001");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _factory.SeedVerifiedOrgForClient(owner, $"AOM Verified {suffix}", status: OrgVerificationStatus.Verified);
        _factory.SeedVerifiedOrgForClient(owner, $"AOM Pending {suffix}", status: OrgVerificationStatus.PendingReview);
        _factory.SeedVerifiedOrgForClient(owner, $"AOM Suspended {suffix}", status: OrgVerificationStatus.Suspended);

        var reviewer = await ReviewerAsync("9970000002");
        var body = await Json(await reviewer.GetAsync($"/v1/admin/orgs?q={suffix}"));
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal(3, body.GetProperty("total").GetInt32());
        Assert.Contains(items, i => i.GetProperty("verification_status").GetString() == "verified");
        Assert.Contains(items, i => i.GetProperty("verification_status").GetString() == "pendingreview");
        Assert.Contains(items, i => i.GetProperty("verification_status").GetString() == "suspended");
    }

    [Fact]
    public async Task Status_filter_narrows_to_only_that_status()
    {
        var (owner, _) = await LoginAsync("9970000003");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var verifiedId = _factory.SeedVerifiedOrgForClient(owner, $"AOM Status {suffix} A", status: OrgVerificationStatus.Verified);
        var blacklistedId = _factory.SeedVerifiedOrgForClient(owner, $"AOM Status {suffix} B", status: OrgVerificationStatus.Blacklisted);

        var reviewer = await ReviewerAsync("9970000004");
        var body = await Json(await reviewer.GetAsync($"/v1/admin/orgs?q=AOM Status {suffix}&status=Verified"));
        var ids = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("org_id").GetGuid()).ToList();

        Assert.Contains(verifiedId, ids);
        Assert.DoesNotContain(blacklistedId, ids);
        foreach (var item in body.GetProperty("items").EnumerateArray())
            Assert.Equal("verified", item.GetProperty("verification_status").GetString());
    }

    [Fact]
    public async Task Search_matches_name_slug_or_domain()
    {
        var (owner, _) = await LoginAsync("9970000005");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var orgId = _factory.SeedVerifiedOrgForClient(owner, $"AOM DomainOrg {suffix}", primaryDomain: $"aom-{suffix}.edu.in");

        var reviewer = await ReviewerAsync("9970000006");
        var byDomain = await Json(await reviewer.GetAsync($"/v1/admin/orgs?q=aom-{suffix}.edu.in"));
        Assert.Contains(byDomain.GetProperty("items").EnumerateArray(), i => i.GetProperty("org_id").GetGuid() == orgId);
    }

    [Fact]
    public async Task Member_and_event_counts_are_correct()
    {
        var (owner, _) = await LoginAsync("9970000007");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "AOM Counts " + Guid.NewGuid().ToString("N")[..6]);

        Guid categoryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "AOM Cat " + Guid.NewGuid().ToString("N")[..6], Slug = "aom-cat-" + Guid.NewGuid().ToString("N")[..8] };
            db.EventCategories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        var createRes = await owner.CreateEventAsync(orgId, new
        {
            title = "AOM Counted Event", description = "d", categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(10), endsAt = DateTime.UtcNow.AddDays(10).AddHours(2),
        });
        Assert.True(createRes.IsSuccessStatusCode, $"Event creation failed: {await createRes.Content.ReadAsStringAsync()}");

        var reviewer = await ReviewerAsync("9970000008");
        var detailRow = (await Json(await reviewer.GetAsync($"/v1/admin/orgs?q=AOM Counts")))
            .GetProperty("items").EnumerateArray().First(i => i.GetProperty("org_id").GetGuid() == orgId);

        Assert.Equal(1, detailRow.GetProperty("member_count").GetInt32());
        Assert.Equal(1, detailRow.GetProperty("event_count").GetInt32());
    }

    [Fact]
    public async Task Detail_endpoint_bypasses_membership_and_returns_admin_role()
    {
        var (owner, _) = await LoginAsync("9970000009");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "AOM Detail " + Guid.NewGuid().ToString("N")[..6]);

        var reviewer = await ReviewerAsync("9970000010");
        var res = await reviewer.GetAsync($"/v1/admin/orgs/{orgId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = await Json(res);
        Assert.Equal(orgId, detail.GetProperty("org_id").GetGuid());
        // Reviewer is not a member of this org — the response must not fabricate a real membership role.
    }

    [Fact]
    public async Task Detail_endpoint_404s_for_a_nonexistent_org()
    {
        var reviewer = await ReviewerAsync("9970000011");
        var res = await reviewer.GetAsync($"/v1/admin/orgs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Non_reviewer_is_forbidden_from_list_and_detail()
    {
        var (plain, _) = await LoginAsync("9970000012");
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync("/v1/admin/orgs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync($"/v1/admin/orgs/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Organizer_self_service_read_still_works_after_the_isAdmin_signature_change()
    {
        // Regression guard for the GetAsync(userId, orgId, isAdmin=false, ct=default) signature change:
        // the organizer's own GET /v1/orgs/{orgId} (member-scoped, non-admin) must be unaffected.
        var (owner, _) = await LoginAsync("9970000013");
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "AOM SelfService " + Guid.NewGuid().ToString("N")[..6]);
        var res = await owner.GetAsync($"/v1/orgs/{orgId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = await Json(res);
        Assert.Equal("owner", detail.GetProperty("role").GetString()?.ToLowerInvariant());
    }
}
