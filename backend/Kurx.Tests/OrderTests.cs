using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Shares one org/owner and a small attendee pool across all tests (bootstrapped once, see
/// the static reset guard) instead of logging in fresh per test. The anonymous-IP rate limiter
/// (60 req/min, Program.cs) applies to every request in this test host regardless of auth state
/// — UseRateLimiter() runs before UseAuthentication(), so ctx.User is still empty when the
/// limiter partitions by user id — so a class with many tests must keep total HTTP call volume
/// well under 60 to avoid flaky 429s. Fixing that ordering is out of scope for this change.
/// </summary>
public class OrderTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static Guid _typeId;
    private static Guid _orgId;
    private static HttpClient _owner = null!;
    private static HttpClient _a1 = null!, _a2 = null!, _a3 = null!;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public OrderTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using (var scope = factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                    var category = new EventCategory { Level = CategoryLevel.Category, Name = "Tech", Slug = "tech-ord" };
                    db.EventCategories.Add(category);
                    // D-367 — the group-mode cases below create TEAM ticket types, which are only legal
                    // where the archetype supports `teams`. `competitive` has it Optional in the seeded
                    // matrix; an event with no Type resolves every capability to Unsupported.
                    var type = new EventCategory
                    {
                        Level = CategoryLevel.Type, ParentId = category.Id, Name = "Order Hackathon",
                        Slug = "order-hackathon", ArchetypeSlug = "competitive",
                    };
                    db.EventCategories.Add(type);
                    db.SaveChanges();
                    _categoryId = category.Id;
                    _typeId = type.Id;
                }

                _owner = LoginAsAsync("9810099001").GetAwaiter().GetResult();
                _orgId = _factory.SeedVerifiedOrgForClient(_owner, "Order Test Org");

                _a1 = LoginAsAsync("9810099010").GetAwaiter().GetResult();
                _a2 = LoginAsAsync("9810099011").GetAwaiter().GetResult();
                _a3 = LoginAsAsync("9810099012").GetAwaiter().GetResult();

                _reset = true;
            }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsAsync(string phone)
    {
        var client = _factory.CreateClient();
        var req = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        if (!req.IsSuccessStatusCode)
            throw new Exception($"OTP request failed for {phone}: {req.StatusCode} {await req.Content.ReadAsStringAsync()}");
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", verify.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<Guid> CreatePublishedEventAsync(string title)
    {
        var ev = await Json(await _owner.CreateEventAsync(_orgId, new
        {
            title,
            description = "Annual summit.",
            categoryId = _categoryId,
            typeId = _typeId,
            venueName = "Order Hall",
            venueAddress = "1 Main St",
            city = "Bengaluru",
            startsAt = DateTime.UtcNow.AddDays(30),
            endsAt = DateTime.UtcNow.AddDays(30).AddHours(8),
        }));
        var eventId = ev.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        var published = await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        return eventId;
    }

    private async Task<Guid> CreateTicketTypeAsync(Guid eventId, string name = "General Admission",
        long price = 0, int qty = 5, string regMode = "Individual", int? groupMin = null, int? groupMax = null,
        int perUserLimit = 5, DateTime? saleStarts = null, DateTime? saleEnds = null, bool isCompetition = false)
    {
        var body = new
        {
            name,
            pricePaise = price,
            pricingUnit = regMode == "Group" ? "PerGroup" : "PerTicket",
            registrationMode = regMode,
            groupMin,
            groupMax,
            quantity = qty,
            saleStarts = saleStarts ?? DateTime.UtcNow.AddDays(-1),
            saleEnds = saleEnds ?? DateTime.UtcNow.AddDays(29),
            perUserLimit,
            isAllAccess = false,
            isCompetition,
        };
        var res = await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{eventId}/ticket-types", body);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> MeIdAsync(HttpClient client)
        => (await Json(await client.GetAsync("/v1/me"))).GetProperty("id").GetGuid();

    private bool HasChatMember(Guid eventId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var roomId = db.ChatRooms.AsNoTracking().Where(r => r.EventId == eventId).Select(r => r.Id).SingleOrDefault();
        return roomId != Guid.Empty && db.ChatMembers.AsNoTracking().Any(m => m.RoomId == roomId && m.UserId == userId);
    }

    // Phase 9 (§17.1): chat membership is a non-money side effect delivered through the outbox, so drive the
    // dispatcher before asserting it — the money path itself only enqueues the message in-transaction.
    private async Task DispatchOutboxAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Kurx.Infrastructure.Jobs.OutboxDispatchJob>().RunAsync(default);
    }

    private int SoldFor(Guid ticketTypeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.TicketTypes.AsNoTracking().Single(t => t.Id == ticketTypeId).Sold;
    }

    [Fact]
    public async Task Free_individual_order_issues_ticket_and_increments_sold()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 1");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 3, perUserLimit: 2);

        var res = await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var order = await Json(res);
        Assert.Equal("paid", order.GetProperty("status").GetString());
        Assert.Equal(0L, order.GetProperty("amount_paise").GetInt64());
        var tickets = order.GetProperty("tickets").EnumerateArray().ToList();
        Assert.Single(tickets);
        Assert.Equal("issued", tickets[0].GetProperty("state").GetString());
        Assert.NotEqual(Guid.Empty, tickets[0].GetProperty("code").GetGuid());

        Assert.Equal(1, SoldFor(ttId));

        await DispatchOutboxAsync();
        Assert.True(HasChatMember(eventId, await MeIdAsync(_a1)), "buyer should auto-join the event chat room");
    }

    [Fact]
    public async Task Paid_ticket_type_on_an_unverified_org_is_blocked()
    {
        // M10 (D-049) superseded D-021's payment_not_supported_yet: a paid ticket type now creates a
        // real order — but only when payments are enabled (organizer paid-verified + org verified, M8).
        // This event's org is unverified, so the live gate returns payments_not_enabled.
        var eventId = await CreatePublishedEventAsync("Order Event 2");
        var ttId = await CreateTicketTypeAsync(eventId, price: 50000);

        var res = await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("payments_not_enabled", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Per_user_limit_is_enforced()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 3");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, perUserLimit: 1);

        var first = await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("limit_exceeded", (await Json(second)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Sold_out_ticket_type_is_rejected()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 4");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 1, perUserLimit: 5);

        Assert.Equal(HttpStatusCode.OK, (await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId })).StatusCode);

        var res = await _a2.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("sold_out", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Order_outside_sale_window_is_rejected()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 5");
        var ttId = await CreateTicketTypeAsync(eventId,
            saleStarts: DateTime.UtcNow.AddDays(-10), saleEnds: DateTime.UtcNow.AddDays(-1));

        var res = await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("not_on_sale", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Group_order_create_join_and_group_full()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 6");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, regMode: "Group", groupMin: 2, groupMax: 2, perUserLimit: 5);

        var create = await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 2 });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var order = await Json(create);
        var joinCode = order.GetProperty("join_code").GetString();
        Assert.False(string.IsNullOrWhiteSpace(joinCode));
        var groupId = order.GetProperty("group_id").GetGuid();

        var join1 = await _a2.PostAsJsonAsync("/v1/groups/join", new { joinCode });
        Assert.Equal(HttpStatusCode.OK, join1.StatusCode);

        // Group is now full (leader + 1 joiner = groupSize 2); a third distinct user is rejected.
        var join2 = await _a3.PostAsJsonAsync("/v1/groups/join", new { joinCode });
        Assert.Equal(HttpStatusCode.BadRequest, join2.StatusCode);
        Assert.Equal("group_full", (await Json(join2)).GetProperty("error").GetString());

        var group = await Json(await _a1.GetAsync($"/v1/groups/{groupId}"));
        Assert.Equal(2, group.GetProperty("members").EnumerateArray().Count());

        /*
         * D-357 — ONE, not two. The roster has two people and the ticket type is `PerGroup`, so the
         * registration unit is the TEAM: it took one inventory unit when the leader registered and the
         * joiner took none, because that member was already inside the slot the team holds.
         *
         * This asserted 2 because inventory used to count participants, which is what made
         * `Quantity = 50` with a team size of 3–5 admit 10–16 teams rather than 50. The number moving
         * here IS the fix; a `PerTicket` group ticket still counts every person, and its own case is
         * pinned in `TeamPricingTests`.
         */
        Assert.Equal(1, SoldFor(ttId));

        await DispatchOutboxAsync();
        Assert.True(HasChatMember(eventId, await MeIdAsync(_a2)), "group joiner should auto-join the event chat room");
    }

    [Fact]
    public async Task Group_size_outside_min_max_is_rejected()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 7");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, regMode: "Group", groupMin: 2, groupMax: 4);

        var res = await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_group_size", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invalid_join_code_is_rejected()
    {
        var res = await _a1.PostAsJsonAsync("/v1/groups/join", new { joinCode = "ZZZZZZ" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_join_code", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task My_tickets_and_my_groups_reflect_created_orders()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 8");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 5);

        await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId });

        var myTickets = (await Json(await _a1.GetAsync("/v1/orders"))).EnumerateArray().ToList();
        Assert.Contains(myTickets, o => o.GetProperty("event_id").GetGuid() == eventId);

        var ownerOrders = (await Json(await _owner.GetAsync("/v1/orders"))).EnumerateArray().ToList();
        Assert.Empty(ownerOrders);
    }

    [Fact]
    public async Task Resend_ticket_is_forbidden_for_non_owner()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 9");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 5);

        var order = await Json(await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var ticketCode = order.GetProperty("tickets")[0].GetProperty("code").GetGuid();

        var resendByOwner = await _owner.PostAsync($"/v1/tickets/{ticketCode}/resend", null);
        Assert.Equal(HttpStatusCode.Forbidden, resendByOwner.StatusCode);

        var resendByAttendee = await _a1.PostAsync($"/v1/tickets/{ticketCode}/resend", null);
        Assert.Equal(HttpStatusCode.NoContent, resendByAttendee.StatusCode);
    }

    // ── Guest checkout (D-036) ──────────────────────────────────────────────

    [Fact]
    public async Task Guest_creates_free_order_without_auth_and_gets_access_token()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 10");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 5);

        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync($"/v1/events/{eventId}/orders", new
        {
            ticketTypeId = ttId, guestName = "Priya Guest", guestPhone = "9820000001", guestEmail = "priya@example.com",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var order = await Json(res);
        Assert.Equal("paid", order.GetProperty("status").GetString());
        var token = order.GetProperty("guest_access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var saved = await db.Orders.AsNoTracking().SingleAsync(o => o.EventId == eventId && o.TicketTypeId == ttId);
            Assert.Null(saved.UserId);
            Assert.Equal("Priya Guest", saved.GuestName);
        }

        var view = await Json(await anon.GetAsync($"/v1/orders/guest/{token}"));
        Assert.Single(view.GetProperty("tickets").EnumerateArray());

        var resend = await anon.PostAsync($"/v1/orders/guest/{token}/resend", null);
        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
    }

    [Fact]
    public async Task Guest_checkout_is_blocked_for_competition_ticket_types()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 11");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 5, isCompetition: true);

        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync($"/v1/events/{eventId}/orders", new
        {
            ticketTypeId = ttId, guestName = "N", guestPhone = "9820000002",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("account_required", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Guest_checkout_is_blocked_for_paid_ticket_types()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 12");
        var ttId = await CreateTicketTypeAsync(eventId, price: 10000);

        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync($"/v1/events/{eventId}/orders", new
        {
            ticketTypeId = ttId, guestName = "N", guestPhone = "9820000003",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("account_required", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Guest_checkout_is_blocked_for_group_mode_ticket_types()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 13");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, regMode: "Group", groupMin: 2, groupMax: 4);

        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync($"/v1/events/{eventId}/orders", new
        {
            ticketTypeId = ttId, groupSize = 2, guestName = "N", guestPhone = "9820000004",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("guest_group_not_supported", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Guest_per_phone_limit_is_enforced()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 14");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, perUserLimit: 1);

        var anon = _factory.CreateClient();
        var body = new { ticketTypeId = ttId, guestName = "Repeat Guest", guestPhone = "9820000005" };

        var first = await anon.PostAsJsonAsync($"/v1/events/{eventId}/orders", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await anon.PostAsJsonAsync($"/v1/events/{eventId}/orders", body);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("limit_exceeded", (await Json(second)).GetProperty("error").GetString());
    }

    // ── Competition team invitations (D-036) ────────────────────────────────

    [Fact]
    public async Task Competition_team_join_by_open_code_is_rejected()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 15");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, regMode: "Group", groupMin: 2, groupMax: 4, isCompetition: true);

        var create = await Json(await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 2 }));
        var joinCode = create.GetProperty("join_code").GetString();

        var res = await _a2.PostAsJsonAsync("/v1/groups/join", new { joinCode });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("competition_requires_invitation", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Competition_team_invite_by_phone_and_accept_issues_ticket()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 16");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, regMode: "Group", groupMin: 2, groupMax: 4, isCompetition: true);

        var create = await Json(await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 2 }));
        var groupId = create.GetProperty("group_id").GetGuid();

        var invite = await Json(await _a1.PostAsJsonAsync($"/v1/events/{eventId}/invitations", new
        {
            name = "Teammate", phone = "9810099011", channel = "WhatsApp", groupId,
        }));
        Assert.Equal(groupId, invite.GetProperty("group_id").GetGuid());
        var token = invite.GetProperty("invite_token").GetString();

        var accept = await _a2.PostAsJsonAsync($"/v1/groups/invitations/{token}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var group = await Json(await _a1.GetAsync($"/v1/groups/{groupId}"));
        Assert.Equal(2, group.GetProperty("members").EnumerateArray().Count());
        // D-357 — one TEAM slot for a two-person team on a `PerGroup` ticket. See the note above.
        Assert.Equal(1, SoldFor(ttId));

        // Re-accepting the same token fails — already accepted.
        var again = await _a2.PostAsJsonAsync($"/v1/groups/invitations/{token}/accept", new { });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("already_accepted", (await Json(again)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Non_captain_non_manager_cannot_send_team_invitation()
    {
        var eventId = await CreatePublishedEventAsync("Order Event 17");
        var ttId = await CreateTicketTypeAsync(eventId, qty: 10, regMode: "Group", groupMin: 2, groupMax: 4, isCompetition: true);

        var create = await Json(await _a1.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 2 }));
        var groupId = create.GetProperty("group_id").GetGuid();

        // _a3 is neither the group's captain nor an org manager for this event.
        var res = await _a3.PostAsJsonAsync($"/v1/events/{eventId}/invitations", new
        {
            name = "Teammate", phone = "9810099012", channel = "WhatsApp", groupId,
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
