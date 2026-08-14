namespace Kurx.Application.Abstractions;

public record CouponView(Guid Id, Guid EventId, string Code, string Kind, decimal? Percent, long? ValuePaise,
    int? MaxRedemptions, int RedeemedCount, int MaxPerUser, long MinOrderPaise,
    DateTime? ValidFrom, DateTime? ValidUntil, bool IsActive, DateTime CreatedAt);

/// <summary>What a code is worth on a given order, without consuming it. The checkout form needs to
/// show the discount before the buyer commits, and quoting must never burn a redemption.</summary>
public record CouponQuoteView(Guid CouponId, string Code, long DiscountPaise, long PayablePaise);

public record CreateCouponInput(string Code, string Kind, decimal? Percent, long? ValuePaise,
    int? MaxRedemptions, int MaxPerUser, long MinOrderPaise, DateTime? ValidFrom, DateTime? ValidUntil);

public record UpdateCouponInput(int? MaxRedemptions, int? MaxPerUser, long? MinOrderPaise,
    DateTime? ValidFrom, DateTime? ValidUntil, bool? IsActive);

/// <summary>Per-event discount codes (D-265).
///
/// <para><b>Quote and redeem are deliberately separate.</b> <see cref="QuoteAsync"/> is a pure read used
/// to price a basket; <see cref="RedeemAsync"/> claims a redemption slot atomically. A single "apply"
/// method would either burn redemptions on every keystroke or hand out a discount it never reserved.</para>
///
/// <para><b><see cref="RedeemAsync"/> claims the counter in SQL</b> (D-240/D-261) — a conditional
/// <c>ExecuteUpdateAsync</c>, never read-modify-write. A coupon that over-redeems under concurrent
/// checkout is a money bug, not a display bug.</para></summary>
public interface ICouponService
{
    /// <summary>Owner/Manager of the event's org; non-member → <c>not_found</c> (D-018).</summary>
    Task<ServiceResult<CouponView>> CreateAsync(Guid userId, Guid eventId, bool isAdmin, CreateCouponInput input, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<CouponView>>> ListAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<CouponView>> UpdateAsync(Guid userId, Guid couponId, bool isAdmin, UpdateCouponInput input, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid couponId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Prices a code against an order total. Consumes nothing.</summary>
    /// <returns><c>coupon_not_found</c> · <c>coupon_inactive</c> · <c>coupon_not_started</c> ·
    /// <c>coupon_expired</c> · <c>coupon_exhausted</c> · <c>coupon_min_order_not_met</c> ·
    /// <c>coupon_user_limit_reached</c>.</returns>
    Task<ServiceResult<CouponQuoteView>> QuoteAsync(Guid? userId, Guid eventId, string code, long orderTotalPaise, CancellationToken ct = default);

    /// <summary>Claims a redemption slot and records it against the order. Idempotent per
    /// (coupon, order): a retried checkout returns the original discount rather than claiming twice.</summary>
    Task<ServiceResult<CouponQuoteView>> RedeemAsync(Guid? userId, Guid eventId, string code, Guid orderId, long orderTotalPaise, CancellationToken ct = default);
}
