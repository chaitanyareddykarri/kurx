using System.Data.Common;
using Hangfire;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>Samples the PostgreSQL catalogs and publishes what RDS does not (DB-9).
///
/// <para><b>Scope, deliberately narrow.</b> Connection <i>count</i>, CPU and free storage already reach
/// CloudWatch from RDS itself and already have alarms; this job does not duplicate them. It exists for the two
/// signals that have no AWS source for RDS PostgreSQL — <b>deadlocks</b> (the CloudWatch <c>Deadlocks</c>
/// metric is Aurora-only) and <b>bloat</b> (not an AWS concept; it lives in <c>pg_stat_user_tables</c>). The
/// server-side connection gauges come along because they cost one column of the same query and answer a
/// question Npgsql's per-process pool metrics cannot: usage against the <i>server's</i> ceiling rather than
/// against this task's.</para>
///
/// <para><b>Observation only.</b> Nothing here vacuums, reconfigures autovacuum, kills a backend or changes a
/// setting. DB-9 is the phase that makes these visible; acting on them is a separate decision that needs the
/// measurements this produces.</para></summary>
[DisableConcurrentExecution(timeoutInSeconds: 60)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // a probe is worthless late; the next tick is 5 minutes away
public class DatabaseHealthProbeJob(KurxDbContext db, ILogger<DatabaseHealthProbeJob> log)
{
    /// <summary>Tables always reported regardless of their current dead-tuple count, so their series exist
    /// continuously and a dashboard shows a flat line rather than a gap. These are the high-churn tables the
    /// DB audit named as future autovacuum-tuning candidates — every row is updated in place on a hot path
    /// (pool decrement, sold-count mirror, wallet balance), which is the shape that bloats.</summary>
    private static readonly string[] WatchedTables = ["inventory_pools", "ticket_types", "organization_wallet"];

    /// <summary>Dead-tuple floor below which a table is not worth a metric series. Chosen to keep CloudWatch
    /// cardinality bounded and the cost with it: on a healthy database almost nothing clears this, so the
    /// usual run reports only <see cref="WatchedTables"/>.</summary>
    private const int MinDeadTuplesToReport = 1_000;

    /// <summary>Hard cap on reported tables. Without it a database-wide vacuum stall would publish a series
    /// per table at once — the moment monitoring cost spikes is the moment an incident starts.</summary>
    private const int MaxTablesReported = 15;

    /// <summary>Last observed cumulative deadlock count, for the delta.
    ///
    /// <para><b>Process-local, and that is a deliberate ceiling.</b> <c>pg_stat_database.deadlocks</c> is
    /// cumulative since the last stats reset, so a delta needs a previous reading. Holding it in a static means
    /// the first probe after a deploy or a restart has no baseline and publishes nothing — one interval of
    /// deadlocks is missed per restart. The alternative is persisting the reading, which buys a single
    /// five-minute window at the cost of a table, a migration and a write on every probe. The alarm this feeds
    /// is deliberately about <i>sustained</i> activity across several periods, so a missed interval at restart
    /// cannot hide what it is looking for.</para>
    ///
    /// <para>Not a race despite being static: <c>DisableConcurrentExecution</c> keeps one run at a time across
    /// every replica, so there is never a second writer.</para></summary>
    private static long? _lastDeadlockCount;

    public async Task RunAsync(CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await ProbeServerAsync(conn, ct);
            await ProbeTablesAsync(conn, ct);
        }
        finally
        {
            // Paired with the explicit open. EF counts explicit opens and would otherwise hold the connection
            // for the whole scope — on a job that runs every five minutes for the life of the process, that is
            // a connection permanently out of a pool this phase exists to keep an eye on.
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Connections against the server ceiling, and deadlocks since the previous probe.</summary>
    private async Task ProbeServerAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        // Two details that are easy to get wrong and were both verified against a live server rather than
        // assumed:
        //
        //   backend_type = 'client backend' — a bare count(*) over pg_stat_activity also counts the
        //   checkpointer, walwriter, background writer, autovacuum launcher and logical replication launcher
        //   (measured: 15 rows, 10 of them client). Those are NOT charged against max_connections, so
        //   including them over-reports utilisation by a constant handful and quietly falsifies the one
        //   comparison this metric exists to support.
        //
        //   current_setting rather than a hard-coded ceiling — max_connections is derived from the instance
        //   class, so resizing the database invalidates any number written down here or in an alarm.
        //
        // COALESCE on deadlocks because a scalar subquery yielding no row is NULL, and GetInt64 throws on
        // DBNull. Defensive rather than observed: pg_stat_database always has a row for the current database.
        cmd.CommandText = """
            SELECT (SELECT count(*) FROM pg_stat_activity
                     WHERE backend_type = 'client backend')                      AS used,
                   current_setting('max_connections')::bigint                    AS max_conn,
                   COALESCE((SELECT deadlocks FROM pg_stat_database
                              WHERE datname = current_database()), 0)            AS deadlocks
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return;

        var used = reader.GetInt64(0);
        var max = reader.GetInt64(1);
        var deadlocks = reader.GetInt64(2);

        DatabaseTelemetry.ConnectionsUsed.Record(used);
        DatabaseTelemetry.ConnectionsMax.Record(max);
        if (max > 0)
            DatabaseTelemetry.ConnectionUtilization.Record(Math.Round(used * 100.0 / max, 2));

        // A stats reset (or a failover onto a fresh instance) moves the counter backwards. Treating that as a
        // negative delta would be meaningless and treating it as `deadlocks` would publish the whole history
        // as one spike, so the reading is re-baselined and nothing is emitted for this interval.
        if (_lastDeadlockCount is { } previous && deadlocks >= previous)
        {
            var delta = deadlocks - previous;
            if (delta > 0)
            {
                DatabaseTelemetry.Deadlocks.Add(delta);
                // Logged as well as counted: the metric says how many, the PostgreSQL log (exported to
                // CloudWatch Logs) says which statements, and this line is what ties the two together by time.
                log.LogWarning(
                    "PostgreSQL reported {Delta} deadlock(s) since the last probe ({Total} cumulative). "
                    + "Deadlock detail is in the postgresql log export, not here.", delta, deadlocks);
            }
        }
        _lastDeadlockCount = deadlocks;
    }

    /// <summary>Per-table bloat: dead-tuple ratio, vacuum recency and total size.</summary>
    private async Task ProbeTablesAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        // GREATEST ignores NULLs in PostgreSQL, so a table vacuumed by only one of the two mechanisms still
        // reports; it is NULL only when the table has never been vacuumed at all, which is handled below.
        // The ::double precision cast is NOT cosmetic. Since PostgreSQL 14 EXTRACT returns `numeric`, so this
        // expression is numeric, Npgsql maps it to `decimal`, and GetDouble then throws InvalidCastException
        // — every five minutes, forever, on any table that has ever been vacuumed. Caught by running the
        // query against a real database rather than by reading it; DatabaseHealthProbeTests now executes this
        // job so a future edit to this SQL cannot reintroduce a type mismatch silently.
        cmd.CommandText = $"""
            SELECT relname,
                   n_live_tup,
                   n_dead_tup,
                   (EXTRACT(EPOCH FROM (now() - GREATEST(last_vacuum, last_autovacuum))) / 3600.0)::double precision
                       AS hours_since_vacuum,
                   pg_total_relation_size(relid) AS total_bytes
              FROM pg_stat_user_tables
             WHERE relname = ANY(@watched) OR n_dead_tup >= @min_dead
             ORDER BY n_dead_tup DESC
             LIMIT {MaxTablesReported}
            """;
        AddParameter(cmd, "watched", WatchedTables);
        AddParameter(cmd, "min_dead", MinDeadTuplesToReport);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var table = reader.GetString(0);
            var live = reader.GetInt64(1);
            var dead = reader.GetInt64(2);
            var bytes = reader.GetInt64(4);
            var tag = new KeyValuePair<string, object?>("table", table);

            // Ratio against live rows, not against size: bloat is dead storage relative to useful storage, so
            // a 40 GB table that is entirely live is healthy and a 4 MB one that is half dead is not. An empty
            // table has no meaningful ratio — reporting 100% for a table with one dead row and no live ones
            // would page for a row that was inserted and deleted.
            if (live > 0)
                DatabaseTelemetry.DeadTupleRatio.Record(Math.Round(dead * 100.0 / live, 2), tag);

            // NULL means never vacuumed. Left unreported rather than substituted with a sentinel: a fabricated
            // "very large" value would be indistinguishable from a real measurement on a graph, and the
            // dead-tuple ratio already carries the signal for a table that needs attention.
            if (!await reader.IsDBNullAsync(3, ct))
                DatabaseTelemetry.HoursSinceVacuum.Record(Math.Round(reader.GetDouble(3), 2), tag);

            DatabaseTelemetry.TableBytes.Record(bytes, tag);
        }
    }

    /// <summary>Adds a parameter without naming Npgsql's concrete type, so this file stays provider-agnostic
    /// the way the rest of Infrastructure's data access is.</summary>
    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
