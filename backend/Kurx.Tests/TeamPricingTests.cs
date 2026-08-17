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

/// <summary>D-372 — a price has a unit, and the unit decides both the charge and the seat count.
///
/// <para><b>What this suite is for.</b> <c>TicketType.PricingUnit</c> has existed since D-020 and was read
/// by nothing in the money path, while <c>CreateOrderAsync</c> refused every paid group with
/// <c>paid_group_not_supported_yet</c>. So "₹2,000 per team" was unrepresentable end to end: either
/// impossible to buy, or — the moment that guard came off without the rest — charged four times over.</para>
///
/// <para>Everything here goes over real HTTP against real Postgres, because the rule spans order creation,
/// the gateway amount, capture, the roster cap and the inventory pool. A unit test of the arithmetic would
/// pass while any one of those five disagreed.</para></summary>
public class TeamPricingTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    /// <summary>₹2,000 — the entry fee for a whole team, and the number that must never be multiplied.</summary>
    private const long TeamPrice = 200_000;
    /// <summary>₹500 — an entry fee for one person.</summary>
    private const long SeatPrice = 50_000;

    public TeamPricingTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Team Cat", Slug = "team-pricing-cat" };
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
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var t = await Json(await c.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.GetProperty("access_token").GetString());
        return (c, t.GetProperty("user_id").GetGuid());
    }

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var (client, userId) = await LoginAsync(phone);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
    }

    /// <summary>A published event carrying one ticket type of the given shape. `paid` drives the owner
    /// through PAN + bank so the live M8 payment gate opens; a free event skips it, which is also the
    /// difference the free cases are meant to exercise.</summary>
    private async Task<(Guid OrgId, Guid EventId, Guid TicketTypeId)> EventWithTicketAsync(
        HttpClient owner, HttpClient reviewer, string phoneSeed,
        long pricePaise, PricingUnit unit, RegistrationMode mode, int quantity = 50,
        int? groupMin = 2, int? groupMax = 4, bool competition = false, bool publish = true)
    {
        if (pricePaise > 0)
        {
            await owner.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Owner" });
            await owner.PostAsJsonAsync("/v1/me/identity/bank",
                new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Owner" });
        }
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Team Org {phoneSeed}", "Company");

        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = $"Team Event {phoneSeed}",
            description = "A competition with team entry.", categoryId = _categoryId,
            venueName = "Arena", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(6),
        }))).GetProperty("id").GetGuid();

        Guid ttId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var tt = new TicketType
            {
                EventId = eventId, Name = "Entry", PricePaise = pricePaise, Quantity = quantity,
                PricingUnit = unit, RegistrationMode = mode, GroupMin = groupMin, GroupMax = groupMax,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30),
                PerUserLimit = 5, IsCompetition = competition,
            };
            db.TicketTypes.Add(tt);
            await db.SaveChangesAsync();
            ttId = tt.Id;
            // A competition ticket type needs its TeamPolicy — the same sync the ticket-type API runs.
            if (competition)
            {
                await scope.ServiceProvider.GetRequiredService<ITeamService>().SyncPolicyAsync(tt.Id);
                await db.SaveChangesAsync();
            }
        }

        _factory.SeedApprovedEventAuthorization(eventId);
        if (!publish) return (orgId, eventId, ttId);
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (orgId, eventId, ttId);
    }

    private async Task<HttpResponseMessage> CaptureAsync(string gatewayOrderId, string paymentId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay")
        {
            Content = JsonContent.Create(new { order_id = gatewayOrderId, payment_id = paymentId }),
        };
        req.Headers.Add("X-Razorpay-Signature", "mock-signature");
        return await _factory.CreateClient().SendAsync(req);
    }

    private async Task<(int Consumed, int Held, int Total)> PoolAsync(Guid ticketTypeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var p = await db.InventoryPools.AsNoTracking()
            .Where(x => x.TicketTypeId == ticketTypeId)
            .Select(x => new { x.Consumed, x.Held, x.Total }).FirstAsync();
        return (p.Consumed, p.Held, p.Total);
    }

    // ── The four combinations ────────────────────────────────────────────────────────────────────

    /// <summary>Unchanged behaviour, asserted so the rest of this file cannot quietly move it.</summary>
    [Fact]
    public async Task Free_individual_charges_nothing_and_takes_one_seat()
    {
        var reviewer = await ReviewerAsync("9930000001");
        var (owner, _) = await LoginAsync("9930000002");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "fi",
            0, PricingUnit.PerTicket, RegistrationMode.Individual);

        var (buyer, _) = await LoginAsync("9930000003");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));

        Assert.Equal("paid", order.GetProperty("status").GetString());
        Assert.Equal(0, order.GetProperty("amount_paise").GetInt64());
        Assert.Equal(1, order.GetProperty("tickets").GetArrayLength());
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);
    }

    [Fact]
    public async Task Paid_individual_charges_the_seat_price_once()
    {
        var reviewer = await ReviewerAsync("9930000010");
        var (owner, _) = await LoginAsync("9930000011");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "pi",
            SeatPrice, PricingUnit.PerTicket, RegistrationMode.Individual);

        var (buyer, _) = await LoginAsync("9930000012");
        var order = await Json(await buyer.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));

        Assert.Equal("pending", order.GetProperty("status").GetString());
        Assert.Equal(SeatPrice, order.GetProperty("amount_paise").GetInt64());

        Assert.Equal(HttpStatusCode.OK,
            (await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_pi")).StatusCode);
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);
    }

    [Fact]
    public async Task Free_team_takes_one_team_slot_and_its_members_take_none()
    {
        var reviewer = await ReviewerAsync("9930000020");
        var (owner, _) = await LoginAsync("9930000021");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "ft",
            0, PricingUnit.PerGroup, RegistrationMode.Group, quantity: 50);

        var (captain, _) = await LoginAsync("9930000022");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 4, displayName = "The Free Four" }));

        Assert.Equal("paid", order.GetProperty("status").GetString());
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);   // one TEAM, not four people

        var joinCode = order.GetProperty("join_code").GetString()!;
        for (var i = 0; i < 3; i++)
        {
            var (member, _) = await LoginAsync($"993000003{i}");
            Assert.Equal(HttpStatusCode.OK,
                (await member.PostAsJsonAsync("/v1/groups/join", new { joinCode })).StatusCode);
        }

        // The whole point of D-372: 50 team slots stays 50 team slots as rosters fill.
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);
    }

    /// <summary><b>The case the report was about.</b> One team, one charge, one slot.</summary>
    [Fact]
    public async Task Paid_team_charges_the_team_price_exactly_once()
    {
        var reviewer = await ReviewerAsync("9930000040");
        var (owner, _) = await LoginAsync("9930000041");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "pt",
            TeamPrice, PricingUnit.PerGroup, RegistrationMode.Group, quantity: 50);

        var (captain, _) = await LoginAsync("9930000042");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 4, displayName = "Paid Four" }));

        // ₹2,000 — NOT ₹8,000. This assertion is the regression guard for the overcharge bug.
        Assert.Equal(TeamPrice, order.GetProperty("amount_paise").GetInt64());
        Assert.Equal("pending", order.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK,
            (await CaptureAsync(order.GetProperty("razorpay_order_id").GetString()!, "pay_pt")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var saved = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == order.GetProperty("id").GetGuid());
        Assert.Equal(TeamPrice, saved.AmountPaise);
        Assert.Equal(4, saved.GroupSize);

        // Billable quantity × unit price must reconstruct the amount, or an admin reading the order
        // cannot tell ₹2,000-for-a-team from ₹2,000-for-one-of-four.
        var item = await db.OrderItems.AsNoTracking().FirstAsync(i => i.OrderId == saved.Id);
        Assert.Equal(1, item.Qty);
        Assert.Equal(TeamPrice, item.UnitPricePaise);
        Assert.Equal(saved.AmountPaise, item.Qty * item.UnitPricePaise);

        // The team exists only after capture — an unpaid team must not be able to recruit.
        var group = await db.Groups.AsNoTracking().FirstAsync(g => g.OrderId == saved.Id);
        Assert.Equal("Paid Four", group.DisplayName);
        Assert.Equal(1, await db.GroupMembers.CountAsync(m => m.GroupId == group.Id));   // the captain
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);
    }

    /// <summary>The other half of the rule: per-PARTICIPANT pricing on a team ticket DOES multiply, because
    /// that is what the organiser said. Without this, "fix the overcharge" would become "never multiply".</summary>
    [Fact]
    public async Task Per_participant_pricing_on_a_team_ticket_multiplies_by_the_team_size()
    {
        var reviewer = await ReviewerAsync("9930000050");
        var (owner, _) = await LoginAsync("9930000051");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "pp",
            SeatPrice, PricingUnit.PerTicket, RegistrationMode.Group, quantity: 50);

        var (captain, _) = await LoginAsync("9930000052");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 3, displayName = "Three Seats" }));

        Assert.Equal(SeatPrice * 3, order.GetProperty("amount_paise").GetInt64());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var item = await db.OrderItems.AsNoTracking().FirstAsync(i => i.OrderId == order.GetProperty("id").GetGuid());
        Assert.Equal(3, item.Qty);
        Assert.Equal(SeatPrice, item.UnitPricePaise);
    }

    // ── Guards ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_team_size_outside_the_configured_bounds_is_refused()
    {
        var reviewer = await ReviewerAsync("9930000060");
        var (owner, _) = await LoginAsync("9930000061");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "bounds",
            TeamPrice, PricingUnit.PerGroup, RegistrationMode.Group, groupMin: 2, groupMax: 4);

        var (captain, _) = await LoginAsync("9930000062");
        foreach (var size in new object?[] { 1, 5, null })
        {
            var res = await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
                new { ticketTypeId = ttId, groupSize = size });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal("invalid_group_size", (await Json(res)).GetProperty("error").GetString());
        }
    }

    /// <summary>Team slots, not seats: with one slot left, one team registers and the next is refused —
    /// regardless of how many people are in either.</summary>
    [Fact]
    public async Task Team_inventory_runs_out_in_teams()
    {
        var reviewer = await ReviewerAsync("9930000070");
        var (owner, _) = await LoginAsync("9930000071");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "cap",
            0, PricingUnit.PerGroup, RegistrationMode.Group, quantity: 1);

        var (first, _) = await LoginAsync("9930000072");
        Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 4 })).StatusCode);

        var (second, _) = await LoginAsync("9930000073");
        var res = await second.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 2 });
        Assert.Equal("sold_out", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>A team may not outgrow the size it was registered (and charged) for. The cap now reads
    /// <c>Order.GroupSize</c>, because <c>OrderItem.Qty</c> is 1 for a PerGroup team.</summary>
    [Fact]
    public async Task A_team_cannot_take_more_members_than_it_registered_for()
    {
        var reviewer = await ReviewerAsync("9930000080");
        var (owner, _) = await LoginAsync("9930000081");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "full",
            0, PricingUnit.PerGroup, RegistrationMode.Group);

        var (captain, _) = await LoginAsync("9930000082");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 2 }));
        var joinCode = order.GetProperty("join_code").GetString()!;

        var (m1, _) = await LoginAsync("9930000083");
        Assert.Equal(HttpStatusCode.OK, (await m1.PostAsJsonAsync("/v1/groups/join", new { joinCode })).StatusCode);

        var (m2, _) = await LoginAsync("9930000084");
        var res = await m2.PostAsJsonAsync("/v1/groups/join", new { joinCode });
        Assert.Equal("group_full", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>§17.1 per-caller idempotency, on the paid team path: a retried create returns the ORIGINAL
    /// order rather than charging a second ₹2,000 or taking a second team slot.</summary>
    [Fact]
    public async Task A_retried_team_purchase_returns_the_original_order()
    {
        var reviewer = await ReviewerAsync("9930000090");
        var (owner, _) = await LoginAsync("9930000091");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "idem",
            TeamPrice, PricingUnit.PerGroup, RegistrationMode.Group);

        var (captain, _) = await LoginAsync("9930000092");
        var body = new { ticketTypeId = ttId, groupSize = 3, idempotencyKey = "team-key-1" };
        var first = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders", body));
        var second = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders", body));

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal(TeamPrice, second.GetProperty("amount_paise").GetInt64());
        Assert.Equal(0, (await PoolAsync(ttId)).Consumed);   // still held, not consumed — one hold only
        Assert.Equal(1, (await PoolAsync(ttId)).Held);
    }

    /// <summary>A re-delivered capture webhook must not mint a second team or a second ticket.</summary>
    [Fact]
    public async Task A_duplicate_capture_does_not_create_a_second_team()
    {
        var reviewer = await ReviewerAsync("9930000100");
        var (owner, _) = await LoginAsync("9930000101");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "dup",
            TeamPrice, PricingUnit.PerGroup, RegistrationMode.Group);

        var (captain, _) = await LoginAsync("9930000102");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 2 }));
        var gatewayId = order.GetProperty("razorpay_order_id").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await CaptureAsync(gatewayId, "pay_dup")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CaptureAsync(gatewayId, "pay_dup")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var orderId = order.GetProperty("id").GetGuid();
        Assert.Equal(1, await db.Groups.CountAsync(g => g.OrderId == orderId));
        Assert.Equal(1, (await PoolAsync(ttId)).Consumed);
    }

    // ── D-374: the authoritative Team, not only its legacy mirror ────────────────────────────────

    /// <summary>A competition team purchase materialises the Phase-10 <c>Team</c>, linked to the registration
    /// the money path produced — the <c>Team.RegistrationId</c> hook §6.5 reserved and left null.</summary>
    [Fact]
    public async Task A_competition_team_purchase_creates_the_authoritative_team()
    {
        var reviewer = await ReviewerAsync("9930000120");
        var (owner, _) = await LoginAsync("9930000121");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "teament",
            0, PricingUnit.PerGroup, RegistrationMode.Group, competition: true);

        var (captain, captainId) = await LoginAsync("9930000122");
        Assert.Equal(HttpStatusCode.OK, (await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 3, displayName = "Real Team" })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.EventId == eventId);

        Assert.NotNull(team);
        Assert.Equal("Real Team", team!.Name);
        Assert.NotNull(team.RegistrationId);   // the link Phase 10 reserved and could not yet fill
        var captainMembership = await db.TeamMemberships.AsNoTracking()
            .SingleAsync(m => m.TeamId == team.Id && m.PersonId == captainId);
        Assert.Equal(TeamRole.Captain, captainMembership.Role);
    }

    /// <summary>Teams exist only where competition does (V3 §6): a plain group purchase still produces a
    /// Group and no Team, so D-374 adds nothing to events that never had teams.</summary>
    [Fact]
    public async Task A_plain_group_purchase_creates_no_team()
    {
        var reviewer = await ReviewerAsync("9930000130");
        var (owner, _) = await LoginAsync("9930000131");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "noteam",
            0, PricingUnit.PerGroup, RegistrationMode.Group, competition: false);

        var (captain, _) = await LoginAsync("9930000132");
        await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 3, displayName = "Just A Group" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Empty(await db.Teams.AsNoTracking().Where(t => t.EventId == eventId).ToListAsync());
    }

    /// <summary>The Team roster follows the Group roster, so the two can never disagree about who is on a
    /// team — and re-running the projection cannot duplicate a member.</summary>
    [Fact]
    public async Task Joining_a_competition_group_adds_the_member_to_the_team()
    {
        var reviewer = await ReviewerAsync("9930000140");
        var (owner, _) = await LoginAsync("9930000141");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "roster",
            0, PricingUnit.PerGroup, RegistrationMode.Group, competition: true);

        var (captain, _) = await LoginAsync("9930000142");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 3, displayName = "Growing" }));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var teamId = (await db.Teams.AsNoTracking().FirstAsync(t => t.EventId == eventId)).Id;
        Assert.Equal(1, await db.TeamMemberships.CountAsync(m => m.TeamId == teamId));

        // A competition ticket routes joining through an invitation, never the open join code (D-036),
        // so the roster is grown the way the product actually grows it.
        var invite = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/invitations", new
        {
            name = "Teammate", phone = "9930000143", channel = "WhatsApp",
            groupId = order.GetProperty("group_id").GetGuid(),
        }));
        var (member, memberId) = await LoginAsync("9930000143");
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsJsonAsync(
            $"/v1/groups/invitations/{invite.GetProperty("invite_token").GetString()}/accept", new { })).StatusCode);

        Assert.Equal(2, await db.TeamMemberships.CountAsync(m => m.TeamId == teamId));
        var joined = await db.TeamMemberships.AsNoTracking().SingleAsync(m => m.TeamId == teamId && m.PersonId == memberId);
        Assert.Equal(TeamRole.Member, joined.Role);
    }

    // ── D-375: one team-capacity number, and it is the pool's ────────────────────────────────────

    /// <summary><c>Event.MaxTeams</c> was stored, echoed and enforced by nothing, so it could contradict the
    /// team slots actually on sale. The eligibility view now projects the authoritative inventory.</summary>
    [Fact]
    public async Task Team_capacity_reads_the_ticket_type_not_the_stored_hint()
    {
        var reviewer = await ReviewerAsync("9930000150");
        var (owner, _) = await LoginAsync("9930000151");
        var (orgId, eventId, _) = await EventWithTicketAsync(owner, reviewer, "cap2",
            0, PricingUnit.PerGroup, RegistrationMode.Group, quantity: 20);

        // The organiser's own note says 50; the ticket type sells 20 team slots.
        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}",
            new { eligibility = new { maxTeams = 50 } });

        var detail = await Json(await owner.GetAsync($"/v1/events/{eventId}"));
        Assert.Equal(20, detail.GetProperty("eligibility").GetProperty("max_teams").GetInt32());
    }

    /// <summary>With no team ticket there is nothing to contradict, so the organiser's own figure stands.</summary>
    [Fact]
    public async Task Team_capacity_falls_back_to_the_stored_value_with_no_team_ticket()
    {
        var reviewer = await ReviewerAsync("9930000160");
        var (owner, _) = await LoginAsync("9930000161");
        var (orgId, eventId, _) = await EventWithTicketAsync(owner, reviewer, "cap3",
            0, PricingUnit.PerTicket, RegistrationMode.Individual);

        await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}",
            new { eligibility = new { maxTeams = 12 } });

        var detail = await Json(await owner.GetAsync($"/v1/events/{eventId}"));
        Assert.Equal(12, detail.GetProperty("eligibility").GetProperty("max_teams").GetInt32());
    }

    // ── D-374: a refusal that names itself ───────────────────────────────────────────────────────

    /// <summary>A paid event's final publish belongs to a reviewer (D-047) — but the refusal said only
    /// "forbidden", on a path where every sibling names itself, so a correct rule read as a defect.</summary>
    [Fact]
    public async Task An_organiser_publishing_a_paid_event_is_told_a_reviewer_is_required()
    {
        var reviewer = await ReviewerAsync("9930000170");
        var (owner, _) = await LoginAsync("9930000171");
        var (orgId, eventId, _) = await EventWithTicketAsync(owner, reviewer, "revreq",
            TeamPrice, PricingUnit.PerGroup, RegistrationMode.Group, publish: false);

        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("reviewer_required", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>Guest checkout stays closed to group registration — unchanged, and asserted because the
    /// paid-group path now runs where a refusal used to stand.</summary>
    [Fact]
    public async Task A_guest_still_cannot_register_a_team()
    {
        var reviewer = await ReviewerAsync("9930000110");
        var (owner, _) = await LoginAsync("9930000111");
        var (_, eventId, ttId) = await EventWithTicketAsync(owner, reviewer, "guest",
            0, PricingUnit.PerGroup, RegistrationMode.Group);

        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 2, guestName = "G", guestPhone = "9990000000" });
        Assert.Equal("guest_group_not_supported", (await Json(res)).GetProperty("error").GetString());
    }
}
