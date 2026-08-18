using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kurx.Api.Json;

/// <summary>Serializes <c>Kurx.Application.Abstractions</c> response types with snake_case property
/// names, so every endpoint speaks the one platform convention (D-259 addendum).
///
/// <para><b>The convention, established by evidence rather than preference.</b> 65 of 75 endpoint files
/// already emit snake_case — 63 through hand-written mappers, 2 through <c>[JsonPropertyName]</c>. Their
/// request bodies, meanwhile, bind camelCase (<c>RepresentationRequestBody</c> takes
/// <c>{"name","type","primaryDomain"}</c> and answers with <c>logo_key</c>, <c>primary_domain</c>). The platform contract is therefore <b>camelCase in, snake_case out</b>.
/// The remaining 10 files never chose a second convention; they forward an Application record straight
/// out of <c>Results.Ok</c> and the default serializer shows through as camelCase. This makes them
/// conform.</para>
///
/// <para><b>Write-only, and that is the whole design.</b> <c>PropertyNamingPolicy</c> was not an option:
/// it governs deserialization too, so it would have demanded snake_case request bodies from every client
/// and from every already-deployed mobile build. Read is delegated untouched, so request binding is
/// exactly as it was — only the response side moves.</para>
///
/// <para>Applied by namespace rather than by a name suffix: the response types are 100 <c>*View</c> plus
/// ~40 others (<c>*Summary</c>, <c>*Entry</c>, <c>*Row</c>, <c>StepUpStatus</c>…), and a suffix rule
/// would silently miss the stragglers — which is the failure mode this exists to end. Request types
/// (<c>*Input</c>) live in the same namespace and are unaffected precisely because Read is delegated.</para></summary>
public class SnakeCaseResponseConverter : JsonConverterFactory
{
    private const string ResponseNamespace = "Kurx.Application.Abstractions";

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.Namespace == ResponseNamespace
           && !typeToConvert.IsEnum
           && !typeToConvert.IsPrimitive;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(
            typeof(SnakeCaseWriter<>).MakeGenericType(typeToConvert))!;

    /// <summary>snake_case → camelCase, the inverse of <see cref="ToSnakeCaseName"/>. Used on the read
    /// path to normalise incoming keys before deserializing, which is what keeps the converter symmetric.
    /// A name with no underscore is already camelCase and passes through unchanged, so request DTOs are
    /// unaffected.</summary>
    public static string ToCamelCaseName(string name)
    {
        if (!name.Contains('_')) return name;

        var builder = new System.Text.StringBuilder(name.Length);
        var upperNext = false;
        foreach (var c in name)
        {
            if (c == '_') { upperNext = true; continue; }
            builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }
        return builder.ToString();
    }

    /// <summary>camelCase → snake_case. Public because the OpenAPI schema filter has to rename
    /// properties with exactly this function — if the two ever disagreed, the published contract would
    /// describe names the API does not emit.
    ///
    /// <para>Consecutive capitals are treated as one acronym, so <c>RazorpayOrderId</c> becomes
    /// <c>razorpay_order_id</c> rather than <c>razorpay_order_i_d</c>.</para></summary>
    public static string ToSnakeCaseName(string name)
    {
        if (name.Length == 0) return name;

        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current))
            {
                var startsNewWord = i > 0
                    && (!char.IsUpper(name[i - 1])
                        || (i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (startsNewWord) builder.Append('_');
                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }
        return builder.ToString();
    }

    private sealed class SnakeCaseWriter<T> : JsonConverter<T>
    {
        // Options WITHOUT this factory, so the inner (de)serialization does not recurse into itself.
        private static readonly JsonSerializerOptions Inner = new(JsonSerializerDefaults.Web);

        /// <summary>Accepts either spelling, so the converter is symmetric.
        ///
        /// <para>An earlier version delegated Read straight to <see cref="Inner"/>, which matches
        /// camelCase only. That made the converter write one shape and read another, so anything that
        /// round-trips one of these records through JSON — <c>ReadFromJsonAsync&lt;DeviceView&gt;()</c> in
        /// the integration suite, most visibly — silently deserialized every property to its default.
        /// Twenty-nine tests turned into "Expected: 50, Actual: 0" style failures that named the assertion
        /// rather than the cause.</para>
        ///
        /// <para>Keys are normalised to camelCase before deserializing. Request DTOs are unaffected: they
        /// arrive camelCase, which contains no underscores and is therefore passed through untouched.</para></summary>
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var buffer = new System.IO.MemoryStream();
            using (var normalized = new Utf8JsonWriter(buffer))
                WriteRenamed(normalized, document.RootElement, ToCamelCaseName);
            return JsonSerializer.Deserialize<T>(buffer.ToArray(), Inner);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            // Serialize once with the default naming, then re-emit renaming every key. Going through a
            // document rather than reflecting over properties is what makes nesting work: a View holding
            // Views is converted in the same pass, at any depth.
            using var document = JsonSerializer.SerializeToDocument(value, Inner);
            WriteRenamed(writer, document.RootElement, ToSnakeCaseName);
        }

        private static void WriteRenamed(Utf8JsonWriter writer, JsonElement element, Func<string, string> rename)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        writer.WritePropertyName(rename(property.Name));
                        WriteRenamed(writer, property.Value, rename);
                    }
                    writer.WriteEndObject();
                    break;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray()) WriteRenamed(writer, item, rename);
                    writer.WriteEndArray();
                    break;

                default:
                    element.WriteTo(writer);
                    break;
            }
        }

    }
}
