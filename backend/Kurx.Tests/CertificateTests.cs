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

public class CertificateTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static Guid _orgId;
    private static HttpClient _owner = null!;
    private static HttpClient _attendee = null!;
    private static HttpClient _stranger = null!;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateTests(KurxApiFactory factory)
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
                    var category = new EventCategory { Level = CategoryLevel.Category, Name = "Tech", Slug = "tech-cert" };
                    db.EventCategories.Add(category);
                    db.SaveChanges();
                    _categoryId = category.Id;
                }

                _owner = LoginAsAsync("9810098001").GetAwaiter().GetResult();
                _orgId = _factory.SeedVerifiedOrgForClient(_owner, "Cert Test Org");

                _attendee = LoginAsAsync("9810098010").GetAwaiter().GetResult();
                _stranger = LoginAsAsync("9810098011").GetAwaiter().GetResult();

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

    private async Task<(Guid EventId, Guid TicketCode)> PublishedEventWithTicketAsync(string title)
    {
        var ev = await Json(await _owner.CreateEventAsync(_orgId, new
        {
            title,
            description = "Annual summit.",
            categoryId = _categoryId,
            venueName = "Cert Hall",
            venueAddress = "1 Main St",
            city = "Bengaluru",
            startsAt = DateTime.UtcNow.AddDays(-1),
            endsAt = DateTime.UtcNow.AddHours(-1),
        }));
        var eventId = ev.GetProperty("id").GetGuid();
        // D-266 M5: a Public event representing a non-personal org needs an approved institutional
        // authorization before it can publish. This fixture's subject is certificates, not that rule.
        _factory.SeedApprovedEventAuthorization(eventId);
        Assert.Equal(HttpStatusCode.OK,
            (await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{eventId}/transition", new { action = "publish" })).StatusCode);

        var tt = await Json(await _owner.PostAsJsonAsync($"/v1/orgs/{_orgId}/events/{eventId}/ticket-types", new
        {
            name = "General",
            pricePaise = 0,
            pricingUnit = "PerTicket",
            registrationMode = "Individual",
            quantity = 10,
            saleStarts = DateTime.UtcNow.AddDays(-10),
            saleEnds = DateTime.UtcNow.AddDays(10),
            perUserLimit = 5,
            isAllAccess = false,
        }));
        var ttId = tt.GetProperty("id").GetGuid();

        var order = await Json(await _attendee.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId }));
        var ticketCode = order.GetProperty("tickets")[0].GetProperty("code").GetGuid();

        return (eventId, ticketCode);
    }

    [Fact]
    public async Task Generate_creates_real_certificate_and_verify_endpoint_reflects_it()
    {
        var (eventId, _) = await PublishedEventWithTicketAsync("Cert Event 1");

        var res = await _owner.PostAsync($"/v1/events/{eventId}/certificates/generate", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(1, (await Json(res)).GetProperty("generated").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var cert = await db.Certificates.AsNoTracking().SingleAsync(c => c.EventId == eventId);
        Assert.NotNull(cert.PdfKey);
        // OTP-only test users have no Email set (never onboarded via PATCH /v1/me), so the
        // email-attachment branch is skipped and the certificate stays Generated, not Emailed.
        Assert.Equal(CertificateStatus.Generated, cert.Status);

        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var pdfBytes = await storage.GetAsync(cert.PdfKey!);
        Assert.True(pdfBytes.Length > 1000, "rendered PDF should be a real multi-KB document, not a placeholder");
        Assert.Equal((byte)'%', pdfBytes[0]); // PDF magic bytes: %PDF-
        Assert.Equal((byte)'P', pdfBytes[1]);

        var verify = await Json(await _factory.CreateClient().GetAsync($"/v1/certificates/{cert.VerifyCode}"));
        Assert.Equal("generated", verify.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(verify.GetProperty("pdf_url").GetString()));
    }

    [Fact]
    public async Task Public_certificate_list_returns_the_users_issued_certificates()
    {
        // Regression: the list query ordered by a projected DTO property, which EF couldn't translate
        // and 500'd at runtime for every call. It must return the ticket-holder's public certificates.
        var (eventId, _) = await PublishedEventWithTicketAsync("Cert Event List");
        Assert.Equal(HttpStatusCode.OK,
            (await _owner.PostAsync($"/v1/events/{eventId}/certificates/generate", null)).StatusCode);

        // The attendee holds the ticket, so they own the certificate; give them a public username to look up by.
        const string username = "certlistuser";
        Assert.Equal(HttpStatusCode.OK,
            (await _attendee.PatchAsJsonAsync("/v1/me/profile", new { name = "Cert List User", username })).StatusCode);

        var res = await _factory.CreateClient().GetAsync($"/v1/public/users/{username}/certificates");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await Json(res);
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        Assert.Contains(list.EnumerateArray(), c => c.GetProperty("event_title").GetString() == "Cert Event List");

        // The key is `issued_at`, never `created_at`. The property behind it is PublicCertificateCard.CreatedAt,
        // renamed on the wire by the hand-written mapper this endpoint used to project through. When the mapper
        // was deleted in favour of returning the record, only a [JsonPropertyName] kept the name — and both web
        // (`issued_at: z.string()`) and Flutter (`@JsonKey(name: 'issued_at')`) bind the renamed one. Dropping
        // the attribute compiles, passes every other test, and silently empties the issue date on both clients.
        var card = list.EnumerateArray().First(c => c.GetProperty("event_title").GetString() == "Cert Event List");
        Assert.True(card.TryGetProperty("issued_at", out var issuedAt), "the certificate card must expose issued_at");
        Assert.NotEqual(default, issuedAt.GetDateTime());
        Assert.False(card.TryGetProperty("created_at", out _), "created_at would mean the rename was lost");
    }

    [Fact]
    public async Task Generate_is_idempotent()
    {
        var (eventId, _) = await PublishedEventWithTicketAsync("Cert Event 2");

        var first = await Json(await _owner.PostAsync($"/v1/events/{eventId}/certificates/generate", null));
        Assert.Equal(1, first.GetProperty("generated").GetInt32());

        var second = await Json(await _owner.PostAsync($"/v1/events/{eventId}/certificates/generate", null));
        Assert.Equal(0, second.GetProperty("generated").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.Certificates.CountAsync(c => c.EventId == eventId));
    }

    [Fact]
    public async Task Generate_is_forbidden_for_non_member()
    {
        var (eventId, _) = await PublishedEventWithTicketAsync("Cert Event 3");

        var res = await _stranger.PostAsync($"/v1/events/{eventId}/certificates/generate", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Ticket_qr_endpoint_returns_real_png_and_enforces_ownership()
    {
        var (_, ticketCode) = await PublishedEventWithTicketAsync("Cert Event 4");

        var ownerRes = await _attendee.GetAsync($"/v1/tickets/{ticketCode}/qr.png");
        Assert.Equal(HttpStatusCode.OK, ownerRes.StatusCode);
        Assert.Equal("image/png", ownerRes.Content.Headers.ContentType?.MediaType);
        var bytes = await ownerRes.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.Equal(0x89, bytes[0]); // PNG magic byte

        // Event organizer (staff) may also fetch it.
        Assert.Equal(HttpStatusCode.OK, (await _owner.GetAsync($"/v1/tickets/{ticketCode}/qr.png")).StatusCode);

        // An unrelated stranger may not.
        Assert.Equal(HttpStatusCode.Forbidden, (await _stranger.GetAsync($"/v1/tickets/{ticketCode}/qr.png")).StatusCode);
    }

    [Fact]
    public async Task Revoke_by_owner_is_visible_on_verify_not_hidden()
    {
        var (eventId, _) = await PublishedEventWithTicketAsync("Cert Event 5");
        await _owner.PostAsync($"/v1/events/{eventId}/certificates/generate", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var cert = await db.Certificates.AsNoTracking().SingleAsync(c => c.EventId == eventId);

        var res = await _owner.PostAsJsonAsync($"/v1/certificates/{cert.Id}/revoke", new { reason = "Issued in error" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var verifyRes = await _factory.CreateClient().GetAsync($"/v1/certificates/{cert.VerifyCode}");
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode); // must not 404 a revoked cert
        var verify = await Json(verifyRes);
        Assert.True(verify.GetProperty("is_revoked").GetBoolean());
        Assert.Equal("Issued in error", verify.GetProperty("revoked_reason").GetString());
    }

    [Fact]
    public async Task Revoke_is_forbidden_for_non_member()
    {
        var (eventId, _) = await PublishedEventWithTicketAsync("Cert Event 6");
        await _owner.PostAsync($"/v1/events/{eventId}/certificates/generate", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var cert = await db.Certificates.AsNoTracking().SingleAsync(c => c.EventId == eventId);

        var res = await _stranger.PostAsJsonAsync($"/v1/certificates/{cert.Id}/revoke", new { reason = "n/a" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
