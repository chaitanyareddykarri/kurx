using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Users;

/// <summary>Experience — depth and breadth of verified event involvement over time (D-225; counts
/// made canonical in D-229).
///
/// <para><b>Every count comes from <see cref="ProfileCounts"/>.</b> This engine decides only how to
/// band them. It previously carried its own "organized" definition, which disagreed with both the
/// root profile and the metrics engine.</para>
///
/// <para><b>A band, never a score.</b> A single number invites comparison the data cannot support:
/// twelve hackathons and twelve conferences are not interchangeable, and no weighting makes them so.
/// The band always renders <i>with</i> the counts that produced it, so a reader can disagree with the
/// summary and still see the facts. Below the first threshold it reads "Building", not "Level 0" —
/// the D-212 precedent.</para>
///
/// <para><b>Employment and education are excluded by design.</b> Employment has no entity, no
/// evidence and no event linkage. Education is self-declared (D-220) and must never contribute to a
/// derived value, or a typed string would inflate it.</para></summary>
public static class ExperienceEngine
{
    public static ExperienceSummary Build(ProfileFactSet facts, DateTime now)
    {
        var organized = ProfileCounts.OrganizedEventIds(facts, now).Count;
        var participated = ProfileCounts.ParticipatedEventIds(facts, now).Count;
        var attended = ProfileCounts.AttendedEventIds(facts).Count;
        var distinct = ProfileCounts.DistinctEventIds(facts, now).Count;
        var leadership = ProfileCounts.LeadershipEventIds(facts, now).Count;
        var verifiedOrgs = facts.Memberships.Count(m => m.IsVerified);
        var (years, first) = ProfileCounts.ActiveSpan(facts, now);

        return new ExperienceSummary(
            Band(distinct, leadership, verifiedOrgs),
            distinct, organized, participated, attended, leadership,
            facts.Memberships.Select(m => m.OrgId).Distinct().Count(), verifiedOrgs,
            ProfileCounts.CompletedAssignmentEventIds(facts).Count,
            facts.Sessions.Count,
            ProfileCounts.CompetitionsWon(facts),
            years, first);
    }

    /// <summary>Band thresholds. Volume sets the floor; a leadership or verified-affiliation floor
    /// lifts it, because running five events is stronger evidence than attending twenty. Deliberately
    /// coarse and explainable — a reader should be able to reconstruct the band from the counts shown
    /// beside it.</summary>
    private static string Band(int distinctEvents, int leadershipEvents, int verifiedOrgs)
    {
        if (distinctEvents < 3) return "Building";

        var band = distinctEvents switch
        {
            < 10 => 1,
            < 25 => 2,
            < 50 => 3,
            _ => 4,
        };

        if (leadershipEvents >= 5) band = Math.Max(band, 2);
        if (leadershipEvents >= 15) band = Math.Max(band, 3);
        if (verifiedOrgs >= 1 && distinctEvents >= 10) band = Math.Max(band, 2);

        return band switch
        {
            1 => "Emerging",
            2 => "Active",
            3 => "Established",
            _ => "Distinguished",
        };
    }
}
