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

/// <summary>
/// D-299 — the production-readiness audit fixes.
///
/// Each test pins a defect the audit found, and each would pass on the broken code only by accident.
/// The two concurrency cases fire real simultaneous requests rather than asserting a lock exists.
/// </summary>
public class AuditFixTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public AuditFixTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            _reset = true;
        }
    }

    // ── H-1: the group-number race ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Concurrent_group_registrations_both_succeed_with_distinct_numbers()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9330000001", "Group Race Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, registrationMode: "Group",
            groupMin: 1, groupMax: 3, quantity: 100);
        var (a, _) = await LoginAsync("9330000002");
        var (b, _) = await LoginAsync("9330000003");

        // `groups (EventId, GroupNumber)` is UNIQUE and the number was `COUNT(*) + 1`. Two buyers in the
        // same window computed the same number and the loser's INSERT threw an unhandled unique
        // violation — a 500 on a purchase.
        var results = await Task.WhenAll(
            a.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 1 }),
            b.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 1 }));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var numbers = await db.Groups.AsNoTracking()
            .Where(g => g.EventId == eventId).Select(g => g.GroupNumber).ToListAsync();
        Assert.Equal(2, numbers.Count);
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public async Task Group_numbers_do_not_go_backwards_after_a_group_is_deleted()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9330000010", "Group Max Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId, registrationMode: "Group",
            groupMin: 1, groupMax: 3, quantity: 100);
        var (a, _) = await LoginAsync("9330000011");
        var (b, _) = await LoginAsync("9330000012");

        Assert.Equal(HttpStatusCode.OK,
            (await a.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 1 })).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            // Remove group #1 directly. COUNT-based numbering would now reissue 1 and collide with a
            // number the unique index may still hold in history; MAX-based numbering moves on to 2.
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var first = await db.Groups.FirstAsync(g => g.EventId == eventId);
            first.GroupNumber = 7;   // simulate a gap without breaking the order's FK graph
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK,
            (await b.PostAsJsonAsync($"/v1/events/{eventId}/orders", new { ticketTypeId = ttId, groupSize = 1 })).StatusCode);

        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<KurxDbContext>();
        var numbers = await db2.Groups.AsNoTracking()
            .Where(g => g.EventId == eventId).Select(g => g.GroupNumber).OrderBy(n => n).ToListAsync();
        Assert.Equal([7, 8], numbers);
    }

    // ── C-1: participant chat side effects are transactional ───────────────────────────────────

    [Fact]
    public async Task Accepting_a_working_role_stages_the_chat_join_on_the_same_transaction()
    {
        var (eventId, participantId, userId) = await SeedInvitedParticipantAsync("9330000020", "Staff Outbox Org");

        using var scope = _factory.Services.CreateScope();
        var participants = scope.ServiceProvider.GetRequiredService<IParticipantService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.True((await participants.RespondAsync(userId, participantId, accept: true)).Ok);

        // The point of the fix: the chat consequence is an outbox row written in the SAME unit of work,
        // not an inline call that can throw after the state has already committed.
        // Filtered on IdempotencyKey, a text column: PayloadJson is `jsonb`, and EF renders `.Contains`
        // on it as LIKE, which Postgres has no jsonb operator for.
        //
        // `chat_join`, not `chat_join_host` — D-300 made participants Members, so this is the same type a
        // ticket purchase enqueues.
        var staged = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.IdempotencyKey!.StartsWith($"chat_join:{eventId}:{userId}:"))
            .ToListAsync();
        Assert.Single(staged);
    }

    [Fact]
    public async Task Removing_a_participant_stages_the_chat_revocation_rather_than_calling_inline()
    {
        var (eventId, participantId, userId) = await SeedInvitedParticipantAsync("9330000030", "Staff Revoke Org");

        using var scope = _factory.Services.CreateScope();
        var participants = scope.ServiceProvider.GetRequiredService<IParticipantService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ownerId = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.CreatedBy).FirstAsync();

        Assert.True((await participants.RespondAsync(userId, participantId, accept: true)).Ok);
        Assert.True((await participants.RemoveAsync(ownerId, eventId, participantId)).Ok);

        // The defect: revocation committed, then the chat call ran inline through the Redis-backed
        // broadcaster. A transient there left a removed organiser holding chat Host — silently, and with
        // nothing to retry it. The row below is what makes the revocation durable.
        var staged = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.IdempotencyKey!.StartsWith($"chat_staff_removed:{eventId}:{userId}:"))
            .ToListAsync();
        Assert.Single(staged);

        var state = await db.EventParticipants.AsNoTracking()
            .Where(p => p.Id == participantId).Select(p => p.State).FirstAsync();
        Assert.Equal(ParticipantState.Removed, state);
    }

    [Fact]
    public async Task The_outbox_dispatcher_understands_both_new_participant_types()
    {
        var (eventId, participantId, userId) = await SeedInvitedParticipantAsync("9330000040", "Staff Dispatch Org");

        using var scope = _factory.Services.CreateScope();
        var participants = scope.ServiceProvider.GetRequiredService<IParticipantService>();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.True((await participants.RespondAsync(userId, participantId, accept: true)).Ok);
        await scope.ServiceProvider.GetRequiredService<Kurx.Infrastructure.Jobs.OutboxDispatchJob>().RunAsync(default);

        // A type with no handler throws OutboxPermanentFailureException by design, so an unhandled type
        // would leave the row failed rather than delivered — which is what this asserts against.
        var room = await db.ChatRooms.AsNoTracking().FirstAsync(r => r.EventId == eventId);
        var member = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == room.Id && m.UserId == userId);
        Assert.NotNull(member);
        // D-300 — Member. Before it, accepting an Operations role made this person a chat Host.
        Assert.Equal(ChatMemberRole.Member, member!.Role);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid ParticipantId, Guid UserId)> SeedInvitedParticipantAsync(
        string phone, string orgName)
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync(phone, orgName);
        var (eventId, _) = await PublishFreeEventAsync(owner, orgId);
        var (_, userId) = await LoginAsync(Bump(phone));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var role = await db.ParticipantRoles.AsNoTracking()
            .FirstAsync(r => r.OrgId == null && r.Class == ParticipantClass.Operations);
        var p = new EventParticipant
        {
            EventId = eventId, RoleSlug = role.Slug,
            SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
            State = ParticipantState.Invited,
        };
        db.EventParticipants.Add(p);
        await db.SaveChangesAsync();
        return (eventId, p.Id, userId);
    }

    private static string Bump(string phone) => phone[..^1] + (char)(phone[^1] + 1);

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<(HttpClient Client, Guid OrgId, Guid UserId)> LoginOrgAsync(string phone, string name)
    {
        var (client, userId) = await LoginAsync(phone);
        var orgId = _factory.SeedVerifiedOrgForClient(client, name + " " + Guid.NewGuid().ToString("N")[..6]);
        return (client, orgId, userId);
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId,
        int quantity = 100, string registrationMode = "Individual", int? groupMin = null, int? groupMax = null)
    {
        var (catId, typeId) = await TaxonAsync();
        // POST /v1/events, not /v1/orgs/{id}/events — a USER owns an event (D-267/D-271); the org is only
        // the one it represents. The org-scoped route was removed.
        var created = await owner.CreateEventAsync(orgId, new
        {
            title = "Audit " + Guid.NewGuid().ToString("N")[..6], description = "d",
            categoryId = catId, typeId, venueName = "V", city = "C",
            startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var eventId = (await Json(created)).GetProperty("id").GetGuid();

        var tt = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Free", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode,
            groupMin, groupMax, quantity,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30),
            perUserLimit = 50, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, tt.StatusCode);
        var ttId = (await Json(tt)).GetProperty("id").GetGuid();

        _factory.SeedApprovedEventAuthorization(eventId);
        // Asserted like every other step above, and worth the two lines: this was the ONE unchecked call in
        // the helper, so a publish that failed returned an unpublished event and the *next* request — the
        // order POST each caller makes — answered 404. Both group-number tests then failed on a status
        // comparison that had nothing to do with the race they exist to pin, which is exactly how a
        // transient setup failure gets read as a concurrency defect. The body is in the message because
        // "publish failed" without the reason costs another full run to learn.
        var published = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        Assert.True(published.IsSuccessStatusCode,
            $"setup: publish returned {(int)published.StatusCode}: {await published.Content.ReadAsStringAsync()}");
        return (eventId, ttId);
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // Pick the TYPE first, deterministically, and only ever a PUBLIC one.
        //
        // This used to be an unordered `FirstAsync` for the category and then any child type, which made
        // the chosen node depend on physical row order — so the same code passed or failed depending on
        // what else had written to the database in that run. When it landed on a Private-product type
        // (a wedding, a private ceremony) publish answered 400 `registration_policy_not_allowed_for_type`:
        // `PolicyResolver.AllowedFor` returns `[InviteOnly]` for a Private product, and a new event
        // defaults to `RegistrationPolicy.Open`. None of these tests are about private-event policy —
        // they are about group numbering and the participant outbox — so the taxonomy they stand on has
        // to be boring and fixed rather than whatever the planner happened to return first.
        var type = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Type && c.ParentId != null
                && c.ProductClass == EventProduct.Public)
            .OrderBy(c => c.Slug).ThenBy(c => c.Id)
            .FirstOrDefaultAsync();
        if (type is not null) return (type.ParentId!.Value, type.Id);

        // No public type seeded: fall back to a stable category so the failure that follows is the
        // test's own assertion rather than an ordering artefact.
        var cat = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Category)
            .OrderBy(c => c.Slug).ThenBy(c => c.Id)
            .FirstAsync();
        return (cat.Id, cat.Id);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
}
