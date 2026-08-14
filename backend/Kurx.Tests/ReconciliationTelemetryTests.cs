using System.Diagnostics.Metrics;
using Kurx.Infrastructure.Telemetry;

namespace Kurx.Tests;

/// <summary>DB-9 — the alarm-shaping half of reconciliation paging.
///
/// <para><b>What this can and cannot prove.</b> These are <b>configuration tests</b>. They prove the
/// application emits the dimensions the CloudWatch alarms in
/// <c>infra/terraform/monitoring_database.tf</c> are written against, which is the half that lives in this
/// repository. They prove nothing about CloudWatch: that Terraform has never been applied, so no alarm has
/// ever fired and none is runtime-verified. See
/// <c>docs/deployment/DATABASE_RUNBOOKS.md#deployment-checklist</c>.</para>
///
/// <para><b>Why the dimensions are worth a test at all.</b> Every one of these alarms filters on
/// <c>outcome</c>. Drop that filter and the drift alarm reads every run including the clean ones — which are
/// the overwhelming majority — and pages every period on a perfectly healthy platform. The reverse mistake is
/// worse: record a failure as zero drift and an <i>unverified</i> invariant becomes indistinguishable from a
/// verified one, which is the precise shape of the D-240 defect that ran undetected. A tag typo in either
/// direction is silent in production, so it is asserted here.</para></summary>
public class ReconciliationTelemetryTests
{
    /// <summary>Captures every measurement the reconciliation meter emits while <paramref name="act"/> runs.
    ///
    /// <para>Listens to the real static meter rather than an injected abstraction on purpose: the thing under
    /// test is what an exporter would actually see, and an indirection would let the production call sites
    /// emit something different from what is asserted here.</para></summary>
    private static List<(string Instrument, long Value, Dictionary<string, string> Tags)> Capture(Action act)
    {
        var captured = new List<(string, long, Dictionary<string, string>)>();
        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ReconciliationTelemetry.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var dict = new Dictionary<string, string>();
            foreach (var tag in tags)
                dict[tag.Key] = tag.Value?.ToString() ?? "";
            lock (captured) captured.Add((instrument.Name, value, dict));
        });
        listener.Start();

        act();
        listener.RecordObservableInstruments();
        return captured.Select(c => (c.Item1, c.Item2, c.Item3)).ToList();
    }

    private static bool HasRun(
        IEnumerable<(string Instrument, long Value, Dictionary<string, string> Tags)> measurements,
        string type, string outcome) =>
        measurements.Any(m =>
            m.Instrument == "kurx.reconciliation.runs" &&
            m.Tags.GetValueOrDefault("type") == type &&
            m.Tags.GetValueOrDefault("outcome") == outcome);

    // ── A clean run must never page ──────────────────────────────────────────

    /// <summary>The one that keeps the alarm usable. A clean run is by far the most common outcome, so if it
    /// shared a dimension with drift the drift alarm would breach on every evaluation period forever, and an
    /// alarm that always fires is one nobody reads.</summary>
    [Fact]
    public void A_clean_run_records_clean_and_nothing_else()
    {
        var measurements = Capture(() => ReconciliationTelemetry.Record("wallet", driftCount: 0));

        Assert.True(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeClean));
        Assert.False(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeDrift));
        Assert.False(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeFailed));
    }

    /// <summary>A clean run publishes no entity count at all — not a zero. The alarm on
    /// <c>drifting_entities</c> would otherwise have a continuous stream of zero datapoints to average, and
    /// `treat_missing_data = notBreaching` stops meaning what it says.</summary>
    [Fact]
    public void A_clean_run_publishes_no_drifting_entity_count()
    {
        var measurements = Capture(() => ReconciliationTelemetry.Record("inventory", driftCount: 0));

        Assert.DoesNotContain(measurements, m => m.Instrument == "kurx.reconciliation.drifting_entities");
    }

    // ── Drift must page, and carry how much ──────────────────────────────────

    [Fact]
    public void Drift_records_the_drift_outcome_and_the_entity_count()
    {
        var measurements = Capture(() => ReconciliationTelemetry.Record("wallet", driftCount: 4));

        Assert.True(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeDrift));
        Assert.False(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeClean));

        var entities = Assert.Single(measurements,
            m => m.Instrument == "kurx.reconciliation.drifting_entities");
        Assert.Equal(4, entities.Value);
        Assert.Equal("wallet", entities.Tags["type"]);
    }

    /// <summary>"One run found drift" and "that run found 4,000 drifting organizations" are the same value on
    /// a run counter and wildly different incidents, which is why the entity count is a separate
    /// instrument.</summary>
    [Fact]
    public void The_entity_count_reflects_the_magnitude_not_just_the_occurrence()
    {
        var measurements = Capture(() => ReconciliationTelemetry.Record("registration", driftCount: 4000));

        Assert.Equal(4000, measurements
            .Single(m => m.Instrument == "kurx.reconciliation.drifting_entities").Value);
        Assert.Equal(1, measurements
            .Single(m => m.Instrument == "kurx.reconciliation.runs").Value);
    }

    // ── Failure must page, and must not read as clean ────────────────────────

    /// <summary>The subtle one. A reconciliation that threw proved nothing, and recording it as a clean run
    /// would turn an unverified invariant into a green dashboard — strictly worse than no reconciliation at
    /// all, because it looks like coverage.</summary>
    [Fact]
    public void A_failure_records_failed_and_is_never_mistaken_for_clean()
    {
        var measurements = Capture(() => ReconciliationTelemetry.RecordFailure("wallet"));

        Assert.True(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeFailed));
        Assert.False(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeClean));
        Assert.False(HasRun(measurements, "wallet", ReconciliationTelemetry.OutcomeDrift));
    }

    /// <summary>A failure has no entity count — nothing was counted, which is the whole point.</summary>
    [Fact]
    public void A_failure_publishes_no_drifting_entity_count()
    {
        var measurements = Capture(() => ReconciliationTelemetry.RecordFailure("inventory"));

        Assert.DoesNotContain(measurements, m => m.Instrument == "kurx.reconciliation.drifting_entities");
    }

    // ── The dimensions the alarms are written against ────────────────────────

    /// <summary>Every alarm in <c>monitoring_database.tf</c> uses <c>for_each</c> over exactly these three
    /// type values. A rename on either side leaves an alarm silently watching a dimension nothing emits —
    /// which is not an error anywhere, just permanent INSUFFICIENT_DATA that looks like health.</summary>
    [Theory]
    [InlineData("wallet")]
    [InlineData("inventory")]
    [InlineData("registration")]
    public void Each_reconciliation_type_emits_under_the_name_its_alarm_filters_on(string type)
    {
        var measurements = Capture(() => ReconciliationTelemetry.Record(type, driftCount: 1));

        Assert.True(HasRun(measurements, type, ReconciliationTelemetry.OutcomeDrift));
    }

    /// <summary>The jobs' own constants are what the production call sites pass, so they are what must match
    /// the Terraform. Asserted against literals rather than against each other — comparing a constant to
    /// itself would pass no matter what either said.</summary>
    [Fact]
    public void The_jobs_declare_the_type_names_the_terraform_expects()
    {
        Assert.Equal("wallet", Kurx.Infrastructure.Jobs.WalletReconciliationJob.ReconciliationType);
        Assert.Equal("inventory", Kurx.Infrastructure.Jobs.InventoryReconciliationJob.ReconciliationType);
        Assert.Equal("registration", Kurx.Infrastructure.Jobs.RegistrationReconciliationJob.ReconciliationType);
    }

    /// <summary>Namespace separation (DB-9 §10) is not cosmetic: the exporter maps a meter to a CloudWatch
    /// namespace, and every database and reconciliation alarm names one. Folding these into
    /// <c>Kurx.Auth</c> would silently move every metric out from under its alarm.</summary>
    [Fact]
    public void The_three_metric_families_stay_in_separate_namespaces()
    {
        Assert.Equal("Kurx.Reconciliation", ReconciliationTelemetry.MeterName);
        Assert.Equal("Kurx.Database", DatabaseTelemetry.MeterName);
        Assert.Equal("Kurx.Auth", KurxTelemetry.MeterName);
    }
}
