using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kurx.Api.OpenApi;

/// <summary>Declares the error responses every operation can actually produce.
///
/// <para><b>The gap this closes.</b> D-313 gave 461 of 518 operations a described success body, but the
/// spec declared <b>five</b> error responses in total across the whole API — while the endpoints contain
/// 634 error returns and every one of them answers the same RFC7807 <c>ProblemDetails</c> shape with the
/// <c>error</c> extension both clients read (<c>api_error.dart</c> resolves its code from
/// <c>map['error']</c>). A generated client therefore had typed success bodies and no type at all for
/// failure, which is the half a client spends most of its error-handling code on.</para>
///
/// <para><b>Why a filter rather than 634 <c>.ProducesProblem()</c> calls.</b> The error contract is
/// uniform — one shape, one meaning, every endpoint. Writing it out per route would be the same
/// declaration typed hundreds of times, would collide with anyone editing those files, and would drift
/// the moment one route was missed. The facts this needs are already on the endpoint's metadata, so it is
/// derived rather than restated.</para>
///
/// <para><b>Only mechanically-derivable codes are added</b>, never a guess about a particular route:</para>
/// <list type="bullet">
///   <item><c>401</c> when the endpoint carries an authorization requirement — the authentication
///     middleware answers 401 for a missing or invalid token, unconditionally.</item>
///   <item><c>403</c> when that requirement names a policy (<c>KurxAdmin</c> and friends) — a satisfied
///     token that fails the policy is refused here.</item>
///   <item><c>400</c> when the operation accepts a request body — malformed JSON and failed
///     <c>WithValidation&lt;T&gt;</c> both answer 400 with the same shape.</item>
/// </list>
///
/// <para>Deliberately absent: <c>404</c>, <c>409</c>, <c>429</c>. Whether a given route can produce those
/// is a property of its logic, not of its metadata, so they stay explicit — an operation filter that
/// guessed them would put fiction in the contract, which is worse than the silence it replaced.</para>
///
/// <para>An existing declaration always wins. A route that has already said something precise about a
/// status code keeps it; this only fills silence.</para></summary>
public class ProblemResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var authorization = metadata.OfType<IAuthorizeData>().ToList();

        // `AllowAnonymous` wins over any inherited requirement, exactly as it does at runtime — so a
        // public route inside an authorized group is not described as if it could 401.
        var anonymous = metadata.OfType<IAllowAnonymous>().Any();
        var requiresAuth = authorization.Count > 0 && !anonymous;

        if (requiresAuth)
        {
            AddProblem(operation, context, StatusCodes.Status401Unauthorized,
                "No token, or a token that is expired, revoked or malformed.");

            // A named policy is a second gate the caller can fail while perfectly authenticated.
            if (authorization.Any(a => !string.IsNullOrEmpty(a.Policy)))
                AddProblem(operation, context, StatusCodes.Status403Forbidden,
                    "Authenticated, but not permitted to perform this operation.");
        }

        if (operation.RequestBody is not null)
            AddProblem(operation, context, StatusCodes.Status400BadRequest,
                "The request body failed validation, or could not be parsed.");
    }

    /// <summary>Adds one error response, referencing the shared <c>ProblemDetails</c> component rather
    /// than inlining a copy — the schema already exists in the document, and a second inline definition
    /// is a contract free to drift from the first.</summary>
    private static void AddProblem(OpenApiOperation operation, OperationFilterContext context,
        int statusCode, string description)
    {
        var key = statusCode.ToString();
        if (operation.Responses.ContainsKey(key)) return;      // an explicit declaration always wins

        var schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository);

        operation.Responses[key] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                // The media type ASP.NET Core actually sends for a ProblemDetails body.
                ["application/problem+json"] = new() { Schema = schema },
            },
        };
    }
}
