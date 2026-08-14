using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>User administration + account moderation (D-060): suspend/ban blocks login AND refresh; unban
/// restores; guards (self, SuperAdmin) hold; triage is Moderation-only. Each phone gets ≤2 OTP requests to
/// stay under the D-005 per-phone cap. Real HTTP/kurx_test.</summary>
public class UserModerationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public UserModerationTests(KurxApiFactory factory)
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

    private async Task<(HttpClient Client, Guid UserId, string Refresh)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (client, t.GetProperty("user_id").GetGuid(), t.GetProperty("refresh_token").GetString()!);
    }

    // A fresh login attempt (new OTP) — returns the raw verify response so the caller can assert the block.
    private async Task<HttpResponseMessage> TryLoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        return await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
    }

    private async Task<(HttpClient Client, Guid UserId)> ModeratorAsync(string phone)
    {
        var (client, userId, _) = await LoginAsync(phone);
        await GrantAsync(userId, PlatformRole.Support);   // Support is Moderation staff
        return (client, userId);
    }

    private async Task GrantAsync(Guid userId, PlatformRole role)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>().GrantAsync(userId, role, grantedBy: null);
    }

    [Fact]
    public async Task Suspending_blocks_fresh_login_and_unban_clears_the_hold()
    {
        var (_, targetId, _) = await LoginAsync("9920000001");     // OTP req #1 for this phone
        var (mod, _) = await ModeratorAsync("9920000002");

        var susp = await Json(await mod.PostAsJsonAsync($"/v1/admin/users/{targetId}/suspend", new { reason = "abuse" }));
        Assert.True(susp.GetProperty("suspended").GetBoolean());

        // Fresh login is now refused with the moderation code.
        var blocked = await TryLoginAsync("9920000001");           // OTP req #2
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        Assert.Equal("account_suspended", (await Json(blocked)).GetProperty("error").GetString());

        // Unban clears both holds (login gate reads these flags).
        var cleared = await Json(await mod.PostAsJsonAsync($"/v1/admin/users/{targetId}/unban", new { }));
        Assert.False(cleared.GetProperty("suspended").GetBoolean());
        Assert.False(cleared.GetProperty("banned").GetBoolean());
    }

    [Fact]
    public async Task A_suspended_account_cannot_refresh_its_session()
    {
        var (_, targetId, refresh) = await LoginAsync("9920000003");   // OTP req #1

        // Set the hold directly (bypassing the endpoint's token-revoke) to exercise the RefreshAsync guard
        // against a still-active refresh token.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var u = await db.Users.FirstAsync(x => x.Id == targetId);
            u.SuspendedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var res = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("account_suspended", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Banning_blocks_login_with_the_banned_code()
    {
        var (_, targetId, _) = await LoginAsync("9920000004");     // OTP req #1
        var (mod, _) = await ModeratorAsync("9920000005");

        await mod.PostAsJsonAsync($"/v1/admin/users/{targetId}/ban", new { reason = "fraud" });

        var blocked = await TryLoginAsync("9920000004");           // OTP req #2
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        Assert.Equal("account_banned", (await Json(blocked)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_non_moderator_cannot_manage_users()
    {
        var (user, _, _) = await LoginAsync("9920000006");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task A_superadmin_cannot_be_moderated()
    {
        var (mod, _) = await ModeratorAsync("9920000007");
        var (_, targetId, _) = await LoginAsync("9920000008");
        await GrantAsync(targetId, PlatformRole.SuperAdmin);

        var res = await mod.PostAsJsonAsync($"/v1/admin/users/{targetId}/ban", new { });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("cannot_moderate_superadmin", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_moderator_cannot_moderate_themselves()
    {
        var (mod, modId) = await ModeratorAsync("9920000009");
        var res = await mod.PostAsJsonAsync($"/v1/admin/users/{modId}/suspend", new { });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("cannot_moderate_self", (await Json(res)).GetProperty("error").GetString());
    }
}
