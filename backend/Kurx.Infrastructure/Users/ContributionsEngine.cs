using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>The contributions heatmap (D-228) — daily activity density over a rolling window.
///
/// <para><b>The privacy trap this engine exists to get right: gate, then bucket. Never bucket, then
/// gate.</b> If a day's contributions were counted first and the hidden ones suppressed afterwards,
/// the day's intensity would still reflect them — and a viewer could read the presence of hidden
/// activity straight off the grid. Each contributing fact is filtered by its own section <i>before</i>
/// it reaches a bucket, so a restricted viewer sees a genuinely emptier heatmap rather than an
/// oracle for what the owner hid.</para>
///
/// <para>Inputs are timestamps the fact-set already carries. Deliberately <b>not</b> logins, page
/// views, or points: those measure engagement with Kurx, not contribution to events, and a heatmap
/// that lights up for browsing would be a vanity surface.</para></summary>
public static class ContributionsEngine
{
    /// <summary>Intensity bands. Four levels plus empty, matching what the clients render — a finer
    /// scale would imply a precision daily counts do not have.</summary>
    private static int Level(int count) => count switch
    {
        0 => 0,
        1 => 1,
        <= 3 => 2,
        <= 6 => 3,
        _ => 4,
    };

    public static ContributionsSummary Build(
        ProfileFactSet facts, SectionAccess access, int months, DateTime now)
    {
        var from = now.Date.AddMonths(-Math.Clamp(months, 1, 12));
        var counts = new Dictionary<DateTime, int>();

        void Add(DateTime when)
        {
            var day = when.Date;
            if (day < from || day > now.Date) return;
            counts[day] = counts.GetValueOrDefault(day) + 1;
        }

        // Each lane is gated by the section that owns its evidence, before anything is bucketed.
        if (access.CanSee(ProfileSection.Attended))
            foreach (var a in facts.Attendance)
                Add(a.CheckedInAt ?? facts.Events[a.EventId].StartsAt);

        if (access.CanSee(ProfileSection.Events))
        {
            foreach (var p in facts.Participations)
                Add(facts.Events[p.EventId].StartsAt);
            foreach (var a in facts.Assignments.Where(a => a.CompletedAt.HasValue))
                Add(a.CompletedAt!.Value);
            foreach (var s in facts.Sessions)
                Add(s.StartsAt);
        }

        if (access.CanSee(ProfileSection.Certificates))
            foreach (var c in facts.Certificates)
                Add(c.IssuedAt);

        if (access.CanSee(ProfileSection.Achievements))
            foreach (var r in facts.Results)
                Add(r.OccurredAt);

        if (access.CanSee(ProfileSection.Organizations))
            foreach (var m in facts.Memberships.Where(m => m.VerifiedAt.HasValue))
                Add(m.VerifiedAt!.Value);

        var days = counts
            .OrderBy(kv => kv.Key)
            .Select(kv => new ContributionDay(DateOnly.FromDateTime(kv.Key), kv.Value, Level(kv.Value)))
            .ToList();

        return new ContributionsSummary(
            DateOnly.FromDateTime(from),
            DateOnly.FromDateTime(now.Date),
            days.Sum(d => d.Count),
            days);
    }
}
