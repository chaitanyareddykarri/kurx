using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-363 — an event that has carried commerce or attendance is never deleted.
///
/// <para><b>The route this closes.</b> Deleting an event is a HARD delete (<c>db.Events.Remove</c>) and
/// 46 tables cascade from <c>events</c> — orders, tickets, registrations, certificates, passes,
/// credentials, admissions. <c>DeleteDraftAsync</c> checked only <c>Status == Draft</c>, and Draft is
/// reachable from Published through <c>unpublish</c>. So this worked:</para>
///
/// <code>Published (with orders) → unpublish → Draft → delete</code>
///
/// <para>…and took every order, ticket and registration with it, silently and unrecoverably. This was
/// not hypothetical: the dev database was found holding a Draft event with an order already on it.</para>
///
/// <para>Both ends are tested, because a guard on the one path you thought of holds only until someone
/// adds a second path.</para></summary>
public class LifecycleDeletionGuardTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public LifecycleDeletionGuardTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Guard", Slug = "guard-del" };
            db.EventCategories.Add(cat);
            db.SaveChanges();
            _categoryId = cat.Id;
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify",
            new { phone, code = _factory.WhatsApp.LastOtpFor(phone) }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    /// <summary>A published event owned by the caller — the state from which the route started.</summary>
    private async Task<(Guid OrgId, Guid EventId)> PublishedAsync(HttpClient owner, string title)
    {
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Guard {Guid.NewGuid():N}"[..18], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title, description = "An event whose deletion is under test.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
            visibility = "Listed",
        }));
        var id = ev.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(id);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "publish" });
        return (orgId, id);
    }

    private async Task<Guid> SeedTicketTypeAsync(Guid eventId, long pricePaise = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var tt = new TicketType
        {
            EventId = eventId, Name = "Entry", PricePaise = pricePaise, Quantity = 50,
            SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(29),
        };
        db.TicketTypes.Add(tt);
        await db.SaveChangesAsync();
        return tt.Id;
    }

    /// <summary>Writes the one row that makes an event un-deletable. Seeded rather than bought so the
    /// test states its precondition instead of depending on the whole checkout path.</summary>
    private async Task SeedOrderAsync(Guid eventId, Guid ticketTypeId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var order = new Order
        {
            EventId = eventId, TicketTypeId = ticketTypeId, UserId = userId,
            Status = OrderStatus.Paid, AmountPaise = 50_000, Currency = "INR",
        };
        db.Orders.Add(order);
        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id, TicketTypeId = ticketTypeId, Qty = 1, UnitPricePaise = 50_000,
        });
        await db.SaveChangesAsync();
    }

    // ── The exit: delete ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_that_has_never_sold_anything_still_deletes()
    {
        // The guard must not swallow the ordinary case: abandoning a draft nobody touched is exactly
        // what this endpoint is for.
        var (owner, _) = await LoginAsync("9950000001");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Guard {Guid.NewGuid():N}"[..18], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Guard Untouched Draft", description = "Nothing ever happened to this.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
        }));
        var id = ev.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK,
            (await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{id}")).StatusCode);
    }

    /// <summary>D-364 — deleting is SOFT, and D-025 always said so. The row survives; every read path
    /// hides it. Asserted through the API rather than on the column, because "the column got set" is not
    /// the claim — "nobody can reach it any more" is.</summary>
    [Fact]
    public async Task Deleting_a_draft_hides_it_everywhere_without_destroying_the_row()
    {
        var (owner, _) = await LoginAsync("9950000008");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Guard {Guid.NewGuid():N}"[..18], "Company");
        var ev = await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Guard Soft Delete", description = "This one gets deleted.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
        }));
        var id = ev.GetProperty("id").GetGuid();
        var slug = ev.GetProperty("slug").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{id}")).StatusCode);

        // Gone from every read, including its OWNER's — a delete the deleter can still see is not a delete.
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/v1/orgs/{orgId}/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/v1/events/{slug}")).StatusCode);
        var mine = await Json(await owner.GetAsync("/v1/me/events"));
        Assert.DoesNotContain(id.ToString(), mine.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // The default query cannot see it…
        Assert.False(await db.Events.AnyAsync(e => e.Id == id));
        // …and the row is still there, stamped. This is the whole difference from the hard delete that
        // used to cascade through 46 tables.
        var row = await db.Events.IgnoreQueryFilters().FirstAsync(e => e.Id == id);
        Assert.NotNull(row.DeletedAt);
    }

    /// <summary>The slug a deleted event held is released. The unique index is partial for this reason —
    /// without it, deleting an event and recreating it under the same title collides on a row nobody can
    /// see, surfacing as a 500 with no visible cause.</summary>
    [Fact]
    public async Task The_title_of_a_deleted_event_can_be_used_again()
    {
        var (owner, _) = await LoginAsync("9950000009");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Guard {Guid.NewGuid():N}"[..18], "Company");
        object Body() => new
        {
            title = "Guard Reused Title", description = "The same title, twice.",
            categoryId = _categoryId, venueName = "Hall", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(4),
        };

        var first = await Json(await owner.CreateEventAsync(orgId, Body()));
        var firstId = first.GetProperty("id").GetGuid();
        var firstSlug = first.GetProperty("slug").GetString()!;
        await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{firstId}");

        var second = await owner.CreateEventAsync(orgId, Body());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstSlug, (await Json(second)).GetProperty("slug").GetString());
    }

    [Fact]
    public async Task A_draft_holding_an_order_is_not_deletable()
    {
        var (owner, ownerId) = await LoginAsync("9950000002");
        var (orgId, id) = await PublishedAsync(owner, "Guard Sold Then Unpublished");
        var ttId = await SeedTicketTypeAsync(id, 50_000);
        await SeedOrderAsync(id, ttId, ownerId);

        // Force the event to Draft directly — the state the route ends in. Asserting on the delete
        // alone, so this test still fails if the `unpublish` guard is the thing that gets removed.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Events.Where(e => e.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EventStatus.Draft));
        }

        var res = await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{id}");
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("event_has_history", (await Json(res)).GetProperty("error").GetString());

        // The point of the guard, not just its return value.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            Assert.True(await db.Events.AnyAsync(e => e.Id == id));
            Assert.True(await db.Orders.AnyAsync(o => o.EventId == id));
            Assert.True(await db.OrderItems.AnyAsync(i => i.TicketTypeId == ttId));
        }
    }

    // ── The entrance: unpublish ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_published_event_holding_an_order_cannot_be_unpublished()
    {
        var (owner, ownerId) = await LoginAsync("9950000003");
        var (orgId, id) = await PublishedAsync(owner, "Guard Unpublish Refused");
        var ttId = await SeedTicketTypeAsync(id, 50_000);
        await SeedOrderAsync(id, ttId, ownerId);

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition",
            new { action = "unpublish" });
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("event_has_history", (await Json(res)).GetProperty("error").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(EventStatus.Published,
            await db.Events.Where(e => e.Id == id).Select(e => e.Status).FirstAsync());
    }

    [Fact]
    public async Task A_published_event_that_has_sold_nothing_still_unpublishes()
    {
        // Unpublish stays available for the case it exists for: pulling back an event nobody has joined.
        var (owner, _) = await LoginAsync("9950000004");
        var (orgId, id) = await PublishedAsync(owner, "Guard Unpublish Allowed");

        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{id}/transition", new { action = "unpublish" })).StatusCode);
    }

    /// <summary>The whole route, end to end. The individual guards above could each pass while the
    /// sequence still worked, if a later change routed around them.</summary>
    [Fact]
    public async Task The_unpublish_then_delete_route_cannot_destroy_a_sold_event()
    {
        var (owner, ownerId) = await LoginAsync("9950000005");
        var (orgId, id) = await PublishedAsync(owner, "Guard Full Route");
        var ttId = await SeedTicketTypeAsync(id, 50_000);
        await SeedOrderAsync(id, ttId, ownerId);

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/transition", new { action = "unpublish" });
        await owner.DeleteAsync($"/v1/orgs/{orgId}/events/{id}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db.Events.AnyAsync(e => e.Id == id), "the event was destroyed");
        Assert.True(await db.Orders.AnyAsync(o => o.EventId == id), "its orders were destroyed");
    }

    // ── The same shape one level down: ticket types ──────────────────────────────────

    /// <summary><c>order_items</c> cascades from <c>ticket_types</c>, and the existing guard asked
    /// <c>sold > 0</c> — what is held right now, not what ever happened. A type whose every order was
    /// refunded reads zero, deletes, and takes its order lines with it, leaving orders whose total
    /// reconciles against nothing.</summary>
    [Fact]
    public async Task A_ticket_type_whose_orders_were_all_refunded_is_still_not_deletable()
    {
        var (owner, ownerId) = await LoginAsync("9950000006");
        var (orgId, id) = await PublishedAsync(owner, "Guard Refunded Type");
        var ttId = await SeedTicketTypeAsync(id, 50_000);
        await SeedOrderAsync(id, ttId, ownerId);

        // Refunded: the money came back, the record stays. `sold` drops to zero and the old guard passed.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Orders.Where(o => o.EventId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Refunded));
        }

        // D-388 — this suite asserts the SOLD-tickets guard, so the live-edit guard must not answer first:
        // `change_request_required` would mask whether `tickets_already_sold` still fires at all, which is
        // the whole point of the test. Suspended for the delete so the real guard is the one that speaks.
        var res = await _factory.WithLiveStatusSuspendedAsync(id, () =>
            owner.DeleteAsync($"/v1/orgs/{orgId}/events/{id}/ticket-types/{ttId}"));
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("tickets_already_sold", (await Json(res)).GetProperty("error").GetString());

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db2.OrderItems.AnyAsync(i => i.TicketTypeId == ttId));
    }

    [Fact]
    public async Task An_unsold_ticket_type_still_deletes()
    {
        var (owner, _) = await LoginAsync("9950000007");
        var (orgId, id) = await PublishedAsync(owner, "Guard Unsold Type");
        var ttId = await SeedTicketTypeAsync(id, 50_000);

        Assert.Equal(HttpStatusCode.OK,
            (await _factory.WithLiveStatusSuspendedAsync(id, () =>
                owner.DeleteAsync($"/v1/orgs/{orgId}/events/{id}/ticket-types/{ttId}"))).StatusCode);
    }
}
