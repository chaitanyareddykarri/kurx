using Hangfire;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Runs every 5 minutes. Transitions waitlist offers from Notified → Expired once the
// claim window closes (OfferExpiresAt), freeing the slot so the queue can advance.
[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class ExpireWaitlistOffersJob
{
    private readonly KurxDbContext _db;
    private readonly ILogger<ExpireWaitlistOffersJob> _log;

    public ExpireWaitlistOffersJob(KurxDbContext db, ILogger<ExpireWaitlistOffersJob> log)
    {
        _db = db;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.TicketWaitlists
            .Where(w => w.Status == WaitlistStatus.Notified && w.OfferExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var entry in expired)
            entry.Status = WaitlistStatus.Expired;

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Expired {Count} waitlist offers", expired.Count);
    }
}
