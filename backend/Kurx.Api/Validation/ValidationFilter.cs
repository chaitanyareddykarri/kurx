using FluentValidation;

namespace Kurx.Api.Validation;

/// <summary>Runs the registered FluentValidation validator for <typeparamref name="T"/> against the first
/// matching endpoint argument before the handler runs, returning a ProblemDetails 400 on failure.</summary>
public class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        var arg = context.Arguments.OfType<T>().FirstOrDefault();
        if (validator is null || arg is null)
            return await next(context);

        var result = await validator.ValidateAsync(arg);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return ProblemResults.Problem("validation_failed", StatusCodes.Status400BadRequest,
                new Dictionary<string, object?> { ["errors"] = errors });
        }

        return await next(context);
    }
}

public static class ValidationFilterExtensions
{
    /// <summary>Validates the <typeparamref name="T"/> request body via its registered FluentValidation validator.</summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<ValidationFilter<T>>();
}
