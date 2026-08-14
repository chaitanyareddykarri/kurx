using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-201/D-203/D-206 (Professional Identity System): event/participation/attendance dedup by
/// EventId, per-org role+verification+period aggregation, and the certificate-vs-badge achievements
/// split. Public profile endpoints are anonymous, so these hit real HTTP against real kurx_test with no
/// login needed for the profile OWNER (seeded directly); LoginAsync is only used to get an org owner.</summary>
public class PublicProfileTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;
    private static int _userSeq;

    public PublicProfileTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
                var cat = new EventCategory { Level = CategoryLevel.Category, Name = "Profile Cat", Slug = "profile-cat" };
                db.EventCategories.Add(cat);
                db.SaveChanges();
                _categoryId = cat.Id;
                _reset = true;
            }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9196{Interlocked.Increment(ref _phoneSeq):D6}";
    private string NextUsername() => $"prof{Interlocked.Increment(ref _userSeq)}u{Guid.NewGuid():N}"[..20];

    private async Task<HttpClient> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>Seeds a User row directly (no OTP login needed — profile reads are anonymous) with a
    /// public profile, and returns (userId, username).</summary>
    private async Task<(Guid Id, string Username)> SeedProfileUserAsync(
        bool showAttended = true, bool showCertificates = true, bool showAllies = true)
    {
        var username = NextUsername();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User
        {
            Phone = NextPhone(), Name = "Profile Test " + username, Username = username,
            ProfilePublic = true, ShowAttended = showAttended, ShowCertificates = showCertificates, ShowAllies = showAllies,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, username);
    }

    private async Task<Guid> CreateAndPublishPastEventAsync(HttpClient owner, Guid orgId, int daysAgo = 5)
    {
        var created = await Json(await owner.CreateEventAsync(orgId, new
        {
            title = "Profile Fest " + Guid.NewGuid().ToString("N")[..6],
            description = "An event with plenty of detail for profile read-model tests.",
            categoryId = _categoryId, venueName = "Main Hall", city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }));
        var eventId = created.GetProperty("id").GetGuid();
        _factory.SeedApprovedEventAuthorization(eventId);   // D-266 M5 — fixture needs a published event
        var publish = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.FirstAsync(e => e.Id == eventId);
        ev.StartsAt = DateTime.UtcNow.AddDays(-daysAgo);
        ev.EndsAt = DateTime.UtcNow.AddDays(-daysAgo).AddHours(2);
        await db.SaveChangesAsync();
        return eventId;
    }

    // ── Event / participation dedup (D-201 fix) ─────────────────────────────

    [Fact]
    public async Task Two_roles_on_the_same_event_count_as_one_event_with_both_roles_listed()
    {
        var owner = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Dedup College " + Guid.NewGuid().ToString("N")[..6]);
        var (userId, username) = await SeedProfileUserAsync();
        var eventId = await CreateAndPublishPastEventAsync(owner, orgId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.EventParticipants.AddRange(
                new EventParticipant { EventId = eventId, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
                    RoleSlug = "speaker", State = ParticipantState.Completed, Visibility = ParticipantVisibility.Public, CompletedAt = DateTime.UtcNow.AddDays(-5) },
                new EventParticipant { EventId = eventId, SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
                    RoleSlug = "judge", State = ParticipantState.Completed, Visibility = ParticipantVisibility.Public, CompletedAt = DateTime.UtcNow.AddDays(-5) });
            await db.SaveChangesAsync();
        }

        var pub = _factory.CreateClient();
        var profile = await Json(await pub.GetAsync($"/v1/public/users/{username}"));
        Assert.Equal(1, profile.GetProperty("stats").GetProperty("participations").GetInt32());

        var timeline = await Json(await pub.GetAsync($"/v1/public/users/{username}/timeline?pageSize=50"));
        var entries = timeline.EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "participation").ToList();
        var entry = Assert.Single(entries);
        var roles = entry.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.Contains("Speaker", roles);
        Assert.Contains("Judge", roles);
    }

    /// <summary>Ticket.OrderItemId is a real FK — a valid ticket needs a real TicketType/Order/OrderItem
    /// chain underneath it, mirroring RefundEndpointsTests' fixture shape.</summary>
    private static async Task<Guid> SeedTicketAsync(KurxDbContext db, Guid eventId, Guid userId, TicketState state, DateTime? checkedInAt = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ticketType = new TicketType { EventId = eventId, Name = "General " + suffix, PricePaise = 0, Quantity = 1000, Sold = 0,
            SaleStarts = DateTime.UtcNow.AddDays(-30), SaleEnds = DateTime.UtcNow.AddDays(30) };
        db.TicketTypes.Add(ticketType);
        var order = new Order { UserId = userId, EventId = eventId, TicketTypeId = ticketType.Id,
            Status = OrderStatus.Paid, AmountPaise = 0, RazorpayOrderId = "seed_" + suffix };
        db.Orders.Add(order);
        var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = 1, UnitPricePaise = 0 };
        db.OrderItems.Add(item);
        var ticket = new Ticket { OrderItemId = item.Id, EventId = eventId, UserId = userId, HmacSig = "seed",
            State = state, CheckedInAt = checkedInAt };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket.Id;
    }

    [Fact]
    public async Task Two_checked_in_tickets_for_one_event_count_as_one_attended_event()
    {
        var owner = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Attend Dedup College " + Guid.NewGuid().ToString("N")[..6]);
        var (userId, username) = await SeedProfileUserAsync();
        var eventId = await CreateAndPublishPastEventAsync(owner, orgId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await SeedTicketAsync(db, eventId, userId, TicketState.CheckedIn, DateTime.UtcNow.AddDays(-5));
            await SeedTicketAsync(db, eventId, userId, TicketState.CheckedIn, DateTime.UtcNow.AddDays(-5));
        }

        var pub = _factory.CreateClient();
        var profile = await Json(await pub.GetAsync($"/v1/public/users/{username}"));
        Assert.Equal(1, profile.GetProperty("stats").GetProperty("events_attended").GetInt32());

        var events = await Json(await pub.GetAsync($"/v1/public/users/{username}/events?type=attended"));
        Assert.Single(events.EnumerateArray());
    }

    // ── Achievements: certificate-vs-badge source split (D-203) ─────────────

    [Fact]
    public async Task Achievements_count_only_certificates_badges_are_listed_separately()
    {
        var owner = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Achieve College " + Guid.NewGuid().ToString("N")[..6]);
        var (userId, username) = await SeedProfileUserAsync();
        var eventId = await CreateAndPublishPastEventAsync(owner, orgId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var ticket1 = await SeedTicketAsync(db, eventId, userId, TicketState.CheckedIn, DateTime.UtcNow.AddDays(-5));
            var ticket2 = await SeedTicketAsync(db, eventId, userId, TicketState.CheckedIn, DateTime.UtcNow.AddDays(-5));
            db.Certificates.AddRange(
                new Certificate { EventId = eventId, TicketId = ticket1, UserId = userId,
                    VerifyCode = "WIN" + Guid.NewGuid().ToString("N")[..6], Kind = CertificateKind.Winner, IsPublic = true },
                new Certificate { EventId = eventId, TicketId = ticket2, UserId = userId,
                    VerifyCode = "PAR" + Guid.NewGuid().ToString("N")[..6], Kind = CertificateKind.Participation, IsPublic = true });
            var badge = new Badge { Name = "Early Adopter", Type = "engagement", Description = "Joined early" };
            db.Badges.Add(badge);
            await db.SaveChangesAsync();
            db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = badge.Id, EarnedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var pub = _factory.CreateClient();
        var profile = await Json(await pub.GetAsync($"/v1/public/users/{username}"));
        Assert.Equal(2, profile.GetProperty("stats").GetProperty("certificates_count").GetInt32());
        Assert.Equal(1, profile.GetProperty("stats").GetProperty("achievements").GetInt32());   // badge excluded from the count

        var achievements = profile.GetProperty("achievements").EnumerateArray().ToList();
        Assert.Equal(2, achievements.Count);   // both the Winner cert AND the badge are listed
        Assert.Contains(achievements, a => a.GetProperty("source").GetString() == "certificate" && a.GetProperty("name").GetString() == "Winner");
        Assert.Contains(achievements, a => a.GetProperty("source").GetString() == "badge" && a.GetProperty("name").GetString() == "Early Adopter");
    }

    // ── Organizations: role/verification/period aggregation (D-206) ─────────

    // Membership has a real DB-enforced unique constraint on (UserId, OrgId) — a person holds exactly
    // one membership row per org, ever (role changes update that row in place). So two distinct users
    // are needed to exercise both label paths, not two rows for one pair.
    [Fact]
    public async Task Org_card_shows_the_claim_role_label_when_verified_and_the_org_role_label_otherwise()
    {
        var owner = await LoginAsync(NextPhone());
        var orgId = _factory.SeedVerifiedOrgForClient(owner, "Org Card College " + Guid.NewGuid().ToString("N")[..6]);
        var (studentId, studentUsername) = await SeedProfileUserAsync();
        var (repId, repUsername) = await SeedProfileUserAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var claim = new MembershipClaim
            {
                UserId = studentId, OrgId = orgId, ClaimedRole = MembershipClaimRole.Student,
                Status = MembershipClaimStatus.Approved,
            };
            db.MembershipClaims.Add(claim);
            await db.SaveChangesAsync();

            db.Memberships.AddRange(
                new Membership { OrgId = orgId, UserId = studentId, Role = OrgRole.Staff, ShowOnProfile = true,
                    IsVerified = true, VerifiedAt = DateTime.UtcNow.AddDays(-30), SourceClaimId = claim.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Membership { OrgId = orgId, UserId = repId, Role = OrgRole.Representative, ShowOnProfile = true,
                    IsVerified = false, CreatedAt = DateTime.UtcNow.AddDays(-10) });
            await db.SaveChangesAsync();
        }

        var pub = _factory.CreateClient();

        var studentProfile = await Json(await pub.GetAsync($"/v1/public/users/{studentUsername}"));
        var studentOrg = Assert.Single(studentProfile.GetProperty("organizations").EnumerateArray());
        Assert.True(studentOrg.GetProperty("is_verified").GetBoolean());
        Assert.Equal(new[] { "Student" },
            studentOrg.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());   // claim label wins

        var repProfile = await Json(await pub.GetAsync($"/v1/public/users/{repUsername}"));
        var repOrg = Assert.Single(repProfile.GetProperty("organizations").EnumerateArray());
        Assert.False(repOrg.GetProperty("is_verified").GetBoolean());
        Assert.Equal(new[] { "Organizer" },
            repOrg.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());   // OrgRole fallback (D-202)
    }
}
