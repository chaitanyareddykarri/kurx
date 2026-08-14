namespace Kurx.Application.Abstractions;

/// <summary>Participants (V3 §5, Phase 6). One model replacing V2's EventAssignment / capability people-lists /
/// org RBAC. Assign/list/respond/remove event participants against the platform role registry; capacity and
/// public-listing are role properties (§5.2); management authz is the §5.4 union (org grant OR an event
/// ORGANISER participant grant). V2 <c>EventAssignment</c> is kept and backfilled during the strangler window.</summary>
public interface IParticipantService
{
    Task<IReadOnlyList<ParticipantRoleView>> ListRolesAsync(CancellationToken ct = default);

    Task<ServiceResult<ParticipantView>> AssignAsync(Guid actorId, Guid orgId, Guid eventId, AssignParticipantInput input, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ParticipantView>>> ListForEventAsync(Guid actorId, Guid eventId, CancellationToken ct = default);
    Task<ServiceResult<ParticipantView>> RespondAsync(Guid userId, Guid participantId, bool accept, CancellationToken ct = default);
    Task<ServiceResult<bool>> RemoveAsync(Guid actorId, Guid eventId, Guid participantId, CancellationToken ct = default);
    Task<IReadOnlyList<ParticipantView>> ListMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One-time migration (idempotent): mirror live V2 EventAssignment rows into EventParticipant,
    /// mapping the 14 free-text roles to platform slugs. Runs at startup after the role seeder.</summary>
    Task<int> BackfillFromAssignmentsAsync(CancellationToken ct = default);
}

/// <summary>Assign a Person (by <paramref name="Phone"/>) or an OrgUnit (by <paramref name="OrgUnitId"/>).
/// Exactly one subject. Team subjects are Phase 10.</summary>
public record AssignParticipantInput(string RoleSlug, string? Phone, Guid? OrgUnitId, string? CustomLabel,
    string? Visibility, bool WholeEvent = true, IReadOnlyList<Guid>? SubEvents = null);

public record ParticipantView(Guid Id, Guid EventId, string SubjectType, Guid SubjectId, string RoleSlug,
    string RoleClass, string? CustomLabel, string State, string Visibility, bool CountsTowardCapacity,
    string? InventorySegment, DateTime CreatedAt);

public record ParticipantRoleView(string Slug, string Name, string Class, bool IsPublic,
    bool CountsTowardCapacity, string? InventorySegment, IReadOnlyList<string> DefaultPermissions);
