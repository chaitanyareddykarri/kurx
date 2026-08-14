using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>The canonical definition of every countable thing on a profile (D-229).
///
/// <para><b>Why this type exists.</b> "Events organized" was previously computed in three places —
/// the cached root summary, <see cref="MetricsEngine"/> and <see cref="ExperienceEngine"/> — with
/// three different filters. They disagreed, so one profile page could display three different numbers
/// for one concept. On a product whose whole claim is that its numbers are trustworthy, that is worse
/// than a missing feature. Patching the three to agree would only have delayed the next divergence;
/// the fix is to make the question answerable in exactly one place.</para>
///
/// <para><b>Every count is a set of event ids, not an integer.</b> Callers that need a number take
/// <c>.Count</c>; callers that need the events themselves (the resume, the timeline) get the same set
/// rather than re-deriving it. Returning the set is what stops a second definition growing.</para>
///
/// <para>Pure functions over <see cref="ProfileFactSet"/>. No queries, no gating — <b>privacy is
/// applied by the caller at emission</b>, because a count's visibility depends on the viewer while
/// its definition does not.</para>
///
/// <para><b>Every count here is COMPLETED activity (D-234).</b> A profile is a record of what someone
/// has done, so nothing counts until its event has ended. This is not a filter applied for tidiness:
/// the event <i>listings</i> in <c>PublicProfileService.GetEventsAsync</c> have always filtered
/// <c>EndsAt &lt; now</c>, so while the metrics did not, a tab could read "Events (8)" above a list of
/// five. Worse, registering for future events inflated a profile's apparent experience — the one thing
/// this product cannot let happen.</para>
///
/// <para>The two exceptions are exceptions only in appearance. <see cref="AttendedEventIds"/> needs no
/// filter because a check-in can only be recorded at the event; <see cref="CompetitionsWon"/> and
/// <see cref="CompetitionEventIds"/> read published results, which exist only after a competition has
/// been judged. Both are completed by construction, and each says so where it is defined.</para>
///
/// <para>Scheduled activity is deliberately <b>not</b> surfaced as a metric. Adding an "upcoming"
/// figure would be a new feature; the honest minimum is that no existing number overstates.</para></summary>
public static class ProfileCounts
{
    /// <summary>Declared as the concrete <see cref="HashSet{T}"/>, not <c>IReadOnlySet</c>: EF Core
    /// translates <c>HashSet.Contains</c> into SQL but throws on the interface, so exposing the
    /// interface here silently broke every query that filtered on it at runtime rather than at
    /// compile time. A rare case where the less abstract type is the correct one.</summary>
    public static readonly HashSet<CertificateKind> AchievementKinds =
    [
        CertificateKind.Winner, CertificateKind.RunnerUp,
        CertificateKind.Finalist, CertificateKind.Appreciation,
    ];

    /// <summary>Participation states that count as real involvement. An <c>Invited</c> row records
    /// an organizer's intent, not the person's activity, so it is never counted.</summary>
    public static bool IsRealInvolvement(ParticipantState state) =>
        state is ParticipantState.Accepted or ParticipantState.Active or ParticipantState.Completed;

    /// <summary>Events this person ran, that have finished.
    ///
    /// <para>Two decisions live here and nowhere else: organizing is <see cref="ProfileEventFact.IsOrganizedByUser"/>
    /// (decided at load, covering both "created it" and "member of the running org"), and an event
    /// only counts <b>once it has ended</b> — an event you have not run yet is not something you have
    /// done. The old metrics and experience engines omitted that time filter and inflated the number
    /// against the root profile's own figure.</para></summary>
    public static IReadOnlySet<Guid> OrganizedEventIds(ProfileFactSet facts, DateTime now) =>
        facts.Events.Values
            .Where(e => e.IsOrganizedByUser && e.EndsAt < now)
            .Select(e => e.EventId)
            .ToHashSet();

    /// <summary>Has this event finished? The single time predicate every count below shares (D-234).
    ///
    /// <para>An event absent from the fact set is treated as <b>not</b> finished — the conservative
    /// direction, since the alternative is counting something we cannot date as completed experience.
    /// </para></summary>
    private static bool HasEnded(ProfileFactSet facts, Guid eventId, DateTime now) =>
        facts.Events.TryGetValue(eventId, out var e) && e.EndsAt < now;

    /// <summary>Events with a public participation row in a real state, deduped by event — two roles
    /// on one event is one event (D-201). <b>Completed only</b> (D-234): being on the roster of a
    /// conference that runs next month is not experience you have.</summary>
    public static IReadOnlySet<Guid> ParticipatedEventIds(ProfileFactSet facts, DateTime now) =>
        facts.Participations
            .Where(p => HasEnded(facts, p.EventId, now))
            .Select(p => p.EventId).ToHashSet();

    /// <summary>Events with a verified check-in, deduped — a person may hold several tickets.
    /// No time filter is needed or wanted: a check-in can only be recorded at the event, so every row
    /// here is already completed activity by construction.</summary>
    public static IReadOnlySet<Guid> AttendedEventIds(ProfileFactSet facts) =>
        facts.Attendance.Select(a => a.EventId).ToHashSet();

    /// <summary><b>Completed only</b> (D-234) — an accepted assignment to a future event is a
    /// commitment, not a credit.</summary>
    public static IReadOnlySet<Guid> AssignedEventIds(ProfileFactSet facts, DateTime now) =>
        facts.Assignments
            .Where(a => HasEnded(facts, a.EventId, now))
            .Select(a => a.EventId).ToHashSet();

    public static IReadOnlySet<Guid> CompletedAssignmentEventIds(ProfileFactSet facts) =>
        facts.Assignments.Where(a => a.CompletedAt.HasValue).Select(a => a.EventId).ToHashSet();

    /// <summary>Every event this person is connected to in any lane, counted once. The honest answer
    /// to "how many events has this person been part of" — and honest requires <b>completed only</b>
    /// (D-234), including the speaking lane: a talk scheduled for next month is not a talk given.</summary>
    public static IReadOnlySet<Guid> DistinctEventIds(ProfileFactSet facts, DateTime now) =>
        OrganizedEventIds(facts, now)
            .Concat(ParticipatedEventIds(facts, now))
            .Concat(AttendedEventIds(facts))
            .Concat(AssignedEventIds(facts, now))
            .Concat(facts.Sessions.Where(s => HasEnded(facts, s.EventId, now)).Select(s => s.EventId))
            .ToHashSet();

    /// <summary>Roles that carry responsibility for an event rather than participation in it.</summary>
    private static readonly HashSet<string> LeadershipSlugs = ["owner", "manager", "coordinator"];

    /// <summary><b>Completed only</b> (D-234) — this feeds the Experience band, which is a claim about
    /// responsibility already carried, not responsibility accepted for later.</summary>
    public static IReadOnlySet<Guid> LeadershipEventIds(ProfileFactSet facts, DateTime now) =>
        facts.Participations
            .Where(p => LeadershipSlugs.Contains(p.RoleSlug) && HasEnded(facts, p.EventId, now))
            .Select(p => p.EventId)
            .ToHashSet();

    /// <summary>Talks actually given. <b>Completed only</b> (D-234) — this was read straight off
    /// <c>facts.Sessions.Count</c>, so a speaker slot accepted for a conference next quarter counted as
    /// a delivered session.</summary>
    public static int SpeakerSessionCount(ProfileFactSet facts, DateTime now) =>
        facts.Sessions.Count(s => HasEnded(facts, s.EventId, now));

    public static int AchievementCertificateCount(ProfileFactSet facts) =>
        facts.Certificates.Count(c => AchievementKinds.Contains(c.Kind));

    public static int CompetitionsWon(ProfileFactSet facts) => facts.Results.Count(r => r.Rank == 1);

    public static IReadOnlySet<Guid> CompetitionEventIds(ProfileFactSet facts) =>
        facts.Results.Select(r => r.EventId).ToHashSet();

    /// <summary>Completion rate over participations that reached a terminal state. Invited-but-never
    /// -answered rows are excluded from the denominator: they measure the organizer's invitation
    /// habits, not this person's follow-through. Null when there is nothing to rate — never 0, which
    /// would read as "never completes anything".</summary>
    public static double? CompletionRate(ProfileFactSet facts)
    {
        var terminal = facts.Participations
            .Where(p => p.State is ParticipantState.Completed or ParticipantState.Active)
            .Select(p => p.EventId).Distinct().Count();
        if (terminal == 0) return null;

        var completed = facts.Participations
            .Where(p => p.State == ParticipantState.Completed)
            .Select(p => p.EventId).Distinct().Count();
        return Math.Round((double)completed / terminal, 3);
    }

    /// <summary>Years since the earliest real activity, not since signup — an account created three
    /// years ago and used last week has not been active for three years.</summary>
    public static (int Years, DateTime? First) ActiveSpan(ProfileFactSet facts, DateTime now)
    {
        var dates = new List<DateTime>();
        dates.AddRange(facts.Participations.Select(p => facts.Events[p.EventId].StartsAt));
        dates.AddRange(facts.Attendance.Select(a => a.CheckedInAt ?? facts.Events[a.EventId].StartsAt));
        dates.AddRange(facts.Sessions.Select(s => s.StartsAt));
        dates.AddRange(facts.Memberships.Select(m => m.JoinedAt));

        if (dates.Count == 0) return (0, null);
        var first = dates.Min();
        return (Math.Max(0, (int)((now - first).TotalDays / 365)), first);
    }

    /// <summary>Event DNA — the kind distribution across every event this person touches.
    /// <b>Completed only</b> (D-234): the DNA describes what someone has actually done, and one
    /// upcoming hackathon should not relabel a person as a hackathon regular.</summary>
    public static IReadOnlyList<EventDnaTag> EventDna(ProfileFactSet facts, int take, DateTime now) =>
        facts.Events.Values
            .Where(e => !string.IsNullOrWhiteSpace(e.KindSlug) && e.EndsAt < now)
            .GroupBy(e => e.KindSlug!)
            .Select(g => new EventDnaTag(g.Key, g.Count()))
            .OrderByDescending(t => t.Count).ThenBy(t => t.Kind)
            .Take(take)
            .ToList();

    /// <summary><b>Completed only</b> (D-234) — "has worked in these cities", not "has a ticket to".</summary>
    public static IReadOnlyList<string> Cities(ProfileFactSet facts, DateTime now) =>
        facts.Events.Values
            .Where(e => !string.IsNullOrWhiteSpace(e.City) && e.EndsAt < now)
            .Select(e => e.City).Distinct().OrderBy(c => c).ToList();
}
