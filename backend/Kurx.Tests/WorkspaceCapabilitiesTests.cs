using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>The permission-gated organizer workspace is driven entirely by
/// GET /v1/orgs/{orgId}/workspace-capabilities. These assert the contract per role + verification status,
/// so the frontend can render nav/permissions without ever inferring them.</summary>
public class WorkspaceCapabilitiesTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public WorkspaceCapabilitiesTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> UserAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", verify.GetProperty("access_token").GetString());
        return (client, verify.GetProperty("user_id").GetGuid());
    }

    private static string[] Perm(JsonElement caps, string module)
        => caps.GetProperty("permissions").GetProperty(module).EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static string[] Workspaces(JsonElement caps)
        => caps.GetProperty("workspaces").EnumerateArray().Select(w => w.GetProperty("key").GetString()!).ToArray();

    private async Task<JsonElement> CapsFor(Guid userId, HttpClient client, string orgName, OrgRole role, OrgVerificationStatus status)
    {
        var orgId = _factory.SeedVerifiedOrg(userId, orgName, role, status: status);
        var res = await client.GetAsync($"/v1/orgs/{orgId}/workspace-capabilities");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await Json(res);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/v1/orgs/{Guid.NewGuid()}/workspace-capabilities");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_org_returns_404()
    {
        var (client, _) = await UserAsync("9710000001");
        var res = await client.GetAsync($"/v1/orgs/{Guid.NewGuid()}/workspace-capabilities");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Verified_owner_unlocks_full_workspace()
    {
        var (client, uid) = await UserAsync("9710000002");
        var caps = await CapsFor(uid, client, "Owner Verified", OrgRole.Owner, OrgVerificationStatus.Verified);

        var rep = caps.GetProperty("representation");
        Assert.Equal("organization", rep.GetProperty("kind").GetString());
        Assert.True(rep.GetProperty("verified").GetBoolean());
        Assert.Equal("verified", rep.GetProperty("verification_status").GetString());
        Assert.Equal("owner", rep.GetProperty("authority").GetString());
        Assert.Equal("verified_representative", rep.GetProperty("represented_as").GetString());

        var events = Perm(caps, "events");
        Assert.Contains("create", events);
        Assert.Contains("publish", events);
        Assert.Contains("manage", events);
        Assert.Contains("view", Perm(caps, "wallet"));

        var ws = Workspaces(caps);
        Assert.Contains("representing", ws); // user-first model: represent orgs, never "manage organizations"
        Assert.DoesNotContain("organizations", ws);
        Assert.Contains("finance", ws);
        Assert.Contains("community", ws);
        Assert.Contains("reports", ws);

        // Representation is authority to represent + request, never org-account management.
        var representing = Perm(caps, "representing");
        Assert.Contains("view", representing);
        Assert.Contains("request", representing);
        Assert.DoesNotContain("manage", representing);
    }

    [Fact]
    public async Task Unverified_owner_gets_limited_workspace()
    {
        var (client, uid) = await UserAsync("9710000003");
        var caps = await CapsFor(uid, client, "Owner Unverified", OrgRole.Owner, OrgVerificationStatus.Unverified);

        Assert.False(caps.GetProperty("representation").GetProperty("verified").GetBoolean());

        var events = Perm(caps, "events");
        Assert.Contains("create", events);
        Assert.DoesNotContain("publish", events);
        Assert.Empty(Perm(caps, "wallet"));

        var ws = Workspaces(caps);
        Assert.DoesNotContain("finance", ws);
        Assert.DoesNotContain("community", ws);
        Assert.DoesNotContain("reports", ws);
        Assert.Contains("events", ws);
        Assert.Contains("settings", ws);
    }

    [Fact]
    public async Task Manager_can_manage_but_not_finance()
    {
        var (client, uid) = await UserAsync("9710000004");
        var caps = await CapsFor(uid, client, "Manager Org", OrgRole.Manager, OrgVerificationStatus.Verified);

        var events = Perm(caps, "events");
        Assert.Contains("manage", events);
        Assert.Contains("publish", events);
        Assert.Empty(Perm(caps, "wallet"));

        var ws = Workspaces(caps);
        Assert.DoesNotContain("finance", ws);
        Assert.Contains("community", ws);
        Assert.Contains("reports", ws);
    }

    [Fact]
    public async Task Representative_can_create_but_not_manage()
    {
        var (client, uid) = await UserAsync("9710000005");
        var caps = await CapsFor(uid, client, "Rep Org", OrgRole.Representative, OrgVerificationStatus.Verified);

        var events = Perm(caps, "events");
        Assert.Contains("create", events);
        Assert.Contains("publish", events);
        Assert.DoesNotContain("manage", events);
        Assert.Empty(Perm(caps, "wallet"));
        Assert.DoesNotContain("finance", Workspaces(caps));
        Assert.Contains("reports", Workspaces(caps));
    }

    [Fact]
    public async Task Finance_sees_wallet_not_event_authoring()
    {
        var (client, uid) = await UserAsync("9710000006");
        var caps = await CapsFor(uid, client, "Finance Org", OrgRole.Finance, OrgVerificationStatus.Verified);

        var events = Perm(caps, "events");
        Assert.DoesNotContain("create", events);
        Assert.Contains("view", events);
        Assert.Contains("view", Perm(caps, "wallet"));
        Assert.Contains("finance", Workspaces(caps));
        Assert.DoesNotContain("reports", Workspaces(caps)); // reports needs an organizer role
    }

    [Fact]
    public async Task Non_member_gets_view_only_base()
    {
        var (_, ownerId) = await UserAsync("9710000007");
        var orgId = _factory.SeedVerifiedOrg(ownerId, "Someone Elses Org", OrgRole.Owner, status: OrgVerificationStatus.Verified);

        var (outsider, _) = await UserAsync("9710000008");
        var res = await outsider.GetAsync($"/v1/orgs/{orgId}/workspace-capabilities");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var caps = await Json(res);

        Assert.Equal(JsonValueKind.Null, caps.GetProperty("representation").GetProperty("authority").ValueKind);
        Assert.Equal("none", caps.GetProperty("representation").GetProperty("represented_as").GetString());
        Assert.Equal(new[] { "view" }, Perm(caps, "events"));
        Assert.DoesNotContain("finance", Workspaces(caps));
        Assert.DoesNotContain("community", Workspaces(caps));
    }

    /// <summary>D-289 — a host running their own event. The verified-representative gate is vacuous
    /// on a personal representation (there is no organization to verify), and applying it anyway made
    /// the matrix answer <c>tickets: ["view"]</c> to the event's own creator, so the web client hid
    /// the create form and a personally-hosted event could never be sold or registered for.
    ///
    /// <para>The matrix was the only thing refusing: <c>IEventAuthority</c> resolves ownership first
    /// and on its own (D-268), so the write endpoints were accepting it the whole time. The last
    /// assertion is the one that matters — advertised and enforced must agree.</para></summary>
    [Fact]
    public async Task A_creator_can_run_their_own_event_from_the_workspace()
    {
        var (client, _) = await UserAsync("9710000009");

        Guid categoryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Personal Cat", Slug = "personal-cat" };
            db.EventCategories.Add(cat);
            db.SaveChanges();
            categoryId = cat.Id;
        }

        // No representingOrgId — the Personal path, where the backend resolves the caller's own org.
        // D-353 narrowed that path to PRIVATE products, so the Type is now stated rather than defaulted.
        Guid privateTypeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            privateTypeId = await db.EventCategories
                .Where(c => c.Level == CategoryLevel.Type && c.ProductClass == EventProduct.Private)
                .Select(c => c.Id).FirstAsync();
        }

        // D-379 — the subject is what the WORKSPACE offers an event's creator, which is unchanged. It
        // reached that state via the personal path; it now reaches it under a real organization.
        var workspaceOrgId = _factory.SeedVerifiedOrgForClient(client, "Workspace Org " + Guid.NewGuid().ToString("N")[..6]);
        var created = await Json(await client.CreateEventAsync(workspaceOrgId, new
        {
            title = "My Own Workshop",
            typeId = privateTypeId,
            description = "An event with plenty of detail for validation.",
            categoryId,
            venueName = "Main Hall",
            city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20),
            endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }));
        var eventId = created.GetProperty("id").GetGuid();
        var representedOrgId = created.GetProperty("representing_org_id").GetGuid();

        var res = await client.GetAsync($"/v1/orgs/{representedOrgId}/workspace-capabilities");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var caps = await Json(res);

        var rep = caps.GetProperty("representation");
        // D-379 — the creator still owns the event; it simply represents a real organization now.
        Assert.Equal("organization", rep.GetProperty("kind").GetString());
        // D-379 — the creator now holds a real seat in the organization they represent, so `authority`
        // is populated. The point the original assertion protected still stands and is asserted below:
        // the client reads `permissions`, never inferring capability from the role string.
        Assert.False(string.IsNullOrWhiteSpace(rep.GetProperty("authority").GetString()));

        Assert.Contains("create", Perm(caps, "tickets"));
        Assert.Contains("update", Perm(caps, "tickets"));
        Assert.Contains("delete", Perm(caps, "tickets"));
        Assert.Contains("create", Perm(caps, "announcements"));
        Assert.Contains("manage", Perm(caps, "attendees"));

        /*
         * The paid-event gate still hangs off the USER's own trust level, not off representing an
         * organization — that is the half worth keeping and it is unchanged.
         *
         * The wallet assertion is retired with the path it described. It read `Assert.Empty` because a
         * self-representation row carried no seat worth anything; the creator now holds a real Owner seat
         * in a real organization, so wallet permissions follow from that seat. Asserting emptiness here
         * would be asserting that D-379 did not happen.
         */
        Assert.False(caps.GetProperty("trust").GetProperty("can_host_paid_events").GetBoolean());

        // The point of the whole fix: what the matrix advertises is what the endpoint accepts.
        var ticketType = await client.PostAsJsonAsync(
            $"/v1/orgs/{representedOrgId}/events/{eventId}/ticket-types",
            new
            {
                name = "General",
                pricePaise = 0,
                quantity = 50,
                registrationMode = "Individual",
                pricingUnit = "perticket",
                perUserLimit = 1,
                saleStarts = DateTime.UtcNow.AddDays(1),
                saleEnds = DateTime.UtcNow.AddDays(19),
            });
        Assert.Equal(HttpStatusCode.OK, ticketType.StatusCode);
    }
}
