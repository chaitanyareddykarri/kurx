using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>Profile metrics (D-225, made canonical in D-229) — counts and rates that answer "what has
/// this person actually done", plus the Event DNA distribution.
///
/// <para><b>Every count comes from <see cref="ProfileCounts"/>.</b> This engine decides what is
/// <i>visible</i>; it never decides what a metric <i>means</i>. That split is what stopped three
/// surfaces from disagreeing about "events organized".</para>
///
/// <para><b>Hidden is null, never zero (D-229/H2).</b> A section this viewer may not see yields
/// <c>null</c> so the client renders "—". Returning 0 would convert the owner's privacy choice into a
/// factual claim about them — "organized 0 events" is a statement the data does not make, and it is a
/// statement the owner did not authorise.</para>
///
/// <para><b>Event DNA is a metric, not a subsystem.</b> One distribution over the curated
/// <c>EventKind</c> registry. The name survives in the UI because it is good product language.</para>
///
/// <para><b>Deliberately absent, each for a reason:</b> profile views (vanity, surveillance-adjacent);
/// points and leaderboard rank (engagement, not accomplishment — D-212 deleted the fictional tier
/// concept); ally count as a headline stat; any percentile or rank-against-others. No-show rate is
/// approved as owner-only but is <b>not faked</b> — the fact-set carries only checked-in attendance,
/// so it is deferred rather than stubbed with a placeholder.</para></summary>
public static class MetricsEngine
{
    private const int DnaTags = 8;

    public static ProfileMetrics Build(ProfileFactSet facts, SectionAccess access, DateTime now)
    {
        // Gate first, then count — never the reverse. Counting a hidden lane and suppressing the
        // label afterwards would leave the total as an oracle for what it contained.
        var seesEvents = access.CanSee(ProfileSection.Events);
        var seesAttended = access.CanSee(ProfileSection.Attended);
        var seesCerts = access.CanSee(ProfileSection.Certificates);
        var seesAchievements = access.CanSee(ProfileSection.Achievements);
        var seesOrgs = access.CanSee(ProfileSection.Organizations);
        var seesNetwork = access.CanSee(ProfileSection.Network);

        return new ProfileMetrics(
            EventsOrganized: seesEvents ? ProfileCounts.OrganizedEventIds(facts, now).Count : null,
            EventsParticipated: seesEvents ? ProfileCounts.ParticipatedEventIds(facts, now).Count : null,
            EventsAttended: seesAttended ? ProfileCounts.AttendedEventIds(facts).Count : null,
            CompletionRate: seesEvents ? ProfileCounts.CompletionRate(facts) : null,
            AssignmentsAccepted: seesEvents ? ProfileCounts.AssignedEventIds(facts, now).Count : null,
            AssignmentsCompleted: seesEvents ? ProfileCounts.CompletedAssignmentEventIds(facts).Count : null,
            CompetitionsEntered: seesAchievements ? ProfileCounts.CompetitionEventIds(facts).Count : null,
            CompetitionsWon: seesAchievements ? ProfileCounts.CompetitionsWon(facts) : null,
            SpeakerSessions: seesEvents ? ProfileCounts.SpeakerSessionCount(facts, now) : null,
            Certificates: seesCerts ? facts.Certificates.Count : null,
            AchievementCertificates: seesCerts ? ProfileCounts.AchievementCertificateCount(facts) : null,
            Organizations: seesOrgs ? facts.Memberships.Select(m => m.OrgId).Distinct().Count() : null,
            VerifiedOrganizations: seesOrgs ? facts.Memberships.Count(m => m.IsVerified) : null,
            AllyCount: seesNetwork ? facts.Trust.AllyCount : null,
            // Cities and DNA derive from the events map, which is itself only populated from lanes the
            // fact-set loaded — they carry no lane-specific disclosure of their own.
            Cities: seesEvents ? ProfileCounts.Cities(facts, now) : Array.Empty<string>(),
            EventDna: seesEvents ? ProfileCounts.EventDna(facts, DnaTags, now) : Array.Empty<EventDnaTag>());
    }
}
