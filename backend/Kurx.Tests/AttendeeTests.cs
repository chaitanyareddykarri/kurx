using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Host attendee sheet (D-054, AttendeeService): Owner/Manager/Staff read the roster + CSV for
/// their event; non-members are hidden (404-not-403). Seeds one registered buyer + one guest so the
/// row composition (user path vs guest-order path) is exercised. Real HTTP + kurx_test.</summary>
public class AttendeeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId, _orgId, _eventId;
    private static HttpClient _owner = null!, _outsider = null!;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public AttendeeTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;

            factory.ResetDatabase();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Tech", Slug = "tech-att" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
            }

            _owner = LoginAsAsync("9840000001").GetAwaiter().GetResult();
            _orgId = _factory.SeedVerifiedOrgForClient(_owner, "Attendee Test Org");
            _eventId = SeedEventWithTwoAttendeesAsync().GetAwaiter().GetResult();
            _outsider = LoginAsAsync("9840000020").GetAwaiter().GetResult();

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

    // A published event + free ticket type, with one registered buyer and one guest → two attendees.
    private async Task<Guid> SeedEventWithTwoAttendeesAsync()
    {
        var ev = await Json(await _owner.CreateEventAsync(_orgId, new
        {
            title = "Attendee Summit",
            description = "Roster test.",
            categoryId = _categoryId,
            venueName = "Roster Hall", venueAddress = "1 Main St", city = "Bengaluru",
            startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(30).AddHours(6),
        }));
        var eventId = ev.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        Assert.Equal(HttpStatusCode.OK, (await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{eventId}/transition", new { action = "publish" })).StatusCode);

        var ttId = (await Json(await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{eventId}/ticket-types", new
        {
            name = "General", pricePaise = 0, pricingUnit = "PerTicket", registrationMode = "Individual",
            quantity = 10, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(29),
            perUserLimit = 5, isAllAccess = false, isCompetition = false,
        }))).GetProperty("id").GetGuid();

        var registered = await LoginAsAsync("9840000010");
        Assert.Equal(HttpStatusCode.OK, (await registered.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId })).StatusCode);

        var guest = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync($"/v1/events/{eventId}/orders", new
        {
            ticketTypeId = ttId, guestName = "Priya Guest", guestPhone = "9820000501",
        })).StatusCode);

        return eventId;
    }

    [Fact]
    public async Task Owner_sees_roster_with_registered_and_guest_attendees()
    {
        var res = await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_eventId}/attendees");
        var raw = await res.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(raw).RootElement;

        Assert.Equal(2, body.GetProperty("total").GetInt32());
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        // Both PII paths land in the roster: the registered buyer's phone (user path) and the guest's
        // name (guest-order path). Asserted on the raw payload to stay independent of field-name casing.
        Assert.Contains("9840000010", raw);
        Assert.Contains("Priya Guest", raw);
    }

    [Fact]
    public async Task Non_member_cannot_list_attendees_and_is_hidden()
    {
        var res = await _outsider.GetAsync($"/v1/orgs/{_orgId}/events/{_eventId}/attendees");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);   // 404-not-403: never leak existence (D-018)
    }

    [Fact]
    public async Task State_filter_narrows_the_roster()
    {
        var issued = await Json(await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_eventId}/attendees?state=issued"));
        Assert.Equal(2, issued.GetProperty("total").GetInt32());

        var checkedIn = await Json(await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_eventId}/attendees?state=checkedin"));
        Assert.Equal(0, checkedIn.GetProperty("total").GetInt32());   // nobody has checked in yet
    }

    [Fact]
    public async Task Csv_export_returns_attendee_rows()
    {
        var res = await _owner.GetAsync($"/v1/orgs/{_orgId}/events/{_eventId}/attendees/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);
        var csv = await res.Content.ReadAsStringAsync();
        Assert.Contains("ticket_code,name,phone", csv);   // header
        Assert.Contains("Priya Guest", csv);              // a seeded attendee
    }
}
