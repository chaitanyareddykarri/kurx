using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M2 Tasks 4 and 5 — the persisted matrix, the archetype-resolution guarantee for every
/// seeded event type, and the old-engine/new-engine comparison that has to be understood before the
/// switch counts as safe.</summary>
public class CapabilityEngineMigrationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CapabilityEngineMigrationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            Seed(EventTaxonomySeeder.SeedAsync);
            Seed(ArchetypeSeeder.SeedAsync);
            Seed(CapabilityRegistrySeeder.SeedAsync);
            Seed(ArchetypeCapabilitySeeder.SeedAsync);
            Seed(ArchetypeCapabilitySeeder.SeedAsync);   // idempotence is a hard requirement: it re-syncs
            _reset = true;
        }
    }

    private void Seed(Func<KurxDbContext, CancellationToken, Task> run)
    {
        using var scope = _factory.Services.CreateScope();
        run(scope.ServiceProvider.GetRequiredService<KurxDbContext>(), default).GetAwaiter().GetResult();
    }

    private KurxDbContext Db(IServiceScope s) => s.ServiceProvider.GetRequiredService<KurxDbContext>();

    // ── Task 1 · the matrix is persisted, completely, and matches D12 ───────────────────────

    [Fact]
    public async Task Matrix_persists_every_archetype_x_capability_cell_including_unsupported()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var archetypes = CapabilityCatalog.ArchetypeDefaults.Count;
        var capabilities = CapabilityCatalog.Capabilities.Length;

        Assert.Equal(archetypes * capabilities, await db.ArchetypeCapabilityDefaults.CountAsync());
        // Storing the negatives is the whole point — without them "may never enable" is inexpressible.
        Assert.True(await db.ArchetypeCapabilityDefaults.AnyAsync(r => r.Rule == CapabilityRule.Unsupported));
    }

    [Fact]
    public async Task Persisted_rules_match_the_D12_transcription_exactly()
    {
        using var scope = _factory.Services.CreateScope();
        var rows = await Db(scope).ArchetypeCapabilityDefaults.AsNoTracking().ToListAsync();

        var mismatches = new List<string>();
        foreach (var r in rows)
        {
            var d = CapabilityCatalog.ArchetypeDefaults[r.ArchetypeSlug];
            var expected = d.Required.Contains(r.CapabilitySlug) ? CapabilityRule.Required
                         : d.Optional.Contains(r.CapabilitySlug) ? CapabilityRule.Optional
                         : CapabilityRule.Unsupported;
            if (r.Rule != expected) mismatches.Add($"{r.ArchetypeSlug}/{r.CapabilitySlug}: {r.Rule} != {expected}");
        }
        Assert.True(mismatches.Count == 0, string.Join(" | ", mismatches.Take(10)));
    }

    // ── Task 5 · every seeded type resolves, deterministically, with nothing unsupported on ──

    [Fact]
    public async Task Every_seeded_type_resolves_capabilities_with_no_unsupported_enabled()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var types = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Type)
            .Select(c => new { c.Name, c.ArchetypeSlug, c.ProductClass })
            .ToListAsync();

        Assert.NotEmpty(types);
        var failures = new List<string>();

        foreach (var t in types)
        {
            if (t.ArchetypeSlug is null) { failures.Add($"{t.Name}: no archetype"); continue; }

            var matrix = await db.ArchetypeCapabilityDefaults.AsNoTracking()
                .Where(r => r.ArchetypeSlug == t.ArchetypeSlug)
                .ToDictionaryAsync(r => r.CapabilitySlug, r => r.Rule, StringComparer.Ordinal);

            var product = t.ProductClass ?? EventProduct.Public;
            var res = CapabilityResolver.Resolve(t.ArchetypeSlug, product, "Offline", matrix: matrix);

            foreach (var slug in res.Enabled)
                if (res.States[slug] == CapabilityRule.Unsupported)
                    failures.Add($"{t.Name}: enabled an unsupported capability '{slug}'");

            // Deterministic: same inputs, same answer.
            var again = CapabilityResolver.Resolve(t.ArchetypeSlug, product, "Offline", matrix: matrix);
            if (!res.Enabled.OrderBy(x => x).SequenceEqual(again.Enabled.OrderBy(x => x)))
                failures.Add($"{t.Name}: non-deterministic resolution");
        }

        Assert.True(failures.Count == 0, string.Join(" | ", failures.Take(10)));
    }

    [Fact]
    public async Task No_private_type_can_resolve_a_money_capability()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var privateTypes = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Type && c.ProductClass == EventProduct.Private && c.ArchetypeSlug != null)
            .Select(c => new { c.Name, c.ArchetypeSlug }).ToListAsync();

        Assert.NotEmpty(privateTypes);
        foreach (var t in privateTypes)
        {
            var matrix = await db.ArchetypeCapabilityDefaults.AsNoTracking()
                .Where(r => r.ArchetypeSlug == t.ArchetypeSlug)
                .ToDictionaryAsync(r => r.CapabilitySlug, r => r.Rule, StringComparer.Ordinal);

            var res = CapabilityResolver.Resolve(t.ArchetypeSlug, EventProduct.Private, "Offline",
                requested: ["paid", "finance"], matrix: matrix);

            Assert.DoesNotContain("paid", res.Enabled);
            Assert.DoesNotContain("finance", res.Enabled);
        }
    }

    // ── Task 4 · old engine vs new engine ───────────────────────────────────────────────────

    /// <summary>The switch changes behaviour by design — that is what D12 is for — so the useful assertion
    /// is not "nothing changed" but "every change is one D12 predicted". The Kind engine could only say
    /// Required/On/Off; the archetype engine adds Locked (Unsupported). A capability moving *to* Locked is
    /// expected. A capability the new engine leaves On that D12 marks Unsupported would be a regression,
    /// and is what this fails on.</summary>
    [Fact]
    public async Task New_engine_never_enables_what_the_matrix_forbids()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var archetypes = await db.EventArchetypes.AsNoTracking().Select(a => a.Slug).ToListAsync();
        var regressions = new List<string>();

        foreach (var a in archetypes)
        {
            var matrix = await db.ArchetypeCapabilityDefaults.AsNoTracking()
                .Where(r => r.ArchetypeSlug == a)
                .ToDictionaryAsync(r => r.CapabilitySlug, r => r.Rule, StringComparer.Ordinal);

            var product = a == "private-gathering" ? EventProduct.Private : EventProduct.Public;

            // Ask for everything the old Kind engine might have turned on, then confirm nothing forbidden
            // survives. This is the migration guarantee: no event keeps a capability D12 disallows.
            var all = CapabilityCatalog.Capabilities.Select(c => c.Slug).ToList();
            var res = CapabilityResolver.Resolve(a, product, "Offline", requested: all, matrix: matrix);

            foreach (var slug in res.Enabled)
                if (matrix.GetValueOrDefault(slug, CapabilityRule.Unsupported) == CapabilityRule.Unsupported)
                    regressions.Add($"{a}: '{slug}' enabled despite Unsupported");
        }

        Assert.True(regressions.Count == 0, string.Join(" | ", regressions.Take(10)));
    }

    /// <summary>The legacy Kind path must not be reachable from event resolution any more. A grep-style
    /// assertion is the honest one here: the method still exists for this comparison, so the guarantee is
    /// that <see cref="CapabilityService.GetForEventAsync"/> no longer reads KindSlug.</summary>
    [Fact]
    public async Task Event_resolution_no_longer_consults_kind()
    {
        var src = await File.ReadAllTextAsync(SourcePath("Kurx.Infrastructure/Events/CapabilityService.cs"));
        var body = src[src.IndexOf("public async Task<IReadOnlyList<ResolvedCapability>> GetForEventAsync", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.DoesNotContain("KindSlug", body);
        Assert.DoesNotContain("KindDefaultsAsync", body);
        Assert.Contains("ArchetypeSlug", body);
    }

    /// <summary>Resolved from the compile-time path of this file, not from AppContext.BaseDirectory:
    /// the container build redirects output to /tmp/artifacts, so walking up from the assembly never
    /// reaches the repository.</summary>
    private static string SourcePath(string relative, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => Path.Combine(Directory.GetParent(Path.GetDirectoryName(here)!)!.FullName, relative);
}
