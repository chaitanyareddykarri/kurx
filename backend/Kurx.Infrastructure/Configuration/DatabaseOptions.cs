using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Kurx.Infrastructure.Configuration;

/// <summary>
/// The one place connection-pool and timeout behaviour is decided (DB-2).
///
/// <para><b>Why this type exists.</b> Before it, <c>AddKurxInfrastructure</c> passed the raw connection
/// string straight to <c>UseNpgsql</c>, so every pool and timeout value was an Npgsql library default —
/// most importantly <c>MaxPoolSize=100</c> <i>per process</i>. That number is not wrong in isolation; it
/// is wrong against the deployment this repository actually declares. `infra/terraform/compute.tf` scales
/// the API to <c>api_desired_count * 5</c> = 10 tasks, and `variables.tf` sizes the database
/// <c>db.t4g.medium</c>, which offers roughly 439 connections. Ten tasks × 100 = 1,000 demanded against
/// ~439 available: exhaustion begins at five tasks, which is halfway through the autoscaling range the
/// infrastructure is configured to use. The failure mode is the ugly one — it surfaces under exactly the
/// load that triggered the scale-out, so autoscaling causes the outage it exists to prevent.</para>
///
/// <para><b>Operator overrides win.</b> If a value is already present in the supplied connection string,
/// it is kept. An operator who writes <c>Maximum Pool Size=40</c> into <c>ConnectionStrings__Default</c>
/// means it, and silently overwriting that would make the connection string a lie. These settings are
/// defaults, not a policy the operator cannot escape.</para>
///
/// <para><b>No retry lives here.</b> <c>EnableRetryOnFailure</c> is deliberately absent: it requires every
/// explicit <c>BeginTransactionAsync</c> site (16 of them, including payment capture) to run inside an
/// <c>IExecutionStrategy</c>, and enabling it without that work throws at runtime. That is DB-4.</para>
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Per-process pool ceiling for API request traffic. Sizing arithmetic in
    /// <c>docs/deployment/CONNECTION_POOLING.md</c>.</summary>
    public int MaxPoolSize { get; init; } = 20;

    /// <summary>Separate ceiling for Hangfire. Background work gets its <b>own</b> pool rather than sharing
    /// the API's, for two reasons. First isolation: a job that holds connections cannot starve request
    /// traffic, and a request spike cannot stall the outbox. Second observability: the two pools are
    /// distinguished by <c>Application Name</c>, so <c>pg_stat_activity</c> answers "is it the API or the
    /// jobs?" directly instead of by inference.</summary>
    public int JobsMaxPoolSize { get; init; } = 8;

    /// <summary>Hangfire worker count, set explicitly rather than left at its default of
    /// <c>min(ProcessorCount × 5, 20)</c> — which on a 2-vCPU Fargate task is 10 workers competing for
    /// <see cref="JobsMaxPoolSize"/> connections, plus Hangfire's own polling and heartbeat connections on
    /// top. Five workers against eight connections leaves room for those.</summary>
    public int JobWorkerCount { get; init; } = 5;

    /// <summary>Connections kept warm. Small and non-zero: the first request after an idle period should
    /// not pay TCP + TLS + auth setup, but holding many idle connections per task is what starves a
    /// database when replica count is the thing that varies.</summary>
    public int MinPoolSize { get; init; } = 2;

    /// <summary>Per-command ceiling. A query that has run this long is not going to succeed usefully; it is
    /// holding a pooled connection that other requests need. Long-running background work that legitimately
    /// exceeds this sets its own timeout on its own context.</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;

    /// <summary>How long to wait for a connection — from the pool when it is saturated, or from the server
    /// when it is not. This is the value that converts pool exhaustion from an indefinite hang into a clean,
    /// observable failure, which is the entire point of configuring it.</summary>
    public int ConnectionTimeoutSeconds { get; init; } = 15;

    /// <summary>Seconds an idle connection may sit above <see cref="MinPoolSize"/> before being closed, so a
    /// traffic spike does not permanently pin the pool at its ceiling long after the spike is over.</summary>
    public int ConnectionIdleLifetimeSeconds { get; init; } = 300;

    public static DatabaseOptions FromConfiguration(IConfiguration config)
    {
        var section = config.GetSection(SectionName);
        var defaults = new DatabaseOptions();
        return new DatabaseOptions
        {
            MaxPoolSize = Read(section, nameof(MaxPoolSize), defaults.MaxPoolSize, min: 1),
            JobsMaxPoolSize = Read(section, nameof(JobsMaxPoolSize), defaults.JobsMaxPoolSize, min: 1),
            JobWorkerCount = Read(section, nameof(JobWorkerCount), defaults.JobWorkerCount, min: 1),
            MinPoolSize = Read(section, nameof(MinPoolSize), defaults.MinPoolSize, min: 0),
            CommandTimeoutSeconds = Read(section, nameof(CommandTimeoutSeconds), defaults.CommandTimeoutSeconds, min: 1),
            ConnectionTimeoutSeconds = Read(section, nameof(ConnectionTimeoutSeconds), defaults.ConnectionTimeoutSeconds, min: 1),
            ConnectionIdleLifetimeSeconds = Read(section, nameof(ConnectionIdleLifetimeSeconds), defaults.ConnectionIdleLifetimeSeconds, min: 0),
        };
    }

    /// <summary>Connection string for API request traffic — <c>Application Name=kurx-api</c>.</summary>
    public string ApplyForApi(string connectionString) => Apply(connectionString, MaxPoolSize, "kurx-api");

    /// <summary>Connection string for Hangfire — <c>Application Name=kurx-jobs</c>. The differing
    /// <c>Application Name</c> is what gives background work its own Npgsql pool: Npgsql pools per distinct
    /// connection string, so this is the mechanism, not just a label.</summary>
    public string ApplyForJobs(string connectionString) => Apply(connectionString, JobsMaxPoolSize, "kurx-jobs");

    /// <summary>Applies these settings to <paramref name="connectionString"/> without overriding anything the
    /// operator set explicitly.</summary>
    /// <remarks>
    /// <para>"Was it set explicitly?" is answered against a plain <see cref="DbConnectionStringBuilder"/>,
    /// NOT against <see cref="NpgsqlConnectionStringBuilder"/>. The Npgsql builder exposes every keyword it
    /// knows about — <c>ContainsKey("Maximum Pool Size")</c> is true whether or not the string mentioned it —
    /// so guarding on it suppresses every default and leaves the library values in place. That defect was
    /// written here first and caught by <c>DatabasePoolTests</c>; the base builder keeps only the keys the
    /// string actually supplied, which is the question being asked.</para>
    ///
    /// <para>Comparing against Npgsql's defaults instead would be wrong for a different reason: it would
    /// silently ignore an operator who deliberately configured the same value the library happens to use.</para>
    /// </remarks>
    private string Apply(string connectionString, int maxPoolSize, string applicationName)
    {
        var provided = new DbConnectionStringBuilder { ConnectionString = connectionString };
        // Npgsql accepts several spellings per setting; an operator who wrote any of them meant it.
        bool WasProvided(params string[] keywords) => keywords.Any(provided.ContainsKey);

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!WasProvided("Maximum Pool Size", "MaxPoolSize", "Max Pool Size"))
            builder.MaxPoolSize = maxPoolSize;
        if (!WasProvided("Minimum Pool Size", "MinPoolSize", "Min Pool Size"))
            builder.MinPoolSize = MinPoolSize;
        // Always stamped, even when the operator set one: the two pools MUST differ here or they collapse
        // into a single shared pool and the isolation this type exists to provide silently disappears.
        builder.ApplicationName = applicationName;
        if (!WasProvided("Command Timeout", "CommandTimeout"))
            builder.CommandTimeout = CommandTimeoutSeconds;
        if (!WasProvided("Timeout", "Connection Timeout", "ConnectionTimeout"))
            builder.Timeout = ConnectionTimeoutSeconds;
        if (!WasProvided("Connection Idle Lifetime", "ConnectionIdleLifetime"))
            builder.ConnectionIdleLifetime = ConnectionIdleLifetimeSeconds;

        // MinPoolSize > MaxPoolSize is rejected by Npgsql at connect time with an error that names neither
        // setting. Caught here so a misconfiguration fails at startup with a message that says what to fix.
        if (builder.MinPoolSize > builder.MaxPoolSize)
            throw new InvalidOperationException(
                $"Database:MinPoolSize ({builder.MinPoolSize}) exceeds Database:MaxPoolSize ({builder.MaxPoolSize}). "
                + "The pool cannot hold more warm connections than its ceiling.");

        // Npgsql refuses an idle lifetime shorter than its pruning interval — a connection cannot be
        // scheduled for removal more often than the pruner runs. It enforces this when the data source is
        // BUILT, i.e. at the first connection attempt, which surfaces as a request failing rather than as a
        // deploy failing. Checked here so a misconfiguration stops the process at startup with a message
        // naming both values.
        if (builder.ConnectionIdleLifetime > 0 && builder.ConnectionIdleLifetime < builder.ConnectionPruningInterval)
            throw new InvalidOperationException(
                $"Database:ConnectionIdleLifetimeSeconds ({builder.ConnectionIdleLifetime}) is below Npgsql's "
                + $"connection pruning interval ({builder.ConnectionPruningInterval}s). An idle connection cannot "
                + "be retired more often than the pruner runs — raise the idle lifetime to at least the pruning "
                + "interval.");

        return builder.ConnectionString;
    }

    /// <summary>One line for the startup log, so "what is this instance's pool ceiling?" is answerable from
    /// the log rather than by reconstructing it from environment variables during an incident.</summary>
    public string Describe(string effectiveConnectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(effectiveConnectionString);
        return $"pool {b.MinPoolSize}..{b.MaxPoolSize}, command timeout {b.CommandTimeout}s, "
             + $"connect timeout {b.Timeout}s, idle lifetime {b.ConnectionIdleLifetime}s";
    }

    private static int Read(IConfiguration section, string key, int fallback, int min)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (!int.TryParse(raw, out var value))
            throw new InvalidOperationException($"Database:{key}='{raw}' is not an integer.");
        if (value < min)
            throw new InvalidOperationException($"Database:{key}={value} is below the minimum of {min}.");
        return value;
    }
}
