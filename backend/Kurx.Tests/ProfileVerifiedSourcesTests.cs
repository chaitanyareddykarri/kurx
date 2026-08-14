using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-222/D-223 — the verified sources that existed in the backend but never reached the
/// profile (competition results, event assignments, speaker sessions), and the Professional Journey
/// derived from them.
///
/// <para>Each source already had a real person FK and an HTTP surface of its own; these tests prove
/// the profile now projects them, that each is gated by the right section, and that the Journey is a
/// first-attainment ladder rather than a second timeline.</para></summary>
public class ProfileVerifiedSourcesTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public ProfileVerifiedSourcesTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9200{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<string> ClaimAsync(Guid userId)
    {
        var username = "src" + Guid.NewGuid().ToString("N")[..15];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Username = username;
        user.ProfilePublic = true;
        await db.SaveChangesAsync();
        return username;
    }

    /// <summary>Seeds a published past event and returns (eventId, orgId).</summary>
    private async Task<(Guid EventId, Guid OrgId)> SeedEventAsync(KurxDbContext db, Guid createdBy, DateTime startsAt,
        bool isPersonal = false)
    {
        var org = new Organization
        {
            Name = "Src Org " + Guid.NewGuid().ToString("N")[..6],
            Slug = "srcorg" + Guid.NewGuid().ToString("N")[..10],
            IsPersonal = isPersonal,
        };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "src-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Src Cat", Slug = "src-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = createdBy,
            Title = "Source Event " + Guid.NewGuid().ToString("N")[..5],
            Slug = "srcev-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = EventVisibility.Listed,
            StartsAt = startsAt, EndsAt = startsAt.AddDays(1),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (ev.Id, org.Id);
    }

    // ── Competition results (D-222) ──────────────────────────────────────────

    [Fact]
    public async Task A_published_competition_win_appears_as_a_result_and_an_achievement()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-20));
            var stage = new Stage { EventId = eventId, Name = "Grand Finale", Sequence = 1 };
            db.Stages.Add(stage);
            await db.SaveChangesAsync();

            db.StageResults.Add(new StageResult
            {
                StageId = stage.Id, SubjectType = CompetitionSubjectType.Person, SubjectId = userId,
                Rank = 1, FinalScore = 98.5m, State = ResultState.Published,
                PublishedAt = DateTime.UtcNow.AddDays(-19),
            });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var competitions = await Json(await anon.GetAsync($"/v1/public/users/{username}/competitions"));
        var first = competitions.EnumerateArray().Single();
        Assert.Equal(1, first.GetProperty("rank").GetInt32());
        Assert.Equal("Grand Finale", first.GetProperty("stage_name").GetString());

        // D-203's extension point: a third achievement source, same shape, no consumer change.
        var profile = await Json(await anon.GetAsync($"/v1/public/users/{username}"));
        var achievements = profile.GetProperty("achievements").EnumerateArray().ToList();
        Assert.Contains(achievements, a => a.GetProperty("source").GetString() == "competition"
            && a.GetProperty("name").GetString() == "Winner");
    }

    /// <summary>A provisional or disputed result is not yet a fact about the person, so it must not
    /// surface at all — an unpublished placement appearing on a profile would be the worst kind of
    /// overclaim on a trust product.</summary>
    [Fact]
    public async Task An_unpublished_competition_result_never_surfaces()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-20));
            // Two stages, not two rows in one: stage_results carries a DB-enforced unique constraint on
            // (StageId, SubjectType, SubjectId) — one result per subject per stage, ever.
            var prelims = new Stage { EventId = eventId, Name = "Prelims", Sequence = 1 };
            var finals = new Stage { EventId = eventId, Name = "Finals", Sequence = 2 };
            db.Stages.AddRange(prelims, finals);
            await db.SaveChangesAsync();

            db.StageResults.AddRange(
                new StageResult { StageId = prelims.Id, SubjectType = CompetitionSubjectType.Person, SubjectId = userId, Rank = 1, State = ResultState.Provisional },
                new StageResult { StageId = finals.Id, SubjectType = CompetitionSubjectType.Person, SubjectId = userId, Rank = 2, State = ResultState.Disputed });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var competitions = await Json(await anon.GetAsync($"/v1/public/users/{username}/competitions"));
        Assert.Empty(competitions.EnumerateArray());
    }

    // ── Assignments (D-222) ──────────────────────────────────────────────────

    [Fact]
    public async Task A_completed_assignment_appears_with_its_role_and_completion()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);
        var completedAt = DateTime.UtcNow.AddDays(-3);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, orgId) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-10));
            db.EventAssignments.Add(new EventAssignment
            {
                EventId = eventId, OrgId = orgId, UserId = userId, Role = "Judge",
                Status = AssignmentStatus.Completed, CompletedAt = completedAt, ShowOnProfile = true,
            });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var assignments = await Json(await anon.GetAsync($"/v1/public/users/{username}/assignments"));
        var first = assignments.EnumerateArray().Single();
        Assert.Equal("Judge", first.GetProperty("role").GetString());
        Assert.Equal("Completed", first.GetProperty("status").GetString());
        Assert.False(first.GetProperty("completed_at").ValueKind == JsonValueKind.Null);
    }

    /// <summary>A self-represented event has no organization (D-268), and the row that satisfies the FK
    /// is named after the person — so emitting it would print someone's own name on their public profile
    /// as the institution that ran the event. All three cards join Organizations for this label; this
    /// pins the one that is easiest to reach. The card must still appear: filtering the join instead of
    /// the label would delete the evidence rather than the attribution.</summary>
    [Fact]
    public async Task A_self_represented_event_names_no_organization_but_keeps_the_card()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, orgId) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-10), isPersonal: true);
            db.EventAssignments.Add(new EventAssignment
            {
                EventId = eventId, OrgId = orgId, UserId = userId, Role = "Judge",
                Status = AssignmentStatus.Accepted, ShowOnProfile = true,
            });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var assignments = await Json(await anon.GetAsync($"/v1/public/users/{username}/assignments"));
        var first = assignments.EnumerateArray().Single();
        Assert.Equal("Judge", first.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("org_name").ValueKind);
    }

    /// <summary>The other half of the pair: a real represented institution IS named. Without this the
    /// test above passes just as happily against a projection that dropped the field entirely.</summary>
    [Fact]
    public async Task A_represented_organization_is_still_named_on_the_card()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, orgId) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-10));
            db.EventAssignments.Add(new EventAssignment
            {
                EventId = eventId, OrgId = orgId, UserId = userId, Role = "Mentor",
                Status = AssignmentStatus.Accepted, ShowOnProfile = true,
            });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var assignments = await Json(await anon.GetAsync($"/v1/public/users/{username}/assignments"));
        var first = assignments.EnumerateArray().Single();
        Assert.StartsWith("Src Org ", first.GetProperty("org_name").GetString());
    }

    /// <summary>The per-row opt-out composes with the section tier as AND — a section set to public
    /// must never override a row the user chose to hide.</summary>
    [Fact]
    public async Task An_assignment_with_show_on_profile_false_stays_hidden()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, orgId) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-10));
            db.EventAssignments.Add(new EventAssignment
            {
                EventId = eventId, OrgId = orgId, UserId = userId, Role = "Volunteer",
                Status = AssignmentStatus.Completed, ShowOnProfile = false,
            });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var assignments = await Json(await anon.GetAsync($"/v1/public/users/{username}/assignments"));
        Assert.Empty(assignments.EnumerateArray());
    }

    // ── Speaker sessions (D-222) ─────────────────────────────────────────────

    [Fact]
    public async Task A_linked_speaker_session_appears_and_sets_the_speaker_trust_signal()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, orgId) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-15));

            var speaker = new Speaker { OrgId = orgId, Name = "Asha", UserId = userId };
            db.Speakers.Add(speaker);
            var session = new EventSession
            {
                EventId = eventId, Title = "Offline-first Flutter", Kind = ScheduleItemKind.Session,
                StartsAt = DateTime.UtcNow.AddDays(-15), EndsAt = DateTime.UtcNow.AddDays(-15).AddHours(1),
            };
            db.EventSessions.Add(session);
            await db.SaveChangesAsync();

            db.EventSessionSpeakers.Add(new EventSessionSpeaker { SessionId = session.Id, SpeakerId = speaker.Id });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var sessions = await Json(await anon.GetAsync($"/v1/public/users/{username}/sessions"));
        Assert.Equal("Offline-first Flutter", sessions.EnumerateArray().Single().GetProperty("session_title").GetString());

        // The same organizer-made link is what earns the Speaker Verified badge (D-221).
        var profile = await Json(await anon.GetAsync($"/v1/public/users/{username}"));
        Assert.True(profile.GetProperty("verification").GetProperty("speaker_verified").GetBoolean());
    }

    /// <summary>A Break is scheduling furniture, not a talk anyone gave.</summary>
    [Fact]
    public async Task A_break_is_not_counted_as_a_speaking_session()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, orgId) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-15));
            var speaker = new Speaker { OrgId = orgId, Name = "Asha", UserId = userId };
            db.Speakers.Add(speaker);
            var session = new EventSession
            {
                EventId = eventId, Title = "Lunch", Kind = ScheduleItemKind.Break,
                StartsAt = DateTime.UtcNow.AddDays(-15), EndsAt = DateTime.UtcNow.AddDays(-15).AddHours(1),
            };
            db.EventSessions.Add(session);
            await db.SaveChangesAsync();
            db.EventSessionSpeakers.Add(new EventSessionSpeaker { SessionId = session.Id, SpeakerId = speaker.Id });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var sessions = await Json(await anon.GetAsync($"/v1/public/users/{username}/sessions"));
        Assert.Empty(sessions.EnumerateArray());
    }

    // ── Professional Journey (D-223) ─────────────────────────────────────────

    /// <summary>The flagship. Nodes are ordered by when each tier was first reached — deliberately not
    /// by a canonical career ladder, because someone can organize before ever volunteering.</summary>
    [Fact]
    public async Task The_journey_is_ordered_by_first_attainment_not_by_a_canonical_ladder()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            // Organized first (2 years ago), volunteered later (1 year ago) — the reverse of the
            // "expected" career order, which is exactly what must be preserved.
            var (organizedEvent, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-730));
            var (volunteeredEvent, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-365));

            db.EventParticipants.AddRange(
                new EventParticipant
                {
                    EventId = organizedEvent, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
                    RoleSlug = "manager", State = ParticipantState.Completed, Visibility = ParticipantVisibility.Public,
                },
                new EventParticipant
                {
                    EventId = volunteeredEvent, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
                    RoleSlug = "volunteer", State = ParticipantState.Completed, Visibility = ParticipantVisibility.Public,
                });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var journey = (await Json(await anon.GetAsync($"/v1/public/users/{username}/journey"))).EnumerateArray().ToList();

        Assert.Equal(2, journey.Count);
        Assert.Equal("organizer", journey[0].GetProperty("tier").GetString());
        Assert.Equal("volunteer", journey[1].GetProperty("tier").GetString());
        // Every node names the row that proved it.
        Assert.Equal("event_participant", journey[0].GetProperty("evidence").GetProperty("kind").GetString());
        Assert.False(string.IsNullOrEmpty(journey[0].GetProperty("evidence").GetProperty("event_slug").GetString()));
    }

    /// <summary>Two roles on one event is one occurrence of that tier — the same dedup rule every other
    /// count on this profile uses (D-201).</summary>
    [Fact]
    public async Task Journey_occurrences_dedupe_by_event()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-100));
            // Same person, same event, two participant-class roles.
            db.EventParticipants.AddRange(
                new EventParticipant { EventId = eventId, SubjectType = ParticipantSubjectType.Person, SubjectId = userId, RoleSlug = "attendee", State = ParticipantState.Completed, Visibility = ParticipantVisibility.Public },
                new EventParticipant { EventId = eventId, SubjectType = ParticipantSubjectType.Person, SubjectId = userId, RoleSlug = "competitor", State = ParticipantState.Completed, Visibility = ParticipantVisibility.Public });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var journey = (await Json(await anon.GetAsync($"/v1/public/users/{username}/journey"))).EnumerateArray().ToList();
        var participant = journey.Single(n => n.GetProperty("tier").GetString() == "participant");
        Assert.Equal(1, participant.GetProperty("occurrences").GetInt32());
    }

    /// <summary>A user with nothing has an empty journey — the honest output, not a failure and not a
    /// set of aspirational placeholders.</summary>
    [Fact]
    public async Task A_user_with_no_activity_has_an_empty_journey()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        var anon = _factory.CreateClient();
        var journey = await Json(await anon.GetAsync($"/v1/public/users/{username}/journey"));
        Assert.Empty(journey.EnumerateArray());
    }

    /// <summary>A tier whose gating section is hidden must be dropped entirely — the Journey must not
    /// become a side channel around the visibility resolver.</summary>
    [Fact]
    public async Task A_journey_tier_is_omitted_when_its_gating_section_is_hidden()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (eventId, _) = await SeedEventAsync(db, userId, DateTime.UtcNow.AddDays(-30));
            var stage = new Stage { EventId = eventId, Name = "Finals", Sequence = 1 };
            db.Stages.Add(stage);
            await db.SaveChangesAsync();
            db.StageResults.Add(new StageResult
            {
                StageId = stage.Id, SubjectType = CompetitionSubjectType.Person, SubjectId = userId,
                Rank = 1, State = ResultState.Published, PublishedAt = DateTime.UtcNow.AddDays(-29),
            });
            await db.SaveChangesAsync();
        }

        var anon = _factory.CreateClient();
        var before = (await Json(await anon.GetAsync($"/v1/public/users/{username}/journey"))).EnumerateArray().ToList();
        Assert.Contains(before, n => n.GetProperty("tier").GetString() == "competition_winner");

        // Achievements gates the competition_winner tier.
        await client.PatchAsJsonAsync("/v1/me/privacy", new
        {
            sections = new Dictionary<string, string> { ["achievements"] = "only_me" },
        });

        var after = (await Json(await anon.GetAsync($"/v1/public/users/{username}/journey"))).EnumerateArray().ToList();
        Assert.DoesNotContain(after, n => n.GetProperty("tier").GetString() == "competition_winner");
    }

    [Fact]
    public async Task The_new_sub_resources_are_404_for_a_hidden_profile()
    {
        var (client, userId) = await LoginAsync(NextPhone());
        var username = await ClaimAsync(userId);
        await client.PatchAsJsonAsync("/v1/me/privacy", new { profilePublic = false });

        var anon = _factory.CreateClient();
        foreach (var path in new[] { "competitions", "assignments", "sessions", "journey" })
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await anon.GetAsync($"/v1/public/users/{username}/{path}")).StatusCode);
        }
    }
}
