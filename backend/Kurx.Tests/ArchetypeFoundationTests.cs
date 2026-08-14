using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M1 (Foundation). The exit criterion for this milestone is "every event resolves an
/// archetype", so the load-bearing test here is <see cref="Every_seeded_type_resolves_exactly_one_archetype"/>
/// — it fails the moment someone adds a type to the taxonomy without deciding how it behaves, which is
/// exactly the drift the archetype layer exists to prevent.</summary>
public class ArchetypeFoundationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ArchetypeFoundationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            // Taxonomy first (the archetype backfill reads Type rows), then the archetype seeder twice:
            // it must run on already-populated databases, so idempotence is a hard requirement, not a nicety.
            Seed(db => EventTaxonomySeeder.SeedAsync(db));
            Seed(db => ArchetypeSeeder.SeedAsync(db));
            Seed(db => ArchetypeSeeder.SeedAsync(db));
            _reset = true;
        }
    }

    private void Seed(Func<KurxDbContext, Task> run)
    {
        using var scope = _factory.Services.CreateScope();
        run(scope.ServiceProvider.GetRequiredService<KurxDbContext>()).GetAwaiter().GetResult();
    }

    private KurxDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<KurxDbContext>();

    /// <summary>D-326 — the classification has to reach the client, and nothing checked that it did.
    ///
    /// <para>Every other assertion in this file reads <c>ProductClass</c> off the entity, so all of them
    /// passed while <c>/v1/categories</c> emitted no <c>product_class</c> at all: <c>CategoryResponse</c>
    /// and its <c>ToJson</c> mapper simply had no slot for it. The visible result was a Create-Event wizard
    /// whose Private branch offered zero categories, because the client filter compares a field the API
    /// never sent — and whose Public branch offered all 19 private types for the same reason.</para>
    ///
    /// <para>Asserted over HTTP, not over the service, because the whole defect lived in the last mapper
    /// before serialization. It also pins the CAPITALISATION: both clients compare against
    /// <c>"Private"</c> exactly, and <c>Level</c> is deliberately lower-cased one line away in the same
    /// mapper, so "lower-case it too for consistency" is a live and silent way to reintroduce this.</para></summary>
    [Fact]
    public async Task Categories_endpoint_emits_the_product_class_clients_filter_on()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/v1/categories?level=Type");
        res.EnsureSuccessStatusCode();
        var types = await res.Content.ReadFromJsonAsync<JsonElement>();

        // Keyed on SLUG, not name: a type under two audiences keeps a node per audience with an
        // audience-prefixed slug (student-workshop / public-workshop), so seven names legitimately
        // occur twice and only the slug is unique. Both nodes of such a pair carry the same product.
        var bySlug = types.EnumerateArray().ToDictionary(
            t => t.GetProperty("slug").GetString()!,
            t => t.TryGetProperty("product_class", out var p) ? p.GetString() : null);

        Assert.Equal("Private", bySlug["wedding"]);
        Assert.Equal("Private", bySlug["graduation-party"]);   // D-322
        Assert.Equal("Public", bySlug["hackathon"]);

        // No Type may reach a client unclassified: the client treats a missing value as Public, so an
        // absent field silently offers private types on the public path.
        var unclassified = bySlug.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList();
        Assert.True(unclassified.Count == 0,
            "Types served without product_class: " + string.Join(", ", unclassified));

        // The exact predicate `typesFor`/`allowsProduct` run, asserted on the real payload.
        Assert.Equal(19, bySlug.Count(kv => kv.Value == "Private"));
        Assert.Equal(92, bySlug.Count(kv => kv.Value != "Private"));
    }

    [Fact]
    public async Task Seeds_fourteen_archetypes_and_rerun_adds_none()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        Assert.Equal(14, await db.EventArchetypes.CountAsync());
        Assert.Equal(1, await db.EventArchetypes.CountAsync(a => a.Product == EventProduct.Private));

        var slugs = await db.EventArchetypes.Select(a => a.Slug).ToListAsync();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    /// <summary>The M1 exit criterion. Asserted over the rows actually in the database rather than over the
    /// seeder's own dictionary, so it still catches a type added by an admin through D-188's console.</summary>
    [Fact]
    public async Task Every_seeded_type_resolves_exactly_one_archetype()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var unmapped = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.ArchetypeSlug == null)
            .Select(c => c.Name)
            .ToListAsync();

        Assert.True(unmapped.Count == 0, "types with no archetype: " + string.Join(", ", unmapped));

        // Every slug written must be a real archetype — a typo in the map would otherwise sit undetected
        // until capability resolution silently returned nothing in M2.
        var known = (await db.EventArchetypes.Select(a => a.Slug).ToListAsync()).ToHashSet();
        var used = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.ArchetypeSlug != null)
            .Select(c => c.ArchetypeSlug!).Distinct().ToListAsync();

        Assert.All(used, s => Assert.Contains(s, known));
    }

    [Fact]
    public async Task Private_types_are_classified_private_and_public_types_public()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        async Task<EventProduct?> ProductOf(string typeName) =>
            (await db.EventCategories.FirstAsync(c => c.Level == CategoryLevel.Type && c.Name == typeName)).ProductClass;

        Assert.Equal(EventProduct.Private, await ProductOf("Wedding"));
        Assert.Equal(EventProduct.Private, await ProductOf("Birthday Party"));
        Assert.Equal(EventProduct.Public, await ProductOf("Hackathon"));
        Assert.Equal(EventProduct.Public, await ProductOf("Blood Drive"));

        // D-266 forbids an archetype that spans both products — that would reintroduce the "Both"
        // classification the taxonomy audit removed. Assert it structurally.
        var spanning = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.ArchetypeSlug != null)
            .GroupBy(c => c.ArchetypeSlug!)
            .Where(g => g.Select(c => c.ProductClass).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        Assert.True(spanning.Count == 0, "archetypes spanning both products: " + string.Join(", ", spanning));
    }

    /// <summary>D-322. The test above pins four types by name, which is why it passed for as long as
    /// "Graduation Party" sat in Private Events ▸ Family Celebrations classified Public — the one row in
    /// the private branch that would have sent a family occasion through the D-307 verification gate and
    /// demanded an organization to represent (ceremonial carries RequiresRepresentation).
    ///
    /// <para>The navigation branch and the product class are two independent recordings of the same
    /// decision — the taxonomy tree says where an organiser finds a type, the archetype says how it
    /// behaves — so they must agree in both directions. Asserted structurally over the whole tree rather
    /// than as a fifth named case, because the failure mode is a type nobody thought to name.</para></summary>
    [Fact]
    public async Task Private_branch_and_private_product_agree_in_both_directions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var types = await db.EventCategories
            .Where(t => t.Level == CategoryLevel.Type)
            .Join(db.EventCategories, t => t.ParentId, c => c.Id, (t, c) => new { Type = t, Category = c })
            .Join(db.EventCategories, x => x.Category.ParentId, a => a.Id,
                (x, a) => new { x.Type.Name, x.Type.ProductClass, Audience = a.Name })
            .ToListAsync();

        var publicInPrivateBranch = types
            .Where(t => t.Audience == "Private Events" && t.ProductClass != EventProduct.Private)
            .Select(t => t.Name).ToList();
        Assert.True(publicInPrivateBranch.Count == 0,
            "Types under Private Events not classified Private: " + string.Join(", ", publicInPrivateBranch));

        var privateOutsideBranch = types
            .Where(t => t.Audience != "Private Events" && t.ProductClass == EventProduct.Private)
            .Select(t => t.Name).ToList();
        Assert.True(privateOutsideBranch.Count == 0,
            "Types classified Private outside Private Events: " + string.Join(", ", privateOutsideBranch));
    }

    /// <summary>The migration adds <c>events.Product</c> NOT NULL to a table that already has rows, so the
    /// default has to be a valid enum member. An empty-string default would pass the migration and then
    /// throw on the first read of any pre-existing event.</summary>
    [Fact]
    public async Task Existing_events_default_to_the_public_product()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        // The column must be aliased "Value": FirstAsync wraps this as a subquery and EF projects
        // s."Value" out of it, so an unaliased column fails with 42703 rather than returning a row.
        var stored = await db.Database
            .SqlQuery<string>($"SELECT column_default AS \"Value\" FROM information_schema.columns WHERE table_name = 'events' AND column_name = 'Product'")
            .FirstAsync();

        Assert.Contains("Public", stored);
    }

    /// <summary>Guards the drift found while writing M1: a nullable enum skips the global enum→text loop in
    /// <c>KurxDbContext</c> (its ClrType is Nullable&lt;T&gt;, so IsEnum is false) and lands as integer while
    /// the non-nullable copies of the same enum land as text.</summary>
    [Fact]
    public async Task Product_columns_are_stored_as_text_everywhere()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var types = await db.Database.SqlQuery<string>($@"
            SELECT table_name || '.' || data_type AS ""Value"" FROM information_schema.columns
            WHERE (table_name = 'events' AND column_name = 'Product')
               OR (table_name = 'event_categories' AND column_name = 'ProductClass')
               OR (table_name = 'event_archetypes' AND column_name = 'Product')
            ORDER BY 1").ToListAsync();

        Assert.Equal(3, types.Count);
        Assert.All(types, t => Assert.EndsWith(".text", t));
    }
}
