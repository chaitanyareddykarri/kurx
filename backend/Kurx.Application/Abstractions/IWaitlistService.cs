namespace Kurx.Application.Abstractions;

public record WaitlistView(Guid Id, Guid EventId, Guid TicketTypeId, int Position, string Status,
    DateTime? OfferExpiresAt, DateTime CreatedAt);

/// <summary>Ticket waitlist (D-064): join when a ticket type is sold out; leave; list mine. Finishes the
/// scaffolded <c>ticket_waitlist</c> table. Auto-promotion on freed capacity is a follow-up (it needs a
/// capacity-freeing trigger — refund/cancel — which doesn't exist yet; the ExpireWaitlistOffersJob is in place).</summary>
public interface IWaitlistService
{
    /// <returns>Fail("not_found") unknown ticket type; Fail("tickets_available") if not sold out (buy instead); Fail("already_waitlisted").</returns>
    Task<ServiceResult<WaitlistView>> JoinAsync(Guid userId, Guid eventId, Guid ticketTypeId, CancellationToken ct = default);
    Task<bool> LeaveAsync(Guid userId, Guid eventId, Guid ticketTypeId, CancellationToken ct = default);
    Task<IReadOnlyList<WaitlistView>> ListMineAsync(Guid userId, CancellationToken ct = default);
}
