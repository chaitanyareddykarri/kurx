using Hangfire;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 3)]   // a lost run costs 24h, so retry it
public class NotificationCleanupJob
{
    /// <summary>Rows deleted per statement.
    ///
    /// <para>Small enough that each DELETE is a short transaction holding few row locks — a large one
    /// would block reads of the notification bell for its duration and bloat WAL in a single burst.
    /// Large enough that a backlog of millions drains in a bounded number of statements rather than
    /// thousands of round trips. 5,000 matches the batch sizes already used elsewhere in this codebase
    /// for bulk work (AnnouncementService fans out in 500s over a slower per-row cost).</para></summary>
    private const int BatchSize = 5_000;

    /// <summary>Hard ceiling on statements per run, so a bug in the predicate — or a table growing faster
    /// than a daily run can drain — cannot become an unbounded loop occupying a Hangfire worker and a
    /// connection indefinitely. At 5,000 per batch this caps one run at 10,000,000 rows; anything beyond
    /// that is drained by tomorrow's run and logged as a warning rather than silently pursued.</summary>
    private const int MaxBatchesPerRun = 2_000;

    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly KurxDbContext _db;
    private readonly ILogger<NotificationCleanupJob> _log;

    public NotificationCleanupJob(KurxDbContext db, ILogger<NotificationCleanupJob> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>Deletes notifications older than 30 days, in bounded database-side batches (DB-3).
    ///
    /// <para><b>What this replaced and why.</b> The previous implementation was
    /// <c>Where(n => n.CreatedAt &lt; cutoff).ToListAsync()</c> then <c>RemoveRange</c> +
    /// <c>SaveChangesAsync</c>. Three compounding problems: every expired row was materialised into the
    /// change tracker; <c>RemoveRange</c> makes EF emit one DELETE statement per row, all inside a single
    /// transaction; and <c>notifications</c> had no index on <c>CreatedAt</c> alone (the composite is led
    /// by <c>UserId</c>), so it began with a sequential scan. At a million stale rows that is a million
    /// tracked entities and a million statements in one transaction — an out-of-memory failure or an
    /// hours-long lock, faithfully retried three times by <c>AutomaticRetry</c>.</para>
    ///
    /// <para><c>ExecuteDeleteAsync</c> — already the established idiom here, used at 24 other sites — sends
    /// one set-based DELETE per batch and materialises nothing. The subquery form
    /// (<c>WHERE Id IN (SELECT … LIMIT n)</c>) is what makes each statement bounded; a bare
    /// <c>ExecuteDeleteAsync</c> on the predicate would delete every matching row in one transaction and
    /// reintroduce the lock problem it exists to avoid.</para>
    ///
    /// <para>Idempotent by construction: it deletes what matches, so a second run finds nothing and stops
    /// on the first batch. Cancellation is honoured between batches, and a cancelled run leaves committed
    /// batches committed — no partial-row state exists to corrupt, since each batch is its own transaction
    /// and a notification is a standalone row nothing else references.</para></summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - Retention;
        var total = 0;
        var batches = 0;

        while (batches < MaxBatchesPerRun)
        {
            ct.ThrowIfCancellationRequested();

            // The inner Select+Take is evaluated by Postgres as a subquery, so at most BatchSize rows are
            // ever locked or deleted by one statement. Ordering is deliberately omitted: any BatchSize of
            // the matching set is equally valid to delete, and an ORDER BY would add a sort for no gain.
            var deleted = await _db.Notifications
                .Where(n => _db.Notifications
                    .Where(x => x.CreatedAt < cutoff)
                    .Select(x => x.Id)
                    .Take(BatchSize)
                    .Contains(n.Id))
                .ExecuteDeleteAsync(ct);

            if (deleted == 0) break;

            total += deleted;
            batches++;
        }

        if (batches >= MaxBatchesPerRun)
            _log.LogWarning(
                "Notification cleanup hit its per-run ceiling of {MaxBatches} batches ({Total} rows deleted). "
                + "Rows older than {Days} days remain; the next run will continue. If this recurs, notification "
                + "volume has outgrown a daily sweep.",
                MaxBatchesPerRun, total, Retention.TotalDays);
        else if (total > 0)
            _log.LogInformation("Cleaned up {Count} notifications older than {Days} days in {Batches} batches.",
                total, Retention.TotalDays, batches);
        else
            _log.LogInformation("No notifications found older than {Days} days.", Retention.TotalDays);
    }
}
