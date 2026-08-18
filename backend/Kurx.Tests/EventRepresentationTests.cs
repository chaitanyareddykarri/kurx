using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Orgs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-379 — a user owns the event; an organization is always the one it represents.
///
/// <para>The invariant, stated once: for every new event <c>E</c>, before it may pass the Representing
/// boundary or be submitted —</para>
/// <code>
/// E.RepresentingOrgId != null
/// E.RepresentingOrg.IsPersonal = false
/// EventAuthorization(E) exists AND EventAuthorization.EventId = E.Id
/// </code>
/// <para>No exception for Public, Private, Free, Paid, Team, Individual, development, an already-verified
/// organization, a previously-used one, or the same organizer. These tests reach the API directly and
/// never touch a client, because the wizard's disabled Continue is a rendering state and the door that
/// matters is the one curl can open.</para></summary>
public class EventRepresentationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public EventRepresentationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var category = new EventCategory
            {
                Level = CategoryLevel.Category, Name = "Representation Cat", Slug = "representation-cat"
            };
            db.EventCategories.Add(category);
            db.SaveChanges();
            _categoryId = category.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone, code = _factory.WhatsApp.LastOtpFor(phone) }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    private object EventBody(string title, string? visibility = null) => new
    {
        title,
        description = "A complete description.",
        categoryId = _categoryId,
        venueName = "Main Hall",
        city = "Vizag",
        startsAt = DateTime.UtcNow.AddDays(20),
        endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        visibility,
    };

    /// <summary>A personal organization row, seeded directly — the platform will not create another.</summary>
    private async Task<Guid> SeedHistoricalPersonalOrgAsync(Guid userId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var org = new Organization
        {
            Name = name, Slug = "self-" + Guid.NewGuid().ToString("N"),
            Type = OrganizationType.Other, NormalizedName = name.ToLowerInvariant(), IsPersonal = true,
        };
        org.CanonicalOrgId = org.Id;
        db.Organizations.Add(org);
        db.Memberships.Add(new Membership { OrgId = org.Id, UserId = userId, Role = OrgRole.Owner });
        await db.SaveChangesAsync();
        return org.Id;
    }

    // ── The creation boundary ─────────────────────────────────────────────────

    /// <summary>#2 — no organization named at all, the honest case.</summary>
    [Fact]
    public async Task Creating_an_event_with_no_organization_is_refused()
    {
        var (client, _) = await LoginAsync("9700015001");
        var res = await client.CreateEventAsync(null, EventBody("No Org"), submittable: false, withAuthorization: false);
        // 409, not 400: the payload is well-formed and the caller is entitled — the event's state
        // forbids proceeding, which is the same shape as the other publish blockers (EventEndpoints).
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("representation_required", await res.Content.ReadAsStringAsync());
    }

    /*
     * #3, #15 — naming your own self-representation row, which is the FORGEABLE case.
     *
     * The caller is that row's Owner, so the permission check passes and a non-null id sails through a
     * null check. `IsPersonal` is therefore read from the database rather than trusted from the client.
     * Before D-379 this was the path an unrepresented event actually travelled.
     */
    [Fact]
    public async Task Creating_an_event_that_names_a_personal_org_is_refused()
    {
        var (client, userId) = await LoginAsync("9700015002");
        var personalOrgId = await SeedHistoricalPersonalOrgAsync(userId, "Just Me Society");

        var res = await client.CreateEventAsync(personalOrgId, EventBody("Personal Rep"), submittable: false, withAuthorization: false);
        // 409, not 400: the payload is well-formed and the caller is entitled — the event's state
        // forbids proceeding, which is the same shape as the other publish blockers (EventEndpoints).
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("representation_required", await res.Content.ReadAsStringAsync());
    }

    /*
     * #4, #5, #6, #7 — visibility and pricing do not buy an exemption.
     *
     * The old rule required a real organization only of a PUBLIC event, so Private and Unlisted were
     * created representing nobody. All three are refused identically now: one rule, no product arm.
     */
    // `EventVisibility` is { Listed, Unlisted, InviteOnly }. "Public"/"Private" are PRODUCT classes, not
    // visibilities — passing them here fails the validator at 400 long before the representation guard,
    // which is exactly the vocabulary slip D-271 exists to prevent.
    [Theory]
    [InlineData(null, "9700015101")]
    [InlineData("Listed", "9700015102")]
    [InlineData("Unlisted", "9700015103")]
    [InlineData("InviteOnly", "9700015104")]
    public async Task No_visibility_exempts_an_event_from_representation(string? visibility, string phone)
    {
        // The phone is passed in rather than derived from the visibility string: deriving it produced an
        // 11-digit number for "InviteOnly", which fails OTP request rather than the assertion under test.
        var (client, _) = await LoginAsync(phone);
        var res = await client.CreateEventAsync(null, EventBody("V " + visibility, visibility), submittable: false, withAuthorization: false);
        // 409, not 400: the payload is well-formed and the caller is entitled — the event's state
        // forbids proceeding, which is the same shape as the other publish blockers (EventEndpoints).
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("representation_required", await res.Content.ReadAsStringAsync());
    }

    /// <summary>#1 — the positive case: a real verified organization is accepted, and the Draft is
    /// represented from the moment it exists.</summary>
    [Fact]
    public async Task Creating_a_draft_with_a_real_verified_organization_is_allowed()
    {
        var (client, _) = await LoginAsync("9700015003");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Real Institute A");

        var res = await client.CreateEventAsync(orgId, EventBody("Real Org"), submittable: false, withAuthorization: false);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var eventId = (await Json(res)).GetProperty("id").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
        Assert.Equal(orgId, ev.RepresentingOrgId);
        Assert.Equal(EventStatus.Draft, ev.Status);
        // #17 — the Draft is represented by the REAL organization, and nothing was minted beside it.
        // Scoped to this event rather than swept globally: sibling tests in this class seed historical
        // personal rows into the same class database on purpose, so an absolute "none exist" assertion
        // would be asserting against their fixtures instead of this one's behaviour.
        Assert.False(await db.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == ev.RepresentingOrgId && o.IsPersonal));
    }

    /*
     * #18 — the minting path is GONE, not merely unused.
     *
     * `ResolveSelfRepresentationAsync` was deleted with D-379, so no code path can produce an
     * `IsPersonal` row. Asserted over the whole table after a create that would previously have minted
     * one: before D-379, creating an event with no organization produced exactly such a row.
     */
    [Fact]
    public async Task No_code_path_creates_a_self_representation_row()
    {
        var (client, userId) = await LoginAsync("9700015004");

        // A DELTA, not an absolute: sibling tests seed historical personal rows into this same class
        // database, so "none exist" would be false for reasons that have nothing to do with minting.
        // What must hold is that this create adds none.
        async Task<int> PersonalCountAsync()
        {
            using var s2 = _factory.Services.CreateScope();
            var d2 = s2.ServiceProvider.GetRequiredService<KurxDbContext>();
            return await d2.Organizations.AsNoTracking().CountAsync(o => o.IsPersonal);
        }

        var before = await PersonalCountAsync();
        await client.CreateEventAsync(null, EventBody("Would Have Minted"), submittable: false, withAuthorization: false);
        Assert.Equal(before, await PersonalCountAsync());

        // And this user — who has no organization at all — acquired none on the way.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Empty(await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Organizations.AsNoTracking().Where(o => o.IsPersonal),
                  m => m.OrgId, o => o.Id, (m, o) => o.Id)
            .ToListAsync());
    }

    /// <summary>#13 — and the route that used to mint one directly is refused.</summary>
    [Fact]
    public async Task Creating_a_personal_organization_over_the_api_is_refused()
    {
        var (client, _) = await LoginAsync("9700015005");
        var res = await client.PostAsJsonAsync("/v1/orgs/", new { name = "Just Me Too", personal = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("personal_org_not_supported", await res.Content.ReadAsStringAsync());
    }

    // ── The submission boundary ───────────────────────────────────────────────

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient c, Guid orgId, Guid eventId)
        => c.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });

    private static async Task<string?> SubmitErrorAsync(HttpClient c, Guid orgId, Guid eventId)
    {
        var res = await SubmitAsync(c, orgId, eventId);
        if (res.StatusCode == HttpStatusCode.OK) return null;
        var body = await Json(res);
        return body.TryGetProperty("error", out var e) ? e.GetString() : body.ToString();
    }

    private async Task AuthorizeAsync(Guid eventId, Guid submittedBy, string letterKey = "private/auth/letter.pdf")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.EventAuthorizations.Add(new EventAuthorization
        {
            EventId = eventId, SubmittedBy = submittedBy,
            HeadName = "R Iyer", HeadDesignation = "Principal",
            OfficialEmail = "head@institute.ac.in", OfficialPhone = "+919876543210",
            RepresentativeRole = "Principal", LetterheadDocumentKey = letterKey,
        });
        await db.SaveChangesAsync();
    }

    /*
     * #8, #9, #16 — the event has its organization but no letter, so it cannot enter the queue; supplying
     * the letter is what unblocks it. Reached over HTTP with no client involved, which is #14/#16's point.
     */
    [Fact]
    public async Task Submission_is_refused_until_the_event_has_its_own_authorization()
    {
        var (client, userId) = await LoginAsync("9700015006");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Real Institute B");
        var eventId = (await Json(await client.CreateEventAsync(orgId, EventBody("Needs Letter"), withAuthorization: false)))
            .GetProperty("id").GetGuid();

        Assert.Equal("event_authorization_required", await SubmitErrorAsync(client, orgId, eventId));

        await AuthorizeAsync(eventId, userId);
        Assert.Null(await SubmitErrorAsync(client, orgId, eventId));
    }

    /// <summary>The letter is the evidence: a row without one records a claim and proves nothing.</summary>
    [Fact]
    public async Task Submission_is_refused_when_the_authorization_has_no_letter()
    {
        var (client, userId) = await LoginAsync("9700015007");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Real Institute C");
        var eventId = (await Json(await client.CreateEventAsync(orgId, EventBody("No Letter"), withAuthorization: false)))
            .GetProperty("id").GetGuid();

        await AuthorizeAsync(eventId, userId, letterKey: "");
        Assert.Equal("letterhead_required", await SubmitErrorAsync(client, orgId, eventId));
    }

    /*
     * #10, #11, #12, #16, #17, #18 — the heart of it, and the exact Event A / Event B scenario.
     *
     * NSRIT College is verified ONCE and reused for both events, which is the intended behaviour: the
     * organiser does not register it again. What is NOT reused is the authorization. Event A's letter
     * cannot satisfy Event B — structurally, because `EventAuthorization` is keyed on `EventId` and one
     * row answers for exactly one event, so this is an invariant of the schema rather than a check that
     * could be forgotten.
     */
    [Fact]
    public async Task The_same_organization_serves_two_events_but_its_authorization_does_not()
    {
        var (client, userId) = await LoginAsync("9700015008");
        var nsrit = _factory.SeedVerifiedOrgForClient(client, "NSRIT College");

        // Event A — Tech Fest.
        var techFest = (await Json(await client.CreateEventAsync(nsrit, EventBody("NSRIT Tech Fest"), withAuthorization: false)))
            .GetProperty("id").GetGuid();
        await AuthorizeAsync(techFest, userId);
        Assert.Null(await SubmitErrorAsync(client, nsrit, techFest));

        // Event B — Hackathon, SAME organization, no registration of it required.
        var hackathon = (await Json(await client.CreateEventAsync(nsrit, EventBody("NSRIT Hackathon"), withAuthorization: false)))
            .GetProperty("id").GetGuid();

        // #11/#12 — Event A's authorization does not carry over. Event B is refused on its own merits.
        Assert.Equal("event_authorization_required", await SubmitErrorAsync(client, nsrit, hackathon));

        await AuthorizeAsync(hackathon, userId);
        Assert.Null(await SubmitErrorAsync(client, nsrit, hackathon));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // #10 — one organization, two events. Correct and intended.
        Assert.Equal(2, await db.Events.CountAsync(e => e.RepresentingOrgId == nsrit && e.DeletedAt == null));
        // #17 — two independent authorizations, each answering for exactly one event.
        var authorizations = await db.EventAuthorizations.AsNoTracking()
            .Where(a => a.EventId == techFest || a.EventId == hackathon).ToListAsync();
        Assert.Equal(2, authorizations.Count);
        Assert.Equal(2, authorizations.Select(a => a.EventId).Distinct().Count());
        Assert.Single(authorizations, a => a.EventId == techFest);
        Assert.Single(authorizations, a => a.EventId == hackathon);
    }

    /*
     * #13, #18, #19, #20 — existing data survives.
     *
     * A historical event under a personal row keeps loading and keeps its representation: the invariant
     * is enforced at the creation and submission boundaries, never as a retroactive validity sweep. What
     * it cannot do is enter the review queue, which is the honest consequence of requiring evidence it
     * never carried — and converting it silently would invent a representation its creator never gave.
     */
    [Fact]
    public async Task A_historical_personal_event_still_loads_but_cannot_be_submitted()
    {
        var (client, userId) = await LoginAsync("9700015009");
        var orgId = _factory.SeedVerifiedOrgForClient(client, "Real Institute D");
        var eventId = (await Json(await client.CreateEventAsync(orgId, EventBody("Legacy"), withAuthorization: false)))
            .GetProperty("id").GetGuid();

        // Re-point it at a personal row, as a pre-D-379 event would be.
        var personalOrgId = await SeedHistoricalPersonalOrgAsync(userId, "Legacy Personal");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db.Events.FirstAsync(e => e.Id == eventId);
            ev.RepresentingOrgId = personalOrgId;
            await db.SaveChangesAsync();
        }
        await AuthorizeAsync(eventId, userId);

        // #19/#20 — not destroyed, not converted: it still reads.
        var read = await client.GetAsync($"/v1/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // …and it cannot proceed, even though its authorization is complete.
        Assert.Equal("representation_required", await SubmitErrorAsync(client, personalOrgId, eventId));

        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<KurxDbContext>();
        var after = await db2.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
        Assert.Equal(personalOrgId, after.RepresentingOrgId);   // untouched
        Assert.Equal(EventStatus.Draft, after.Status);
    }
}
