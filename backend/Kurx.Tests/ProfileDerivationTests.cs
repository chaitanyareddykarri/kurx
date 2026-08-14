using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-225 — the derivation engines: identity labels + headline, experience, and metrics.
///
/// <para>The properties worth pinning are the ones a reader would be misled by if they broke: that a
/// derived value never contradicts the facts beside it, that hidden sections shrink counts rather
/// than leaking through them, and that a person with no activity gets honest emptiness rather than a
/// fabricated floor.</para></summary>
public class ProfileDerivationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileDerivationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9202{Interlocked.Increment(ref _phoneSeq):D6}";

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

    private async Task<string> ClaimAsync(Guid userId)
    {
        var username = "der" + Guid.NewGuid().ToString("N")[..15];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Username = username;
        user.ProfilePublic = true;
        await db.SaveChangesAsync();
        return username;
    }

    private async Task<(Guid EventId, Guid OrgId)> SeedEventAsync(
        KurxDbContext db, Guid createdBy, DateTime startsAt, string? kindSlug = null)
    {
        var org = new Organization
        {
            Name = "Der Org " + Guid.NewGuid().ToString("N")[..6],
            Slug = "derorg" + Guid.NewGuid().ToString("N")[..10],
        };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "der-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Der Cat", Slug = "der-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = createdBy,
            Title = "Der Event " + Guid.NewGuid().ToString("N")[..5],
            Slug = "derev-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = EventVisibility.Listed,
            KindSlug = kindSlug,
            StartsAt = startsAt, EndsAt = startsAt.AddDays(1),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (ev.Id, org.Id);
    }

    /// <summary><c>Ticket.OrderItemId</c> is a real FK — a valid ticket needs a TicketType/Order/
    /// OrderItem chain underneath it (the same fixture shape <see cref="PublicProfileTests"/> uses).
    /// A bare <c>Guid.NewGuid()</c> fails with 23503, which is the schema being right.</summary>
    private static async Task SeedCheckedInTicketAsync(KurxDbContext db, Guid eventId, Guid userId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ticketType = new TicketType
        {
            EventId = eventId, Name = "General " + suffix, PricePaise = 0, Quantity = 1000, Sold = 0,
            SaleStarts = DateTime.UtcNow.AddDays(-30), SaleEnds = DateTime.UtcNow.AddDays(30),
        };
        db.TicketTypes.Add(ticketType);
        var order = new Order
        {
            UserId = userId, EventId = eventId, TicketTypeId = ticketType.Id,
            Status = OrderStatus.Paid, AmountPaise = 0, RazorpayOrderId = "seed_" + suffix,
        };
        db.Orders.Add(order);
        var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = 1, UnitPricePaise = 0 };
        db.OrderItems.Add(item);
        db.Tickets.Add(new Ticket
        {
            OrderItemId = item.Id, EventId = eventId, UserId = userId, HmacSig = "seed",
            State = TicketState.CheckedIn, CheckedInAt = DateTime.UtcNow.AddDays(-10),
        });
        await db.SaveChangesAsync();
    }

    private static EventParticipant Participation(Guid eventId, Guid userId, string slug) => new()
    {
        EventId = eventId, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
        RoleSlug = slug, State = ParticipantState.Completed, Visibility = ParticipantVisibility.Public,
    };

    // ── Identity + headline ──────────────────────────────────────────────────

    /// <summary>The headline is the short form of the labels — it must never contain a token the label
    /// pool does not, or the profile would assert an identity nothing backs.</summary>
    [Fact]
    public async Task The_headline_only_ever_contains_earned_labels()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-30));
            db.EventParticipants.AddRange(
                Participation(eventId, userId, "speaker"),
                Participation(eventId, userId, "manager"));
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var profile = await Json(await anon.GetAsync($"/v1/public/users/{username}"));

        var labels = profile.GetProperty("identity_labels").EnumerateArray()
            .Select(l => l.GetString()!).ToList();
        var headline = profile.GetProperty("derived_headline").GetString()!;

        Assert.NotEmpty(labels);
        Assert.NotEmpty(headline);
        foreach (var token in headline.Split(" • "))
            Assert.Contains(token, labels);

        // Provenance must mark it derived — a client keys its badge off this, not off the field name.
        Assert.Equal("derived", profile.GetProperty("_meta").GetProperty("derived_headline").GetString());
        Assert.Equal("self_declared", profile.GetProperty("_meta").GetProperty("headline").GetString());
    }

    [Fact]
    public async Task A_headline_is_at_most_three_tokens_and_sixty_characters()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-30));
            // Enough distinct identities to overflow the cap.
            db.EventParticipants.AddRange(
                Participation(eventId, userId, "owner"),
                Participation(eventId, userId, "manager"),
                Participation(eventId, userId, "judge"),
                Participation(eventId, userId, "mentor"),
                Participation(eventId, userId, "speaker"));
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var headline = (await Json(await anon.GetAsync($"/v1/public/users/{username}")))
            .GetProperty("derived_headline").GetString()!;

        Assert.True(headline.Length <= 60, $"headline too long: '{headline}'");
        Assert.True(headline.Split(" • ").Length <= 3);
    }

    /// <summary>Determinism: a headline that changed between reads would read as unreliable on a page
    /// whose whole claim is reliability.</summary>
    [Fact]
    public async Task The_headline_is_stable_across_reads()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-30));
            db.EventParticipants.AddRange(
                Participation(eventId, userId, "judge"),
                Participation(eventId, userId, "speaker"),
                Participation(eventId, userId, "volunteer"));
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var first = (await Json(await anon.GetAsync($"/v1/public/users/{username}"))).GetProperty("derived_headline").GetString();
        var second = (await Json(await anon.GetAsync($"/v1/public/users/{username}"))).GetProperty("derived_headline").GetString();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task A_user_with_no_activity_has_an_empty_headline_not_a_placeholder()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var profile = await Json(await anon.GetAsync($"/v1/public/users/{username}"));

        Assert.Equal(string.Empty, profile.GetProperty("derived_headline").GetString());
        Assert.Empty(profile.GetProperty("identity_labels").EnumerateArray());
    }

    // ── Experience ───────────────────────────────────────────────────────────

    /// <summary>Below the first threshold it reads "Building", never a fabricated tier — the D-212
    /// precedent.</summary>
    [Fact]
    public async Task Experience_reads_building_below_the_first_threshold()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var experience = await Json(await anon.GetAsync($"/v1/public/users/{username}/experience"));

        Assert.Equal("Building", experience.GetProperty("band").GetString());
        Assert.Equal(0, experience.GetProperty("distinct_events").GetInt32());
    }

    /// <summary>The band always travels with the counts that produced it, so a reader can check it.</summary>
    [Fact]
    public async Task Experience_reports_the_counts_behind_the_band()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            for (var i = 0; i < 4; i++)
            {
                var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-30 - i));
                db.EventParticipants.Add(Participation(eventId, userId, "competitor"));
            }
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var experience = await Json(await anon.GetAsync($"/v1/public/users/{username}/experience"));

        Assert.Equal(4, experience.GetProperty("distinct_events").GetInt32());
        Assert.Equal("Emerging", experience.GetProperty("band").GetString());
        Assert.True(experience.TryGetProperty("years_active", out _));
        Assert.True(experience.TryGetProperty("first_activity_at", out _));
    }

    // ── Metrics ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Metrics_report_the_dna_distribution_and_reach()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (a, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-40), kindSlug: "hackathon");
            var (b, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-30), kindSlug: "hackathon");
            var (c, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-20), kindSlug: "conference");
            db.EventParticipants.AddRange(
                Participation(a, userId, "competitor"),
                Participation(b, userId, "competitor"),
                Participation(c, userId, "speaker"));
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var metrics = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));

        Assert.Equal(3, metrics.GetProperty("events_participated").GetInt32());
        var dna = metrics.GetProperty("event_dna").EnumerateArray().ToList();
        Assert.Equal("hackathon", dna[0].GetProperty("kind").GetString());
        Assert.Equal(2, dna[0].GetProperty("count").GetInt32());
        Assert.Contains("Hyderabad", metrics.GetProperty("cities").EnumerateArray().Select(c => c.GetString()));
    }

    /// <summary>The privacy trap this engine has to avoid: a hidden section must shrink the number,
    /// not be counted and then relabelled. Null means hidden, and must never be rendered as zero.</summary>
    [Fact]
    public async Task A_hidden_section_makes_its_metric_null_rather_than_leaking_a_count()
    {
        var (client, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-10));
            await SeedCheckedInTicketAsync(db, eventId, userId);
        }

        await client.PatchAsJsonAsync("/v1/me/privacy", new { showAttended = true });

        var anon = _factory.CreateClient();
        var visible = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));
        Assert.Equal(1, visible.GetProperty("events_attended").GetInt32());

        await client.PatchAsJsonAsync("/v1/me/privacy", new { showAttended = false });

        var hidden = await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"));
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("events_attended").ValueKind);
    }

    [Fact]
    public async Task Metrics_expose_no_vanity_or_moderation_fields()
    {
        var (_, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var raw = (await Json(await anon.GetAsync($"/v1/public/users/{username}/metrics"))).GetRawText();

        foreach (var forbidden in new[]
                 { "profile_views", "views", "points", "leaderboard", "rank", "percentile",
                   "no_show", "risk", "fraud", "report" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_new_sub_resources_are_404_for_a_hidden_profile()
    {
        var (client, userId) = await LoginAsync();
        var username = await ClaimAsync(userId);
        await client.PatchAsJsonAsync("/v1/me/privacy", new { profilePublic = false });

        var anon = _factory.CreateClient();
        foreach (var path in new[] { "metrics", "experience" })
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await anon.GetAsync($"/v1/public/users/{username}/{path}")).StatusCode);
        }
    }
}
