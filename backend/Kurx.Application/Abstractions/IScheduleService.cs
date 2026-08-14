namespace Kurx.Application.Abstractions;

public record SessionInput(string Title, string? Description, string? Kind, DateTime StartsAt, DateTime EndsAt, int? Sort,
    Guid? InventoryPoolId = null);   // V3 §3.4 (Phase 12): an AgendaItem may hold a pool for a seat limit (config)

public record SessionView(Guid Id, Guid EventId, string Title, string Description, string Kind,
    DateTime StartsAt, DateTime EndsAt, int Sort, IReadOnlyList<Guid> SpeakerIds, Guid? InventoryPoolId);

/// <summary>Multi-day agenda: sessions and breaks for an event, ordered by Sort then StartsAt.</summary>
public interface IScheduleService
{
    Task<ServiceResult<SessionView>> CreateAsync(Guid userId, Guid eventId, bool isAdmin, SessionInput input, CancellationToken ct = default);

    Task<ServiceResult<SessionView>> UpdateAsync(Guid userId, Guid sessionId, bool isAdmin, SessionInput input, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid sessionId, bool isAdmin, CancellationToken ct = default);

    Task<IReadOnlyList<SessionView>> ListForEventAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Bulk-sets Sort for the given session ids, in the order supplied.</summary>
    Task<ServiceResult<bool>> ReorderAsync(Guid userId, Guid eventId, bool isAdmin, IReadOnlyList<Guid> orderedSessionIds, CancellationToken ct = default);
}
