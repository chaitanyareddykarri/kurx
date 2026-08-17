using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>The Team subsystem (V3 §6, Phase 10). Additive — the purchase <see cref="Group"/> stays as a legacy
/// mirror and the Phase-9 money path is untouched. Formation only: create/roster/invite/join-request/substitute/
/// lifecycle/merge/split + <see cref="TeamPolicy"/>. Organiser actions reuse the Phase-6 event-permission union.</summary>
public class TeamService(KurxDbContext db, IEventPermissionService permissions) : ITeamService
{
    private static readonly TeamMembershipState[] LiveStates = [TeamMembershipState.Invited, TeamMembershipState.Requested, TeamMembershipState.Active];
    private static readonly TeamState[] PreCompetitionStates = [TeamState.Forming, TeamState.Complete, TeamState.Locked];

    // ── TeamPolicy (§6.3) ────────────────────────────────────────────────────
    public async Task SyncPolicyAsync(Guid ticketTypeId, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FindAsync([ticketTypeId], ct);
        if (tt is null || !tt.IsCompetition) return;   // TeamPolicy exists only for competition ticket types

        var policy = await db.TeamPolicies.FirstOrDefaultAsync(p => p.TicketTypeId == ticketTypeId, ct);
        if (policy is null)
        {
            policy = new TeamPolicy
            {
                EventId = tt.EventId, TicketTypeId = tt.Id,
                MinSize = tt.GroupMin is > 0 ? tt.GroupMin!.Value : 1,
                MaxSize = tt.GroupMax is > 0 ? tt.GroupMax!.Value : (tt.GroupMin is > 0 ? tt.GroupMin!.Value : 4),
                FormationMode = TeamFormationMode.InviteOnly,   // competition team joining is invite-based today
                JoinApproval = TeamJoinApproval.Captain,
            };
            db.TeamPolicies.Add(policy);
        }
        else if (tt.GroupMin is > 0 && tt.GroupMax is > 0)   // keep sizes in step with the ticket type's group bounds
        {
            policy.MinSize = tt.GroupMin!.Value;
            policy.MaxSize = tt.GroupMax!.Value;
        }
        policy.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<ServiceResult<TeamPolicyView>> GetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await IsOrganiserAsync(actorId, eventId, isAdmin, ct)) return ServiceResult<TeamPolicyView>.Fail("forbidden");
        var p = await db.TeamPolicies.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == eventId && x.TicketTypeId == ticketTypeId, ct);
        return p is null ? ServiceResult<TeamPolicyView>.Fail("not_found") : ServiceResult<TeamPolicyView>.Success(ToPolicyView(p));
    }

    public async Task<ServiceResult<TeamPolicyView>> SetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, TeamPolicyInput input, CancellationToken ct = default)
    {
        if (!await IsOrganiserAsync(actorId, eventId, isAdmin, ct)) return ServiceResult<TeamPolicyView>.Fail("forbidden");
        var p = await db.TeamPolicies.FirstOrDefaultAsync(x => x.EventId == eventId && x.TicketTypeId == ticketTypeId, ct);
        if (p is null) return ServiceResult<TeamPolicyView>.Fail("not_found");

        if (!TryEnum(input.FormationMode, out TeamFormationMode formation, p.FormationMode)) return ServiceResult<TeamPolicyView>.Fail("invalid_formation_mode");
        if (!TryEnum(input.JoinApproval, out TeamJoinApproval approval, p.JoinApproval)) return ServiceResult<TeamPolicyView>.Fail("invalid_join_approval");
        if (!TryEnum(input.IncompleteTeamPolicy, out IncompleteTeamPolicy incomplete, p.IncompleteTeamPolicy)) return ServiceResult<TeamPolicyView>.Fail("invalid_incomplete_policy");
        var min = input.MinSize ?? p.MinSize;
        var max = input.MaxSize ?? p.MaxSize;
        if (min < 1 || max < min) return ServiceResult<TeamPolicyView>.Fail("invalid_size");

        /*
         * D-357 — the ticket type owns team size, because that is the bound people were CHARGED against.
         *
         * `SyncPolicyAsync` already seeds this policy from `TicketType.GroupMin/GroupMax` and re-syncs it
         * whenever they change, so the two start and stay in step — but nothing stopped this method from
         * pulling them apart again. An organiser selling a 2–4 team entry could widen the policy to 2–10,
         * and the sixth member would join a team whose registration bought room for four: `CreateOrderAsync`
         * validates the purchase against `GroupMin/GroupMax`, and the roster cap is the size that order
         * recorded. The policy may narrow within the sold bounds; it may not exceed them.
         */
        var bounds = await db.TicketTypes.AsNoTracking()
            .Where(t => t.Id == ticketTypeId).Select(t => new { t.GroupMin, t.GroupMax }).FirstOrDefaultAsync(ct);
        if (bounds is not null)
        {
            if (bounds.GroupMin is > 0 && min < bounds.GroupMin.Value)
                return ServiceResult<TeamPolicyView>.Fail("size_below_ticket_type");
            if (bounds.GroupMax is > 0 && max > bounds.GroupMax.Value)
                return ServiceResult<TeamPolicyView>.Fail("size_above_ticket_type");
        }
        if (input.SubstitutesAllowed is < 0) return ServiceResult<TeamPolicyView>.Fail("invalid_substitutes");

        p.MinSize = min; p.MaxSize = max; p.FormationMode = formation; p.JoinApproval = approval;
        p.IncompleteTeamPolicy = incomplete;
        if (input.LockAt is not null) p.LockAt = input.LockAt;
        if (input.NameEditUntil is not null) p.NameEditUntil = input.NameEditUntil;
        if (input.RosterEditUntil is not null) p.RosterEditUntil = input.RosterEditUntil;
        if (input.MentorEditUntil is not null) p.MentorEditUntil = input.MentorEditUntil;
        if (input.MaxTeamsPerPersonInEvent is { } mt) p.MaxTeamsPerPersonInEvent = mt;
        if (input.UnlimitedTeamsPerTree is { } ut) p.UnlimitedTeamsPerTree = ut;
        if (input.AllowSoloAsTeam is { } solo) p.AllowSoloAsTeam = solo;
        if (input.AllowCrossOrgMembers is { } cross) p.AllowCrossOrgMembers = cross;
        if (input.SubstitutesAllowed is { } sa) p.SubstitutesAllowed = sa;
        if (input.SubstitutionDeadline is not null) p.SubstitutionDeadline = input.SubstitutionDeadline;
        p.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ServiceResult<TeamPolicyView>.Success(ToPolicyView(p));
    }

    // ── Formation ────────────────────────────────────────────────────────────
    /*
     * D-359 — the §6.5 hook Phase 10 left open.
     *
     * `Team.RegistrationId` was added with the note "Null this phase — the team-slot purchase flow is
     * deferred; the field exists so the later competitive-purchase phase can link without a migration."
     * This is that phase: a competition group registration now produces the authoritative Team beside the
     * legacy purchase `Group`, so the two stop disagreeing about who is on a team.
     *
     * Deliberately NOT routed through `CreateTeamAsync`: that entry point enforces formation mode, the
     * per-person team limit and a required name — rules a completed purchase has already settled, and any
     * one of which would refuse a team the buyer has been charged for. The purchase is the authorisation.
     * Same entities, same policy, same slug rule; no second team model.
     *
     * Idempotent in all three directions (no policy, team already made, member already on the roster), so a
     * webhook replay or a re-run projection cannot fork a team or double a roster.
     */
    public async Task MaterialiseForGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return;

        // Teams exist only where competition does (V3 §6) — the same gate `SyncPolicyAsync` applies, so a
        // plain group purchase keeps its Group and nothing else, exactly as before.
        var tt = await db.TicketTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == group.TicketTypeId, ct);
        if (tt is null || !tt.IsCompetition) return;

        // `(EventId, Slug)` is the team's unique key, and the slug is derived from the group — so this both
        // finds a team made by an earlier run and, via `Local`, one added in this very transaction.
        var slug = GroupSlug(group);
        var team = await db.Teams.FirstOrDefaultAsync(t => t.EventId == group.EventId && t.Slug == slug, ct)
            ?? db.Teams.Local.FirstOrDefault(t => t.EventId == group.EventId && t.Slug == slug);

        if (team is null)
        {
            var name = string.IsNullOrWhiteSpace(group.DisplayName) ? $"Team {group.GroupNumber}" : group.DisplayName!.Trim();
            team = new Team
            {
                EventId = group.EventId,
                TicketTypeId = group.TicketTypeId,
                Name = name,
                Slug = slug,
                State = TeamState.Forming,
            };
            db.Teams.Add(team);
            Audit(group.LeaderUserId, "user", "team.created", team.Id,
                $"{{\"event_id\":\"{group.EventId}\",\"name\":\"{team.Name}\",\"from_group\":\"{group.Id}\"}}");
        }

        // Link the Team to the registration the money path produced, which is the whole point of the field.
        // Read rather than passed in so this stays callable from any projection order.
        team.RegistrationId ??= await db.Registrations.AsNoTracking()
            .Where(r => r.OrderId == group.OrderId).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);

        // The roster, mirrored from the group. The leader is the captain; everyone else is a member.
        var groupMembers = await db.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == group.Id && m.UserId != null)
            .Select(m => m.UserId!.Value).ToListAsync(ct);
        var existing = (await db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == team.Id).Select(m => m.PersonId).ToListAsync(ct))
            .Concat(db.TeamMemberships.Local.Where(m => m.TeamId == team.Id).Select(m => m.PersonId))
            .ToHashSet();

        foreach (var personId in groupMembers.Where(p => !existing.Contains(p)))
        {
            db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = team.Id,
                PersonId = personId,
                Role = personId == group.LeaderUserId ? TeamRole.Captain : TeamRole.Member,
                State = TeamMembershipState.Active,
            });
        }
    }

    /// <summary>A team's slug, derived from the group it mirrors. Deterministic on purpose: it is what makes
    /// <see cref="MaterialiseForGroupAsync"/> idempotent without a second lookup key, and `groups (EventId,
    /// GroupNumber)` is already unique, so it cannot collide within an event.</summary>
    private static string GroupSlug(Group group) => $"team-{group.GroupNumber}";

    public async Task<ServiceResult<TeamView>> CreateTeamAsync(Guid userId, Guid eventId, Guid ticketTypeId, TeamInput input, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketTypeId && t.EventId == eventId, ct);
        if (tt is null || !tt.IsCompetition) return ServiceResult<TeamView>.Fail("not_a_competition");
        var policy = await db.TeamPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.TicketTypeId == ticketTypeId, ct);
        if (policy is null) return ServiceResult<TeamView>.Fail("not_found");
        if (string.IsNullOrWhiteSpace(input.Name)) return ServiceResult<TeamView>.Fail("name_required");

        // Organiser-driven formation modes are not open to users.
        var isOrganiser = await permissions.HasAsync(userId, eventId, "event:manage", ct);
        if (policy.FormationMode is TeamFormationMode.OrganiserAssigned or TeamFormationMode.RandomAllocation && !isOrganiser)
            return ServiceResult<TeamView>.Fail("organiser_forms_teams");

        if (await CountUserTeamsInEventAsync(userId, eventId, ct) >= policy.MaxTeamsPerPersonInEvent)
            return ServiceResult<TeamView>.Fail("team_limit_reached");

        var team = new Team
        {
            EventId = eventId, TicketTypeId = ticketTypeId,
            Name = input.Name.Trim(),
            Slug = await UniqueSlugAsync(eventId, input.Slug ?? input.Name, ct),
            LogoUrl = input.LogoUrl, Tagline = input.Tagline, DeclaredOrgUnitId = input.DeclaredOrgUnitId,
            State = TeamState.Forming,
        };
        db.Teams.Add(team);
        db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, PersonId = userId, Role = TeamRole.Captain, State = TeamMembershipState.Active });
        Audit(userId, "user", "team.created", team.Id, $"{{\"event_id\":\"{eventId}\",\"name\":\"{team.Name}\"}}");
        await db.SaveChangesAsync(ct);
        return await TeamViewResultAsync(team.Id, ct);
    }

    public async Task<ServiceResult<TeamView>> UpdateTeamAsync(Guid userId, Guid teamId, bool isAdmin, TeamInput input, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return ServiceResult<TeamView>.Fail("not_found");
        if (!await CanManageTeamAsync(userId, team, isAdmin, ct)) return ServiceResult<TeamView>.Fail("forbidden");
        if (team.State != TeamState.Forming && team.State != TeamState.Complete) return ServiceResult<TeamView>.Fail("team_locked");

        var policy = await db.TeamPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.TicketTypeId == team.TicketTypeId, ct);
        if (policy?.NameEditUntil is { } until && DateTime.UtcNow > until && !isAdmin) return ServiceResult<TeamView>.Fail("name_edit_closed");

        if (!string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim() != team.Name)
        {
            team.Name = input.Name.Trim();
            team.Slug = await UniqueSlugAsync(team.EventId, input.Slug ?? input.Name, ct, exceptTeamId: team.Id);
        }
        if (input.LogoUrl is not null) team.LogoUrl = input.LogoUrl;
        if (input.Tagline is not null) team.Tagline = input.Tagline;
        if (input.DeclaredOrgUnitId is not null) team.DeclaredOrgUnitId = input.DeclaredOrgUnitId;
        team.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await TeamViewResultAsync(team.Id, ct);
    }

    public async Task<ServiceResult<TeamInviteView>> InviteAsync(Guid userId, Guid teamId, TeamInviteInput input, CancellationToken ct = default)
    {
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return ServiceResult<TeamInviteView>.Fail("not_found");
        if (!await CanManageTeamAsync(userId, team, isAdmin: false, ct) && !await permissions.HasAsync(userId, team.EventId, "event:manage", ct))
            return ServiceResult<TeamInviteView>.Fail("forbidden");
        if (!PreCompetitionStates.Contains(team.State) || team.State == TeamState.Locked) return ServiceResult<TeamInviteView>.Fail("team_locked");
        if (input.PersonId is null && string.IsNullOrWhiteSpace(input.Email) && string.IsNullOrWhiteSpace(input.Phone))
            return ServiceResult<TeamInviteView>.Fail("invitee_required");
        if (!TryEnum(input.Role, out TeamRole role, TeamRole.Member)) return ServiceResult<TeamInviteView>.Fail("invalid_role");
        if (await ActiveCountAsync(teamId, ct) >= await MaxSizeAsync(team.TicketTypeId, ct)) return ServiceResult<TeamInviteView>.Fail("team_full");

        var invite = new TeamInvite
        {
            TeamId = teamId, InviteePersonId = input.PersonId,
            InviteeEmail = input.Email?.Trim(), InviteePhone = input.Phone?.Trim(),
            Role = role, Token = GenerateToken(), ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        db.TeamInvites.Add(invite);
        Audit(userId, "user", "team.invited", teamId, null);
        await db.SaveChangesAsync(ct);
        return ServiceResult<TeamInviteView>.Success(ToInviteView(invite));
    }

    public async Task<ServiceResult<TeamView>> AcceptInviteAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var invite = await db.TeamInvites.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null || invite.State != TeamInviteState.Pending) return ServiceResult<TeamView>.Fail("invalid_invite");
        if (DateTime.UtcNow > invite.ExpiresAt) { invite.State = TeamInviteState.Expired; await db.SaveChangesAsync(ct); return ServiceResult<TeamView>.Fail("invite_expired"); }
        // A person-scoped invite may only be accepted by that person.
        if (invite.InviteePersonId is { } pid && pid != userId) return ServiceResult<TeamView>.Fail("forbidden");

        var team = await db.Teams.AsNoTracking().FirstAsync(t => t.Id == invite.TeamId, ct);
        var add = await TryAddActiveMemberAsync(team, userId, invite.Role, ct);
        if (add is not null) return ServiceResult<TeamView>.Fail(add);
        invite.State = TeamInviteState.Accepted;
        Audit(userId, "user", "team.invite_accepted", team.Id, null);
        await db.SaveChangesAsync(ct);
        await RecomputeStateAsync(team.Id, ct);
        return await TeamViewResultAsync(team.Id, ct);
    }

    public async Task<ServiceResult<bool>> RevokeInviteAsync(Guid userId, Guid inviteId, bool isAdmin, CancellationToken ct = default)
    {
        var invite = await db.TeamInvites.FirstOrDefaultAsync(i => i.Id == inviteId, ct);
        if (invite is null) return ServiceResult<bool>.Fail("not_found");
        var team = await db.Teams.AsNoTracking().FirstAsync(t => t.Id == invite.TeamId, ct);
        if (!await CanManageTeamAsync(userId, team, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (invite.State == TeamInviteState.Pending) { invite.State = TeamInviteState.Revoked; await db.SaveChangesAsync(ct); }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<TeamJoinRequestView>> RequestJoinAsync(Guid userId, Guid teamId, string? message, CancellationToken ct = default)
    {
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return ServiceResult<TeamJoinRequestView>.Fail("not_found");
        var policy = await db.TeamPolicies.AsNoTracking().FirstAsync(p => p.TicketTypeId == team.TicketTypeId, ct);
        if (policy.FormationMode == TeamFormationMode.InviteOnly) return ServiceResult<TeamJoinRequestView>.Fail("invite_only");
        if (!PreCompetitionStates.Contains(team.State) || team.State == TeamState.Locked) return ServiceResult<TeamJoinRequestView>.Fail("team_locked");
        if (await IsLiveMemberAsync(teamId, userId, ct)) return ServiceResult<TeamJoinRequestView>.Fail("already_a_member");
        if (await db.TeamJoinRequests.AnyAsync(r => r.TeamId == teamId && r.PersonId == userId && r.State == TeamJoinRequestState.Pending, ct))
            return ServiceResult<TeamJoinRequestView>.Fail("already_requested");
        if (await CountUserTeamsInEventAsync(userId, team.EventId, ct) >= policy.MaxTeamsPerPersonInEvent)
            return ServiceResult<TeamJoinRequestView>.Fail("team_limit_reached");

        var req = new TeamJoinRequest { TeamId = teamId, PersonId = userId, Message = message };
        // NONE approval → auto-join immediately if there is room.
        if (policy.JoinApproval == TeamJoinApproval.None)
        {
            var add = await TryAddActiveMemberAsync(team, userId, TeamRole.Member, ct);
            if (add is not null) return ServiceResult<TeamJoinRequestView>.Fail(add);
            req.State = TeamJoinRequestState.Approved; req.DecidedAt = DateTime.UtcNow;
        }
        db.TeamJoinRequests.Add(req);
        await db.SaveChangesAsync(ct);
        if (policy.JoinApproval == TeamJoinApproval.None) await RecomputeStateAsync(teamId, ct);
        return ServiceResult<TeamJoinRequestView>.Success(ToJoinRequestView(req));
    }

    public async Task<ServiceResult<bool>> DecideJoinRequestAsync(Guid userId, Guid requestId, bool approve, bool isAdmin, CancellationToken ct = default)
    {
        var req = await db.TeamJoinRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null || req.State != TeamJoinRequestState.Pending) return ServiceResult<bool>.Fail("not_found");
        var team = await db.Teams.AsNoTracking().FirstAsync(t => t.Id == req.TeamId, ct);
        var policy = await db.TeamPolicies.AsNoTracking().FirstAsync(p => p.TicketTypeId == team.TicketTypeId, ct);
        // CAPTAIN approval → a captain (or organiser) decides; ORGANISER approval → organiser only.
        var isOrganiser = isAdmin || await permissions.HasAsync(userId, team.EventId, "event:manage", ct);
        var isCaptain = await CanManageTeamAsync(userId, team, isAdmin: false, ct);
        if (policy.JoinApproval == TeamJoinApproval.Organiser ? !isOrganiser : !(isCaptain || isOrganiser))
            return ServiceResult<bool>.Fail("forbidden");

        if (approve)
        {
            var add = await TryAddActiveMemberAsync(team, req.PersonId, TeamRole.Member, ct);
            if (add is not null) return ServiceResult<bool>.Fail(add);
            req.State = TeamJoinRequestState.Approved;
        }
        else req.State = TeamJoinRequestState.Rejected;
        req.DecidedBy = userId; req.DecidedAt = DateTime.UtcNow;
        Audit(userId, "user", approve ? "team.join_approved" : "team.join_rejected", team.Id, null);
        await db.SaveChangesAsync(ct);
        if (approve) await RecomputeStateAsync(team.Id, ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> LeaveAsync(Guid userId, Guid teamId, CancellationToken ct = default)
    {
        var m = await db.TeamMemberships.FirstOrDefaultAsync(x => x.TeamId == teamId && x.PersonId == userId && LiveStates.Contains(x.State), ct);
        if (m is null) return ServiceResult<bool>.Fail("not_a_member");
        // The last active captain must transfer the captaincy (promote another) before leaving.
        if (m.Role == TeamRole.Captain && await db.TeamMemberships.CountAsync(x => x.TeamId == teamId && x.Role == TeamRole.Captain && x.State == TeamMembershipState.Active, ct) <= 1
            && await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.State == TeamMembershipState.Active && x.PersonId != userId, ct))
            return ServiceResult<bool>.Fail("captain_must_transfer_first");
        m.State = TeamMembershipState.Left; m.LeftAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecomputeStateAsync(teamId, ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> RemoveMemberAsync(Guid userId, Guid teamId, Guid membershipId, bool isAdmin, CancellationToken ct = default)
    {
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return ServiceResult<bool>.Fail("not_found");
        if (!await CanManageTeamAsync(userId, team, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        var m = await db.TeamMemberships.FirstOrDefaultAsync(x => x.Id == membershipId && x.TeamId == teamId && LiveStates.Contains(x.State), ct);
        if (m is null) return ServiceResult<bool>.Fail("not_found");
        if (m.PersonId == userId) return ServiceResult<bool>.Fail("use_leave");   // a captain removing themselves uses Leave
        m.State = TeamMembershipState.Removed; m.LeftAt = DateTime.UtcNow;
        Audit(userId, "user", "team.member_removed", teamId, null);
        await db.SaveChangesAsync(ct);
        await RecomputeStateAsync(teamId, ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<TeamView>> SubstituteAsync(Guid userId, Guid teamId, Guid outMembershipId, Guid inPersonId, bool isAdmin, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return ServiceResult<TeamView>.Fail("not_found");
        if (!await CanManageTeamAsync(userId, team, isAdmin, ct)) return ServiceResult<TeamView>.Fail("forbidden");
        var policy = await db.TeamPolicies.AsNoTracking().FirstAsync(p => p.TicketTypeId == team.TicketTypeId, ct);
        if (policy.SubstitutionDeadline is { } dl && DateTime.UtcNow > dl && !isAdmin) return ServiceResult<TeamView>.Fail("substitution_closed");

        var outM = await db.TeamMemberships.FirstOrDefaultAsync(m => m.Id == outMembershipId && m.TeamId == teamId && m.State == TeamMembershipState.Active, ct);
        if (outM is null) return ServiceResult<TeamView>.Fail("not_found");
        if (await IsLiveMemberAsync(teamId, inPersonId, ct)) return ServiceResult<TeamView>.Fail("already_a_member");
        var priorSubs = await db.TeamMemberships.CountAsync(m => m.TeamId == teamId && m.State == TeamMembershipState.Replaced, ct);
        if (priorSubs >= policy.SubstitutesAllowed) return ServiceResult<TeamView>.Fail("no_substitutes_left");

        // Substitution is an EDGE, not a delete (§6.2/§6.5): the outgoing membership is Replaced and points at the
        // incoming one. Person-inventory stays balanced — the Admission transfers, no new draw (that's the deferred
        // team-slot purchase phase; here the roster edge is recorded).
        var inM = new TeamMembership { TeamId = teamId, PersonId = inPersonId, Role = outM.Role, State = TeamMembershipState.Active };
        db.TeamMemberships.Add(inM);
        outM.State = TeamMembershipState.Replaced; outM.LeftAt = DateTime.UtcNow; outM.ReplacedByMembershipId = inM.Id;
        Audit(userId, "user", "team.substituted", teamId, $"{{\"out\":\"{outM.PersonId}\",\"in\":\"{inPersonId}\"}}");
        await db.SaveChangesAsync(ct);
        return await TeamViewResultAsync(teamId, ct);
    }

    // ── Lifecycle + composite transactions (§6.2/§6.4) ───────────────────────
    public async Task<ServiceResult<TeamView>> TransitionAsync(Guid actorId, Guid teamId, bool isAdmin, string action, string? reason, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return ServiceResult<TeamView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, team.EventId, isAdmin, ct)) return ServiceResult<TeamView>.Fail("forbidden");

        var target = action.ToLowerInvariant() switch
        {
            "lock" => TeamState.Locked,
            "compete" => TeamState.Competing,
            "disqualify" => TeamState.Disqualified,
            "withdraw" => TeamState.Withdrawn,
            "eliminate" => TeamState.Eliminated,
            "finalist" => TeamState.Finalist,
            _ => (TeamState?)null,
        };
        if (target is null) return ServiceResult<TeamView>.Fail("invalid_action");
        if (target == TeamState.Disqualified && string.IsNullOrWhiteSpace(reason)) return ServiceResult<TeamView>.Fail("reason_required");
        if (!IsValidTransition(team.State, target.Value)) return ServiceResult<TeamView>.Fail("invalid_transition");

        team.State = target.Value; team.UpdatedAt = DateTime.UtcNow;
        Audit(actorId, isAdmin ? "admin" : "user", $"team.{action.ToLowerInvariant()}", teamId, reason is null ? null : $"{{\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason)}}}");
        await db.SaveChangesAsync(ct);
        return await TeamViewResultAsync(teamId, ct);
    }

    public async Task<ServiceResult<TeamView>> MergeAsync(Guid actorId, Guid teamAId, Guid teamBId, bool isAdmin, CancellationToken ct = default)
    {
        if (teamAId == teamBId) return ServiceResult<TeamView>.Fail("same_team");
        var a = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamAId, ct);
        var bTeam = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamBId, ct);
        if (a is null || bTeam is null || a.EventId != bTeam.EventId) return ServiceResult<TeamView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, a.EventId, isAdmin, ct)) return ServiceResult<TeamView>.Fail("forbidden");
        // Prohibited once the competition has progressed (§6.4): both the state gate and, now that scoring exists,
        // the "any Stage scoring has begun" gate — a merge mid-judging orphans a tombstoned team's recorded scores.
        if (!PreCompetitionStates.Contains(a.State) || !PreCompetitionStates.Contains(bTeam.State)) return ServiceResult<TeamView>.Fail("merge_after_lock_forbidden");
        if (await ScoringHasBegunAsync(teamAId, ct) || await ScoringHasBegunAsync(teamBId, ct)) return ServiceResult<TeamView>.Fail("merge_after_scoring_forbidden");
        var maxSize = await MaxSizeAsync(a.TicketTypeId, ct);
        if (await ActiveCountAsync(teamAId, ct) + await ActiveCountAsync(teamBId, ct) > maxSize) return ServiceResult<TeamView>.Fail("exceeds_max_size");

        // Move B's active members into A; the B captain is demoted; B becomes a tombstone referencing A.
        var bMembers = await db.TeamMemberships.Where(m => m.TeamId == teamBId && m.State == TeamMembershipState.Active).ToListAsync(ct);
        var aPersonIds = (await db.TeamMemberships.Where(m => m.TeamId == teamAId && LiveStates.Contains(m.State) && m.PersonId != null).Select(m => m.PersonId!.Value).ToListAsync(ct)).ToHashSet();
        foreach (var m in bMembers)
        {
            m.State = TeamMembershipState.Left; m.LeftAt = DateTime.UtcNow;   // close the B-side row (history)
            if (m.PersonId is { } pid && aPersonIds.Add(pid))
                db.TeamMemberships.Add(new TeamMembership { TeamId = teamAId, PersonId = pid, Role = m.Role == TeamRole.Captain ? TeamRole.Member : m.Role, State = TeamMembershipState.Active });
        }
        bTeam.State = TeamState.Withdrawn; bTeam.MergedIntoTeamId = teamAId; bTeam.UpdatedAt = DateTime.UtcNow;
        Audit(actorId, isAdmin ? "admin" : "user", "team.merged", teamAId, $"{{\"merged_from\":\"{teamBId}\"}}");
        await db.SaveChangesAsync(ct);
        return await TeamViewResultAsync(teamAId, ct);
    }

    public async Task<ServiceResult<TeamSplitView>> SplitAsync(Guid actorId, Guid teamId, IReadOnlyList<Guid> movePersonIds, string newName, bool isAdmin, CancellationToken ct = default)
    {
        var a = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (a is null) return ServiceResult<TeamSplitView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, a.EventId, isAdmin, ct)) return ServiceResult<TeamSplitView>.Fail("forbidden");
        if (!PreCompetitionStates.Contains(a.State)) return ServiceResult<TeamSplitView>.Fail("split_after_lock_forbidden");
        // §6.4: a split after scoring changes "who competed" under the surviving team, invalidating its recorded scores.
        if (await ScoringHasBegunAsync(teamId, ct)) return ServiceResult<TeamSplitView>.Fail("split_after_scoring_forbidden");
        if (string.IsNullOrWhiteSpace(newName)) return ServiceResult<TeamSplitView>.Fail("name_required");
        var move = movePersonIds.Distinct().ToHashSet();
        if (move.Count == 0) return ServiceResult<TeamSplitView>.Fail("no_members_to_move");

        var actives = await db.TeamMemberships.Where(m => m.TeamId == teamId && m.State == TeamMembershipState.Active && m.PersonId != null).ToListAsync(ct);
        if (!move.IsSubsetOf(actives.Select(m => m.PersonId!.Value).ToHashSet())) return ServiceResult<TeamSplitView>.Fail("member_not_on_team");
        var policy = await db.TeamPolicies.AsNoTracking().FirstAsync(p => p.TicketTypeId == a.TicketTypeId, ct);
        var remaining = actives.Count - move.Count;
        if (remaining < policy.MinSize || move.Count < policy.MinSize) return ServiceResult<TeamSplitView>.Fail("both_sides_below_min");

        // A retains the members not moved; a new team B' receives the moved members (Phase-10 formation split — the
        // original keeps its identity; the full tombstone-both variant matters only once results exist, a later phase).
        var b = new Team { EventId = a.EventId, TicketTypeId = a.TicketTypeId, Name = newName.Trim(), Slug = await UniqueSlugAsync(a.EventId, newName, ct), State = TeamState.Forming };
        db.Teams.Add(b);
        var first = true;
        foreach (var m in actives.Where(m => move.Contains(m.PersonId!.Value)))
        {
            m.State = TeamMembershipState.Left; m.LeftAt = DateTime.UtcNow;   // leaves A
            db.TeamMemberships.Add(new TeamMembership { TeamId = b.Id, PersonId = m.PersonId, Role = first ? TeamRole.Captain : TeamRole.Member, State = TeamMembershipState.Active });
            first = false;
        }
        a.UpdatedAt = DateTime.UtcNow;
        Audit(actorId, isAdmin ? "admin" : "user", "team.split", teamId, $"{{\"new_team\":\"{b.Id}\"}}");
        await db.SaveChangesAsync(ct);
        var av = (await TeamViewResultAsync(teamId, ct)).Value!;
        var bv = (await TeamViewResultAsync(b.Id, ct)).Value!;
        return ServiceResult<TeamSplitView>.Success(new TeamSplitView(av, bv));
    }

    // ── Reads ────────────────────────────────────────────────────────────────
    public async Task<ServiceResult<IReadOnlyList<TeamView>>> GetForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var teams = await db.Teams.AsNoTracking().Where(t => t.EventId == eventId).OrderBy(t => t.Name).ToListAsync(ct);
        IReadOnlyList<TeamView> views = await ToViewsAsync(teams, ct);
        return ServiceResult<IReadOnlyList<TeamView>>.Success(views);
    }

    public async Task<ServiceResult<TeamView>> GetTeamAsync(Guid teamId, CancellationToken ct = default) => await TeamViewResultAsync(teamId, ct);

    public async Task<IReadOnlyList<TeamView>> MyTeamsAsync(Guid userId, CancellationToken ct = default)
    {
        var teamIds = await db.TeamMemberships.AsNoTracking().Where(m => m.PersonId == userId && LiveStates.Contains(m.State)).Select(m => m.TeamId).Distinct().ToListAsync(ct);
        var teams = await db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToListAsync(ct);
        return await ToViewsAsync(teams, ct);
    }

    public async Task<int> BackfillFromGroupsAsync(CancellationToken ct = default)
    {
        // A Team for every existing competition Group that has no team yet. Group stays a legacy mirror.
        var groups = await db.Groups.AsNoTracking()
            .Where(g => db.TicketTypes.Any(t => t.Id == g.TicketTypeId && t.IsCompetition) && !db.Teams.Any(t => t.EventId == g.EventId && t.TicketTypeId == g.TicketTypeId && t.Name == (g.DisplayName ?? "Team " + g.GroupNumber)))
            .ToListAsync(ct);
        var created = 0;
        foreach (var g in groups)
        {
            await SyncPolicyAsync(g.TicketTypeId, ct);
            var team = new Team { EventId = g.EventId, TicketTypeId = g.TicketTypeId, Name = g.DisplayName ?? $"Team {g.GroupNumber}", Slug = await UniqueSlugAsync(g.EventId, g.DisplayName ?? $"team-{g.GroupNumber}", ct), State = TeamState.Forming };
            db.Teams.Add(team);
            var members = await db.GroupMembers.AsNoTracking().Where(m => m.GroupId == g.Id && m.UserId != null).ToListAsync(ct);
            var seen = new HashSet<Guid>();
            foreach (var m in members)
                if (seen.Add(m.UserId!.Value))
                    db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, PersonId = m.UserId, Role = m.UserId == g.LeaderUserId ? TeamRole.Captain : TeamRole.Member, State = TeamMembershipState.Active });
            if (!seen.Contains(g.LeaderUserId))
                db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, PersonId = g.LeaderUserId, Role = TeamRole.Captain, State = TeamMembershipState.Active });
            created++;
        }
        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }

    // ── helpers ────────────────────────────────────────────────────────────
    private async Task<string?> TryAddActiveMemberAsync(Team team, Guid personId, TeamRole role, CancellationToken ct)
    {
        if (!PreCompetitionStates.Contains(team.State) || team.State == TeamState.Locked) return "team_locked";
        if (await IsLiveMemberAsync(team.Id, personId, ct)) return "already_a_member";
        if (await ActiveCountAsync(team.Id, ct) >= await MaxSizeAsync(team.TicketTypeId, ct)) return "team_full";
        if (await CountUserTeamsInEventAsync(personId, team.EventId, ct) >= await MaxTeamsPerPersonAsync(team.TicketTypeId, ct)) return "team_limit_reached";
        db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, PersonId = personId, Role = role, State = TeamMembershipState.Active });
        return null;   // the caller saves, then calls RecomputeStateAsync (which must read the persisted roster)
    }

    // Reflect roster size onto the FORMING/COMPLETE state (a team reaching min_size is Complete). MUST run AFTER the
    // roster change is saved — it counts from the database, so a still-tracked add/leave would miscount. Persists
    // its own change.
    private async Task RecomputeStateAsync(Guid teamId, CancellationToken ct)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null || (team.State != TeamState.Forming && team.State != TeamState.Complete)) return;
        var min = (await db.TeamPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.TicketTypeId == team.TicketTypeId, ct))?.MinSize ?? 1;
        var target = await ActiveCountAsync(teamId, ct) >= min ? TeamState.Complete : TeamState.Forming;
        if (team.State != target) { team.State = target; team.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
    }

    private Task<int> ActiveCountAsync(Guid teamId, CancellationToken ct) => db.TeamMemberships.CountAsync(m => m.TeamId == teamId && m.State == TeamMembershipState.Active, ct);
    private Task<bool> IsLiveMemberAsync(Guid teamId, Guid personId, CancellationToken ct) => db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.PersonId == personId && LiveStates.Contains(m.State), ct);
    private async Task<int> CountUserTeamsInEventAsync(Guid userId, Guid eventId, CancellationToken ct)
        => await db.TeamMemberships.CountAsync(m => m.PersonId == userId && LiveStates.Contains(m.State) && db.Teams.Any(t => t.Id == m.TeamId && t.EventId == eventId), ct);
    private async Task<int> MaxSizeAsync(Guid ttId, CancellationToken ct) => (await db.TeamPolicies.AsNoTracking().Where(p => p.TicketTypeId == ttId).Select(p => (int?)p.MaxSize).FirstOrDefaultAsync(ct)) ?? int.MaxValue;
    private async Task<int> MaxTeamsPerPersonAsync(Guid ttId, CancellationToken ct) => (await db.TeamPolicies.AsNoTracking().Where(p => p.TicketTypeId == ttId).Select(p => (int?)p.MaxTeamsPerPersonInEvent).FirstOrDefaultAsync(ct)) ?? 1;

    private async Task<bool> IsOrganiserAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct) => isAdmin || await permissions.HasAsync(actorId, eventId, "event:manage", ct);

    /// <summary>Has any Stage scoring begun for this team as a competition subject (§6.4)? A team subject's scores/votes
    /// can only live in its own event's stages, so a subject-id lookup is exact. Once true, merge/split are prohibited.</summary>
    private async Task<bool> ScoringHasBegunAsync(Guid teamId, CancellationToken ct)
        => await db.JudgeScores.AnyAsync(s => s.SubjectType == CompetitionSubjectType.Team && s.SubjectId == teamId, ct)
           || await db.PublicVotes.AnyAsync(v => v.SubjectType == CompetitionSubjectType.Team && v.SubjectId == teamId, ct);
    private async Task<bool> CanManageTeamAsync(Guid userId, Team team, bool isAdmin, CancellationToken ct)
    {
        if (await IsOrganiserAsync(userId, team.EventId, isAdmin, ct)) return true;
        return await db.TeamMemberships.AnyAsync(m => m.TeamId == team.Id && m.PersonId == userId && m.State == TeamMembershipState.Active && (m.Role == TeamRole.Captain || m.Role == TeamRole.CoCaptain), ct);
    }

    private static bool IsValidTransition(TeamState from, TeamState to) => (from, to) switch
    {
        (TeamState.Forming, TeamState.Locked) or (TeamState.Complete, TeamState.Locked) => true,
        (TeamState.Locked, TeamState.Competing) => true,
        (_, TeamState.Disqualified) or (_, TeamState.Withdrawn) => from is not TeamState.Disqualified and not TeamState.Withdrawn,
        (TeamState.Competing, TeamState.Eliminated) or (TeamState.Competing, TeamState.Finalist) => true,
        _ => false,
    };

    private async Task<string> UniqueSlugAsync(Guid eventId, string basis, CancellationToken ct, Guid? exceptTeamId = null)
    {
        var root = Slugify(basis);
        var slug = root;
        var n = 1;
        while (await db.Teams.AnyAsync(t => t.EventId == eventId && t.Slug == slug && (exceptTeamId == null || t.Id != exceptTeamId), ct))
            slug = $"{root}-{++n}";
        return slug;
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Trim().ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-' or '_' && sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "team-" + Guid.NewGuid().ToString("N")[..6] : slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }

    private static string GenerateToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = RandomNumberGenerator.GetBytes(16);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private void Audit(Guid actorId, string actorType, string action, Guid entityId, string? detailsJson)
        => db.AuditLogs.Add(new AuditLog { ActorType = actorType, ActorId = actorId, Action = action, Entity = "teams", EntityId = entityId, DetailsJson = detailsJson });

    private async Task<ServiceResult<TeamView>> TeamViewResultAsync(Guid teamId, CancellationToken ct)
    {
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        return team is null ? ServiceResult<TeamView>.Fail("not_found") : ServiceResult<TeamView>.Success((await ToViewsAsync([team], ct))[0]);
    }

    private async Task<List<TeamView>> ToViewsAsync(List<Team> teams, CancellationToken ct)
    {
        var ids = teams.Select(t => t.Id).ToList();
        var memberships = await db.TeamMemberships.AsNoTracking().Where(m => ids.Contains(m.TeamId)).ToListAsync(ct);
        var personIds = memberships.Where(m => m.PersonId != null).Select(m => m.PersonId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => personIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return teams.Select(t =>
        {
            var ms = memberships.Where(m => m.TeamId == t.Id).ToList();
            return new TeamView(t.Id, t.EventId, t.TicketTypeId, t.Name, t.Slug, t.LogoUrl, t.Tagline, t.DeclaredOrgUnitId,
                t.State.ToString(), t.MergedIntoTeamId, ms.Count(m => m.State == TeamMembershipState.Active),
                ms.Select(m => new TeamMemberView(m.Id, m.PersonId, m.PersonId is { } p ? names.GetValueOrDefault(p) : null, m.Role.ToString(), m.State.ToString(), m.ReplacedByMembershipId, m.JoinedAt)).ToList(),
                t.CreatedAt);
        }).ToList();
    }

    private static TeamInviteView ToInviteView(TeamInvite i) => new(i.Id, i.TeamId, i.InviteePersonId, i.InviteeEmail, i.InviteePhone, i.Role.ToString(), i.State.ToString(), i.Token, i.ExpiresAt);
    private static TeamJoinRequestView ToJoinRequestView(TeamJoinRequest r) => new(r.Id, r.TeamId, r.PersonId, r.Message, r.State.ToString(), r.CreatedAt);
    private static TeamPolicyView ToPolicyView(TeamPolicy p) => new(p.EventId, p.TicketTypeId, p.MinSize, p.MaxSize, p.FormationMode.ToString(), p.JoinApproval.ToString(),
        p.LockAt, p.NameEditUntil, p.RosterEditUntil, p.MentorEditUntil, p.MaxTeamsPerPersonInEvent, p.UnlimitedTeamsPerTree, p.AllowSoloAsTeam, p.AllowCrossOrgMembers,
        p.SubstitutesAllowed, p.SubstitutionDeadline, p.IncompleteTeamPolicy.ToString());

    private static bool TryEnum<T>(string? s, out T value, T current) where T : struct, Enum
    {
        value = current;
        if (s is null) return true;
        if (!Enum.TryParse<T>(s, ignoreCase: true, out var v)) return false;
        value = v; return true;
    }
}
