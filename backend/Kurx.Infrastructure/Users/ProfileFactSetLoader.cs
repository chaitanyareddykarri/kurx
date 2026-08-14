using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Users;

/// <summary>Materialises a person's verified facts once per request (D-224).
///
/// <para>Scoped and memoised per user id: a request that projects several sections pays for one load.
/// <c>ConnectionEngine</c> will ask for two different people in one request, which is why the memo is
/// keyed by user rather than being a single field.</para>
///
/// <para><b>The private-event invariant is enforced here, once.</b> Events are loaded first, filtered
/// to non-private, and every subsequent fact is joined against that set — so a fact whose event is
/// private cannot enter the fact-set at all, and no downstream engine has to remember the rule. That
/// is a real reduction in how many places can get it wrong: it used to be repeated in every query.</para>
///
/// <para><b>Query count is a fixed 15</b> regardless of how many sections a request projects. That is
/// the number that matters: it does not grow per section, and it replaced a root profile that ran the
/// cached summary, a separate organizations query, three achievement-source queries <i>and</i> the
/// fact-set. (D-224 originally documented 8; D-229 added the badge and three trust-fact loads and the
/// figure was not updated — corrected here rather than left as a stale claim.)</para>
///
/// <para>The three trust queries are each a cheap `EXISTS`/`COUNT` on an indexed user id. If this
/// load ever needs shrinking, they are the obvious candidates to fold into one — measure first.</para></summary>
public class ProfileFactSetLoader(KurxDbContext db) : IProfileFactSetLoader
{
    private readonly Dictionary<Guid, ProfileFactSet> _memo = new();

    /// <summary>Deliberately <b>per-request only</b> — there is no cross-request cache (D-229).
    ///
    /// <para>D-229 briefly serialised the whole fact-set into the distributed cache to replace the
    /// summary cache it deleted. That was withdrawn before merge: the record contains an
    /// <c>IReadOnlyDictionary</c> and several nested record lists, and round-tripping it through
    /// System.Text.Json broke the timeline in a way the type system did not catch. A fragile cache on
    /// the profile's hot path is worse than none.</para>
    ///
    /// <para>The performance case is weaker than it looks anyway: making the fact-set canonical
    /// removed the parallel summary/orgs/achievement queries the root profile used to run <i>as well
    /// as</i> the fact-set, so one load per request is already less work than the audited state. If
    /// profile reads later prove hot, cache a small purpose-shaped projection rather than this
    /// record — that is a decision with its own measurement, not a default.</para></summary>
    public async Task<ProfileFactSet> LoadAsync(Guid userId, CancellationToken ct = default)
    {
        if (_memo.TryGetValue(userId, out var memoised)) return memoised;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.CreatedAt, u.EmailVerifiedAt })
            .FirstOrDefaultAsync(ct);

        // Every table this person's public events can come from. Loaded as ids first so the event
        // detail query is one IN(...) rather than four separate joins repeated per fact family.
        var participationEventIds = await db.EventParticipants.AsNoTracking()
            .Where(p => p.SubjectType == ParticipantSubjectType.Person && p.SubjectId == userId
                && p.Visibility == ParticipantVisibility.Public
                && (p.State == ParticipantState.Accepted || p.State == ParticipantState.Active
                    || p.State == ParticipantState.Completed))
            .Select(p => new { p.EventId, p.RoleSlug, p.State })
            .ToListAsync(ct);

        var assignments = await db.EventAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.ShowOnProfile
                && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Completed))
            .Select(a => new { a.EventId, a.Role, a.CustomRole, a.Status, a.CompletedAt })
            .ToListAsync(ct);

        var attendance = await db.Tickets.AsNoTracking()
            .Where(t => t.UserId == userId && t.State == TicketState.CheckedIn)
            .Select(t => new { t.EventId, t.CheckedInAt })
            .ToListAsync(ct);

        var sessions = await db.Speakers.AsNoTracking()
            .Where(s => s.UserId == userId && s.DeletedAt == null)
            .Join(db.EventSessionSpeakers.AsNoTracking(), s => s.Id, ss => ss.SpeakerId, (s, ss) => ss)
            .Join(db.EventSessions.AsNoTracking().Where(x => x.Kind == ScheduleItemKind.Session),
                ss => ss.SessionId, sess => sess.Id, (ss, sess) => sess)
            .Select(sess => new { sess.EventId, sess.Title, sess.StartsAt, sess.EndsAt })
            .ToListAsync(ct);

        // Published only. A provisional result is not yet a fact about the person and a disputed one
        // is contested — neither belongs in a set called "facts".
        var results = await db.StageResults.AsNoTracking()
            .Where(r => r.SubjectType == CompetitionSubjectType.Person && r.SubjectId == userId
                && r.State == ResultState.Published)
            .Join(db.Stages.AsNoTracking(), r => r.StageId, s => s.Id,
                (r, s) => new { s.EventId, StageName = s.Name, r.Rank, r.FinalScore, r.PublishedAt, r.CreatedAt })
            .ToListAsync(ct);

        // Events the person organizes come from their memberships, so memberships load before events.
        var memberships = await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.ShowOnProfile)
            .Join(db.Organizations.AsNoTracking(), m => m.OrgId, o => o.Id, (m, o) => new
            {
                m.OrgId, o.Name, o.Slug, o.LogoKey, m.Role, m.SourceClaimId, m.IsVerified,
                OrgIsVerified = o.VerificationStatus == OrgVerificationStatus.Verified,
                m.CreatedAt, m.VerifiedAt, m.ValidUntil,
            })
            .ToListAsync(ct);

        var claimRoles = await ClaimRolesAsync(memberships.Select(m => m.SourceClaimId), ct);

        var certificates = await db.Certificates.AsNoTracking()
            .Where(c => c.UserId == userId && c.IsPublic && !c.IsRevoked)
            .Select(c => new { c.Id, c.EventId, c.Kind, c.CreatedAt, c.VerifyCode })
            .ToListAsync(ct);

        var badges = await db.UserBadges.AsNoTracking()
            .Where(b => b.UserId == userId)
            .Join(db.Badges.AsNoTracking(), ub => ub.BadgeId, b => b.Id,
                (ub, b) => new { b.Name, b.Description, b.IconKey, ub.EarnedAt })
            .ToListAsync(ct);

        var trust = new ProfileTrustFacts(
            IdentityVerified: await db.UserIdentities.AsNoTracking()
                .AnyAsync(i => i.UserId == userId && i.Status == IdentityStatus.Approved, ct),
            EmailVerified: user?.EmailVerifiedAt is not null,
            SpeakerLinked: await db.Speakers.AsNoTracking()
                .AnyAsync(s => s.UserId == userId && s.DeletedAt == null, ct),
            AllyCount: await db.AllyConnections.AsNoTracking()
                .CountAsync(a => (a.UserLowId == userId || a.UserHighId == userId)
                    && a.Status == AllyStatus.Accepted, ct));

        var teams = await db.TeamMemberships.AsNoTracking()
            .Where(m => m.PersonId == userId && m.State == TeamMembershipState.Active)
            .Join(db.Teams.AsNoTracking(), m => m.TeamId, t => t.Id,
                (m, t) => new { m.TeamId, t.Name, m.Role, m.JoinedAt })
            .ToListAsync(ct);

        var orgIds = memberships.Select(m => m.OrgId).Distinct().ToList();
        var referencedEventIds = participationEventIds.Select(p => p.EventId)
            .Concat(assignments.Select(a => a.EventId))
            .Concat(attendance.Select(a => a.EventId))
            .Concat(sessions.Select(s => s.EventId))
            .Concat(results.Select(r => r.EventId))
            .Concat(certificates.Select(c => c.EventId))
            .Distinct()
            .ToList();

        // One event load: everything referenced above, plus everything this person organized. The
        // private filter lives here and nowhere else.
        //
        // `IsOrganizedByUser` is decided here, once (D-229). `orgIds` comes from ShowOnProfile
        // memberships only, so a membership the user hides contributes to nothing downstream — the
        // fact simply never claims they organized it (H1).
        var events = await db.Events.AsNoTracking()
            .Where(e => e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed
                && (referencedEventIds.Contains(e.Id)
                    || ((orgIds.Contains(e.RepresentingOrgId) || e.CreatedBy == userId)
                        && (e.Status == EventStatus.Published || e.Status == EventStatus.Closed))))
            .Join(db.Organizations.AsNoTracking(), e => e.RepresentingOrgId, o => o.Id, (e, o) => new ProfileEventFact(
                e.Id, e.Title, e.Slug, e.BannerKey, e.StartsAt, e.EndsAt, e.City, e.KindSlug,
                e.RepresentingOrgId, o.Name, e.Status,
                (orgIds.Contains(e.RepresentingOrgId) || e.CreatedBy == userId)
                    && (e.Status == EventStatus.Published || e.Status == EventStatus.Closed)))
            .ToDictionaryAsync(e => e.EventId, ct);

        var factSet = new ProfileFactSet(
            userId,
            user?.CreatedAt ?? DateTime.UtcNow,
            events,
            // Each list is filtered to events that survived the private/visibility filter above, so a
            // consumer can index Events[fact.EventId] without a null check.
            participationEventIds.Where(p => events.ContainsKey(p.EventId))
                .Select(p => new ProfileParticipationFact(p.EventId, p.RoleSlug, p.State)).ToList(),
            assignments.Where(a => events.ContainsKey(a.EventId))
                .Select(a => new ProfileAssignmentFact(a.EventId, a.Role, a.CustomRole, a.Status, a.CompletedAt)).ToList(),
            attendance.Where(a => events.ContainsKey(a.EventId))
                .Select(a => new ProfileAttendanceFact(a.EventId, a.CheckedInAt)).ToList(),
            sessions.Where(s => events.ContainsKey(s.EventId))
                .Select(s => new ProfileSessionFact(s.EventId, s.Title, s.StartsAt, s.EndsAt)).ToList(),
            results.Where(r => events.ContainsKey(r.EventId))
                .Select(r => new ProfileResultFact(
                    r.EventId, r.StageName, r.Rank, r.FinalScore, r.PublishedAt ?? r.CreatedAt)).ToList(),
            memberships.Select(m => new ProfileMembershipFact(
                m.OrgId, m.Name, m.Slug, m.LogoKey, m.Role,
                m.SourceClaimId is Guid claimId && claimRoles.TryGetValue(claimId, out var role) ? role : null,
                m.IsVerified, m.OrgIsVerified, m.CreatedAt, m.VerifiedAt, m.ValidUntil)).ToList(),
            teams.Select(t => new ProfileTeamFact(t.TeamId, t.Name, t.Role, t.JoinedAt)).ToList(),
            certificates.Where(c => events.ContainsKey(c.EventId))
                .Select(c => new ProfileCertificateFact(c.Id, c.EventId, c.Kind, c.CreatedAt, c.VerifyCode)).ToList(),
            badges.Select(b => new ProfileBadgeFact(b.Name, b.Description, b.IconKey, b.EarnedAt)).ToList(),
            trust);

        _memo[userId] = factSet;
        return factSet;
    }

    /// <summary>The real affiliation behind a verified membership (D-206). Skipped entirely when no
    /// membership carries a claim, so the common case costs nothing.</summary>
    private async Task<Dictionary<Guid, MembershipClaimRole>> ClaimRolesAsync(
        IEnumerable<Guid?> claimIds, CancellationToken ct)
    {
        var ids = claimIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];

        return await db.MembershipClaims.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.ClaimedRole, ct);
    }
}
