using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-334 Phase 2 — the two gates, and the free-claim door.
///
/// <para>The happy path is the least interesting thing here. What is worth pinning is what the service
/// REFUSES: an event that never enabled the capability, a caller with no authority over one that did,
/// and a priced product reached through the door marked "free".</para></summary>
public class EntitlementServiceTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public EntitlementServiceTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    private static EntitlementProductInput Lunch(string inclusion = "SeparatePurchase", long price = 10_000) =>
        new("Meal", "Lunch", "Lunch", null, "Rice and dal", null, price, "Both", inclusion,
            MaxPerParticipant: 2, AllowsPartialRedemption: true, null, null, ["counter-1"], ["veg"]);

    /// <summary>An event that never turned the feature on has no entitlement surface — not an empty one.
    /// This is the optional-feature promise at the service boundary (D-334 §34, §42.1).</summary>
    [Fact]
    public async Task Capability_off_means_the_event_has_no_entitlement_surface()
    {
        var (ownerId, eventId) = await SeedEventAsync(enableCapability: false);

        var created = await Svc(s => s.CreateProductAsync(ownerId, eventId, Lunch(), isAdmin: false));
        Assert.False(created.Ok);
        Assert.Equal("capability_disabled", created.Error);

        // And the read is a 404-shaped refusal too, so a caller cannot infer the feature exists.
        var listed = await Svc(s => s.ListForEventAsync(ownerId, eventId, isAdmin: false));
        Assert.False(listed.Ok);
        Assert.Equal("not_found", listed.Error);
    }

    /// <summary>A stranger cannot configure someone else's event, and is not told it exists (D-018).</summary>
    [Fact]
    public async Task A_caller_without_authority_cannot_create_a_product()
    {
        var (_, eventId) = await SeedEventAsync(enableCapability: true);
        var strangerId = await NewUserAsync();

        var created = await Svc(s => s.CreateProductAsync(strangerId, eventId, Lunch(), isAdmin: false));
        Assert.False(created.Ok);
        Assert.Equal("not_found", created.Error);
    }

    /// <summary>Created unpublished, and invisible to participants until the organiser says otherwise.</summary>
    [Fact]
    public async Task A_product_is_created_unpublished_and_hidden_until_published()
    {
        var (ownerId, eventId) = await SeedEventAsync(enableCapability: true);

        var created = await Svc(s => s.CreateProductAsync(ownerId, eventId, Lunch(), isAdmin: false));
        Assert.True(created.Ok);
        Assert.False(created.Value!.IsPublished);

        var asOwner = await Svc(s => s.ListForEventAsync(ownerId, eventId, isAdmin: false));
        Assert.Single(asOwner.Value!);          // the organiser sees their own draft

        var published = await Svc(s => s.SetPublishedAsync(ownerId, created.Value.Id, true, isAdmin: false));
        Assert.True(published.Ok);
        Assert.True(published.Value!.IsPublished);
    }

    /// <summary>The free door refuses anything priced, even when the organiser mislabelled it — the
    /// inclusion mode and the price are checked independently (D-334 §14, §37).</summary>
    [Fact]
    public async Task Claiming_refuses_a_product_that_is_not_free()
    {
        var (ownerId, eventId) = await SeedEventAsync(enableCapability: true);
        var created = await Svc(s => s.CreateProductAsync(ownerId, eventId, Lunch(price: 10_000), isAdmin: false));
        await Svc(s => s.SetPublishedAsync(ownerId, created.Value!.Id, true, isAdmin: false));

        var claim = await Svc(s => s.ClaimFreeAsync(ownerId, created.Value!.Id, 1));
        Assert.False(claim.Ok);
        Assert.Equal("not_claimable", claim.Error);     // SeparatePurchase, so the free door is shut
    }

    /// <summary>A free product cannot be defined with a price at all — the contradiction is refused at
    /// definition rather than surfacing as a payment wall on a button labelled "Claim".</summary>
    [Fact]
    public async Task A_free_claim_product_cannot_carry_a_price()
    {
        var (ownerId, eventId) = await SeedEventAsync(enableCapability: true);

        var created = await Svc(s => s.CreateProductAsync(
            ownerId, eventId, Lunch(inclusion: "FreeClaim", price: 5_000), isAdmin: false));

        Assert.False(created.Ok);
        Assert.Equal("free_claim_cannot_be_priced", created.Error);
    }

    /// <summary>The whole free path, and the per-participant cap that bounds it (D-334 §37).</summary>
    [Fact]
    public async Task Claiming_a_free_product_issues_one_grant_and_enforces_the_cap()
    {
        var (ownerId, eventId) = await SeedEventAsync(enableCapability: true);
        var created = await Svc(s => s.CreateProductAsync(
            ownerId, eventId, Lunch(inclusion: "FreeClaim", price: 0), isAdmin: false));
        var productId = created.Value!.Id;
        await Svc(s => s.SetPublishedAsync(ownerId, productId, true, isAdmin: false));

        var first = await Svc(s => s.ClaimFreeAsync(ownerId, productId, 2));
        Assert.True(first.Ok);
        Assert.Equal(2, first.Value!.Quantity);
        Assert.Equal(2, first.Value.RemainingQuantity);
        Assert.Equal(20, first.Value.SecureToken.Length);       // unguessable, not the printed number

        // MaxPerParticipant is 2 and they already hold 2, so the next one is refused.
        var second = await Svc(s => s.ClaimFreeAsync(ownerId, productId, 1));
        Assert.False(second.Ok);
        Assert.Equal("limit_exceeded", second.Error);

        var mine = await Svc(s => s.ListMineAsync(ownerId, eventId));
        Assert.Single(mine.Value!);
        Assert.Equal("Issued", mine.Value![0].Status);
    }

    /// <summary>Counters are the organiser's business. A participant browsing the menu is not told how
    /// many lunches have sold — the field stays in the shape and reads zero.</summary>
    [Fact]
    public async Task Participants_do_not_see_issuance_counters()
    {
        var (ownerId, eventId) = await SeedEventAsync(enableCapability: true);
        var created = await Svc(s => s.CreateProductAsync(
            ownerId, eventId, Lunch(inclusion: "FreeClaim", price: 0), isAdmin: false));
        await Svc(s => s.SetPublishedAsync(ownerId, created.Value!.Id, true, isAdmin: false));
        await Svc(s => s.ClaimFreeAsync(ownerId, created.Value!.Id, 1));

        var asOwner = await Svc(s => s.ListForEventAsync(ownerId, eventId, isAdmin: false));
        Assert.Equal(1, asOwner.Value![0].IssuedCount);

        // A participant with standing sees the product and a zeroed counter.
        var participantId = await NewParticipantAsync(eventId);
        var asParticipant = await Svc(s => s.ListForEventAsync(participantId, eventId, isAdmin: false));
        Assert.Single(asParticipant.Value!);
        Assert.Equal(0, asParticipant.Value![0].IssuedCount);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private async Task<T> Svc<T>(Func<IEntitlementService, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IEntitlementService>());
    }

    private async Task<Guid> NewUserAsync()
    {
        var phone = $"+9198{Random.Shared.NextInt64(10_000_000, 99_999_999)}";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        return (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user_id").GetGuid();
    }

    /// <summary>Standing on the event without authority over it — the Participant rung, reached here by a
    /// programme participation rather than a ticket so the helper needs no order.</summary>
    private async Task<Guid> NewParticipantAsync(Guid eventId)
    {
        var userId = await NewUserAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // The audience floor of D-272: an ACTIVE person participation is Participant standing, the same
        // rung a live ticket reaches. Using a participation keeps the helper free of an order.
        db.EventParticipants.Add(new EventParticipant
        {
            EventId = eventId,
            SubjectType = ParticipantSubjectType.Person,
            SubjectId = userId,
            RoleSlug = "volunteer",
            State = ParticipantState.Active,
        });
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task<(Guid OwnerId, Guid EventId)> SeedEventAsync(bool enableCapability)
    {
        var ownerId = await NewUserAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var category = new EventCategory
        {
            Level = CategoryLevel.Category, Name = "Ent", Slug = $"ent-{Guid.NewGuid():N}"[..12]
        };
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();

        var orgId = _factory.SeedVerifiedOrg(ownerId, $"Ent Org {Guid.NewGuid():N}"[..18]);
        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = ownerId, CategoryId = category.Id,
            Title = "Ent Event", Slug = $"ent-{Guid.NewGuid():N}"[..16],
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(2), EndsAt = DateTime.UtcNow.AddDays(3),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);

        // Off is the absence of a row, so enabling is an insert and disabling is simply not doing one.
        if (enableCapability)
        {
            db.Set<EventCapability>().Add(new EventCapability
            {
                EventId = ev.Id, CapabilitySlug = "entitlements", State = CapabilityState.On,
            });
        }

        await db.SaveChangesAsync();
        return (ownerId, ev.Id);
    }
}
