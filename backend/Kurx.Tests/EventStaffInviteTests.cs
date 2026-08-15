using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-319 — the staff-invite round trip, end to end over HTTP.
///
/// <para>Before this, <c>AssignAsync</c> wrote a row and stopped: no notification, no screen, and
/// <c>RespondAsync</c> refuses everyone except the invitee themselves. Since the §14.2 go-live gate
/// waits on an <c>Accepted</c> assignment, an invite nobody could discover stalled the lifecycle in a
/// way nothing reported. These cases pin the whole chain — invite → notify → list → accept → go live —
/// because each link was individually present and only the chain was broken.</para></summary>
public class EventStaffInviteTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public EventStaffInviteTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Assigning_notifies_the_invitee_and_names_the_event_on_their_own_list()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700019101", "Invite Org");
        var (invitee, inviteeId) = await LoginAsync("9700019102");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);

        var assigned = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = "9700019102", role = "Judge" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        // The notification is the whole point: it is the only thing that tells the invitee to go look.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var note = await db.Notifications.AsNoTracking()
                .SingleAsync(n => n.UserId == inviteeId && n.Kind == NotificationKinds.StaffInvited);
            Assert.Contains("/assignments", note.DataJson);   // the deep-link convention every client follows
        }

        // …and the row it points at has to be identifiable. Role + a bare uuid is not answerable.
        var mine = await Json(await invitee.GetAsync("/v1/me/assignments"));
        var row = mine.EnumerateArray().Single();
        Assert.Equal("invited", row.GetProperty("status").GetString());
        Assert.Equal("Judge", row.GetProperty("role").GetString());
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("event_title").GetString()));
        Assert.Equal("Invite Org", row.GetProperty("representing_org_name").GetString());
        Assert.True(row.GetProperty("event_starts_at").GetDateTime() > DateTime.UtcNow);
    }

    /// <summary>The invitee is the only actor who can accept — not the organiser who invited them, and
    /// not a stranger holding the id. There is deliberately no override path anywhere.</summary>
    [Fact]
    public async Task Only_the_invitee_may_answer_their_own_assignment()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700019111", "Answer Org");
        var (_, _) = await LoginAsync("9700019112");
        var (stranger, _) = await LoginAsync("9700019113");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);

        var assignment = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = "9700019112", role = "Volunteer" }));
        var id = assignment.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync($"/v1/assignments/{id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsync($"/v1/assignments/{id}/accept", null)).StatusCode);
    }

    /// <summary>Answering is one-shot. A declined invite cannot be re-accepted by replaying the call.</summary>
    [Fact]
    public async Task Answering_twice_is_refused()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700019121", "Once Org");
        var (invitee, _) = await LoginAsync("9700019122");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);

        var assignment = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = "9700019122", role = "Host" }));
        var id = assignment.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await invitee.PostAsync($"/v1/assignments/{id}/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await invitee.PostAsync($"/v1/assignments/{id}/accept", null)).StatusCode);
    }

    /// <summary>The reason the chain matters: §14.2's go-live gate is satisfied by acceptance and by
    /// nothing else, so this asserts the refusal AND the release, over HTTP, with no seeded row.</summary>
    [Fact]
    public async Task Accepting_an_invite_is_what_unblocks_go_live()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700019131", "Golive Org");
        var (invitee, _) = await LoginAsync("9700019132");
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);

        var blocked = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "go_live" });
        Assert.Contains("no_staff_assigned", await blocked.Content.ReadAsStringAsync());

        var assignment = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = "9700019132", role = "Stage Manager" }));
        var id = assignment.GetProperty("id").GetGuid();

        // Still blocked while it sits at Invited — an unanswered invite is not staff.
        var stillBlocked = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "go_live" });
        Assert.Contains("no_staff_assigned", await stillBlocked.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await invitee.PostAsync($"/v1/assignments/{id}/accept", null)).StatusCode);

        var live = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "go_live" });
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        // Lowercase: every event response lower-cases status (EventEndpoints.ToEventJson), which is the
        // platform convention and what both clients match on raw — Flutter's lifecycle step and status
        // colour switch on 'live' with no normalisation, so PascalCase here would be the defect.
        Assert.Equal("live", (await Json(live)).GetProperty("status").GetString());
    }

    /// <summary>D-268 — a self-represented event carries no organization identity. The representation row
    /// is named after the person, so emitting it here would surface the internal row on every invite.</summary>
    [Fact]
    public async Task A_self_represented_event_names_no_organization_on_the_invite()
    {
        var (host, _) = await LoginAsync("9700019141");
        var (invitee, _) = await LoginAsync("9700019142");
        var eventId = await CreateSelfRepresentedEventAsync(host);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var orgId = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.RepresentingOrgId).SingleAsync();

        var assigned = await host.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = "9700019142", role = "Photographer" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        var row = (await Json(await invitee.GetAsync("/v1/me/assignments"))).EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, row.GetProperty("representing_org_name").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("event_title").GetString()));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<(HttpClient Client, Guid OrgId, Guid UserId)> LoginOrgAsync(string phone, string name)
    {
        var (client, userId) = await LoginAsync(phone);
        return (client, _factory.SeedVerifiedOrgForClient(client, name), userId);
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug).Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.ParentId!.Value, t.Id);
    }

    private object EventBody((Guid CatId, Guid TypeId) taxon) => new
    {
        title = "Ev " + Guid.NewGuid().ToString("N")[..6],
        description = "a real description",
        categoryId = taxon.CatId,
        typeId = taxon.TypeId,
        venueName = "Main Hall",
        city = "C",
        startsAt = DateTime.UtcNow.AddDays(20),
        endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
    };

    /// <summary>Passing a null representing org exercises the Personal path, which resolves the internal
    /// self-representation row rather than naming an institution.
    ///
    /// <para>D-353 narrowed that path to PRIVATE products, so the taxon moved from `hackathon` (Public) to
    /// `birthday-party` — a real seeded Private Type, not a fixture invented for the test. The subject here
    /// is staff invitations on a self-hosted event; the product axis was incidental and now has to be
    /// stated rather than defaulted.</para></summary>
    private async Task<Guid> CreateSelfRepresentedEventAsync(HttpClient host)
    {
        var res = await host.CreateEventAsync(null, EventBody(await TaxonAsync("birthday-party")));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var id = (await Json(res)).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(id);
        return id;
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId)
    {
        var res = await owner.CreateEventAsync(orgId, EventBody(await TaxonAsync("hackathon")));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var eventId = (await Json(res)).GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);

        var tt = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "GA", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            quantity = 100, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 50, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, tt.StatusCode);
        var ttId = (await Json(tt)).GetProperty("id").GetGuid();

        var pub = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, pub.StatusCode);
        return (eventId, ttId);
    }
}
