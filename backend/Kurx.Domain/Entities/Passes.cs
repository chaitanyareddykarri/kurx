using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>The commercial product (V3 §9.2), authoritative from the Phase 9 cut-over. A Pass is what a subject
/// buys; it grants one or more <see cref="AdmissionRight"/>. Phase 9 Option A implements <b>SINGLE scope only</b>:
/// a Pass maps 1:1 to a legacy <see cref="TicketType"/> (reused, not replaced — the TicketType stays as the legacy
/// mirror), and grants a single <see cref="AdmissionScope.Single"/> AdmissionRight to that ticket type's event.
/// Multi-scope passes (Subtree/Set/Query) are a later wave. Price is <see cref="Money"/> (§9.1).</summary>
public class Pass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    /// <summary>The legacy ticket type this Pass supersedes as the product, 1:1 (Phase 9 Option A). Unique.</summary>
    public Guid TicketTypeId { get; set; }
    public string Name { get; set; } = null!;
    public long PricePaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;   // ISO-4217; the event's settlement currency (§9.1)
    public int Quantity { get; set; }
    public DateTime SaleStarts { get; set; }
    public DateTime SaleEnds { get; set; }
    public int PerSubjectLimit { get; set; } = 5;
    public PassVisibility Visibility { get; set; } = PassVisibility.Public;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>What a <see cref="Pass"/> grants (V3 §9.2). Phase 9 Option A: <see cref="AdmissionScope.Single"/> only —
/// admission to exactly one event, in person, one use. <c>Scope</c>/<c>Channel</c> carry the full V3 vocabulary so
/// later waves add Subtree/Set/Query + Virtual/Either without an enum migration, but only Single/InPerson are ever
/// created this phase.</summary>
public class AdmissionRight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PassId { get; set; }
    public AdmissionScope Scope { get; set; } = AdmissionScope.Single;
    public Guid EventId { get; set; }               // the single event admitted to (Scope=Single)
    public AdmissionChannel Channel { get; set; } = AdmissionChannel.InPerson;
    public int Uses { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Value Allocation Record (V3 §9.5) — the immutable snapshot of how an order line's price is allocated
/// across the events it admits to, written <b>at purchase</b> and <b>never recomputed</b>. It is the sole basis for
/// both revenue reporting and refunds, so the two can never diverge. Phase 9 Option A is single-scope, so each order
/// line produces exactly one VAR line = the full line price on its one event (<c>Basis = list_price</c>). Multi-scope
/// allocation (weight / equal-share with deterministic rounding) is a later wave.</summary>
public class ValueAllocationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid EventId { get; set; }
    public long AllocatedPaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;   // ISO-4217 (§9.1)
    public string Basis { get; set; } = "list_price";               // list_price | allocation_weight | equal_share
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}
