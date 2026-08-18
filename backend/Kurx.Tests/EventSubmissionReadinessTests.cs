using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-378 — an event entering the review queue carries the fields its listing is made of.
///
/// <para>Both wizards now block Continue until each step is answered, but a disabled button is a
/// rendering state and not an authorization: <c>POST …/transition {"action":"submit_review"}</c> is
/// reachable with curl. These tests are the server half — they never touch a client.</para>
///
/// <para>The gate sits on <b>submit_review only</b>. Create and update stay permissive because the
/// wizard writes a Draft step by step and <c>EventDraftBodyValidator</c> exists to preserve exactly the
/// half-filled state a stricter rule would refuse (D-266 M8). <see cref="Draft_still_saves_incomplete"/>
/// is the test that keeps this honest.</para></summary>
public class EventSubmissionReadinessTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public EventSubmissionReadinessTests(KurxApiFactory factory)
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
                Level = CategoryLevel.Category, Name = "Readiness Cat", Slug = "readiness-cat"
            };
            db.EventCategories.Add(category);
            db.SaveChanges();
            _categoryId = category.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone, code = _factory.WhatsApp.LastOtpFor(phone) }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return c;
    }

    private async Task<(HttpClient Owner, Guid OrgId, Guid EventId)> DraftAsync(string phone)
    {
        var owner = await LoginAsync(phone);
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Readiness Org " + phone);
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Readiness " + Guid.NewGuid().ToString("N")[..6],
            description = "A complete description.",
            categoryId = _categoryId,
            venueName = "Main Hall",
            city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20),
            endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        // Fields withheld — incompleteness is this class's subject. The AUTHORIZATION is filed, because
        // D-379 refuses a letterless event before any field check and this class is not about the letter.
        }, submittable: false, withAuthorization: true);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (owner, orgId, (await Json(res)).GetProperty("id").GetGuid());
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient c, Guid orgId, Guid eventId)
        => c.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });

    private static async Task<string?> SubmitErrorAsync(HttpClient c, Guid orgId, Guid eventId)
    {
        var res = await SubmitAsync(c, orgId, eventId);
        if (res.StatusCode == HttpStatusCode.OK) return null;
        var body = await Json(res);
        return body.TryGetProperty("error", out var e) ? e.GetString() : body.ToString();
    }

    /// <summary>Fills the groups the gate asks for, one PATCH per group, so a test can stop part-way.</summary>
    private static async Task PatchAsync(HttpClient c, Guid orgId, Guid eventId, object body)
    {
        // PATCH, not PUT: the endpoint is `events.MapPatch("/{eventId:guid}", ...)` and a PUT here is a
        // bare 405 with no body, which reads in a test failure as "the gate said the wrong thing".
        var res = await c.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}", body);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private static object ContentGroup => new { content = new { tagline = "One long day", shortDescription = "For students.", rules = "Be kind." } };
    private static object LocationGroup => new { location = new { building = "Block A", floor = "2", room = "204", googleMapsUrl = "https://maps.example.com/x" } };
    private static object ScheduleGroup => new
    {
        schedule = new
        {
            registrationOpensAt = DateTime.UtcNow.AddDays(1),
            registrationClosesAt = DateTime.UtcNow.AddDays(2),
            checkinOpensAt = DateTime.UtcNow.AddDays(3),
            checkinClosesAt = DateTime.UtcNow.AddDays(4),
            resultDate = DateTime.UtcNow.AddDays(5),
            certificateReleaseAt = DateTime.UtcNow.AddDays(6),
        }
    };
    private static object EligibilityGroup => new { eligibility = new { minAge = 16, maxAge = 30, maxTeams = 40 } };
    private static object LegalGroup => new
    {
        legal = new
        {
            termsUrl = "https://example.com/terms", codeOfConduct = "Be kind.",
            refundPolicy = "No refunds.", cancellationPolicy = "Cancel any time."
        }
    };

    /*
     * The whole contract in one walk: each group is refused in turn, and the event only enters the queue
     * once every one of them is answered. Written as a sequence rather than as five independent tests
     * because the ORDER is the assertion — a gate that returned the same code for every missing group
     * would tell an organiser nothing about which step to go back to.
     */
    [Fact]
    public async Task Submit_for_review_refuses_each_missing_group_in_turn_then_accepts()
    {
        var (owner, orgId, eventId) = await DraftAsync("9700014001");

        Assert.Equal("missing_tagline", await SubmitErrorAsync(owner, orgId, eventId));
        await PatchAsync(owner, orgId, eventId, ContentGroup);

        Assert.Equal("missing_building", await SubmitErrorAsync(owner, orgId, eventId));
        await PatchAsync(owner, orgId, eventId, LocationGroup);

        Assert.Equal("missing_registration_opens", await SubmitErrorAsync(owner, orgId, eventId));
        await PatchAsync(owner, orgId, eventId, ScheduleGroup);

        Assert.Equal("missing_min_age", await SubmitErrorAsync(owner, orgId, eventId));
        await PatchAsync(owner, orgId, eventId, EligibilityGroup);

        Assert.Equal("missing_terms_url", await SubmitErrorAsync(owner, orgId, eventId));
        await PatchAsync(owner, orgId, eventId, LegalGroup);

        Assert.Null(await SubmitErrorAsync(owner, orgId, eventId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(EventStatus.PendingReview,
            await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.Status).FirstAsync());
    }

    /// <summary>Whitespace is not content — the same rule both clients apply with `.trim()`.</summary>
    [Fact]
    public async Task A_field_holding_only_spaces_is_treated_as_missing()
    {
        var (owner, orgId, eventId) = await DraftAsync("9700014002");
        await PatchAsync(owner, orgId, eventId,
            new { content = new { tagline = "   ", shortDescription = "\t", rules = " " } });

        Assert.Equal("missing_tagline", await SubmitErrorAsync(owner, orgId, eventId));
    }

    /*
     * D-378's conditional half, and the one that would deadlock event creation if it were wrong.
     *
     * An ONLINE event has no building, floor or room — those fields are not missing, they do not exist,
     * and neither wizard renders them. A gate that demanded them would make every online event
     * permanently unsubmittable through the only door the review lifecycle has.
     */
    [Fact]
    public async Task An_online_event_is_asked_for_its_own_fields_and_not_the_physical_ones()
    {
        var (owner, orgId, eventId) = await DraftAsync("9700014003");
        await PatchAsync(owner, orgId, eventId, ContentGroup);
        await PatchAsync(owner, orgId, eventId, ScheduleGroup);
        await PatchAsync(owner, orgId, eventId, EligibilityGroup);
        await PatchAsync(owner, orgId, eventId, LegalGroup);
        await PatchAsync(owner, orgId, eventId,
            new { eventMode = "Online", onlineUrl = "https://meet.example.com/x" });

        // Never asks for a building. Asks for the online fields instead, one at a time.
        Assert.Equal("missing_meeting_platform", await SubmitErrorAsync(owner, orgId, eventId));
        await PatchAsync(owner, orgId, eventId, new { location = new { meetingPlatform = "Meet" } });

        Assert.Equal("missing_meeting_password", await SubmitErrorAsync(owner, orgId, eventId));
        await PatchAsync(owner, orgId, eventId, new { location = new { meetingPassword = "hack2026" } });

        Assert.Null(await SubmitErrorAsync(owner, orgId, eventId));
    }

    /*
     * The exclusions, which are the half that can deadlock creation.
     *
     * A Private product's archetype has `teams`, `scoring` and `certificates` Unsupported, so neither
     * wizard renders the results date, the certificate date, the team cap OR either age bound — only
     * Gender survives the filter. A gate that demanded any of those five would refuse the organiser for
     * a field that was never on their screen.
     *
     * This test exists because that is precisely what shipped in the first version: the age bounds sat
     * outside the Private arm on the server while both clients skipped them, so a wedding could be
     * completed in the wizard and then refused with `missing_min_age`. No other test submits a Private
     * event, so nothing caught it until the live E2E did.
     */
    [Fact]
    public async Task A_private_event_is_not_asked_for_the_five_fields_it_is_never_shown()
    {
        var (owner, orgId, eventId) = await DraftAsync("9700014005");
        await PatchAsync(owner, orgId, eventId, ContentGroup);
        await PatchAsync(owner, orgId, eventId, LocationGroup);
        await PatchAsync(owner, orgId, eventId, LegalGroup);
        // Only the two windows a private event is shown. Results + certificates withheld, and the whole
        // eligibility group withheld: no ages, no team cap.
        await PatchAsync(owner, orgId, eventId, new
        {
            schedule = new
            {
                registrationOpensAt = DateTime.UtcNow.AddDays(1),
                registrationClosesAt = DateTime.UtcNow.AddDays(2),
                checkinOpensAt = DateTime.UtcNow.AddDays(3),
                checkinClosesAt = DateTime.UtcNow.AddDays(4),
            }
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // `Product` is derived from the Type at create; set here because this fixture's category has no
        // Private Type, and the gate reads the column rather than re-resolving the archetype.
        var ev = await db.Events.FirstAsync(e => e.Id == eventId);
        ev.Product = EventProduct.Private;
        await db.SaveChangesAsync();

        Assert.Null(await SubmitErrorAsync(owner, orgId, eventId));

        // And the five really were absent, rather than defaulted in behind our back — which would look
        // identical from the API response alone.
        var after = await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
        Assert.Null(after.MinAge);
        Assert.Null(after.MaxAge);
        Assert.Null(after.ResultDate);
        Assert.Null(after.CertificateReleaseAt);
        Assert.Null(after.MaxTeams);
    }

    /*
     * §13 — the requirement is "cannot CONTINUE past an unanswered step", never "an incomplete draft
     * cannot be saved". The autosave endpoint stores the wizard's in-progress form verbatim, including
     * steps that would fail every rule above; refusing it here would delete the feature.
     */
    [Fact]
    public async Task Draft_still_saves_incomplete()
    {
        var (owner, orgId, eventId) = await DraftAsync("9700014004");

        // The event row itself: created and persisted with none of the submission fields set.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId);
            Assert.Equal(EventStatus.Draft, ev.Status);
            Assert.Null(ev.Tagline);
            Assert.Null(ev.TermsUrl);
        }

        // And the wizard's half-filled payload saves against it.
        // The autosave route is NOT org-scoped: `publicEvents = app.MapGroup("/v1/events")`.
        var res = await owner.PutAsJsonAsync($"/v1/events/{eventId}/draft",
            new { payloadJson = """{"step":"content","tagline":""}""", stepKey = "content" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // Still refused for review, which is the whole point of putting the gate on that transition.
        Assert.Equal("missing_tagline", await SubmitErrorAsync(owner, orgId, eventId));
    }
}
