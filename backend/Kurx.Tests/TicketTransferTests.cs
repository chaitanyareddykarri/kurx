using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Ticket transfer claim flow (D-062). `TicketTransferService.ClaimAsync` reassigns `ticket.UserId`
/// and rotates `ticket.Code` so the sender's old QR screenshot dies. These tests pin the ownership
/// consequences of that rotation, which shipped unasserted: a ticket must follow its *current owner*
/// (`ticket.UserId`), never the order's buyer.
///
/// The scenario (organizer / sender / recipient as three distinct users, so nothing here is
/// explained by org-staff access) is built once in the constructor and asserted by each test; the
/// anonymous-IP rate limiter (see OrderTests) means total HTTP volume per class must stay well
/// under 60.
/// </summary>
public class TicketTransferTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    private static Guid _categoryId, _orgId, _eventId, _ticketId;
    private static Guid _oldCode;
    private static HttpClient _owner = null!, _sender = null!, _recipient = null!;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public TicketTransferTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Xfer", Slug = "xfer-cat" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
            }

            _owner = LoginAsAsync("9810098001").GetAwaiter().GetResult();
            _sender = LoginAsAsync("9810098002").GetAwaiter().GetResult();
            _recipient = LoginAsAsync("9810098003").GetAwaiter().GetResult();

            _orgId = _factory.SeedVerifiedOrgForClient(_owner, "Transfer Test Org");

            BuildClaimedTransferAsync().GetAwaiter().GetResult();
            _reset = true;
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsAsync(string phone)
    {
        var client = _factory.CreateClient();
        var req = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        if (!req.IsSuccessStatusCode)
            throw new Exception($"OTP request failed for {phone}: {req.StatusCode}");
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", verify.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>Organizer publishes a free event; sender buys a ticket, transfers it, recipient claims it.</summary>
    private async Task BuildClaimedTransferAsync()
    {
        var ev = await Json(await _owner.CreateEventAsync(_orgId, new
        {
            title = "Transfer Summit",
            description = "A published event for transfer tests.",
            categoryId = _categoryId,
            venueName = "Transfer Hall",
            city = "Bengaluru",
            startsAt = DateTime.UtcNow.AddDays(30),
            endsAt = DateTime.UtcNow.AddDays(30).AddHours(8),
        }));
        _eventId = ev.GetProperty("id").GetGuid();
        // D-388 — the ticket type is created BEFORE the event goes live, which is the order the platform
        // now requires: a live Public event's ticket types are frozen behind an approved change request.
        // This fixture only ever needed "a published event that sells one free ticket", and configuring
        // then publishing reaches that state the way a real organiser does. Nothing about transfers —
        // this suite's actual subject — depends on the order these two setup calls were made in.
        var ttRes = await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{_eventId}/ticket-types", new
        {
            name = "Free",
            pricePaise = 0L,
            pricingUnit = "PerTicket",
            registrationMode = "Individual",
            quantity = 5,
            saleStarts = DateTime.UtcNow.AddDays(-1),
            saleEnds = DateTime.UtcNow.AddDays(29),
            perUserLimit = 5,
            isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, ttRes.StatusCode);

        _factory.SeedApprovedEventAuthorization(_eventId);   // D-266 M5 — fixture needs a published event
        var published = await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{_eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var ticketTypeId = (await Json(ttRes)).GetProperty("id").GetGuid();

        var order = await Json(await _sender.PostAsJsonAsync($"/v1/events/{_eventId}/orders", new { ticketTypeId }));
        var ticket = order.GetProperty("tickets")[0];
        _ticketId = ticket.GetProperty("id").GetGuid();
        _oldCode = ticket.GetProperty("code").GetGuid();

        var transfer = await Json(await _sender.PostAsJsonAsync($"/v1/tickets/{_ticketId}/transfer",
            new { toPhone = "9810098003" }));
        var transferCode = transfer.GetProperty("transfer_code").GetString();

        var claim = await _recipient.PostAsJsonAsync("/v1/transfers/claim", new { transferCode });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
    }

    /// <summary>Reads the ticket's current code straight from the DB — the source of truth the
    /// endpoints are being checked against (a leaking endpoint can't be used to learn it).</summary>
    private Guid CurrentCode()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.Tickets.Single(t => t.Id == _ticketId).Code;
    }

    private static List<JsonElement> TicketsIn(JsonElement orders) =>
        orders.EnumerateArray().SelectMany(o => o.GetProperty("tickets").EnumerateArray()).ToList();

    [Fact]
    public async Task Claim_moves_the_ticket_off_the_senders_list_and_onto_the_claimants()
    {
        var senderTickets = TicketsIn(await Json(await _sender.GetAsync("/v1/orders")));
        Assert.DoesNotContain(senderTickets, t => t.GetProperty("id").GetGuid() == _ticketId);

        var recipientOrders = await Json(await _recipient.GetAsync("/v1/orders"));
        var claimed = Assert.Single(TicketsIn(recipientOrders), t => t.GetProperty("id").GetGuid() == _ticketId);
        Assert.Equal(CurrentCode(), claimed.GetProperty("code").GetGuid());

        // Surfacing the claimed ticket means surfacing the buyer's order alongside it. That order is
        // not the claimant's: it must never carry the buyer's guest access token, which is a bearer
        // credential for the whole order (D-036).
        foreach (var order in recipientOrders.EnumerateArray())
            Assert.Null(order.GetProperty("guest_access_token").GetString());
    }

    [Fact]
    public async Task Sender_is_never_served_the_rotated_code_by_the_order_list()
    {
        var rotated = CurrentCode();
        Assert.NotEqual(_oldCode, rotated); // the claim really did rotate it

        var raw = await (await _sender.GetAsync("/v1/orders")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(rotated.ToString(), raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rotated_code_binds_to_the_claimant_and_the_old_code_dies_at_the_gate()
    {
        // The sender's pre-transfer screenshot no longer scans.
        var oldScan = await _owner.PostAsJsonAsync($"/v1/gate/{_eventId}/scan", new { ticketCode = _oldCode });
        Assert.Equal(HttpStatusCode.BadRequest, oldScan.StatusCode);
        Assert.Equal("invalid_code", (await Json(oldScan)).GetProperty("error").GetString());

        // The QR image for the rotated code belongs to the claimant, not the former holder.
        var rotated = CurrentCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await _sender.GetAsync($"/v1/tickets/{rotated}/qr.png")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _recipient.GetAsync($"/v1/tickets/{rotated}/qr.png")).StatusCode);

        // And the rotated code admits at the gate.
        var newScan = await _owner.PostAsJsonAsync($"/v1/gate/{_eventId}/scan", new { ticketCode = rotated });
        Assert.Equal(HttpStatusCode.OK, newScan.StatusCode);
        Assert.True((await Json(newScan)).GetProperty("admitted").GetBoolean());
    }
}
