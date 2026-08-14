using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
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
}
