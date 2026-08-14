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

/// <summary>V3 Kind registry (Event Architecture V3 §2, Phase 1). The registry + aliases are seeded at
/// startup; these tests verify the closed 20-Kind catalog, the 145 data-driven aliases, resolution, and
/// the additive KindSlug stamping on create + backfill. The legacy taxonomy is untouched (covered by
/// <see cref="EventTaxonomyTests"/>).</summary>
public class KindRegistryTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public KindRegistryTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset)
            {
                factory.ResetDatabase();   // host startup seeds taxonomy + kinds + aliases
                _reset = true;
            }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Seeds_20_kinds_and_145_aliases_idempotently()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Re-run the seeder twice: it must insert nothing (startup already seeded) and never duplicate.
        await KindRegistrySeeder.SeedAsync(db);
        await KindRegistrySeeder.SeedAsync(db);

        Assert.Equal(20, await db.EventKinds.CountAsync());
        // D-266 M3 Step 6: aliases are derived from seeded type names, and the canonical taxonomy renamed
        // or merged many of them, so the count is no longer the legacy 145. Asserted as "one alias per
        // resolvable type, no duplicates" rather than a frozen number — EventKind is archived (M2) and
        // exists only so historical rows still resolve.
        var aliasCount = await db.KindAliases.CountAsync();
        Assert.InRange(aliasCount, 90, await db.EventCategories.CountAsync(c => c.Level == CategoryLevel.Type));

        // NormalizedAlias is the resolution key — it must be globally unique.
        var norm = await db.KindAliases.Select(a => a.NormalizedAlias).ToListAsync();
        Assert.Equal(norm.Count, norm.Distinct().Count());

        // The 7 V3 groups are all present.
        var groups = await db.EventKinds.Select(k => k.GroupSlug).Distinct().ToListAsync();
        Assert.Equal(7, groups.Count);
    }

    [Fact]
    public async Task Every_alias_resolves_to_a_real_kind_with_expected_mappings()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var kindSlugs = (await db.EventKinds.Select(k => k.Slug).ToListAsync()).ToHashSet();
        var aliases = await db.KindAliases.ToListAsync();

        // No alias may point at a Kind that does not exist (data integrity of the 145→20 map).
        Assert.All(aliases, a => Assert.Contains(a.KindSlug, kindSlugs));

        string KindFor(string typeSlug) =>
            aliases.Single(a => a.NormalizedAlias == typeSlug).KindSlug;

        Assert.Equal("hackathon", KindFor("hackathon"));
        Assert.Equal("hackathon", KindFor("ideathon"));           // V3 §20 canonical example
        Assert.Equal("competition", KindFor("coding-competition"));
        // Cricket Tournament folded into Sports Tournament in Step 6, and the archived KindCatalog maps by
        // legacy NAME, so no alias exists for the canonical slug. That is correct: Kind is retired and only
        // resolves the historical rows that still carry a KindSlug. Asserting a canonical type here would be
        // asserting that a retired subsystem learned a new name.
        Assert.Equal("wedding", KindFor("wedding"));
        Assert.Equal("celebration", KindFor("birthday-party"));
        // Marathon became Running Event in Step 6, so "student-marathon"/"public-marathon" no longer exist
        // — same reason as Cricket above. The surviving cross-audience case still proves the prefix rule:
        // a name under two audiences yields two prefixed aliases mapping to the same Kind.
        var prefixed = aliases.Where(a => a.NormalizedAlias.StartsWith("student-", StringComparison.Ordinal)
                                       || a.NormalizedAlias.StartsWith("public-", StringComparison.Ordinal))
                              .ToList();
        Assert.NotEmpty(prefixed);
        Assert.All(prefixed, a => Assert.Contains(a.KindSlug, kindSlugs));
    }

    [Fact]
    public async Task Kinds_endpoint_returns_the_20_kinds_grouped_with_aliases()
    {
        var anon = _factory.CreateClient();
        var kinds = await Json(await anon.GetAsync("/v1/kinds"));

        Assert.Equal(20, kinds.GetArrayLength());

        var slugs = kinds.EnumerateArray().Select(k => k.GetProperty("slug").GetString()).ToList();
        Assert.Contains("hackathon", slugs);
        Assert.Contains("wedding", slugs);
        Assert.Contains("selection-drive", slugs);

        var hackathon = kinds.EnumerateArray().Single(k => k.GetProperty("slug").GetString() == "hackathon");
        Assert.Equal("competitive", hackathon.GetProperty("group_slug").GetString());
        var hackathonAliases = hackathon.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()).ToList();
        Assert.Contains("Ideathon", hackathonAliases);
    }

    [Fact]
    public async Task Resolve_maps_type_then_falls_back_to_category()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IKindService>();

        var hackathonTypeId = await db.EventCategories.Where(c => c.Slug == "hackathon").Select(c => c.Id).SingleAsync();
        var competitionsCatId = await db.EventCategories.Where(c => c.Slug == "competitions").Select(c => c.Id).SingleAsync();

        Assert.Equal("hackathon", await svc.ResolveKindSlugAsync(hackathonTypeId, competitionsCatId));
        // No type → category fallback map.
        Assert.Equal("competition", await svc.ResolveKindSlugAsync(null, competitionsCatId));
        // Nothing to resolve → null (additive: an unmapped event stays unkinded, never an error).
        Assert.Null(await svc.ResolveKindSlugAsync(null, null));
    }

    [Fact]
    public async Task Create_stamps_kind_and_backfill_repairs_a_null()
    {
        var (client, orgId, hackathonTypeId, competitionsCatId) = await OwnerWithOrgAsync("9700000801", "Kind Org");

        var body = new
        {
            title = "Kurx Hack 2026",
            description = "Build something.",
            categoryId = competitionsCatId,
            typeId = hackathonTypeId,
            venueName = "Campus Hall",
            city = "Hyderabad",
            startsAt = DateTime.UtcNow.AddDays(20),
            endsAt = DateTime.UtcNow.AddDays(20).AddHours(24),
        };
        var res = await client.CreateEventAsync(orgId, body);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var eventId = (await Json(res)).GetProperty("id").GetGuid();

        // Create stamped the analysable Kind from the chosen taxonomy.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var stored = await db.Events.Where(e => e.Id == eventId).Select(e => e.KindSlug).SingleAsync();
            Assert.Equal("hackathon", stored);

            // Simulate a pre-column row, then prove the idempotent backfill repairs it.
            var ev = await db.Events.SingleAsync(e => e.Id == eventId);
            ev.KindSlug = null;
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IKindService>();
            var updated = await svc.BackfillEventKindsAsync();
            Assert.True(updated >= 1);

            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var stored = await db.Events.Where(e => e.Id == eventId).Select(e => e.KindSlug).SingleAsync();
            Assert.Equal("hackathon", stored);
        }
    }

    private async Task<(HttpClient Client, Guid OrgId, Guid HackathonTypeId, Guid CompetitionsCatId)> OwnerWithOrgAsync(
        string phone, string orgName)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code });
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await Json(verify)).GetProperty("access_token").GetString());

        var orgId = _factory.SeedVerifiedOrgForClient(client, orgName);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var hackathonTypeId = await db.EventCategories.Where(c => c.Slug == "hackathon").Select(c => c.Id).SingleAsync();
        var competitionsCatId = await db.EventCategories.Where(c => c.Slug == "competitions").Select(c => c.Id).SingleAsync();
        return (client, orgId, hackathonTypeId, competitionsCatId);
    }
}
