using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Telemetry;

/// <summary>The platform's single <see cref="ActivitySource"/> and <see cref="Meter"/> (AM10, D-100).
///
/// <para>Names are the contract the collector and dashboards are configured against, so they live in
/// one place rather than being re-typed at each call site — a typo'd source name emits spans that
/// silently never reach the backend.</para></summary>
public static class KurxTelemetry
{
    public const string ActivitySourceName = "Kurx.Auth";
    public const string MeterName = "Kurx.Auth";

    /// <summary>Bumped only on a breaking change to span/metric shape, so a dashboard can pin it.</summary>
    public const string Version = "1.0.0";

    public static readonly ActivitySource Source = new(ActivitySourceName, Version);
    public static readonly Meter Meter = new(MeterName, Version);

    // Counters, not gauges: these are events over time. Dashboards derive rates; alerting derives
    // ratios (e.g. denied/total) which is what actually indicates an attack in progress.
    public static readonly Counter<long> AuthAttempts =
        Meter.CreateCounter<long>("kurx.auth.attempts", unit: "{attempt}",
            description: "Authentication attempts by rail and outcome.");

    public static readonly Counter<long> SecurityEvents =
        Meter.CreateCounter<long>("kurx.auth.security_events", unit: "{event}",
            description: "Security-relevant auth events by type and severity.");

    public static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>("kurx.auth.operation.duration", unit: "ms",
            description: "Duration of authentication operations.");

    /// <summary>Database failures, classified (DB-2).
    ///
    /// <para>The <c>reason</c> tag is the whole point. Npgsql's raw metrics tell you a connection attempt
    /// failed; they do not tell you <i>which</i> of four operationally distinct situations you are in, and
    /// those four have different responses:</para>
    /// <list type="bullet">
    ///   <item><c>pool_exhausted</c> — OUR ceiling was hit. The database is fine. Raise
    ///     <c>Database__MaxPoolSize</c>, or find what is holding connections.</item>
    ///   <item><c>server_connection_limit</c> — POSTGRES refused (53300). Every replica together exceeds
    ///     the server. Lower the per-task ceiling or add a pooler; raising ours makes it worse.</item>
    ///   <item><c>command_timeout</c> — a query outlived <c>CommandTimeoutSeconds</c>. A slow query, not a
    ///     capacity problem.</item>
    ///   <item><c>unavailable</c> — the server could not be reached at all.</item>
    /// </list>
    /// <para>Without the distinction, the natural reaction to every one of them is "raise the pool size",
    /// which is the correct fix for exactly one and actively harmful for another.</para></summary>
    public static readonly Counter<long> DatabaseFailures =
        Meter.CreateCounter<long>("kurx.db.failures", unit: "{failure}",
            description: "Database connection and command failures by classified reason.");

    /// <summary>The Npgsql-owned meter carrying pool gauges (connection usage, pending requests, timeouts)
    /// and command duration. Named here rather than typed at the registration site for the same reason
    /// <see cref="MeterName"/> is: a typo produces silence, not an error.</summary>
    public const string NpgsqlMeterName = "Npgsql";

    public static void RecordDatabaseFailure(string reason) =>
        DatabaseFailures.Add(1, new KeyValuePair<string, object?>("reason", reason));
}

/// <summary>OpenTelemetry-backed <see cref="IAuthTelemetry"/>.
///
/// <para>Every tag on these spans and metrics is deliberately low-cardinality and non-sensitive.
/// A phone number, OTP, nonce or token in a span attribute would be exported to a third-party
/// backend and readable by anyone with dashboard access — telemetry is not a safe place to debug
/// credentials.</para></summary>
public class AuthTelemetry : IAuthTelemetry
{
    public IDisposable StartOperation(string operation, Guid? userId = null)
    {
        var activity = KurxTelemetry.Source.StartActivity($"auth.{operation}", ActivityKind.Internal);
        // A GUID is the least-identifying handle available; never a phone or email.
        if (activity is not null && userId is not null)
            activity.SetTag("kurx.user_id", userId.Value.ToString());

        return new OperationScope(activity, operation);
    }

    public void RecordAuthAttempt(string method, string outcome) =>
        KurxTelemetry.AuthAttempts.Add(1,
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("outcome", outcome));

    public void RecordSecurityEvent(string type, string severity)
    {
        KurxTelemetry.SecurityEvents.Add(1,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("severity", severity));

        // Also attach to the active span so a trace shows *why* a request was refused, not just that
        // it was — the difference between a usable and a useless incident timeline.
        Activity.Current?.AddEvent(new ActivityEvent($"security.{type}",
            tags: [new KeyValuePair<string, object?>("severity", severity)]));
    }

    public void RecordFailure(string reason)
    {
        var activity = Activity.Current;
        if (activity is null) return;
        activity.SetStatus(ActivityStatusCode.Error, reason);
        // The error CODE only. An exception message carries internals and varies unboundedly, which
        // both leaks and destroys the value of grouping.
        activity.SetTag("kurx.failure_reason", reason);
    }

    /// <summary>Ends the span and records its duration, so latency is available as a metric even
    /// when trace sampling drops the span itself.</summary>
    private sealed class OperationScope(Activity? activity, string operation) : IDisposable
    {
        private readonly long _start = Stopwatch.GetTimestamp();

        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
            KurxTelemetry.OperationDuration.Record(elapsed,
                new KeyValuePair<string, object?>("operation", operation));
            activity?.Dispose();
        }
    }
}
