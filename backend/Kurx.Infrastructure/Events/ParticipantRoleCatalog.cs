using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Events;

/// <summary>The platform participant-role registry (V3 §5.3, Phase 6): the 7 hardcoded classes and their
/// standard slugs, with the role properties §5.2/§5.4 need. Closed catalog seeded idempotently at startup;
/// org-extended slugs are a later write path. Only permissions that exist today are declared — ORGANISER
/// roles grant event-scoped <c>participants:manage</c> (the §5.4 fourth grant source); <c>scoring:submit</c>
/// is declared on evaluators for the scoring phase but has no consumer yet.</summary>
public static class ParticipantRoleCatalog
{
    public record RoleDef(string Slug, string Name, ParticipantClass Class, bool CountsTowardCapacity,
        string? InventorySegment, bool IsPublic, string[] DefaultPermissions);

    private const string Manage = "participants:manage";
    private const string EventManage = "event:manage";
    private const string Score = "scoring:submit";
    private static readonly string[] None = [];

    public static readonly RoleDef[] Roles =
    [
        // ── ORGANISER — run the event; the §5.4 event-scoped grant source ───────────────────────────────
        new("owner",        "Owner",        ParticipantClass.Organiser,  false, null, false, [Manage, EventManage]),
        new("manager",      "Manager",      ParticipantClass.Organiser,  false, null, false, [Manage, EventManage]),
        new("coordinator",  "Coordinator",  ParticipantClass.Organiser,  false, null, false, [Manage]),
        new("staff",        "Staff",        ParticipantClass.Organiser,  false, null, false, None),

        // ── OPERATIONS — no attendee inventory (§5.2) ───────────────────────────────────────────────────
        new("volunteer",         "Volunteer",         ParticipantClass.Operations, false, null, false, None),
        new("security",          "Security",          ParticipantClass.Operations, false, null, false, None),
        new("medical",           "Medical",           ParticipantClass.Operations, false, null, false, None),
        new("technical",         "Technical",         ParticipantClass.Operations, false, null, false, None),
        new("registration_desk", "Registration Desk", ParticipantClass.Operations, false, null, false, None),

        // ── CONTENT — publicly listed ───────────────────────────────────────────────────────────────────
        new("speaker",   "Speaker",   ParticipantClass.Content, false, null, true, None),
        new("artist",    "Artist",    ParticipantClass.Content, false, null, true, None),
        new("performer", "Performer", ParticipantClass.Content, false, null, true, None),
        new("panelist",  "Panelist",  ParticipantClass.Content, false, null, true, None),
        new("trainer",   "Trainer",   ParticipantClass.Content, false, null, true, None),

        // ── EVALUATION — scoring capability references these (§5.1) ──────────────────────────────────────
        new("judge",      "Judge",      ParticipantClass.Evaluation, false, null, true,  [Score]),
        new("examiner",   "Examiner",   ParticipantClass.Evaluation, false, null, false, [Score]),
        new("referee",    "Referee",    ParticipantClass.Evaluation, false, null, true,  [Score]),
        new("scrutineer", "Scrutineer", ParticipantClass.Evaluation, false, null, false, None),
        new("mentor",     "Mentor",     ParticipantClass.Evaluation, false, null, true,  None),

        // ── PARTICIPANT — consume attendee inventory (§5.2) ─────────────────────────────────────────────
        new("attendee",    "Attendee",    ParticipantClass.Participant, true, "general", false, None),
        new("competitor",  "Competitor",  ParticipantClass.Participant, true, "general", true,  None),
        new("team_member", "Team Member", ParticipantClass.Participant, true, "general", true,  None),
        new("delegate",    "Delegate",    ParticipantClass.Participant, true, "general", false, None),

        // ── COMMERCIAL — publicly listed, no attendee inventory ─────────────────────────────────────────
        new("sponsor",   "Sponsor",   ParticipantClass.Commercial, false, null, true,  None),
        new("exhibitor", "Exhibitor", ParticipantClass.Commercial, false, null, true,  None),
        new("recruiter", "Recruiter", ParticipantClass.Commercial, false, null, false, None),
        new("vendor",    "Vendor",    ParticipantClass.Commercial, false, null, false, None),

        // ── OBSERVER — VIP consumes the VIP segment; Media/Guest/Chaperone consume none (§5.2) ───────────
        new("vip",       "VIP",       ParticipantClass.Observer, true,  "vip",  false, None),
        new("media",     "Media",     ParticipantClass.Observer, false, null,   true,  None),
        new("guest",     "Guest",     ParticipantClass.Observer, false, null,   false, None),
        new("chaperone", "Chaperone", ParticipantClass.Observer, false, null,   false, None),
    ];

    /// <summary>V2 EventAssignment.Role → V3 slug for the one-time backfill. Judgment calls are commented;
    /// the free-text V2 role is preserved in EventParticipant.CustomLabel so nothing is lost.</summary>
    public static readonly IReadOnlyDictionary<string, string> AssignmentRoleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Volunteer"] = "volunteer",
        ["Judge"] = "judge",
        ["Moderator"] = "panelist",             // judgment: a moderator runs a panel
        ["Registration Desk"] = "registration_desk",
        ["Stage Manager"] = "technical",        // judgment: stage/technical operations
        ["Security"] = "security",
        ["Photographer"] = "media",             // judgment: media/observer
        ["Videographer"] = "media",             // judgment: media/observer
        ["Host"] = "coordinator",               // judgment: the MC runs the show
        ["Media Team"] = "media",
        ["Speaker Coordinator"] = "coordinator",
        ["Technical Team"] = "technical",
        ["Support Team"] = "volunteer",         // judgment: general support ≈ volunteer
        ["Custom"] = "staff",                   // generic ORGANISER seat; the free-text name rides in CustomLabel
    };
}
