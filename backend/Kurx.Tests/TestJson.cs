using System.Net.Http.Json;
using System.Text.Json;

namespace Kurx.Tests;

/// <summary>Deserialization options matching the platform's response contract: <b>snake_case out</b>
/// (D-259, applied at runtime by <c>SnakeCaseResponseConverter</c>).
///
/// <para><b>Why this has to exist.</b> <c>ReadFromJsonAsync&lt;T&gt;</c> with default options is
/// case-insensitive but not separator-insensitive, so it cannot match <c>fcm_token</c> to
/// <c>FcmToken</c>. It does not throw — it leaves every unmatched property at its default. A test that
/// asserts a count still passes while one asserting a value sees <c>0</c> or <c>null</c>, which reads as
/// a broken feature rather than a deserialization mismatch. Two tests failed exactly that way.</para>
///
/// <para>Use <see cref="ReadAsync{T}"/> for any typed read of a <c>Kurx.Application.Abstractions</c>
/// response. Reading into <c>JsonElement</c> is unaffected and needs nothing.</para></summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public static Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(SnakeCase);
}
