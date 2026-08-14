using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-228 — the Professional Resume and the contributions heatmap.
///
/// <para>The two properties that matter most are privacy properties, because both surfaces aggregate
/// across every section: the resume must be composed <b>for the requesting viewer</b> (otherwise
/// downloading the PDF reads what the page refuses to show), and the heatmap must <b>gate before
/// bucketing</b> (otherwise a day's intensity is an oracle for hidden activity).</para></summary>
public class ProfileResumeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileResumeTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9203{Interlocked.Increment(ref _phoneSeq):D6}";

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

    private async Task<string> ClaimAsync(Guid userId, string name = "Resume Tester")
    {
        var username = "res" + Guid.NewGuid().ToString("N")[..15];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Username = username;
        user.Name = name;
        user.ProfilePublic = true;
        await db.SaveChangesAsync();
        return username;
    }

    private async Task<Guid> SeedEventWithRoleAsync(
        KurxDbContext db, Guid userId, string roleSlug, DateTime startsAt)
    {
        var org = new Organization
        {
            Name = "Res Org " + Guid.NewGuid().ToString("N")[..6],
            Slug = "resorg" + Guid.NewGuid().ToString("N")[..10],
        };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "res-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Res Cat", Slug = "res-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = userId,
            Title = "Resume Event " + Guid.NewGuid().ToString("N")[..5],
            Slug = "resev-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = EventVisibility.Listed,
            StartsAt = startsAt, EndsAt = startsAt.AddDays(1),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        db.EventParticipants.Add(new EventParticipant
        {
            EventId = ev.Id, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
            RoleSlug = roleSlug, State = ParticipantState.Completed,
            Visibility = ParticipantVisibility.Public,
        });
        await db.SaveChangesAsync();
        return ev.Id;
    }

    // ── Resume ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_resume_renders_a_real_pdf()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await SeedEventWithRoleAsync(db, userId, "speaker", DateTime.UtcNow.AddDays(-30));
        }

        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/v1/public/users/{username}/resume");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        // A PDF always starts with %PDF- — cheapest unambiguous proof the renderer produced a real
        // document rather than an empty or error body.
        Assert.True(bytes.Length > 1000, $"resume suspiciously small: {bytes.Length} bytes");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    /// <summary>A person with nothing still gets a valid document — the resume must degrade to a
    /// short honest page, not fail or invent filler.</summary>
    [Fact]
    public async Task A_user_with_no_activity_still_gets_a_valid_resume()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/v1/public/users/{username}/resume");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    /// <summary>The privacy property. A hidden profile must not be downloadable as a document — if the
    /// page is 404 the resume must be too, or the PDF becomes a trivial bypass of the whole model.</summary>
    [Fact]
    public async Task A_hidden_profile_has_no_downloadable_resume()
    {
        var (client, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);
        await client.PatchAsJsonAsync("/v1/me/privacy", new { profilePublic = false });

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/v1/public/users/{username}/resume")).StatusCode);

        // The owner can still download their own.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/v1/public/users/{username}/resume")).StatusCode);
    }

    /// <summary>A restricted section must shrink the document. Comparing byte length is crude but it
    /// is the property that matters: the viewer's copy cannot contain what their page would hide.</summary>
    [Fact]
    public async Task A_hidden_section_produces_a_smaller_resume_for_a_stranger_than_for_the_owner()
    {
        var (client, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 3; i++)
                await SeedEventWithRoleAsync(db, userId, "speaker", DateTime.UtcNow.AddDays(-40 - i));
        }

        var anon = _factory.CreateClient();
        var openSize = (await (await anon.GetAsync($"/v1/public/users/{username}/resume")).Content.ReadAsByteArrayAsync()).Length;

        // Hide the events section from everyone but the owner.
        await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { ["events"] = "only_me" },
        });

        var restrictedSize = (await (await anon.GetAsync($"/v1/public/users/{username}/resume")).Content.ReadAsByteArrayAsync()).Length;
        var ownerSize = (await (await client.GetAsync($"/v1/public/users/{username}/resume")).Content.ReadAsByteArrayAsync()).Length;

        Assert.True(restrictedSize < openSize, "hiding a section did not shrink the stranger's resume");
        Assert.True(ownerSize > restrictedSize, "the owner's own resume lost content they can see");
    }

    // ── Contributions heatmap ────────────────────────────────────────────────

    [Fact]
    public async Task Contributions_bucket_activity_by_day_with_an_intensity_level()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);
        var when = DateTime.UtcNow.AddDays(-20);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Three participations on the same day → one bucket with a count of 3.
            for (var i = 0; i < 3; i++)
                await SeedEventWithRoleAsync(db, userId, "competitor", when);
        }

        var anon = _factory.CreateClient();
        var contributions = await Json(await anon.GetAsync($"/v1/public/users/{username}/contributions"));

        Assert.Equal(3, contributions.GetProperty("total").GetInt32());
        var day = contributions.GetProperty("days").EnumerateArray().Single();
        Assert.Equal(3, day.GetProperty("count").GetInt32());
        Assert.Equal(2, day.GetProperty("level").GetInt32());   // 2–3 contributions = level 2
        Assert.Equal(when.ToString("yyyy-MM-dd"), day.GetProperty("date").GetString());
    }

    /// <summary>The subtlest privacy trap in the whole system: if the engine bucketed first and gated
    /// afterwards, the day would still appear with its original intensity and the viewer could read
    /// the presence of hidden activity straight off the grid.</summary>
    [Fact]
    public async Task A_day_whose_only_activity_is_hidden_disappears_entirely()
    {
        var (client, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await SeedEventWithRoleAsync(db, userId, "speaker", DateTime.UtcNow.AddDays(-15));
        }

        var anon = _factory.CreateClient();
        var before = await Json(await anon.GetAsync($"/v1/public/users/{username}/contributions"));
        Assert.Equal(1, before.GetProperty("total").GetInt32());

        await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { ["events"] = "only_me" },
        });

        var after = await Json(await anon.GetAsync($"/v1/public/users/{username}/contributions"));
        // Not "count reduced" — the day must be gone. A remaining bucket of any intensity would leak
        // that something happened on it.
        Assert.Equal(0, after.GetProperty("total").GetInt32());
        Assert.Empty(after.GetProperty("days").EnumerateArray());

        // The owner still sees their own day.
        var owner = await Json(await client.GetAsync($"/v1/public/users/{username}/contributions"));
        Assert.Equal(1, owner.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Contributions_outside_the_window_are_excluded()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await SeedEventWithRoleAsync(db, userId, "competitor", DateTime.UtcNow.AddDays(-400));
            await SeedEventWithRoleAsync(db, userId, "competitor", DateTime.UtcNow.AddDays(-10));
        }

        var anon = _factory.CreateClient();
        var contributions = await Json(await anon.GetAsync($"/v1/public/users/{username}/contributions?months=12"));

        Assert.Equal(1, contributions.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Contributions_are_404_for_a_hidden_profile()
    {
        var (client, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);
        await client.PatchAsJsonAsync("/v1/me/privacy", new { profilePublic = false });

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await anon.GetAsync($"/v1/public/users/{username}/contributions")).StatusCode);
    }
}
