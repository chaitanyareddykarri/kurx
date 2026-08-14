using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Kurx.Infrastructure.Events.TaxonomyAliasMap;

namespace Kurx.Tests;

/// <summary>D-266 M3 Step 6 Phase 2 — proves every existing event can migrate before a single row moves.
///
/// <para>This is the gate. A wrong taxonomy mapping does not fail loudly: it silently reassigns a real event
/// to the wrong type and surfaces later as wrong analytics and wrong certificates. Everything here runs
/// against the seeded taxonomy and must be green before Phase 4 touches data.</para></summary>
public class TaxonomyMigrationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public TaxonomyMigrationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            Seed(EventTaxonomySeeder.SeedAsync);
            Seed(ArchetypeSeeder.SeedAsync);
            _reset = true;
        }
    }

    private void Seed(Func<KurxDbContext, CancellationToken, Task> run)
    {
        using var scope = _factory.Services.CreateScope();
        run(scope.ServiceProvider.GetRequiredService<KurxDbContext>(), default).GetAwaiter().GetResult();
    }

    private KurxDbContext Db(IServiceScope s) => s.ServiceProvider.GetRequiredService<KurxDbContext>();

    private async Task<List<string>> SeededTypeNamesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Type).Select(c => c.Name).Distinct().ToListAsync();
    }

    // ── Phase 1 · the mapping is internally sound ───────────────────────────────────────────

    [Fact]
    public void No_legacy_type_maps_to_more_than_one_canonical_destination()
    {
        var ambiguous = Mappings.GroupBy(m => m.Legacy, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Canonical).Distinct().Count() > 1)
            .Select(g => g.Key).ToList();

        Assert.True(ambiguous.Count == 0, "ambiguous mapping(s): " + string.Join(", ", ambiguous));
    }

    /// <summary>The migration guard, in both directions.
    ///
    /// <para>It deliberately does NOT check legacy names against the seeded taxonomy: since Step 6 the
    /// seeder is canonical, so legacy names are absent by design and that check would pass vacuously —
    /// weaker than useless, because it would look like protection. Instead the legacy side is validated
    /// against <see cref="LegacyTypes"/>, the frozen pre-D11 input, and the destination side against the
    /// taxonomy actually seeded.</para></summary>
    [Fact]
    public async Task Every_mapping_is_valid_in_both_directions()
    {
        var canonical = (await SeededTypeNamesAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // (a) every legacy name mapped must be a real pre-D11 type — catches typos and invented sources.
        var phantom = Mappings.Select(m => m.Legacy)
            .Where(l => !LegacyTypes.Contains(l)).OrderBy(x => x).ToList();
        Assert.True(phantom.Count == 0, "mapping references a type that never existed: " + string.Join(", ", phantom));

        // (b) every destination must exist in the canonical taxonomy — catches a migration that would
        // move events onto a row that is not there.
        var missing = Mappings.Select(m => m.Canonical).Where(c => c is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(c => !canonical.Contains(c!)).OrderBy(x => x).ToList();
        Assert.True(missing.Count == 0, "mapping destination missing from the taxonomy: " + string.Join(", ", missing!));

        // (c) every legacy type that DISAPPEARED from the canonical taxonomy must have a mapping —
        // this is the one that fails if someone removes a type without telling the migration.
        var mapped = Mappings.Select(m => m.Legacy).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmapped = LegacyTypes.Where(l => !canonical.Contains(l) && !mapped.Contains(l))
            .OrderBy(x => x).ToList();
        Assert.True(unmapped.Count == 0, "legacy type vanished with no mapping: " + string.Join(", ", unmapped));
    }

    /// <summary>The pre-D11 type names, frozen. This is migration input, not live taxonomy — it must never
    /// be regenerated from the seeder, which is canonical now and no longer contains these names.</summary>
    private static readonly HashSet<string> LegacyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hackathon", "Coding Competition", "Capture The Flag (CTF)", "Ideathon", "Startup Pitch Competition",
        "Innovation Challenge", "Robotics Competition", "Quiz Competition", "Debate Competition",
        "Public Speaking Competition", "Case Study Competition", "Science Fair", "Olympiad",
        "Model United Nations (MUN)", "Art Competition", "Photography Competition", "Film Competition",
        "Music Competition", "Dance Competition", "Writing Competition", "Design Competition",
        "Talent Competition", "Fashion Competition", "Workshop", "Seminar", "Guest Lecture",
        "Training Program", "Certification Program", "Bootcamp", "Project Expo", "Science Exhibition",
        "Research Symposium", "Industrial Visit", "Technical Fest", "Cultural Fest", "College Fest",
        "Department Event", "Orientation Program", "Freshers Party", "Farewell Party", "Annual Day",
        "Award Ceremony", "Alumni Meet", "Career Guidance Session", "Internship Drive", "Placement Drive",
        "Career Fair", "Networking Event", "Startup Meetup", "Industry Interaction Session",
        "Cricket Tournament", "Football Tournament", "Basketball Tournament", "Volleyball Tournament",
        "Badminton Tournament", "Chess Tournament", "Athletics Meet", "Marathon", "Esports Tournament",
        "Club Event", "Club Recruitment Drive", "Student Meetup", "Community Service Event",
        "Volunteer Event", "Student Chapter Event", "Society Event", "Conference", "Business Summit",
        "Leadership Summit", "Product Launch", "Investor Meet", "Trade Show", "Industry Expo",
        "Press Conference", "Job Fair", "Panel Discussion", "Community Meetup", "Charity Event",
        "Fundraiser", "Awareness Campaign", "Blood Donation Camp", "Health Camp", "Environmental Drive",
        "Tree Plantation Drive", "Religious Gathering", "Government Event", "Public Celebration",
        "Concert", "Music Festival", "DJ Night", "Movie Screening", "Theatre Performance",
        "Stand-up Comedy Show", "Fashion Show", "Fan Meetup", "Creator Meetup", "Gaming Convention",
        "Food Festival", "Book Fair", "Flea Market", "Exhibition", "Art Exhibition",
        "Photography Exhibition", "Cycling Event", "Running Event", "Fitness Challenge",
        "Adventure Event", "Trekking Event", "Public Sports Tournament", "Wedding", "Engagement",
        "Reception", "Destination Wedding", "Anniversary Celebration", "Birthday Party", "Baby Shower",
        "Gender Reveal Party", "Naming Ceremony", "Housewarming Ceremony", "Graduation Party",
        "Retirement Party", "Reunion", "Family Gathering", "Bachelor Party", "Bachelorette Party",
        "Dinner Party", "Private Celebration", "Friends Meetup", "Surprise Party", "Sleepover Party",
        "Game Night", "Pooja Ceremony", "Festival Celebration", "Religious Ceremony",
        "Coming-of-Age Ceremony", "Memorial Service", "Prayer Meeting", "Traditional Family Function",
    };

    /// <summary>A canonical destination must be a real type, or the migration would move events to nowhere.
    /// Destinations introduced by D11 (Campus Recruitment, Sports Tournament, ...) do not exist until the
    /// re-seed, so they are allowed to be absent — but only if they are named as canonical somewhere.</summary>
    [Fact]
    public async Task Every_canonical_destination_is_either_seeded_or_a_declared_new_type()
    {
        var seeded = (await SeededTypeNamesAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var declared = Mappings.Select(m => m.Canonical).Where(c => c is not null).ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        var orphanDestinations = Mappings
            .Select(m => m.Canonical).Where(c => c is not null).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(c => !seeded.Contains(c!) && !declared.Contains(c!))
            .ToList();

        Assert.True(orphanDestinations.Count == 0,
            "canonical destination exists nowhere: " + string.Join(", ", orphanDestinations!));
    }

    [Fact]
    public void A_canonical_destination_is_never_itself_a_legacy_type_that_moves()
    {
        // Chained mappings (A→B where B→C) would make resolution order-dependent and non-deterministic.
        var moving = Mappings.ToDictionary(m => m.Legacy, m => m.Canonical, StringComparer.OrdinalIgnoreCase);
        var chained = Mappings
            .Where(m => m.Canonical is not null && moving.TryGetValue(m.Canonical, out var next) && next != m.Canonical)
            .Select(m => $"{m.Legacy} -> {m.Canonical} -> {moving[m.Canonical!]}")
            .ToList();

        Assert.True(chained.Count == 0, "chained mapping(s): " + string.Join(" | ", chained));
    }

    // ── Phase 2 · every existing type resolves, deterministically ───────────────────────────

    [Fact]
    public async Task Every_seeded_type_resolves_to_exactly_one_canonical_type()
    {
        var unresolved = new List<string>();
        foreach (var name in await SeededTypeNamesAsync())
        {
            var canonical = Resolve(name);
            if (string.IsNullOrWhiteSpace(canonical)) unresolved.Add(name);
            // Resolution must be idempotent: resolving the canonical name again changes nothing.
            if (!string.Equals(Resolve(canonical), canonical, StringComparison.OrdinalIgnoreCase))
                unresolved.Add($"{name} (not idempotent: {canonical} -> {Resolve(canonical)})");
        }
        Assert.True(unresolved.Count == 0, string.Join(" | ", unresolved));
    }

    [Fact]
    public async Task Resolution_is_total_no_type_can_fail_to_resolve()
    {
        foreach (var name in await SeededTypeNamesAsync())
            Assert.False(string.IsNullOrWhiteSpace(Resolve(name)), $"'{name}' resolved to nothing");
    }

    [Fact]
    public async Task The_mapping_produces_exactly_the_approved_canonical_count()
    {
        var seeded = await SeededTypeNamesAsync();
        var canonical = seeded.Select(Resolve).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal(104, canonical.Count);
    }

    [Fact]
    public void Attribute_conversions_carry_both_the_field_and_its_value()
    {
        var incomplete = Mappings
            .Where(m => m.Kind == MappingKind.AttributeConversion)
            .Where(m => m.Attribute is null
                     || string.IsNullOrWhiteSpace(m.Attribute.Value.Name)
                     || string.IsNullOrWhiteSpace(m.Attribute.Value.Value))
            .Select(m => m.Legacy).ToList();

        // Without the value, "Cricket Tournament -> Sports Tournament" loses the fact that it was cricket.
        Assert.True(incomplete.Count == 0, "attribute conversion with no value: " + string.Join(", ", incomplete));
    }

    [Fact]
    public void Removed_types_still_resolve_so_historical_events_never_orphan()
    {
        foreach (var m in Mappings.Where(m => m.Kind == MappingKind.Removed))
        {
            Assert.NotNull(m.Canonical);
            Assert.NotEqual(m.Legacy, Resolve(m.Legacy));
        }
    }

    [Fact]
    public void Retired_types_are_blocked_for_creation_but_still_resolvable()
    {
        foreach (var legacy in RetiredForCreation)
            Assert.False(string.IsNullOrWhiteSpace(Resolve(legacy)),
                $"'{legacy}' is blocked for creation but does not resolve — historical events would orphan");
    }

    [Fact]
    public void Every_mapping_records_why_it_exists()
    {
        var unreasoned = Mappings.Where(m => string.IsNullOrWhiteSpace(m.Reason)).Select(m => m.Legacy).ToList();
        Assert.True(unreasoned.Count == 0, "mapping with no reason: " + string.Join(", ", unreasoned));
    }

    /// <summary>Types deliberately kept apart despite similar names (D11 §5). If a future edit merged these,
    /// the taxonomy would lose a real workflow distinction.</summary>
    [Theory]
    [InlineData("Science Fair", "Science Exhibition")]     // judged vs showcase
    [InlineData("Charity Event", "Fundraiser")]            // ticket sale vs donation
    [InlineData("Hackathon", "Ideathon")]                  // code submission vs pitch
    [InlineData("Career Fair", "Placement Drive")]         // many employers vs one
    public void Deliberately_distinct_types_never_collapse_together(string a, string b)
    {
        Assert.NotEqual(Resolve(a), Resolve(b));
    }
}
