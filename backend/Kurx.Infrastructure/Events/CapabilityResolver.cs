using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M2 — the one place a capability decision is made.
///
/// <para>The D12 archetype matrix is the single source of truth. Resolution is a fixed pipeline:
/// <b>Product → Archetype → Matrix → Dependency graph → Defaults</b>. Every capability comes out of that
/// pipeline; there is deliberately no special-case branch for any individual slug, because the moment one
/// exists the matrix stops being authoritative and the clients start disagreeing with the backend.</para>
///
/// <para><b>"Universal required" is gone.</b> The old <c>CapDef.Universal</c> flag meant "always on, for
/// every event". It now supplies nothing more than a *default* (see <see cref="DefaultCapabilities"/>):
/// a default may enable a capability the archetype marks Optional, and may never enable one the archetype
/// marks Unsupported. Unsupported always wins; Required always wins; defaults never override the matrix.</para>
///
/// <para>Registration policy and visibility are steps 3 and 4 of the approved precedence order. They have
/// no inputs yet — the policy engine is M3 — so they are absent rather than stubbed. Adding them means
/// extending <see cref="StateOf"/>, not adding a branch elsewhere.</para></summary>
public static class CapabilityResolver
{
    /// <summary>Money. A Private product can never take payment (D13 §0), so these are Unsupported for it
    /// at step 1 — before the archetype matrix is even consulted. Listed here rather than inferred from the
    /// "commerce" group because <c>quotas</c> and <c>waitlist</c> are commerce-grouped but carry no money.</summary>
    private static readonly HashSet<string> MoneyCapabilities =
        new(StringComparer.Ordinal) { "paid", "finance" };   // donations/passes/seat-map moved to Finance & Ticketing (M2)

    /// <summary>Formerly <c>Universal = true</c>. Convenience only: enabled when the archetype permits.</summary>
    public static IReadOnlyList<string> DefaultCapabilities { get; } =
        CapabilityCatalog.Capabilities.Where(c => c.Universal).Select(c => c.Slug).ToArray();

    private static readonly Dictionary<string, CapabilityCatalog.CapDef> BySlug =
        CapabilityCatalog.Capabilities.ToDictionary(c => c.Slug, StringComparer.Ordinal);

    /// <summary>Steps 1–2: what the archetype and product permit, before dependencies or defaults.
    ///
    /// <para><paramref name="matrix"/> is the persisted <c>archetype_capability_defaults</c> grid. When it is
    /// supplied it is authoritative — the admin console owns those rows after first boot (D-188). When it is
    /// null the code transcription of D12 is used instead, which is what the pure precedence tests exercise
    /// and what a fresh database seeds from. Both paths run the same pipeline; only the source of step 2
    /// differs.</para></summary>
    public static CapabilityRule StateOf(string slug, string? archetypeSlug, EventProduct product, string mode,
        IReadOnlyDictionary<string, CapabilityRule>? matrix = null)
    {
        if (!BySlug.TryGetValue(slug, out var def)) return CapabilityRule.Unsupported;

        // Mode gate — a venue map on a purely online event is not a policy choice, it is meaningless.
        if (!def.AvailableModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
            return CapabilityRule.Unsupported;

        // Step 1 — product rules outrank everything below them.
        if (product == EventProduct.Private && MoneyCapabilities.Contains(slug))
            return CapabilityRule.Unsupported;

        // Step 2 — the archetype matrix. An event with no archetype resolved yet gets nothing beyond the
        // product rules; that is the honest answer, not a reason to fall back to the old Kind defaults.
        if (archetypeSlug is null) return CapabilityRule.Unsupported;

        if (matrix is not null)
            return matrix.GetValueOrDefault(slug, CapabilityRule.Unsupported);

        if (!CapabilityCatalog.ArchetypeDefaults.TryGetValue(archetypeSlug, out var d))
            return CapabilityRule.Unsupported;
        if (d.Required.Contains(slug, StringComparer.Ordinal)) return CapabilityRule.Required;
        if (d.Optional.Contains(slug, StringComparer.Ordinal)) return CapabilityRule.Optional;
        return CapabilityRule.Unsupported;   // absence means Unsupported, not "off"
    }

    public sealed record Resolution(
        IReadOnlyDictionary<string, CapabilityRule> States,
        IReadOnlySet<string> Enabled,
        IReadOnlyList<string> Rejected);

    /// <summary>The full pipeline. <paramref name="requested"/> is what the organiser asked for; anything
    /// Unsupported comes back in <see cref="Resolution.Rejected"/> rather than being silently dropped, so
    /// the caller can tell "you may not" apart from "you did not".</summary>
    public static Resolution Resolve(string? archetypeSlug, EventProduct product, string mode,
        IEnumerable<string>? requested = null, IReadOnlyDictionary<string, CapabilityRule>? matrix = null)
    {
        var states = CapabilityCatalog.Capabilities.ToDictionary(
            c => c.Slug, c => StateOf(c.Slug, archetypeSlug, product, mode, matrix), StringComparer.Ordinal);

        var enabled = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new List<string>();

        // Required — always on, regardless of what anyone asked for.
        foreach (var (slug, state) in states)
            if (state == CapabilityRule.Required) enabled.Add(slug);

        // Organiser choices — only an Optional capability is theirs to turn on.
        foreach (var slug in requested ?? [])
        {
            if (!states.TryGetValue(slug, out var state) || state == CapabilityRule.Unsupported)
            {
                rejected.Add(slug);
                continue;
            }
            enabled.Add(slug);
        }

        // Defaults — last, and only where the archetype already permits it.
        foreach (var slug in DefaultCapabilities)
            if (states.TryGetValue(slug, out var s) && s == CapabilityRule.Optional) enabled.Add(slug);

        // Dependency graph — enabling something pulls in what it needs. A dependency the archetype or
        // product forbids makes the dependent impossible too, so it is dropped rather than half-enabled.
        bool changed;
        do
        {
            changed = false;
            foreach (var slug in enabled.ToList())
            {
                foreach (var dep in BySlug[slug].DependsOn)
                {
                    if (states.TryGetValue(dep, out var ds) && ds != CapabilityRule.Unsupported)
                    {
                        if (enabled.Add(dep)) changed = true;
                    }
                    else if (enabled.Remove(slug))
                    {
                        if (!rejected.Contains(slug)) rejected.Add(slug);
                        changed = true;
                    }
                }
            }
        } while (changed);

        return new Resolution(states, enabled, rejected);
    }

    /// <summary>Why a capability may not be enabled, or null if it may. The backend refuses these whatever
    /// a client sends — that is what makes the engine authoritative rather than advisory.</summary>
    public static string? Reject(string slug, string? archetypeSlug, EventProduct product, string mode)
    {
        if (!BySlug.TryGetValue(slug, out var def)) return "unknown_capability";
        if (!def.AvailableModes.Contains(mode, StringComparer.OrdinalIgnoreCase)) return "capability_unavailable_for_mode";
        if (product == EventProduct.Private && MoneyCapabilities.Contains(slug)) return "private_product_cannot_take_payment";
        return StateOf(slug, archetypeSlug, product, mode) == CapabilityRule.Unsupported
            ? "capability_unsupported_for_archetype"
            : null;
    }
}
