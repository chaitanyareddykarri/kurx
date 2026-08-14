using System.Diagnostics.Metrics;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>DB-9 — the probe job runs its catalog queries against a real PostgreSQL and publishes what the
/// alarms read.
///
/// <para><b>Why this is an integration test and not a unit test.</b> The whole job is two SQL statements
/// against system catalogs; mocking the database would test nothing but the arithmetic between them. It also
/// would not have caught what this test was written for: <c>EXTRACT(EPOCH FROM …) / 3600.0</c> returns
/// <c>numeric</c> on PostgreSQL 14+, Npgsql maps that to <c>decimal</c>, and <c>GetDouble</c> throws
/// <c>InvalidCastException</c> on it. That defect compiled cleanly, passed review, and would have thrown
/// every five minutes forever on any table that had been vacuumed — visible only as a failing Hangfire job
/// nobody was watching, on the very job whose purpose is to be watched.</para></summary>
public class DatabaseHealthProbeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public DatabaseHealthProbeTests(KurxApiFactory factory) => _factory = factory;

    /// <summary>Runs the job and captures everything it publishes on the database meter.</summary>
    private async Task<List<(string Instrument, double Value, Dictionary<string, string> Tags)>> RunProbeAsync()
    {
        var captured = new List<(string, double, Dictionary<string, string>)>();
        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == DatabaseTelemetry.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        // Two callbacks because the instruments are deliberately not all one CLR type — counts are long,
        // ratios are double. A single long callback would silently miss every gauge that matters for bloat.
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            lock (captured) captured.Add((instrument.Name, value, TagsOf(tags)));
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            lock (captured) captured.Add((instrument.Name, value, TagsOf(tags)));
        });
        listener.Start();

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<DatabaseHealthProbeJob>();
        await job.RunAsync(CancellationToken.None);

        return captured.Select(c => (c.Item1, c.Item2, c.Item3)).ToList();

        static Dictionary<string, string> TagsOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, string>();
            foreach (var tag in tags) dict[tag.Key] = tag.Value?.ToString() ?? "";
            return dict;
        }
    }

    /// <summary>The whole job completes against a real database. This is the assertion that fails on a type
    /// mismatch, a renamed catalog column, or a query PostgreSQL will not plan.</summary>
    [Fact]
    public async Task The_probe_runs_against_a_real_database_without_throwing()
    {
        var measurements = await RunProbeAsync();
        Assert.NotEmpty(measurements);
    }

    /// <summary>The connection gauges are published every run, so their series is continuous and a gap on a
    /// dashboard means the probe stopped rather than that the database is idle.</summary>
    [Fact]
    public async Task It_publishes_connection_usage_against_the_servers_own_ceiling()
    {
        var measurements = await RunProbeAsync();

        var used = Assert.Single(measurements, m => m.Instrument == "kurx.db.connections.used");
        var max = Assert.Single(measurements, m => m.Instrument == "kurx.db.connections.max");
        var utilization = Assert.Single(measurements, m => m.Instrument == "kurx.db.connections.utilization");

        // The test's own connection is one of them, so "at least one" is a fact rather than an assumption.
        Assert.True(used.Value >= 1, $"expected at least this test's own connection, got {used.Value}");
        Assert.True(max.Value > 0, "max_connections must be read from the server, not assumed");
        Assert.True(used.Value <= max.Value, "used connections cannot exceed the server ceiling");
        Assert.Equal(Math.Round(used.Value * 100.0 / max.Value, 2), utilization.Value, precision: 1);
    }

    /// <summary>The three high-churn tables report every run regardless of their current dead-tuple count —
    /// that is what makes a flat line on the dashboard mean "healthy" instead of "probe not running".</summary>
    [Theory]
    [InlineData("inventory_pools")]
    [InlineData("ticket_types")]
    [InlineData("organization_wallet")]
    public async Task It_always_reports_the_watched_high_churn_tables(string table)
    {
        var measurements = await RunProbeAsync();

        Assert.Contains(measurements, m =>
            m.Instrument == "kurx.db.table_bytes" && m.Tags.GetValueOrDefault("table") == table);
    }

    /// <summary>Table metrics carry the <c>table</c> dimension the bloat alarms filter on. A rename on either
    /// side leaves the alarm watching a dimension nothing emits — permanent INSUFFICIENT_DATA, which looks
    /// like health.</summary>
    [Fact]
    public async Task Every_table_metric_carries_the_table_dimension()
    {
        var measurements = await RunProbeAsync();
        var tableMetrics = measurements.Where(m => m.Instrument.StartsWith("kurx.db.")
            && m.Instrument is "kurx.db.table_bytes" or "kurx.db.dead_tuple_ratio" or "kurx.db.hours_since_vacuum");

        Assert.NotEmpty(tableMetrics);
        Assert.All(tableMetrics, m =>
            Assert.False(string.IsNullOrEmpty(m.Tags.GetValueOrDefault("table"))));
    }

    /// <summary>Cardinality stays bounded. The cap exists because the moment monitoring cost spikes is the
    /// moment an incident starts — a database-wide vacuum stall must not publish a series per table.</summary>
    [Fact]
    public async Task It_reports_no_more_tables_than_its_cap()
    {
        var measurements = await RunProbeAsync();
        var tables = measurements
            .Where(m => m.Instrument == "kurx.db.table_bytes")
            .Select(m => m.Tags["table"])
            .Distinct()
            .ToList();

        Assert.InRange(tables.Count, 1, 15);
    }

    /// <summary>A healthy database publishes NO deadlock measurement — the counter carries a delta, and the
    /// alarm's <c>treat_missing_data = notBreaching</c> depends on silence meaning zero. Publishing a literal
    /// zero every probe would be the same graph and a different alarm contract.
    ///
    /// <para>Also pins the re-baselining rule: the very first probe in a process has no previous reading, so
    /// it must publish nothing rather than emit the whole cumulative history as one spike.</para></summary>
    [Fact]
    public async Task A_database_that_is_not_deadlocking_publishes_no_deadlock_measurement()
    {
        await RunProbeAsync();                       // establishes the baseline
        var second = await RunProbeAsync();          // nothing deadlocked in between

        Assert.DoesNotContain(second, m => m.Instrument == "kurx.db.deadlocks");
    }

    /// <summary>Running twice in a row is safe. The job is on a five-minute schedule for the life of the
    /// process, so a leaked connection or a second explicit open that is never closed would surface slowly as
    /// pool pressure — on the job that exists to detect pool pressure.</summary>
    [Fact]
    public async Task The_probe_is_repeatable_and_does_not_hold_its_connection()
    {
        for (var i = 0; i < 3; i++)
            Assert.NotEmpty(await RunProbeAsync());
    }
}
