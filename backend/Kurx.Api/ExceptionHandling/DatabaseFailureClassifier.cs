using Npgsql;

namespace Kurx.Api.ExceptionHandling;

/// <summary>What kind of database failure this was (DB-2). The distinction is operational, not cosmetic:
/// three of these four look identical in a log and have different — in one case opposite — remedies.</summary>
public enum DatabaseFailureKind
{
    /// <summary>Not a database failure. Fall through to the generic handler.</summary>
    None = 0,

    /// <summary>OUR pool ceiling was reached. The database is healthy and has spare capacity; this
    /// instance simply will not open another connection. Remedy: raise <c>Database__MaxPoolSize</c>, or
    /// find the query holding connections.</summary>
    PoolExhausted,

    /// <summary>POSTGRES refused the connection (SQLSTATE 53300, <c>too_many_connections</c>). Every
    /// replica together has exceeded the server. Remedy: LOWER the per-task ceiling, or add a pooler.
    /// Raising our pool size here makes it strictly worse — which is why this must never be reported as
    /// <see cref="PoolExhausted"/>.</summary>
    ServerConnectionLimit,

    /// <summary>A statement outlived <c>CommandTimeoutSeconds</c>, or was cancelled server-side
    /// (57014). A slow query, not a capacity problem.</summary>
    CommandTimeout,

    /// <summary>A row lock could not be taken within <c>lock_timeout</c> (55P03). Someone else holds it.
    /// Retryable, and deliberately preferred over waiting forever.</summary>
    LockTimeout,

    /// <summary>The server could not be reached at all.</summary>
    Unavailable,
}

/// <summary>Maps an exception to a <see cref="DatabaseFailureKind"/>.
///
/// <para>Split out from the exception handler so the mapping can be asserted directly in tests. The
/// alternative — proving this by actually exhausting a pool or killing a server mid-suite — tests the
/// harness more than the code, and would make the suite non-deterministic.</para></summary>
public static class DatabaseFailureClassifier
{
    /// <summary>Npgsql's pool-exhaustion message. There is no distinct exception type or error code for it,
    /// so the message is the only available signal. Matched case-insensitively on a stable fragment rather
    /// than the whole string, which embeds the configured sizes and therefore varies per deployment.</summary>
    private const string PoolExhaustedFragment = "connection pool has been exhausted";

    public static DatabaseFailureKind Classify(Exception? exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            // PostgresException first: a server-assigned SQLSTATE is authoritative and unambiguous, where
            // the checks below are inference.
            if (e is PostgresException pg)
            {
                switch (pg.SqlState)
                {
                    case "53300": return DatabaseFailureKind.ServerConnectionLimit;   // too_many_connections
                    case "57014": return DatabaseFailureKind.CommandTimeout;          // query_canceled
                    case "55P03": return DatabaseFailureKind.LockTimeout;             // lock_not_available
                    case "57P01":                                                     // admin_shutdown
                    case "57P02":                                                     // crash_shutdown
                    case "57P03": return DatabaseFailureKind.Unavailable;             // cannot_connect_now
                }
                // Any other PostgresException is a real query/constraint error — a bug or a business
                // conflict, not an infrastructure failure. Do not dress it up as one.
                return DatabaseFailureKind.None;
            }

            if (e is NpgsqlException npgsql)
            {
                if (npgsql.Message.Contains(PoolExhaustedFragment, StringComparison.OrdinalIgnoreCase))
                    return DatabaseFailureKind.PoolExhausted;

                // A TimeoutException under an NpgsqlException is Npgsql's shape for "the command did not
                // finish in time". Checked after the pool-exhaustion fragment because pool exhaustion also
                // surfaces as a timeout, and reporting it as a slow query would send an operator hunting a
                // query when the actual answer is the pool ceiling.
                if (npgsql.InnerException is TimeoutException) return DatabaseFailureKind.CommandTimeout;

                // Npgsql's own judgement — covers socket failures, DNS, and failover windows.
                if (npgsql.IsTransient) return DatabaseFailureKind.Unavailable;
            }

            if (e is TimeoutException) return DatabaseFailureKind.CommandTimeout;
        }

        return DatabaseFailureKind.None;
    }

    /// <summary>The stable <c>error</c> code clients and <c>problem-copy.ts</c> key on.</summary>
    public static string ErrorCode(DatabaseFailureKind kind) => kind switch
    {
        DatabaseFailureKind.PoolExhausted => "database_busy",
        DatabaseFailureKind.ServerConnectionLimit => "database_busy",
        DatabaseFailureKind.CommandTimeout => "database_timeout",
        DatabaseFailureKind.LockTimeout => "resource_busy",
        DatabaseFailureKind.Unavailable => "database_unavailable",
        _ => "internal_error",
    };

    /// <summary>The low-cardinality telemetry tag. Distinct from <see cref="ErrorCode"/> on purpose: the
    /// client is told "busy, try again" for both pool and server exhaustion because its action is the same,
    /// while the operator must be able to tell them apart because theirs is the opposite.</summary>
    public static string TelemetryReason(DatabaseFailureKind kind) => kind switch
    {
        DatabaseFailureKind.PoolExhausted => "pool_exhausted",
        DatabaseFailureKind.ServerConnectionLimit => "server_connection_limit",
        DatabaseFailureKind.CommandTimeout => "command_timeout",
        DatabaseFailureKind.LockTimeout => "lock_timeout",
        DatabaseFailureKind.Unavailable => "unavailable",
        _ => "unknown",
    };
}
