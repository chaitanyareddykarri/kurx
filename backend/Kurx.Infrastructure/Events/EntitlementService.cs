using System.Security.Cryptography;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Food, meal, merchandise and access entitlements (D-334), Phase 2: definition, publication,
/// free claim, and the holder's own reads.
///
/// <para><b>Every write passes two gates.</b> The caller's authority on the event, resolved live through
/// <see cref="IEventAuthority"/> (D-015 — never a token claim, so a removed collaborator loses access on
/// their next request rather than their next login), and the event's <c>entitlements</c> capability. The
/// second gate is what makes the feature optional: an event that never enabled it answers 404, not an
/// empty list, because an empty list says "this exists and is unused".</para>
///
/// <para><b>What this service does not do.</b> It does not price, charge, refund or redeem. Paid
/// purchase runs through the existing order path (Phase 4) and redemption is Phase 5; both are absent
/// here rather than stubbed, because a stub is something a caller can accidentally depend on.</para></summary>
public class EntitlementService(
    KurxDbContext db,
    IEventAuthority authority,
    IAuditWriter audit) : IEntitlementService
{
    private const string CapabilitySlug = "entitlements";

    public async Task<ServiceResult<EntitlementProductView>> CreateProductAsync(
        Guid actorId, Guid eventId, EntitlementProductInput input, bool isAdmin, CancellationToken ct = default)
    {
        var gate = await GateForManageAsync(actorId, eventId, isAdmin, ct);
        if (gate is not null) return ServiceResult<EntitlementProductView>.Fail(gate);

        if (Validate(input) is { } invalid) return ServiceResult<EntitlementProductView>.Fail(invalid);

        var product = new EntitlementProduct
        {
            EventId = eventId,
            Kind = ParseKind(input.Kind),
            MealSlot = ParseMealSlot(input.MealSlot),
            Name = input.Name.Trim(),
            CustomLabel = input.CustomLabel?.Trim(),
            Description = input.Description?.Trim(),
            ImageKey = input.ImageKey,
            PricePaise = input.PricePaise,
            Delivery = ParseDelivery(input.Delivery),
            Inclusion = ParseInclusion(input.Inclusion),
            MaxPerParticipant = input.MaxPerParticipant,
            AllowsPartialRedemption = input.AllowsPartialRedemption,
            AvailableFrom = input.AvailableFrom,
            AvailableUntil = input.AvailableUntil,
            RedemptionLocationsJson = JsonSerializer.Serialize(input.RedemptionLocations ?? []),
            TagsJson = JsonSerializer.Serialize(input.Tags ?? []),
            // Never published on creation. An organiser mid-configuration has not decided to sell
            // anything, and a half-configured lunch appearing in a participant's list is worse than
            // one extra click.
            IsPublished = false,
        };
        db.EntitlementProducts.Add(product);

        audit.Write(new AuditEvent("entitlement_product.created", "entitlement_products", product.Id,
            ActorId: actorId, After: new { product.Name, Kind = product.Kind.ToString(), product.PricePaise }));

        await db.SaveChangesAsync(ct);
        return ServiceResult<EntitlementProductView>.Success(ToProductView(product, 0, 0));
    }

    public async Task<ServiceResult<EntitlementProductView>> UpdateProductAsync(
        Guid actorId, Guid productId, EntitlementProductInput input, bool isAdmin, CancellationToken ct = default)
    {
        var product = await db.EntitlementProducts.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return ServiceResult<EntitlementProductView>.Fail("not_found");

        var gate = await GateForManageAsync(actorId, product.EventId, isAdmin, ct);
        if (gate is not null) return ServiceResult<EntitlementProductView>.Fail(gate);

        if (Validate(input) is { } invalid) return ServiceResult<EntitlementProductView>.Fail(invalid);

        var before = new { product.Name, product.PricePaise, product.MaxPerParticipant };

        product.Kind = ParseKind(input.Kind);
        product.MealSlot = ParseMealSlot(input.MealSlot);
        product.Name = input.Name.Trim();
        product.CustomLabel = input.CustomLabel?.Trim();
        product.Description = input.Description?.Trim();
        product.ImageKey = input.ImageKey;
        product.PricePaise = input.PricePaise;
        product.Delivery = ParseDelivery(input.Delivery);
        product.Inclusion = ParseInclusion(input.Inclusion);
        product.MaxPerParticipant = input.MaxPerParticipant;
        product.AllowsPartialRedemption = input.AllowsPartialRedemption;
        product.AvailableFrom = input.AvailableFrom;
        product.AvailableUntil = input.AvailableUntil;
        product.RedemptionLocationsJson = JsonSerializer.Serialize(input.RedemptionLocations ?? []);
        product.TagsJson = JsonSerializer.Serialize(input.Tags ?? []);
        product.UpdatedAt = DateTime.UtcNow;

        audit.Write(new AuditEvent("entitlement_product.updated", "entitlement_products", product.Id,
            ActorId: actorId, Before: before,
            After: new { product.Name, product.PricePaise, product.MaxPerParticipant }));

        await db.SaveChangesAsync(ct);
        var (issued, redeemed) = await CountsAsync(product.Id, ct);
        return ServiceResult<EntitlementProductView>.Success(ToProductView(product, issued, redeemed));
    }

    public async Task<ServiceResult<EntitlementProductView>> SetPublishedAsync(
        Guid actorId, Guid productId, bool published, bool isAdmin, CancellationToken ct = default)
    {
        var product = await db.EntitlementProducts.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return ServiceResult<EntitlementProductView>.Fail("not_found");

        var gate = await GateForManageAsync(actorId, product.EventId, isAdmin, ct);
        if (gate is not null) return ServiceResult<EntitlementProductView>.Fail(gate);

        product.IsPublished = published;
        product.UpdatedAt = DateTime.UtcNow;

        // Withdrawing stops issuance; it does NOT revoke what people already hold. Somebody who bought a
        // lunch keeps it when the organiser stops selling lunches — taking it back is a cancellation with
        // its own audit trail and refund, not a side effect of a visibility toggle.
        audit.Write(new AuditEvent(published ? "entitlement_product.published" : "entitlement_product.withdrawn",
            "entitlement_products", product.Id, ActorId: actorId));

        await db.SaveChangesAsync(ct);
        var (issued, redeemed) = await CountsAsync(product.Id, ct);
        return ServiceResult<EntitlementProductView>.Success(ToProductView(product, issued, redeemed));
    }

    public async Task<ServiceResult<IReadOnlyList<EntitlementProductView>>> ListForEventAsync(
        Guid? actorId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(actorId, eventId, isAdmin, ct);
        if (!access.EventExists) return ServiceResult<IReadOnlyList<EntitlementProductView>>.Fail("not_found");

        // The capability gate comes before anything else a caller could learn from. An event without the
        // feature has no entitlement surface, and saying so with an empty list would be a different claim.
        if (!await IsEnabledAsync(eventId, ct))
            return ServiceResult<IReadOnlyList<EntitlementProductView>>.Fail("not_found");

        var manages = access.Can(EventPermission.ManageContent);

        var products = await db.EntitlementProducts.AsNoTracking()
            .Where(p => p.EventId == eventId && (manages || p.IsPublished))
            .OrderBy(p => p.MealSlot).ThenBy(p => p.Name)
            .ToListAsync(ct);

        // Counters are an organiser's business — how many lunches sold is commercial information, not
        // something a participant browsing the menu is owed. The field stays in the shape and reads zero,
        // so one client can render both cases without branching on presence.
        var counts = manages
            ? await db.EntitlementGrants.AsNoTracking()
                .Where(g => products.Select(p => p.Id).Contains(g.EntitlementProductId))
                .GroupBy(g => g.EntitlementProductId)
                .Select(g => new { ProductId = g.Key, Issued = g.Count(), Redeemed = g.Sum(x => x.RedeemedQuantity) })
                .ToDictionaryAsync(x => x.ProductId, x => (x.Issued, x.Redeemed), ct)
            : [];

        var views = products
            .Select(p => counts.TryGetValue(p.Id, out var c)
                ? ToProductView(p, c.Issued, c.Redeemed)
                : ToProductView(p, 0, 0))
            .ToList();

        return ServiceResult<IReadOnlyList<EntitlementProductView>>.Success(views);
    }

    public async Task<ServiceResult<EntitlementGrantView>> ClaimFreeAsync(
        Guid actorId, Guid productId, int quantity, CancellationToken ct = default)
    {
        if (quantity < 1) return ServiceResult<EntitlementGrantView>.Fail("invalid_quantity");

        var product = await db.EntitlementProducts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return ServiceResult<EntitlementGrantView>.Fail("not_found");
        if (!await IsEnabledAsync(product.EventId, ct)) return ServiceResult<EntitlementGrantView>.Fail("not_found");

        var access = await authority.ResolveAsync(actorId, product.EventId, isAdmin: false, ct);
        if (!access.EventExists) return ServiceResult<EntitlementGrantView>.Fail("not_found");
        // Standing on the event is what entitles someone to claim from it. A stranger browsing a public
        // event is not owed its lunches.
        if (!access.Can(EventPermission.Participate)) return ServiceResult<EntitlementGrantView>.Fail("forbidden");

        if (!product.IsPublished) return ServiceResult<EntitlementGrantView>.Fail("not_published");

        // The free door refuses anything that costs money, twice over: by inclusion mode and by price.
        // Either check alone would let a misconfiguration through — a FreeClaim product with a price, or
        // a zero-priced product the organiser meant to sell as an add-on.
        if (product.Inclusion != EntitlementInclusion.FreeClaim)
            return ServiceResult<EntitlementGrantView>.Fail("not_claimable");
        if (product.PricePaise != 0)
            return ServiceResult<EntitlementGrantView>.Fail("payment_required");

        var now = DateTime.UtcNow;
        if (product.AvailableUntil is { } until && until <= now)
            return ServiceResult<EntitlementGrantView>.Fail("expired");

        // The per-participant cap counts what they already hold, not what they are asking for.
        var held = await db.EntitlementGrants
            .Where(g => g.EntitlementProductId == productId && g.UserId == actorId
                && g.Status != EntitlementGrantStatus.Cancelled && g.Status != EntitlementGrantStatus.Refunded)
            .SumAsync(g => (int?)g.Quantity, ct) ?? 0;
        if (held + quantity > product.MaxPerParticipant)
            return ServiceResult<EntitlementGrantView>.Fail("limit_exceeded");

        var grant = new EntitlementGrant
        {
            EntitlementProductId = productId,
            UserId = actorId,
            Quantity = quantity,
            Status = EntitlementGrantStatus.Issued,
            SecureToken = NewSecureToken(),
            ExpiresAt = product.AvailableUntil,
        };
        db.EntitlementGrants.Add(grant);

        audit.Write(new AuditEvent("entitlement_grant.claimed", "entitlement_grants", grant.Id,
            ActorId: actorId, After: new { grant.Quantity, ProductId = productId }));

        await db.SaveChangesAsync(ct);

        var view = await BuildGrantViewAsync(grant.Id, ct);
        return view is null
            ? ServiceResult<EntitlementGrantView>.Fail("not_found")
            : ServiceResult<EntitlementGrantView>.Success(view);
    }

    public async Task<ServiceResult<IReadOnlyList<EntitlementGrantView>>> ListMineAsync(
        Guid userId, Guid? eventId, CancellationToken ct = default)
    {
        var rows = await GrantQuery()
            .Where(x => x.Grant.UserId == userId && (eventId == null || x.Product.EventId == eventId))
            .OrderByDescending(x => x.Grant.IssuedAt)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<EntitlementGrantView>>.Success(rows.Select(ToGrantView).ToList());
    }

    // ── gates ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Returns an error code when the caller may not configure this event's entitlements, null
    /// when they may. Existence is checked before authority so a caller with no standing cannot tell a
    /// private event from a missing one (D-018).</summary>
    private async Task<string?> GateForManageAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct)
    {
        var access = await authority.ResolveAsync(actorId, eventId, isAdmin, ct);
        if (!access.EventExists) return "not_found";
        if (!access.Can(EventPermission.ManageContent)) return access.HasStanding ? "forbidden" : "not_found";
        if (!await IsEnabledAsync(eventId, ct)) return "capability_disabled";
        return null;
    }

    /// <summary>Whether THIS event has the feature on.
    ///
    /// <para><b>Read from <c>event_capabilities</c>, not from <c>ICapabilityService.GetForEventAsync</c>.</b>
    /// That method resolves the archetype × mode <i>defaults</i> and deliberately does not consult the
    /// per-event table, so an Optional capability always resolves to Off there — asking it would make
    /// this feature permanently unreachable no matter what an organiser did. The per-event table is the
    /// record of what an event actually has: <c>MaterializeForEventAsync</c> stores only non-Off states,
    /// so a row at On or Required means enabled and its absence means off.</para>
    ///
    /// <para><b>Known gap.</b> Nothing writes an On row yet — per-event capability toggles are an unbuilt
    /// phase (see the comment in <c>MaterializeForEventAsync</c>). Until that lands, enabling this for an
    /// event is a direct insert. The organiser-facing toggle belongs with the Food &amp; Coupons dashboard
    /// (D-334 Phase 6) and is a prerequisite for the feature being usable in production.</para></summary>
    private async Task<bool> IsEnabledAsync(Guid eventId, CancellationToken ct)
        => await db.EventCapabilities.AsNoTracking().AnyAsync(
            c => c.EventId == eventId && c.CapabilitySlug == CapabilitySlug
                && (c.State == CapabilityState.On || c.State == CapabilityState.Required), ct);

    // ── projection ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Member-init, not a positional record: EF cannot translate a constructor call in a
    /// projection over a join, and materialising the whole grant set client-side to work around that
    /// would page the table into memory.</summary>
    private class GrantRow
    {
        public EntitlementGrant Grant { get; init; } = null!;
        public EntitlementProduct Product { get; init; } = null!;
        public Guid EventId { get; init; }
        public string EventTitle { get; init; } = null!;
    }

    private IQueryable<GrantRow> GrantQuery() =>
        from g in db.EntitlementGrants.AsNoTracking()
        join p in db.EntitlementProducts.AsNoTracking() on g.EntitlementProductId equals p.Id
        join e in db.Events.AsNoTracking() on p.EventId equals e.Id
        select new GrantRow { Grant = g, Product = p, EventId = e.Id, EventTitle = e.Title };

    private async Task<EntitlementGrantView?> BuildGrantViewAsync(Guid grantId, CancellationToken ct)
    {
        var row = await GrantQuery().FirstOrDefaultAsync(x => x.Grant.Id == grantId, ct);
        return row is null ? null : ToGrantView(row);
    }

    private static EntitlementGrantView ToGrantView(GrantRow row) => new(
        row.Grant.Id,
        row.Product.Id,
        row.EventId,
        row.EventTitle,
        row.Product.Name,
        row.Product.Kind.ToString(),
        row.Product.MealSlot.ToString(),
        row.Product.Delivery.ToString(),
        row.Grant.Quantity,
        row.Grant.RedeemedQuantity,
        row.Grant.Quantity - row.Grant.RedeemedQuantity,
        // Expiry is derived, never stored: a stored status needs a sweeper and is wrong between runs.
        // "Expired" is deliberately NOT a member of the stored enum — a stored expiry needs a
        // sweeper and is wrong between runs — so the derived state is a literal at the projection.
        Expired(row) ? "Expired" : row.Grant.Status.ToString(),
        row.Grant.SecureToken,
        row.Grant.CouponNumber,
        row.Grant.ExpiresAt,
        Deserialize(row.Product.RedemptionLocationsJson));

    private static bool Expired(GrantRow row)
    {
        if (row.Grant.Status is EntitlementGrantStatus.Cancelled or EntitlementGrantStatus.Refunded) return false;
        var until = row.Grant.ExpiresAt ?? row.Product.AvailableUntil;
        return until is { } u && u <= DateTime.UtcNow;
    }

    private static EntitlementProductView ToProductView(EntitlementProduct p, int issued, int redeemed) => new(
        p.Id, p.EventId, p.Kind.ToString(), p.MealSlot.ToString(), p.Name, p.CustomLabel, p.Description,
        p.ImageKey, p.PricePaise, p.Currency, p.Delivery.ToString(), p.Inclusion.ToString(),
        p.MaxPerParticipant, p.AllowsPartialRedemption, p.AvailableFrom, p.AvailableUntil,
        Deserialize(p.RedemptionLocationsJson), Deserialize(p.TagsJson), p.IsPublished, issued, redeemed);

    private async Task<(int Issued, int Redeemed)> CountsAsync(Guid productId, CancellationToken ct)
    {
        var rows = await db.EntitlementGrants.AsNoTracking()
            .Where(g => g.EntitlementProductId == productId)
            .Select(g => g.RedeemedQuantity)
            .ToListAsync(ct);
        return (rows.Count, rows.Sum());
    }

    private static IReadOnlyList<string> Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    // ── validation (D-334 §37) ─────────────────────────────────────────────────────────────────

    private static string? Validate(EntitlementProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return "name_required";
        if (input.PricePaise < 0) return "invalid_price";
        if (input.MaxPerParticipant < 1) return "invalid_max_per_participant";
        if (input.AvailableFrom is { } from && input.AvailableUntil is { } until && until <= from)
            return "invalid_validity_window";
        if (!TryParse<EntitlementKind>(input.Kind, out _)) return "invalid_kind";
        if (!TryParse<EntitlementDelivery>(input.Delivery, out _)) return "invalid_delivery";
        if (!TryParse<EntitlementInclusion>(input.Inclusion, out _)) return "invalid_inclusion";
        if (input.MealSlot is not null && !TryParse<MealSlot>(input.MealSlot, out _)) return "invalid_meal_slot";
        // A free-claim product that costs money is a contradiction the organiser should see now, not when
        // the first participant hits a payment wall on a button labelled "Claim".
        if (TryParse<EntitlementInclusion>(input.Inclusion, out var inclusion)
            && inclusion == EntitlementInclusion.FreeClaim && input.PricePaise != 0)
            return "free_claim_cannot_be_priced";
        return null;
    }

    private static bool TryParse<T>(string value, out T parsed) where T : struct
        => Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(typeof(T), parsed);

    private static EntitlementKind ParseKind(string v) => Enum.Parse<EntitlementKind>(v, true);
    private static EntitlementDelivery ParseDelivery(string v) => Enum.Parse<EntitlementDelivery>(v, true);
    private static EntitlementInclusion ParseInclusion(string v) => Enum.Parse<EntitlementInclusion>(v, true);
    private static MealSlot ParseMealSlot(string? v)
        => string.IsNullOrWhiteSpace(v) ? MealSlot.None : Enum.Parse<MealSlot>(v, true);

    /// <summary>The QR's payload. Same alphabet and length as <c>IdCard.VerifyCode</c> — one verification
    /// convention across cards, certificates and coupons — but 20 characters rather than 10, because this
    /// one authorises a spend rather than revealing a public fact, and it is the only thing standing
    /// between a guessed string and a free lunch.</summary>
    private static string NewSecureToken()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTVWXYZ0123456789";
        Span<byte> bytes = stackalloc byte[20];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(20, bytes.ToArray(), (span, b) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = alphabet[b[i] % alphabet.Length];
        });
    }
}
