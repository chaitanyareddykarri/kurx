using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kurx.Api.OpenApi;

/// <summary>Describes a file download as raw binary rather than base64.
///
/// <para>The six download endpoints (CSV exports, the ticket QR PNG, the resume PDF, the local-disk
/// storage receiver) declare <c>.Produces(200, typeof(byte[]), "text/csv")</c> and friends. Swashbuckle
/// maps <c>byte[]</c> to <c>type: string, format: byte</c>, and in OpenAPI 3.0 <c>format: byte</c> means
/// <b>base64-encoded</b>. These endpoints stream raw bytes, so that description is wrong in the one way
/// that matters: a generated client would base64-decode a CSV and get garbage, and a human reading the
/// contract would expect an encoded payload that never arrives. <c>format: binary</c> is the OpenAPI 3.0
/// spelling for "the bytes themselves".</para>
///
/// <para><b>Scoped to non-JSON response bodies on purpose.</b> A <c>byte[]</c> nested inside a JSON
/// response genuinely is base64 on the wire, and rewriting it would introduce the mirror-image lie. Today
/// no such property exists — all six <c>format: byte</c> occurrences in the published contract are these
/// downloads — but the guard is what keeps that true if one is ever added. A <c>$ref</c> schema carries
/// neither Type nor Format, so a referenced component can never be caught by this.</para>
///
/// <para>An operation filter rather than a schema filter because the fact being corrected is not a
/// property of <c>byte[]</c> — it is a property of <i>this response, at this content type</i>. A schema
/// filter sees the CLR type with no idea which media type it is about to be serialized as.</para></summary>
public class BinaryResponseSchemaFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var response in operation.Responses.Values)
        {
            if (response.Content is null) continue;

            foreach (var (contentType, media) in response.Content)
            {
                if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase)) continue;
                if (media.Schema is not { Type: "string", Format: "byte" }) continue;

                media.Schema.Format = "binary";
            }
        }
    }
}
