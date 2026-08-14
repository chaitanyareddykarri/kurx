using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Orders;

/// <summary>Per-event discount codes (D-265). See <see cref="ICouponService"/> for the contract.</summary>
public class CouponService(KurxDbContext db, IEventPermissionService perms) : ICouponService
{
    private const string Manage = "event:manage";

    /// <summary>Codes are stored and compared uppercase — nobody types a coupon with the case the
    /// organiser had in mind.</summary>
    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private static CouponView ToView(Coupon c) => new(c.Id, c.EventId, c.Code, c.Kind.ToString(),
        c.Percent, c.ValuePaise, c.MaxRedemptions, c.RedeemedCount, c.MaxPerUser, c.MinOrderPaise,
        c.ValidFrom, c.ValidUntil, c.IsActive, c.CreatedAt);

    /// <summary>Non-member gets <c>not_found</c>, never <c>forbidden</c> — an event's existence is not
    /// leaked by its coupon endpoints either (D-018).</summary>
    private async Task<bool> CanManageAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct)
        => isAdmin || await perms.HasAsync(userId, eventId, Manage, ct);

    // ── Organiser CRUD ────────────────────────────────────────────────────────

    public async Task<ServiceResult<CouponView>> CreateAsync(Guid userId, Guid eventId, bool isAdmin,
        CreateCouponInput input, CancellationToken ct = default)
    {
        if (!await db.Events.AnyAsync(e => e.Id == eventId && e.DeletedAt == null, ct))
            return ServiceResult<CouponView>.Fail("not_found");
        if (!await CanManageAsync(userId, eventId, isAdmin, ct))
            return ServiceResult<CouponView>.Fail("not_found");

        var code = Normalize(input.Code ?? "");
        if (code.Length is < 3 or > 40) return ServiceResult<CouponView>.Fail("invalid_code");
        if (!Enum.TryParse<CouponKind>(input.Kind, true, out var kind))
            return ServiceResult<CouponView>.Fail("invalid_coupon_kind");

        // A percent coupon with no percent, or a flat one with no amount, discounts nothing — it would
        // look valid at checkout and take nothing off.
        if (kind == CouponKind.Percent && input.Percent is not (> 0 and <= 100))
            return ServiceResult<CouponView>.Fail("invalid_percent");
        if (kind == CouponKind.Flat && input.ValuePaise is not > 0)
            return ServiceResult<CouponView>.Fail("invalid_value");
        if (input.MaxRedemptions is <= 0) return ServiceResult<CouponView>.Fail("invalid_max_redemptions");
        if (input.ValidFrom is not null && input.ValidUntil is not null && input.ValidUntil <= input.ValidFrom)
            return ServiceResult<CouponView>.Fail("invalid_coupon_window");

        if (await db.Coupons.AnyAsync(c => c.EventId == eventId && c.Code == code, ct))
            return ServiceResult<CouponView>.Fail("coupon_code_taken");

        var coupon = new Coupon
        {
            EventId = eventId,
            Code = code,
            Kind = kind,
            Percent = kind == CouponKind.Percent ? input.Percent : null,
            ValuePaise = kind == CouponKind.Flat ? input.ValuePaise : null,
            MaxRedemptions = input.MaxRedemptions,
            MaxPerUser = input.MaxPerUser <= 0 ? 1 : input.MaxPerUser,
            MinOrderPaise = input.MinOrderPaise < 0 ? 0 : input.MinOrderPaise,
            ValidFrom = input.ValidFrom is null ? null : DateTime.SpecifyKind(input.ValidFrom.Value, DateTimeKind.Utc),
            ValidUntil = input.ValidUntil is null ? null : DateTime.SpecifyKind(input.ValidUntil.Value, DateTimeKind.Utc),
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);
        return ServiceResult<CouponView>.Success(ToView(coupon));
    }

    public async Task<ServiceResult<IReadOnlyList<CouponView>>> ListAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, eventId, isAdmin, ct))
            return ServiceResult<IReadOnlyList<CouponView>>.Fail("not_found");

        var rows = await db.Coupons.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<CouponView>>.Success(rows.Select(ToView).ToList());
    }

    public async Task<ServiceResult<CouponView>> UpdateAsync(Guid userId, Guid couponId, bool isAdmin,
        UpdateCouponInput input, CancellationToken ct = default)
    {
        var coupon = await db.Coupons.SingleOrDefaultAsync(c => c.Id == couponId, ct);
        if (coupon is null) return ServiceResult<CouponView>.Fail("not_found");
        if (!await CanManageAsync(userId, coupon.EventId, isAdmin, ct))
            return ServiceResult<CouponView>.Fail("not_found");

        // The code and kind are immutable: editing them would silently repoint every redemption already
        // recorded against this row at different terms.
        if (input.MaxRedemptions is not null)
        {
            if (input.MaxRedemptions <= 0) return ServiceResult<CouponView>.Fail("invalid_max_redemptions");
            // Lowering the cap below what has already gone out would make the counter read as
            // over-redeemed forever.
            if (input.MaxRedemptions < coupon.RedeemedCount)
                return ServiceResult<CouponView>.Fail("max_below_redeemed");
            coupon.MaxRedemptions = input.MaxRedemptions;
        }
        if (input.MaxPerUser is > 0) coupon.MaxPerUser = input.MaxPerUser.Value;
        if (input.MinOrderPaise is >= 0) coupon.MinOrderPaise = input.MinOrderPaise.Value;
        if (input.ValidFrom is not null) coupon.ValidFrom = DateTime.SpecifyKind(input.ValidFrom.Value, DateTimeKind.Utc);
        if (input.ValidUntil is not null) coupon.ValidUntil = DateTime.SpecifyKind(input.ValidUntil.Value, DateTimeKind.Utc);
        if (input.IsActive is not null) coupon.IsActive = input.IsActive.Value;

        if (coupon.ValidFrom is not null && coupon.ValidUntil is not null && coupon.ValidUntil <= coupon.ValidFrom)
            return ServiceResult<CouponView>.Fail("invalid_coupon_window");

        coupon.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<CouponView>.Success(ToView(coupon));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid couponId, bool isAdmin, CancellationToken ct = default)
    {
        var coupon = await db.Coupons.SingleOrDefaultAsync(c => c.Id == couponId, ct);
        if (coupon is null) return ServiceResult<bool>.Fail("not_found");
        if (!await CanManageAsync(userId, coupon.EventId, isAdmin, ct))
            return ServiceResult<bool>.Fail("not_found");

        // A redeemed coupon is deactivated, never deleted: its redemptions are money history, and a
        // dangling coupon_id would orphan them.
        if (coupon.RedeemedCount > 0)
        {
            coupon.IsActive = false;
            coupon.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.Coupons.Remove(coupon);
        }
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── Quote / redeem ────────────────────────────────────────────────────────

    /// <summary>The shared eligibility check. Returns the coupon and its discount, or an error code.
    /// Used by both quote and redeem so the two can never disagree about what a code is worth.</summary>
    private async Task<(Coupon? Coupon, long Discount, string? Error)> EvaluateAsync(
        Guid? userId, Guid eventId, string code, long orderTotalPaise, CancellationToken ct)
    {
        var normalized = Normalize(code ?? "");
        var coupon = await db.Coupons.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventId == eventId && c.Code == normalized, ct);

        if (coupon is null) return (null, 0, "coupon_not_found");
        if (!coupon.IsActive) return (null, 0, "coupon_inactive");

        var now = DateTime.UtcNow;
        if (coupon.ValidFrom is not null && now < coupon.ValidFrom) return (null, 0, "coupon_not_started");
        if (coupon.ValidUntil is not null && now >= coupon.ValidUntil) return (null, 0, "coupon_expired");
        if (coupon.MaxRedemptions is not null && coupon.RedeemedCount >= coupon.MaxRedemptions)
            return (null, 0, "coupon_exhausted");
        if (orderTotalPaise < coupon.MinOrderPaise) return (null, 0, "coupon_min_order_not_met");

        if (userId is not null)
        {
            var mine = await db.CouponRedemptions
                .CountAsync(r => r.CouponId == coupon.Id && r.UserId == userId, ct);
            if (mine >= coupon.MaxPerUser) return (null, 0, "coupon_user_limit_reached");
        }

        var discount = coupon.Kind == CouponKind.Percent
            // Rounded to whole paise; money is never fractional (D-004).
            ? (long)Math.Round(orderTotalPaise * (coupon.Percent ?? 0) / 100m, MidpointRounding.AwayFromZero)
            : coupon.ValuePaise ?? 0;

        // A discount larger than the basket must not produce a negative payable.
        discount = Math.Clamp(discount, 0, orderTotalPaise);
        return (coupon, discount, null);
    }

    public async Task<ServiceResult<CouponQuoteView>> QuoteAsync(Guid? userId, Guid eventId, string code,
        long orderTotalPaise, CancellationToken ct = default)
    {
        var (coupon, discount, error) = await EvaluateAsync(userId, eventId, code, orderTotalPaise, ct);
        return error is not null
            ? ServiceResult<CouponQuoteView>.Fail(error)
            : ServiceResult<CouponQuoteView>.Success(
                new CouponQuoteView(coupon!.Id, coupon.Code, discount, orderTotalPaise - discount));
    }

    public async Task<ServiceResult<CouponQuoteView>> RedeemAsync(Guid? userId, Guid eventId, string code,
        Guid orderId, long orderTotalPaise, CancellationToken ct = default)
    {
        var (coupon, discount, error) = await EvaluateAsync(userId, eventId, code, orderTotalPaise, ct);
        if (error is not null) return ServiceResult<CouponQuoteView>.Fail(error);

        // Idempotent per (coupon, order): a retried checkout returns the original discount rather than
        // claiming a second slot. Checked before the claim so a retry never even touches the counter.
        var existing = await db.CouponRedemptions.AsNoTracking()
            .SingleOrDefaultAsync(r => r.CouponId == coupon!.Id && r.OrderId == orderId, ct);
        if (existing is not null)
            return ServiceResult<CouponQuoteView>.Success(
                new CouponQuoteView(coupon!.Id, coupon.Code, existing.DiscountPaise, orderTotalPaise - existing.DiscountPaise));

        // ── The claim (D-240/D-261) ──────────────────────────────────────────
        // Conditional UPDATE, never read-modify-write. The WHERE carries the cap, so the database
        // decides who gets the last slot: N concurrent callers issue N updates and exactly
        // (cap - already_redeemed) of them report a row changed. Reading the count and then writing
        // count+1 would let every concurrent caller read the same value and all succeed.
        var claimed = await db.Coupons
            .Where(c => c.Id == coupon!.Id
                        && c.IsActive
                        && (c.MaxRedemptions == null || c.RedeemedCount < c.MaxRedemptions))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RedeemedCount, c => c.RedeemedCount + 1), ct);

        if (claimed == 0) return ServiceResult<CouponQuoteView>.Fail("coupon_exhausted");

        db.CouponRedemptions.Add(new CouponRedemption
        {
            CouponId = coupon!.Id,
            OrderId = orderId,
            UserId = userId,
            DiscountPaise = discount,
        });
        await db.SaveChangesAsync(ct);

        return ServiceResult<CouponQuoteView>.Success(
            new CouponQuoteView(coupon.Id, coupon.Code, discount, orderTotalPaise - discount));
    }
}
