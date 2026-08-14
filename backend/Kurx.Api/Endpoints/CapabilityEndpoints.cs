using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>V3 Capability registry read surface (Phase 2, V3 §11). Public, like <c>/v1/kinds</c> — the ~45
/// capabilities and a Kind's resolved defaults. An event's effective capabilities are exposed on the
/// org-scoped event route (<c>/v1/orgs/{orgId}/events/{eventId}/capabilities</c>, in EventEndpoints, which
/// reuses the event's existing view authorization). Additive: no existing route changes.</summary>
public static class CapabilityEndpoints
{
    public static void MapCapabilityEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/capabilities", async (ICapabilityService svc, CancellationToken ct) =>
        {
            var caps = await svc.ListRegistryAsync(ct);
            // CapabilityView is a proven 1:1 snake_case projection of all eight properties.
            return Results.Ok(caps);
        }).WithTags("capabilities").Produces<IReadOnlyList<CapabilityView>>();

        // D-266 M2: replaces /v1/kinds/{slug}/capabilities. Kind is archived; the archetype matrix is the
        // capability authority, and it is the only one that can answer "unsupported".
        app.MapGet("/v1/archetypes/{slug}/capabilities", async (string slug, string? mode,
            ICapabilityService svc, CancellationToken ct) =>
        {
            var caps = await svc.GetForArchetypeAsync(slug, mode, ct);
            return caps.Count == 0 ? Results.NotFound() : Results.Ok(caps.Select(ToJson));
        }).WithTags("capabilities").Produces<IReadOnlyList<ResolvedCapabilityResponse>>();
    }

    /// <summary>Shared projection for a resolved capability — reused by the event-capabilities endpoint.</summary>
    internal static ResolvedCapabilityResponse ToJson(ResolvedCapability c) => new(
        c.Slug,
        c.Name,
        c.GroupSlug,
        c.State.ToLowerInvariant(),
        c.WorkspaceTab);
}
