using Hangfire;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Runs every minute. Transitions SeatHold.Active → Expired for holds past their window and RELEASES the
// authoritative pool hold (Held -= qty, §17.1, Phase 9), keeping the legacy TicketType.Sold mirror in step.
[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class ExpireSeatHoldsJob
{
    private readonly KurxDbContext _db;
    private readonly ILogger<ExpireSeatHoldsJob> _log;
    private readonly IInventoryService _inventory;

    public ExpireSeatHoldsJob(KurxDbContext db, ILogger<ExpireSeatHoldsJob> log, IInventoryService inventory)
    {
        _db = db;
        _log = log;
        _inventory = inventory;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.SeatHolds
            .Where(h => h.Status == SeatHoldStatus.Active && h.ExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        var ticketTypeIds = expired.Select(h => h.TicketTypeId).Distinct().ToList();
        var ticketTypes = await _db.TicketTypes
            .Where(t => ticketTypeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        foreach (var hold in expired)
        {
            hold.Status = SeatHoldStatus.Expired;
            if (ticketTypes.TryGetValue(hold.TicketTypeId, out var tt))
                tt.Sold = Math.Max(0, tt.Sold - hold.Qty);   // legacy mirror
            // Release the authoritative pool hold (§17.1). Prefer the hold's own pool; fall back to the general pool.
            var poolId = hold.PoolId ?? await _inventory.GeneralPoolIdAsync(hold.TicketTypeId, ct);
            if (poolId is { } pid) await _inventory.ReleaseHoldAsync(pid, hold.Qty, ct);
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Expired {Count} seat holds", expired.Count);
    }
}
