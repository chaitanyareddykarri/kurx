using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Admin dashboard summary (D-058): live aggregate counts for the console landing page. Available
/// to any platform staff; a user with no platform role is 403'd. Real HTTP/kurx_test.</summary>
public class AdminDashboardTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public AdminDashboardTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
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

    [Fact]
    public async Task Any_staff_role_sees_the_summary_with_the_expected_shape()
    {
        // Support is the lowest-privilege staff role — proves the summary is not reviewer-only.
        var (client, userId) = await LoginAsync("9940000001");
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
                .GrantAsync(userId, PlatformRole.Support, grantedBy: null);

        var res = await client.GetAsync("/v1/admin/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await Json(res);
        foreach (var key in new[]
        {
            "pending_org_verifications", "pending_membership_claims", "pending_events",
            "blacklist_entries", "staff_count", "new_users_24h", "total_users", "total_orgs", "total_events"
        })
        {
            Assert.True(body.TryGetProperty(key, out var v), $"missing {key}");
            Assert.True(v.GetInt32() >= 0);
        }
        Assert.True(body.GetProperty("staff_count").GetInt32() >= 1);   // at least this Support user
    }

    [Fact]
    public async Task A_user_with_no_platform_role_is_forbidden()
    {
        var (user, _) = await LoginAsync("9940000002");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/v1/admin/dashboard/summary")).StatusCode);
    }

    /// <summary>D-368 — the dashboard tile, the analytics aggregate and the admin registry list must all
    /// mean the same thing by "organization": not soft-deleted, and not a self-representation row.
    ///
    /// <para>The bug this pins: both COUNTs filtered on <c>DeletedAt</c> alone while the list also carried
    /// D-353's <c>!IsPersonal</c>, so a console holding one organization reported three.</para>
    ///
    /// <para>Asserted as a <b>delta</b> across a known seed rather than against absolute totals — the class
    /// shares one database with its siblings, and a fixed expected number would be a test about test
    /// ordering. The cross-surface equality below is absolute, because that one is the actual
    /// contract.</para></summary>
    [Fact]
    public async Task Organization_counts_exclude_self_representation_and_deleted_rows_on_every_surface()
    {
        var (client, userId) = await LoginAsync("9940000003");
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
                // Reviewer, because /v1/admin/orgs is VerificationReviewer-gated while the two aggregates
                // accept any staff role — this test has to reach all three.
                .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);

        var before = await CountsAsync(client);

        var tag = Guid.NewGuid().ToString("N")[..8];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // One real organization — the only one of the four that may be counted.
            db.Organizations.Add(NewOrg($"D368 Real {tag}", isPersonal: false, deleted: false));
            // Two self-representation rows: persistence for a person hosting under their own name (D-268).
            db.Organizations.Add(NewOrg($"D368 Person A {tag}", isPersonal: true, deleted: false));
            db.Organizations.Add(NewOrg($"D368 Person B {tag}", isPersonal: true, deleted: false));
            // A soft-deleted real organization — proves DeletedAt survived the rewrite onto the shared
            // predicate, so a fix for one half cannot quietly drop the other.
            db.Organizations.Add(NewOrg($"D368 Deleted {tag}", isPersonal: false, deleted: true));
            await db.SaveChangesAsync();
        }

        var after = await CountsAsync(client);

        Assert.Equal(1, after.Dashboard - before.Dashboard);
        Assert.Equal(1, after.Analytics - before.Analytics);
        Assert.Equal(1, after.List - before.List);

        // The contract, stated directly: one definition of "organization" across all three surfaces.
        Assert.Equal(after.List, after.Dashboard);
        Assert.Equal(after.List, after.Analytics);

        // And the rows themselves are untouched — this is a read-side rule, never a deletion.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.Equal(2, db.Organizations.Count(o => o.IsPersonal && o.Name.Contains(tag)));
        }
    }

    private static Organization NewOrg(string name, bool isPersonal, bool deleted)
    {
        var org = new Organization
        {
            Name = name,
            // Unique per row: `ix_organizations_slug_active` is UNIQUE WHERE "DeletedAt" IS NULL.
            Slug = $"d368-{Guid.NewGuid():N}"[..24],
            Type = OrganizationType.Other,
            NormalizedName = name.ToLowerInvariant(),
            IsPersonal = isPersonal,
            DeletedAt = deleted ? DateTime.UtcNow : null,
        };
        org.CanonicalOrgId = org.Id;
        return org;
    }

    private static async Task<(int Dashboard, int Analytics, int List)> CountsAsync(HttpClient client)
    {
        var summary = await Json(await client.GetAsync("/v1/admin/dashboard/summary"));
        var analytics = await Json(await client.GetAsync("/v1/admin/analytics"));
        var list = await Json(await client.GetAsync("/v1/admin/orgs?limit=1"));
        return (summary.GetProperty("total_orgs").GetInt32(),
                analytics.GetProperty("total_orgs").GetInt32(),
                // `total` is the unpaginated count, so limit=1 keeps the payload small without
                // changing the number under test.
                list.GetProperty("total").GetInt32());
    }
}
