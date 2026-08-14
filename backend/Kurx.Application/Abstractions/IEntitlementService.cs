namespace Kurx.Application.Abstractions;

/// <summary>What an organiser supplies to define a product (D-334). Deliberately not the entity: the
/// entity carries counters and timestamps a caller must never set, and a shared type would make that
/// distinction a code-review question instead of a compile-time one.</summary>
public record EntitlementProductInput(
    string Kind,
    string? MealSlot,
    string Name,
    string? CustomLabel,
    string? Description,
    string? ImageKey,
    long PricePaise,
    string Delivery,
    string Inclusion,
    int MaxPerParticipant,
    bool AllowsPartialRedemption,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    IReadOnlyList<string>? RedemptionLocations,
    IReadOnlyList<string>? Tags);

/// <summary>An organiser's view of a product, including the counters a participant never sees.</summary>
public record EntitlementProductView(
    Guid Id,
    Guid EventId,
    string Kind,
    string MealSlot,
    string Name,
    string? CustomLabel,
    string? Description,
    string? ImageKey,
    long PricePaise,
    string Currency,
    string Delivery,
    string Inclusion,
    int MaxPerParticipant,
    bool AllowsPartialRedemption,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    IReadOnlyList<string> RedemptionLocations,
    IReadOnlyList<string> Tags,
    bool IsPublished,
    int IssuedCount,
    int RedeemedCount);

/// <summary>One holder's claim, as the holder sees it. <see cref="RemainingQuantity"/> is computed rather
/// than stored, so it cannot drift from the counter that authorises a redemption.</summary>
public record EntitlementGrantView(
    Guid Id,
    Guid EntitlementProductId,
    Guid EventId,
    string EventTitle,
    string ProductName,
    string Kind,
    string MealSlot,
    string Delivery,
    int Quantity,
    int RedeemedQuantity,
    int RemainingQuantity,
    string Status,
    string SecureToken,
    string? CouponNumber,
    DateTime? ExpiresAt,
    IReadOnlyList<string> RedemptionLocations);

/// <summary>Food, meal, merchandise and access entitlements (D-334) — definition, publication and the
/// holder's own reads. Purchase, redemption and reissue are later phases and are deliberately absent:
/// an interface that declares them before they exist invites a caller to depend on a stub.
///
/// <para><b>Two gates, always both.</b> Every write checks the caller's authority on the event
/// (<c>IEventAuthority</c>, live per request — never a token claim) AND that the event has the
/// <c>entitlements</c> capability enabled. The second is what makes the feature optional: an event that
/// never turned it on has no entitlement surface at all, and asking for one is a 404 rather than an
/// empty list, because an empty list implies the feature is there and unused.</para></summary>
public interface IEntitlementService
{
    Task<ServiceResult<EntitlementProductView>> CreateProductAsync(
        Guid actorId, Guid eventId, EntitlementProductInput input, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<EntitlementProductView>> UpdateProductAsync(
        Guid actorId, Guid productId, EntitlementProductInput input, bool isAdmin, CancellationToken ct = default);

    /// <summary>Publish or withdraw. Withdrawing never revokes grants already issued — a participant who
    /// holds a lunch keeps it when the organiser stops selling lunches.</summary>
    Task<ServiceResult<EntitlementProductView>> SetPublishedAsync(
        Guid actorId, Guid productId, bool published, bool isAdmin, CancellationToken ct = default);

    /// <summary>The event's products. An organiser sees every product and its counters; anyone else sees
    /// only published ones, and the counters are zeroed rather than omitted so the shape is stable.</summary>
    Task<ServiceResult<IReadOnlyList<EntitlementProductView>>> ListForEventAsync(
        Guid? actorId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Claim a free product (D-334 §14). Refuses anything priced or not marked
    /// <c>FreeClaim</c> — the paid path is Phase 4 and must not be reachable through this door.</summary>
    Task<ServiceResult<EntitlementGrantView>> ClaimFreeAsync(
        Guid actorId, Guid productId, int quantity, CancellationToken ct = default);

    /// <summary>"My Coupons" (D-334 §17).</summary>
    Task<ServiceResult<IReadOnlyList<EntitlementGrantView>>> ListMineAsync(
        Guid userId, Guid? eventId, CancellationToken ct = default);
}
