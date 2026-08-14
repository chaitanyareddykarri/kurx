using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Telemetry;

namespace Kurx.Tests;

/// <summary>Proves telemetry is actually <b>emitted</b>, not merely configured (AM10, D-100).
///
/// <para>Wiring an exporter and asserting the app still starts proves nothing — the failure mode
/// that matters is silent: a mis-named ActivitySource or an unregistered Meter produces an app that
/// runs perfectly and sends no data, which is only discovered during an incident. These tests
/// subscribe to the real .NET diagnostics plumbing and assert the signals arrive.</para>
///
/// Pure in-process: no collector, no backend, no network.</summary>
public class TelemetryTests
{
    private static AuthTelemetry Telemetry() => new();

    /// <summary>Listens to the platform's ActivitySource exactly as an exporter would.</summary>
    private static (ActivityListener Listener, List<Activity> Captured) ListenForSpans()
    {
        var captured = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == KurxTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = captured.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, captured);
    }

    private static (MeterListener Listener, List<(string Name, long Value, Dictionary<string, object?> Tags)> Captured)
        ListenForCounters()
    {
        var captured = new List<(string, long, Dictionary<string, object?>)>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KurxTelemetry.MeterName) l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var tag in tags) dict[tag.Key] = tag.Value;
            captured.Add((instrument.Name, value, dict));
        });
        listener.Start();
        return (listener, captured);
    }

    [Fact]
    public void An_auth_operation_emits_a_span_carrying_the_user_id()
    {
        var (listener, spans) = ListenForSpans();
        using (listener)
        {
            var userId = Guid.NewGuid();
            using (Telemetry().StartOperation("login.start", userId)) { }

            var span = Assert.Single(spans);
            Assert.Equal("auth.login.start", span.OperationName);
            Assert.Equal(userId.ToString(), span.GetTagItem("kurx.user_id"));
        }
    }

    [Fact]
    public void A_failure_marks_the_span_with_an_error_code_not_a_message()
    {
        var (listener, spans) = ListenForSpans();
        using (listener)
        {
            var telemetry = Telemetry();
            using (telemetry.StartOperation("login.approve"))
            {
                telemetry.RecordFailure("device_not_trusted");
            }

            var span = Assert.Single(spans);
            Assert.Equal(ActivityStatusCode.Error, span.Status);
            // A stable code, so failures group. An exception message would vary unboundedly and
            // could carry internals into a third-party backend.
            Assert.Equal("device_not_trusted", span.GetTagItem("kurx.failure_reason"));
        }
    }

    [Fact]
    public void A_security_event_is_both_a_metric_and_a_span_event()
    {
        var (spanListener, spans) = ListenForSpans();
        var (meterListener, measurements) = ListenForCounters();
        using (spanListener)
        using (meterListener)
        {
            var telemetry = Telemetry();
            using (telemetry.StartOperation("refresh"))
            {
                telemetry.RecordSecurityEvent("refresh.reuse_detected", "critical");
            }
            meterListener.RecordObservableInstruments();

            // The metric is what an alert fires on...
            var metric = Assert.Single(measurements, m => m.Name == "kurx.auth.security_events");
            Assert.Equal(1, metric.Value);
            Assert.Equal("refresh.reuse_detected", metric.Tags["type"]);
            Assert.Equal("critical", metric.Tags["severity"]);

            // ...and the span event is what makes an incident timeline readable.
            var span = Assert.Single(spans);
            Assert.Contains(span.Events, e => e.Name == "security.refresh.reuse_detected");
        }
    }

    [Fact]
    public void Auth_attempts_are_counted_by_rail_and_outcome()
    {
        var (listener, measurements) = ListenForCounters();
        using (listener)
        {
            var telemetry = Telemetry();
            telemetry.RecordAuthAttempt("device", "success");
            telemetry.RecordAuthAttempt("device", "denied");
            telemetry.RecordAuthAttempt("passkey", "success");

            var attempts = measurements.Where(m => m.Name == "kurx.auth.attempts").ToList();
            Assert.Equal(3, attempts.Count);

            // Splitting by rail AND outcome is what makes "passkey logins are failing but device
            // logins are fine" visible — a single success counter would hide it.
            Assert.Contains(attempts, m => (string?)m.Tags["method"] == "device" && (string?)m.Tags["outcome"] == "denied");
            Assert.Contains(attempts, m => (string?)m.Tags["method"] == "passkey" && (string?)m.Tags["outcome"] == "success");
        }
    }

    [Fact]
    public void Operation_duration_is_recorded_even_when_no_span_is_sampled()
    {
        // Latency must survive trace sampling: at 1% sampling the span is usually dropped, so if
        // duration lived only on the span, p99 would be built from 1% of requests.
        var captured = new List<(string Instrument, double Value)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KurxTelemetry.MeterName) l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
            captured.Add((instrument.Name, value)));
        listener.Start();

        // No ActivityListener registered => no span is created at all.
        using (Telemetry().StartOperation("passkey.register")) { }

        Assert.Contains(captured, m => m.Instrument == "kurx.auth.operation.duration");
    }

    [Fact]
    public void Telemetry_is_safe_when_nothing_is_listening()
    {
        // Production may run with tracing disabled. Every call must be a no-op, never a null-deref:
        // telemetry that can crash the request it measures is worse than no telemetry.
        var telemetry = Telemetry();
        var exception = Record.Exception(() =>
        {
            using (telemetry.StartOperation("login.start", Guid.NewGuid()))
            {
                telemetry.RecordFailure("some_code");
                telemetry.RecordSecurityEvent("x", "info");
            }
            telemetry.RecordAuthAttempt("otp", "success");
        });

        Assert.Null(exception);
    }

    [Fact]
    public void The_telemetry_contract_is_the_one_the_collector_is_configured_against()
    {
        // These names are the contract dashboards and the collector pipeline are wired to. Renaming
        // one silently stops data flowing, so a change here should be a deliberate, reviewed edit.
        Assert.Equal("Kurx.Auth", KurxTelemetry.ActivitySourceName);
        Assert.Equal("Kurx.Auth", KurxTelemetry.MeterName);
        Assert.Equal("kurx.auth.attempts", KurxTelemetry.AuthAttempts.Name);
        Assert.Equal("kurx.auth.security_events", KurxTelemetry.SecurityEvents.Name);
        Assert.Equal("kurx.auth.operation.duration", KurxTelemetry.OperationDuration.Name);
    }

    [Fact]
    public void The_telemetry_interface_exposes_no_way_to_pass_a_secret()
    {
        // Spans are exported to a third-party backend and readable by anyone with dashboard access.
        // The contract deliberately accepts only low-cardinality identifiers and outcomes — this
        // pins that no overload ever grows a free-form payload parameter.
        var methods = typeof(IAuthTelemetry).GetMethods();
        var parameterNames = methods.SelectMany(m => m.GetParameters()).Select(p => p.Name!.ToLowerInvariant());

        foreach (var forbidden in new[] { "token", "secret", "nonce", "phone", "email", "code", "signature", "payload" })
        {
            Assert.DoesNotContain(parameterNames, name => name.Contains(forbidden));
        }
    }
}
