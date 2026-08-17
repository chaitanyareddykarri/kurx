using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-366 — a team's price depends on its size.
///
/// <para>D-372 settled what a team price MEANS: charged once for the whole team, one inventory slot,
/// never multiplied by the roster. What it could not express is the shape organisers actually use —
/// 2 → ₹250, 3 → ₹300, 4–5 → ₹400 — because a <c>TicketType</c> carries one price for its whole
/// <c>GroupMin..GroupMax</c> range.</para>
///
/// <para>The assertions that matter most are the two ends of the same claim: a 2-member team pays the
/// 2-member band and a 3-member team pays the 3-member band, from the same ticket type, proved from the
/// stored order rather than the response alone. Everything else here exists so those two cannot pass by
/// accident.</para></summary>
public class TeamSizePricingTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static Guid _typeId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    private const long TwoPrice = 25_000;    // ₹250
    private const long ThreePrice = 30_000;  // ₹300
    private const long BigPrice = 40_000;    // ₹400 — a BAND, 4–5, not a single size

    public TeamSizePricingTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Tiered", Slug = "tiered-pricing" };
            db.EventCategories.Add(cat);
            // D-367 — a team ticket is only legal where the archetype supports `teams`, so these events
            // carry a Type that does. `competitive` has it Optional in the seeded matrix; an event with no
            // Type at all resolves every capability to Unsupported and could not have a team registration.
            var type = new EventCategory
            {
                Level = CategoryLevel.Type, ParentId = cat.Id, Name = "Tiered Hackathon",
                Slug = "tiered-hackathon", ArchetypeSlug = "competitive",
            };
            db.EventCategories.Add(type);
            db.SaveChanges();
            _categoryId = cat.Id;
            _typeId = type.Id;
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

    /// <summary>The bands from the decision's own example, as a request body.</summary>
    private static object[] Bands() =>
    [
        new { minSize = 2, maxSize = 2, pricePaise = TwoPrice },
        new { minSize = 3, maxSize = 3, pricePaise = ThreePrice },
        new { minSize = 4, maxSize = 5, pricePaise = BigPrice },
    ];

    private static object TicketBody(object[]? tiers, int groupMin = 2, int groupMax = 5, long price = TwoPrice) => new
    {
        name = "Team Entry",
        pricePaise = price,
        pricingUnit = "PerGroup",
        registrationMode = "Group",
        groupMin,
        groupMax,
        quantity = 100,                      // 100 TEAM slots (D-372), not 100 people
        saleStarts = DateTime.UtcNow.AddDays(-1),
        saleEnds = DateTime.UtcNow.AddDays(30),
        perUserLimit = 5,
        isAllAccess = false,
        isCompetition = false,
        priceTiers = tiers,
    };

    /// <summary>A published, paid-capable team event whose ticket carries the bands.</summary>
    private async Task<(Guid OrgId, Guid EventId, Guid TicketTypeId)> TieredEventAsync(
        HttpClient owner, string seed, object[]? tiers = null)
    {
        await owner.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Owner" });
        await owner.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Owner" });
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Tier Org {seed}", "Company");

        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = $"Tiered Event {seed}",
            description = "A competition priced by team size.", categoryId = _categoryId, typeId = _typeId,
            venueName = "Arena", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(6),
        }))).GetProperty("id").GetGuid();

        var created = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types",
            TicketBody(tiers ?? Bands()));
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var ttId = (await Json(created)).GetProperty("id").GetGuid();

        _factory.SeedApprovedEventAuthorization(eventId);
        var reviewer = await _factory.ReviewerClientAsync();
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "submit_review" });
        await reviewer.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (orgId, eventId, ttId);
    }

    // ── The claim, proved at both ends ───────────────────────────────────────────────

    /// <summary><b>The proof.</b> Same ticket type, two teams, two different sizes, two different prices —
    /// and neither is the other multiplied by anything.</summary>
    [Fact]
    public async Task A_two_member_team_and_a_three_member_team_pay_different_prices()
    {
        var (owner, _) = await LoginAsync("9940000001");
        var (_, eventId, ttId) = await TieredEventAsync(owner, "proof");

        var (captainA, _) = await LoginAsync("9940000002");
        var two = await Json(await captainA.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 2, displayName = "Duo" }));

        var (captainB, _) = await LoginAsync("9940000003");
        var three = await Json(await captainB.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 3, displayName = "Trio" }));

        Assert.Equal(TwoPrice, two.GetProperty("amount_paise").GetInt64());     // ₹250
        Assert.Equal(ThreePrice, three.GetProperty("amount_paise").GetInt64()); // ₹300
        Assert.NotEqual(two.GetProperty("amount_paise").GetInt64(), three.GetProperty("amount_paise").GetInt64());

        // …and from the stored rows, because the response is the easy half. `Qty × UnitPrice` must
        // still reconstruct the amount: 1 team × the band it resolved to.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        foreach (var (order, expected, size) in new[]
                 {
                     (two, TwoPrice, 2), (three, ThreePrice, 3),
                 })
        {
            var id = order.GetProperty("id").GetGuid();
            var saved = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == id);
            var item = await db.OrderItems.AsNoTracking().FirstAsync(i => i.OrderId == id);
            Assert.Equal(expected, saved.AmountPaise);
            Assert.Equal(size, saved.GroupSize);
            Assert.Equal(1, item.Qty);                          // one TEAM
            Assert.Equal(expected, item.UnitPricePaise);        // the band, not the headline
            Assert.Equal(saved.AmountPaise, item.Qty * item.UnitPricePaise);
        }
    }

    /// <summary>A band covering several sizes charges the same for each of them — and still once.</summary>
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task A_range_band_prices_every_size_inside_it_the_same(int size)
    {
        var (owner, _) = await LoginAsync($"994000001{size}");
        var (_, eventId, ttId) = await TieredEventAsync(owner, $"range{size}");

        var (captain, _) = await LoginAsync($"994000002{size}");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = size, displayName = $"Team of {size}" }));

        // ₹400 for a team of 4 AND a team of 5 — never ₹400 × 4.
        Assert.Equal(BigPrice, order.GetProperty("amount_paise").GetInt64());
    }

    /// <summary>The regression that would undo D-372: a band must never be multiplied by the roster.</summary>
    [Fact]
    public async Task A_band_is_charged_once_for_the_whole_team()
    {
        var (owner, _) = await LoginAsync("9940000030");
        var (_, eventId, ttId) = await TieredEventAsync(owner, "once");

        var (captain, _) = await LoginAsync("9940000031");
        var order = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 3, displayName = "Not Nine Hundred" }));

        Assert.Equal(ThreePrice, order.GetProperty("amount_paise").GetInt64());
        Assert.NotEqual(ThreePrice * 3, order.GetProperty("amount_paise").GetInt64());
    }

    // ── Configuration validation ─────────────────────────────────────────────────────

    /// <summary>Every case the decision names as invalid, refused at the door with a code that says
    /// which one it was — "invalid" alone leaves an organiser guessing between four rules.</summary>
    [Theory]
    // 2–4 and 3–5: a team of 3 matches both.
    [InlineData("overlap", "overlapping_price_tiers")]
    // 2 and 4–5: a team of 3 is allowed by the ticket and priced by nothing.
    [InlineData("gap", "price_tier_gap")]
    // 2–3 only, while the ticket admits up to 5.
    [InlineData("short", "price_tier_gap")]
    // A band for teams of 6 on a ticket that admits at most 5.
    [InlineData("outside", "price_tier_outside_group_size")]
    // ₹0 is not a price; a free event has no bands at all.
    [InlineData("zero", "invalid_price_tier")]
    // Bands on an individual ticket — per-participant pricing already scales with the roster.
    [InlineData("individual", "price_tiers_require_group")]
    public async Task An_invalid_band_set_is_refused(string shape, string expected)
    {
        var (owner, _) = await LoginAsync($"99400001{shape.Length:D2}");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, $"Bad Bands {shape}", "Company");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = $"Bad bands {shape}", description = "Invalid tier configurations.",
            categoryId = _categoryId, typeId = _typeId, venueName = "Arena", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(6),
        }))).GetProperty("id").GetGuid();

        object[] tiers = shape switch
        {
            "overlap" =>
            [
                new { minSize = 2, maxSize = 4, pricePaise = TwoPrice },
                new { minSize = 3, maxSize = 5, pricePaise = ThreePrice },
            ],
            "gap" =>
            [
                new { minSize = 2, maxSize = 2, pricePaise = TwoPrice },
                new { minSize = 4, maxSize = 5, pricePaise = BigPrice },
            ],
            "short" => [new { minSize = 2, maxSize = 3, pricePaise = TwoPrice }],
            "outside" =>
            [
                new { minSize = 2, maxSize = 5, pricePaise = TwoPrice },
                new { minSize = 6, maxSize = 6, pricePaise = BigPrice },
            ],
            "zero" => [new { minSize = 2, maxSize = 5, pricePaise = 0L }],
            _ => [new { minSize = 2, maxSize = 5, pricePaise = TwoPrice }],
        };

        var body = shape == "individual"
            ? (object)new
            {
                name = "Solo", pricePaise = TwoPrice, pricingUnit = "PerTicket", registrationMode = "Individual",
                groupMin = (int?)null, groupMax = (int?)null, quantity = 100,
                saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
                perUserLimit = 5, isAllAccess = false, isCompetition = false, priceTiers = tiers,
            }
            : TicketBody(tiers);

        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", body);
        Assert.False(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.Equal(expected, (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary>The headline price is DERIVED, not trusted: a client sending ₹9,999 alongside bands
    /// starting at ₹250 gets ₹250, so "from ₹250" cannot become a lie and the paid/free filter cannot be
    /// driven by a number nobody charges.</summary>
    [Fact]
    public async Task The_tickets_own_price_becomes_the_cheapest_band()
    {
        var (owner, _) = await LoginAsync("9940000050");
        var orgId = await _factory.CreateVerifiedOrgAsync(owner, "Headline Org", "Company");
        var eventId = (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Headline", description = "Derived headline price.", categoryId = _categoryId,
            typeId = _typeId,   // D-367 — a team ticket needs an archetype that supports teams
            venueName = "Arena", city = "Chennai",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(6),
        }))).GetProperty("id").GetGuid();

        var created = await Json(await owner.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{eventId}/ticket-types", TicketBody(Bands(), price: 999_900)));

        Assert.Equal(TwoPrice, created.GetProperty("price_paise").GetInt64());
    }

    /// <summary>The bands come back on the ticket, ordered, so a client can render the price table
    /// without inventing an order for it.</summary>
    [Fact]
    public async Task The_bands_are_returned_with_the_ticket_type()
    {
        var (owner, _) = await LoginAsync("9940000060");
        var (orgId, eventId, _) = await TieredEventAsync(owner, "view");

        var list = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types"));
        var tiers = list[0].GetProperty("price_tiers");
        Assert.Equal(3, tiers.GetArrayLength());
        Assert.Equal(2, tiers[0].GetProperty("min_size").GetInt32());
        Assert.Equal(TwoPrice, tiers[0].GetProperty("price_paise").GetInt64());
        Assert.Equal(5, tiers[2].GetProperty("max_size").GetInt32());
    }

    // ── Backward compatibility ───────────────────────────────────────────────────────

    /// <summary>The promise D-366 makes to every event that predates it: no bands, no change. A team
    /// ticket with a single price still charges that price for every size it admits.</summary>
    [Fact]
    public async Task A_ticket_with_no_bands_prices_exactly_as_before()
    {
        var (owner, _) = await LoginAsync("9940000070");
        var (_, eventId, ttId) = await TieredEventAsync(owner, "legacy", tiers: []);

        var (captain, _) = await LoginAsync("9940000071");
        var two = await Json(await captain.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 2, displayName = "Legacy Duo" }));
        var (captain2, _) = await LoginAsync("9940000072");
        var four = await Json(await captain2.PostAsJsonAsync($"/v1/events/{eventId}/orders",
            new { ticketTypeId = ttId, groupSize = 4, displayName = "Legacy Four" }));

        // One price, every size — D-372's behaviour, untouched.
        Assert.Equal(TwoPrice, two.GetProperty("amount_paise").GetInt64());
        Assert.Equal(TwoPrice, four.GetProperty("amount_paise").GetInt64());
    }

    /// <summary>Bands are replaced wholesale, not merged: an update carrying a new set leaves exactly
    /// that set, because a rule collection edited row by row can pass through a state with a hole in it.</summary>
    [Fact]
    public async Task Updating_the_bands_replaces_them_rather_than_merging()
    {
        var (owner, _) = await LoginAsync("9940000080");
        var (orgId, eventId, ttId) = await TieredEventAsync(owner, "replace");

        var updated = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}",
            TicketBody([new { minSize = 2, maxSize = 5, pricePaise = BigPrice }]));
        Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var rows = await db.TicketPriceTiers.AsNoTracking().Where(t => t.TicketTypeId == ttId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(BigPrice, rows[0].PricePaise);
    }

    /// <summary><b>The defect an audit found, not a test.</b> Web's host tickets page and mobile's
    /// ticket editor both PATCH the whole row and know nothing about bands. Under "absent replaces with
    /// none", renaming a banded ticket deleted every band and dropped every team size to the cheapest
    /// price — a silent repricing performed by a client that never mentioned prices.</summary>
    [Fact]
    public async Task An_update_that_says_nothing_about_bands_leaves_them_alone()
    {
        var (owner, _) = await LoginAsync("9940000100");
        var (orgId, eventId, ttId) = await TieredEventAsync(owner, "keep");

        // Exactly what the tickets page sends: every field of the row, no `priceTiers` key at all.
        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}", new
        {
            name = "Renamed Entry", pricePaise = 99_900, pricingUnit = "PerGroup", registrationMode = "Group",
            groupMin = 2, groupMax = 5, quantity = 100,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 5, isAllAccess = false, isCompetition = false,
        });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var rows = await db.TicketPriceTiers.AsNoTracking().Where(t => t.TicketTypeId == ttId).ToListAsync();
        Assert.Equal(3, rows.Count);

        // …and the headline still follows the bands, not the ₹999 that client sent alongside them.
        var tt = await db.TicketTypes.AsNoTracking().FirstAsync(t => t.Id == ttId);
        Assert.Equal(TwoPrice, tt.PricePaise);
    }

    /// <summary>An explicit empty set is the only way to clear bands — distinct from saying nothing.</summary>
    [Fact]
    public async Task An_explicit_empty_set_clears_the_bands()
    {
        var (owner, _) = await LoginAsync("9940000101");
        var (orgId, eventId, ttId) = await TieredEventAsync(owner, "clear");

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}",
            TicketBody([], price: 50_000));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Empty(await db.TicketPriceTiers.AsNoTracking().Where(t => t.TicketTypeId == ttId).ToListAsync());
        // With no bands the ticket is priced by its own amount again, exactly as a pre-D-366 row.
        Assert.Equal(50_000, (await db.TicketTypes.AsNoTracking().FirstAsync(t => t.Id == ttId)).PricePaise);
    }

    /// <summary>An update can invalidate the stored bands without mentioning them: narrowing the largest
    /// team to 3 leaves rules pricing teams of 4 and 5, which the ticket no longer admits. Refused, not
    /// silently trimmed — trimming would decide on the organiser's behalf which prices to delete.</summary>
    [Fact]
    public async Task Narrowing_the_team_size_under_the_stored_bands_is_refused()
    {
        var (owner, _) = await LoginAsync("9940000102");
        var (orgId, eventId, ttId) = await TieredEventAsync(owner, "narrow");

        var res = await owner.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}", new
        {
            name = "Team Entry", pricePaise = TwoPrice, pricingUnit = "PerGroup", registrationMode = "Group",
            groupMin = 2, groupMax = 3, quantity = 100,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 5, isAllAccess = false, isCompetition = false,
        });
        Assert.False(res.IsSuccessStatusCode);
        Assert.Equal("price_tier_outside_group_size", (await Json(res)).GetProperty("error").GetString());
    }

    /// <summary><b>The second audit defect.</b> `CloneAsync` names the ticket fields it copies, so it
    /// carried price, unit, mode and bounds and dropped the bands — a cloned team event kept only the
    /// cheapest one, and a team of five paid ₹250 instead of ₹400 with nothing saying the price list had
    /// changed. The same shape as D-340's thirty silent field losses.</summary>
    [Fact]
    public async Task Cloning_an_event_carries_the_bands_with_the_ticket()
    {
        var (owner, _) = await LoginAsync("9940000103");
        var (orgId, eventId, _) = await TieredEventAsync(owner, "clone");

        var clone = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/clone", new { title = "Cloned Tiers" });
        Assert.True(clone.IsSuccessStatusCode, await clone.Content.ReadAsStringAsync());
        var cloneId = (await Json(clone)).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var clonedTicket = await db.TicketTypes.AsNoTracking().FirstAsync(t => t.EventId == cloneId);
        var bands = await db.TicketPriceTiers.AsNoTracking()
            .Where(b => b.TicketTypeId == clonedTicket.Id).OrderBy(b => b.MinSize).ToListAsync();

        Assert.Equal(3, bands.Count);
        Assert.Equal(TwoPrice, bands[0].PricePaise);
        Assert.Equal(BigPrice, bands[2].PricePaise);
        Assert.Equal(5, bands[2].MaxSize);
    }

    /// <summary>The database refuses an overlap even when the service is bypassed — which is what makes
    /// "two rules match this team" a state that cannot exist rather than one the code hopes to catch.</summary>
    [Fact]
    public async Task The_database_itself_refuses_overlapping_bands()
    {
        var (owner, _) = await LoginAsync("9940000090");
        var (_, _, ttId) = await TieredEventAsync(owner, "exclude");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.TicketPriceTiers.Add(new TicketPriceTier
        {
            TicketTypeId = ttId, MinSize = 3, MaxSize = 4, PricePaise = ThreePrice,   // overlaps 3–3 and 4–5
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
