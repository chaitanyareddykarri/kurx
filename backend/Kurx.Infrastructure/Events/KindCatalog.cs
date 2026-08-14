namespace Kurx.Infrastructure.Events;

/// <summary>
/// V3 §2 — the closed 20-Kind catalog plus the INITIAL seed values for the Kind registry (Phase 1).
/// A Kind is a named, analysable bundle of capability *defaults* (V3/D-131); it never gates engine
/// behaviour. The 145 retired taxonomy Type names survive as aliases (kind_aliases).
///
/// <para><b>Data-driven at runtime.</b> Alias→Kind resolution reads the <c>kind_aliases</c> TABLE, never
/// this file, so a mapping can be corrected in the database without a code change (Phase-1 requirement).
/// This file only supplies the initial seed; <see cref="KindRegistrySeeder"/> inserts missing rows and
/// never overwrites an existing one, so a manual correction survives a reboot.</para>
/// </summary>
public static class KindCatalog
{
    public sealed record KindDef(string Slug, string Name, string GroupSlug, string GroupName);

    /// <summary>The 20 Kinds, in V3 §2 order, across the 7 V3 groups. Closed vocabulary (V3 §2 tenet).</summary>
    public static readonly KindDef[] Kinds =
    [
        new("hackathon", "Hackathon", "competitive", "Competitive"),
        new("competition", "Competition", "competitive", "Competitive"),
        new("tournament", "Tournament", "competitive", "Competitive"),
        new("race", "Race", "competitive", "Competitive"),
        new("conference", "Conference", "learning", "Learning"),
        new("workshop", "Workshop", "learning", "Learning"),
        new("talk", "Talk", "learning", "Learning"),
        new("training", "Training", "learning", "Learning"),
        new("meetup", "Meetup", "community", "Community"),
        new("exhibition", "Exhibition", "community", "Community"),
        new("selection-drive", "Selection Drive", "community", "Community"),
        new("performance", "Performance", "experience", "Experience"),
        new("trip", "Trip", "experience", "Experience"),
        new("ceremony", "Ceremony", "ceremonial", "Ceremonial"),
        new("celebration", "Celebration", "ceremonial", "Ceremonial"),
        new("wedding", "Wedding", "ceremonial", "Ceremonial"),
        new("fundraiser", "Fundraiser", "purpose", "Purpose"),
        new("camp", "Camp", "purpose", "Purpose"),
        new("festival", "Festival", "structural", "Structural"),
        new("meeting", "Meeting", "structural", "Structural"),
    ];

    /// <summary>
    /// The 145 legacy taxonomy Type NAME → Kind slug. Keyed by the exact name seeded by
    /// <see cref="EventTaxonomySeeder"/>; the 7 names that recur across audiences (Workshop, Seminar,
    /// Marathon, Networking Event, Startup Meetup, Volunteer Event, Career Fair) map once and apply to
    /// every row of that name.
    ///
    /// <para><b>Judgment calls</b> (V3 §2 admission test — "what does the system do differently?"). The
    /// non-obvious mappings are commented inline; every one is data and can be re-pointed in the
    /// <c>kind_aliases</c> table without a code change:</para>
    /// <list type="bullet">
    /// <item>CTF → competition (scored solve-contest, not a build-a-project hackathon)</item>
    /// <item>Ideathon → hackathon (V3 §20 lists "ideathon" → Hackathon explicitly)</item>
    /// <item>Innovation Challenge / Science Fair / MUN / Fitness Challenge → competition (judged + ranked)</item>
    /// <item>Athletics Meet → tournament (multi-event scored meet; alt Race)</item>
    /// <item>Career Fair / Job Fair / Club Recruitment Drive → selection-drive (recruitment intent)</item>
    /// <item>Product Launch → talk; Investor Meet / Government Event → meeting</item>
    /// <item>Orientation Program / Religious Gathering / Prayer Meeting → ceremony (formal program)</item>
    /// <item>Community Service / Volunteer / Awareness / Environmental / Tree Plantation → camp (volunteer drives)</item>
    /// <item>Reunion / Family Gathering / Game Night / Sleepover → celebration</item>
    /// <item>Gaming Convention / Flea Market / Book Fair → exhibition</item>
    /// <item>Engagement / Reception / Festival Celebration → celebration (Wedding kind reserved for weddings proper)</item>
    /// </list>
    /// </summary>
    public static readonly Dictionary<string, string> TypeNameToKind = new(StringComparer.Ordinal)
    {
        // ── Student · Competitions ──
        ["Hackathon"] = "hackathon",
        ["Coding Competition"] = "competition",
        ["Capture The Flag (CTF)"] = "competition",   // judgment: scored solve-contest, not build-a-project
        ["Ideathon"] = "hackathon",                    // V3 §20 canonical example
        ["Startup Pitch Competition"] = "competition",
        ["Innovation Challenge"] = "competition",       // judgment: judged challenge (borderline hackathon)
        ["Robotics Competition"] = "competition",
        ["Quiz Competition"] = "competition",
        ["Debate Competition"] = "competition",
        ["Public Speaking Competition"] = "competition",
        ["Case Study Competition"] = "competition",
        ["Science Fair"] = "competition",               // judgment: judged exhibits + prizes
        ["Olympiad"] = "competition",
        ["Model United Nations (MUN)"] = "competition", // judgment: delegates compete for awards
        ["Art Competition"] = "competition",
        ["Photography Competition"] = "competition",
        ["Film Competition"] = "competition",
        ["Music Competition"] = "competition",
        ["Dance Competition"] = "competition",
        ["Writing Competition"] = "competition",
        ["Design Competition"] = "competition",
        ["Talent Competition"] = "competition",
        ["Fashion Competition"] = "competition",
        // ── Student · Workshops & Learning ──
        ["Workshop"] = "workshop",
        ["Seminar"] = "talk",
        ["Guest Lecture"] = "talk",
        ["Training Program"] = "training",
        ["Certification Program"] = "training",
        ["Bootcamp"] = "training",
        ["Project Expo"] = "exhibition",
        ["Science Exhibition"] = "exhibition",
        ["Research Symposium"] = "conference",
        ["Industrial Visit"] = "trip",
        // ── Student · Campus Events ──
        ["Technical Fest"] = "festival",
        ["Cultural Fest"] = "festival",
        ["College Fest"] = "festival",
        ["Department Event"] = "meetup",                // judgment: generic gathering
        ["Orientation Program"] = "ceremony",           // judgment: formal program
        ["Freshers Party"] = "celebration",
        ["Farewell Party"] = "celebration",
        ["Annual Day"] = "ceremony",
        ["Award Ceremony"] = "ceremony",
        ["Alumni Meet"] = "meetup",
        // ── Student · Career & Professional ──
        ["Career Guidance Session"] = "talk",
        ["Internship Drive"] = "selection-drive",
        ["Placement Drive"] = "selection-drive",
        ["Career Fair"] = "selection-drive",            // judgment: recruitment over exhibition
        ["Networking Event"] = "meetup",
        ["Startup Meetup"] = "meetup",
        ["Industry Interaction Session"] = "talk",
        // ── Student · Sports & Gaming ──
        ["Cricket Tournament"] = "tournament",
        ["Football Tournament"] = "tournament",
        ["Basketball Tournament"] = "tournament",
        ["Volleyball Tournament"] = "tournament",
        ["Badminton Tournament"] = "tournament",
        ["Chess Tournament"] = "tournament",
        ["Athletics Meet"] = "tournament",              // judgment: multi-event scored meet
        ["Marathon"] = "race",
        ["Esports Tournament"] = "tournament",
        // ── Student · Clubs & Communities ──
        ["Club Event"] = "meetup",
        ["Club Recruitment Drive"] = "selection-drive", // judgment: recruitment
        ["Student Meetup"] = "meetup",
        ["Community Service Event"] = "camp",           // judgment: volunteer drive
        ["Volunteer Event"] = "camp",                   // judgment: volunteer drive
        ["Student Chapter Event"] = "meetup",
        ["Society Event"] = "meetup",
        // ── Public · Professional & Business ──
        ["Conference"] = "conference",
        ["Business Summit"] = "conference",
        ["Leadership Summit"] = "conference",
        ["Product Launch"] = "talk",                    // judgment: presentation-led
        ["Investor Meet"] = "meeting",                  // judgment
        ["Trade Show"] = "exhibition",
        ["Industry Expo"] = "exhibition",
        ["Press Conference"] = "conference",
        ["Job Fair"] = "selection-drive",
        ["Panel Discussion"] = "talk",
        // ── Public · Community & Social ──
        ["Community Meetup"] = "meetup",
        ["Charity Event"] = "fundraiser",
        ["Fundraiser"] = "fundraiser",
        ["Awareness Campaign"] = "camp",                // judgment: volunteer drive
        ["Blood Donation Camp"] = "camp",
        ["Health Camp"] = "camp",
        ["Environmental Drive"] = "camp",               // judgment: volunteer drive
        ["Tree Plantation Drive"] = "camp",
        ["Religious Gathering"] = "ceremony",           // judgment: formal program
        ["Government Event"] = "meeting",               // judgment
        ["Public Celebration"] = "celebration",
        // ── Public · Entertainment & Lifestyle ──
        ["Concert"] = "performance",
        ["Music Festival"] = "festival",
        ["DJ Night"] = "performance",
        ["Movie Screening"] = "performance",
        ["Theatre Performance"] = "performance",
        ["Stand-up Comedy Show"] = "performance",
        ["Fashion Show"] = "performance",
        ["Fan Meetup"] = "meetup",
        ["Creator Meetup"] = "meetup",
        ["Gaming Convention"] = "exhibition",           // judgment: booths/showcase
        ["Food Festival"] = "festival",
        ["Book Fair"] = "exhibition",
        ["Flea Market"] = "exhibition",                 // judgment: stalls/showcase
        ["Exhibition"] = "exhibition",
        ["Art Exhibition"] = "exhibition",
        ["Photography Exhibition"] = "exhibition",
        // ── Public · Sports & Fitness ──
        ["Cycling Event"] = "race",
        ["Running Event"] = "race",
        ["Fitness Challenge"] = "competition",          // judgment: scored challenge (alt Race)
        ["Adventure Event"] = "trip",
        ["Trekking Event"] = "trip",
        ["Public Sports Tournament"] = "tournament",
        // ── Private · Family Celebrations ──
        ["Wedding"] = "wedding",
        ["Engagement"] = "celebration",                 // judgment: Wedding kind reserved for weddings proper
        ["Reception"] = "celebration",                  // judgment
        ["Destination Wedding"] = "wedding",
        ["Anniversary Celebration"] = "celebration",
        ["Birthday Party"] = "celebration",
        ["Baby Shower"] = "celebration",
        ["Gender Reveal Party"] = "celebration",
        ["Naming Ceremony"] = "ceremony",
        ["Housewarming Ceremony"] = "ceremony",         // judgment: alt Celebration
        ["Graduation Party"] = "celebration",
        ["Retirement Party"] = "celebration",
        ["Reunion"] = "celebration",                    // judgment: family celebration (alt Meetup)
        ["Family Gathering"] = "celebration",
        // ── Private · Personal Gatherings ──
        ["Bachelor Party"] = "celebration",
        ["Bachelorette Party"] = "celebration",
        ["Dinner Party"] = "celebration",
        ["Private Celebration"] = "celebration",
        ["Friends Meetup"] = "meetup",
        ["Surprise Party"] = "celebration",
        ["Sleepover Party"] = "celebration",            // judgment: alt Meetup
        ["Game Night"] = "celebration",                 // judgment: alt Meetup
        // ── Private · Religious & Traditional ──
        ["Pooja Ceremony"] = "ceremony",
        ["Festival Celebration"] = "celebration",       // judgment: alt Ceremony
        ["Religious Ceremony"] = "ceremony",
        ["Coming-of-Age Ceremony"] = "ceremony",
        ["Memorial Service"] = "ceremony",
        ["Prayer Meeting"] = "ceremony",                // judgment: religious gathering, not a corporate Meeting
        ["Traditional Family Function"] = "celebration",
    };

    /// <summary>Fallback for events with no Type: the 13 mid-level category slugs → a representative Kind.
    /// Best-effort only (a category spans several Kinds) and used solely to resolve a KindSlug when the
    /// event has no TypeId. Not stored as aliases, so it never affects the 145-alias count.</summary>
    public static readonly Dictionary<string, string> CategorySlugToKind = new(StringComparer.Ordinal)
    {
        ["competitions"] = "competition",
        ["workshops-learning"] = "workshop",
        ["campus-events"] = "festival",
        ["career-professional"] = "selection-drive",
        ["sports-gaming"] = "tournament",
        ["clubs-communities"] = "meetup",
        ["professional-business"] = "conference",
        ["community-social"] = "meetup",
        ["entertainment-lifestyle"] = "performance",
        ["sports-fitness"] = "race",
        ["family-celebrations"] = "celebration",
        ["personal-gatherings"] = "celebration",
        ["religious-traditional"] = "ceremony",
    };
}
