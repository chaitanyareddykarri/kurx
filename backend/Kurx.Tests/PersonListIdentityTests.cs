using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-20x — every real-user list (event assignments/team, speakers, org members, attendees,
/// event leaderboard) exposes enough identity for the client to render Connect/View-Profile, and the
/// new `/v1/public/users?q=` search endpoint. Sponsors are deliberately excluded (a brand/company
/// record, not a person) — not tested here because there is nothing to assert.</summary>
public class PersonListIdentityTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public PersonListIdentityTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Identity Cat", Slug = "identity-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9199{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task SetUsernameAsync(Guid userId, string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        // Fresh OTP-only test logins have Name = "" (no onboarding step ran) — set a real one so
        // assertions about "identity exposure" test something meaningful, not an empty string that
        // happens to also satisfy a hollowed-out check.
        if (string.IsNullOrEmpty(user.Name)) user.Name = "Test " + username;
        user.Username = username;
        user.ProfilePublic = true;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId) =>
        (await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Identity Fest " + Guid.NewGuid().ToString("N")[..6],
            description = "An event with plenty of detail for identity-exposure tests.",
            categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }))).GetProperty("id").GetGuid();

    [Fact]
    public async Task Event_assignment_list_exposes_the_assignees_identity()
    {
        var (owner, _) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Assignment Identity College " + Guid.NewGuid().ToString("N")[..6]);
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, volunteerId) = await LoginAsync(NextPhone());
        var volunteerPhone = await GetPhoneAsync(volunteerId);
        await SetUsernameAsync(volunteerId, "volidentity" + Guid.NewGuid().ToString("N")[..8]);

        var assign = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments",
            new { phone = volunteerPhone, role = "Volunteer", customRole = (string?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var created = await Json(assign);
        Assert.Equal(volunteerId, created.GetProperty("user_id").GetGuid());
        Assert.False(string.IsNullOrEmpty(created.GetProperty("assignee_name").GetString()));
        Assert.NotNull(created.GetProperty("assignee_username").GetString());

        var list = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/assignments"));
        var row = Assert.Single(list.EnumerateArray());
        Assert.Equal(volunteerId, row.GetProperty("user_id").GetGuid());
        Assert.NotNull(row.GetProperty("assignee_username").GetString());
    }

    [Fact]
    public async Task Speaker_without_a_linked_account_has_no_identity_speaker_with_one_does()
    {
        var (owner, _) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Speaker Identity College " + Guid.NewGuid().ToString("N")[..6]);
        var (_, realUserId) = await LoginAsync(NextPhone());
        await SetUsernameAsync(realUserId, "speakeridentity" + Guid.NewGuid().ToString("N")[..8]);

        var guestSpeaker = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/speakers",
            new { name = "Guest Speaker No Account", bio = "", photoKey = (string?)null, company = "Acme", role = "CTO", socialLinksJson = (string?)null, userId = (Guid?)null }));
        Assert.Equal(JsonValueKind.Null, guestSpeaker.GetProperty("user_id").ValueKind);   // no account -> no identity, correctly

        var linkedSpeaker = await Json(await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/speakers",
            new { name = "Real Speaker", bio = "", photoKey = (string?)null, company = "Acme", role = "CTO", socialLinksJson = (string?)null, userId = realUserId }));
        Assert.Equal(realUserId, linkedSpeaker.GetProperty("user_id").GetGuid());
        Assert.NotNull(linkedSpeaker.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Org_member_list_exposes_avatar_and_verification()
    {
        var (owner, ownerId) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Member Identity College " + Guid.NewGuid().ToString("N")[..6]);

        var members = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/members"));
        var row = Assert.Single(members.EnumerateArray(), m => m.GetProperty("user_id").GetGuid() == ownerId);
        Assert.True(row.TryGetProperty("is_verified", out _));
        Assert.True(row.TryGetProperty("avatar_key", out _));
    }

    [Fact]
    public async Task Public_user_search_finds_public_profiles_by_name_or_username()
    {
        var (_, userId) = await LoginAsync(NextPhone());
        var username = "searchable" + Guid.NewGuid().ToString("N")[..8];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.Name = "Zzyzx Findme Person";
            user.Username = username;
            user.ProfilePublic = true;
            await db.SaveChangesAsync();
        }

        var pub = _factory.CreateClient();
        var results = await Json(await pub.GetAsync("/v1/public/users?q=Zzyzx"));
        Assert.Contains(results.EnumerateArray(), r => r.GetProperty("username").GetString() == username);

        var tooShort = await Json(await pub.GetAsync("/v1/public/users?q=Z"));
        Assert.Empty(tooShort.EnumerateArray());   // below the 2-char floor — no full-table scan
    }

    [Fact]
    public async Task Attendee_list_exposes_identity_only_for_a_public_linked_account()
    {
        var (owner, _) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Attendee Identity College " + Guid.NewGuid().ToString("N")[..6]);
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, publicUserId) = await LoginAsync(NextPhone());
        await SetUsernameAsync(publicUserId, "attendeeidentity" + Guid.NewGuid().ToString("N")[..8]);
        var (_, privateUserId) = await LoginAsync(NextPhone());   // never claims a username / stays default ProfilePublic=true but no handle

        Guid publicTicketId, privateTicketId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ttPublic = new TicketType { EventId = eventId, Name = "General", PricePaise = 0, Quantity = 100, SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30) };
            db.TicketTypes.Add(ttPublic);
            var orderPublic = new Order { UserId = publicUserId, EventId = eventId, TicketTypeId = ttPublic.Id, Status = OrderStatus.Paid, AmountPaise = 0, RazorpayOrderId = "seed_" + Guid.NewGuid().ToString("N")[..8] };
            db.Orders.Add(orderPublic);
            var itemPublic = new OrderItem { OrderId = orderPublic.Id, TicketTypeId = ttPublic.Id, Qty = 1, UnitPricePaise = 0 };
            db.OrderItems.Add(itemPublic);
            var ticketPublic = new Ticket { OrderItemId = itemPublic.Id, EventId = eventId, UserId = publicUserId, HmacSig = "seed", State = TicketState.Issued };
            db.Tickets.Add(ticketPublic);

            var orderPrivate = new Order { UserId = privateUserId, EventId = eventId, TicketTypeId = ttPublic.Id, Status = OrderStatus.Paid, AmountPaise = 0, RazorpayOrderId = "seed_" + Guid.NewGuid().ToString("N")[..8] };
            db.Orders.Add(orderPrivate);
            var itemPrivate = new OrderItem { OrderId = orderPrivate.Id, TicketTypeId = ttPublic.Id, Qty = 1, UnitPricePaise = 0 };
            db.OrderItems.Add(itemPrivate);
            var ticketPrivate = new Ticket { OrderItemId = itemPrivate.Id, EventId = eventId, UserId = privateUserId, HmacSig = "seed", State = TicketState.Issued };
            db.Tickets.Add(ticketPrivate);
            await db.SaveChangesAsync();
            publicTicketId = ticketPublic.Id;
            privateTicketId = ticketPrivate.Id;
        }

        var attendees = await Json(await owner.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/attendees"));
        var items = attendees.GetProperty("items").EnumerateArray().ToList();
        var publicRow = Assert.Single(items, i => i.GetProperty("ticket_id").GetGuid() == publicTicketId);
        Assert.NotNull(publicRow.GetProperty("buyer_username").GetString());
        var privateRow = Assert.Single(items, i => i.GetProperty("ticket_id").GetGuid() == privateTicketId);
        Assert.Equal(JsonValueKind.Null, privateRow.GetProperty("buyer_username").ValueKind);   // no username claimed -> no profile link
    }

    [Fact]
    public async Task Event_leaderboard_exposes_username_camel_case_and_only_for_public_profiles()
    {
        var (owner, _) = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Leaderboard Identity College " + Guid.NewGuid().ToString("N")[..6]);
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, publicUserId) = await LoginAsync(NextPhone());
        await SetUsernameAsync(publicUserId, "boardidentity" + Guid.NewGuid().ToString("N")[..8]);
        var (_, privateUserId) = await LoginAsync(NextPhone());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // Claims a username but keeps the profile private — proves the leaderboard gates on
            // `ProfilePublic`, not merely on whether a username happens to be set.
            var priv = await db.Users.FirstAsync(u => u.Id == privateUserId);
            priv.Username = "hiddenboard" + Guid.NewGuid().ToString("N")[..8];
            priv.ProfilePublic = false;
            var tt = new TicketType { EventId = eventId, Name = "General", PricePaise = 0, Quantity = 100, SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30) };
            db.TicketTypes.Add(tt);
            foreach (var uid in new[] { publicUserId, privateUserId })
            {
                var order = new Order { UserId = uid, EventId = eventId, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = 0, RazorpayOrderId = "seed_" + Guid.NewGuid().ToString("N")[..8] };
                db.Orders.Add(order);
                var item = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = 0 };
                db.OrderItems.Add(item);
                db.Tickets.Add(new Ticket { OrderItemId = item.Id, EventId = eventId, UserId = uid, HmacSig = "seed", State = TicketState.CheckedIn });
            }
            await db.SaveChangesAsync();
        }

        var board = await Json(await owner.GetAsync($"/v1/leaderboards/events/{eventId}"));
        var entries = board.EnumerateArray().ToList();
        var publicRow = Assert.Single(entries, e => e.GetProperty("user_id").GetGuid() == publicUserId);
        Assert.NotNull(publicRow.GetProperty("username").GetString());
        var privateRow = Assert.Single(entries, e => e.GetProperty("user_id").GetGuid() == privateUserId);
        Assert.Equal(JsonValueKind.Null, privateRow.GetProperty("username").ValueKind);
    }

    private async Task<string> GetPhoneAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.Phone).FirstAsync();
    }
}
