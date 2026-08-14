using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kurx.Tests;

/// <summary>Creating an event over HTTP the way a client does after D-267: <c>POST /v1/events</c>, with the
/// organization the caller represents carried as a body field rather than a path segment. There is no
/// org-scoped create route any more — an organization is never required to reach event creation.
///
/// <para>Tests keep passing the org id positionally because almost every fixture already has one (they seed a
/// verified org to exercise the publish gate); this helper folds it into the body so the 80-odd existing call
/// sites did not each have to grow a <c>representingOrgId</c> property. Passing <c>null</c> exercises the
/// Personal path, where the backend resolves or creates the caller's own "just me" org.</para></summary>
public static class CreateEventRequest
{
    public static Task<HttpResponseMessage> CreateEventAsync(this HttpClient client, Guid? representingOrgId, object body)
    {
        var node = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (representingOrgId is { } orgId) node["representingOrgId"] = orgId;
        return client.PostAsJsonAsync("/v1/events", node);
    }
}
