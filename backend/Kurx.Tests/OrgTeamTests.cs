using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Org team management (D-064): org invitations (A5) + event assignments (A4). Real HTTP/kurx_test.</summary>
public class OrgTeamTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public OrgTeamTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Team Cat", Slug = "team-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return client;
    }

    private Task<Guid> CreateOrgAsync(HttpClient owner)
        => Task.FromResult(_factory.SeedVerifiedOrgForClient(owner, "Team Org " + Guid.NewGuid().ToString("N")[..6], "Company"));

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
        => (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Team Event " + Guid.NewGuid().ToString("N")[..6],
            description = "x", categoryId = _categoryId, venueName = "Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(5), endsAt = DateTime.UtcNow.AddDays(5).AddHours(2),
        }))).GetProperty("id").GetGuid();

    private async Task<string> InviteTokenAsync(Guid orgId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.OrgInvitations.Where(i => i.OrgId == orgId).Select(i => i.Token).FirstAsync();
    }

    [Fact]
    public async Task Owner_invites_a_teammate_who_accepts_and_becomes_a_member()
    {
        var owner = await LoginAsync("9900000001");
        var orgId = await CreateOrgAsync(owner);
        var invitee = await LoginAsync("9900000002");   // exists before the invite

        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/invitations", new { phone = "9900000002", role = "Staff" })).StatusCode);

        var mine = await Json(await invitee.GetAsync("/v1/me/org-invitations"));
        Assert.Contains(mine.EnumerateArray(), i => i.GetProperty("org_id").GetGuid() == orgId);

        var token = await InviteTokenAsync(orgId);
        Assert.Equal(HttpStatusCode.OK, (await invitee.PostAsync($"/v1/org-invitations/{token}/accept", null)).StatusCode);

        var myOrgs = await Json(await invitee.GetAsync("/v1/me/representations"));
        Assert.Contains(myOrgs.EnumerateArray(), o => o.GetProperty("organization_id").GetGuid() == orgId);
    }

    [Fact]
    public async Task A_non_member_cannot_invite_to_an_org()
    {
        var owner = await LoginAsync("9900000003");
        var orgId = await CreateOrgAsync(owner);
        var outsider = await LoginAsync("9900000004");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.PostAsJsonAsync($"/v1/orgs/{orgId}/invitations", new { phone = "9900000009", role = "Staff" })).StatusCode);
    }

    [Fact]
    public async Task Owner_assigns_a_role_and_the_invitee_accepts_it()
    {
        var owner = await LoginAsync("9900000005");
        var orgId = await CreateOrgAsync(owner);
        var eventId = await CreateEventAsync(owner, orgId);
        var helper = await LoginAsync("9900000006");

        var assigned = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = "9900000006", role = "Volunteer" }));
        var assignmentId = assigned.GetProperty("id").GetGuid();
        Assert.Equal("invited", assigned.GetProperty("status").GetString());

        var mine = await Json(await helper.GetAsync("/v1/me/assignments"));
        Assert.Contains(mine.EnumerateArray(), a => a.GetProperty("id").GetGuid() == assignmentId);

        var accepted = await Json(await helper.PostAsync($"/v1/assignments/{assignmentId}/accept", null));
        Assert.Equal("accepted", accepted.GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_invalid_assignment_role_is_rejected()
    {
        var owner = await LoginAsync("9900000007");
        var orgId = await CreateOrgAsync(owner);
        var eventId = await CreateEventAsync(owner, orgId);
        await LoginAsync("9900000008");

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = "9900000008", role = "Wizard" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_role", (await Json(res)).GetProperty("error").GetString());
    }
}
