using System.Text;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M3 Step 6: the CANONICAL taxonomy (104 logical types), generated from D-023's original content by
/// applying TaxonomyAliasMap — the same mapping the upgrade migration uses, so a fresh install and an
/// upgraded install converge on exactly the same taxonomy. Six types keep a navigation node under a second
/// audience (approved); Volunteer Event is the one cross-audience merge. A **one-time
/// seed for a brand-new database only** (D-188, Platform Taxonomy Management) — not a permanently-maintained
/// source of truth. <see cref="SeedAsync"/> no-ops the moment <c>event_categories</c> has any row, from this
/// seeder or from an admin. Slugs are globally unique (the table's unique index); a type name that recurs
/// across audiences (e.g. "Marathon") is disambiguated with its audience prefix (<c>student-marathon</c> /
/// <c>public-marathon</c>), all other types keep the bare kebab slug.</summary>
public static class EventTaxonomySeeder
{
    private sealed record AudienceDef(string Name, string Prefix, CategoryDef[] Categories);
    private sealed record CategoryDef(string Name, string[] Types);

    private static readonly AudienceDef[] Taxonomy =
    [
        new("Student Events", "student",
        [
            new("Competitions",
            [
                "Hackathon", "Coding Competition", "Capture The Flag (CTF)", "Ideathon",
                "Startup Pitch Competition", "Innovation Challenge", "Robotics Competition", "Quiz Competition",
                "Debate Competition", "Public Speaking Competition", "Case Study Competition", "Science Fair",
                "Olympiad", "Model United Nations (MUN)", "Creative Competition", "Performance Competition",
            ]),
            new("Workshops & Learning",
            [
                "Workshop", "Seminar", "Guest Lecture", "Training Program", "Certification Program", "Bootcamp",
                "Project Expo", "Science Exhibition", "Research Symposium", "Site Visit",
            ]),
            new("Campus Events",
            [
                "Technical Fest", "Cultural Fest", "Campus Fest", "Department Event", "Orientation Program",
                "Welcome Event", "Farewell Event", "Annual Celebration", "Award Ceremony", "Alumni Meet",
            ]),
            new("Career & Professional",
            [
                "Career Guidance Session", "Internship Recruitment", "Campus Recruitment", "Career Fair",
                "Networking Event", "Startup Meetup", "Industry Talk",
            ]),
            new("Sports & Gaming",
            [
                "Sports Tournament", "Running Event", "Esports Tournament",
            ]),
            new("Clubs & Communities",
            [
                "Club Event", "Club Recruitment Drive", "Student Meetup",
            ]),
        ]),
        new("Public Events", "public",
        [
            new("Professional & Business",
            [
                "Conference", "Business Summit", "Leadership Summit", "Networking Event", "Product Launch",
                "Startup Meetup", "Trade Show", "Press Conference", "Career Fair", "Workshop", "Seminar",
                "Panel Discussion",
            ]),
            new("Community & Social",
            [
                "Community Meetup", "Charity Event", "Fundraiser", "Awareness Campaign", "Blood Drive",
                "Health Screening", "Volunteer Event", "Environmental Drive", "Tree Planting", "Government Event",
                "Public Celebration",
            ]),
            new("Entertainment & Lifestyle",
            [
                "Concert", "Music Festival", "DJ Night", "Movie Screening", "Theatre Performance",
                "Stand-up Comedy Show", "Fashion Show", "Fan Meetup", "Creator Meetup", "Gaming Convention",
                "Food Festival", "Book Fair", "Flea Market", "Exhibition",
            ]),
            new("Sports & Fitness",
            [
                "Running Event", "Cycling Event", "Fitness Challenge", "Adventure Event", "Trekking Event",
                "Sports Tournament",
            ]),
        ]),
        new("Private Events", "private",
        [
            new("Family Celebrations",
            [
                "Wedding", "Engagement", "Reception", "Anniversary Celebration", "Birthday Party", "Baby Shower",
                "Private Celebration", "Naming Ceremony", "Housewarming Ceremony", "Graduation Party",
                "Retirement Party", "Reunion", "Family Gathering",
            ]),
            new("Personal Gatherings",
            [
                "Pre-Wedding Celebration", "Dinner Party", "Friends Meetup", "Surprise Party", "Game Night",
            ]),
            new("Religious & Traditional",
            [
                "Private Ceremony",
            ]),
        ]),
    ];

    /// <summary>D-188 (Platform Taxonomy Management): a one-time bootstrap for a brand-new database only —
    /// not a permanent reconciliation loop. Once <c>event_categories</c> has any row (from this seeder or
    /// from an admin creating one via <see cref="Kurx.Application.Abstractions.ICategoryService"/>), this
    /// returns immediately and never inserts, updates, or repairs anything again. The Admin Console is the
    /// taxonomy's source of truth from that point on — a developer editing <see cref="Taxonomy"/> after
    /// launch has no effect on any already-running environment; it only shapes what a *fresh* database
    /// starts with.</summary>
    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        if (await db.EventCategories.AnyAsync(ct)) return;

        // A type name that occurs under more than one audience gets its audience prefix so the
        // globally-unique slug stays deterministic and collision-free (D-023) — only matters within this
        // one bulk insert now, since there is no longer a reconciliation pass to keep it correct against.
        var typeSlugCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var audience in Taxonomy)
            foreach (var category in audience.Categories)
                foreach (var typeName in category.Types)
                {
                    var slug = Slugify(typeName);
                    typeSlugCounts[slug] = typeSlugCounts.GetValueOrDefault(slug) + 1;
                }

        for (var ai = 0; ai < Taxonomy.Length; ai++)
        {
            var audience = Taxonomy[ai];
            var audienceNode = new EventCategory { Level = CategoryLevel.Audience, Name = audience.Name, Slug = Slugify(audience.Name), Sort = ai, IsVisible = true };
            db.EventCategories.Add(audienceNode);

            for (var ci = 0; ci < audience.Categories.Length; ci++)
            {
                var category = audience.Categories[ci];
                var categoryNode = new EventCategory { Level = CategoryLevel.Category, Name = category.Name, Slug = Slugify(category.Name), Sort = ci, ParentId = audienceNode.Id, IsVisible = true };
                db.EventCategories.Add(categoryNode);

                for (var ti = 0; ti < category.Types.Length; ti++)
                {
                    var typeName = category.Types[ti];
                    var natural = Slugify(typeName);
                    var slug = typeSlugCounts[natural] > 1 ? $"{audience.Prefix}-{natural}" : natural;
                    db.EventCategories.Add(new EventCategory { Level = CategoryLevel.Type, Name = typeName, Slug = slug, Sort = ti, ParentId = categoryNode.Id, IsVisible = true });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // Same kebab rule as CategoryService.Slugify so admin-created and seeded slugs match.
    private static string Slugify(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
