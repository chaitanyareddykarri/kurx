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

/// <summary>V3 §7.5 (delegated / SeatBlock) + §7.6 (walk-in), Phase 13. Additive over the approved money path — every
/// registration mints through the existing Order→Ticket projection. Walk-in draws the WalkIn segment pool (staff-only,
/// idempotent). SeatBlock reserves N unassigned admissions; the delegate console binds people, governed by deadline +
/// reassign limit + audit. Payment is data/authz only (FREE|DEFERRED); no live collection.</summary>
public class DelegatedRegistrationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public DelegatedRegistrationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private IWalkInService WalkIns(IServiceScope s) => s.ServiceProvider.GetRequiredService<IWalkInService>();
    private ISeatBlockService Blocks(IServiceScope s) => s.ServiceProvider.GetRequiredService<ISeatBlockService>();

    // ── Walk-in (§7.6) ────────────────────────────────────────────────────────

    [Fact]
    public async Task Walk_in_requires_staff_and_a_walkin_pool_then_mints_the_chain()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700013001", "WalkIn Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, strangerId) = await LoginAsync("9700013002");
        var (_, staffId) = await LoginAsync("9700013003");
        await SeedStaffAsync(eventId, staffId);

        using var scope = _factory.Services.CreateScope();
        var svc = WalkIns(scope);
        var input = new WalkInInput(ttId, "Rahul", "9800013001", null, Free: true, IdempotencyKey: null);
        Assert.Equal("forbidden", (await svc.CreateAsync(strangerId, eventId, false, input)).Error);
        Assert.Equal("no_walkin_pool", (await svc.CreateAsync(staffId, eventId, false, input)).Error);   // no WalkIn pool yet

        await SeedWalkInPoolAsync(eventId, ttId, total: 5);
        var r = await svc.CreateAsync(staffId, eventId, false, input);
        Assert.True(r.Ok);
        Assert.NotEqual(Guid.Empty, r.Value!.AdmissionId);
        Assert.NotNull(r.Value!.CredentialId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var adm = await db.Admissions.AsNoTracking().FirstAsync(a => a.Id == r.Value!.AdmissionId);
        var pool = await db.InventoryPools.AsNoTracking().FirstAsync(p => p.TicketTypeId == ttId && p.Segment == InventorySegment.WalkIn);
        Assert.Equal(pool.Id, adm.PoolId);          // admission references the WalkIn pool it consumed
        Assert.Equal(1, pool.Consumed);             // §17.1 reconciliation stays exact
    }

    [Fact]
    public async Task Walk_in_is_idempotent_on_replay()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700013010", "Idem Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, staffId) = await LoginAsync("9700013011");
        await SeedStaffAsync(eventId, staffId);
        await SeedWalkInPoolAsync(eventId, ttId, total: 5);

        using var scope = _factory.Services.CreateScope();
        var svc = WalkIns(scope);
        var input = new WalkInInput(ttId, "Priya", "9800013010", null, Free: true, IdempotencyKey: "gate-desk-1-0001");
        var first = await svc.CreateAsync(staffId, eventId, false, input);
        var replay = await svc.CreateAsync(staffId, eventId, false, input);   // queued walk-in replayed on reconnect
        Assert.True(first.Ok && replay.Ok);
        Assert.Equal(first.Value!.OrderId, replay.Value!.OrderId);            // same order, not a duplicate

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.Registrations.CountAsync(r => r.EventId == eventId));
        Assert.Equal(1, (await db.InventoryPools.AsNoTracking().FirstAsync(p => p.TicketTypeId == ttId && p.Segment == InventorySegment.WalkIn)).Consumed);
    }

    // ── SeatBlock / delegated (§7.5) ──────────────────────────────────────────

    [Fact]
    public async Task Seat_block_mints_unassigned_admissions()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700013020", "Block Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, delegateId) = await LoginAsync("9700013021");
        var (_, strangerId) = await LoginAsync("9700013022");
        var unitId = await OrgUnitAsync(eventId);

        using var scope = _factory.Services.CreateScope();
        var svc = Blocks(scope);
        Assert.Equal("forbidden", (await svc.CreateAsync(strangerId, eventId, false, Block(ttId, unitId, delegateId, 3))).Error);
        var created = await svc.CreateAsync(ownerId, eventId, false, Block(ttId, unitId, delegateId, 3));
        Assert.True(created.Ok);
        Assert.Equal(3, created.Value!.Quantity);

        var seats = (await svc.ListSeatsAsync(ownerId, created.Value!.Id, false)).Value!;
        Assert.Equal(3, seats.Count);
        Assert.All(seats, s => Assert.Null(s.PersonId));   // all UNASSIGNED
        var status = (await svc.StatusAsync(ownerId, created.Value!.Id, false)).Value!;
        Assert.Equal(3, status.Unassigned);
        Assert.Equal(0, status.Assigned);
    }

    [Fact]
    public async Task Delegate_assigns_and_reassignment_is_governed()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700013030", "Assign Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, delegateId) = await LoginAsync("9700013031");
        var (_, p1) = await LoginAsync("9700013032");
        var (_, p2) = await LoginAsync("9700013033");
        var (_, p3) = await LoginAsync("9700013034");
        var (_, strangerId) = await LoginAsync("9700013035");
        var unitId = await OrgUnitAsync(eventId);

        using var scope = _factory.Services.CreateScope();
        var svc = Blocks(scope);
        var block = (await svc.CreateAsync(ownerId, eventId, false, Block(ttId, unitId, delegateId, 2, reassignLimit: 1))).Value!;
        var seat = (await svc.ListSeatsAsync(delegateId, block.Id, false)).Value![0];

        Assert.Equal("forbidden", (await svc.AssignAsync(strangerId, seat.Id, false, new SeatAssignInput(p1, null))).Error);   // authz
        var assigned = await svc.AssignAsync(delegateId, seat.Id, false, new SeatAssignInput(p1, null));   // first assign (not a reassign)
        Assert.True(assigned.Ok);
        Assert.Equal(p1, assigned.Value!.PersonId);
        Assert.Equal(0, assigned.Value!.ReassignCount);

        var reassigned = await svc.AssignAsync(delegateId, seat.Id, false, new SeatAssignInput(p2, null));   // reassign #1 (within limit 1)
        Assert.True(reassigned.Ok);
        Assert.Equal(p2, reassigned.Value!.PersonId);
        Assert.Equal(1, reassigned.Value!.ReassignCount);
        Assert.Equal("reassign_limit_reached", (await svc.AssignAsync(delegateId, seat.Id, false, new SeatAssignInput(p3, null))).Error);   // exceeds limit

        // The bound admission carries the person + a credential (minted by the re-run projection).
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var adm = await db.Admissions.AsNoTracking().FirstAsync(a => a.Id == seat.AdmissionId);
        Assert.Equal(p2, adm.PersonId);
        Assert.NotNull(adm.CredentialId);
        Assert.True(await db.Credentials.AsNoTracking().AnyAsync(c => c.PersonId == p2 && c.EventId == eventId));
    }

    // ── Review-fix regressions (H1 · M1 · M3) ────────────────────────────────

    [Fact]   // H1 — a NONE-identity walk-in replayed CONCURRENTLY must yield exactly one registration + one consume.
    public async Task Walk_in_none_identity_concurrent_replay_creates_exactly_one_registration()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700013040", "H1 Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, staffId) = await LoginAsync("9700013041");
        await SeedStaffAsync(eventId, staffId);
        await SeedWalkInPoolAsync(eventId, ttId, total: 10);

        // NONE identity (no guest phone) + a stable idempotency key, fired concurrently — the offline reconnect race.
        var input = new WalkInInput(ttId, null, null, null, Free: true, IdempotencyKey: "gate-none-0001");
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            using var s = _factory.Services.CreateScope();
            return await WalkIns(s).CreateAsync(staffId, eventId, false, input);
        })));

        Assert.All(results, r => Assert.True(r.Ok, r.Error));
        Assert.Single(results.Select(r => r.Value!.OrderId).Distinct());   // every replay returned the SAME order

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.Registrations.CountAsync(x => x.EventId == eventId));   // exactly one registration
        Assert.Equal(1, (await db.InventoryPools.AsNoTracking().FirstAsync(p => p.TicketTypeId == ttId && p.Segment == InventorySegment.WalkIn)).Consumed);   // consumed once
    }

    [Fact]   // M1 — reassign/unassign must revoke the previous assignee's now-orphaned credential (no orphaned Active).
    public async Task Reassignment_revokes_the_previous_assignees_orphaned_credential()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700013050", "M1 Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, delegateId) = await LoginAsync("9700013051");
        var (_, p1) = await LoginAsync("9700013052");
        var (_, p2) = await LoginAsync("9700013053");
        var unitId = await OrgUnitAsync(eventId);

        using var scope = _factory.Services.CreateScope();
        var svc = Blocks(scope);
        var block = (await svc.CreateAsync(ownerId, eventId, false, Block(ttId, unitId, delegateId, 1, reassignLimit: 5))).Value!;
        var seat = (await svc.ListSeatsAsync(delegateId, block.Id, false)).Value![0];
        Assert.True((await svc.AssignAsync(delegateId, seat.Id, false, new SeatAssignInput(p1, null))).Ok);
        Assert.True((await svc.AssignAsync(delegateId, seat.Id, false, new SeatAssignInput(p2, null))).Ok);   // reassign p1 → p2

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var p1Cred = await db.Credentials.AsNoTracking().FirstAsync(c => c.EventId == eventId && c.PersonId == p1);
        var p2Cred = await db.Credentials.AsNoTracking().FirstAsync(c => c.EventId == eventId && c.PersonId == p2);
        Assert.Equal(CredentialState.Revoked, p1Cred.State);   // orphaned → revoked (history preserved)
        Assert.Equal(CredentialState.Active, p2Cred.State);    // current holder stays active

        Assert.True((await svc.UnassignAsync(delegateId, seat.Id, false)).Ok);
        Assert.Equal(CredentialState.Revoked, (await db.Credentials.AsNoTracking().FirstAsync(c => c.Id == p2Cred.Id)).State);
    }

    [Fact]   // M3 — concurrent reassignments must not race past the reassign limit.
    public async Task Concurrent_reassignment_respects_the_limit()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700013060", "M3 Org");
        var (eventId, ttId) = await PublishFreeEventAsync(owner, orgId);
        var (_, delegateId) = await LoginAsync("9700013061");
        var (_, p1) = await LoginAsync("9700013062");
        var (_, p2) = await LoginAsync("9700013063");
        var (_, p3) = await LoginAsync("9700013064");
        var (_, p4) = await LoginAsync("9700013065");
        var unitId = await OrgUnitAsync(eventId);

        Guid seatId;
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = Blocks(scope);
            var block = (await svc.CreateAsync(ownerId, eventId, false, Block(ttId, unitId, delegateId, 1, reassignLimit: 1))).Value!;
            seatId = (await svc.ListSeatsAsync(delegateId, block.Id, false)).Value![0].Id;
            Assert.True((await svc.AssignAsync(delegateId, seatId, false, new SeatAssignInput(p1, null))).Ok);   // first assign (count 0)
        }

        // Three concurrent reassignments with limit 1 — exactly one may succeed.
        var results = await Task.WhenAll(new[] { p2, p3, p4 }.Select(t => Task.Run(async () =>
        {
            using var s = _factory.Services.CreateScope();
            return await Blocks(s).AssignAsync(delegateId, seatId, false, new SeatAssignInput(t, null));
        })));
        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.Equal(2, results.Count(r => r.Error == "reassign_limit_reached"));

        using var check = _factory.Services.CreateScope();
        Assert.Equal(1, (await check.ServiceProvider.GetRequiredService<KurxDbContext>().SeatBlockSeats.AsNoTracking().FirstAsync(s => s.Id == seatId)).ReassignCount);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static SeatBlockInput Block(Guid ttId, Guid unitId, Guid delegateId, int qty, int reassignLimit = 0)
        => new(ttId, unitId, null, delegateId, "Free", qty, null, reassignLimit);

    private async Task<Guid> OrgUnitAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var unitId = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.OrgUnitId).FirstAsync();
        if (unitId is { } u) return u;
        var orgId = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.RepresentingOrgId).FirstAsync();
        return await db.OrgUnits.AsNoTracking().Where(x => x.OrgId == orgId).Select(x => x.Id).FirstAsync();
    }

    private async Task SeedStaffAsync(Guid eventId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.EventParticipants.Add(new EventParticipant { EventId = eventId, SubjectType = ParticipantSubjectType.Person, SubjectId = userId, RoleSlug = "registration_desk", State = ParticipantState.Active });
        await db.SaveChangesAsync();
    }

    private async Task SeedWalkInPoolAsync(Guid eventId, Guid ticketTypeId, int total)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.InventoryPools.Add(new InventoryPool { EventId = eventId, TicketTypeId = ticketTypeId, Segment = InventorySegment.WalkIn, Total = total });
        await db.SaveChangesAsync();
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishFreeEventAsync(HttpClient owner, Guid orgId)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "GA", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Individual",
            quantity = 100, saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50, isAllAccess = false,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ttId = (await Json(res)).GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Ev " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        return (await Json(res)).GetProperty("id").GetGuid();
    }

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
        return (client, _factory.SeedVerifiedOrgForClient(client, name), userId);
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug).Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.ParentId!.Value, t.Id);
    }
}
