using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Events;

/// <summary>D-367 — reading a resolved capability set, in one place.
///
/// <para>The RESOLUTION legitimately differs by subject: an existing event resolves through
/// <see cref="ICapabilityService.GetForEventAsync"/> (its snapshotted archetype), an incoming Type
/// through <see cref="ICapabilityService.GetForArchetypeAsync"/>. The INTERPRETATION must not differ, and
/// it is two lines — which is exactly the size at which two copies drift silently. If "unsupported" ever
/// means more than <c>Locked</c>, it changes here and both enforcement points follow.</para></summary>
internal static class CapabilitySet
{
    /// <summary><c>Locked</c> is how <see cref="CapabilityResolver"/> reports <c>Unsupported</c>; every
    /// other state — <c>Off</c> (supported, not yet enabled), <c>On</c>, <c>Required</c> — is support.
    /// An absent capability is unsupported too, which is what an unknown or null archetype resolves to:
    /// fail closed, never fail open.</summary>
    public static bool Supports(IReadOnlyList<ResolvedCapability> resolved, string slug)
    {
        var cap = resolved.FirstOrDefault(c => c.Slug == slug);
        return cap is not null
            && !string.Equals(cap.State, nameof(CapabilityState.Locked), StringComparison.OrdinalIgnoreCase);
    }
}
