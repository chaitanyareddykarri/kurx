using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record CreateCouponBody(string Code, string Kind, decimal? Percent, long? ValuePaise,
    int? MaxRedemptions, int MaxPerUser = 1, long MinOrderPaise = 0,
    DateTime? ValidFrom = null, DateTime? ValidUntil = null);

public record UpdateCouponBody(int? MaxRedemptions, int? MaxPerUser, long? MinOrderPaise,
    DateTime? ValidFrom, DateTime? ValidUntil, bool? IsActive);

public record QuoteCouponBody(string Code, long OrderTotalPaise);

/// <summary>Per-event discount codes (D-265).
///
/// <para>Quote is a **read** and consumes nothing — the checkout form prices a basket with it before the
/// buyer commits. Redemption happens inside the order flow, not here: exposing a public "redeem" route
/// would let anyone burn a coupon's allocation without ever placing an order.</para></summary>
public static class CouponEndpoints
{
    public static void MapCouponEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/events/{eventId:guid}/coupons").WithTags("coupons").RequireAuthorization();

        g.MapPost("/", async (Guid eventId, CreateCouponBody body, ClaimsPrincipal p, ICouponService svc, CancellationToken ct) =>
        {
            var input = new CreateCouponInput(body.Code, body.Kind, body.Percent, body.ValuePaise,
                body.MaxRedemptions, body.MaxPerUser, body.MinOrderPaise, body.ValidFrom, body.ValidUntil);
            var r = await svc.CreateAsync(UserId(p), eventId, IsAdmin(p), input, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CouponView>()
          .WithSummary("Create a discount code — Owner/Manager of the event's org");

        g.MapGet("/", async (Guid eventId, ClaimsPrincipal p, ICouponService svc, CancellationToken ct) =>
        {
            var r = await svc.ListAsync(UserId(p), eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<CouponView>>()
          .WithSummary("List an event's discount codes");

        // Quote is available to any authenticated buyer — it is how the checkout form shows a discount
        // before the order exists. It never touches RedeemedCount.
        g.MapPost("/quote", async (Guid eventId, QuoteCouponBody body, ClaimsPrincipal p, ICouponService svc, CancellationToken ct) =>
        {
            var r = await svc.QuoteAsync(UserId(p), eventId, body.Code ?? "", body.OrderTotalPaise, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CouponQuoteView>()
          .WithSummary("Price a code against a basket total. Consumes nothing.");

        var one = app.MapGroup("/v1/coupons/{couponId:guid}").WithTags("coupons").RequireAuthorization();

        one.MapPatch("/", async (Guid couponId, UpdateCouponBody body, ClaimsPrincipal p, ICouponService svc, CancellationToken ct) =>
        {
            var input = new UpdateCouponInput(body.MaxRedemptions, body.MaxPerUser, body.MinOrderPaise,
                body.ValidFrom, body.ValidUntil, body.IsActive);
            var r = await svc.UpdateAsync(UserId(p), couponId, IsAdmin(p), input, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<CouponView>()
          .WithSummary("Update a code's limits or window. Code and kind are immutable.");

        one.MapDelete("/", async (Guid couponId, ClaimsPrincipal p, ICouponService svc, CancellationToken ct) =>
        {
            var r = await svc.DeleteAsync(UserId(p), couponId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).WithSummary("Delete an unused code; a redeemed one is deactivated instead (its redemptions are money history)").Produces(StatusCodes.Status204NoContent);
    }

    private static IResult Fail(string? error) => error switch
    {
        "not_found" or "coupon_not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "coupon_code_taken" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");
}
