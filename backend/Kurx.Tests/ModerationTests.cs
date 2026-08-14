using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Content moderation (C-5, D-059): any user files a report; Moderation staff triage it. Covers
/// create + dedup + validation, the triage authz gate, and resolve/already-closed. Real HTTP/kurx_test.</summary>
public class ModerationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public ModerationTests(KurxApiFactory factory)
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

    private async Task<HttpClient> SupportAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.Support, grantedBy: null);
        return client;
    }

    [Fact]
    public async Task User_files_a_report_then_a_duplicate_of_the_same_subject_is_rejected()
    {
        var (user, _) = await LoginAsync("9930000001");
        var entityId = Guid.NewGuid();

        var res = await user.PostAsJsonAsync("/v1/reports", new { entityType = "event", entityId, reason = "spam" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("open", (await Json(res)).GetProperty("status").GetString());

        var dup = await user.PostAsJsonAsync("/v1/reports", new { entityType = "event", entityId, reason = "spam again" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("already_reported", (await Json(dup)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task An_invalid_entity_type_is_rejected()
    {
        var (user, _) = await LoginAsync("9930000002");
        var res = await user.PostAsJsonAsync("/v1/reports", new { entityType = "spaceship", entityId = Guid.NewGuid(), reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_entity_type", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Moderation_staff_can_list_and_resolve_a_report_once()
    {
        var (reporter, _) = await LoginAsync("9930000003");
        var created = await Json(await reporter.PostAsJsonAsync("/v1/reports",
            new { entityType = "user", entityId = Guid.NewGuid(), reason = "abuse" }));
        var reportId = created.GetProperty("id").GetGuid();

        var support = await SupportAsync("9930000004");
        var list = await Json(await support.GetAsync("/v1/admin/reports?status=open"));
        Assert.Contains(list.EnumerateArray(), e => e.GetProperty("id").GetGuid() == reportId);

        var resolved = await support.PostAsJsonAsync($"/v1/admin/reports/{reportId}/resolve", new { });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal("resolved", (await Json(resolved)).GetProperty("status").GetString());

        // Terminal — a second resolve/dismiss is refused.
        var again = await support.PostAsJsonAsync($"/v1/admin/reports/{reportId}/dismiss", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_closed", (await Json(again)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_plain_user_cannot_triage_reports()
    {
        var (user, _) = await LoginAsync("9930000005");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/v1/admin/reports")).StatusCode);
    }
}
