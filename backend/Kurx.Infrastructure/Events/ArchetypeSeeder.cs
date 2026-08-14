using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Seeds the 14 behavioural archetypes of D-266 §D12 and maps every seeded event type onto one
/// of them.
///
/// <para>Unlike <see cref="EventTaxonomySeeder"/> — which no-ops the instant <c>event_categories</c> has a
/// single row, because D-188 hands taxonomy ownership to the admin console after first boot — this seeder
/// must run on populated databases too: the archetype column is new, so every existing type has a null to
/// fill. It therefore backfills only rows whose <see cref="EventCategory.ArchetypeSlug"/> is still null and
/// never overwrites an admin's choice.</para>
///
/// <para>The map is keyed on type <b>name</b> rather than slug because slugs carry an audience prefix for
/// the names that recur across audiences (<c>student-workshop</c> / <c>business-workshop</c>), and both
/// resolve to the same archetype — a Workshop behaves like a Workshop whoever runs it.</para></summary>
public static class ArchetypeSeeder
{
    /// <param name="RequiresRepresentation">D12 §6's Representation column (D-266 M5).</param>
    private sealed record ArchetypeDef(string Slug, string Name, string Description, EventProduct Product,
        bool RequiresRepresentation = false, bool RequiresFinancialReview = false);

    private static readonly ArchetypeDef[] Archetypes =
    [
        new("competitive", "Competitive", "Entrants compete; work is submitted and judged", EventProduct.Public),
        new("learning", "Learning", "Instruction delivered; attendance recognised", EventProduct.Public),
        new("conference", "Conference", "Multi-track programme with speakers", EventProduct.Public),
        new("exhibition", "Exhibition", "Exhibitors occupy booths; visitors browse", EventProduct.Public),
        new("tournament", "Tournament", "Bracketed contests between opponents", EventProduct.Public),
        new("endurance", "Endurance", "Route or distance participation, not opponents", EventProduct.Public),
        new("recruitment", "Recruitment", "Candidates meet employers; slots and interviews", EventProduct.Public, RequiresRepresentation: true),
        new("performance", "Performance", "Performers on a stage; audience attends", EventProduct.Public),
        new("community", "Community", "People gather to meet; light structure", EventProduct.Public),
        new("civic", "Civic", "Public-benefit service; never monetised", EventProduct.Public),
        new("fundraising", "Fundraising", "Money is solicited for a cause", EventProduct.Public, RequiresFinancialReview: true),
        new("festival", "Festival", "Umbrella event containing sub-events", EventProduct.Public, RequiresRepresentation: true),
        new("ceremonial", "Ceremonial", "Formal institutional occasion", EventProduct.Public, RequiresRepresentation: true),
        new("private-gathering", "Private Gathering", "Personal or family occasion; invitation only", EventProduct.Private),
    ];

    /// <summary>Type name → archetype slug. Covers both the names seeded since D-023 and the D-266 §D11
    /// renames, so the map stays correct across the rename regardless of which side a database is on.</summary>
    private static readonly Dictionary<string, string> TypeArchetypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── A1 Competitive — entrants submit work that is judged ────────────────────────────
        ["Hackathon"] = "competitive",
        ["Coding Competition"] = "competitive",
        ["Capture The Flag (CTF)"] = "competitive",
        ["Ideathon"] = "competitive",
        ["Startup Pitch Competition"] = "competitive",
        ["Innovation Challenge"] = "competitive",
        ["Robotics Competition"] = "competitive",
        ["Quiz Competition"] = "competitive",
        ["Debate Competition"] = "competitive",
        ["Public Speaking Competition"] = "competitive",
        ["Case Study Competition"] = "competitive",
        ["Science Fair"] = "competitive",           // judged — unlike Science Exhibition (D11 §5)
        ["Olympiad"] = "competitive",
        ["Model United Nations (MUN)"] = "competitive",
        ["Creative Competition"] = "competitive",   // D11 §4 — submission-based
        ["Performance Competition"] = "competitive",// D11 §4 — live/staged
        // pre-D11 names, still present in existing databases
        ["Art Competition"] = "competitive",
        ["Photography Competition"] = "competitive",
        ["Film Competition"] = "competitive",
        ["Writing Competition"] = "competitive",
        ["Design Competition"] = "competitive",
        ["Music Competition"] = "competitive",
        ["Dance Competition"] = "competitive",
        ["Talent Competition"] = "competitive",
        ["Fashion Competition"] = "competitive",

        // ── A2 Learning — instruction delivered, attendance recognised ──────────────────────
        ["Workshop"] = "learning",
        ["Seminar"] = "learning",
        ["Guest Lecture"] = "learning",
        ["Training Program"] = "learning",
        ["Certification Program"] = "learning",
        ["Bootcamp"] = "learning",
        ["Research Symposium"] = "learning",
        ["Site Visit"] = "learning",
        ["Industrial Visit"] = "learning",          // pre-D11 name
        ["Career Guidance Session"] = "learning",
        ["Industry Talk"] = "learning",
        ["Industry Interaction Session"] = "learning",
        ["Panel Discussion"] = "learning",

        // ── A3 Conference — multi-track programme ──────────────────────────────────────────
        ["Conference"] = "conference",
        ["Business Summit"] = "conference",
        ["Leadership Summit"] = "conference",

        // ── A4 Exhibition — exhibitors occupy booths, visitors browse ───────────────────────
        ["Project Expo"] = "exhibition",
        ["Science Exhibition"] = "exhibition",      // showcase, not judged (D11 §5)
        ["Trade Show"] = "exhibition",
        ["Exhibition"] = "exhibition",
        ["Flea Market"] = "exhibition",
        ["Book Fair"] = "exhibition",
        ["Gaming Convention"] = "exhibition",
        ["Product Launch"] = "exhibition",
        ["Industry Expo"] = "exhibition",           // pre-D11, merged into Trade Show
        ["Art Exhibition"] = "exhibition",
        ["Photography Exhibition"] = "exhibition",

        // ── A5 Tournament — bracketed contests between opponents ────────────────────────────
        ["Sports Tournament"] = "tournament",
        ["Esports Tournament"] = "tournament",
        ["Cricket Tournament"] = "tournament",
        ["Football Tournament"] = "tournament",
        ["Basketball Tournament"] = "tournament",
        ["Volleyball Tournament"] = "tournament",
        ["Badminton Tournament"] = "tournament",
        ["Chess Tournament"] = "tournament",
        ["Public Sports Tournament"] = "tournament",

        // ── A6 Endurance — route and distance, no opponent ──────────────────────────────────
        ["Running Event"] = "endurance",
        ["Cycling Event"] = "endurance",
        ["Fitness Challenge"] = "endurance",
        ["Adventure Event"] = "endurance",
        ["Trekking Event"] = "endurance",
        ["Marathon"] = "endurance",                 // pre-D11, merged into Running Event
        ["Athletics Meet"] = "endurance",

        // ── A7 Recruitment — candidates meet employers ──────────────────────────────────────
        ["Career Fair"] = "recruitment",
        ["Campus Recruitment"] = "recruitment",
        ["Internship Recruitment"] = "recruitment",
        ["Placement Drive"] = "recruitment",
        ["Internship Drive"] = "recruitment",
        ["Job Fair"] = "recruitment",
        ["Club Recruitment Drive"] = "recruitment",

        // ── A8 Performance — performers on a stage ──────────────────────────────────────────
        ["Concert"] = "performance",
        ["DJ Night"] = "performance",
        ["Movie Screening"] = "performance",
        ["Theatre Performance"] = "performance",
        ["Stand-up Comedy Show"] = "performance",
        ["Fashion Show"] = "performance",
        ["Public Celebration"] = "performance",

        // ── A9 Community — people gather to meet ────────────────────────────────────────────
        ["Community Meetup"] = "community",
        ["Student Meetup"] = "community",
        ["Networking Event"] = "community",
        ["Startup Meetup"] = "community",
        ["Fan Meetup"] = "community",
        ["Creator Meetup"] = "community",
        ["Club Event"] = "community",
        ["Alumni Meet"] = "community",
        ["Food Festival"] = "community",
        ["Society Event"] = "community",            // pre-D11, merged into Club Event
        ["Student Chapter Event"] = "community",
        ["Press Conference"] = "community",
        ["Investor Meet"] = "community",            // removed by D11; mapped so legacy rows still resolve

        // ── A10 Civic — public-benefit service, never monetised ─────────────────────────────
        ["Blood Drive"] = "civic",
        ["Health Screening"] = "civic",
        ["Volunteer Event"] = "civic",
        ["Environmental Drive"] = "civic",
        ["Tree Planting"] = "civic",
        ["Awareness Campaign"] = "civic",
        ["Government Event"] = "civic",
        ["Blood Donation Camp"] = "civic",          // pre-D11 names
        ["Health Camp"] = "civic",
        ["Tree Plantation Drive"] = "civic",
        ["Community Service Event"] = "civic",

        // ── A11 Fundraising — money solicited for a cause ───────────────────────────────────
        ["Charity Event"] = "fundraising",          // sells tickets…
        ["Fundraiser"] = "fundraising",             // …vs collects donations (D11 §5)

        // ── A12 Festival — umbrella containing sub-events ───────────────────────────────────
        ["Technical Fest"] = "festival",
        ["Cultural Fest"] = "festival",
        ["Campus Fest"] = "festival",
        ["College Fest"] = "festival",              // pre-D11 name
        ["Music Festival"] = "festival",

        // ── A13 Ceremonial — formal institutional occasion ──────────────────────────────────
        ["Award Ceremony"] = "ceremonial",
        ["Annual Celebration"] = "ceremonial",
        ["Annual Day"] = "ceremonial",              // pre-D11 name
        ["Orientation Program"] = "ceremonial",
        ["Welcome Event"] = "ceremonial",
        ["Farewell Event"] = "ceremonial",
        ["Freshers Party"] = "ceremonial",          // pre-D11 names
        ["Farewell Party"] = "ceremonial",
        ["Department Event"] = "ceremonial",

        // ── A14 Private Gathering — personal or family, invitation only ─────────────────────
        ["Wedding"] = "private-gathering",
        ["Engagement"] = "private-gathering",
        ["Reception"] = "private-gathering",
        ["Anniversary Celebration"] = "private-gathering",
        ["Birthday Party"] = "private-gathering",
        ["Baby Shower"] = "private-gathering",
        ["Naming Ceremony"] = "private-gathering",
        ["Housewarming Ceremony"] = "private-gathering",
        // D-322 — sits in Private Events ▸ Family Celebrations. It was mapped to `ceremonial` above
        // because the map is keyed on name and "Graduation Party" reads institutional next to
        // "Freshers Party"/"Farewell Party"; the taxonomy places it with Anniversary and Retirement.
        ["Graduation Party"] = "private-gathering",
        ["Retirement Party"] = "private-gathering",
        ["Reunion"] = "private-gathering",
        ["Family Gathering"] = "private-gathering",
        ["Pre-Wedding Celebration"] = "private-gathering",
        ["Dinner Party"] = "private-gathering",
        ["Private Celebration"] = "private-gathering",
        ["Friends Meetup"] = "private-gathering",
        ["Surprise Party"] = "private-gathering",
        ["Game Night"] = "private-gathering",
        ["Private Ceremony"] = "private-gathering",
        // pre-D11 private names still present in existing databases
        ["Destination Wedding"] = "private-gathering",
        ["Bachelor Party"] = "private-gathering",
        ["Bachelorette Party"] = "private-gathering",
        ["Gender Reveal Party"] = "private-gathering",
        ["Sleepover Party"] = "private-gathering",
        ["Pooja Ceremony"] = "private-gathering",
        ["Festival Celebration"] = "private-gathering",
        ["Religious Ceremony"] = "private-gathering",
        ["Coming-of-Age Ceremony"] = "private-gathering",
        ["Memorial Service"] = "private-gathering",
        ["Prayer Meeting"] = "private-gathering",
        ["Traditional Family Function"] = "private-gathering",
        ["Religious Gathering"] = "private-gathering",
    };

    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        var rows = await db.EventArchetypes.ToListAsync(ct);
        var existing = rows.Select(a => a.Slug).ToList();
        var missing = Archetypes.Where(a => !existing.Contains(a.Slug)).ToList();
        for (var i = 0; i < missing.Count; i++)
        {
            var d = missing[i];
            db.EventArchetypes.Add(new EventArchetype
            {
                Slug = d.Slug, Name = d.Name, Description = d.Description,
                Product = d.Product,
                RequiresRepresentation = d.RequiresRepresentation,
                RequiresFinancialReview = d.RequiresFinancialReview,
                Sort = Array.FindIndex(Archetypes, a => a.Slug == d.Slug),
            });
        }

        // D-266 M5 — backfill the new column on databases seeded before it existed. Unlike ArchetypeSlug
        // on a category, this is not an admin-editable choice (no console surface writes it), so
        // reconciling it to the D12 matrix cannot overwrite anyone's decision.
        var touchedFlags = false;
        foreach (var row in rows)
        {
            var def = Archetypes.FirstOrDefault(a => a.Slug == row.Slug);
            if (def is null) continue;
            if (row.RequiresRepresentation == def.RequiresRepresentation
                && row.RequiresFinancialReview == def.RequiresFinancialReview) continue;
            row.RequiresRepresentation = def.RequiresRepresentation;
            row.RequiresFinancialReview = def.RequiresFinancialReview;
            touchedFlags = true;
        }
        if (missing.Count > 0 || touchedFlags) await db.SaveChangesAsync(ct);

        // D-322 — repair the one Type the name-keyed map put on the wrong side of the product boundary.
        // The map above is now correct for a fresh database; every already-seeded one still holds
        // ceremonial/Public, and the backfill below cannot reach it because its ArchetypeSlug is not null.
        //
        // Conditional on the exact drifted pair rather than a blind UPDATE, for the same reason the
        // backfill only touches nulls: a deliberate re-map must survive a restart. No admin write surface
        // for ArchetypeSlug exists today (CategoryService never assigns it), so this is the cheap guard
        // against one being added later, not a live race. Idempotent — after the first run nothing matches.
        // A migration would be the usual home for a data repair, but this seeder already reconciles
        // populated databases on every boot and is the only writer of both columns.
        await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type
                        && c.Slug == "graduation-party"
                        && c.ArchetypeSlug == "ceremonial"
                        && c.ProductClass == EventProduct.Public)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ArchetypeSlug, "private-gathering")
                .SetProperty(c => c.ProductClass, (EventProduct?)EventProduct.Private), ct);

        // Backfill only what is still unmapped — an admin's later re-map must survive a restart.
        var unmapped = await db.EventCategories
            .Where(c => c.ArchetypeSlug == null && c.Level == CategoryLevel.Type)
            .ToListAsync(ct);
        if (unmapped.Count == 0) return;

        var privateProduct = Archetypes.Where(a => a.Product == EventProduct.Private)
                                       .Select(a => a.Slug).ToHashSet();
        var touched = false;
        foreach (var cat in unmapped)
        {
            if (!TypeArchetypes.TryGetValue(cat.Name, out var slug)) continue;
            cat.ArchetypeSlug = slug;
            cat.ProductClass = privateProduct.Contains(slug) ? EventProduct.Private : EventProduct.Public;
            touched = true;
        }
        if (touched) await db.SaveChangesAsync(ct);
    }

    /// <summary>Exposed for the M1 completeness test: every seeded type must resolve an archetype.</summary>
    public static bool TryResolve(string typeName, out string slug) => TypeArchetypes.TryGetValue(typeName, out slug!);
}
