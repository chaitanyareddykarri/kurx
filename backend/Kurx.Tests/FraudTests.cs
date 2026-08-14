using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Fraud prevention (M13, D-052): a hard blocklist + risk score. The trust layer reads it live,
/// so a blacklisted or high-risk user loses CanOrganizePaid (which cascades into the event-publish and
/// payment gates). Blacklisted org names can't be created. Real HTTP/kurx_test.</summary>
public class FraudTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public FraudTests(KurxApiFactory factory)
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

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
    }

    private static async Task MakePaidCapable(HttpClient client)
    {
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "User" });
        await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "User" });
    }

    private static async Task<bool> CanOrganizePaid(HttpClient client)
        => (await Json(await client.GetAsync("/v1/me"))).GetProperty("trust").GetProperty("can_organize_paid").GetBoolean();

    [Fact]
    public async Task Blacklisting_the_users_phone_removes_paid_organizing()
    {
        var reviewer = await ReviewerAsync("9980000001");
        var (user, _) = await LoginAsync("9980000002");
        await MakePaidCapable(user);
        Assert.True(await CanOrganizePaid(user));   // baseline: identity + bank verified

        await reviewer.PostAsJsonAsync("/v1/admin/blacklist", new { kind = "phone", value = "9980000002" });

        Assert.False(await CanOrganizePaid(user));  // blacklist is read live
    }

    [Fact]
    public async Task A_high_risk_score_removes_paid_organizing()
    {
        var reviewer = await ReviewerAsync("9980000003");
        var (user, userId) = await LoginAsync("9980000004");
        await MakePaidCapable(user);
        Assert.True(await CanOrganizePaid(user));

        await reviewer.PostAsJsonAsync("/v1/admin/fraud-signals",
            new { subjectType = "useridentity", subjectId = userId, kind = "manual", score = 150 });

        Assert.False(await CanOrganizePaid(user));  // risk >= threshold
    }

    [Fact]
    public async Task A_blacklisted_org_name_cannot_be_created()
    {
        var reviewer = await ReviewerAsync("9980000005");
        await reviewer.PostAsJsonAsync("/v1/admin/blacklist", new { kind = "orgname", value = "Fake IIT Bombay", reason = "impersonation" });

        var (user, _) = await LoginAsync("9980000006");
        // Different casing normalizes to the same blocked name — blocked at representation-request time (D-075).
        var res = await user.PostAsJsonAsync("/v1/orgs/representation-requests", new
        {
            name = "fake iit bombay", type = "College",
            documents = new[] { new { docType = "registration_cert", storageKey = "k" } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("org_blacklisted", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Admin_can_add_list_and_remove_blacklist_entries()
    {
        var reviewer = await ReviewerAsync("9980000007");
        var added = await Json(await reviewer.PostAsJsonAsync("/v1/admin/blacklist", new { kind = "email", value = "Bad@Example.com" }));
        var id = added.GetProperty("id").GetGuid();
        Assert.Equal("bad@example.com", added.GetProperty("value").GetString());   // normalized

        var list = await Json(await reviewer.GetAsync("/v1/admin/blacklist"));
        Assert.Contains(list.EnumerateArray(), e => e.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.OK, (await reviewer.DeleteAsync($"/v1/admin/blacklist/{id}")).StatusCode);
    }

    [Fact]
    public async Task Non_reviewer_cannot_manage_the_blacklist()
    {
        var (user, _) = await LoginAsync("9980000008");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.PostAsJsonAsync("/v1/admin/blacklist", new { kind = "phone", value = "9999999999" })).StatusCode);
    }
}
