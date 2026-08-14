using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Users;

/// <summary>Organization cards (D-206; moved onto the fact-set in D-229).
///
/// <para>Per-org event, certificate and achievement counts used to be their own queries, which meant
/// the per-org event count could disagree with the profile-wide one. Both now project the same facts,
/// so an org card's "5 events" is necessarily a subset of the profile's total by construction.</para>
///
/// <para>Only <c>ShowOnProfile</c> memberships reach the fact-set at all, so a hidden membership
/// contributes to no card, no count and no per-org rollup (D-229/H1).</para></summary>
public static class OrganizationsEngine
{
    public static IReadOnlyList<OrgProfileView> Build(ProfileFactSet facts, DateTime now)
    {
        var organizedIds = ProfileCounts.OrganizedEventIds(facts, now);

        return facts.Memberships
            .GroupBy(m => m.OrgId)
            .Select(group =>
            {
                var m = group.OrderByDescending(x => x.IsVerified).ThenBy(x => x.JoinedAt).First();

                // Subsets of the profile-wide sets, filtered to this org — never recounted.
                var orgEvents = organizedIds.Count(id => facts.Events[id].OrgId == m.OrgId);
                var orgCerts = facts.Certificates.Count(c => facts.Events[c.EventId].OrgId == m.OrgId);
                var orgAchievements = facts.Certificates.Count(c =>
                    ProfileCounts.AchievementKinds.Contains(c.Kind)
                    && facts.Events[c.EventId].OrgId == m.OrgId);

                return new OrgProfileView(
                    m.OrgId, m.OrgName, m.OrgSlug, m.LogoKey,
                    [RoleLabel(m.ClaimedRole, m.OrgRole)],
                    m.IsVerified, group.Min(x => x.JoinedAt), m.ValidUntil,
                    orgEvents, orgCerts, orgAchievements);
            })
            .OrderByDescending(o => o.IsVerified).ThenBy(o => o.JoinedAt)
            .ToList();
    }

    /// <summary>The affiliation label (D-206): the claim's real-world role when the membership was
    /// verified through one, else the platform RBAC role. `Representative` reads as "Organizer"
    /// because that is what it means to a reader outside the org's RBAC model.</summary>
    public static string RoleLabel(MembershipClaimRole? claimed, OrgRole orgRole) => claimed switch
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
