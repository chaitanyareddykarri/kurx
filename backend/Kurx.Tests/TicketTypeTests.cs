using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

public class TicketTypeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public TicketTypeTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var category = new EventCategory { Level = CategoryLevel.Category, Name = "Tech", Slug = "tech-tt" };
                db.EventCategories.Add(category);
                db.SaveChanges();
                _categoryId = category.Id;
                _reset = true;
            }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid OrgId, Guid EventId)> OwnerWithDraftEventAsync(string phone, string orgName)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        var tokens = await Json(verify);
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());

        var orgId = _factory.SeedVerifiedOrgForClient(client, orgName);

        var ev = await Json(await client.CreateEventAsync(orgId, new
        {
            title = "TechConf 2026",
            description = "Annual summit.",
            categoryId = _categoryId,
            venueName = "Tech Hall",
            venueAddress = "456 Park Ave",
            city = "Hyderabad",
            startsAt = DateTime.UtcNow.AddDays(30),
            endsAt = DateTime.UtcNow.AddDays(30).AddHours(8),
        }));
        return (client, orgId, ev.GetProperty("id").GetGuid());
    }

    private static object ValidTicketBody(string name = "General Admission", long price = 50000, int qty = 200) => new
    {
        name,
        pricePaise = price,
        pricingUnit = "PerTicket",
        registrationMode = "Individual",
        quantity = qty,
        saleStarts = DateTime.UtcNow.AddDays(-1),
        saleEnds = DateTime.UtcNow.AddDays(29),
        perUserLimit = 2,
        isAllAccess = false,
    };

    // ── Ticket type CRUD ─────────────────────────────────────────────────────

    [Fact]
    public async Task Owner_can_create_ticket_type()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000001", "TT Org 1");
        var res = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody());
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await Json(res);
        Assert.Equal("General Admission", body.GetProperty("name").GetString());
        Assert.Equal(50000L, body.GetProperty("price_paise").GetInt64());
        Assert.Equal(200, body.GetProperty("quantity").GetInt32());
        Assert.Equal(0, body.GetProperty("sold").GetInt32());
        Assert.Equal(200, body.GetProperty("available").GetInt32());
    }

    [Fact]
    public async Task Owner_can_list_ticket_types_for_event()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000002", "TT Org 2");
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody("VIP", 100000, 50));
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody("Student", 10000, 500));

        var res = await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = (await Json(res)).EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        // Ordered by price ascending
        Assert.Equal("Student", list[0].GetProperty("name").GetString());
        Assert.Equal("VIP", list[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Owner_can_update_ticket_type()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000003", "TT Org 3");
        var create = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody()));
        var ttId = create.GetProperty("id").GetGuid();

        var patch = new
        {
            name = "Early Bird",
            pricePaise = 30000L,
            pricingUnit = "PerTicket",
            registrationMode = "Individual",
            quantity = 100,
            saleStarts = DateTime.UtcNow.AddDays(-1),
            saleEnds = DateTime.UtcNow.AddDays(29),
            perUserLimit = 1,
            isAllAccess = false,
        };
        var res = await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}", patch);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await Json(res);
        Assert.Equal("Early Bird", updated.GetProperty("name").GetString());
        Assert.Equal(30000L, updated.GetProperty("price_paise").GetInt64());
    }

    [Fact]
    public async Task Owner_can_delete_unsold_ticket_type()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000004", "TT Org 4");
        var create = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody()));
        var ttId = create.GetProperty("id").GetGuid();

        var res = await client.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = (await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types"))).EnumerateArray().ToList();
        Assert.Empty(list);
    }

    [Fact]
    public async Task Non_member_cannot_create_ticket_type()
    {
        var (ownerClient, orgId, eventId) = await OwnerWithDraftEventAsync("9800000005", "TT Org 5");

        // Create a second user who is not a member
        var stranger = _factory.CreateClient();
        await stranger.PostAsJsonAsync("/v1/auth/otp/request", new { phone = "9800000099" });
        var code = _factory.WhatsApp.LastOtpFor("9800000099");
        var verify = await Json(await stranger.PostAsJsonAsync("/v1/auth/otp/verify", new { phone = "9800000099", code }));
        stranger.DefaultRequestHeaders.Authorization = new("Bearer", verify.GetProperty("access_token").GetString());

        var res = await stranger.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Public_list_returns_404_for_draft_event()
    {
        var (_, _, eventId) = await OwnerWithDraftEventAsync("9800000006", "TT Org 6");
        await _factory.CreateClient().PostAsJsonAsync($"/v1/orgs/{Guid.NewGuid()}/events/{eventId}/ticket-types", ValidTicketBody());

        var res = await _factory.CreateClient().GetAsync($"/v1/events/{eventId}/ticket-types");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Group_ticket_type_requires_group_min_max()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000007", "TT Org 7");
        var body = new
        {
            name = "Group Pack",
            pricePaise = 200000L,
            pricingUnit = "PerGroup",
            registrationMode = "Group",
            // Missing GroupMin / GroupMax
            quantity = 20,
            saleStarts = DateTime.UtcNow.AddDays(-1),
            saleEnds = DateTime.UtcNow.AddDays(29),
            perUserLimit = 1,
            isAllAccess = false,
        };
        var res = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", body);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ── Form fields ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Owner_can_add_and_list_form_fields()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000010", "TT Org 10");
        var create = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody()));
        var ttId = create.GetProperty("id").GetGuid();

        var field = new { key = "dietary", label = "Dietary preference", type = "Text", scope = "PerRegistration", required = false };
        var res = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields", field);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var f = await Json(res);
        Assert.Equal("dietary", f.GetProperty("key").GetString());
        Assert.Equal("Text", f.GetProperty("type").GetString());

        var list = (await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields"))).EnumerateArray().ToList();
        Assert.Single(list);
        Assert.Equal("dietary", list[0].GetProperty("key").GetString());
    }

    [Fact]
    public async Task Duplicate_field_key_is_rejected()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000011", "TT Org 11");
        var create = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody()));
        var ttId = create.GetProperty("id").GetGuid();

        var field = new { key = "tshirt", label = "T-shirt size", type = "Select", scope = "PerRegistration", required = true };
        await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields", field);
        var dup = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields", field);
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    [Fact]
    public async Task Owner_can_update_and_delete_field()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000012", "TT Org 12");
        var create = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody()));
        var ttId = create.GetProperty("id").GetGuid();

        var field = new { key = "city", label = "Your city", type = "Text", scope = "PerRegistration", required = false };
        var added = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields", field));
        var fieldId = added.GetProperty("id").GetGuid();

        // Update label
        var update = new { key = "city", label = "City of residence", type = "Text", scope = "PerRegistration", required = true };
        var patchRes = await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields/{fieldId}", update);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);
        var updated = await Json(patchRes);
        Assert.Equal("City of residence", updated.GetProperty("label").GetString());
        Assert.True(updated.GetProperty("required").GetBoolean());

        // Delete
        var del = await client.DeleteAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields/{fieldId}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var list = (await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields"))).EnumerateArray().ToList();
        Assert.Empty(list);
    }

    [Fact]
    public async Task Field_key_must_be_snake_case()
    {
        var (client, orgId, eventId) = await OwnerWithDraftEventAsync("9800000013", "TT Org 13");
        var create = await Json(await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", ValidTicketBody()));
        var ttId = create.GetProperty("id").GetGuid();

        var bad = new { key = "MyField", label = "Bad key", type = "Text", scope = "PerRegistration", required = false };
        var res = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/fields", bad);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
