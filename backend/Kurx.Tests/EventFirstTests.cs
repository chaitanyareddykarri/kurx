using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Event-first architecture (D-074/D-075). Institutions are never self-minted: a not-yet-registered
/// org is staged as a hidden representation request (PendingReview, unsearchable, no Owner — the submitter is
/// a *pending* Representative with zero trust capability). Admin approval materializes it into the registry,
/// verifies the representative, and lets its pending events publish. Real HTTP + kurx_test.</summary>
public class EventFirstTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static Guid _privateTypeId;

    public EventFirstTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "EF Cat", Slug = "ef-cat" };
                db.EventCategories.Add(cat);
                // D-353 — a Type-less event resolves Public (D-326's documented fallback), and a Public
                // event now requires a verified organization. Self-hosting survives only on the Private
                // side, so exercising it at all needs a Type that actually classifies Private. Without
                // this every "personal event" test below would be testing an impossible shape.
                var privateType = new EventCategory
                {
                    Level = CategoryLevel.Type, ParentId = cat.Id,
                    Name = "EF Private Type", Slug = "ef-private-type",
                    ProductClass = EventProduct.Private,
                };
                db.EventCategories.Add(privateType);
                db.SaveChanges();
                _categoryId = cat.Id;
                _privateTypeId = privateType.Id;
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
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private static Task<HttpResponseMessage> SubmitRequest(HttpClient client, string name, string? type = "College") =>
        client.PostAsJsonAsync("/v1/orgs/representation-requests", new
        {
            name, type,
            documents = new[] { new { docType = "registration_cert", storageKey = "private/verif/reg.pdf" } },
        });

    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId)
        => (await Json(await client.CreateEventAsync(orgId, new
        {
            title = "Fresher Fest " + Guid.NewGuid().ToString("N")[..6],
            description = "A great event with plenty of detail.",
            categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> Transition(HttpClient client, Guid orgId, Guid eventId, string action) =>
        client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action });

    // ── D-353 · a public event must represent a real organization ──────────────────────────────
    //
    // Both refusals are pinned because the second is the one a client can forge: naming the caller's own
    // self-representation row passes the authority check (they are its Owner) and passes a null check
    // (the id is non-null). Verified against the live API before the guard existed — the request reached
    // category validation, which is downstream of it.

    [Fact]
    public async Task Public_event_without_a_representation_is_refused()
    {
        var user = await LoginAsync("9960000090");

        var res = await user.CreateEventAsync(null, new
        {
            title = "Unrepresented Fest",
            description = "A great event with plenty of detail.",
            categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("representation_required", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Naming_your_own_self_representation_row_does_not_satisfy_the_rule()
    {
        var user = await LoginAsync("9960000091");

        // Create a PRIVATE-shaped event first so the self-representation row exists to be named. It is
        // minted on first creation and is never surfaced, so this is the only way to obtain its id — which
        // is also why a real attacker would have to read it out of a response that never carries it.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var me = await Json(await user.GetAsync("/v1/me"));
        var userId = me.GetProperty("id").GetGuid();
        var selfOrg = new Domain.Entities.Organization
        {
            Name = "Self Row", Slug = "self-" + Guid.NewGuid().ToString("N"),
            NormalizedName = "self row", IsPersonal = true,
        };
        selfOrg.CanonicalOrgId = selfOrg.Id;
        db.Organizations.Add(selfOrg);
        db.Memberships.Add(new Domain.Entities.Membership
        {
            OrgId = selfOrg.Id, UserId = userId, Role = Domain.Enums.OrgRole.Owner,
        });
        await db.SaveChangesAsync();

        var res = await user.CreateEventAsync(selfOrg.Id, new
        {
            title = "Forged Representation Fest",
            description = "A great event with plenty of detail.",
            categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        });

        // The same code as the omitted case: to the organiser it is one rule, not two.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("representation_required", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Representation_request_stages_a_hidden_pending_org()
    {
        var user = await LoginAsync("9960000001");
        var org = await Json(await SubmitRequest(user, "Hidden Institute of Tech"));
        var orgId = org.GetProperty("id").GetGuid();
        var slug = org.GetProperty("slug").GetString();

        Assert.Equal("pendingreview", org.GetProperty("verification_status").GetString());
        Assert.Equal("representative", org.GetProperty("role").GetString());   // never Owner (D-074)

        // Not in the registry, and no public profile (never leak a staged org's existence, D-018).
        Assert.Empty((await Json(await user.GetAsync("/v1/orgs/search?q=Hidden Institute of Tech"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/v1/public/orgs/{slug}")).StatusCode);

        // The submitter is a *pending* representative → zero trust capability until an admin approves.
        var caps = await Json(await user.GetAsync($"/v1/orgs/{orgId}/my-capabilities"));
        Assert.False(caps.GetProperty("can_represent_org").GetBoolean());
        Assert.False(caps.GetProperty("is_org_verified").GetBoolean());
    }

    [Fact]
    public async Task Direct_org_create_is_rejected_for_institutions_but_allowed_for_personal()
    {
        var user = await LoginAsync("9960000002");

        var institution = await user.PostAsJsonAsync("/v1/orgs/", new { name = "Self Minted College", type = "College" });
        Assert.Equal(HttpStatusCode.BadRequest, institution.StatusCode);
        Assert.Equal("use_representation_request", (await Json(institution)).GetProperty("error").GetString());

        // D-379 — the personal half is refused now. An institution still routes through a
        // representation request; a personal organization can no longer be created at all.
        var personal = await user.PostAsJsonAsync("/v1/orgs/", new { name = "Just Me Society", personal = true });
        Assert.Equal(HttpStatusCode.BadRequest, personal.StatusCode);
        Assert.Equal("personal_org_not_supported", (await Json(personal)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Pending_org_event_waits_for_verification_then_publishes_on_approval()
    {
        var user = await LoginAsync("9960000003");
        var org = await Json(await SubmitRequest(user, "Waiting Room University"));
        var orgId = org.GetProperty("id").GetGuid();

        // The submitter can create (manage) the event under the pending placeholder org...
        var eventId = await CreateEventAsync(user, orgId);

        // ...but it cannot publish while the institution is unverified — even though it is free.
        var blocked = await Transition(user, orgId, eventId, "publish");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("pending_org_verification", (await Json(blocked)).GetProperty("error").GetString());

        // Admin approves the representation request → org materialized into the registry + rep verified.
        var reviewer = await _factory.ReviewerClientAsync();
        var approve = await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review", new { decision = "approve" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var caps = await Json(await user.GetAsync($"/v1/orgs/{orgId}/my-capabilities"));
        Assert.True(caps.GetProperty("is_org_verified").GetBoolean());
        Assert.True(caps.GetProperty("can_represent_org").GetBoolean());        // pending rep is now verified

        Assert.NotEmpty((await Json(await user.GetAsync("/v1/orgs/search?q=Waiting Room University"))).EnumerateArray());

        // D-266 M5: org verification and institutional authorization are different facts. This test is
        // about the first — that the org became verified — so the second is satisfied here rather than
        // left to mask it. Seeded AFTER the block above, which must still fail for its own reason.
        _factory.SeedApprovedEventAuthorization(eventId);

        var published = await Transition(user, orgId, eventId, "publish");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("published", (await Json(published)).GetProperty("status").GetString());
    }

    // ── User-first navigation (D-267) ────────────────────────────────────────────────────────────
    // The client never enumerates organizations to find a person's events, and never picks one to
    // reach event creation. These three cover the endpoints that make that true.

    /// <summary>Self-hosting survives D-353 on the PRIVATE side, and carries no organization on the wire.
    ///
    /// <para>This asserted the same thing for a Public event, which is exactly what D-353 removed. The
    /// subject was never the product axis — it is that a self-hosted event reports
    /// <c>representation.kind = "personal"</c> with no organization id or name, and that the
    /// FK-satisfying row is never offered as something to represent. All of that still holds; only the
    /// product it holds for narrowed.</para></summary>
    [Fact]
    public async Task Every_event_names_the_real_organization_it_represents()
    {
        // This user has never onboarded, so `Users.Name` is "" — the case that made the resolver fail
        // `invalid_name` and take event creation down with it.
        var user = await LoginAsync("9960000010");

        await user.CreateEventAsync(SelfOrgAsync(user), NewSelfHostedEventBody("Solo Gig"));
        await user.CreateEventAsync(SelfOrgAsync(user), NewSelfHostedEventBody("Solo Gig Two"));

        var mine = await Json(await user.GetAsync("/v1/me/events"));
        var rows = mine.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);

        // D-379 — both now name the real organization they represent. This block asserted the opposite
        // (kind "personal", no id, no name) because the events were self-hosted; that shape is retired.
        Assert.All(rows, r =>
        {
            var rep = r.GetProperty("representation");
            // D-379 — always "organization" now; the personal kind is retired for new events.
            Assert.Equal("organization", rep.GetProperty("kind").GetString());
            // D-379 — inverted. These asserted that a self-hosted event leaks no organization, because
            // there was none in the domain. Every event names a real one now, so both must be present.
            Assert.NotEqual(JsonValueKind.Null, rep.GetProperty("organization_id").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(rep.GetProperty("organization_name").GetString()));
        });

        // …and the organization IS offered as something to represent, because it is a real one. The
        // original assertion (empty) was about the self-representation row never being offered — there
        // is no such row any more, so the meaningful check is that the real organization appears.
        var reps = (await Json(await user.GetAsync("/v1/me/representations"))).EnumerateArray().ToList();
        Assert.NotEmpty(reps);
    }

    [Fact]
    public async Task The_owner_manages_their_own_event_without_any_organization_membership()
    {
        var user = await LoginAsync("9960000015");
        var eventId = (await Json(await user.CreateEventAsync(SelfOrgAsync(user), NewSelfHostedEventBody("Owned By Me"))))
            .GetProperty("id").GetGuid();

        // Ownership alone authorizes the event surface. Before D-268 every one of these resolved to
        // "are you a member of the event's organization?", so a person hosting under their own name
        // could only manage their event because a synthetic membership had been minted for them.
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/v1/events/{eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/v1/orgs/{Guid.NewGuid()}/events/{eventId}/workspace")).StatusCode);

        var renamed = await user.PatchAsJsonAsync($"/v1/orgs/{Guid.NewGuid()}/events/{eventId}",
            new { title = "Owned By Me, Renamed" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Owned By Me, Renamed", (await Json(renamed)).GetProperty("title").GetString());

        // A stranger owns nothing here and represents nobody — 404, never 403 (D-018).
        var stranger = await LoginAsync("9960000016");
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/v1/orgs/{Guid.NewGuid()}/events/{eventId}/workspace")).StatusCode);
    }

    [Fact]
    public async Task My_events_spans_every_org_the_caller_belongs_to_and_excludes_everyone_elses()
    {
        var user = await LoginAsync("9960000011");
        var stranger = await LoginAsync("9960000012");

        // One event as themselves, one representing an institution they staged — two different orgs,
        // one flat list. The caller supplies no org id at all.
        await user.CreateEventAsync(SelfOrgAsync(user), NewSelfHostedEventBody("Personal Meetup"));
        var orgId = (await Json(await SubmitRequest(user, "Two Hats Institute"))).GetProperty("id").GetGuid();
        await user.CreateEventAsync(orgId, NewEventBody("Institute Summit"));

        await stranger.CreateEventAsync(SelfOrgAsync(stranger), NewSelfHostedEventBody("Not Yours"));

        var mine = (await Json(await user.GetAsync("/v1/me/events"))).GetProperty("items").EnumerateArray().ToList();
        var titles = mine.Select(r => r.GetProperty("title").GetString()).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Contains("Personal Meetup", titles);
        Assert.Contains("Institute Summit", titles);
        Assert.DoesNotContain("Not Yours", titles);

        // Representation rides along as row metadata so the UI can say "representing X" without a
        // second call — it is never the container the caller had to open first, and never the owner.
        var summit = mine.Single(r => r.GetProperty("title").GetString() == "Institute Summit");
        var rep = summit.GetProperty("representation");
        Assert.Equal("organization", rep.GetProperty("kind").GetString());
        Assert.Equal(orgId, rep.GetProperty("organization_id").GetGuid());
        Assert.Equal("Two Hats Institute", rep.GetProperty("organization_name").GetString());

        var personal = mine.Single(r => r.GetProperty("title").GetString() == "Personal Meetup");
        Assert.Equal("organization", personal.GetProperty("representation").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task An_event_is_readable_by_its_own_id_and_stays_hidden_from_non_members()
    {
        var user = await LoginAsync("9960000013");
        var stranger = await LoginAsync("9960000014");
        var eventId = (await Json(await user.CreateEventAsync(SelfOrgAsync(user), NewSelfHostedEventBody("Id Addressed")))).GetProperty("id").GetGuid();

        var mine = await user.GetAsync($"/v1/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal("Id Addressed", (await Json(mine)).GetProperty("title").GetString());

        // A draft belongs to its host; an outsider gets 404, never 403 (D-018 — no existence leak).
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/v1/events/{eventId}")).StatusCode);
    }

    private static object NewEventBody(string title) => new
    {
        title,
        description = "A great event with plenty of detail.",
        categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
        startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
    };

    /// <summary>A self-hosted event. Since D-353 that means a PRIVATE one: a Public event must name a
    /// verified organization, so `representingOrgId: null` is legal only on the Private side. The tests
    /// below are about ownership, visibility and listing — not about representation — so they carry the
    /// Private Type rather than acquiring an organization they were never testing.</summary>
    private static object NewSelfHostedEventBody(string title) => new
    {
        title,
        description = "A great event with plenty of detail.",
        categoryId = _categoryId, typeId = _privateTypeId, venueName = "Main Hall", city = "Vizag",
        startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
    };
    /*
     * D-379 — a real organization for a caller who used to host under their own name.
     *
     * These tests' subjects are id-addressability, `/me/events` scoping and creator authority; the fact
     * that the event was self-represented was incidental to all three and is no longer possible. One
     * verified organization per caller keeps each test's own subject intact.
     */
    private Guid SelfOrgAsync(HttpClient client) =>
        _factory.SeedVerifiedOrgForClient(client, "Own Org " + Guid.NewGuid().ToString("N")[..8]);

}
