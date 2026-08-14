using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

public class EventTaxonomyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public EventTaxonomyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                // Seed twice: proves the seeder is idempotent (re-running inserts no duplicate rows)
                // and leaves the DB in the exact-count state the assertions below rely on.
                Seed();
                Seed();
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private void Seed()
    {
        using var scope = _factory.Services.CreateScope();
        EventTaxonomySeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<KurxDbContext>())
            .GetAwaiter().GetResult();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Seeds_exact_tier_counts_and_no_duplicates_on_rerun()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var audiences = await db.EventCategories.CountAsync(c => c.Level == CategoryLevel.Audience);
        var categories = await db.EventCategories.CountAsync(c => c.Level == CategoryLevel.Category);
        var typeRows = await db.EventCategories.CountAsync(c => c.Level == CategoryLevel.Type);
        var logicalTypes = await db.EventCategories.Where(c => c.Level == CategoryLevel.Type)
            .Select(c => c.Name).Distinct().CountAsync();

        Assert.Equal(3, audiences);

        // D-266 M3 Step 6: the canonical taxonomy is 104 LOGICAL types. Row count is higher because a type
        // that belongs under more than one audience keeps a navigation node per audience (D-023's tree),
        // which is why the two are asserted separately rather than as one magic number.
        Assert.Equal(104, logicalTypes);

        // The row total is proved, not hardcoded: rows = logical types + one extra node per duplicate.
        var duplicateNodes = await db.EventCategories.Where(c => c.Level == CategoryLevel.Type)
            .GroupBy(c => c.Name).Where(g => g.Count() > 1).CountAsync();
        Assert.Equal(logicalTypes + duplicateNodes, typeRows);

        // And the grand total is the three tiers, nothing else.
        Assert.Equal(audiences + categories + typeRows, await db.EventCategories.CountAsync());

        // Slugs are globally unique — the seeder must never produce a collision (D-023).
        var slugs = await db.EventCategories.Select(c => c.Slug).ToListAsync();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    [Fact]
    public async Task Parent_child_links_and_collision_prefixes_are_correct()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var student = await db.EventCategories.SingleAsync(c => c.Slug == "student-events");
        Assert.Equal(CategoryLevel.Audience, student.Level);
        Assert.Null(student.ParentId);

        var competitions = await db.EventCategories.SingleAsync(c => c.Slug == "competitions");
        Assert.Equal(CategoryLevel.Category, competitions.Level);
        Assert.Equal(student.Id, competitions.ParentId);

        var hackathon = await db.EventCategories.SingleAsync(c => c.Slug == "hackathon");
        Assert.Equal(CategoryLevel.Type, hackathon.Level);
        Assert.Equal(competitions.Id, hackathon.ParentId);

        // D-266 M3 Step 6 — exactly these seven canonical types keep a navigation node under more than
        // one audience. Asserted as a closed set: an eighth duplicate means the taxonomy grew a node it
        // should not have, which is the defect that broke seeding during the migration.
        var duplicates = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type)
            .GroupBy(c => c.Name).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToListAsync();

        Assert.Equal(
            new[] { "Career Fair", "Networking Event", "Running Event", "Seminar",
                    "Sports Tournament", "Startup Meetup", "Workshop" },
            duplicates.OrderBy(x => x, StringComparer.Ordinal));

        // Every duplicated name is audience-prefixed and the bare slug never exists (D-023's rule).
        foreach (var name in duplicates)
        {
            var bare = new string(name.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
            Assert.False(await db.EventCategories.AnyAsync(c => c.Slug == bare),
                $"'{name}' is duplicated, so the bare slug '{bare}' must not exist");
        }

        // "Running Event" is the post-D11 name for Marathon; both audience branches keep their own node.
        var studentRun = await db.EventCategories.SingleAsync(c => c.Slug == "student-running-event");
        var publicRun = await db.EventCategories.SingleAsync(c => c.Slug == "public-running-event");
        var studentSports = await db.EventCategories.SingleAsync(c => c.Slug == "sports-gaming");
        var publicSports = await db.EventCategories.SingleAsync(c => c.Slug == "sports-fitness");
        Assert.Equal(studentSports.Id, studentRun.ParentId);
        Assert.Equal(publicSports.Id, publicRun.ParentId);
    }

    [Fact]
    public async Task Categories_endpoint_returns_the_seeded_category_tier()
    {
        var anon = _factory.CreateClient();
        var categories = await Json(await anon.GetAsync("/v1/categories?level=Category"));
        Assert.Equal(13, categories.GetArrayLength());
        var slugs = categories.EnumerateArray().Select(c => c.GetProperty("slug").GetString()).ToList();
        Assert.Contains("competitions", slugs);
        Assert.Contains("family-celebrations", slugs);
    }
}
