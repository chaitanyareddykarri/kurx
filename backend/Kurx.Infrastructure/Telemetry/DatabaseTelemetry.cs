using System.Diagnostics.Metrics;

namespace Kurx.Infrastructure.Telemetry;

/// <summary>PostgreSQL **engine** metrics (DB-9), kept in their own meter — and therefore their own
/// CloudWatch namespace — separate from <see cref="KurxTelemetry"/>'s application/security metrics and from
/// <see cref="ReconciliationTelemetry"/>'s business-correctness ones.
///
/// <para><b>Why the application reads these at all.</b> RDS publishes <c>DatabaseConnections</c>,
/// <c>CPUUtilization</c> and <c>FreeStorageSpace</c> to CloudWatch, and those already have alarms. It does
/// <b>not</b> publish deadlocks or table bloat for RDS PostgreSQL — the <c>Deadlocks</c> CloudWatch metric
/// exists only for <i>Aurora</i> PostgreSQL, and bloat is not an AWS concept at all. Both facts live solely
/// in PostgreSQL's own catalogs (<c>pg_stat_database</c>, <c>pg_stat_user_tables</c>). Sampling them here and
/// exporting through the OTLP pipeline the platform already runs is what makes them alarmable without
/// inventing a metric name AWS does not publish, and without standing up a second monitoring stack.</para></summary>
public static class DatabaseTelemetry
{
    public const string MeterName = "Kurx.Database";

    public static readonly Meter Meter = new(MeterName, KurxTelemetry.Version);

    /// <summary>Deadlocks observed since the previous probe.
    ///
    /// <para>A <b>counter of deltas</b>, not the catalog's own cumulative figure. <c>pg_stat_database.deadlocks</c>
    /// only ever rises (until a stats reset), so exporting it raw would make every alarm evaluate "has this
    /// database ever deadlocked" instead of "is it deadlocking now". The delta is computed in
    /// <c>DatabaseHealthProbeJob</c>, which is Hangfire-recurring and therefore already single-run across
    /// replicas — without that, every replica would probe the same shared counter and multiply it.</para></summary>
    public static readonly Counter<long> Deadlocks =
        Meter.CreateCounter<long>("kurx.db.deadlocks", unit: "{deadlock}",
            description: "PostgreSQL deadlocks detected since the previous probe.");

    /// <summary><b>Client</b> backends currently connected to the database, server-side.
    ///
    /// <para>Distinct from Npgsql's pool gauges (DB-2), which describe what <i>this process</i> holds. This is
    /// the total across every API task, every Hangfire worker and every human with psql open — the number that
    /// is actually measured against <c>max_connections</c>, and the only one that can answer "are we near the
    /// server's ceiling" rather than "are we near ours".</para>
    ///
    /// <para>Client backends only. PostgreSQL's own background processes appear in <c>pg_stat_activity</c> but
    /// are not charged against <c>max_connections</c>, so counting them would inflate this by a constant
    /// handful and make the comparison it exists for slightly false.</para></summary>
    public static readonly Gauge<long> ConnectionsUsed =
        Meter.CreateGauge<long>("kurx.db.connections.used", unit: "{connection}",
            description: "Backends connected to PostgreSQL, server-wide.");

    /// <summary>The server's configured <c>max_connections</c>. Exported so a dashboard and an alarm can be
    /// written against the ratio without hard-coding an instance-class-specific number that silently becomes
    /// wrong the moment the instance is resized.</summary>
    public static readonly Gauge<long> ConnectionsMax =
        Meter.CreateGauge<long>("kurx.db.connections.max", unit: "{connection}",
            description: "PostgreSQL max_connections.");

    /// <summary>Connections used as a percentage of <c>max_connections</c>. Derived here rather than in the
    /// alarm because CloudWatch metric math across two custom metrics is materially harder to review than a
    /// threshold on one number, and this is the number an operator actually reasons about.</summary>
    public static readonly Gauge<double> ConnectionUtilization =
        Meter.CreateGauge<double>("kurx.db.connections.utilization", unit: "%",
            description: "Connections used as a percentage of max_connections.");

    /// <summary>Dead tuples as a percentage of live tuples, per table.
    ///
    /// <para><b>This is bloat; table size is not.</b> A large table that is entirely live data is healthy, and
    /// alarming on bytes would page for ordinary growth. What matters is dead storage relative to useful
    /// storage — that is what autovacuum reclaims and what indicates it is falling behind.</para></summary>
    public static readonly Gauge<double> DeadTupleRatio =
        Meter.CreateGauge<double>("kurx.db.dead_tuple_ratio", unit: "%",
            description: "Dead tuples as a percentage of live tuples, per table.");

    /// <summary>Hours since a table was last vacuumed by either autovacuum or a manual VACUUM.
    ///
    /// <para>Paired with <see cref="DeadTupleRatio"/> on purpose: a high ratio with a recent vacuum is a table
    /// churning faster than its threshold and is a tuning question, while a high ratio with no recent vacuum
    /// means autovacuum is not running on it at all — a different problem with a different fix.</para></summary>
    public static readonly Gauge<double> HoursSinceVacuum =
        Meter.CreateGauge<double>("kurx.db.hours_since_vacuum", unit: "h",
            description: "Hours since the table was last vacuumed (autovacuum or manual).");

    /// <summary>Total on-disk bytes for a table including its indexes and TOAST. Context for the two gauges
    /// above — never an alarm on its own, for the reason given on <see cref="DeadTupleRatio"/>.</summary>
    public static readonly Gauge<long> TableBytes =
        Meter.CreateGauge<long>("kurx.db.table_bytes", unit: "By",
            description: "Total relation size including indexes and TOAST, per table.");
}

/// <summary>**Business-correctness** metrics (DB-9) — its own meter, and therefore its own CloudWatch
/// namespace, because these say nothing about whether the database or the application is healthy. Both can be
/// entirely well while money is wrong, which is exactly the situation D-240 shipped and nothing detected.
///
/// <para><b>Three outcomes, deliberately one counter.</b> <c>outcome</c> is a dimension rather than three
/// separate metrics, matching <c>kurx.auth.attempts</c>'s existing <c>outcome</c> tag so the alarms read the
/// same way the platform's existing ones do. The three are operationally distinct and must stay
/// distinguishable:</para>
/// <list type="bullet">
///   <item><c>clean</c> — the job ran and found nothing. **Must never page**, and it is by far the most common
///     value, so anything that alarms on the metric without pinning <c>outcome</c> would page continuously.</item>
///   <item><c>drift</c> — the job ran and the invariant does not hold. Pages: this is money or inventory
///     disagreeing with its own ledger.</item>
///   <item><c>failed</c> — the job itself threw. Pages differently: the invariant is now <i>unverified</i>,
///     which is not the same as violated and must not be read as "clean".</item>
/// </list></summary>
public static class ReconciliationTelemetry
{
    public const string MeterName = "Kurx.Reconciliation";

    public static readonly Meter Meter = new(MeterName, KurxTelemetry.Version);

    public const string OutcomeClean = "clean";
    public const string OutcomeDrift = "drift";
    public const string OutcomeFailed = "failed";

    /// <summary>One increment per reconciliation run, tagged with its type and outcome.</summary>
    public static readonly Counter<long> Runs =
        Meter.CreateCounter<long>("kurx.reconciliation.runs", unit: "{run}",
            description: "Reconciliation job executions by type and outcome.");

    /// <summary>How many entities were found drifting — organizations, pools or events depending on the type.
    ///
    /// <para>Separate from <see cref="Runs"/> because "one run found drift" and "that run found 4,000 drifting
    /// organizations" are the same value on a run counter and wildly different incidents. Deliberately a count
    /// of entities and <b>not</b> an amount: a paise figure on a metric exported to a third-party backend is
    /// financial detail in a place that does not need it, and the structured log already carries it for an
    /// operator who has authenticated to read it.</para></summary>
    public static readonly Counter<long> DriftingEntities =
        Meter.CreateCounter<long>("kurx.reconciliation.drifting_entities", unit: "{entity}",
            description: "Entities found drifting by a reconciliation run, by type.");

    /// <summary>Records a run's outcome. <paramref name="driftCount"/> of zero records a clean run.</summary>
    public static void Record(string type, int driftCount)
    {
        var outcome = driftCount == 0 ? OutcomeClean : OutcomeDrift;
        Runs.Add(1,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("outcome", outcome));
        if (driftCount > 0)
            DriftingEntities.Add(driftCount, new KeyValuePair<string, object?>("type", type));
    }

    /// <summary>Records that the reconciliation itself could not complete. Kept separate from
    /// <see cref="Record"/> so a caller cannot express a failure as "zero drift", which is the one mistake
    /// that would turn an unverified invariant into a green dashboard.</summary>
    public static void RecordFailure(string type) =>
        Runs.Add(1,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("outcome", OutcomeFailed));
}
