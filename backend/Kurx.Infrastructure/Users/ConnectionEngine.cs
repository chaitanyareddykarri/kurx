using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>Professional relationships (D-226) — <i>why</i> two people know each other, derived from
/// the verified contexts they share.
///
/// <para><b>This is not a second connection system.</b> The connection itself is an
/// <c>AllyConnection</c> row with mutual consent, owned by <see cref="AllyService"/>; this engine only
/// explains it. Connection existence is <c>verified</c>; the relationship type is <c>derived</c>, with
/// the shared context as its evidence.</para>
///
/// <para><b>Directional by necessity.</b> "A judged B" is not the same statement as "B judged A", so
/// every relationship is produced from one subject's perspective and the inverse is a separate label.
/// A symmetric model would quietly assert things that are false.</para>
///
/// <para><b>No numeric strength.</b> A "connection strength" score invites a leaderboard of
/// friendships and is not defensible from this data. The output is ranked <i>reasons</i> in plain
/// language, which is the same choice <c>GetSuggestionsAsync</c> already makes.</para>
///
/// <para>Pure: two fact-sets in, relationships out. No queries — which is why it is safe to call for a
/// page of connections, and why the loader memoises per user (D-224).</para></summary>
public static class ConnectionEngine
{
    /// <summary>Roles that mean "ran this event" rather than "took part in it".</summary>
    private static readonly HashSet<string> OrganiserClass = ["owner", "manager", "coordinator"];

    public static IReadOnlyList<ProfileRelationship> Derive(ProfileFactSet subject, ProfileFactSet other)
    {
        var relationships = new List<ProfileRelationship>();

        // ── Shared events, paired by the role class each side held ──
        var sharedEventIds = subject.Events.Keys.Intersect(other.Events.Keys).ToList();
        var pairCounts = new Dictionary<string, (int Count, string Context)>();

        foreach (var eventId in sharedEventIds)
        {
            var mine = RolesOn(subject, eventId);
            var theirs = RolesOn(other, eventId);
            if (mine.Count == 0 || theirs.Count == 0) continue;

            var title = subject.Events[eventId].Title;
            foreach (var type in PairTypes(mine, theirs))
            {
                var current = pairCounts.TryGetValue(type, out var existing)
                    ? existing
                    : (Count: 0, Context: title);
                pairCounts[type] = (current.Count + 1, current.Context);
            }
        }

        foreach (var (type, value) in pairCounts.OrderByDescending(p => p.Value.Count))
            relationships.Add(new ProfileRelationship(type, Label(type), value.Count, value.Context));

        // ── Shared teams ──
        var sharedTeams = subject.Teams
            .Join(other.Teams, a => a.TeamId, b => b.TeamId, (a, b) => new { a, b })
            .ToList();
        foreach (var group in sharedTeams.GroupBy(t => TeamPairType(t.a.Role, t.b.Role)))
        {
            var first = group.First();
            relationships.Add(new ProfileRelationship(
                group.Key, Label(group.Key), group.Count(), first.a.TeamName));
        }

        // ── Shared organizations. A shared *verified* org is reported as a community rather than
        // collapsed into the same label — "we're both verified members of X" is a stronger statement.
        var sharedOrgs = subject.Memberships
            .Join(other.Memberships, a => a.OrgId, b => b.OrgId, (a, b) => new { a, b })
            .ToList();

        var communities = sharedOrgs.Where(o => o.a.IsVerified && o.b.IsVerified).ToList();
        if (communities.Count > 0)
            relationships.Add(new ProfileRelationship("shared_community", Label("shared_community"),
                communities.Count, communities[0].a.OrgName));

        var plainOrgs = sharedOrgs.Where(o => !(o.a.IsVerified && o.b.IsVerified)).ToList();
        if (plainOrgs.Count > 0)
            relationships.Add(new ProfileRelationship("shared_organization", Label("shared_organization"),
                plainOrgs.Count, plainOrgs[0].a.OrgName));

        // ── Shared achievements: both recognised at the same event. The most interesting shared
        // context Kurx can show, and it costs one intersection over facts already loaded.
        var myAchievementEvents = AchievementEventIds(subject);
        var theirAchievementEvents = AchievementEventIds(other);
        var sharedAchievements = myAchievementEvents.Intersect(theirAchievementEvents).ToList();
        if (sharedAchievements.Count > 0)
            relationships.Add(new ProfileRelationship("shared_achievement", Label("shared_achievement"),
                sharedAchievements.Count, subject.Events[sharedAchievements[0]].Title));

        return relationships;
    }

    /// <summary>Every role the person held on one event, across participations and assignments.</summary>
    private static HashSet<string> RolesOn(ProfileFactSet facts, Guid eventId)
    {
        var roles = facts.Participations.Where(p => p.EventId == eventId)
            .Select(p => p.RoleSlug).ToHashSet();

        foreach (var a in facts.Assignments.Where(a => a.EventId == eventId))
        {
            // The 14-role assignment vocabulary mapped onto participation slugs, so both sides of a
            // pair speak one language. Roles with no equivalent contribute "worked together" only.
            roles.Add(a.Role switch
            {
                "Volunteer" => "volunteer",
                "Judge" => "judge",
                "Host" => "owner",
                _ => "staff",
            });
        }

        if (facts.Sessions.Any(s => s.EventId == eventId)) roles.Add("speaker");
        return roles;
    }

    /// <summary>The relationship types produced by one pair of role sets, from the subject's side.</summary>
    private static IEnumerable<string> PairTypes(HashSet<string> mine, HashSet<string> theirs)
    {
        var myOrganiser = mine.Overlaps(OrganiserClass);
        var theirOrganiser = theirs.Overlaps(OrganiserClass);

        if (myOrganiser && theirOrganiser) yield return "organized_together";
        if (mine.Contains("volunteer") && theirs.Contains("volunteer")) yield return "volunteered_together";
        if (mine.Contains("competitor") && theirs.Contains("competitor")) yield return "competed_together";

        if (mine.Contains("judge") && theirs.Contains("competitor")) yield return "judged";
        if (mine.Contains("competitor") && theirs.Contains("judge")) yield return "judged_by";

        if (mine.Contains("speaker") && theirOrganiser) yield return "spoke_at_their_event";
        if (myOrganiser && theirs.Contains("speaker")) yield return "hosted_their_talk";

        if (mine.Contains("mentor") && theirs.Contains("competitor")) yield return "mentored";
        if (mine.Contains("competitor") && theirs.Contains("mentor")) yield return "mentored_by";

        // The catch-all, emitted only when nothing more specific applies: both did real work at the
        // same event. "attendee" alone is co-presence, not collaboration, so it does not qualify.
        var meaningfulMine = mine.Any(r => r != "attendee");
        var meaningfulTheirs = theirs.Any(r => r != "attendee");
        if (meaningfulMine && meaningfulTheirs) yield return "worked_together";
    }

    private static string TeamPairType(TeamRole mine, TeamRole theirs) => (mine, theirs) switch
    {
        (TeamRole.Captain, not TeamRole.Captain) => "led_their_team",
        (not TeamRole.Captain, TeamRole.Captain) => "team_led_by",
        (TeamRole.Mentor, _) => "mentored",
        (_, TeamRole.Mentor) => "mentored_by",
        _ => "teammates",
    };

    private static HashSet<Guid> AchievementEventIds(ProfileFactSet facts)
    {
        var ids = facts.Results.Select(r => r.EventId).ToHashSet();
        foreach (var c in facts.Certificates.Where(c =>
                     c.Kind is CertificateKind.Winner or CertificateKind.RunnerUp
                         or CertificateKind.Finalist or CertificateKind.Appreciation))
        {
            ids.Add(c.EventId);
        }
        return ids;
    }

    /// <summary>Plain-language labels, from the subject's perspective. Deliberately verbs and facts,
    /// never adjectives — "Judged their entry", not "Strong connection".</summary>
    private static string Label(string type) => type switch
    {
        "organized_together" => "Organized events together",
        "volunteered_together" => "Volunteered together",
        "competed_together" => "Competed together",
        "judged" => "Judged their entry",
        "judged_by" => "Judged by them",
        "spoke_at_their_event" => "Spoke at their event",
        "hosted_their_talk" => "Hosted their talk",
        "mentored" => "Mentored them",
        "mentored_by" => "Mentored by them",
        "teammates" => "Teammates",
        "led_their_team" => "Led their team",
        "team_led_by" => "On their team",
        "shared_organization" => "Same organization",
        "shared_community" => "Same verified community",
        "shared_achievement" => "Recognised at the same event",
        _ => "Worked together",
    };
}
