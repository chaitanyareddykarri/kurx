using Kurx.Api.Middleware;
using Kurx.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Diagnostics;

namespace Kurx.Api.ExceptionHandling;

/// <summary>Catches any exception that reaches the middleware pipeline and turns it into an RFC7807 ProblemDetails response.</summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? httpContext.TraceIdentifier;

        if (exception is ApiException api)
        {
            logger.LogWarning("Handled API exception {ErrorCode} for {Method} {Path} (correlation {CorrelationId})",
                api.ErrorCode, httpContext.Request.Method, httpContext.Request.Path, correlationId);

            httpContext.Response.StatusCode = api.StatusCode;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = api.StatusCode,
                    Title = api.ErrorCode,
                    Extensions = { ["error"] = api.ErrorCode },
                },
            });
        }

        // A malformed request is the CLIENT's error, not ours. Minimal-API model binding throws
        // BadHttpRequestException for unparseable JSON, a wrong property type, a missing required query
        // parameter, or a numeric literal that overflows the target type. Falling through to the generic
        // branch returned 500 and logged an Error with a stack trace, so any authenticated caller could
        // manufacture error-level noise at will and every such response violated the RFC7807 contract by
        // reporting a server fault for a client mistake. Handled here, once, for the whole platform.
        if (exception is BadHttpRequestException badRequest)
        {
            logger.LogWarning("Malformed request for {Method} {Path} (correlation {CorrelationId}): {Reason}",
                httpContext.Request.Method, httpContext.Request.Path, correlationId, badRequest.Message);

            // BadHttpRequestException.StatusCode is usually 400 but is 413/415 for payload/media-type
            // problems — honour whatever it carries rather than flattening everything to 400.
            httpContext.Response.StatusCode = badRequest.StatusCode;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = "malformed_request",
                    // The framework message names the offending parameter and leaks no internals, but it
                    // is still withheld outside Development to keep responses uniform.
                    Detail = env.IsDevelopment() ? badRequest.Message : "The request could not be parsed.",
                    Extensions = { ["error"] = "malformed_request" },
                },
            });
        }

        // DB-2: a database capacity or timeout failure is not a server fault in the 500 sense — the request
        // was well-formed and would have succeeded with capacity. Reporting it as 500 buries it in the same
        // bucket as real bugs, so the one signal that says "the pool is too small" or "Postgres is at its
        // connection limit" is invisible on every dashboard.
        //
        // 503 + Retry-After, because these ARE retryable — the failure happens either before the
        // transaction opens (pool exhaustion, connection refused) or inside one that then rolls back
        // (command/lock timeout), so no partial write survives. NOTHING is retried here: the status tells
        // the caller it may retry, and the caller decides. An automatic retry inside the server would be a
        // second attempt at a payment capture or a withdrawal whose first attempt's fate is unknown.
        var dbFailure = DatabaseFailureClassifier.Classify(exception);
        if (dbFailure != DatabaseFailureKind.None)
        {
            var reason = DatabaseFailureClassifier.TelemetryReason(dbFailure);
            var errorCode = DatabaseFailureClassifier.ErrorCode(dbFailure);
            KurxTelemetry.RecordDatabaseFailure(reason);

            logger.LogError(exception,
                "Database failure ({Reason}) for {Method} {Path} (correlation {CorrelationId})",
                reason, httpContext.Request.Method, httpContext.Request.Path, correlationId);

            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            httpContext.Response.Headers.RetryAfter = "1";
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = errorCode,
                    // The reason is withheld outside Development: which ceiling was hit is capacity
                    // intelligence, and an anonymous caller learning that the pool saturates at N
                    // concurrent requests has been handed the shape of a cheap denial-of-service.
                    Detail = env.IsDevelopment()
                        ? $"Database failure: {reason}. {exception.Message}"
                        : "The service is temporarily unable to reach its database. Please retry.",
                    Extensions = { ["error"] = errorCode },
                },
            });
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path} (correlation {CorrelationId})",
            httpContext.Request.Method, httpContext.Request.Path, correlationId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = env.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred. Contact support with the correlation id.",
            },
        });
    }
}
