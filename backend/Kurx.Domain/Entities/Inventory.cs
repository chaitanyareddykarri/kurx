using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>Capacity as a segmented inventory, not a scalar (V3 §8.1, Phase 7). Phase 7 is an <b>additive
/// dual-write shadow</b>: exactly one General/InPerson/PersonSlot/Event pool is minted per <see cref="TicketType"/>
/// with <see cref="Total"/> = TicketType.Quantity and <see cref="Consumed"/> kept in lock-step with
/// TicketType.Sold at every sale/refund. The scalar remains the oversell authority this phase; the §17.1
/// conditional-decrement contract that makes pools authoritative is Phase 9. <see cref="Held"/>/<see cref="Allocated"/>
/// (the hold lifecycle) and the non-general segments are the affordances later phases fill in.</summary>
public class InventoryPool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    /// <summary>The TicketType this pool backs (Phase 7 general pools). Nullable for future event/venue-scoped
    /// pools that don't map to one ticket type.</summary>
    public Guid? TicketTypeId { get; set; }

    public InventoryScope Scope { get; set; } = InventoryScope.Event;
    public InventorySegment Segment { get; set; } = InventorySegment.General;
    public InventoryChannel Channel { get; set; } = InventoryChannel.InPerson;
    public InventoryUnit Unit { get; set; } = InventoryUnit.PersonSlot;

    // The four counters (§8.2 lifecycle: free → held → allocated → consumed). In Phase 7, <see cref="Consumed"/>
    // is a SHADOW of the scalar TicketType.Sold (= held + issued) — it is NOT yet V3's terminal scanned-Consumed
    // state, and Held/Allocated stay 0. Held / Allocated / Consumed become independent, V3-accurate states in the
    // Phase 9 authority cut-over (§17.1); until then, reconcile Consumed against Sold, never against admissions.
    public int Total { get; set; }
    public int Held { get; set; }
    public int Allocated { get; set; }
    public int Consumed { get; set; }

    public int OversellAllowance { get; set; }              // §8.1 default 0 — explicit, never accidental
    /// <summary>§8.1 release_policy `{ at, to }` — stored config; the release automation is Phase 9.</summary>
    public string? ReleasePolicyJson { get; set; }
    public NoShowPolicy NoShowPolicy { get; set; } = NoShowPolicy.None;
    public int NoShowReleaseMinutes { get; set; }
    /// <summary>§8.5 waitlist config `{ enabled, ordering, offer_ttl, auto_promote }` — stored config.</summary>
    public string? WaitlistConfigJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
