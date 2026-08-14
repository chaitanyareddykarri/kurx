namespace Kurx.Application.Abstractions;

public record AssignmentView(Guid Id, Guid EventId, Guid OrgId, Guid UserId, string Role, string? CustomRole,
    string Status, bool ShowOnProfile, string? Notes, DateTime CreatedAt,
    // Assignee identity (D-20x) — every assignment is a real Kurx account (UserId is non-nullable
    // on the entity), so Name is always present; Username/AvatarKey null when unclaimed/not public.
    string AssigneeName, string? AssigneeUsername, string? AssigneeAvatarKey,
    // Event identity (D-319). The organiser's list needs to say WHO was invited and already did; the
    // invitee's own list needs to say WHICH event, hosted by whom, and when — an inbox row reading
    // "Judge" with a bare uuid is not answerable. Both lists carry it because one view feeding two
    // audiences is cheaper than a second record that drifts from this one.
    // RepresentingOrgName is null for a self-represented event, and deliberately so: D-268 says a
    // personally-hosted event carries NO organization identity, and the self-representation row is
    // named after the person. Emitting it here would surface that internal row on every invite.
    string EventTitle, string? EventSlug, DateTime EventStartsAt, string? RepresentingOrgName);

/// <summary>Event staff assignments (D-064): Owner/Manager invite users to a role (Volunteer/Judge/… or
/// Custom) at an event; the invitee accepts/declines; accepted assignments show on their public profile.
/// Finishes the scaffolded <c>event_assignments</c> table.</summary>
public interface IEventAssignmentService
{
    Task<ServiceResult<AssignmentView>> AssignAsync(Guid actorId, Guid orgId, Guid eventId, string phone,
        string role, string? customRole, string? notes, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<AssignmentView>>> ListForEventAsync(Guid actorId, Guid orgId, Guid eventId, CancellationToken ct = default);
    Task<ServiceResult<bool>> RemoveAsync(Guid actorId, Guid orgId, Guid eventId, Guid assignmentId, CancellationToken ct = default);
    Task<ServiceResult<AssignmentView>> RespondAsync(Guid userId, Guid assignmentId, bool accept, CancellationToken ct = default);
    Task<IReadOnlyList<AssignmentView>> ListMineAsync(Guid userId, CancellationToken ct = default);
}
