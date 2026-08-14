using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-334 Phase 3 — meal entitlement on the ID card (§8, §38).
///
/// <para>The claim under test is that the printed quantity is <b>never</b> a stored or client-supplied
/// number. It is read from the same grants redemption decrements, at the moment the card is generated,
/// so an allocation change shows up on regeneration and a stale value cannot become the authority.</para></summary>
public class IdCardMealDisplayTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public IdCardMealDisplayTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    /// <summary>Off by default, and a card that shows no meals renders exactly as it did before this
    /// feature existed — the field is simply absent.</summary>
    [Fact]
    public async Task Meal_display_is_off_unless_the_issuer_asks_for_it()
    {
        var ctx = await SeedAsync();
        await GrantMealsAsync(ctx, MealSlot.Lunch, 1);

        var card = await IssueAsync(ctx, showMeals: false);
        Assert.False(card.ShowMealInfo);
        Assert.Null(card.MealLine);
    }

    /// <summary>The compact form of §8, ordered as a day rather than alphabetically.</summary>
    [Fact]
    public async Task The_line_reads_as_a_day_and_counts_the_live_grants()
    {
        var ctx = await SeedAsync();
        await GrantMealsAsync(ctx, MealSlot.Breakfast, 1);
        await GrantMealsAsync(ctx, MealSlot.Lunch, 1);
        await GrantMealsAsync(ctx, MealSlot.Dinner, 1);
        await GrantMealsAsync(ctx, MealSlot.Snack, 2);

        var card = await IssueAsync(ctx, showMeals: true);
        Assert.True(card.ShowMealInfo);
        Assert.Equal("B1 · L1 · S2 · D1", card.MealLine);
    }

    /// <summary>§38, the whole point: change the allocation, and the regenerated card says the new
    /// number. Nothing was written to the card when the grant changed.</summary>
    [Fact]
    public async Task Changing_the_allocation_changes_what_a_regenerated_card_shows()
    {
        var ctx = await SeedAsync();
        var grantId = await GrantMealsAsync(ctx, MealSlot.Lunch, 1);

        var before = await IssueAsync(ctx, showMeals: true);
        Assert.Equal("L1", before.MealLine);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var grant = await db.EntitlementGrants.FirstAsync(g => g.Id == grantId);
            grant.Quantity = 2;
            await db.SaveChangesAsync();
        }

        var after = await Svc(s => s.GetAsync(ctx.OwnerId, before.Id, isAdmin: false));
        Assert.Equal("L2", after.Value!.MealLine);
    }

    /// <summary>A cancelled entitlement is not an entitlement, so it prints nothing.</summary>
    [Fact]
    public async Task Cancelled_and_refunded_grants_are_not_printed()
    {
        var ctx = await SeedAsync();
        var lunch = await GrantMealsAsync(ctx, MealSlot.Lunch, 1);
        await GrantMealsAsync(ctx, MealSlot.Dinner, 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var grant = await db.EntitlementGrants.FirstAsync(g => g.Id == lunch);
            grant.Status = EntitlementGrantStatus.Refunded;
            await db.SaveChangesAsync();
        }

        var card = await IssueAsync(ctx, showMeals: true);
        Assert.Equal("D1", card.MealLine);
    }

    /// <summary>A card asserting no event has no meals to draw from, so the switch is refused rather
    /// than silently wired to nothing.
    ///
    /// <para>Under D-335 every card is issued against an event, so this state can no longer be reached
    /// through <c>IssueAsync</c> — but rows written before it still carry a null <c>EventId</c>, and the
    /// guard exists for exactly those. The row is therefore inserted directly: the test is now also the
    /// evidence that a pre-D-335 card stays readable and behaves sanely.</para></summary>
    [Fact]
    public async Task A_legacy_card_with_no_event_cannot_show_meals()
    {
        var ctx = await SeedAsync();

        var legacyId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.IdCards.Add(new Kurx.Domain.Entities.IdCard
            {
                Id = legacyId,
                OrgId = ctx.OrgId,
                UserId = ctx.HolderId,
                EventId = null,                       // the pre-D-335 shape
                VerifyCode = "LEG" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant(),
                CardNumber = "2025/9999",
                IssuedBy = ctx.OwnerId,
            });
            await db.SaveChangesAsync();
        }

        var toggled = await Svc(s => s.SetMealDisplayAsync(ctx.OwnerId, legacyId, true, isAdmin: true));

        Assert.False(toggled.Ok);
        Assert.Equal("not_an_event_card", toggled.Error);
    }

    /// <summary>The toggle needs verified membership of the ISSUING org, so an outsider cannot reach it.
    ///
    /// <para><b>Note what this does not claim.</b> <c>CanManageAsync</c> admits any verified member of the
    /// issuing org, and D-331 requires the holder to be one — so a holder CAN toggle their own card. That
    /// is inherited D-331 authorization, not something this feature chose, and it is safe here only
    /// because the quantities are read from grants rather than typed: the worst a holder achieves is
    /// printing a true number they would rather hide. It would NOT be safe for a field they could set.</para></summary>
    [Fact]
    public async Task Someone_outside_the_issuing_org_cannot_switch_the_meal_panel_on()
    {
        var ctx = await SeedAsync();
        var card = await IssueAsync(ctx, showMeals: false);
        var strangerId = await NewUserAsync();

        var asStranger = await Svc(s => s.SetMealDisplayAsync(strangerId, card.Id, true, isAdmin: false));
        Assert.False(asStranger.Ok);
        Assert.Equal("forbidden", asStranger.Error);
    }

    /// <summary>Turning it on later works, and reads the quantities that exist then.</summary>
    [Fact]
    public async Task An_issuer_can_switch_it_on_after_issue()
    {
        var ctx = await SeedAsync();
        await GrantMealsAsync(ctx, MealSlot.Lunch, 3);
        var card = await IssueAsync(ctx, showMeals: false);
        Assert.Null(card.MealLine);

        var toggled = await Svc(s => s.SetMealDisplayAsync(ctx.OwnerId, card.Id, true, isAdmin: false));
        Assert.True(toggled.Ok);
        Assert.Equal("L3", toggled.Value!.MealLine);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private record Ctx(Guid OwnerId, Guid HolderId, Guid OrgId, Guid EventId);

    private async Task<T> Svc<T>(Func<IIdCardService, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IIdCardService>());
    }

    private async Task<IdCardView> IssueAsync(Ctx ctx, bool showMeals)
    {
        var input = new IdCardIssueInput(ctx.HolderId, "S1", "CSE", "BTech", "3",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            ctx.EventId, null, showMeals);

        var r = await Svc(s => s.IssueAsync(ctx.OwnerId, ctx.EventId, input, isAdmin: false));
        Assert.True(r.Ok, $"issue failed: {r.Error}");
        return r.Value!;
    }

    /// <summary>One meal product and one grant of it to the holder.</summary>
    private async Task<Guid> GrantMealsAsync(Ctx ctx, MealSlot slot, int quantity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var product = new EntitlementProduct
        {
            EventId = ctx.EventId, Kind = EntitlementKind.Meal, MealSlot = slot,
            Name = slot.ToString(), PricePaise = 0, IsPublished = true,
        };
        db.EntitlementProducts.Add(product);

        var grant = new EntitlementGrant
        {
            EntitlementProductId = product.Id, UserId = ctx.HolderId, Quantity = quantity,
            Status = EntitlementGrantStatus.Issued, SecureToken = Guid.NewGuid().ToString("N")[..20],
        };
        db.EntitlementGrants.Add(grant);
        await db.SaveChangesAsync();
        return grant.Id;
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

    private async Task<Ctx> SeedAsync()
    {
        var ownerId = await NewUserAsync();
        var holderId = await NewUserAsync();
        var orgId = _factory.SeedVerifiedOrg(ownerId, $"Meal Org {Guid.NewGuid():N}"[..18]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // The holder must be a verified member of the issuing org — D-331 refuses to issue otherwise.
        db.Memberships.Add(new Membership
        {
            OrgId = orgId, UserId = holderId, Role = OrgRole.Staff, IsVerified = true
        });

        var category = new EventCategory
        {
            Level = CategoryLevel.Category, Name = "Meal", Slug = $"meal-{Guid.NewGuid():N}"[..12]
        };
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = ownerId, CategoryId = category.Id,
            Title = "Meal Event", Slug = $"meal-{Guid.NewGuid():N}"[..16],
            ShortCode = $"M{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(2), EndsAt = DateTime.UtcNow.AddDays(3),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);

        // D-335 — issuance now requires the event's creator to be verified, satisfied here through the
        // same approved proofs TrustService reads rather than by a bypass flag (the suite runs with
        // IDENTITY_VERIFICATION_BYPASS unset).
        db.UserIdentities.Add(new UserIdentity
        {
            UserId = ownerId,
            Level = IdentityLevel.Bank,
            Status = IdentityStatus.Approved,
            GovtIdStatus = IdentityStatus.Approved,
            PanStatus = IdentityStatus.Approved,
            BankStatus = IdentityStatus.Approved,
            PennyDropStatus = PennyDropStatus.Passed,
            BankNameMatch = NameMatchStatus.Match,
        });

        await db.SaveChangesAsync();

        return new Ctx(ownerId, holderId, orgId, ev.Id);
    }
}
