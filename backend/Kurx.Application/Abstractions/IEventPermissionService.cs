namespace Kurx.Application.Abstractions;

/// <summary>Effective event-scoped permission (V3 §5.4, Phase 6). The union of the caller's org/unit grants
/// and their ACTIVE participant grants on this event, evaluated live per request (never from a token —
/// preserving D-015). Participant grants are a FOURTH grant source, scoped strictly to the event: a
/// participant role never grants org-level permission, and grants flow down only.</summary>
public interface IEventPermissionService
{
    Task<bool> HasAsync(Guid userId, Guid eventId, string permission, CancellationToken ct = default);
}
