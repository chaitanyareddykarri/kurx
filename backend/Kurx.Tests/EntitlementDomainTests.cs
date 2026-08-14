using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-334 Phase 1 — the entitlement schema and the one guarantee it has to make on its own.
///
/// <para><b>What is under test here is not a feature.</b> Phase 1 ships tables, a capability slug and a
/// concurrency contract; the services and endpoints come later. So these tests pin the two things a
/// later phase could silently break: that the counter cannot be over-spent by two racing counters, and
/// that every event which already exists is untouched by the feature's arrival.</para></summary>
public class EntitlementDomainTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public EntitlementDomainTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    private KurxDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<KurxDbContext>();

    /// <summary>The capability is available everywhere and ON nowhere, which is the whole optional-feature
    /// promise (D-334 §34, §42.1, §42.24).
    ///
    /// <para>Asserted as data rather than as a code path, because a code path can be edited into a default
    /// and a seeded Rule cannot be without this failing.</para></summary>
    [Fact]
    public async Task Capability_is_registered_but_enabled_for_no_event()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var capability = await db.Capabilities.SingleOrDefaultAsync(c => c.Slug == "entitlements");
        Assert.NotNull(capability);
        Assert.False(capability!.IsUniversal);          // universal would force it on every event

        // The matrix stores a row for EVERY (archetype, capability) pair including the negatives, so the
        // presence of rows says nothing — the Rule does. Two separate claims are being made here:
        //
        //   Optional in every archetype   → an organiser CAN turn it on (Unsupported would mean never)
        //   Required in none              → it is never turned on FOR them, which is the D-334 promise
        var rules = await db.Set<ArchetypeCapabilityDefault>()
            .Where(d => d.CapabilitySlug == "entitlements")
            .Select(d => d.Rule)
            .ToListAsync();

        Assert.NotEmpty(rules);
        Assert.All(rules, rule => Assert.Equal(CapabilityRule.Optional, rule));

        // And nothing has been switched on for a real event by the migration itself.
        var enabledSomewhere = await db.Set<EventCapability>()
            .CountAsync(c => c.CapabilitySlug == "entitlements");
        Assert.Equal(0, enabledSomewhere);
    }

    /// <summary>Two staff, one remaining lunch (D-334 §21, §42.8).
    ///
    /// <para>The guard is the conditional UPDATE, not application logic: both statements run, and the
    /// second matches no row because its WHERE clause re-evaluates the counter the first one moved. A
    /// read-modify-write would have both read 0, both write 1, and one lunch would be served twice.</para></summary>
    [Fact]
    public async Task Concurrent_redemption_cannot_exceed_the_granted_quantity()
    {
        var grantId = await SeedGrantAsync(quantity: 1);

        // Both "counters" attempt the same single unit at once.
        var results = await Task.WhenAll(
            RedeemAsync(grantId, 1),
            RedeemAsync(grantId, 1));

        Assert.Equal(1, results.Count(rowsAffected => rowsAffected == 1));   // exactly one winner
        Assert.Equal(1, results.Count(rowsAffected => rowsAffected == 0));   // exactly one refusal

        using var scope = _factory.Services.CreateScope();
        var grant = await Db(scope).EntitlementGrants.AsNoTracking().SingleAsync(g => g.Id == grantId);
        Assert.Equal(1, grant.RedeemedQuantity);        // never 2
    }

    /// <summary>Partial redemption arithmetic: three lunches, spend one, two remain (D-334 §13, §42.15).</summary>
    [Fact]
    public async Task Partial_redemption_leaves_the_remainder_spendable()
    {
        var grantId = await SeedGrantAsync(quantity: 3);

        Assert.Equal(1, await RedeemAsync(grantId, 1));
        Assert.Equal(1, await RedeemAsync(grantId, 2));
        // The fourth unit does not exist, and the counter is what says so.
        Assert.Equal(0, await RedeemAsync(grantId, 1));

        using var scope = _factory.Services.CreateScope();
        var grant = await Db(scope).EntitlementGrants.AsNoTracking().SingleAsync(g => g.Id == grantId);
        Assert.Equal(3, grant.RedeemedQuantity);
    }

    /// <summary>A product delivered as BOTH physical and digital is still one grant, so spending the
    /// paper coupon leaves nothing on the phone (D-334 §12, §42.6). Phase 1 can only assert the shape —
    /// one row, one counter — which is precisely the shape a later issuance service must not break.</summary>
    [Fact]
    public async Task Both_delivery_is_one_grant_not_two()
    {
        var grantId = await SeedGrantAsync(quantity: 1, delivery: EntitlementDelivery.Both);

        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var grant = await db.EntitlementGrants.AsNoTracking().SingleAsync(g => g.Id == grantId);

        var siblings = await db.EntitlementGrants.AsNoTracking()
            .CountAsync(g => g.EntitlementProductId == grant.EntitlementProductId && g.UserId == grant.UserId);
        Assert.Equal(1, siblings);
    }

    /// <summary>The retry guard (D-334 §21): a scanner re-sending the same confirmation records one
    /// redemption, not two.</summary>
    [Fact]
    public async Task Duplicate_idempotency_key_is_refused_by_the_database()
    {
        var grantId = await SeedGrantAsync(quantity: 2);
        var key = $"scan-{Guid.NewGuid()}";

        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (_, staffId) = await SeedEventAndUserAsync(db);

        db.EntitlementRedemptions.Add(new EntitlementRedemption
        {
            EntitlementGrantId = grantId, Quantity = 1, RedeemedByUserId = staffId, IdempotencyKey = key
        });
        await db.SaveChangesAsync();

        db.EntitlementRedemptions.Add(new EntitlementRedemption
        {
            EntitlementGrantId = grantId, Quantity = 1, RedeemedByUserId = staffId, IdempotencyKey = key
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    /// <summary>The conditional decrement itself (D-240/D-261: shared counters move in SQL only).
    /// Returns rows-affected, which IS the outcome — 1 redeemed, 0 refused.</summary>
    private async Task<int> RedeemAsync(Guid grantId, int quantity)
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).EntitlementGrants
            .Where(g => g.Id == grantId && g.RedeemedQuantity + quantity <= g.Quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.RedeemedQuantity, g => g.RedeemedQuantity + quantity));
    }

    /// <summary>A real user via the real auth flow, and an event they own — the suite's standing rule is
    /// that persistence is never mocked, so the FKs these rows satisfy are the actual ones.</summary>
    private async Task<(Guid EventId, Guid UserId)> SeedEventAndUserAsync(KurxDbContext db)
    {
        var phone = $"+9198{Random.Shared.NextInt64(10_000_000, 99_999_999)}";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        var userId = (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user_id").GetGuid();

        var category = new EventCategory
        {
            Level = CategoryLevel.Category, Name = "Ent", Slug = $"ent-{Guid.NewGuid():N}"[..12]
        };
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();

        var orgId = _factory.SeedVerifiedOrg(userId, $"Ent Org {Guid.NewGuid():N}"[..18]);
        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = userId, CategoryId = category.Id,
            Title = "Ent Event", Slug = $"ent-{Guid.NewGuid():N}"[..16],
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(2), EndsAt = DateTime.UtcNow.AddDays(3),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (ev.Id, userId);
    }

    private async Task<Guid> SeedGrantAsync(int quantity, EntitlementDelivery delivery = EntitlementDelivery.Digital)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var (eventId, userId) = await SeedEventAndUserAsync(db);

        var product = new EntitlementProduct
        {
            EventId = eventId, Kind = EntitlementKind.Meal, MealSlot = MealSlot.Lunch,
            Name = "Lunch", PricePaise = 10_000, Delivery = delivery,
            AllowsPartialRedemption = true, IsPublished = true
        };
        db.EntitlementProducts.Add(product);

        var grant = new EntitlementGrant
        {
            EntitlementProductId = product.Id, UserId = userId, Quantity = quantity,
            SecureToken = Guid.NewGuid().ToString("N")[..20]
        };
        db.EntitlementGrants.Add(grant);
        await db.SaveChangesAsync();
        return grant.Id;
    }
}
