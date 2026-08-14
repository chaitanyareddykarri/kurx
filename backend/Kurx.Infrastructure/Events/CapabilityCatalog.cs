namespace Kurx.Infrastructure.Events;

/// <summary>
/// V3 §11 + §19 — the closed **31**-entry event-Capability catalog and the Kind×Capability default matrix
/// (Event Architecture V3, Phase 2). Seed source only; the runtime reads the seeded tables so any value
/// is correctable in the database without a code change (D-132/CA-1 — config is data).
///
/// <para><b>Declared dependencies (§11.4, the only permitted coupling — a shallow DAG):</b>
/// <c>stages → scoring</c> (a stage carries a scoring policy, §10.1) and <c>certificates → attendance</c>
/// (a certificate is issued to an attendee). Both are consistent with the §19 matrix (no Kind has the
/// dependent on without its dependency). More edges are declared as their subsystems land (Phases 10–12).</para>
///
/// <para><b>Mode-gating (§11.3):</b> only <c>seat-map</c>, <c>route-timing</c>, <c>travel</c> are excluded
/// from Online. Kind never restricts availability — only mode does.</para>
/// </summary>
public static class CapabilityCatalog
{
    public sealed record CapDef(string Slug, string Name, string Group, bool Universal,
        string? WorkspaceTab, string[] DependsOn, string[] AvailableModes);

    private static readonly string[] AllModes = ["Offline", "Online", "Hybrid"];
    private static readonly string[] OfflineHybrid = ["Offline", "Hybrid"];   // mode-gated out of Online (§11.3)
    private static readonly string[] None = [];

    /// <summary>Groups: universal · structure · people · commerce · output.</summary>
    /// <summary>The 30 event capabilities of D12, plus `entitlements` (D-334) — and only those. D-266 M2 reduced this from 57: the
    /// other 27 were not event behaviour at all but Registration, Ticketing, Invitation, Scheduling,
    /// Eligibility, Finance or Infrastructure concerns wearing a capability flag, and each had a duplicate
    /// source of truth in its own subsystem. A slug belongs here only if the answer to "does this archetype
    /// fundamentally support this behaviour?" can differ between archetypes.</summary>
    public static readonly CapDef[] Capabilities =
    [
        new("check-in", "Check-in", "universal", true, "Check-in", None, AllModes),
        new("announcements", "Announcements", "universal", true, "Announcements", None, AllModes),
        new("media-gallery", "Media Gallery", "universal", true, "Media", None, AllModes),
        new("feedback", "Feedback", "universal", true, "Feedback", None, AllModes),
        new("chat", "Chat", "universal", true, "Chat", None, AllModes),
        new("sub-events", "Sub-Events", "structure", false, "Sub-Events", None, AllModes),
        new("agenda", "Agenda", "structure", false, "Agenda", None, AllModes),
        new("tracks", "Tracks", "structure", false, "Tracks", None, AllModes),
        new("teams", "Teams", "people", false, "Teams", None, AllModes),
        new("submissions", "Submissions", "people", false, "Submissions", None, AllModes),
        new("scoring", "Scoring", "people", false, "Judging", None, AllModes),
        new("speakers", "Speakers", "people", false, "Speakers", None, AllModes),
        new("mentors", "Mentors", "people", false, "Mentors", None, AllModes),
        new("volunteers", "Volunteers", "people", false, "Volunteers", None, AllModes),
        new("paid", "Paid", "commerce", false, "Pricing", None, AllModes),
        new("sponsors", "Sponsors", "commerce", false, "Sponsors", None, AllModes),
        new("certificates", "Certificates", "output", false, "Certificates", ["attendance"], AllModes),
        new("attendance", "Attendance", "output", false, "Attendance", None, AllModes),
        new("booths", "Booths", "structure", false, "Booths", None, AllModes),
        new("maps", "Venue Map", "structure", false, "Map", None, OfflineHybrid),
        new("polls", "Polls", "people", false, "Polls", None, AllModes),
        new("live-qa", "Live Q&A", "people", false, "Live Q&A", None, AllModes),
        new("networking", "Networking", "people", false, "Networking", None, AllModes),
        new("resources", "Resources", "output", false, "Resources", None, AllModes),
        new("reviews", "Reviews", "output", false, "Reviews", None, AllModes),
        new("leaderboard", "Leaderboard", "output", false, "Leaderboard", ["scoring"], AllModes),
        new("finance", "Finance", "commerce", false, "Finance", ["paid"], AllModes),
        new("analytics", "Analytics", "output", false, "Analytics", None, AllModes),
        new("problem-statements", "Problem Statements", "structure", false, "Problems", None, AllModes),
        new("forms", "Custom Forms", "people", false, "Forms", None, AllModes),
        // D-334. Not universal, and OPTIONAL — never Required — in every archetype below, so it is off
        // for every event that exists today and every event created tomorrow until an organiser turns it
        // on. That is the optional-feature guarantee expressed as data rather than as a code branch.
        //
        // Optional in all 14 rather than a chosen few: catering is orthogonal to what an event *is*. A
        // hackathon, a graduation and a fundraiser can each feed people, and Unsupported here would mean
        // "this archetype may never serve a meal", which is not a claim the matrix should be making. The
        // real constraint is the mode gate — a coupon redeemed at a counter is meaningless online.
        new("entitlements", "Food & Coupons", "commerce", false, "Food & Coupons", None, OfflineHybrid),
    ];


    /// <summary>D-266 M2 — the archetype × capability matrix of D12 §2, transcribed verbatim.
    ///
    /// <para><b>Absence means UNSUPPORTED here, not "off".</b> That inverts <see cref="KindDefaults"/>,
    /// where an omitted cell meant "available but off" and nothing was ever forbidden. The inversion is
    /// the whole point of M2: "a Workshop cannot enable Leaderboard" is only expressible if the model can
    /// say *no*. Universal capabilities are still always required and are not repeated per archetype.</para></summary>
    public sealed record ArchetypeDefault(string[] Required, string[] Optional);

    public static readonly Dictionary<string, ArchetypeDefault> ArchetypeDefaults = new(StringComparer.Ordinal)
    {
        ["competitive"] = new(
            ["submissions", "scoring", "attendance", "check-in", "announcements", "chat", "feedback", "forms", "analytics"],
            ["teams", "mentors", "problem-statements", "leaderboard", "sub-events", "speakers", "agenda", "resources", "paid", "finance", "sponsors", "maps", "certificates", "volunteers", "networking", "polls", "live-qa", "media-gallery", "reviews", "entitlements"]),
        ["learning"] = new(
            ["sub-events", "agenda", "resources", "attendance", "check-in", "certificates", "live-qa", "announcements", "chat", "feedback", "forms", "analytics"],
            ["mentors", "submissions", "speakers", "paid", "finance", "sponsors", "maps", "volunteers", "networking", "polls", "media-gallery", "reviews", "entitlements"]),
        ["conference"] = new(
            ["sub-events", "tracks", "speakers", "agenda", "attendance", "check-in", "networking", "live-qa", "announcements", "chat", "feedback", "forms", "analytics"],
            ["resources", "paid", "finance", "sponsors", "booths", "maps", "certificates", "volunteers", "polls", "media-gallery", "reviews", "entitlements"]),
        ["exhibition"] = new(
            ["sponsors", "booths", "maps", "attendance", "check-in", "networking", "announcements", "media-gallery", "chat", "forms", "analytics"],
            ["scoring", "sub-events", "speakers", "agenda", "resources", "paid", "finance", "volunteers", "reviews", "feedback", "entitlements"]),
        ["tournament"] = new(
            ["scoring", "leaderboard", "agenda", "attendance", "check-in", "announcements", "chat", "forms", "analytics"],
            ["teams", "paid", "finance", "sponsors", "maps", "certificates", "volunteers", "networking", "polls", "media-gallery", "reviews", "feedback", "entitlements"]),
        ["endurance"] = new(
            ["agenda", "maps", "attendance", "check-in", "volunteers", "announcements", "chat", "forms", "analytics"],
            ["teams", "leaderboard", "resources", "paid", "finance", "sponsors", "certificates", "networking", "media-gallery", "reviews", "feedback", "entitlements"]),
        ["recruitment"] = new(
            ["agenda", "attendance", "check-in", "networking", "announcements", "chat", "forms", "analytics"],
            ["submissions", "scoring", "sub-events", "speakers", "resources", "sponsors", "booths", "maps", "volunteers", "live-qa", "feedback", "entitlements"]),
        ["performance"] = new(
            ["speakers", "agenda", "paid", "finance", "attendance", "check-in", "announcements", "media-gallery", "reviews", "forms", "analytics"],
            ["sponsors", "maps", "volunteers", "networking", "polls", "chat", "feedback", "entitlements"]),
        ["community"] = new(
            ["networking", "announcements", "chat", "forms", "analytics"],
            ["sub-events", "speakers", "agenda", "resources", "paid", "finance", "sponsors", "maps", "attendance", "check-in", "volunteers", "polls", "live-qa", "media-gallery", "reviews", "feedback", "entitlements"]),
        ["civic"] = new(
            ["attendance", "volunteers", "announcements", "chat", "forms", "analytics"],
            ["teams", "speakers", "agenda", "resources", "sponsors", "maps", "check-in", "certificates", "networking", "polls", "media-gallery", "feedback", "entitlements"]),
        ["fundraising"] = new(
            ["finance", "sponsors", "announcements", "media-gallery", "forms", "analytics"],
            ["sub-events", "speakers", "agenda", "paid", "maps", "attendance", "check-in", "volunteers", "networking", "chat", "feedback", "entitlements"]),
        ["festival"] = new(
            ["tracks", "agenda", "sponsors", "maps", "attendance", "check-in", "networking", "announcements", "media-gallery", "chat", "feedback", "forms", "analytics"],
            ["teams", "mentors", "submissions", "problem-statements", "scoring", "leaderboard", "sub-events", "speakers", "resources", "paid", "finance", "booths", "certificates", "volunteers", "polls", "live-qa", "reviews", "entitlements"]),
        ["ceremonial"] = new(
            ["agenda", "attendance", "announcements", "media-gallery", "forms", "analytics"],
            ["sub-events", "speakers", "paid", "finance", "sponsors", "maps", "check-in", "certificates", "volunteers", "networking", "polls", "live-qa", "chat", "feedback", "entitlements"]),
        ["private-gathering"] = new(
            ["announcements", "media-gallery", "chat"],
            ["agenda", "maps", "attendance", "check-in", "polls", "forms", "analytics", "entitlements"]),
    };

    // D-266 M2: the Kind x capability matrix that used to live here is gone. It was reached through the
    // name-matched KindAlias chain D-188 flags as fragile, it could not express "unsupported", and it
    // referenced 27 slugs that are no longer event capabilities. The archetype matrix above replaces it.

}
