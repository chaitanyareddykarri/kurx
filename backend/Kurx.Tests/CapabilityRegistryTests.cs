using Kurx.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 Capability registry (Event Architecture V3 §11 + §19, Phase 2). Verifies the ~45-capability
/// catalog, the Kind×Capability default matrix, mode-gating (§11.3), the depends_on DAG (§11.4), and the
/// additive per-event materialization + read endpoints. Distinct from trust capabilities and D-116
/// workspace-capabilities, both untouched.</summary>
public class CapabilityRegistryTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public CapabilityRegistryTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }   // startup seeds kinds + capabilities
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Seeds_the_31_event_capabilities_and_the_matrix_idempotently()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await CapabilityRegistrySeeder.SeedAsync(db);
        await CapabilityRegistrySeeder.SeedAsync(db);

        Assert.Equal(31, await db.Capabilities.CountAsync());   // 30 from D-266 M2, +1 entitlements (D-334)
        // 8 before M2; registration/reminders/audit became platform infrastructure and left the catalog.
        // IsUniversal now only marks a DEFAULT — the archetype matrix decides whether it may be enabled.
        Assert.Equal(5, await db.Capabilities.CountAsync(c => c.IsUniversal));
        Assert.Equal(5, await db.Capabilities.Select(c => c.GroupSlug).Distinct().CountAsync());
        // The Kind matrix is retired; the archetype matrix stores every cell, including the negatives.
        Assert.Equal(0, await db.KindCapabilityDefaults.CountAsync());
        Assert.Equal(14 * 31, await db.ArchetypeCapabilityDefaults.CountAsync());

        // The two declared depends_on edges are present in the registry (§11.4).
        // stages/officials/prizes folded into judging configuration in M2; leaderboard keeps the
        // dependency that matters — there is nothing to rank without scoring.
        var leaderboard = await db.Capabilities.SingleAsync(c => c.Slug == "leaderboard");
        Assert.Contains("scoring", leaderboard.DependsOnJson);
        var certs = await db.Capabilities.SingleAsync(c => c.Slug == "certificates");
        Assert.Contains("attendance", certs.DependsOnJson);
    }

    [Fact]
    public async Task Archetype_defaults_match_the_D12_matrix_and_kind_defaults_are_gone()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // D-266 M2 retired the Kind x capability table: keeping it would be a second, stale authority for
        // the same question the archetype matrix now answers.
        Assert.Equal(0, await db.KindCapabilityDefaults.CountAsync());

        async Task<CapabilityRule?> Rule(string archetype, string cap) => await db.ArchetypeCapabilityDefaults
            .Where(d => d.ArchetypeSlug == archetype && d.CapabilitySlug == cap)
            .Select(d => (CapabilityRule?)d.Rule).FirstOrDefaultAsync();

        Assert.Equal(CapabilityRule.Required, await Rule("competitive", "submissions"));
        Assert.Equal(CapabilityRule.Required, await Rule("competitive", "scoring"));
        Assert.Equal(CapabilityRule.Optional, await Rule("competitive", "teams"));
        // Every cell is stored, including the negatives — that is what makes "may never enable" sayable.
        Assert.Equal(CapabilityRule.Unsupported, await Rule("competitive", "tracks"));
        Assert.Equal(CapabilityRule.Unsupported, await Rule("learning", "leaderboard"));
    }

    [Fact]
    public async Task Registry_and_archetype_capability_endpoints()
    {
        var anon = _factory.CreateClient();

        // D-266 M2 reduced the catalog from 57 slugs to D12's 30: the rest were Registration / Ticketing /
        // ...and D-334 added `entitlements` as the 31st, the first addition since that reduction.
        // Invitation / Scheduling / Eligibility / Finance / Infrastructure concerns, each already owned by
        // its own subsystem.
        var registry = await Json(await anon.GetAsync("/v1/capabilities"));
        Assert.Equal(31, registry.GetArrayLength());

        var competitive = await Json(await anon.GetAsync("/v1/archetypes/competitive/capabilities"));
        var byslug = competitive.EnumerateArray().ToDictionary(
            c => c.GetProperty("slug").GetString()!, c => c.GetProperty("state").GetString());
        Assert.Equal("required", byslug["submissions"]);
        Assert.Equal("required", byslug["scoring"]);
        // Tracks has no cell for Competitive, so it is locked — not merely off. That distinction is the
        // reason the archetype matrix replaced the Kind defaults.
        Assert.Equal("locked", byslug["tracks"]);

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/v1/archetypes/nope/capabilities")).StatusCode);
    }

    [Fact]
    public async Task Every_kinds_declared_dependencies_are_resolved()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICapabilityService>();
        var registry = await svc.ListRegistryAsync();
        var deps = registry.ToDictionary(c => c.Slug, c => c.DependsOn);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var archetypes = await db.EventArchetypes.Select(a => a.Slug).ToListAsync();

        // §11.4 invariant, now over archetypes: no ON/REQUIRED capability may have an unmet dependency.
        // "Locked" counts as unmet too — a capability cannot be on while something it needs is forbidden.
        foreach (var archetype in archetypes)
        {
            var states = (await svc.GetForArchetypeAsync(archetype)).ToDictionary(c => c.Slug, c => c.State);
            foreach (var (slug, state) in states)
            {
                if (state.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                    state.Equals("locked", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var dep in deps[slug])
                    Assert.False(
                        states[dep].Equals("off", StringComparison.OrdinalIgnoreCase) ||
                        states[dep].Equals("locked", StringComparison.OrdinalIgnoreCase),
                        $"{archetype}: capability '{slug}' is {state} but its dependency '{dep}' is {states[dep]}");
            }
        }
    }

    [Fact]
    public async Task Mode_gating_locks_the_venue_map_when_online()
    {
        var (client, orgId, cricketTypeId, sportsCatId) = await OwnerWithOrgAsync("9700000901", "Caps Sport Org");

        async Task<string> MapState(string mode, string? url)
        {
            var body = new
            {
                title = $"Cup {mode}", description = "Matches.", categoryId = sportsCatId, typeId = cricketTypeId,
                venueName = "Ground", city = "Chennai",
                startsAt = DateTime.UtcNow.AddDays(15), endsAt = DateTime.UtcNow.AddDays(16),
                eventMode = mode, onlineUrl = url,
            };
            var id = (await Json(await client.CreateEventAsync(orgId, body))).GetProperty("id").GetGuid();
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICapabilityService>();
            return (await svc.GetForEventAsync(id)).Single(c => c.Slug == "maps").State.ToLowerInvariant();
        }

        // seat-map moved to the Ticketing subsystem in M2, so the mode-gating guarantee is asserted on the
        // capability that is still event behaviour: a venue map is meaningless for a purely online event.
        // Offline keeps it available; Online gates it out entirely, which the engine reports as locked.
        Assert.Equal("off", await MapState("Offline", null));
        Assert.Equal("locked", await MapState("Online", "https://live.example.com"));
    }

    [Fact]
    public async Task Create_materializes_capabilities_and_backfill_repairs_them()
    {
        var (client, orgId, hackTypeId, compCatId) = await OwnerWithOrgAsync("9700000902", "Caps Hack Org");
        var body = new
        {
            title = "Caps Hack", description = "Build.", categoryId = compCatId, typeId = hackTypeId,
            venueName = "Lab", city = "Pune",
            startsAt = DateTime.UtcNow.AddDays(15), endsAt = DateTime.UtcNow.AddDays(16),
        };
        var eventId = (await Json(await client.CreateEventAsync(orgId, body))).GetProperty("id").GetGuid();

        // Materialized on create: teams REQUIRED present, donations (OFF) absent.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var rows = await db.EventCapabilities.Where(x => x.EventId == eventId)
                .Select(x => x.CapabilitySlug).ToListAsync();
            // D12 makes submissions/scoring Required for Competitive; teams is only Optional, so it is
            // Off and correctly unmaterialized — a real behaviour change from the hackathon Kind defaults.
            Assert.Contains("submissions", rows);
            Assert.Contains("attendance", rows);
            Assert.DoesNotContain("teams", rows);
            Assert.DoesNotContain("tracks", rows);   // no cell for Competitive => locked

            // Wipe + backfill repairs it (idempotent).
            db.EventCapabilities.RemoveRange(db.EventCapabilities.Where(x => x.EventId == eventId));
            await db.SaveChangesAsync();
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ICapabilityService>();
            Assert.True(await svc.BackfillEventCapabilitiesAsync() >= 1);
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            // D12 §2 makes Teams only Optional for Competitive (the hackathon Kind had it Required), so it
            // is Off and correctly unmaterialized. Submissions is Required, so it is the probe that proves
            // backfill restored the rows rather than that a legacy default survived.
            Assert.True(await db.EventCapabilities.AnyAsync(x => x.EventId == eventId && x.CapabilitySlug == "submissions"));
        }

        // Endpoint returns the effective set (all caps with state).
        var caps = await Json(await client.GetAsync($"/v1/orgs/{orgId}/events/{eventId}/capabilities"));
        var byslug = caps.EnumerateArray().ToDictionary(
            c => c.GetProperty("slug").GetString()!, c => c.GetProperty("state").GetString());
        // D12 §2 makes Teams Optional for Competitive ("off"), not Required as the hackathon Kind did.
        // submissions is the Required probe; tracks is the locked case; teams proves off != locked.
        Assert.Equal("required", byslug["submissions"]);
        Assert.Equal("off", byslug["teams"]);
        Assert.Equal("locked", byslug["tracks"]);
    }

    private async Task<(HttpClient Client, Guid OrgId, Guid TypeId, Guid CatId)> OwnerWithOrgAsync(
        string phone, string orgName, string typeSlug = "hackathon")
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await Json(verify)).GetProperty("access_token").GetString());
        var orgId = _factory.SeedVerifiedOrgForClient(client, orgName);

        // Choose the taxonomy type per test: hackathon (→ hackathon kind) or cricket-tournament (→ tournament).
        var wantSports = orgName.Contains("Sport");
        // Cricket Tournament became an attribute of Sports Tournament in D-266 M3 Step 6; the student
        // branch keeps the audience-prefixed slug because the name recurs across audiences.
        var slug = wantSports ? "student-sports-tournament" : "hackathon";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == slug)
            .Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (client, orgId, t.Id, t.ParentId!.Value);
    }

    // ── Phase-2 review fixes: Kind change, Mode change, Clone, registry validation, untyped ──

    [Fact]
    public async Task Changing_event_kind_recalculates_capabilities_with_no_stale()
    {
        var (client, orgId) = await LoginOrgAsync("9700000911", "Kind Change Org");
        var (hackType, hackCat) = await TaxonAsync("hackathon");
        var id = await CreateEventAsync(client, orgId, hackCat, hackType);

        var before = await EventCapsAsync(id);
        // Teams is Optional for Competitive under D12 §2 and therefore Off; submissions and scoring are the
        // Required pair. Asserting teams here would be asserting the retired Kind defaults.
        Assert.Contains("submissions", before);
        Assert.Contains("scoring", before);

        // Change Kind → Conference. This exercises re-materialization over existing rows (the bug path).
        var (confType, confCat) = await TaxonAsync("conference");
        var patch = await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}",
            new { categoryId = confCat, typeId = confType });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var after = await EventCapsAsync(id);
        Assert.Contains("speakers", after);       // conference REQUIRED
        Assert.Contains("agenda", after);
        Assert.DoesNotContain("teams", after);    // hackathon-only → recalculated away (no stale)
        Assert.DoesNotContain("scoring", after);
        Assert.DoesNotContain("stages", after);
    }

    [Fact]
    public async Task Changing_event_mode_recalculates_mode_gated_capabilities()
    {
        var (client, orgId) = await LoginOrgAsync("9700000912", "Mode Change Org");
        var (cricketType, sportsCat) = await TaxonAsync("student-sports-tournament");
        var id = await CreateEventAsync(client, orgId, sportsCat, cricketType);

        // maps is Optional for Tournament, so it is never materialized in either mode; what changes is its
        // resolved state. Offline leaves it available (off = the organiser simply has not turned it on),
        // Online forbids it outright (locked). Off vs locked is precisely the distinction the Kind engine
        // could not make, and it is what a client needs to know whether to render the toggle at all.
        async Task<string> MapsState()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICapabilityService>();
            return (await svc.GetForEventAsync(id)).Single(c => c.Slug == "maps").State.ToLowerInvariant();
        }

        Assert.Equal("off", await MapsState());

        var patch = await client.PatchAsJsonAsync($"/v1/orgs/{orgId}/events/{id}",
            new { eventMode = "Online", onlineUrl = "https://live.example.com" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        Assert.Equal("locked", await MapsState());
    }

    [Fact]
    public async Task Clone_receives_the_correct_capability_set()
    {
        var (client, orgId) = await LoginOrgAsync("9700000913", "Clone Caps Org");
        var (hackType, hackCat) = await TaxonAsync("hackathon");
        var id = await CreateEventAsync(client, orgId, hackCat, hackType);

        var cloneRes = await client.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{id}/clone", new { title = "Clone" });
        Assert.Equal(HttpStatusCode.OK, cloneRes.StatusCode);
        var cloneId = (await Json(cloneRes)).GetProperty("id").GetGuid();

        var caps = await EventCapsAsync(cloneId);
        // teams is only Optional for Competitive under D12, so it is Off and unmaterialized.
        Assert.Contains("submissions", caps);
        Assert.Contains("scoring", caps);
        // registration/reminders/audit left the engine in M2 — they are platform infrastructure that no
        // event may disable, so asserting them as capabilities would re-create the concept that was removed.
        Assert.Contains("attendance", caps);
        Assert.DoesNotContain("donations", caps);  // OFF for hackathon
    }

    [Fact]
    public async Task Every_kind_default_references_a_real_capability()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var registry = (await db.Capabilities.Select(c => c.Slug).ToListAsync()).ToHashSet();
        var used = await db.KindCapabilityDefaults.Select(d => d.CapabilitySlug).Distinct().ToListAsync();
        var orphans = used.Where(s => !registry.Contains(s)).ToList();
        Assert.True(orphans.Count == 0, $"kind_capability_defaults reference unknown capabilities: {string.Join(", ", orphans)}");
    }

    [Fact]
    public async Task Untyped_event_resolves_no_capabilities_rather_than_falling_back()
    {
        var (client, orgId) = await LoginOrgAsync("9700000914", "Untyped Org");

        // A custom Category not in the seeded taxonomy → no Kind → only the 8 universal capabilities.
        Guid customCat;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var cat = new Kurx.Domain.Entities.EventCategory
            {
                Level = Kurx.Domain.Enums.CategoryLevel.Category,
                Name = "Custom Cat", Slug = $"custom-{Guid.NewGuid():N}",
            };
            db.EventCategories.Add(cat);
            await db.SaveChangesAsync();
            customCat = cat.Id;
        }
        var id = await CreateEventAsync(client, orgId, customCat, typeId: null);

        var caps = await EventCapsAsync(id);
        // Pre-M2 this fell back to the 8 "universal" capabilities. Two approved decisions changed it:
        // "Universal Required" was removed (universals are now only defaults, and defaults may never
        // enable what the matrix does not permit), and registration/reminders/audit left the engine
        // entirely as Layer 2 platform infrastructure. With no type there is no archetype, and the
        // resolver returns Unsupported for everything rather than guessing — so nothing materializes.
        // This is the "never falls back to legacy logic" guarantee, asserted at its sharpest point.
        Assert.Empty(caps);
    }

    private async Task<(HttpClient Client, Guid OrgId)> LoginOrgAsync(string phone, string orgName)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await Json(verify)).GetProperty("access_token").GetString());
        return (client, _factory.SeedVerifiedOrgForClient(client, orgName));
    }

    private async Task<(Guid TypeId, Guid CatId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug)
            .Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.Id, t.ParentId!.Value);
    }

    private async Task<Guid> CreateEventAsync(HttpClient client, Guid orgId, Guid catId, Guid? typeId)
    {
        var body = new
        {
            title = "Caps Event", description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C",
            startsAt = DateTime.UtcNow.AddDays(15), endsAt = DateTime.UtcNow.AddDays(16),
        };
        var res = await client.CreateEventAsync(orgId, body);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<List<string>> EventCapsAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.EventCapabilities.Where(x => x.EventId == eventId)
            .Select(x => x.CapabilitySlug).ToListAsync();
    }
}
