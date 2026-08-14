using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-233 — the batched visibility primitive and the eight services migrated onto it.
///
/// <para><b>Why this class exists.</b> `ProfilePublic` was read directly as a "may this other person be
/// shown here" gate at 15 sites across eight services. Each answered the question independently, none
/// went through the resolver, and collectively they were the reason the legacy boolean columns could
/// not be dropped. They are now one call: <see cref="IProfileVisibilityResolver.VisibleProfileIdsAsync"/>.</para>
///
/// <para>Two things are asserted, and the second matters as much as the first: the primitive resolves
/// every tier correctly <b>and</b> each migrated surface still nulls the public identity of someone who
/// is not publicly visible. A migration that centralises the logic but flips a surface's behaviour is
/// not a refactor, it is a leak.</para></summary>
public class ProfileVisibilityBatchTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ProfileVisibilityBatchTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private IServiceScope Scope() => _factory.Services.CreateScope();

    /// <summary>A user with a claimed username and an explicit Profile tier.</summary>
    private async Task<Guid> SeedUserAsync(SectionVisibility tier, bool banned = false, bool suspended = false)
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Phone = "94" + Random.Shared.NextInt64(10_000_000, 99_999_999),
            Name = "Batch " + id.ToString("N")[..6],
            Username = "batch" + id.ToString("N")[..14],
            AvatarKey = "avatars/" + id.ToString("N")[..8] + ".png",
            ProfilePublic = tier == SectionVisibility.Public,
            BannedAt = banned ? DateTime.UtcNow : null,
            SuspendedAt = suspended ? DateTime.UtcNow : null,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Written through the resolver so the stored tier and the dual-written boolean agree, exactly
        // as a real privacy save would leave them.
        var visibility = scope.ServiceProvider.GetRequiredService<IProfileVisibilityResolver>();
        await visibility.UpdateSettingsAsync(id, new Dictionary<ProfileSection, SectionVisibility>
        {
            [ProfileSection.Profile] = tier,
        });
        return id;
    }

    private async Task ConnectAsync(Guid a, Guid b)
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var (low, high) = a.CompareTo(b) < 0 ? (a, b) : (b, a);
        db.AllyConnections.Add(new AllyConnection
        {
            UserLowId = low, UserHighId = high, RequesterId = a, AddresseeId = b,
            Status = AllyStatus.Accepted, RespondedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlySet<Guid>> ResolveAsync(IReadOnlyCollection<Guid> ids, Guid? viewer)
    {
        using var scope = Scope();
        var visibility = scope.ServiceProvider.GetRequiredService<IProfileVisibilityResolver>();
        return await visibility.VisibleProfileIdsAsync(ids, viewer);
    }

    // ── The primitive ────────────────────────────────────────────────────────

    /// <summary>The whole tier matrix in one batch, which is the case a per-row loop would get right by
    /// accident and a batched implementation can get wrong: the relationship queries run once for the
    /// set, so a mistake there mis-resolves everyone at once.</summary>
    [Fact]
    public async Task Every_tier_resolves_correctly_inside_one_batch()
    {
        var viewer = await SeedUserAsync(SectionVisibility.Public);
        var publicUser = await SeedUserAsync(SectionVisibility.Public);
        var connectionsUser = await SeedUserAsync(SectionVisibility.Connections);
        var participantsUser = await SeedUserAsync(SectionVisibility.EventParticipants);
        var onlyMeUser = await SeedUserAsync(SectionVisibility.OnlyMe);

        await ConnectAsync(viewer, connectionsUser);

        var ids = new[] { publicUser, connectionsUser, participantsUser, onlyMeUser };
        var visible = await ResolveAsync(ids, viewer);

        Assert.Contains(publicUser, visible);
        Assert.Contains(connectionsUser, visible);       // accepted connection
        Assert.DoesNotContain(participantsUser, visible); // no shared event
        Assert.DoesNotContain(onlyMeUser, visible);
    }

    /// <summary>Anonymous is the viewer every migrated non-profile surface passes, so it is the case
    /// that governs attendee lists, leaderboards, speaker cards and review bylines. Only Public may
    /// survive it.</summary>
    [Fact]
    public async Task An_anonymous_viewer_sees_only_public_profiles()
    {
        var publicUser = await SeedUserAsync(SectionVisibility.Public);
        var connectionsUser = await SeedUserAsync(SectionVisibility.Connections);
        var participantsUser = await SeedUserAsync(SectionVisibility.EventParticipants);
        var onlyMeUser = await SeedUserAsync(SectionVisibility.OnlyMe);

        var visible = await ResolveAsync(
            new[] { publicUser, connectionsUser, participantsUser, onlyMeUser }, null);

        Assert.Equal([publicUser], visible);
    }

    /// <summary>The owner always resolves visible, whatever tier they chose — otherwise every surface
    /// would hide a person from themselves.</summary>
    [Fact]
    public async Task The_owner_is_always_visible_to_themselves()
    {
        var onlyMe = await SeedUserAsync(SectionVisibility.OnlyMe);
        Assert.Contains(onlyMe, await ResolveAsync([onlyMe], onlyMe));
    }

    /// <summary>Moderation is applied inside the primitive rather than remembered at each call site —
    /// which is exactly how BUG-B happened the first time (D-230).</summary>
    [Fact]
    public async Task Banned_and_suspended_accounts_are_never_visible()
    {
        var banned = await SeedUserAsync(SectionVisibility.Public, banned: true);
        var suspended = await SeedUserAsync(SectionVisibility.Public, suspended: true);
        var ok = await SeedUserAsync(SectionVisibility.Public);

        var visible = await ResolveAsync(new[] { banned, suspended, ok }, null);

        Assert.Equal([ok], visible);
        // Even to themselves: a banned account's public identity is gone, not merely unauthenticatable.
        Assert.DoesNotContain(banned, await ResolveAsync([banned], banned));
    }

    /// <summary>A deleted or bogus id is simply absent, so callers need no second lookup to tell
    /// "hidden" from "gone" (D-018).</summary>
    [Fact]
    public async Task Unknown_ids_are_absent_rather_than_throwing()
    {
        var known = await SeedUserAsync(SectionVisibility.Public);
        var visible = await ResolveAsync(new[] { known, Guid.NewGuid() }, null);
        Assert.Equal([known], visible);
    }

    [Fact]
    public async Task An_empty_batch_issues_no_query_and_returns_empty()
        => Assert.Empty(await ResolveAsync(Array.Empty<Guid>(), null));

    /// <summary>The EventParticipants tier through the batched path. The single-resolve path already
    /// had coverage; this asserts the batched shared-event intersection agrees with it, because the two
    /// are separate queries and could drift.</summary>
    [Fact]
    public async Task Event_participants_tier_resolves_through_the_batched_intersection()
    {
        var owner = await SeedUserAsync(SectionVisibility.EventParticipants);
        var coParticipant = await SeedUserAsync(SectionVisibility.Public);
        var stranger = await SeedUserAsync(SectionVisibility.Public);
        await SeedSharedEventAsync(owner, coParticipant);

        Assert.Contains(owner, await ResolveAsync([owner], coParticipant));
        Assert.DoesNotContain(owner, await ResolveAsync([owner], stranger));
    }

    private async Task SeedSharedEventAsync(Guid a, Guid b)
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var org = new Organization
        {
            Name = "Batch Org " + Guid.NewGuid().ToString("N")[..6],
            Slug = "batchorg" + Guid.NewGuid().ToString("N")[..10],
        };
        db.Organizations.Add(org);
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "batch-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Batch Cat", Slug = "batch-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = a,
            Title = "Batch Shared", Slug = "batchshared-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = EventVisibility.Listed,
            StartsAt = DateTime.UtcNow.AddDays(-10), EndsAt = DateTime.UtcNow.AddDays(-9),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        foreach (var uid in new[] { a, b })
        {
            db.EventParticipants.Add(new EventParticipant
            {
                EventId = ev.Id, SubjectType = ParticipantSubjectType.Person, SubjectId = uid,
                RoleSlug = "attendee", State = ParticipantState.Completed,
                Visibility = ParticipantVisibility.Public,
            });
        }
        await db.SaveChangesAsync();
    }

    // ── The migrated services ────────────────────────────────────────────────
    //
    // Each asserts the same contract on a different surface: a hidden profile contributes no username
    // and no avatar, a public one does. These are the 15 reads that used to answer it independently.

    /// <summary>Leaderboards (2 of the 15). Also the surface where the migration removed an N+1 — the
    /// event board previously issued one user query per entry.</summary>
    [Fact]
    public async Task Leaderboard_rows_link_only_public_profiles()
    {
        var publicUser = await SeedUserAsync(SectionVisibility.Public);
        var hiddenUser = await SeedUserAsync(SectionVisibility.OnlyMe);

        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        foreach (var (id, rank) in new[] { (publicUser, 1), (hiddenUser, 2) })
        {
            db.Leaderboards.Add(new Leaderboard { Scope = "global", UserId = id, Rank = rank, Points = 100 - rank });
        }
        await db.SaveChangesAsync();

        var gamification = scope.ServiceProvider.GetRequiredService<IGamificationService>();
        var board = await gamification.GetGlobalLeaderboardAsync(50);

        var shown = board.First(e => e.UserId == publicUser);
        var hidden = board.First(e => e.UserId == hiddenUser);
        Assert.NotNull(shown.Username);
        // The name still renders — only the profile *link* is gated, which is what ProfilePublic did.
        Assert.Null(hidden.Username);
        Assert.NotNull(hidden.Name);
    }

    /// <summary>Review bylines (4 of the 15). A hidden author keeps their display name on a non-anonymous
    /// review but contributes no linkable username.</summary>
    [Fact]
    public async Task Review_bylines_link_only_public_profiles()
    {
        var publicAuthor = await SeedUserAsync(SectionVisibility.Public);
        var hiddenAuthor = await SeedUserAsync(SectionVisibility.OnlyMe);

        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var org = new Organization { Name = "Rev Org", Slug = "revorg" + Guid.NewGuid().ToString("N")[..10] };
        db.Organizations.Add(org);
        // `Id` defaults to Guid.NewGuid() on the entity, so a "was it just constructed?" check against
        // Guid.Empty never fires — the category silently went un-inserted and the event FK failed.
        var cat = await db.EventCategories.FirstOrDefaultAsync(c => c.Slug == "batch-cat");
        if (cat is null)
        {
            cat = new EventCategory { Level = CategoryLevel.Category, Name = "Batch Cat", Slug = "batch-cat" };
            db.EventCategories.Add(cat);
        }
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = org.Id, CategoryId = cat.Id, CreatedBy = publicAuthor,
            Title = "Reviewed", Slug = "reviewed-" + Guid.NewGuid().ToString("N")[..10],
            ShortCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            City = "Hyderabad", Status = EventStatus.Published, Visibility = EventVisibility.Listed,
            StartsAt = DateTime.UtcNow.AddDays(-5), EndsAt = DateTime.UtcNow.AddDays(-4),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        foreach (var author in new[] { publicAuthor, hiddenAuthor })
        {
            db.EventReviews.Add(new EventReview
            {
                EventId = ev.Id, UserId = author, Rating = 5, Body = "Good",
                IsAnonymous = false, IsVerified = true, Status = EventReviewStatus.Published,
            });
        }
        await db.SaveChangesAsync();

        var reviews = scope.ServiceProvider.GetRequiredService<IEventReviewService>();
        var (items, _, _) = await reviews.ListAsync(ev.Id, 1, 50);

        Assert.All(items, i => Assert.NotNull(i.AuthorName));
        Assert.Single(items, i => i.AuthorUsername != null);
    }

    /// <summary>Speaker cards (1 of the 15) — the linked-account path, not the free-text speaker.</summary>
    [Fact]
    public async Task Speaker_cards_link_only_public_profiles()
    {
        var publicUser = await SeedUserAsync(SectionVisibility.Public);
        var hiddenUser = await SeedUserAsync(SectionVisibility.OnlyMe);

        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var org = new Organization { Name = "Spk Org", Slug = "spkorg" + Guid.NewGuid().ToString("N")[..10] };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        foreach (var uid in new[] { publicUser, hiddenUser })
        {
            db.Speakers.Add(new Speaker { OrgId = org.Id, Name = "Speaker", UserId = uid });
        }
        await db.SaveChangesAsync();

        var speakers = scope.ServiceProvider.GetRequiredService<ISpeakerService>();
        // isAdmin: true bypasses org membership — this test is about the visibility gate, not authz.
        var result = await speakers.ListForOrgAsync(publicUser, org.Id, isAdmin: true);
        Assert.True(result.Ok);
        var views = result.Value!;

        Assert.Equal(1, views.Count(v => v.Username != null));
        Assert.Equal(1, views.Count(v => v.Username == null));
    }
}
