using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kurx.Api.OpenApi;

/// <summary>Keeps the published schema in step with <see cref="Json.SnakeCaseResponseConverter"/>.
///
/// <para>Swashbuckle derives property names from the CLR type and the serializer's naming policy. The
/// response convention is applied by a <i>converter</i>, not a policy, so Swashbuckle cannot see it — the
/// spec would keep advertising camelCase for the very types the API now emits as snake_case. A contract
/// artifact that is wrong is worse than no artifact, and this is precisely the class of bug the spec was
/// introduced to end, so the two must move together.</para>
///
/// <para><b>Request types are excluded.</b> Only the response side changed; <c>*Input</c> types in this
/// namespace are bound from request bodies and still arrive camelCase. No response type in
/// <c>Kurx.Application.Abstractions</c> ends in "Input" (they are <c>*View</c>, <c>*Result</c>,
/// <c>*Summary</c>, <c>*Entry</c>, <c>*Row</c>, …), so the suffix is a sound discriminator here even
/// though it would not be for picking out responses.</para></summary>
public class SnakeCaseResponseSchemaFilter : ISchemaFilter
{
    private const string ResponseNamespace = "Kurx.Application.Abstractions";

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.Namespace != ResponseNamespace) return;
        if (context.Type.Name.EndsWith("Input", StringComparison.Ordinal)) return;
        if (schema.Properties is null || schema.Properties.Count == 0) return;

        schema.Properties = schema.Properties.ToDictionary(
            p => Json.SnakeCaseResponseConverter.ToSnakeCaseName(p.Key),
            p => p.Value);

        if (schema.Required is { Count: > 0 })
            schema.Required = new HashSet<string>(
                schema.Required.Select(Json.SnakeCaseResponseConverter.ToSnakeCaseName));
    }
}
