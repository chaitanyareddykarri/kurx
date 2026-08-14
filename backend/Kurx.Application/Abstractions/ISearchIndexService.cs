namespace Kurx.Application.Abstractions;

/// <summary>Maintains the V3 §15 discovery index (Phase 16). The index is **outbox-fed, never dual-written**: an event
/// write enqueues a <c>search.reindex</c> message in its own transaction, and the outbox dispatcher calls
/// <see cref="ProjectAsync"/> to (re)build or remove that event's document. <see cref="RefreshSignalsAsync"/> keeps the
/// asynchronous ranking signals (velocity/conversion) and the RECURRING one-listing collapse current on a schedule.</summary>
public interface ISearchIndexService
{
    /// <summary>Idempotently rebuild one event's search document — or delete it when the event is no longer a
    /// discoverable (Published + Public + not-deleted) candidate. Safe to call repeatedly (at-least-once outbox).</summary>
    Task ProjectAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>One-time backfill: project every discoverable event that has no document yet. Returns the count.</summary>
    Task<int> BackfillAsync(CancellationToken ct = default);

    /// <summary>Refresh the asynchronous ranking signals (recent-view velocity over the window, conversion counts) and
    /// recompute the RECURRING series primary occurrence. Idempotent; run periodically by a background job.</summary>
    Task<int> RefreshSignalsAsync(CancellationToken ct = default);
}
