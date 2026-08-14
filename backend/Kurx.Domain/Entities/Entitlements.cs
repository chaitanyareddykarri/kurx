using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>An organiser-defined thing a participant can hold a claim on — a lunch, a t-shirt, entry to
/// the after-party (D-334).
///
/// <para><b>Why this is not called a Coupon.</b> <see cref="Coupon"/> is D-265's discount code: a
/// percentage off an order total. A claim on a lunch shares none of its mechanics. The organiser-facing
/// UI still reads "Food &amp; Coupons" because that is the word organisers use; the domain does not,
/// because two things called Coupon in one codebase is a defect waiting to be written.</para>
///
/// <para><b>What it is NOT.</b> Not a product in its own right — it hangs off the event, and where it is
/// sold it is sold through the existing <see cref="Pass"/>/order path rather than a parallel commerce
/// route. Nothing here duplicates pricing, checkout or refund logic; <see cref="PricePaise"/> is the
/// list price the order line copies, and refunds remain the <see cref="ValueAllocationRecord"/>'s
/// business.</para></summary>
public class EntitlementProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }

    public EntitlementKind Kind { get; set; } = EntitlementKind.Meal;

    /// <summary><see cref="MealSlot.None"/> for every non-meal kind. Not nullable so the organiser's
    /// per-sitting statistics (D-334 §25) group without a null branch.</summary>
    public MealSlot MealSlot { get; set; } = MealSlot.None;

    public string Name { get; set; } = null!;

    /// <summary>Set only when <see cref="Kind"/> is <see cref="EntitlementKind.Custom"/> — the organiser's
    /// own category name (D-334 §2). <see cref="Name"/> names the item; this names its type.</summary>
    public string? CustomLabel { get; set; }

    public string? Description { get; set; }

    /// <summary>Object key in the existing storage provider, never a URL: the bucket and the signing
    /// scheme are deployment concerns and a stored URL outlives both.</summary>
    public string? ImageKey { get; set; }

    // ── Money. Paise as long (D-004); never decimal, never float. ───────────────────────────────
    /// <summary>List price. Zero is a legitimate value and means free — distinct from
    /// <see cref="EntitlementInclusion.FreeClaim"/>, which is about how it is obtained, not what it
    /// costs. A priced item included with registration still reports revenue.</summary>
    public long PricePaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;

    public EntitlementDelivery Delivery { get; set; } = EntitlementDelivery.Digital;
    public EntitlementInclusion Inclusion { get; set; } = EntitlementInclusion.SeparatePurchase;

    /// <summary>Per-participant cap. At least 1 — a cap of zero is <see cref="EntitlementInclusion.NotAvailable"/>,
    /// said properly.</summary>
    public int MaxPerParticipant { get; set; } = 1;

    /// <summary>Whether a grant may be spent a piece at a time (D-334 §13). When false a redemption takes
    /// the whole quantity, which is what "one lunch, one scan" means; when true a holder with three can
    /// spend one and keep two.</summary>
    public bool AllowsPartialRedemption { get; set; }

    // ── Validity. The window the QR is checked against at redemption (D-334 §19). ───────────────
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableUntil { get; set; }

    /// <summary>JSON array of redemption location codes; empty means "anywhere at this event". Checked
    /// server-side at redemption, so a counter cannot honour a coupon meant for another (D-334 §20).</summary>
    public string RedemptionLocationsJson { get; set; } = "[]";

    /// <summary>Organiser's free-form tags — "veg", "jain", "contains-nuts" (D-334 §3). Dietary facts are
    /// stated by the organiser about the food; nothing here records anything about a participant.</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>False while the organiser is still configuring it. An unpublished product grants nothing
    /// and is invisible to participants.</summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One holder's claim on one <see cref="EntitlementProduct"/> — the row a QR code resolves to
/// and the only thing redemption ever decrements (D-334 §13).
///
/// <para><b>One grant, whatever the delivery.</b> A product set to <see cref="EntitlementDelivery.Both"/>
/// still produces exactly one of these. The printed coupon and the in-app card render the same row, so
/// spending it on paper leaves nothing to spend on the phone. Two rows would be two claims.</para>
///
/// <para><b><see cref="RedeemedQuantity"/> is a shared counter and is mutated in SQL only</b>
/// (D-240/D-261), exactly as <see cref="Coupon.RedeemedCount"/> is. The guard is a conditional update —
/// <c>SET RedeemedQuantity = RedeemedQuantity + n WHERE Id = @id AND RedeemedQuantity + n &lt;= Quantity</c>
/// — whose rows-affected is 0 for the loser of a race. Read-modify-write in application code would let
/// two counters at two doors serve the same lunch twice, which is a food-cost bug, not a display
/// bug.</para></summary>
public class EntitlementGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntitlementProductId { get; set; }

    /// <summary>The holder. The existing user; no shadow participant identity (D-334 §35).</summary>
    public Guid UserId { get; set; }

    /// <summary>Set when the grant came from a registration — included or an add-on. Null for a
    /// separate purchase or a free claim, which need no registration to exist.</summary>
    public Guid? RegistrationId { get; set; }

    /// <summary>The order that paid for it, where one did. Null for free claims and included meals. This
    /// is the handle refunds arrive by (D-334 §28).</summary>
    public Guid? OrderId { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>Never written from application code. See the class remarks.</summary>
    public int RedeemedQuantity { get; set; }

    public EntitlementGrantStatus Status { get; set; } = EntitlementGrantStatus.Issued;

    /// <summary>What the QR encodes. Random and unguessable — the printed coupon number is a human
    /// convenience and is never the thing that authorises a redemption (D-334 §30). Same 10-char base32
    /// shape as <see cref="IdCard.VerifyCode"/> and <see cref="Certificate.VerifyCode"/>, so one
    /// verification convention covers all three.</summary>
    public string SecureToken { get; set; } = null!;

    /// <summary>Human-facing number printed on a physical coupon. Unique per event, and worth nothing on
    /// its own.</summary>
    public string? CouponNumber { get; set; }

    /// <summary>Overrides the product's window for this holder when set; otherwise the product's applies.
    /// A reissue after a loss can carry a shorter one.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Set when a reissue replaced this grant (D-334 §29). The old row stays for audit; it is
    /// <see cref="EntitlementGrantStatus.Cancelled"/> and points at its replacement.</summary>
    public Guid? SupersededByGrantId { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One redemption event against a <see cref="EntitlementGrant"/> — who spent what, where, and
/// when (D-334 §19, §31).
///
/// <para><b>The row is the audit record.</b> It is append-only: a correction is another row, never an
/// edit, because a redemption that can be rewritten proves nothing about what happened at the
/// counter.</para>
///
/// <para><b><see cref="IdempotencyKey"/> is unique</b>, so a scanner that retries on a flaky connection
/// records one redemption rather than two. That is the second half of the concurrency guarantee: the
/// conditional update on the grant stops two staff spending the same unit, and this stops one staff
/// member spending it twice by pressing the button again (D-334 §21).</para></summary>
public class EntitlementRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntitlementGrantId { get; set; }

    /// <summary>How much of the grant this took. Always the full remaining quantity unless the product
    /// sets <see cref="EntitlementProduct.AllowsPartialRedemption"/>.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>The staff member who confirmed it — an existing user, checked against the event's roles
    /// at redemption rather than trusted from the request (D-334 §22).</summary>
    public Guid RedeemedByUserId { get; set; }

    /// <summary>Which counter, where the organiser configured locations. Null when the event has none.</summary>
    public string? LocationCode { get; set; }

    /// <summary>Client-supplied, unique. See the class remarks.</summary>
    public string IdempotencyKey { get; set; } = null!;

    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
}
