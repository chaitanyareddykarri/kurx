using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>Evidence that a registrant accepted an event's organiser terms (D-265).
///
/// <para>The hash, not the text, is stored: an organiser can edit <c>Event.ConsentText</c> afterwards,
/// and a consent row that silently starts pointing at different wording is worse than no row at all.
/// Comparing the stored hash to the current text is what tells you whether the terms moved under a
/// registrant's feet.</para></summary>
public class RegistrationConsent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }            // unique — one consent per registration
    public Guid EventId { get; set; }
    public Guid? UserId { get; set; }                   // null for a guest registration (D-036)
    /// <summary>SHA-256 of the exact consent text shown at acceptance.</summary>
    public string ConsentTextHash { get; set; } = null!;
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Source IP at acceptance. Retained as legal evidence, never displayed.</summary>
    public string? Ip { get; set; }
}

/// <summary>A discount code for one event (D-265).
///
/// <para><see cref="RedeemedCount"/> is a shared counter and is mutated in SQL only (D-240/D-261):
/// a coupon that over-redeems under concurrent checkout is a money bug, not a display bug.</para></summary>
public class Coupon
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    /// <summary>Stored uppercase; unique per event. Case-insensitive on entry — nobody types a
    /// coupon with the case the organiser had in mind.</summary>
    public string Code { get; set; } = null!;
    public CouponKind Kind { get; set; } = CouponKind.Percent;
    /// <summary>Percent (1–100) when <see cref="Kind"/> is Percent; unused otherwise.</summary>
    public decimal? Percent { get; set; }
    /// <summary>Flat discount in paise when <see cref="Kind"/> is Flat; unused otherwise.</summary>
    public long? ValuePaise { get; set; }
    /// <summary>Null = unlimited. A cap only means something because the counter below is claimed
    /// atomically at redemption.</summary>
    public int? MaxRedemptions { get; set; }
    public int RedeemedCount { get; set; }
    /// <summary>Per-person cap; 1 stops one account draining a whole allocation.</summary>
    public int MaxPerUser { get; set; } = 1;
    public long MinOrderPaise { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One redemption of a <see cref="Coupon"/>. Unique on (CouponId, OrderId) so a retried
/// checkout cannot double-count, and queryable by user to enforce <see cref="Coupon.MaxPerUser"/>.</summary>
public class CouponRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CouponId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? UserId { get; set; }                   // null for a guest order
    public long DiscountPaise { get; set; }             // what this redemption actually took off
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
