using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-265 coupons.
///
/// <para>The load-bearing test here is <see cref="Concurrent_redemption_never_exceeds_the_cap"/>. A
/// coupon that over-redeems under parallel checkout is a money bug, and the read-modify-write shape
/// that causes it passes every sequential test — which is exactly why D-240 and D-261 exist.</para></summary>
public class CouponTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CouponTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Coupons", Slug = "coupons-d265" };
            db.EventCategories.Add(category);
            db.SaveChanges();
            _categoryId = category.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    private async Task<(HttpClient Client, Guid OrgId)> OwnerWithOrgAsync(string phone, string orgName)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, _factory.SeedVerifiedOrgForClient(client, orgName));
    }

    private async Task<Guid> EventAsync(HttpClient client, Guid orgId, string title)
    {
        var res = await client.CreateEventAsync(orgId, new
        {
            title,
            categoryId = _categoryId,
            city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(10),
            endsAt = DateTime.UtcNow.AddDays(10).AddHours(3),
        });
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    // ── CRUD + authz ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Owner_creates_a_percent_coupon_and_lists_it()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000901", "Coupon Org");
        var eventId = await EventAsync(client, orgId, "Coupon Event");

        var res = await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "welcome10", kind = "Percent", percent = 10m, maxRedemptions = 100 });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // Codes normalise to uppercase — nobody types a coupon in the case the organiser imagined.
        Assert.Equal("WELCOME10", (await Json(res)).GetProperty("code").GetString());

        var list = await Json(await client.GetAsync($"/v1/events/{eventId}/coupons"));
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task A_non_member_gets_not_found_never_forbidden()
    {
        var (owner, orgId) = await OwnerWithOrgAsync("9700000902", "Coupon Owner Org");
        var eventId = await EventAsync(owner, orgId, "Private Coupons");

        var (stranger, _) = await OwnerWithOrgAsync("9700000903", "Stranger Coupon Org");
        var res = await stranger.GetAsync($"/v1/events/{eventId}/coupons");

        // D-018: an event's existence is not leaked by its coupon endpoints either.
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_duplicate_code_on_the_same_event_is_rejected()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000904", "Dup Coupon Org");
        var eventId = await EventAsync(client, orgId, "Dup Coupons");
        var body = new { code = "SAVE", kind = "Flat", valuePaise = 5000L };

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons", body)).StatusCode);
        var second = await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("coupon_code_taken", (await Json(second)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task The_same_code_may_exist_on_two_different_events()
    {
        // Uniqueness is per event, not global — two organisers both wanting WELCOME10 is normal.
        var (client, orgId) = await OwnerWithOrgAsync("9700000905", "Two Event Org");
        var first = await EventAsync(client, orgId, "First Coupon Event");
        var second = await EventAsync(client, orgId, "Second Coupon Event");
        var body = new { code = "SHARED", kind = "Percent", percent = 5m };

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/v1/events/{first}/coupons", body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/v1/events/{second}/coupons", body)).StatusCode);
    }

    [Theory]
    [InlineData("Percent", null, "invalid_percent")]
    [InlineData("Flat", null, "invalid_value")]
    public async Task A_coupon_that_would_discount_nothing_is_rejected(string kind, decimal? percent, string expected)
    {
        // Otherwise it looks valid at checkout and takes nothing off.
        var (client, orgId) = await OwnerWithOrgAsync($"97000009{(kind == "Percent" ? 6 : 7)}0", $"Invalid {kind} Org");
        var eventId = await EventAsync(client, orgId, $"Invalid {kind}");

        var res = await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = $"BAD{kind}", kind, percent, valuePaise = (long?)null });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(expected, (await Json(res)).GetProperty("error").GetString());
    }

    // ── Quote ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quote_prices_a_basket_without_consuming_a_redemption()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000908", "Quote Org");
        var eventId = await EventAsync(client, orgId, "Quote Event");
        await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "TEN", kind = "Percent", percent = 10m, maxRedemptions = 5 });

        for (var i = 0; i < 3; i++)
        {
            var q = await Json(await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons/quote",
                new { code = "ten", orderTotalPaise = 100_000L }));
            Assert.Equal(10_000L, q.GetProperty("discount_paise").GetInt64());
            Assert.Equal(90_000L, q.GetProperty("payable_paise").GetInt64());
        }

        // Quoting three times must not have burned three of the five slots.
        var list = await Json(await client.GetAsync($"/v1/events/{eventId}/coupons"));
        Assert.Equal(0, list[0].GetProperty("redeemed_count").GetInt32());
    }

    [Fact]
    public async Task A_discount_larger_than_the_basket_never_yields_a_negative_payable()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000909", "Clamp Org");
        var eventId = await EventAsync(client, orgId, "Clamp Event");
        await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "HUGE", kind = "Flat", valuePaise = 500_000L });

        var q = await Json(await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons/quote",
            new { code = "HUGE", orderTotalPaise = 10_000L }));

        Assert.Equal(10_000L, q.GetProperty("discount_paise").GetInt64());
        Assert.Equal(0L, q.GetProperty("payable_paise").GetInt64());
    }

    [Fact]
    public async Task A_basket_below_the_minimum_is_refused()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000910", "MinOrder Org");
        var eventId = await EventAsync(client, orgId, "MinOrder Event");
        await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "BIGSPEND", kind = "Flat", valuePaise = 5_000L, minOrderPaise = 100_000L });

        var res = await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons/quote",
            new { code = "BIGSPEND", orderTotalPaise = 50_000L });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("coupon_min_order_not_met", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task An_expired_or_unstarted_coupon_is_refused()
    {
        var (client, orgId) = await OwnerWithOrgAsync("9700000911", "Window Org");
        var eventId = await EventAsync(client, orgId, "Window Event");
        var now = DateTime.UtcNow;

        await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "PAST", kind = "Percent", percent = 10m, validUntil = now.AddDays(-1) });
        await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "FUTURE", kind = "Percent", percent = 10m, validFrom = now.AddDays(1) });

        var expired = await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons/quote",
            new { code = "PAST", orderTotalPaise = 100_000L });
        var early = await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons/quote",
            new { code = "FUTURE", orderTotalPaise = 100_000L });

        Assert.Equal("coupon_expired", (await Json(expired)).GetProperty("error").GetString());
        Assert.Equal("coupon_not_started", (await Json(early)).GetProperty("error").GetString());
    }

    // ── Redemption + the race ────────────────────────────────────────────────

    [Fact]
    public async Task Concurrent_redemption_never_exceeds_the_cap()
    {
        // THE test for this feature. `RedeemedCount` is claimed with a conditional UPDATE whose WHERE
        // carries the cap, so the database decides who gets the last slot. The read-modify-write shape
        // this guards against passes every sequential test and over-redeems the moment two buyers
        // check out at once.
        var (client, orgId) = await OwnerWithOrgAsync("9700000912", "Race Org");
        var eventId = await EventAsync(client, orgId, "Race Event");
        var created = await Json(await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "LIMITED3", kind = "Flat", valuePaise = 1_000L, maxRedemptions = 3, maxPerUser = 50 }));
        var couponId = created.GetProperty("id").GetGuid();

        // A SCOPE PER ATTEMPT is load-bearing. Reusing one service would share one DbContext, which EF
        // refuses to use concurrently — the test would fail on a threading error without ever
        // exercising the database race. Each request in production gets its own scope and its own
        // connection, and that is the only arrangement where the cap is actually under contention.
        //
        // Each attempt uses a DISTINCT order id so the idempotency short-circuit cannot mask the race:
        // every one of the twelve genuinely tries to claim a slot.
        var attempts = Enumerable.Range(0, 12).Select(async _ =>
        {
            using var s = _factory.Services.CreateScope();
            return await s.ServiceProvider.GetRequiredService<ICouponService>()
                .RedeemAsync(null, eventId, "LIMITED3", Guid.NewGuid(), 100_000L);
        }).ToArray();
        var results = await Task.WhenAll(attempts);

        Assert.Equal(3, results.Count(r => r.Ok));
        Assert.All(results.Where(r => !r.Ok), r => Assert.Equal("coupon_exhausted", r.Error));

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        var coupon = await db.Coupons.AsNoTracking().SingleAsync(c => c.Id == couponId);
        Assert.Equal(3, coupon.RedeemedCount);
        Assert.Equal(3, await db.CouponRedemptions.CountAsync(r => r.CouponId == couponId));
    }

    [Fact]
    public async Task Redeeming_twice_for_the_same_order_claims_only_one_slot()
    {
        // A retried checkout must not double-count against the cap.
        var (client, orgId) = await OwnerWithOrgAsync("9700000913", "Idempotent Org");
        var eventId = await EventAsync(client, orgId, "Idempotent Event");
        var created = await Json(await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "ONCE", kind = "Flat", valuePaise = 2_000L, maxRedemptions = 10 }));
        var couponId = created.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICouponService>();
        var orderId = Guid.NewGuid();

        var first = await svc.RedeemAsync(null, eventId, "ONCE", orderId, 100_000L);
        var second = await svc.RedeemAsync(null, eventId, "ONCE", orderId, 100_000L);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(first.Value!.DiscountPaise, second.Value!.DiscountPaise);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, (await db.Coupons.AsNoTracking().SingleAsync(c => c.Id == couponId)).RedeemedCount);
    }

    [Fact]
    public async Task A_redeemed_coupon_is_deactivated_rather_than_deleted()
    {
        // Its redemptions are money history; deleting the row would orphan them.
        var (client, orgId) = await OwnerWithOrgAsync("9700000914", "Delete Org");
        var eventId = await EventAsync(client, orgId, "Delete Event");
        var created = await Json(await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "USED", kind = "Flat", valuePaise = 1_000L }));
        var couponId = created.GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ICouponService>();
            Assert.True((await svc.RedeemAsync(null, eventId, "USED", Guid.NewGuid(), 50_000L)).Ok);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/v1/coupons/{couponId}")).StatusCode);

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<KurxDbContext>();
        var coupon = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(c => c.Id == couponId);
        Assert.NotNull(coupon);
        Assert.False(coupon!.IsActive);
    }

    [Fact]
    public async Task The_cap_cannot_be_lowered_below_what_has_already_been_redeemed()
    {
        // Otherwise the counter reads as over-redeemed forever.
        var (client, orgId) = await OwnerWithOrgAsync("9700000915", "Lower Cap Org");
        var eventId = await EventAsync(client, orgId, "Lower Cap Event");
        var created = await Json(await client.PostAsJsonAsync($"/v1/events/{eventId}/coupons",
            new { code = "CAP", kind = "Flat", valuePaise = 1_000L, maxRedemptions = 10 }));
        var couponId = created.GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ICouponService>();
            for (var i = 0; i < 3; i++)
                await svc.RedeemAsync(null, eventId, "CAP", Guid.NewGuid(), 50_000L);
        }

        var res = await client.PatchAsJsonAsync($"/v1/coupons/{couponId}", new { maxRedemptions = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("max_below_redeemed", (await Json(res)).GetProperty("error").GetString());
    }
}
