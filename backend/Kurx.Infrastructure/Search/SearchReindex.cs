using Kurx.Domain.Entities;

namespace Kurx.Infrastructure.Search;

/// <summary>Builds the <c>search.reindex</c> outbox message (V3 §15, Phase 16). Every service that mutates a field the
/// discovery document is built from — the event row + tags (EventService), audience rule (AudienceService), ticket
/// pricing (TicketTypeService), series membership/mode (SeriesService) — adds one of these to <c>outbox_messages</c>
/// in the SAME transaction as its write, so the index is fed transactionally and never dual-written. The projector
/// rebuilds from live state, so duplicate messages are harmless (idempotent); the per-message key just keeps each row
/// unique.</summary>
public static class SearchReindex
{
    public static OutboxMessage Message(Guid eventId) => new()
    {
        Type = "search.reindex",
        PayloadJson = $"{{\"eventId\":\"{eventId}\"}}",
        IdempotencyKey = $"search.reindex:{eventId}:{Guid.CreateVersion7():N}",
    };
}
