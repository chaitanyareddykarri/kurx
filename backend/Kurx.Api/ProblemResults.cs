namespace Kurx.Api;

/// <summary>Builds RFC7807 ProblemDetails results for expected (non-exceptional) API error cases,
/// so every error response — handled or unhandled — shares the same shape.</summary>
public static class ProblemResults
{
    public static IResult Problem(string? errorCode, int statusCode, IDictionary<string, object?>? extra = null)
    {
        var extensions = new Dictionary<string, object?>(extra ?? new Dictionary<string, object?>())
        {
            ["error"] = errorCode,
        };
        return Results.Problem(detail: errorCode, statusCode: statusCode, extensions: extensions);
    }
}
