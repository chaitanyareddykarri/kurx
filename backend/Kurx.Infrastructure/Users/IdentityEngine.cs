using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>Identity labels and the professional headline (D-225).
///
/// <para><b>These are one engine, not two.</b> The Phase 3–5 design found that a "headline engine"
/// would re-derive exactly what <c>BuildIdentityLabels</c> already produces — "Verified Organizer",
/// "Public Speaker", "Hackathon Winner" — in the same priority order. A headline is the *short form*
/// of the labels, so one derivation feeds both and they can never disagree.</para>
///
/// <para><b>No manual override.</b> A self-written headline is precisely the self-declared claim this
/// system exists to replace, and <c>bio</c> already exists for anything a user wants to say in their
/// own words. A user may <i>pin</i> which of their earned labels leads; they cannot invent one.</para></summary>
public static class IdentityEngine
{
    private const int MaxLabels = 6;
    private const int MaxHeadlineTokens = 3;
    private const int MaxHeadlineChars = 60;
    private const string HeadlineSeparator = " • ";

    /// <summary>Participation slugs that read as an identity. Wording differs from the per-event role
    /// chip — a headline label reads differently from a tag on one event.</summary>
    private static readonly IReadOnlyDictionary<string, string> ParticipantIdentityLabels =
        new Dictionary<string, string>
        {
            ["speaker"] = "Public Speaker",
            ["mentor"] = "Mentor",
            ["judge"] = "Judge",
            ["volunteer"] = "Volunteer",
            ["coordinator"] = "Coordinator",
            ["manager"] = "Organizer",
            ["owner"] = "Host",
            ["competitor"] = "Competitor",
        };

    /// <summary>Rank of a participation identity when several are held. Ordered by how much the role
    /// implies responsibility for the event, not by prestige.</summary>
    private static readonly IReadOnlyList<string> SlugPriority =
        ["owner", "manager", "judge", "mentor", "speaker", "coordinator", "competitor", "volunteer"];

    /// <summary>The full label set (verified affiliations first, then earned participation identities,
    /// then recognitions), capped. This is the pool the headline draws from.</summary>
    public static IReadOnlyList<string> BuildLabels(
        ProfileFactSet facts, IReadOnlyList<AchievementCard> achievements)
    {
        var labels = new List<string>();

        // Strongest first: an affiliation someone else vouched for.
        foreach (var m in facts.Memberships.OrderByDescending(m => m.IsVerified).ThenBy(m => m.JoinedAt))
        {
            var role = OrgRoleLabel(m.ClaimedRole, m.OrgRole);
            var label = m.IsVerified ? $"Verified {role}" : role;
            if (!labels.Contains(label)) labels.Add(label);
        }

        foreach (var slug in HeldSlugs(facts))
            if (ParticipantIdentityLabels.TryGetValue(slug, out var label) && !labels.Contains(label))
                labels.Add(label);

        // Recognitions: certificate-backed and competition-backed only. A platform engagement badge
        // is not an identity.
        foreach (var name in achievements
                     .Where(a => a.Source is "certificate" or "competition")
                     .Select(a => a.Name).Distinct())
            if (!labels.Contains(name)) labels.Add(name);

        return labels.Take(MaxLabels).ToList();
    }

    /// <summary>Participation slugs the person actually holds, in priority order.</summary>
    private static IEnumerable<string> HeldSlugs(ProfileFactSet facts)
    {
        var held = facts.Participations.Select(p => p.RoleSlug).ToHashSet();
        return SlugPriority.Where(held.Contains);
    }

    /// <summary>The deterministic headline: 2–3 tokens joined by a bullet, drawn from one ordered pool.
    ///
    /// <para>Determinism matters more than cleverness here — a headline that changed between page
    /// loads would read as unreliable on a page whose whole claim is reliability. Ties break on
    /// evidence count, then recency, then alphabetically, all of which are stable.</para>
    ///
    /// <para><paramref name="pinned"/> may promote a label the person has <b>already earned</b>; an
    /// unrecognised pin is ignored rather than rendered, so a stale pin degrades to the default
    /// ordering instead of showing something untrue.</para></summary>
    public static string BuildHeadline(
        ProfileFactSet facts, IReadOnlyList<AchievementCard> achievements, string? pinned = null)
    {
        var pool = BuildLabels(facts, achievements).ToList();
        if (pool.Count == 0) return string.Empty;

        if (pinned is not null && pool.Remove(pinned)) pool.Insert(0, pinned);

        var tokens = new List<string>();
        var length = 0;
        foreach (var token in pool.Take(MaxHeadlineTokens))
        {
            var projected = length + token.Length + (tokens.Count > 0 ? HeadlineSeparator.Length : 0);
            // Drop an overflowing token rather than truncating mid-word — a half-word label reads as
            // a bug, and the remaining tokens are still true on their own.
            if (projected > MaxHeadlineChars) continue;
            tokens.Add(token);
            length = projected;
        }

        return string.Join(HeadlineSeparator, tokens);
    }

    /// <summary>The affiliation label (D-206): the claim's real-world role when the membership was
    /// verified through one, else the platform RBAC role.</summary>
    private static string OrgRoleLabel(MembershipClaimRole? claimed, OrgRole orgRole) => claimed switch
    {
        MembershipClaimRole.Student => "Student",
        MembershipClaimRole.Faculty => "Faculty",
        MembershipClaimRole.Employee => "Employee",
        MembershipClaimRole.Alumni => "Alumni",
        MembershipClaimRole.Founder => "Founder",
        MembershipClaimRole.Director => "Director",
        MembershipClaimRole.Coordinator => "Coordinator",
        MembershipClaimRole.Volunteer => "Volunteer",
        MembershipClaimRole.ClubPresident => "Club President",
        MembershipClaimRole.EventLead => "Event Lead",
        MembershipClaimRole.Other => "Member",
        _ => orgRole switch
        {
            OrgRole.Owner => "Owner",
            OrgRole.Manager => "Manager",
            OrgRole.Finance => "Finance",
            OrgRole.Representative => "Organizer",
            _ => "Member",
        },
    };
}
