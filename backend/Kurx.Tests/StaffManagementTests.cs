using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Staff &amp; role management (admin console, D-056): SuperAdmin-only grant/revoke of platform
/// roles over the live <c>platform_roles</c> table (M2/D-040). Covers authz, the happy path, the
/// not-found / invalid-role branches, and the last-SuperAdmin lockout guard. Real HTTP / kurx_test.</summary>
public class StaffManagementTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public StaffManagementTests(KurxApiFactory factory)
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

    private async Task<(HttpClient Client, Guid UserId, string Phone)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid(), phone);
    }

    private async Task GrantAsync(Guid userId, PlatformRole role)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>().GrantAsync(userId, role, grantedBy: null);
    }

    private async Task<(HttpClient Client, Guid UserId)> SuperAdminAsync(string phone)
    {
        var (client, userId, _) = await LoginAsync(phone);
        await GrantAsync(userId, PlatformRole.SuperAdmin);
        return (client, userId);
    }

    private static IEnumerable<string?> Roles(JsonElement el) =>
        el.GetProperty("roles").EnumerateArray().Select(r => r.GetString());

    [Fact]
    public async Task SuperAdmin_grants_a_role_by_phone_and_it_shows_in_the_list_and_on_me()
    {
        var (admin, _) = await SuperAdminAsync("9970000001");
        var (target, targetId, targetPhone) = await LoginAsync("9970000002");

        var granted = await Json(await admin.PostAsJsonAsync("/v1/admin/staff/grant",
            new { phone = targetPhone, role = "Support" }));
        Assert.Equal(targetId, granted.GetProperty("user_id").GetGuid());
        Assert.Contains("Support", Roles(granted));

        var list = await Json(await admin.GetAsync("/v1/admin/staff"));
        Assert.Contains(list.EnumerateArray(), e => e.GetProperty("user_id").GetGuid() == targetId);

        // The target's own /v1/me now carries the role — the RBAC source the admin console derives from.
        var me = await Json(await target.GetAsync("/v1/me"));
        Assert.Contains("Support", me.GetProperty("platform_roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Granting_to_an_unregistered_phone_is_404()
    {
        var (admin, _) = await SuperAdminAsync("9970000003");
        var res = await admin.PostAsJsonAsync("/v1/admin/staff/grant", new { phone = "9970009999", role = "Support" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("user_not_found", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task An_invalid_role_is_rejected()
    {
        var (admin, _) = await SuperAdminAsync("9970000004");
        var (_, _, phone) = await LoginAsync("9970000005");
        var res = await admin.PostAsJsonAsync("/v1/admin/staff/grant", new { phone, role = "Wizard" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_role", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_granted_role_can_be_revoked()
    {
        var (admin, _) = await SuperAdminAsync("9970000006");
        var (target, targetId, phone) = await LoginAsync("9970000007");
        await admin.PostAsJsonAsync("/v1/admin/staff/grant", new { phone, role = "FinanceOps" });

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/v1/admin/staff/{targetId}/roles/FinanceOps")).StatusCode);

        var me = await Json(await target.GetAsync("/v1/me"));
        Assert.DoesNotContain("FinanceOps", me.GetProperty("platform_roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task A_reviewer_is_staff_but_cannot_manage_staff()
    {
        // A VerificationReviewer holds a platform role (is "staff") but the staff console is SuperAdmin-only.
        var (client, userId, _) = await LoginAsync("9970000008");
        await GrantAsync(userId, PlatformRole.VerificationReviewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/v1/admin/staff")).StatusCode);
    }

    [Fact]
    public async Task A_plain_user_cannot_manage_staff()
    {
        var (user, _, _) = await LoginAsync("9970000009");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/v1/admin/staff")).StatusCode);
    }

    [Fact]
    public async Task The_last_superadmin_cannot_be_revoked()
    {
        var (admin, adminId) = await SuperAdminAsync("9970000010");

        // Force "exactly one SuperAdmin" deterministically regardless of what other tests created:
        // revoke every *other* SuperAdmin (allowed — the caller keeps the role throughout).
        var supers = await Json(await admin.GetAsync("/v1/admin/staff?role=SuperAdmin"));
        foreach (var s in supers.EnumerateArray())
        {
            var id = s.GetProperty("user_id").GetGuid();
            if (id != adminId) await admin.DeleteAsync($"/v1/admin/staff/{id}/roles/SuperAdmin");
        }

        // Caller is now the sole SuperAdmin — revoking it is blocked (no total lockout).
        var res = await admin.DeleteAsync($"/v1/admin/staff/{adminId}/roles/SuperAdmin");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("cannot_revoke_last_superadmin", (await Json(res)).GetProperty("error").GetString());
    }
}
