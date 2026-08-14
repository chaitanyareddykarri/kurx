using Hangfire;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

// Runs daily. After an event ends and the 7-day Razorpay settlement window clears,
// transitions LedgerEntry.Collected → Available and updates the OrganizationWallet cache
// so the org's dashboard shows funds as withdrawable.
[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 3)]   // a lost run costs 24h, so retry it
public class CollectedToAvailableLedgerJob
{
    // Razorpay standard settlement window. Override via configuration in a future D-NNN.
    private const int SettlementDays = 7;

    private readonly KurxDbContext _db;
    private readonly ILogger<CollectedToAvailableLedgerJob> _log;

    public CollectedToAvailableLedgerJob(KurxDbContext db, ILogger<CollectedToAvailableLedgerJob> log)
    {
        _db = db;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-SettlementDays);

        // Ledger entries are Collected; their associated event must have ended before the cutoff.
        // Projected AsNoTracking: every mutation below is an in-SQL statement, so a tracked copy would
        // only be a second, stale writer of the same rows.
        var entries = await _db.LedgerEntries.AsNoTracking()
            .Where(l => l.State == LedgerState.Collected)
            .Join(_db.Events.AsNoTracking(),
                l => l.EventId,
                e => e.Id,
                (l, e) => new { Entry = l, Event = e })
            .Where(x => x.Event.EndsAt < cutoff)
            .Select(x => new { x.Entry.Id, x.Entry.OrgId, x.Entry.AmountPaise })
            .ToListAsync(ct);

        if (entries.Count == 0) return;

        // One transaction per entry, each doing an atomic claim then an in-SQL move.
        //
        // Previously this loaded the wallets, applied `-= / +=` in memory and saved once. Two things broke:
        // a re-entrant run (Hangfire re-dispatches a job whose invisibility timeout lapsed) could apply the
        // same delta twice, and the read-modify-write raced every concurrent capture/refund on the same
        // wallet — both writers computing an absolute balance from their own stale snapshot.
        //
        // The conditional claim on the entry's own State makes maturation idempotent: a second run finds
        // the row already Available, claims 0, and moves no money.
        var matured = 0;
        var skipped = 0;
        var underfunded = 0;

        foreach (var entry in entries)
        {
            var amount = entry.AmountPaise;
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var claimed = await _db.LedgerEntries
                .Where(l => l.Id == entry.Id && l.State == LedgerState.Collected)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.State, LedgerState.Available), ct);
            if (claimed == 0)
            {
                await tx.RollbackAsync(ct);
                skipped++;
                continue;
            }

            // Guarded so an already-drifted wallet fails this entry loudly instead of tripping the
            // ck_org_wallet_collected CHECK and taking the whole sweep down with it.
            var moved = await _db.OrganizationWallets
                .Where(w => w.OrgId == entry.OrgId && w.CollectedPaise >= amount)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.CollectedPaise, w => w.CollectedPaise - amount)
                    .SetProperty(w => w.AvailablePaise, w => w.AvailablePaise + amount)
                    .SetProperty(w => w.UpdatedAt, DateTime.UtcNow), ct);
            if (moved == 0)
            {
                await tx.RollbackAsync(ct);
                underfunded++;
                _log.LogWarning(
                    "Ledger entry {EntryId} (org {OrgId}, {AmountPaise} paise) not matured: wallet has no " +
                    "sufficient Collected balance. Wallet cache has drifted from the ledger.",
                    entry.Id, entry.OrgId, amount);
                continue;
            }

            await tx.CommitAsync(ct);
            matured++;
        }

        _log.LogInformation(
            "Settled {Count} ledger entries (Collected→Available) across {Orgs} orgs; {Skipped} already matured, {Underfunded} blocked on wallet drift",
            matured, entries.Select(e => e.OrgId).Distinct().Count(), skipped, underfunded);
    }
}
