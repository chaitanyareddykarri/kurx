using Kurx.Application.Abstractions;

namespace Kurx.Api.Middleware;

/// <summary>
/// D-102 (M3a): exposes the correlation id that <see cref="CorrelationIdMiddleware"/> stamped on the
/// request, so audit rows written deep in Infrastructure can be joined to the logs and traces for the
/// same request. Falls back to the framework's trace identifier when the middleware hasn't run (e.g. a
/// request short-circuited earlier in the pipeline).
/// </summary>
public class HttpContextCorrelationAccessor(IHttpContextAccessor accessor) : ICorrelationAccessor
{
    public string? CorrelationId
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null) return null;
            return context.Items[CorrelationIdMiddleware.ItemKey] as string ?? context.TraceIdentifier;
        }
    }
}
