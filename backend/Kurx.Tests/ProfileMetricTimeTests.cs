using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-234 — profile metrics count <b>completed</b> activity only.
///
/// <para><b>The defect.</b> The fact-set loads every non-private event a person touches, future ones
/// included, and the metric engines counted them. Registering for a conference next month therefore
/// raised "events participated"; accepting a speaker slot raised "speaker sessions". A profile could
/// advertise experience its owner had not yet had — on a product whose entire claim is that its
/// numbers are verifiable, that is the worst possible failure mode.</para>
///
/// <para><b>Why it was invisible.</b> The event <i>listings</i> have always filtered
/// <c>EndsAt &lt; now</c>. Counts and lists disagreed, so a tab could read "Events (8)" above five
/// rows, and nobody compared the two.</para>
///
/// <para>These tests seed one person with exactly one finished event and one future event in every
/// lane, then assert each metric reports 1 and not 2.</para></summary>
public class ProfileMetricTimeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileMetricTimeTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9377{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync()
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    /// <summary>One org, two events — one finished, one still to come — with the same person attached
    /// to both in every lane the metrics read.</summary>
    private async Task<string> SeedPastAndFutureAsync(Guid userId)
    {
        var username = "mt" + Guid.NewGuid().ToString("N")[..16];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Username = username;
        user.ProfilePublic = true;

        var org = new Organization
        {
            Name = "Time Org " + Guid.NewGuid().ToString("N")[..6],
            Slug = "timeorg" + Guid.NewGuid().ToString("N")[..10],
        };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "time-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Time Cat", Slug = "time-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        // The membership is what makes both events "organized by" this user.
        db.Memberships.Add(new Membership
        {
            OrgId = org.Id, UserId = userId, Role = OrgRole.Manager, ShowOnProfile = true,
        });

        var events = new List<Event>();
        foreach (var (label, startOffset, endOffset, kind) in new[]
        {
            ("Past", -10, -9, "hackathon"),
            ("Future", 30, 31, "workshop"),
        })
        {
            var ev = new Event
            {
                RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = userId,
                Title = label + " Event", Slug = label.ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N")[..10],
                ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                City = label + "ville", KindSlug = kind,
                Status = EventStatus.Published, Visibility = EventVisibility.Listed,
                StartsAt = DateTime.UtcNow.AddDays(startOffset), EndsAt = DateTime.UtcNow.AddDays(endOffset),
            };
            events.Add(ev);
            db.Events.Add(ev);
        }
        await db.SaveChangesAsync();

        foreach (var ev in events)
        {
            db.EventParticipants.Add(new EventParticipant
            {
                EventId = ev.Id, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
                RoleSlug = "coordinator",             // also exercises the leadership count
                State = ParticipantState.Completed,
                Visibility = ParticipantVisibility.Public,
            });
            db.EventAssignments.Add(new EventAssignment
            {
                EventId = ev.Id, OrgId = org.Id, UserId = userId,
                Role = "Volunteer", Status = AssignmentStatus.Accepted,
                ShowOnProfile = true,
            });
        }
        await db.SaveChangesAsync();
        return username;
    }

    /// <summary>The core contract: every event-derived metric counts the finished event and not the
    /// scheduled one. Asserting the exact number matters — an assertion of "&gt; 0" would have passed
    /// against the defect.</summary>
    [Fact]
    public async Task Future_events_are_excluded_from_every_completed_activity_metric()
    {
        var (_, userId) = await LoginAsync();
        var username = await SeedPastAndFutureAsync(userId);

        var anon = _factory.CreateClient();
        var m = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));

        Assert.Equal(1, m.GetProperty("events_organized").GetInt32());
        Assert.Equal(1, m.GetProperty("events_participated").GetInt32());
        Assert.Equal(1, m.GetProperty("assignments_accepted").GetInt32());

        // Cities and DNA are derived from the same event map and must agree with the counts above:
        // the future event is in another city and of another kind, so leaking it is unmistakable.
        var cities = m.GetProperty("cities").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Equal(["Pastville"], cities);

        var dna = m.GetProperty("event_dna").EnumerateArray()
            .Select(d => d.GetProperty("kind").GetString()).ToList();
        Assert.Equal(["hackathon"], dna);
    }

    /// <summary>The inconsistency that made this reachable: the listing filtered by time and the count
    /// did not. They must now describe the same set — a tab label can never exceed the list beneath it.
    /// </summary>
    [Fact]
    public async Task The_events_count_matches_the_events_the_listing_returns()
    {
        var (_, userId) = await LoginAsync();
        var username = await SeedPastAndFutureAsync(userId);

        var anon = _factory.CreateClient();
        var m = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));
        var conducted = await Json(await anon.GetAsync($"/v1/public/users/{username}/events?type=conducted"));

        Assert.Equal(conducted.GetArrayLength(), m.GetProperty("events_organized").GetInt32());
    }

    /// <summary>The Experience band is a claim about experience already had, so it reads the same
    /// completed-only counts. A future event must not push someone into a higher band.</summary>
    [Fact]
    public async Task Experience_counts_exclude_future_events()
    {
        var (_, userId) = await LoginAsync();
        var username = await SeedPastAndFutureAsync(userId);

        var anon = _factory.CreateClient();
        var e = await Json(await anon.GetAsync($"/v1/public/users/{username}/experience"));

        Assert.Equal(1, e.GetProperty("distinct_events").GetInt32());
        Assert.Equal(1, e.GetProperty("events_organized").GetInt32());
        Assert.Equal(1, e.GetProperty("leadership_events").GetInt32());
    }
}
