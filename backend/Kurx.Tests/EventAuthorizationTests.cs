using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>The authorization contract for every event-management surface, per authority level (D-269).
///
/// <para><b>Why this exists.</b> Eleven services each carried their own <c>CanManage</c>/<c>RoleAsync</c>
/// pair or an inline membership query. Seven were byte-identical; the rest had drifted, and nothing
/// compared them, so the drift was invisible. This file is the comparison: one matrix of caller × surface,
/// written against the behaviour that shipped, so consolidating onto <c>IEventAuthority</c> cannot quietly
/// widen or narrow a permission.</para>
///
/// <para>Every case goes over real HTTP against real Postgres — the authorization that matters is the one
/// the endpoint actually applies, not the one the service method claims.</para></summary>
public class EventAuthorizationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static Guid _categoryId;
    private static Guid _typeId;

    public EventAuthorizationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Auth Cat", Slug = "auth-cat" };
            db.EventCategories.Add(cat);
            db.SaveChanges();
            _categoryId = cat.Id;

            // A PRIVATE type, deliberately. This class is about who may act on an event, and it creates
            // self-represented ones — which D-353 confines to private products, since a public event must
            // name a real institution. An untyped event resolves to Public, so leaving the type off would
            // have every event here refused at creation for a reason that has nothing to do with authority.
            var type = new EventCategory
            {
                Level = CategoryLevel.Type, Name = "Auth Type", Slug = "auth-type",
                ParentId = cat.Id, ProductClass = EventProduct.Private,
            };
            db.EventCategories.Add(type);
            db.SaveChanges();
            _typeId = type.Id;
            _reset = true;
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

    private static object EventBody(string title) => new
    {
        title,
        description = "An event with plenty of detail for validation.",
        categoryId = _categoryId,
        typeId = _typeId,
        venueName = "Main Hall",
        city = "Vizag",
        startsAt = DateTime.UtcNow.AddDays(20),
        endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
    };

    /// <summary>Adds a seat in the event's own organization for another user — how a collaborator,
    /// staff member or representative comes to hold authority over someone else's event.</summary>
    private void SeatIn(Guid orgId, Guid userId, OrgRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.Memberships.Add(new Membership { OrgId = orgId, UserId = userId, Role = role, IsVerified = true });
        db.SaveChanges();
    }

    /// <summary>An event owned by <paramref name="phone"/>'s user and representing a verified org, which is
    /// the shape every collaborator case needs.</summary>
    private async Task<(HttpClient Owner, Guid OwnerId, Guid OrgId, Guid EventId)> HostedEventAsync(string phone, string orgName)
    {
        var (owner, ownerId) = await UserAsync(phone);
        var orgId = _factory.SeedVerifiedOrg(ownerId, orgName);
        var created = await Json(await owner.PostAsJsonAsync("/v1/events",
            new { title = $"{orgName} Summit", description = "An event with plenty of detail for validation.",
                  categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
                  startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
                  representingOrgId = orgId }));
        return (owner, ownerId, orgId, created.GetProperty("id").GetGuid());
    }

    /// <summary>A valid free ticket type. Spelled out because the endpoint validates every field — a
    /// half-filled body returns 400 and would mask the 200/403 this file is actually asserting.</summary>
    private static object TicketBody(string name, int quantity) => new
    {
        name,
        pricePaise = 0L,
        pricingUnit = nameof(PricingUnit.PerTicket),
        registrationMode = nameof(RegistrationMode.Individual),
        quantity,
        saleStarts = DateTime.UtcNow.AddDays(-1),
        saleEnds = DateTime.UtcNow.AddDays(19),
        perUserLimit = 1,
        isAllAccess = false,
    };

    // Every event-management surface, addressed exactly as a client does. `{o}`/`{e}` are substituted;
    // the org segment is a path artefact (D-268) — authorization never reads it.
    private static string Url(string template, Guid orgId, Guid eventId)
        => template.Replace("{o}", orgId.ToString()).Replace("{e}", eventId.ToString());

    private static readonly string[] ManageContentReads =
    [
        "/v1/orgs/{o}/events/{e}/ticket-types",
        "/v1/orgs/{o}/events/{e}/sessions",
        "/v1/orgs/{o}/events/{e}/speakers",
        "/v1/orgs/{o}/events/{e}/sponsors",
    ];

    // ── The owner (event creator) ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creator_manages_every_surface_of_their_own_event()
    {
        /*
         * D-379 — the event represents a real organization, and the creator's membership is deleted below
         * so the subject is unchanged and sharper: authority resolves CREATOR-FIRST (D-269), with no
         * organization seat backing it. It used to reach that state via personal representation, which no
         * longer exists.
         */
        var (owner, ownerId) = await UserAsync("9970000001");
        var ownOrgId = _factory.SeedVerifiedOrgForClient(owner, "Outright Org " + Guid.NewGuid().ToString("N")[..8]);
        var created = await Json(await owner.PostAsJsonAsync("/v1/events",
            new
            {
                representingOrgId = ownOrgId,
                title = "Owned Outright",
                description = "An event with plenty of detail for validation.",
                categoryId = _categoryId, typeId = _typeId,
                venueName = "Main Hall", city = "Vizag",
                startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
            }));
        var eventId = created.GetProperty("id").GetGuid();
        var orgId = created.GetProperty("org_id").GetGuid();

        /*
         * The membership is deliberately LEFT IN PLACE, and the old comment claiming "no organization seat
         * exists anywhere for this person" was wrong for this test's whole life: the retired
         * `ResolveSelfRepresentationAsync` seeded an `OrgRole.Owner` row alongside every self-representation,
         * so the caller always had a seat and these org-scoped routes passed because of it.
         *
         * Deleting it (which D-379 briefly did here) tests a state this flow never produced, and these
         * routes answer Forbidden for it. Creator-first authority with no seat (D-269) is real and is
         * covered where it belongs — `EventAudienceAuthorizationTests`.
         */
        foreach (var template in ManageContentReads)
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(Url(template, orgId, eventId))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/attendees")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/payment-readiness")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/workspace")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/v1/events/{eventId}/announcements")).StatusCode);

        // ...and writes, not just reads.
        var ticket = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types",
            TicketBody("General", 50));
        Assert.Equal(HttpStatusCode.OK, ticket.StatusCode);

        var renamed = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { title = "Owned Outright II" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
    }

    // ── Representation authority (the D-075 organizer role) ──────────────────────────────────────

    [Fact]
    public async Task Representative_manages_content_on_an_event_they_did_not_create()
    {
        var (_, _, orgId, eventId) = await HostedEventAsync("9970000002", "Rep Institute");
        var (rep, repId) = await UserAsync("9970000003");
        SeatIn(orgId, repId, OrgRole.Representative);

        // D-269 fix: Representative is manage-capable by D-075's own definition, but nine sub-resources
        // omitted it — so the platform's primary organizer role could publish an event and then not add a
        // ticket type to it.
        foreach (var template in ManageContentReads)
            Assert.Equal(HttpStatusCode.OK, (await rep.GetAsync(Url(template, orgId, eventId))).StatusCode);

        var ticket = await rep.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types",
            TicketBody("Rep Tier", 10));
        Assert.Equal(HttpStatusCode.OK, ticket.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await rep.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics")).StatusCode);
    }

    // ── Organization Manager (collaborator) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Org_manager_manages_content_on_a_colleagues_event()
    {
        var (_, _, orgId, eventId) = await HostedEventAsync("9970000004", "Manager College");
        var (manager, managerId) = await UserAsync("9970000005");
        SeatIn(orgId, managerId, OrgRole.Manager);

        foreach (var template in ManageContentReads)
            Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(Url(template, orgId, eventId))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/attendees")).StatusCode);
        var ticket = await manager.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types",
            TicketBody("Manager Tier", 10));
        Assert.Equal(HttpStatusCode.OK, ticket.StatusCode);
    }

    // ── Staff: the roster, and nothing that changes what is sold ─────────────────────────────────

    [Fact]
    public async Task Staff_read_the_attendee_roster_but_cannot_manage_content()
    {
        var (_, _, orgId, eventId) = await HostedEventAsync("9970000006", "Staff Academy");
        var (staff, staffId) = await UserAsync("9970000007");
        SeatIn(orgId, staffId, OrgRole.Staff);

        // Front-of-house needs the list...
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/attendees")).StatusCode);

        // ...and nothing that changes the event. Preserved exactly: Staff was never manage-capable.
        var ticket = await staff.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types",
            TicketBody("Nope", 5));
        Assert.Equal(HttpStatusCode.Forbidden, ticket.StatusCode);

        var renamed = await staff.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { title = "Hijacked" });
        Assert.Equal(HttpStatusCode.Forbidden, renamed.StatusCode);
    }

    // ── Finance: money authority is not event authority ──────────────────────────────────────────

    [Fact]
    public async Task Finance_holds_no_authority_over_event_content()
    {
        var (_, _, orgId, eventId) = await HostedEventAsync("9970000008", "Finance Trust");
        var (finance, financeId) = await UserAsync("9970000009");
        SeatIn(orgId, financeId, OrgRole.Finance);

        var ticket = await finance.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types",
            TicketBody("Nope", 5));
        Assert.Equal(HttpStatusCode.Forbidden, ticket.StatusCode);
    }

    // ── Non-member and anonymous ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_stranger_gets_nothing_and_a_draft_event_hides_its_existence()
    {
        var (_, _, orgId, eventId) = await HostedEventAsync("9970000010", "Closed Doors");
        var (stranger, _) = await UserAsync("9970000011");

        // Hidden-resource boundary (D-018): a draft 404s rather than 403s for someone with no standing.
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/v1/events/{eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/attendees")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/workspace")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/analytics")).StatusCode);

        var ticket = await stranger.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types",
            TicketBody("Nope", 5));
        Assert.Equal(HttpStatusCode.Forbidden, ticket.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { title = "Hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_are_refused_before_authorization_runs()
    {
        var (_, _, orgId, eventId) = await HostedEventAsync("9970000012", "Anon Guard");
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/v1/events/{eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/attendees")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types")).StatusCode);
    }

    // ── Platform administrator ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Platform_reviewer_reads_the_event_but_holds_no_authority_over_its_sub_resources()
    {
        var (_, _, orgId, eventId) = await HostedEventAsync("9970000013", "Admin Reach");
        var reviewer = await _factory.ReviewerClientAsync();

        // A VerificationReviewer is a *platform role*, not the `KurxAdmin` claim — so it never reaches
        // EventAuthorityLevel.Admin. What it does have is D-191's explicit reviewer branch in CanViewAsync,
        // which lets it read any event's detail for moderation. Both facts are preserved exactly; the
        // distinction is easy to misread, which is why it is pinned here.
        Assert.Equal(HttpStatusCode.OK, (await reviewer.GetAsync($"/v1/events/{eventId}")).StatusCode);

        // No seat, no `kurx_admin` claim → no authority over the event's content. Unchanged by D-269.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reviewer.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types")).StatusCode);

        // And D-191 itself: content edit stays organizer-owned even for a genuine admin.
        var renamed = await reviewer.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", new { title = "Admin Edit" });
        Assert.Equal(HttpStatusCode.Forbidden, renamed.StatusCode);
    }

    // ── The org segment in the path is inert ─────────────────────────────────────────────────────

    [Fact]
    public async Task Authorization_reads_the_event_never_the_org_id_in_the_path()
    {
        var (owner, _, _, eventId) = await HostedEventAsync("9970000014", "Path Proof");
        var (stranger, strangerId) = await UserAsync("9970000015");
        var strangersOrg = _factory.SeedVerifiedOrg(strangerId, "Stranger Own Org");

        // The owner reaches their event through a wrong — even hostile — org segment, because the segment
        // is a path artefact and authorization resolves from the event itself (D-268/D-269).
        Assert.Equal(HttpStatusCode.OK,
            (await owner.GetAsync($"/v1/orgs/{strangersOrg}/events/{eventId}/ticket-types")).StatusCode);

        // And a stranger cannot reach someone else's event by naming an org they DO manage — the classic
        // confused-deputy shape this consolidation had to be checked against.
        var ticket = await stranger.PostAsJsonAsync($"/v1/orgs/{strangersOrg}/events/{eventId}/ticket-types",
            TicketBody("Escalation", 5));
        Assert.Equal(HttpStatusCode.Forbidden, ticket.StatusCode);
    }
}
