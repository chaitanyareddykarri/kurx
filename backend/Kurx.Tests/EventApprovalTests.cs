using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Event approval + payment gate (M8, D-047). Free events publish directly; a PAID event
/// (any priced ticket type) can't self-publish — the org must submit_review (gated on the organizer's
/// paid-organizing capability + org verification, M7) and a platform reviewer publishes it. Payment
/// readiness is computed live. Real HTTP/kurx_test.</summary>
public class EventApprovalTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;

    public EventApprovalTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Approval Cat", Slug = "approval-cat" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<HttpClient> ReviewerAsync(string phone)
    {
        var client = await LoginAsync(phone);
        var userId = (await Json(await client.GetAsync("/v1/me"))).GetProperty("id").GetGuid();
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
            .GrantAsync(userId, PlatformRole.VerificationReviewer, grantedBy: null);
        return client;
    }

    // D-075: seed a Verified org the caller manages (institutions are no longer self-minted via POST /v1/orgs).
    private Task<Guid> CreateOrgAsync(HttpClient client, string name)
        => Task.FromResult(_factory.SeedVerifiedOrgForClient(client, name));

    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId)
    {
        var res = await client.CreateEventAsync(orgId, new
        {
            title = "Gala " + Guid.NewGuid().ToString("N")[..6],
            description = "A great event with details.",
            categoryId = _categoryId,
            venueName = "Main Hall",
            city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20),
            endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task AddPaidTicketTypeAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.TicketTypes.Add(new TicketType
        {
            EventId = eventId, Name = "General", PricePaise = 50000, Quantity = 100,
            SaleStarts = DateTime.UtcNow, SaleEnds = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();
    }

    private static async Task VerifyPaidIdentityAsync(HttpClient client)
    {
        await client.PostAsJsonAsync("/v1/me/identity/pan", new { pan = "ABCDE1234F", name = "Organizer" });
        await client.PostAsJsonAsync("/v1/me/identity/bank",
            new { accountNumber = "12345678", ifsc = "HDFC0001234", holderName = "Organizer" });
    }

    private async Task VerifyOrgAsync(HttpClient owner, HttpClient reviewer, Guid orgId)
    {
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/verification/submit",
            new { documents = new[] { new { docType = "registration_cert", storageKey = "k" } } });
        await reviewer.PostAsJsonAsync($"/v1/admin/orgs/{orgId}/verification/review", new { decision = "approve" });
    }

    /// <summary>D-266 M5 added institutional authorization to the publish gate. This class is about the
    /// PAID-event approval rules (who may publish, and after what review), so the authorization is fixture
    /// setup — seeded only for publish, leaving every other transition to be refused on its own merits.</summary>
    private Task<HttpResponseMessage> Transition(HttpClient client, Guid orgId, Guid eventId, string action)
    {
        if (action is "publish") _factory.SeedApprovedEventAuthorization(eventId);
        return client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action });
    }

    [Fact]
    public async Task Free_event_publishes_directly()
    {
        var owner = await LoginAsync("9950000001");
        var orgId = await CreateOrgAsync(owner, "Free Publish Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var res = await Transition(owner, orgId, eventId, "publish");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("published", (await Json(res)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Paid_event_cannot_self_publish()
    {
        var owner = await LoginAsync("9950000002");
        var orgId = await CreateOrgAsync(owner, "Paid Self Publish Org");
        var eventId = await CreateEventAsync(owner, orgId);
        await AddPaidTicketTypeAsync(eventId);
        var res = await Transition(owner, orgId, eventId, "publish");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("paid_event_requires_review", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Paid_submit_review_blocked_when_organizer_not_verified()
    {
        var owner = await LoginAsync("9950000003");
        var orgId = await CreateOrgAsync(owner, "Unverified Paid Org");
        var eventId = await CreateEventAsync(owner, orgId);
        await AddPaidTicketTypeAsync(eventId);
        var res = await Transition(owner, orgId, eventId, "submit_review");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("organizer_not_verified_for_paid", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Paid_event_full_review_flow_and_readiness()
    {
        var reviewer = await ReviewerAsync("9950000004");
        var owner = await LoginAsync("9950000005");
        var orgId = await CreateOrgAsync(owner, "Verified Paid Org");
        await VerifyPaidIdentityAsync(owner);
        await VerifyOrgAsync(owner, reviewer, orgId);

        var eventId = await CreateEventAsync(owner, orgId);
        await AddPaidTicketTypeAsync(eventId);

        // Paid, but not yet published → payments not enabled.
        var pr1 = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/payment-readiness"));
        Assert.True(pr1.GetProperty("is_paid").GetBoolean());
        Assert.False(pr1.GetProperty("payments_enabled").GetBoolean());

        // Organizer + org now verified → submit_review succeeds.
        // D-266 M4 Stage 4: submit_review is now an alias landing on PendingReview — the retired InReview
        // conflated "waiting for a reviewer" with "a reviewer has it", which the claim flow needs apart.
        // The action name is kept so existing clients still work; only the state it produces changed.
        var sr = await Transition(owner, orgId, eventId, "submit_review");
        Assert.Equal(HttpStatusCode.OK, sr.StatusCode);
        Assert.Equal("pendingreview", (await Json(sr)).GetProperty("status").GetString());

        // Org owner still can't publish a paid event (not a reviewer).
        Assert.Equal(HttpStatusCode.Forbidden, (await Transition(owner, orgId, eventId, "publish")).StatusCode);

        // Reviewer publishes.
        var rp = await Transition(reviewer, orgId, eventId, "publish");
        Assert.Equal(HttpStatusCode.OK, rp.StatusCode);
        Assert.Equal("published", (await Json(rp)).GetProperty("status").GetString());

        // Now payments are enabled (computed live).
        var pr2 = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/payment-readiness"));
        Assert.True(pr2.GetProperty("payments_enabled").GetBoolean());
    }
}
