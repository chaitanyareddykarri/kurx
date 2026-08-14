using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Analytics;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §4.1 (Phase 4) — the OrgUnit structural tree. Additive: every org gets a root unit
/// (materialised on first need), an event auto-binds its owning unit, clones inherit it, and the permission
/// chain-walk resolves a descendant unit's effective org role. Existing per-org authz is unchanged.</summary>
public class OrgUnitTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public OrgUnitTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task New_event_auto_binds_the_org_root_unit()
    {
        var (client, orgId) = await LoginOrgAsync("9700001001", "Root Bind Org");
        var eventId = await CreateEventAsync(client, orgId, "Bind One");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var unitId = await db.Events.Where(e => e.Id == eventId).Select(e => e.OrgUnitId).SingleAsync();
        Assert.NotNull(unitId);

        var unit = await db.OrgUnits.SingleAsync(u => u.Id == unitId);
        Assert.Equal(orgId, unit.OrgId);
        Assert.Null(unit.ParentId);                         // the org's root
        Assert.Equal("organization", unit.Kind);
        Assert.Equal(OrgUnitState.Active, unit.State);
        Assert.Equal($"/{unit.Id}/", unit.Path);            // materialised path, "/self/" for a root
    }

    [Fact]
    public async Task Root_unit_is_shared_across_events_and_unique_per_org()
    {
        var (client, orgId) = await LoginOrgAsync("9700001002", "Shared Root Org");
        var e1 = await CreateEventAsync(client, orgId, "Ev A");
        var e2 = await CreateEventAsync(client, orgId, "Ev B");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var u1 = await db.Events.Where(e => e.Id == e1).Select(e => e.OrgUnitId).SingleAsync();
        var u2 = await db.Events.Where(e => e.Id == e2).Select(e => e.OrgUnitId).SingleAsync();
        Assert.Equal(u1, u2);                               // both events point at the one root
        Assert.Equal(1, await db.OrgUnits.CountAsync(u => u.OrgId == orgId && u.ParentId == null));
    }

    [Fact]
    public async Task EnsureRoot_is_idempotent()
    {
        var (_, orgId) = await LoginOrgAsync("9700001003", "Idempotent Org");
        using var scope = _factory.Services.CreateScope();
        var units = scope.ServiceProvider.GetRequiredService<IOrgUnitService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var first = await units.EnsureRootAsync(orgId);
        await db.SaveChangesAsync();
        var second = await units.EnsureRootAsync(orgId);
        await db.SaveChangesAsync();

        Assert.Equal(first, second);
        Assert.Equal(1, await db.OrgUnits.CountAsync(u => u.OrgId == orgId && u.ParentId == null));
    }

    [Fact]
    public async Task Clone_carries_the_org_unit()
    {
        var (client, orgId) = await LoginOrgAsync("9700001004", "Clone Org");
        var eventId = await CreateEventAsync(client, orgId, "Original");

        var cloneId = (await Json(await client.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{eventId}/clone", new { title = "Copied" }))).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var srcUnit = await db.Events.Where(e => e.Id == eventId).Select(e => e.OrgUnitId).SingleAsync();
        var cloneUnit = await db.Events.Where(e => e.Id == cloneId).Select(e => e.OrgUnitId).SingleAsync();
        Assert.NotNull(srcUnit);
        Assert.Equal(srcUnit, cloneUnit);
    }

    [Fact]
    public async Task Permission_walk_resolves_org_role_through_a_descendant_unit()
    {
        var (client, orgId) = await LoginOrgAsync("9700001005", "Walk Org");
        await CreateEventAsync(client, orgId, "Seed");      // materialises the org's root unit

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var units = scope.ServiceProvider.GetRequiredService<IOrgUnitService>();

        var root = await db.OrgUnits.SingleAsync(u => u.OrgId == orgId && u.ParentId == null);
        var ownerId = await db.Memberships.Where(m => m.OrgId == orgId && m.Role == OrgRole.Owner)
            .Select(m => m.UserId).FirstAsync();

        // A department one level below the root. The org-level Owner grant must still resolve here — that is
        // the ancestry walk (V3 §4.1): a descendant unit inherits an ancestor's grant.
        var child = new OrgUnit { OrgId = orgId, ParentId = root.Id, Kind = "department", Name = "CSE" };
        child.Path = $"{root.Path}{child.Id}/";
        db.OrgUnits.Add(child);
        await db.SaveChangesAsync();

        Assert.Equal(OrgRole.Owner, await units.ResolveOrgRoleAsync(ownerId, child.Id));   // inherited down the chain
        Assert.Equal(OrgRole.Owner, await units.ResolveOrgRoleAsync(ownerId, root.Id));
        Assert.Null(await units.ResolveOrgRoleAsync(Guid.NewGuid(), child.Id));            // stranger → no grant
        Assert.Null(await units.ResolveOrgRoleAsync(ownerId, Guid.NewGuid()));             // unknown unit → null
    }

    // ── Phase 17 (V3 §16): dual-tree revenue rollup ─────────────────────────────

    /// <summary>V3 §16: <c>RevenueForOrgAsync(includeDescendantUnits: true)</c> widens the scope to every event
    /// under the OrgUnit subtree via the materialised <see cref="OrgUnit.Path"/>. No product-facing API assigns
    /// an event to a non-root unit today (<c>IOrgUnitService</c> exposes only <c>EnsureRootAsync</c> /
    /// <c>ResolveOrgRoleAsync</c> this phase — every event auto-binds the root), so this seeds the scenario
    /// directly at the DB layer, the same way <see cref="Permission_walk_resolves_org_role_through_a_descendant_unit"/>
    /// seeds a child unit — proving the rollup query itself is correct ahead of that assignment capability existing.</summary>
    [Fact]
    public async Task Org_revenue_rollup_includes_a_descendant_units_event_only_when_asked()
    {
        var (client, orgId) = await LoginOrgAsync("9700001020", "Rollup Org");
        var rootEventId = await CreateEventAsync(client, orgId, "Root Event");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var facts = scope.ServiceProvider.GetRequiredService<IAnalyticsFactSource>();

        var root = await db.OrgUnits.SingleAsync(u => u.OrgId == orgId && u.ParentId == null);
        var child = new OrgUnit { OrgId = orgId, ParentId = root.Id, Kind = "chapter", Name = "Chapter" };
        child.Path = $"{root.Path}{child.Id}/";
        db.OrgUnits.Add(child);
        await db.SaveChangesAsync();

        var childEventId = await CreateEventAsync(client, orgId, "Chapter Event");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE events SET \"OrgUnitId\" = {child.Id} WHERE \"Id\" = {childEventId}");

        await SeedRevenueAsync(db, rootEventId, 10_000);
        await SeedRevenueAsync(db, childEventId, 25_000);

        Assert.Equal(10_000, await facts.RevenueForOrgAsync(orgId, includeDescendantUnits: false, default));
        Assert.Equal(35_000, await facts.RevenueForOrgAsync(orgId, includeDescendantUnits: true, default));
    }

    /// <summary>Minimal Paid order + VAR line for one event — bypasses the full checkout flow, which is out of
    /// scope for a rollup-filter test (the checkout flow itself is covered by <c>EventRegistrationTests</c>).</summary>
    private async Task SeedRevenueAsync(KurxDbContext db, Guid eventId, long amountPaise)
    {
        var tt = new TicketType
        {
            EventId = eventId, Name = "General", PricePaise = amountPaise, Quantity = 100, Sold = 1,
            SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(9),
        };
        db.TicketTypes.Add(tt);
        var order = new Order
        {
            EventId = eventId, TicketTypeId = tt.Id, Status = OrderStatus.Paid, AmountPaise = amountPaise,
            RazorpayOrderId = "rzp_" + Guid.NewGuid().ToString("N")[..8],
            GuestName = "Rollup Buyer", GuestPhone = $"9198{Random.Shared.Next(100000, 999999)}",
        };
        db.Orders.Add(order);
        var item = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = 1, UnitPricePaise = amountPaise };
        db.OrderItems.Add(item);
        db.ValueAllocationRecords.Add(new ValueAllocationRecord
        {
            OrderId = order.Id, OrderItemId = item.Id, EventId = eventId, AllocatedPaise = amountPaise,
        });
        await db.SaveChangesAsync();
    }

    // ── Priority-4 fixes: backfill, concurrency, depth, clone fallback ──

    [Fact]
    public async Task Backfill_gives_each_legacy_org_one_root_and_repoints_events()
    {
        var (client, orgId) = await LoginOrgAsync("9700001012", "Legacy Backfill Org");
        var e1 = await CreateEventAsync(client, orgId, "Legacy A");
        var e2 = await CreateEventAsync(client, orgId, "Legacy B");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Degrade to the pre-Phase-4 state: events own no unit, and the org has no root unit.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET \"OrgUnitId\" = NULL WHERE \"OrgId\" = {orgId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM org_units WHERE \"OrgId\" = {orgId}");
        Assert.Equal(0, await db.OrgUnits.CountAsync(u => u.OrgId == orgId));

        // Run the migration's backfill transformation (non-batched equivalent — identical end state).
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO org_units ("Id", "OrgId", "ParentId", "Kind", "Name", "Path", "State", "CreatedAt")
            SELECT gen_random_uuid(), o."Id", NULL, 'organization', o."Name", '', 'Active', now()
            FROM organizations o
            WHERE NOT EXISTS (SELECT 1 FROM org_units u WHERE u."OrgId" = o."Id" AND u."ParentId" IS NULL)
            """);
        await db.Database.ExecuteSqlRawAsync("""UPDATE org_units SET "Path" = '/' || "Id" || '/' WHERE "ParentId" IS NULL AND "Path" = ''""");
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE events e SET "OrgUnitId" = u."Id" FROM org_units u
            WHERE u."OrgId" = e."OrgId" AND u."ParentId" IS NULL AND e."OrgUnitId" IS NULL
            """);

        var roots = await db.OrgUnits.Where(u => u.OrgId == orgId && u.ParentId == null).ToListAsync();
        Assert.Single(roots);                                       // exactly one root per org
        Assert.Equal($"/{roots[0].Id}/", roots[0].Path);           // materialised path filled in
        Assert.Equal(roots[0].Id, await db.Events.Where(e => e.Id == e1).Select(e => e.OrgUnitId).SingleAsync());
        Assert.Equal(roots[0].Id, await db.Events.Where(e => e.Id == e2).Select(e => e.OrgUnitId).SingleAsync());
    }

    [Fact]
    public async Task Concurrent_EnsureRoot_creates_exactly_one_root()
    {
        var (_, orgId) = await LoginOrgAsync("9700001010", "Race Org");   // no events yet → no root

        var ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();       // its own DbContext + connection
            return await scope.ServiceProvider.GetRequiredService<IOrgUnitService>().EnsureRootAsync(orgId);
        }));

        Assert.Single(ids.Distinct());                              // every caller agrees on one root id
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.OrgUnits.CountAsync(u => u.OrgId == orgId && u.ParentId == null));
    }

    [Fact]
    public async Task Deep_hierarchy_paths_are_consistent_and_walk_resolves_at_depth()
    {
        var (client, orgId) = await LoginOrgAsync("9700001013", "Deep Tree Org");
        await CreateEventAsync(client, orgId, "Seed");              // materialises the root

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var units = scope.ServiceProvider.GetRequiredService<IOrgUnitService>();

        var root = await db.OrgUnits.SingleAsync(u => u.OrgId == orgId && u.ParentId == null);
        var ownerId = await db.Memberships.Where(m => m.OrgId == orgId && m.Role == OrgRole.Owner)
            .Select(m => m.UserId).FirstAsync();

        var child = new OrgUnit { OrgId = orgId, ParentId = root.Id, Kind = "department", Name = "CSE" };
        child.Path = $"{root.Path}{child.Id}/";
        var grandchild = new OrgUnit { OrgId = orgId, ParentId = child.Id, Kind = "program", Name = "AI" };
        grandchild.Path = $"{child.Path}{grandchild.Id}/";
        db.OrgUnits.AddRange(child, grandchild);
        await db.SaveChangesAsync();

        Assert.Equal($"/{root.Id}/", root.Path);                                   // depth 1
        Assert.Equal($"/{root.Id}/{child.Id}/", child.Path);                       // depth 2
        Assert.Equal($"/{root.Id}/{child.Id}/{grandchild.Id}/", grandchild.Path);  // depth 3
        // Ancestors are every id in the path except self — parsed, no recursive query.
        Assert.Equal(new[] { root.Id, child.Id },
            grandchild.Path.Trim('/').Split('/').SkipLast(1).Select(Guid.Parse).ToArray());
        // The org-level Owner grant resolves three levels down (V3 §4.1 ancestry).
        Assert.Equal(OrgRole.Owner, await units.ResolveOrgRoleAsync(ownerId, grandchild.Id));
    }

    [Fact]
    public async Task Clone_falls_back_to_root_when_source_has_no_unit()
    {
        var (client, orgId) = await LoginOrgAsync("9700001011", "Clone Fallback Org");
        var eventId = await CreateEventAsync(client, orgId, "Legacy");

        // Simulate a pre-backfill source event: clear its owning unit (the root itself is left in place).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET \"OrgUnitId\" = NULL WHERE \"Id\" = {eventId}");
        }

        var cloneId = (await Json(await client.PostAsJsonAsync(
            $"/v1/orgs/{orgId}/events/{eventId}/clone", new { title = "Copied" }))).GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cloneUnit = await db.Events.Where(e => e.Id == cloneId).Select(e => e.OrgUnitId).SingleAsync();
            var root = await db.OrgUnits.Where(u => u.OrgId == orgId && u.ParentId == null).Select(u => u.Id).SingleAsync();
            Assert.NotNull(cloneUnit);                              // fallback fired despite the null source
            Assert.Equal(root, cloneUnit);
        }
    }

    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId, string title)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await client.CreateEventAsync(orgId, new
        {
            title = title + " " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Client, Guid OrgId)> LoginOrgAsync(string phone, string name)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await Json(verify)).GetProperty("access_token").GetString());
        return (client, _factory.SeedVerifiedOrgForClient(client, name));
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug)
            .Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.ParentId!.Value, t.Id);
    }
}
