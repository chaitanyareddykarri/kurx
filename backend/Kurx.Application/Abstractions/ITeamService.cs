namespace Kurx.Application.Abstractions;

/// <summary>The Team subsystem (V3 §6, Phase 10) — the only group entity, and only where competition exists.
/// Additive: the purchase <c>Group</c> stays as a legacy compatibility mirror and the Phase-9 money path is
/// untouched. This phase is <b>formation only</b>: create/roster/invite/join-request/substitute/lifecycle/merge/
/// split + <c>TeamPolicy</c>. Organiser actions reuse the Phase-6 <c>event:manage</c> permission union — no
/// parallel authorization.
///
/// <para><b>§6.5 team-slot purchase landed in D-374.</b> A competition group registration now materialises the
/// Team alongside the legacy purchase <c>Group</c> — see <see cref="MaterialiseForGroupAsync"/> — so buying a team
/// entry produces the authoritative Team rather than only its mirror. The Phase-9 money path is still
/// untouched: the Team is a projection of a completed registration, never a second inventory or payment
/// subject.</para></summary>
public interface ITeamService
{
    // ── TeamPolicy (§6.3) — one per competition ticket type, like the registration policy / Pass ──────────────
    /// <summary>Ensure a TeamPolicy exists for a competition ticket type, derived from its config. Added on the
    /// caller's DbContext (not saved) so it commits with the ticket-type change. No-op for non-competition types.</summary>
    Task SyncPolicyAsync(Guid ticketTypeId, CancellationToken ct = default);
    Task<ServiceResult<TeamPolicyView>> GetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<TeamPolicyView>> SetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, TeamPolicyInput input, CancellationToken ct = default);

    // ── Formation (users + captains) ──────────────────────────────────────────────────────────────────────────
    /// <summary>D-374 — materialise the authoritative <c>Team</c> for a purchase <c>Group</c>, and keep its roster
    /// in step as members join.
    ///
    /// <para><b>Why not <see cref="CreateTeamAsync"/>.</b> That is the user-facing formation entry point and
    /// enforces things a completed purchase has already settled: it refuses organiser-formed modes, applies
    /// <c>MaxTeamsPerPersonInEvent</c>, and demands a name. Routing a paid registration through it would let a
    /// policy refuse a team someone has already been charged for. This is the projection side — the purchase IS
    /// the authorisation — and it reuses the same entities, policy and slug rules rather than adding a second
    /// team model.</para>
    ///
    /// <para>Idempotent and additive: a no-op for a non-competition ticket type, for a group whose Team already
    /// exists, and for a member already on the roster. Rows are added to the CALLER's DbContext and not saved,
    /// so the Team commits inside the same money transaction as the Group it mirrors.</para></summary>
    Task MaterialiseForGroupAsync(Guid groupId, CancellationToken ct = default);

    Task<ServiceResult<TeamView>> CreateTeamAsync(Guid userId, Guid eventId, Guid ticketTypeId, TeamInput input, CancellationToken ct = default);
    Task<ServiceResult<TeamView>> UpdateTeamAsync(Guid userId, Guid teamId, bool isAdmin, TeamInput input, CancellationToken ct = default);
    Task<ServiceResult<TeamInviteView>> InviteAsync(Guid userId, Guid teamId, TeamInviteInput input, CancellationToken ct = default);
    Task<ServiceResult<TeamView>> AcceptInviteAsync(Guid userId, string token, CancellationToken ct = default);
    Task<ServiceResult<bool>> RevokeInviteAsync(Guid userId, Guid inviteId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<TeamJoinRequestView>> RequestJoinAsync(Guid userId, Guid teamId, string? message, CancellationToken ct = default);
    Task<ServiceResult<bool>> DecideJoinRequestAsync(Guid userId, Guid requestId, bool approve, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<bool>> LeaveAsync(Guid userId, Guid teamId, CancellationToken ct = default);
    Task<ServiceResult<bool>> RemoveMemberAsync(Guid userId, Guid teamId, Guid membershipId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<TeamView>> SubstituteAsync(Guid userId, Guid teamId, Guid outMembershipId, Guid inPersonId, bool isAdmin, CancellationToken ct = default);

    // ── Lifecycle + composite transactions (organiser, §6.2/§6.4) ─────────────────────────────────────────────
    /// <summary>Organiser lifecycle transition: <c>lock</c>, <c>compete</c>, <c>disqualify</c> (requires reason),
    /// <c>withdraw</c>, <c>eliminate</c>, <c>finalist</c>. Every transition is audited.</summary>
    Task<ServiceResult<TeamView>> TransitionAsync(Guid actorId, Guid teamId, bool isAdmin, string action, string? reason, CancellationToken ct = default);
    Task<ServiceResult<TeamView>> MergeAsync(Guid actorId, Guid teamAId, Guid teamBId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<TeamSplitView>> SplitAsync(Guid actorId, Guid teamId, IReadOnlyList<Guid> movePersonIds, string newName, bool isAdmin, CancellationToken ct = default);

    // ── Reads ─────────────────────────────────────────────────────────────────────────────────────────────────
    Task<ServiceResult<IReadOnlyList<TeamView>>> GetForEventAsync(Guid eventId, CancellationToken ct = default);
    Task<ServiceResult<TeamView>> GetTeamAsync(Guid teamId, CancellationToken ct = default);
    Task<IReadOnlyList<TeamView>> MyTeamsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One-time, idempotent migration: back-fill a Team from every existing <c>Group</c> on a competition
    /// ticket type (captain = leader, memberships from the group members). Runs at startup. Group stays a mirror.</summary>
    Task<int> BackfillFromGroupsAsync(CancellationToken ct = default);
}

public record TeamInput(string Name, string? Slug = null, string? LogoUrl = null, string? Tagline = null, Guid? DeclaredOrgUnitId = null);

public record TeamInviteInput(Guid? PersonId = null, string? Email = null, string? Phone = null, string? Role = null);

public record TeamPolicyInput(int? MinSize, int? MaxSize, string? FormationMode, string? JoinApproval, DateTime? LockAt,
    DateTime? NameEditUntil, DateTime? RosterEditUntil, DateTime? MentorEditUntil, int? MaxTeamsPerPersonInEvent,
    bool? UnlimitedTeamsPerTree, bool? AllowSoloAsTeam, bool? AllowCrossOrgMembers, int? SubstitutesAllowed,
    DateTime? SubstitutionDeadline, string? IncompleteTeamPolicy);

public record TeamMemberView(Guid MembershipId, Guid? PersonId, string? Name, string Role, string State, Guid? ReplacedByMembershipId, DateTime JoinedAt);

public record TeamView(Guid Id, Guid EventId, Guid TicketTypeId, string Name, string Slug, string? LogoUrl, string? Tagline,
    Guid? DeclaredOrgUnitId, string State, Guid? MergedIntoTeamId, int ActiveMemberCount, IReadOnlyList<TeamMemberView> Members, DateTime CreatedAt);

public record TeamSplitView(TeamView Original, TeamView NewTeam);

public record TeamInviteView(Guid Id, Guid TeamId, Guid? InviteePersonId, string? InviteeEmail, string? InviteePhone, string Role, string State, string Token, DateTime ExpiresAt);

public record TeamJoinRequestView(Guid Id, Guid TeamId, Guid PersonId, string? Message, string State, DateTime CreatedAt);

public record TeamPolicyView(Guid EventId, Guid TicketTypeId, int MinSize, int MaxSize, string FormationMode, string JoinApproval,
    DateTime? LockAt, DateTime? NameEditUntil, DateTime? RosterEditUntil, DateTime? MentorEditUntil, int MaxTeamsPerPersonInEvent,
    bool UnlimitedTeamsPerTree, bool AllowSoloAsTeam, bool AllowCrossOrgMembers, int SubstitutesAllowed, DateTime? SubstitutionDeadline,
    string IncompleteTeamPolicy);
