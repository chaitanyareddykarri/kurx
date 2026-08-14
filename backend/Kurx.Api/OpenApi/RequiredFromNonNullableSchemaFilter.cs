using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kurx.Api.OpenApi;

/// <summary>Marks every non-nullable property <c>required</c>, so the published contract states which
/// keys the API always sends.
///
/// <para><b>Why this exists rather than a Swashbuckle switch.</b> The intended one-liner is
/// <c>SchemaGeneratorOptions.NonNullableReferenceTypesAsRequired</c>, which <b>does not exist in
/// Swashbuckle 6.6.2</b> — the version this project pins. It was added in 6.7.0. Setting it was a
/// compile error, which is how a change that never built reached the working tree. The choice was
/// therefore between bumping the dependency and re-implementing eleven lines of it; this file is the
/// second, because a package bump on a shared checkout regenerates the whole spec for a reason
/// unrelated to the contract itself, and this repository already owns two schema filters — required-set
/// rewriting is <see cref="SnakeCaseResponseSchemaFilter"/>'s job as well.</para>
///
/// <para><b>Nullable is the input, not an assumption.</b> <c>SupportNonNullableReferenceTypes()</c> has
/// already run by this point and set <see cref="OpenApiSchema.Nullable"/> from C#'s own annotations, so
/// this filter reads a decision Swashbuckle made rather than re-deriving one by reflection. The two can
/// never disagree, which a parallel <c>NullabilityInfoContext</c> walk could.</para>
///
/// <para><b>Registered after <see cref="SnakeCaseResponseSchemaFilter"/></b> so the names it adds are
/// the snake_case ones actually on the wire. It reads whatever keys are present, so it would still be
/// correct in the other order — the ordering is for the reader, not for correctness.</para>
///
/// <para><b>Deliberately conservative.</b> A nullable-but-always-present key (<c>Languages</c>, sent as
/// <c>null</c> rather than omitted) is left out of <c>required</c>. That understates the contract in the
/// safe direction: a client that treats it as absent still works, whereas overstating would make the
/// validator demand a key some response genuinely omits.</para></summary>
public class RequiredFromNonNullableSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties is null || schema.Properties.Count == 0) return;

        schema.Required ??= new HashSet<string>();
        foreach (var (name, property) in schema.Properties)
            if (!property.Nullable)
                schema.Required.Add(name);
    }
}
