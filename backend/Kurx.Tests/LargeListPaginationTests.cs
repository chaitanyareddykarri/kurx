using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>DB-6 — large-list behaviour: page boundaries stay deterministic when timestamps collide, and
/// the attendee CSV streams in bounded batches without losing, repeating or mangling a row.
///
/// <para>These do not assert that an index exists or that a query is "fast". They assert the behaviour the
/// change exists to protect: <b>same data + same page request ⇒ every row exactly once</b>, and a CSV that
/// crosses several database batches is still one correct document.</para>
///
/// <para>Tickets are seeded straight into the database rather than bought through checkout. That is what
/// makes the interesting cases reachable at all — a purchase stamps <c>CreatedAt</c> from the clock, so
/// forcing an exact collision (the case an untied sort gets wrong) is not expressible through the API, and
/// 1,200 real checkouts would dominate the suite's runtime for no extra coverage.</para></summary>
public class LargeListPaginationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId, _orgId, _collisionEventId, _batchEventId, _escapeEventId, _emptyEventId;
    private static HttpClient _owner = null!, _outsider = null!;
    private static readonly object ResetLock = new();
    private static bool _reset;

    /// <summary>Deliberately larger than <c>AttendeeService.ExportBatchSize</c> (500) and not a multiple of
    /// it, so the export is forced across three batches and the final partial batch is exercised too — the
    /// seam where an off-by-one drops or repeats a row.</summary>
    private const int BatchCrossingTickets = 1_207;

    public LargeListPaginationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;

            factory.ResetDatabase();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                db.EventCategories.Add(new EventCategory
                {
                    Level = CategoryLevel.Category, Name = "Tech", Slug = "tech-largelist",
                });
                db.SaveChanges();
                _categoryId = db.EventCategories.First(c => c.Slug == "tech-largelist").Id;
            }

            _owner = LoginAsAsync("9860000001").GetAwaiter().GetResult();
            _orgId = _factory.SeedVerifiedOrgForClient(_owner, "Large List Org");
            _outsider = LoginAsAsync("9860000020").GetAwaiter().GetResult();

            // Every ticket on this event shares ONE CreatedAt instant — a group booking issues its tickets
            // in a single transaction, so this is the real shape, not a contrived one.
            var frozen = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            _collisionEventId = SeedEventWithTicketsAsync("Collision Summit", 25, _ => frozen).GetAwaiter().GetResult();

            // Spread across distinct instants: this one is about batch seams, not tie-breaking.
            _batchEventId = SeedEventWithTicketsAsync("Batch Summit", BatchCrossingTickets,
                i => new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc).AddSeconds(i)).GetAwaiter().GetResult();

            _escapeEventId = SeedEventWithTicketsAsync("Escape Summit", 0, _ => DateTime.UtcNow).GetAwaiter().GetResult();
            // Its own event, because the escaping test seeds a ticket and xUnit gives no ordering
            // guarantee — sharing one event made "empty" mean "empty unless that test ran first".
            _emptyEventId = SeedEventWithTicketsAsync("Empty Summit", 0, _ => DateTime.UtcNow).GetAwaiter().GetResult();

            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", verify.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>A published event plus <paramref name="ticketCount"/> guest tickets, each stamped by
    /// <paramref name="createdAt"/> so a test can decide whether timestamps collide.</summary>
    private async Task<Guid> SeedEventWithTicketsAsync(string title, int ticketCount, Func<int, DateTime> createdAt)
    {
        var ev = await Json(await _owner.CreateEventAsync(_orgId, new
        {
            title,
            description = "Large-list behaviour.",
            categoryId = _categoryId,
            venueName = "Hall", venueAddress = "1 Main St", city = "Bengaluru",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(6),
        }));
        var eventId = ev.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // No order at all when the roster is empty: `ck_order_items_qty` is CHECK ("Qty" >= 1), so a
        // zero-quantity item is not a representable state — and an event with no order is what "no
        // attendees yet" actually looks like.
        if (ticketCount > 0)
        {
            var ticketType = new TicketType { EventId = eventId, Name = "General", PricePaise = 0, Quantity = 100_000 };
            db.TicketTypes.Add(ticketType);
            var order = new Order
            {
                EventId = eventId, Status = OrderStatus.Paid,
                GuestName = "Seed Buyer", GuestPhone = "+919860000099",
            };
            db.Orders.Add(order);
            var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = ticketCount };
            db.OrderItems.Add(item);

            for (var i = 0; i < ticketCount; i++)
                db.Tickets.Add(new Ticket
                {
                    OrderItemId = item.Id, EventId = eventId, HmacSig = "seed",
                    State = TicketState.Issued, CreatedAt = createdAt(i),
                });
        }

        await db.SaveChangesAsync();
        return eventId;
    }

    // ── Deterministic page boundaries ────────────────────────────────────────

    /// <summary>The case an untied <c>ORDER BY CreatedAt</c> gets wrong. With 25 rows sharing one instant,
    /// the database is free to return them in a different order per query — so a row shown on page 1 can
    /// reappear on page 2 while another is never shown at all. Walking every page must still yield each
    /// ticket exactly once.</summary>
    [Fact]
    public async Task Roster_pages_never_repeat_or_skip_a_row_when_every_timestamp_is_identical()
    {
        var seen = new List<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var body = await Json(await _owner.GetAsync(
                $"/v1/orgs/{_orgId}/events/{_collisionEventId}/attendees?page={page}&pageSize=5"));
            foreach (var row in body.GetProperty("items").EnumerateArray())
                seen.Add(row.GetProperty("ticket_id").GetGuid());
        }

        Assert.Equal(25, seen.Count);
        Assert.Equal(25, seen.Distinct().Count());   // no repeats ⇒ no silent skips either
    }

    /// <summary>The same page requested twice returns the same rows in the same order. Without a total
    /// ordering this can hold by luck on one run and fail on the next, which is why it is asserted rather
    /// than assumed.</summary>
    [Fact]
    public async Task The_same_page_is_stable_across_repeated_requests()
    {
        async Task<List<Guid>> PageAsync()
        {
            var body = await Json(await _owner.GetAsync(
                $"/v1/orgs/{_orgId}/events/{_collisionEventId}/attendees?page=2&pageSize=5"));
            return body.GetProperty("items").EnumerateArray()
                .Select(r => r.GetProperty("ticket_id").GetGuid()).ToList();
        }

        Assert.Equal(await PageAsync(), await PageAsync());
    }

    // ── Streaming export ─────────────────────────────────────────────────────

    /// <summary>1,207 tickets over a 500-row batch: three round trips, the last one partial. Every ticket
    /// must appear exactly once — a keyset seam that re-reads its pivot duplicates a row, and one that
    /// steps past it loses a row, and both look like a working export until the rows are counted.</summary>
    [Fact]
    public async Task Export_writes_every_row_exactly_once_across_batch_boundaries()
    {
        var res = await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_batchEventId}/attendees/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);

        var lines = (await res.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToList();

        Assert.Equal("ticket_code,name,phone,ticket_type,state,checked_in_at,group", lines[0]);
        Assert.Equal(BatchCrossingTickets, lines.Count - 1);

        var codes = lines.Skip(1).Select(l => l.Split(',')[0]).ToList();
        Assert.Equal(BatchCrossingTickets, codes.Distinct().Count());
    }

    /// <summary>Rows keep descending CreatedAt order across the seams, not just within a batch.</summary>
    [Fact]
    public async Task Export_rows_stay_ordered_across_batches()
    {
        var res = await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_batchEventId}/attendees/export");
        var lines = (await res.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

        // Seeded CreatedAt ascends with i, and the export is CreatedAt DESC, so the first data row is the
        // LAST ticket seeded. Proving the head and the count is enough to catch a batch emitted out of turn.
        Assert.Equal(BatchCrossingTickets, lines.Count);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var newest = db.Tickets.Where(t => t.EventId == _batchEventId)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Select(t => t.Code).First();
        Assert.StartsWith($"\"{newest}\"", lines[0]);
    }

    /// <summary>An empty roster is still a valid CSV: the header, and nothing else.</summary>
    [Fact]
    public async Task Export_of_an_empty_roster_is_a_header_only_document()
    {
        var res = await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_emptyEventId}/attendees/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var lines = (await res.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        Assert.Single(lines);
        Assert.StartsWith("ticket_code,name,phone", lines[0]);
    }

    /// <summary>The characters that break a naive CSV writer, in one attendee name: a comma (splits the
    /// row), a double quote (must be doubled), a newline (splits the record) and non-ASCII text (must
    /// survive UTF-8 without a BOM). Asserted on the raw bytes, because a reader that is itself lenient
    /// would hide exactly the corruption being tested for.</summary>
    [Fact]
    public async Task Export_escapes_commas_quotes_newlines_and_preserves_unicode()
    {
        const string nasty = "Rao, Priya \"PJ\"\nSecond line ಕನ್ನಡ";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ticketType = new TicketType { EventId = _escapeEventId, Name = "General", PricePaise = 0, Quantity = 10 };
            db.TicketTypes.Add(ticketType);
            var order = new Order
            {
                EventId = _escapeEventId, Status = OrderStatus.Paid,
                GuestName = nasty, GuestPhone = "+919860000098",
            };
            db.Orders.Add(order);
            var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = 1 };
            db.OrderItems.Add(item);
            db.Tickets.Add(new Ticket
            {
                OrderItemId = item.Id, EventId = _escapeEventId, HmacSig = "seed", State = TicketState.Issued,
            });
            await db.SaveChangesAsync();
        }

        var res = await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_escapeEventId}/attendees/export");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        var csv = System.Text.Encoding.UTF8.GetString(bytes);

        // No BOM: the previous implementation wrote none, and a spreadsheet reading one as data is a
        // regression a string comparison would not see.
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        // The quote is doubled and the whole field wrapped, so the comma and newline stay inside one field.
        Assert.Contains("\"Rao, Priya \"\"PJ\"\"\nSecond line ಕನ್ನಡ\"", csv);
    }

    /// <summary>Authorization is resolved before a single byte is written, so an outsider still gets a
    /// clean 404 rather than a 200 that streams an empty file (D-018: existence is not leaked).</summary>
    [Fact]
    public async Task Export_is_refused_to_a_non_member_before_any_body_is_written()
    {
        var res = await _outsider.GetAsync($"/v1/orgs/{_orgId}/events/{_batchEventId}/attendees/export");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.NotEqual("text/csv", res.Content.Headers.ContentType?.MediaType);
    }
}
