using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-265 — the create-event wizard's field groups.
///
/// <para>These assert the two things the grouped-record design can get wrong: that a group supplied
/// on create is persisted, and that a group supplied on PATCH updates only the fields it names
/// rather than nulling its neighbours. Every validation branch is covered too, because a wizard that
/// accepts an impossible window (closes before it opens, max age below min) produces an event nobody
/// can register for and no error anybody can see.</para></summary>
public class EventCreationFieldsTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public EventCreationFieldsTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Tech", Slug = "tech-d265" };
            db.EventCategories.Add(category);
            db.SaveChanges();
            _categoryId = category.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    private async Task<(HttpClient Client, Guid OrgId)> OwnerWithOrgAsync(string phone, string orgName)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, _factory.SeedVerifiedOrgForClient(client, orgName));
    }

    private object CreateBody(string title, object? extra = null)
    {
        var b = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["description"] = "A great show.",
            ["categoryId"] = _categoryId,
            ["venueName"] = "The Venue",
            ["city"] = "Chennai",
            ["startsAt"] = DateTime.UtcNow.AddDays(10),
            ["endsAt"] = DateTime.UtcNow.AddDays(10).AddHours(3),
        };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties())
                b[p.Name] = p.GetValue(extra);
        return b;
    }

    private async Task<Event> LoadAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId);
    }

    // ── Persistence on create ────────────────────────────────────────────────

    [Fact]
    public async Task Create_persists_every_field_group()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000801", "D265 Create Org");

        var res = await client.CreateEventAsync(orgId, CreateBody("Full Wizard", new
        {
            Content = new { tagline = "Build the future", shortDescription = "48h hackathon", rules = "No pre-built code." },
            Legal = new { codeOfConduct = "Be excellent.", requiresConsent = true, consentText = "I agree to the rules." },
            Schedule = new
            {
                registrationOpensAt = DateTime.UtcNow.AddDays(1),
                registrationClosesAt = DateTime.UtcNow.AddDays(9),
                autoClose = true,
            },
            Location = new { building = "Block A", floor = "3", room = "301" },
            Eligibility = new { minAge = 18, maxAge = 25, genderRestriction = "Any", maxTeams = 40 },
            Commerce = new { taxPercent = 18.0m, taxInclusive = false },
        }), submittable: false);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var ev = await LoadAsync((await Json(res)).GetProperty("id").GetGuid());
        Assert.Equal("Build the future", ev.Tagline);
        Assert.Equal("48h hackathon", ev.ShortDescription);
        Assert.Equal("No pre-built code.", ev.Rules);
        Assert.Equal("Be excellent.", ev.CodeOfConduct);
        Assert.True(ev.RequiresConsent);
        Assert.True(ev.AutoClose);
        Assert.Equal("Block A", ev.Building);
        Assert.Equal("301", ev.Room);
        Assert.Equal(18, ev.MinAge);
        Assert.Equal(40, ev.MaxTeams);
        Assert.Equal(18.0m, ev.TaxPercent);
        Assert.False(ev.TaxInclusive);
    }

    [Fact]
    public async Task Create_without_any_group_leaves_the_new_fields_unset()
    {
        // The groups are optional and trailing precisely so every pre-D-265 client keeps working.
        var (client, orgId) = await OwnerWithOrgAsync("9700000802", "D265 Legacy Org");
        var res = await client.CreateEventAsync(orgId, CreateBody("Plain Event"), submittable: false);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var ev = await LoadAsync((await Json(res)).GetProperty("id").GetGuid());
        Assert.Null(ev.Tagline);
        Assert.Null(ev.MinAge);
        Assert.False(ev.RequiresConsent);
        Assert.Equal(GenderRestriction.Any, ev.GenderRestriction);
        Assert.True(ev.TaxInclusive);   // the documented default, not merely "not false"
    }

    // ── Partial update ───────────────────────────────────────────────────────

    [Fact]
    public async Task Patching_one_field_in_a_group_leaves_its_neighbours_alone()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000803", "D265 Patch Org");
        var created = await Json(await client.CreateEventAsync(orgId, CreateBody("Patch Me", new
        {
            Content = new { tagline = "Original tagline", rules = "Original rules" },
        }), submittable: false));
        var eventId = created.GetProperty("id").GetGuid();

        var res = await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}",
            new { content = new { tagline = "New tagline" } });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var ev = await LoadAsync(eventId);
        Assert.Equal("New tagline", ev.Tagline);
        // This is the whole point of null-means-untouched: a PATCH naming only the tagline must not
        // silently wipe the rules the organiser wrote in a different step of the wizard.
        Assert.Equal("Original rules", ev.Rules);
    }

    [Fact]
    public async Task An_empty_string_clears_a_field()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000804", "D265 Clear Org");
        var created = await Json(await client.CreateEventAsync(orgId, CreateBody("Clear Me", new
        {
            Content = new { tagline = "Delete me" },
        }), submittable: false));
        var eventId = created.GetProperty("id").GetGuid();

        await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}",
            new { content = new { tagline = "" } });

        Assert.Null((await LoadAsync(eventId)).Tagline);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_registration_window_that_closes_before_it_opens_is_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000810", "D265 Reg Window Org");
        var now = DateTime.UtcNow;

        var res = await client.CreateEventAsync(orgId, CreateBody("Bad Reg Window", new
        {
            Schedule = new { registrationOpensAt = now.AddDays(5), registrationClosesAt = now.AddDays(2) },
        }), submittable: false);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_registration_window", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_checkin_window_that_closes_before_it_opens_is_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000811", "D265 Checkin Window Org");
        var now = DateTime.UtcNow;

        var res = await client.CreateEventAsync(orgId, CreateBody("Bad Checkin Window", new
        {
            Schedule = new { checkinOpensAt = now.AddDays(5), checkinClosesAt = now.AddDays(2) },
        }), submittable: false);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_checkin_window", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_max_age_below_the_min_is_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000812", "D265 Age Org");
        var res = await client.CreateEventAsync(orgId, CreateBody("Bad Ages", new { Eligibility = new { minAge = 30, maxAge = 18 } }), submittable: false);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_age_range", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Requiring_consent_without_consent_text_is_rejected()
    {
        // Otherwise every registrant "accepts" an empty string and the stored hash evidences nothing.
        var (client, orgId) = await OwnerWithOrgAsync("9700000813", "D265 Consent Org");
        var res = await client.CreateEventAsync(orgId, CreateBody("No Consent Text", new { Legal = new { requiresConsent = true } }), submittable: false);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("consent_text_required", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task An_out_of_range_tax_percent_is_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000814", "D265 Tax Org");
        var res = await client.CreateEventAsync(orgId, CreateBody("Bad Tax", new { Commerce = new { taxPercent = 150.0m } }), submittable: false);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_tax_percent", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task An_unknown_gender_restriction_is_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000815", "D265 Gender Org");
        var res = await client.CreateEventAsync(orgId, CreateBody("Bad Gender", new { Eligibility = new { genderRestriction = "wizards" } }), submittable: false);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_gender_restriction", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_tagline_over_the_cap_is_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000816", "D265 Tagline Org");
        var res = await client.CreateEventAsync(orgId, CreateBody("Long Tagline", new { Content = new { tagline = new string('x', 161) } }), submittable: false);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("tagline_too_long", (await Json(res)).GetProperty("error").GetString());
    }

    // ── Read-back ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_groups_are_returned_not_write_only()
    {
        // Without this the wizard writes fields nothing ever reads: an edit form cannot prefill and
        // a detail page cannot render what was saved.
        var (client, orgId) = await OwnerWithOrgAsync("9700000818", "D265 Readback Org");
        var created = await Json(await client.CreateEventAsync(orgId, CreateBody("Readable", new
        {
            Content = new { tagline = "Read me back" },
            Eligibility = new { minAge = 16 },
        }), submittable: false));
        var eventId = created.GetProperty("id").GetGuid();

        var res = await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await Json(res);
        Assert.Equal("Read me back", body.GetProperty("content").GetProperty("tagline").GetString());
        Assert.Equal(16, body.GetProperty("eligibility").GetProperty("min_age").GetInt32());
    }

    [Fact]
    public async Task The_meeting_password_is_never_serialised()
    {
        // `ToEventJson` serves the organiser reads AND the public GET /v1/events/{slug}. A password
        // reachable from that projection is a password published to anonymous visitors.
        var (client, orgId) = await OwnerWithOrgAsync("9700000819", "D265 Secret Org");
        var created = await Json(await client.CreateEventAsync(orgId, CreateBody("Secret Room", new
        {
            eventMode = "Online",
            onlineUrl = "https://meet.example.com/abc",
            Location = new { meetingPlatform = "Meet", meetingPassword = "hunter2" },
        }), submittable: false));
        var eventId = created.GetProperty("id").GetGuid();

        // Stored…
        Assert.Equal("hunter2", (await LoadAsync(eventId)).MeetingPassword);

        // …but absent from the wire, on the organiser's own read and therefore on the public one too.
        var raw = await (await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("meeting_password", raw);
        // The non-secret half of the group still comes back.
        Assert.Contains("Meet", raw);
    }

    // ── Visibility ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Invite_only_is_an_author_visibility_and_is_distinct_from_admin_hidden()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000817", "D265 Visibility Org");
        var created = await Json(await client.CreateEventAsync(orgId, CreateBody("Invite Only Event", new { visibility = "InviteOnly" }), submittable: false));

        var ev = await LoadAsync(created.GetProperty("id").GetGuid());
        Assert.Equal(EventVisibility.InviteOnly, ev.Visibility);
        // IsHidden is the admin moderation flag; choosing InviteOnly must never set it, or
        // "why can't anyone see my event" becomes unanswerable.
        Assert.False(ev.IsHidden);
    }
}
