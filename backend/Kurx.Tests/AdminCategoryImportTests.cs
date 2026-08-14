using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Kurx.Tests;

/// <summary>The taxonomy import endpoints refuse a malformed body instead of throwing.
///
/// <para><c>ImportBody.Nodes</c> deserialises to null when the caller omits <c>nodes</c>, and
/// <c>ToExport</c> projected that null list — an <c>ArgumentNullException</c> that escaped as a 500.
/// A 500 is the one failure shape an admin client cannot act on: it carries no machine-readable code,
/// so the console rendered it as a generic fault indistinguishable from the API being down, and the
/// real cause (a body the caller could have fixed) was only visible in the server log.</para></summary>
public class AdminCategoryImportTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public AdminCategoryImportTests(KurxApiFactory factory) => _factory = factory;

    public static TheoryData<string, object> MalformedBodies() => new()
    {
        // `nodes` absent entirely — the shape that produced the 500.
        { "/v1/admin/categories/import/preview", new { } },
        { "/v1/admin/categories/import/apply", new { } },
        // `nodes` present but empty: nothing to import is still a refusal, not a no-op success.
        { "/v1/admin/categories/import/preview", new { nodes = Array.Empty<object>() } },
        { "/v1/admin/categories/import/apply", new { nodes = Array.Empty<object>() } },
    };

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public async Task A_body_with_no_nodes_is_refused_not_thrown(string path, object body)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync(path, body);

        // The assertion that matters is NOT 500. Unauthenticated is a legitimate outcome here — the
        // point is that the request is rejected by a guard rather than by an unhandled exception.
        Assert.NotEqual(HttpStatusCode.InternalServerError, res.StatusCode);

        if (res.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("nodes_required", problem.GetProperty("error").GetString());
        }
    }
}
