using Kurx.Api.Middleware;
using Kurx.Infrastructure.Telemetry;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Kurx.Api.Observability;

/// <summary>OpenTelemetry pipeline (AM10, D-100): traces, metrics and runtime instrumentation over
/// a single shared resource.
///
/// <para><b>Exporter selection is explicit, never guessed.</b> With `OTEL_EXPORTER_OTLP_ENDPOINT`
/// set, telemetry goes to that collector (CloudWatch/X-Ray via the ADOT collector in production);
/// without it, nothing is exported. There is deliberately no "helpful" default endpoint — a
/// silently mis-targeted exporter looks identical to a working one until an incident, when the data
/// isn't there.</para></summary>
public static class TelemetryRegistration
{
    public static IServiceCollection AddKurxTelemetry(this IServiceCollection services,
        IConfiguration config, IHostEnvironment env)
    {
        var otlpEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var serviceName = config["OTEL_SERVICE_NAME"] ?? "kurx-api";

        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: KurxTelemetry.Version)
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment", env.EnvironmentName),
            ]);

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resource)
                    .AddSource(KurxTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        // Health checks and JWKS are polled constantly by load balancers and
                        // clients; tracing them buries real traffic in noise and costs money on a
                        // per-span-billed backend.
                        o.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/health") &&
                            !ctx.Request.Path.StartsWithSegments("/.well-known");

                        o.EnrichWithHttpRequest = (activity, request) =>
                        {
                            // Tie the trace to the correlation id already threaded through logs and
                            // ProblemDetails responses, so a user-reported id reaches the trace.
                            if (request.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var id))
                                activity.SetTag("kurx.correlation_id", id?.ToString());
                        };
                        o.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    // Npgsql emits spans for every command, which is what turns "the request was
                    // slow" into "this query was slow".
                    .AddNpgsql();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resource)
                    .AddMeter(KurxTelemetry.MeterName)
                    // DB-2: Npgsql's own pool instrumentation — connection usage (used vs idle), pending
                    // requests waiting for a connection, pool timeouts, and command duration. Pending
                    // requests is the leading indicator: it rises before anything fails, which is the
                    // difference between resizing the pool on a graph and resizing it during an incident.
                    // Paired with kurx.db.failures below, which classifies WHY a failure happened —
                    // Npgsql reports that one occurred, not whether the ceiling that was hit was ours or
                    // PostgreSQL's, and those two have opposite remedies.
                    .AddMeter(KurxTelemetry.NpgsqlMeterName)
                    // DB-9. Two further meters, deliberately NOT folded into Kurx.Auth: the exporter maps a
                    // meter to a CloudWatch namespace, so keeping them separate is what lets an operator
                    // reason about database-engine health, application/security behaviour and financial
                    // correctness independently. They fail independently too — the database can be perfectly
                    // healthy while a wallet disagrees with its ledger, which is exactly the D-240 situation
                    // no signal existed for.
                    .AddMeter(DatabaseTelemetry.MeterName)          // engine: deadlocks, connections, bloat
                    .AddMeter(ReconciliationTelemetry.MeterName)    // business correctness: drift, failures
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
