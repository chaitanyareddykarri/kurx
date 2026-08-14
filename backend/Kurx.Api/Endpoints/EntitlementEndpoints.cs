using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>Food, meal, merchandise and access entitlements (D-334) — Phase 2: definition, publication,
/// free claim and the holder's own reads.
///
/// <para><b>Route shape follows who is asserting what</b>, as the ID-card routes do. Definition hangs off
/// <c>/v1/events/{eventId}</c> because the event is the authority; a single product is addressed
/// directly once it exists; the holder's list hangs off <c>/v1/me</c>.</para>
///
/// <para><b>"Coupon" appears nowhere in these paths.</b> <c>/v1/events/{id}/coupons</c> already means
/// D-265 discount codes. The organiser UI still says "Food &amp; Coupons"; the wire does not.</para></summary>
public static class EntitlementEndpoints
{
    public record ProductBody(
        string Kind, string? MealSlot, string Name, string? CustomLabel, string? Description,
        string? ImageKey, long PricePaise, string Delivery, string Inclusion, int MaxPerParticipant,
        bool AllowsPartialRedemption, DateTime? AvailableFrom, DateTime? AvailableUntil,
        IReadOnlyList<string>? RedemptionLocations, IReadOnlyList<string>? Tags);

    public record PublishBody(bool Published);
    public record ClaimBody(int Quantity);

    public static void MapEntitlementEndpoints(this WebApplication app)
    {
        var forEvent = app.MapGroup("/v1/events/{eventId:guid}/entitlements")
            .WithTags("entitlements").RequireAuthorization();

        // List. Organisers see every product and its counters; anyone else with standing sees published
        // products with the counters zeroed — how many lunches sold is commercial information.
        forEvent.MapGet("/", async (Guid eventId, ClaimsPrincipal p, IEntitlementService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForEventAsync(UserId(p), eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<EntitlementProductView>>();

        forEvent.MapPost("/", async (Guid eventId, ProductBody body, ClaimsPrincipal p,
            IEntitlementService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateProductAsync(UserId(p), eventId, ToInput(body), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<EntitlementProductView>();

        var product = app.MapGroup("/v1/entitlements/{productId:guid}")
            .WithTags("entitlements").RequireAuthorization();

        product.MapPatch("/", async (Guid productId, ProductBody body, ClaimsPrincipal p,
            IEntitlementService svc, CancellationToken ct) =>
        {
            var r = await svc.UpdateProductAsync(UserId(p), productId, ToInput(body), IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<EntitlementProductView>();

        // Publication is its own route rather than a field on PATCH: "make this visible to participants"
        // is a distinct decision from "correct the description", and it is the one worth auditing on its
        // own line.
        product.MapPost("/publish", async (Guid productId, PublishBody body, ClaimsPrincipal p,
            IEntitlementService svc, CancellationToken ct) =>
        {
            var r = await svc.SetPublishedAsync(UserId(p), productId, body.Published, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<EntitlementProductView>();

        // Free claim only. Paid purchase goes through the existing order path in Phase 4 and is
        // deliberately not reachable from here — the service refuses a priced product outright.
        product.MapPost("/claim", async (Guid productId, ClaimBody body, ClaimsPrincipal p,
            IEntitlementService svc, CancellationToken ct) =>
        {
            var r = await svc.ClaimFreeAsync(UserId(p), productId, body.Quantity, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<EntitlementGrantView>();

        // "My Coupons". Optional ?eventId= narrows it to one event's wallet.
        app.MapGet("/v1/me/entitlements", async (Guid? eventId, ClaimsPrincipal p,
            IEntitlementService svc, CancellationToken ct) =>
        {
            var r = await svc.ListMineAsync(UserId(p), eventId, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).WithTags("entitlements").RequireAuthorization().Produces<IReadOnlyList<EntitlementGrantView>>();
    }

    private static EntitlementProductInput ToInput(ProductBody b) => new(
        b.Kind, b.MealSlot, b.Name, b.CustomLabel, b.Description, b.ImageKey, b.PricePaise,
        b.Delivery, b.Inclusion, b.MaxPerParticipant, b.AllowsPartialRedemption,
        b.AvailableFrom, b.AvailableUntil, b.RedemptionLocations, b.Tags);

    /// <summary>An event whose entitlements the caller may not see is absent, not refused (D-018) — the
    /// service already collapses that case, and <c>capability_disabled</c> is 404 for the same reason:
    /// a feature that was never turned on has no surface to be forbidden from.</summary>
    private static IResult Fail(string? error) => error switch
    {
        "not_found" or "capability_disabled" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "payment_required" => ProblemResults.Problem(error, StatusCodes.Status402PaymentRequired),
        "limit_exceeded" or "not_claimable" or "not_published" or "expired"
            => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error ?? "bad_request", StatusCodes.Status400BadRequest),
    };

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");
}
