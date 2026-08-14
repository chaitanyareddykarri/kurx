using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Read + resolution over the V3 Capability registry (Phase 2, V3 §11). The effective set is
/// computed from the seeded registry + kind defaults + mode-gating + the depends_on DAG, so reads never
/// diverge from what materialization stores (both use <see cref="ResolveStates"/>).</summary>
public class CapabilityService(KurxDbContext db) : ICapabilityService
{
    public async Task<IReadOnlyList<CapabilityView>> ListRegistryAsync(CancellationToken ct = default)
    {
        var caps = await db.Capabilities.AsNoTracking().OrderBy(c => c.Sort).ToListAsync(ct);
        return caps.Select(c => new CapabilityView(c.Slug, c.Name, c.GroupSlug, c.IsUniversal, c.WorkspaceTab,
            Arr(c.DependsOnJson), Arr(c.AvailableModesJson), c.Sort)).ToList();
    }

    /// <summary>D-266 M2 — the archetype's own capability set, independent of any event. "Hybrid" is the
    /// default mode because it is the only value present in every AvailableModes list, so it shows what the
    /// archetype supports without a venue decision narrowing it.</summary>
    public async Task<IReadOnlyList<ResolvedCapability>> GetForArchetypeAsync(string archetypeSlug,
        string? mode = null, CancellationToken ct = default)
    {
        var product = await db.EventArchetypes.AsNoTracking()
            .Where(a => a.Slug == archetypeSlug).Select(a => (EventProduct?)a.Product).FirstOrDefaultAsync(ct);
        if (product is null) return Array.Empty<ResolvedCapability>();

        var caps = await db.Capabilities.AsNoTracking().OrderBy(c => c.Sort).ToListAsync(ct);
        var matrix = await MatrixAsync(archetypeSlug, ct);
        var res = CapabilityResolver.Resolve(archetypeSlug, product.Value, mode ?? "Hybrid", matrix: matrix);
        return Project(caps, ToStates(caps, res));
    }

    /// <summary>D-266 M2 — resolution now runs the archetype pipeline: Product -> Archetype -> Matrix ->
    /// Dependency DAG -> Defaults. The Kind path this replaces reached its defaults through the name-matched
    /// KindAlias chain D-188 flags as fragile, and could not express "unsupported" at all.
    ///
    /// <para>The persisted matrix (<c>archetype_capability_defaults</c>) is the authority, not the code
    /// constant: the admin console owns the rows after first boot, exactly as it owns the taxonomy (D-188).
    /// <see cref="CapabilityResolver"/> holds the pipeline; this method only supplies its inputs.</para></summary>
    public async Task<IReadOnlyList<ResolvedCapability>> GetForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => new { e.ArchetypeSlug, e.Product, e.EventMode }).FirstOrDefaultAsync(ct);
        if (ev is null) return Array.Empty<ResolvedCapability>();

        var caps = await db.Capabilities.AsNoTracking().OrderBy(c => c.Sort).ToListAsync(ct);
        var matrix = await MatrixAsync(ev.ArchetypeSlug, ct);
        var resolution = CapabilityResolver.Resolve(ev.ArchetypeSlug, ev.Product, ev.EventMode.ToString(), matrix: matrix);

        return Project(caps, ToStates(caps, resolution));
    }

    /// <summary>The persisted matrix for one archetype, as the rule-per-slug map the resolver consumes.
    /// Falls back to nothing (not to Kind) when the archetype is unknown — an unresolved archetype must
    /// surface as "no capabilities", never as legacy behaviour wearing a new name.</summary>
    /// <summary>Rule -> stored state. Unsupported surfaces as Locked, never Off: Off invites a client to
    /// render a toggle the backend would then refuse, while Locked says "not available here" in one word.</summary>
    private static Dictionary<string, CapabilityState> ToStates(IEnumerable<Capability> caps,
        CapabilityResolver.Resolution res)
    {
        var states = new Dictionary<string, CapabilityState>(StringComparer.Ordinal);
        foreach (var c in caps)
        {
            var rule = res.States.GetValueOrDefault(c.Slug, CapabilityRule.Unsupported);
            states[c.Slug] = rule == CapabilityRule.Unsupported ? CapabilityState.Locked
                           : rule == CapabilityRule.Required ? CapabilityState.Required
                           : res.Enabled.Contains(c.Slug) ? CapabilityState.On
                           : CapabilityState.Off;
        }
        return states;
    }

    private async Task<IReadOnlyDictionary<string, CapabilityRule>> MatrixAsync(string? archetypeSlug, CancellationToken ct)
    {
        if (archetypeSlug is null) return new Dictionary<string, CapabilityRule>(StringComparer.Ordinal);
        return await db.ArchetypeCapabilityDefaults.AsNoTracking()
            .Where(r => r.ArchetypeSlug == archetypeSlug)
            .ToDictionaryAsync(r => r.CapabilitySlug, r => r.Rule, StringComparer.Ordinal, ct);
    }

    public async Task MaterializeForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var effective = await GetForEventAsync(eventId, ct);
        if (effective.Count == 0) return;   // event not found — nothing to do

        // Clear + re-insert from the resolved defaults. ExecuteDeleteAsync removes the rows directly
        // (no load, no change-tracking), so a re-materialize on a Kind/mode change can never hit the EF
        // identity conflict of a Deleted row and a re-Added row sharing the same (EventId, CapabilitySlug)
        // key — the bug the review found. No per-event overrides exist yet (toggles are a later phase);
        // when they do, this becomes a merge that preserves override rows. Only non-OFF states are stored.
        await db.EventCapabilities.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        foreach (var c in effective)
        {
            // Off = available but not chosen; Locked = forbidden by the archetype matrix or product
            // rules. Neither is an enabled capability, and materializing a Locked row would hand clients a
            // capability the backend refuses to honour.
            if (c.State is nameof(CapabilityState.Off) or nameof(CapabilityState.Locked)) continue;
            db.EventCapabilities.Add(new EventCapability
            {
                EventId = eventId, CapabilitySlug = c.Slug, State = Enum.Parse<CapabilityState>(c.State),
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> BackfillEventCapabilitiesAsync(CancellationToken ct = default)
    {
        // D-266 M2: keyed on the archetype axis, not the retired Kind. Filtering on KindSlug skipped every
        // event created after the switch — those carry an ArchetypeSlug and may have no Kind at all — so the
        // backfill silently repaired only legacy rows and left new events with no capabilities.
        var ids = await db.Events
            .Where(e => e.ArchetypeSlug != null && !db.EventCapabilities.Any(x => x.EventId == e.Id))
            .Select(e => e.Id).ToListAsync(ct);
        foreach (var id in ids) await MaterializeForEventAsync(id, ct);
        return ids.Count;
    }

    // ── resolution ─────────────────────────────────────────────────────────────


    private static IReadOnlyList<ResolvedCapability> Project(List<Capability> caps, Dictionary<string, CapabilityState> states)
        => caps.Select(c => new ResolvedCapability(c.Slug, c.Name, c.GroupSlug, states[c.Slug].ToString(), c.WorkspaceTab)).ToList();

    private static string[] Arr(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
}
