namespace Kurx.Api.ExceptionHandling;

/// <summary>Thrown by endpoint helpers to short-circuit the request with a specific status/error code,
/// handled by <see cref="GlobalExceptionHandler"/> instead of surfacing as a generic 500.</summary>
public class ApiException(int statusCode, string errorCode) : Exception(errorCode)
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;
}
