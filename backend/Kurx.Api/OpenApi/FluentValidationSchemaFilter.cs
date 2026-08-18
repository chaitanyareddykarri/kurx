using System.Reflection;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kurx.Api.OpenApi;

/// <summary>Projects the FluentValidation rules for a request DTO onto its OpenAPI schema (D-259).
///
/// <para>Swashbuckle infers a schema from the CLR type alone, so it describes
/// <c>AddMemberBody.Role</c> as a plain string while <c>AddMemberBodyValidator</c> rejects
/// anything that is not an <c>OrgRole</c> name. The published contract therefore documented
/// shape but not constraints, and the CI drift gate could not see a validation rule tighten or loosen.
/// This closes that gap without taking a new dependency
/// (<c>MicroElements.Swashbuckle.FluentValidation</c> does the same job and is the upgrade path if this
/// ever needs to cover more rule kinds than the five below).</para>
///
/// <para>Covers what the codebase actually uses, counted across <c>Kurx.Api/Validation</c>: NotEmpty,
/// MaximumLength/Length, Matches, GreaterThan and IsEnumName. Anything else is ignored rather than
/// guessed at — an under-described schema is honest, a wrongly-described one is not.</para></summary>
/// <remarks>Takes <see cref="IServiceScopeFactory"/>, not <see cref="IServiceProvider"/>: validators are
/// registered scoped by <c>AddValidatorsFromAssemblyContaining</c>, and resolving a scoped service from
/// the root provider throws. Swashbuckle builds filters from the root, so the earlier version returned
/// HTTP 500 for the whole spec document the moment it reached the first validated DTO.</remarks>
public class FluentValidationSchemaFilter(IServiceScopeFactory scopeFactory) : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties is null || schema.Properties.Count == 0) return;

        using var scope = scopeFactory.CreateScope();
        var validator = scope.ServiceProvider
            .GetService(typeof(IValidator<>).MakeGenericType(context.Type)) as IValidator;
        if (validator is null) return;

        var descriptor = validator.CreateDescriptor();
        foreach (var member in descriptor.GetMembersWithValidators())
        {
            // Schema keys are camelCase; the rule is declared against the CLR member name.
            var key = schema.Properties.Keys.FirstOrDefault(
                k => string.Equals(k, member.Key, StringComparison.OrdinalIgnoreCase));
            if (key is null) continue;

            foreach (var (rule, _) in member)
                ApplyRule(schema, key, schema.Properties[key], rule);
        }
    }

    private static void ApplyRule(OpenApiSchema parent, string key, OpenApiSchema property, IPropertyValidator rule)
    {
        switch (rule)
        {
            // NotEmpty means both "must be supplied" and "must not be blank" — the second is the part a
            // consumer cannot infer from `required` alone.
            case INotEmptyValidator:
                if (!parent.Required.Contains(key)) parent.Required.Add(key);
                if (property.Type == "string") property.MinLength ??= 1;
                break;

            // Covers MaximumLength, MinimumLength and Length(min, max) — all one validator underneath.
            case ILengthValidator length:
                if (length.Max > 0) property.MaxLength = length.Max;
                if (length.Min > 0) property.MinLength = length.Min;
                break;

            case IRegularExpressionValidator regex:
                property.Pattern = regex.Expression;
                break;

            // GreaterThan(n) is exclusive; GreaterThanOrEqual is not. Both surface as `minimum`, so the
            // exclusive flag is what keeps them distinguishable.
            case IComparisonValidator comparison when comparison.ValueToCompare is IConvertible convertible:
                var bound = Convert.ToDecimal(convertible);
                if (comparison.Comparison is Comparison.GreaterThan or Comparison.GreaterThanOrEqual)
                {
                    property.Minimum = bound;
                    property.ExclusiveMinimum = comparison.Comparison == Comparison.GreaterThan;
                }
                else if (comparison.Comparison is Comparison.LessThan or Comparison.LessThanOrEqual)
                {
                    property.Maximum = bound;
                    property.ExclusiveMaximum = comparison.Comparison == Comparison.LessThan;
                }
                break;

            default:
                if (EnumNamesFor(rule) is { Count: > 0 } names)
                {
                    property.Enum = names.Select(n => (IOpenApiAny)new OpenApiString(n)).ToList();
                    // The property stays a string: the wire type is a string, the enum only narrows it.
                }
                break;
        }
    }

    /// <summary>The permitted names behind <c>IsEnumName(typeof(T))</c>.
    ///
    /// <para>FluentValidation's <c>StringEnumValidator</c> keeps its target type in a private field and
    /// exposes no accessor, so this reads it reflectively. That is deliberately confined to one method
    /// and fails soft: a FluentValidation upgrade that renames the field costs the enum list on this one
    /// property, never a broken build or a wrong schema. If this ever stops finding it, switch to
    /// MicroElements.Swashbuckle.FluentValidation rather than widening the reflection.</para></summary>
    private static IReadOnlyList<string>? EnumNamesFor(IPropertyValidator rule)
    {
        if (!rule.GetType().Name.StartsWith("StringEnumValidator", StringComparison.Ordinal)) return null;

        var fields = rule.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(f => f.GetValue(rule))
            .ToList();

        // Depending on the version the validator keeps either the enum Type or the pre-computed names.
        // Accept whichever is present rather than betting on one internal layout.
        if (fields.OfType<Type>().FirstOrDefault(t => t.IsEnum) is { } enumType)
            return Enum.GetNames(enumType);

        return fields.OfType<IEnumerable<string>>().FirstOrDefault()?.ToList();
    }
}
