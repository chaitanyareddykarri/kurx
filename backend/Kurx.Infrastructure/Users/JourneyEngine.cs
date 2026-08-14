using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>The Professional Journey (D-223) — Kurx's signature profile feature.
///
/// <para>A <b>first-attainment ladder</b>: the first time this person provably reached each
/// professional tier, each node anchored to the row that proved it. Deliberately not a second
/// timeline — the Timeline answers "what has this person done" (every milestone), the Journey answers
/// "how have they grown" (the first time each tier was reached). Same source rows, different question,
/// no duplicated aggregation.</para>
///
/// <para><b>Ordering is chronological, never a canonical career ladder.</b> Someone can organize an
/// event before ever volunteering at one; presenting a fixed Participant→Volunteer→…→Mentor sequence
/// would assert a progression the data does not support. Tier weight affects visual emphasis only.</para>
///
/// <para><b>No suggested next step.</b> "You are 2 events from Organizer" would turn identity into
/// gamification — precisely the fictional-tier concept D-212 deleted rather than patched.</para>
///
/// <para>Since D-224 this is a <b>pure function</b> over <see cref="ProfileFactSet"/> — it opens no
/// connection and issues no query, which is what makes it testable without a database and what lets
/// the Resume project it alongside every other section from a single load.</para></summary>
public static class JourneyEngine
{
    /// <summary>Which section gates each tier, so a node sourced from a hidden section is omitted for
    /// that viewer rather than leaking through the Journey. Every tier must appear here — a tier with
    /// no gate would be a bypass of the resolver.</summary>
    private static readonly IReadOnlyDictionary<string, ProfileSection> TierGates =
        new Dictionary<string, ProfileSection>
        {
            ["attendee"] = ProfileSection.Attended,
            ["participant"] = ProfileSection.Events,
            ["volunteer"] = ProfileSection.Events,
            ["team_lead"] = ProfileSection.Events,
            ["competition_winner"] = ProfileSection.Achievements,
            ["speaker"] = ProfileSection.Events,
            ["judge"] = ProfileSection.Events,
            ["mentor"] = ProfileSection.Events,
            ["organizer"] = ProfileSection.Events,
            ["host"] = ProfileSection.Events,
            ["verified_member"] = ProfileSection.Organizations,
        };

    /// <summary>Participation role slugs that map onto a tier. The slugs are the real
    /// <c>ParticipantRole</c> registry values (D-202) — no new vocabulary is invented here.</summary>
    private static readonly IReadOnlyDictionary<string, string> SlugTiers = new Dictionary<string, string>
    {
        ["attendee"] = "participant",
        ["competitor"] = "participant",
        ["volunteer"] = "volunteer",
        ["speaker"] = "speaker",
        ["judge"] = "judge",
        ["mentor"] = "mentor",
        ["manager"] = "organizer",
        ["owner"] = "host",
    };

    public static IReadOnlyList<JourneyNode> Build(ProfileFactSet facts, SectionAccess access)
    {
        var nodes = new List<JourneyNode>();

        // ── Participation-derived tiers ──
        foreach (var group in facts.Participations
                     .Where(p => SlugTiers.ContainsKey(p.RoleSlug))
                     .GroupBy(p => SlugTiers[p.RoleSlug]))
        {
            // Dedupe by event first: two roles on one event is one occurrence of that tier, matching
            // the dedup rule every other count on this profile already uses (D-201).
            var eventIds = group.Select(p => p.EventId).Distinct().ToList();
            var earliest = eventIds.Select(id => facts.Events[id]).MinBy(e => e.StartsAt)!;
            nodes.Add(new JourneyNode(group.Key, earliest.StartsAt, eventIds.Count,
                "event_participant", earliest.Title, earliest.Slug, null, null));
        }

        // ── Volunteer / judge / host via EventAssignment — the 14-role operational vocabulary, which
        // the participation lane collapses. Merged into the same tier rather than duplicated.
        foreach (var group in facts.Assignments
                     .Select(a => (Tier: AssignmentTier(a.Role), a.EventId))
                     .Where(a => a.Tier is not null)
                     .GroupBy(a => a.Tier!))
        {
            var eventIds = group.Select(a => a.EventId).Distinct().ToList();
            var earliest = eventIds.Select(id => facts.Events[id]).MinBy(e => e.StartsAt)!;
            MergeTier(nodes, new JourneyNode(group.Key, earliest.StartsAt, eventIds.Count,
                "event_assignment", earliest.Title, earliest.Slug, null, null));
        }

        // ── Speaker via the organizer-made Speaker.UserId link (D-208) ──
        if (facts.Sessions.Count > 0)
        {
            var earliest = facts.Sessions.MinBy(s => s.StartsAt)!;
            var ev = facts.Events[earliest.EventId];
            MergeTier(nodes, new JourneyNode("speaker", earliest.StartsAt, facts.Sessions.Count,
                "event_session_speaker", ev.Title, ev.Slug, earliest.SessionTitle, null));
        }

        // ── Attendee: a verified check-in ──
        if (facts.Attendance.Count > 0)
        {
            var withDates = facts.Attendance
                .Select(a => (a.EventId, When: a.CheckedInAt ?? facts.Events[a.EventId].StartsAt))
                .ToList();
            var earliest = withDates.MinBy(a => a.When);
            var ev = facts.Events[earliest.EventId];
            nodes.Add(new JourneyNode("attendee", earliest.When,
                withDates.Select(a => a.EventId).Distinct().Count(),
                "ticket_checked_in", ev.Title, ev.Slug, null, null));
        }

        // ── Team lead / mentor via TeamMembership ──
        foreach (var group in facts.Teams
                     .Where(t => t.Role is TeamRole.Captain or TeamRole.Mentor)
                     .GroupBy(t => t.Role == TeamRole.Captain ? "team_lead" : "mentor"))
        {
            var earliest = group.MinBy(t => t.JoinedAt)!;
            MergeTier(nodes, new JourneyNode(group.Key, earliest.JoinedAt, group.Count(),
                "team_membership", earliest.TeamName, null, null, null));
        }

        // ── Competition winner: a published first place ──
        var wins = facts.Results.Where(r => r.Rank == 1).ToList();
        if (wins.Count > 0)
        {
            var earliest = wins.MinBy(r => r.OccurredAt)!;
            var ev = facts.Events[earliest.EventId];
            nodes.Add(new JourneyNode("competition_winner", earliest.OccurredAt, wins.Count,
                "stage_result", ev.Title, ev.Slug, earliest.StageName, null));
        }

        // ── Verified member: an org verified this person's affiliation ──
        var verified = facts.Memberships.Where(m => m.IsVerified && m.VerifiedAt.HasValue).ToList();
        if (verified.Count > 0)
        {
            var earliest = verified.MinBy(m => m.VerifiedAt!.Value)!;
            nodes.Add(new JourneyNode("verified_member", earliest.VerifiedAt!.Value, verified.Count,
                "membership_verified", null, null, null, earliest.OrgName));
        }

        // Privacy last, in one place: a tier whose gating section this viewer cannot see is dropped
        // entirely rather than summarised, so the Journey can never disclose what a section hides.
        return nodes
            .Where(n => TierGates.TryGetValue(n.Tier, out var section) && access.CanSee(section))
            .OrderBy(n => n.FirstAttainedAt)
            .ToList();
    }

    /// <summary>Keeps the earliest attainment when two sources feed one tier (e.g. a volunteer role
    /// recorded both as a participation and as an assignment) and sums the occurrences.</summary>
    private static void MergeTier(List<JourneyNode> nodes, JourneyNode incoming)
    {
        var existing = nodes.FindIndex(n => n.Tier == incoming.Tier);
        if (existing < 0)
        {
            nodes.Add(incoming);
            return;
        }

        var current = nodes[existing];
        nodes[existing] = current.FirstAttainedAt <= incoming.FirstAttainedAt
            ? current with { Occurrences = current.Occurrences + incoming.Occurrences }
            : incoming with { Occurrences = current.Occurrences + incoming.Occurrences };
    }

    /// <summary>The assignment roles that correspond to a journey tier. Roles with no tier (Media
    /// Team, Photographer, …) are real service but not a distinct professional rung, so they are
    /// counted by the assignments section and deliberately not invented into one here.</summary>
    private static string? AssignmentTier(string role) => role switch
    {
        "Volunteer" => "volunteer",
        "Judge" => "judge",
        "Host" => "host",
        _ => null,
    };
}
