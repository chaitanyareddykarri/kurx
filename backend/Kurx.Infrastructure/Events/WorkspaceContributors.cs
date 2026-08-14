using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M2 — the platform layers that own organiser surfaces, one contributor each. Event
/// capabilities are contributed separately by <see cref="CapabilityWorkspaceContributor"/>; everything here
/// exists because its subsystem exists, not because an archetype permits a behaviour.</summary>
internal static class WorkspaceOwners
{
    public const string Infrastructure = "infrastructure";
    public const string Registration = "registration";
    public const string Ticketing = "ticketing";
    public const string Invitations = "invitations";
    public const string Scheduling = "scheduling";
    public const string Finance = "finance";
    public const string Capabilities = "capabilities";
}

/// <summary>Layer 2 — always present. No event may disable registration, reminders or the audit trail, so
/// these surfaces carry no visibility rule at all: an event existing is the only precondition.</summary>
public sealed class InfrastructureWorkspaceContributor : IWorkspaceContributor
{
    public Task<IReadOnlyList<WorkspaceSurface>> GetSurfacesAsync(Guid eventId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WorkspaceSurface>>(
        [
            new("registrations", "Registrations", 10, WorkspaceOwners.Registration),
            new("check-in", "Check-in", 20, WorkspaceOwners.Registration),
            new("invitations", "Invitations", 30, WorkspaceOwners.Invitations),
            new("schedule", "Schedule", 40, WorkspaceOwners.Scheduling),
            new("audit", "Activity", 900, WorkspaceOwners.Infrastructure),
        ]);
}

/// <summary>Layers 5 and 9 — ticketing and finance surface only where money is possible at all. The rule is
/// the event's product, not its archetype: a Private product can never take payment (D13 §0), so showing it
/// a Pricing tab would offer something the backend would refuse.</summary>
public sealed class CommerceWorkspaceContributor(KurxDbContext db) : IWorkspaceContributor
{
    public async Task<IReadOnlyList<WorkspaceSurface>> GetSurfacesAsync(Guid eventId, CancellationToken ct = default)
    {
        var product = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => (EventProduct?)e.Product).FirstOrDefaultAsync(ct);
        if (product is not EventProduct.Public) return [];

        return
        [
            new("tickets", "Tickets", 50, WorkspaceOwners.Ticketing),
            new("finance", "Finance", 60, WorkspaceOwners.Finance, DependsOn: ["tickets"]),
        ];
    }
}

/// <summary>Layer 1 — the D12 event capabilities, which still own their own tabs. Unchanged in behaviour
/// from the pre-M2 generator; it is simply no longer the only contributor.</summary>
public sealed class CapabilityWorkspaceContributor(ICapabilityService capabilities) : IWorkspaceContributor
{
    public async Task<IReadOnlyList<WorkspaceSurface>> GetSurfacesAsync(Guid eventId, CancellationToken ct = default)
    {
        var caps = await capabilities.GetForEventAsync(eventId, ct);
        return caps
            .Where(c => c.State != nameof(CapabilityState.Off)
                     && c.State != nameof(CapabilityState.Locked)
                     && !string.IsNullOrEmpty(c.WorkspaceTab))
            .GroupBy(c => c.WorkspaceTab!)
            // Capability tabs occupy the 1000+ band, one Order each. Ordered by tab name so the numbers are
            // stable across requests: Order must be unique platform-wide, and reusing a single value here
            // would collide the moment an archetype resolved two tabbed capabilities.
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select((g, i) => new WorkspaceSurface(
                TabId: g.Key.ToLowerInvariant().Replace(' ', '-'),
                DisplayName: g.Key,
                Order: 1000 + i,
                Owner: WorkspaceOwners.Capabilities))
            .ToList();
    }
}

/// <summary>Composes every contributor into one ordered surface list. Deterministic: ordered by
/// <c>Order</c> then <c>TabId</c>, never by contributor registration order, so DI ordering cannot change
/// what an organiser sees.</summary>
public sealed class WorkspaceComposer(IEnumerable<IWorkspaceContributor> contributors)
{
    /// <summary>Thrown when the contributor set is internally inconsistent. This is a wiring defect, not a
    /// runtime condition, so it fails startup rather than degrading a request.</summary>
    public sealed class WorkspaceConfigurationException(string message) : Exception(message);

    public async Task<IReadOnlyList<WorkspaceSurface>> ComposeAsync(Guid eventId, CancellationToken ct = default)
    {
        var all = new List<WorkspaceSurface>();
        foreach (var c in contributors) all.AddRange(await c.GetSurfacesAsync(eventId, ct));
        return Compose(all);
    }

    /// <summary>Pure composition, separated so startup validation and tests can exercise it without a
    /// database. Ordering is deterministic <b>by design</b>: every surface declares a unique Order, and a
    /// collision is an error rather than a silent alphabetical fallback that would let two subsystems
    /// disagree about position depending on what they happened to be named.</summary>
    public static IReadOnlyList<WorkspaceSurface> Compose(IReadOnlyList<WorkspaceSurface> surfaces)
    {
        var dupeIds = surfaces.GroupBy(s => s.TabId, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();
        if (dupeIds.Count > 0)
            throw new WorkspaceConfigurationException(
                "Duplicate workspace tab ids: " + string.Join("; ", dupeIds.Select(g =>
                    $"'{g.Key}' claimed by {string.Join(" and ", g.Select(x => x.Owner))}")));

        var dupeOrder = surfaces.GroupBy(s => s.Order).Where(g => g.Count() > 1).ToList();
        if (dupeOrder.Count > 0)
            throw new WorkspaceConfigurationException(
                "Duplicate workspace Order values: " + string.Join("; ", dupeOrder.Select(g =>
                    $"Order {g.Key} claimed by {string.Join(" and ", g.Select(x => $"{x.Owner}/{x.TabId}"))}")));

        var byId = surfaces.ToDictionary(s => s.TabId, StringComparer.Ordinal);

        // A declared dependency on a tab no contributor offers is a wiring error, not a reason to hide the
        // surface: silently dropping it is how a subsystem loses a page without anyone noticing.
        foreach (var s in surfaces)
            foreach (var dep in s.DependsOn ?? [])
                if (!byId.ContainsKey(dep) && !surfaces.Any(x => x.TabId == dep))
                    throw new WorkspaceConfigurationException(
                        $"Workspace surface '{s.TabId}' ({s.Owner}) depends on '{dep}', which no contributor provides.");

        DetectCycles(byId);

        return surfaces.OrderBy(s => s.Order).ToList();
    }

    private static void DetectCycles(Dictionary<string, WorkspaceSurface> byId)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);   // 1 = visiting, 2 = done
        var stack = new List<string>();

        void Visit(string id)
        {
            if (state.GetValueOrDefault(id) == 2) return;
            if (state.GetValueOrDefault(id) == 1)
                throw new WorkspaceConfigurationException(
                    "Workspace dependency cycle: " + string.Join(" -> ", stack.Concat([id])));

            state[id] = 1;
            stack.Add(id);
            foreach (var dep in byId[id].DependsOn ?? []) Visit(dep);
            stack.RemoveAt(stack.Count - 1);
            state[id] = 2;
        }

        foreach (var id in byId.Keys) Visit(id);
    }

    /// <summary>Startup gate. Composes against a throwaway event id so contributor wiring is validated
    /// before the first request rather than on it.</summary>
    public async Task ValidateAsync(CancellationToken ct = default)
    {
        var all = new List<WorkspaceSurface>();
        foreach (var c in contributors) all.AddRange(await c.GetSurfacesAsync(Guid.Empty, ct));
        Compose(all);
    }
}
