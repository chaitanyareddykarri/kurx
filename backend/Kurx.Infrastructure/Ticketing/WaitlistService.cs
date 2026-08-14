using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Ticketing;

/// <summary>Ticket waitlist (D-064). Join only when the ticket type is sold out — decided by the authoritative
/// inventory pool's remaining availability (§17.1, Phase 9), not the legacy TicketType.Sold mirror.</summary>
public class WaitlistService(KurxDbContext db, IInventoryService inventory) : IWaitlistService
{
    public async Task<ServiceResult<WaitlistView>> JoinAsync(Guid userId, Guid eventId, Guid ticketTypeId, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketTypeId && t.EventId == eventId, ct);
        if (tt is null) return ServiceResult<WaitlistView>.Fail("not_found");
        // Sold-out is decided by the AUTHORITATIVE pool (§17.1, Phase 9), never the legacy TicketType.Sold mirror.
        if (await inventory.AvailableAsync(ticketTypeId, ct) > 0) return ServiceResult<WaitlistView>.Fail("tickets_available");
        if (await db.TicketWaitlists.AnyAsync(w => w.TicketTypeId == ticketTypeId && w.UserId == userId && w.Status == WaitlistStatus.Waiting, ct))
            return ServiceResult<WaitlistView>.Fail("already_waitlisted");

        var count = await db.TicketWaitlists.CountAsync(w => w.TicketTypeId == ticketTypeId && w.Status == WaitlistStatus.Waiting, ct);
        var entry = new TicketWaitlist
        {
            EventId = eventId, TicketTypeId = ticketTypeId,
            PoolId = await inventory.GeneralPoolIdAsync(ticketTypeId, ct),   // V3 §8.5 — a waitlist attaches to a pool (Phase 7)
            UserId = userId,
            Position = count + 1, Status = WaitlistStatus.Waiting,
        };
        db.TicketWaitlists.Add(entry);
        await db.SaveChangesAsync(ct);
        return ServiceResult<WaitlistView>.Success(ToView(entry));
    }

    public async Task<bool> LeaveAsync(Guid userId, Guid eventId, Guid ticketTypeId, CancellationToken ct = default)
        => await db.TicketWaitlists
            .Where(w => w.TicketTypeId == ticketTypeId && w.EventId == eventId && w.UserId == userId && w.Status == WaitlistStatus.Waiting)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, WaitlistStatus.Cancelled), ct) > 0;

    public async Task<IReadOnlyList<WaitlistView>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.TicketWaitlists.AsNoTracking()
            .Where(w => w.UserId == userId && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Notified))
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new { w.Id, w.EventId, w.TicketTypeId, w.Position, w.Status, w.OfferExpiresAt, w.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(w => new WaitlistView(w.Id, w.EventId, w.TicketTypeId, w.Position,
            w.Status.ToString(), w.OfferExpiresAt, w.CreatedAt)).ToList();
    }

    private static WaitlistView ToView(TicketWaitlist w) => new(w.Id, w.EventId, w.TicketTypeId, w.Position,
        w.Status.ToString(), w.OfferExpiresAt, w.CreatedAt);
}
