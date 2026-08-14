using Serilog.Context;

namespace Kurx.Api.Middleware;

/// <summary>Assigns/propagates a correlation id so a single request can be traced across logs and error responses.
/// Pushes it into Serilog's LogContext (requires Enrich.FromLogContext() in the logger config) for the whole
/// downstream pipeline, so every log line written while handling this request — not just the completion
/// summary — carries the same correlation id.</summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
