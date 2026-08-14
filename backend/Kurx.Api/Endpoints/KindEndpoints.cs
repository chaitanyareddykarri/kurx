using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>V3 Kind registry read surface (Phase 1). Public, like <c>/v1/categories</c> — the 20 Kinds and
/// their legacy aliases power future kind-aware creation/discovery clients. Additive: no existing route
/// changes.</summary>
public static class KindEndpoints
{
    public static void MapKindEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/kinds", async (IKindService svc, CancellationToken ct) =>
        {
            var kinds = await svc.ListAsync(ct);
            return Results.Ok(kinds);
        }).WithTags("kinds").Produces<IReadOnlyList<KindView>>();
    }
}
