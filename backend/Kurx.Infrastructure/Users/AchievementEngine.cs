using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>Achievements (D-203; moved onto the fact-set in D-229) — recognitions, not certificates
/// and not generic engagement badges.
///
/// <para>Three independently-swappable sources returning one shape, exactly as D-203 designed. Adding
/// a fourth is one method here and nothing anywhere else. What changed in D-229 is that all three now
/// project the fact-set instead of issuing their own queries — the competition source in particular
/// used to re-run the whole published-results join that the fact-set already held, which was a second
/// definition of "published result" waiting to drift.</para></summary>
public static class AchievementEngine
{
    public static IReadOnlyList<AchievementCard> Build(ProfileFactSet facts, SectionAccess access, int take)
    {
        var cards = new List<AchievementCard>();

        // Certificate-backed recognitions. Gated with the certificates section: a hidden certificate
        // must not reappear here wearing a different label.
        if (access.CanSee(ProfileSection.Certificates))
            cards.AddRange(FromCertificates(facts, take));

        if (access.CanSee(ProfileSection.Achievements))
            cards.AddRange(FromCompetitionResults(facts, take));

        // Platform badges carry no event and so no section of their own; they ride with Achievements.
        if (access.CanSee(ProfileSection.Achievements))
            cards.AddRange(FromBadges(facts, take));

        return cards.OrderByDescending(a => a.EarnedAt).Take(take).ToList();
    }

    private static IEnumerable<AchievementCard> FromCertificates(ProfileFactSet facts, int take) =>
        facts.Certificates
            .Where(c => ProfileCounts.AchievementKinds.Contains(c.Kind))
            .OrderByDescending(c => c.IssuedAt)
            .Take(take)
            .Select(c =>
            {
                var ev = facts.Events[c.EventId];
                return new AchievementCard(
                    CertLabel(c.Kind), $"at {ev.Title}", null, c.IssuedAt,
                    "certificate", ev.Title, ev.Slug, ev.OrgName);
            });

    /// <summary>A judged placement is the strongest recognition the platform holds — it is decided by
    /// judges, not issued by an organizer.</summary>
    private static IEnumerable<AchievementCard> FromCompetitionResults(ProfileFactSet facts, int take) =>
        facts.Results
            .OrderBy(r => r.Rank).ThenByDescending(r => r.OccurredAt)
            .Take(take)
            .Select(r =>
            {
                var ev = facts.Events[r.EventId];
                return new AchievementCard(
                    RankLabel(r.Rank), $"{r.StageName} · {ev.Title}", null, r.OccurredAt,
                    "competition", ev.Title, ev.Slug, ev.OrgName);
            });

    private static IEnumerable<AchievementCard> FromBadges(ProfileFactSet facts, int take) =>
        facts.Badges
            .OrderByDescending(b => b.EarnedAt)
            .Take(take)
            .Select(b => new AchievementCard(
                b.Name, b.Description, b.IconKey, b.EarnedAt, "badge", null, null, null));

    /// <summary>Placement wording. Beyond third the rank is stated plainly rather than dressed up —
    /// "5th place" is honest; "Finalist" would be a claim the stored rank does not make.</summary>
    public static string RankLabel(int rank) => rank switch
    {
        1 => "Winner",
        2 => "Runner-up",
        3 => "Third place",
        _ => $"{Ordinal(rank)} place",
    };

    private static string Ordinal(int n) => (n % 100 is >= 11 and <= 13) ? $"{n}th" : (n % 10) switch
    {
        1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th",
    };

    public static string CertLabel(CertificateKind kind) => kind switch
    {
        CertificateKind.Winner => "Winner",
        CertificateKind.RunnerUp => "Runner-up",
        CertificateKind.Finalist => "Finalist",
        CertificateKind.Appreciation => "Appreciation",
        CertificateKind.Participation => "Participation",
        CertificateKind.Completion => "Completion",
        _ => kind.ToString(),
    };
}
