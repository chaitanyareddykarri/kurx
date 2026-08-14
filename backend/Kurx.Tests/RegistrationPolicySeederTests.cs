using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-266 M3 Step 2 — every Type carries an explicit registration-policy allow-list, and the stored
/// list can never contradict what <see cref="PolicyResolver"/> would compute.</summary>
public class RegistrationPolicySeederTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public RegistrationPolicySeederTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            Seed(EventTaxonomySeeder.SeedAsync);
            Seed(ArchetypeSeeder.SeedAsync);
            Seed(RegistrationPolicySeeder.SeedAsync);
            Seed(RegistrationPolicySeeder.SeedAsync);   // idempotent + re-syncs
            _reset = true;
        }
    }

    private void Seed(Func<KurxDbContext, CancellationToken, Task> run)
    {
        using var scope = _factory.Services.CreateScope();
        run(scope.ServiceProvider.GetRequiredService<KurxDbContext>(), default).GetAwaiter().GetResult();
    }

    private static EventRegistrationPolicy[] Parse(string json) =>
        JsonSerializer.Deserialize<string[]>(json)!.Select(Enum.Parse<EventRegistrationPolicy>).ToArray();

    [Fact]
    public async Task Every_type_has_an_explicit_allow_list()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var missing = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.AllowedRegistrationPoliciesJson == null)
            .Select(c => c.Name).ToListAsync();

        Assert.True(missing.Count == 0, "types with no allow-list: " + string.Join(", ", missing.Take(10)));
    }

    [Fact]
    public async Task No_type_allows_every_policy()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var all = Enum.GetValues<EventRegistrationPolicy>().Length;

        var rows = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.AllowedRegistrationPoliciesJson != null)
            .Select(c => new { c.Name, c.AllowedRegistrationPoliciesJson }).ToListAsync();

        var permissive = rows.Where(r => Parse(r.AllowedRegistrationPoliciesJson!).Length >= all)
                             .Select(r => r.Name).ToList();
        Assert.True(permissive.Count == 0, "types allowing everything: " + string.Join(", ", permissive));
    }

    [Fact]
    public async Task Private_types_allow_invite_only()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var rows = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.ProductClass == EventProduct.Private
                     && c.AllowedRegistrationPoliciesJson != null)
            .Select(c => new { c.Name, c.AllowedRegistrationPoliciesJson }).ToListAsync();

        Assert.NotEmpty(rows);
        foreach (var r in rows)
            Assert.Equal([EventRegistrationPolicy.InviteOnly], Parse(r.AllowedRegistrationPoliciesJson!));
    }

    /// <summary>The named product decisions of Step 2, asserted individually so a silent change to the
    /// archetype default cannot quietly relax one of them.</summary>
    [Theory]
    [InlineData("Blood Drive", new[] { "Open" })]
    // The seeder is canonical since Step 6, so these assert the canonical names. The overrides still
    // cover the legacy names too, for databases mid-upgrade.
    [InlineData("Campus Recruitment", new[] { "CollegeRestricted", "InviteOnly" })]
    [InlineData("Alumni Meet", new[] { "AlumniOnly", "InviteOnly" })]
    [InlineData("Fundraiser", new[] { "Open" })]
    public async Task Named_types_carry_their_approved_allow_list(string typeName, string[] expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var json = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.Name == typeName)
            .Select(c => c.AllowedRegistrationPoliciesJson).FirstOrDefaultAsync();

        Assert.NotNull(json);
        Assert.Equal(expected.Select(Enum.Parse<EventRegistrationPolicy>), Parse(json!));
    }

    /// <summary>The stored list and the resolver must agree. If they can disagree there are two sources of
    /// truth for who may register, which is the failure mode M3 exists to prevent.</summary>
    [Fact]
    public async Task Stored_allow_lists_never_contradict_the_resolver()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var rows = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type && c.AllowedRegistrationPoliciesJson != null)
            .Select(c => new { c.Name, c.ProductClass, c.AllowedRegistrationPoliciesJson }).ToListAsync();

        var conflicts = new List<string>();
        foreach (var r in rows)
        {
            var stored = Parse(r.AllowedRegistrationPoliciesJson!);
            var resolved = PolicyResolver.AllowedFor(r.ProductClass ?? EventProduct.Public, stored);
            // The resolver may narrow (product rules), never widen beyond what is stored.
            foreach (var p in resolved)
                if (!stored.Contains(p)) conflicts.Add($"{r.Name}: resolver allows {p}, taxonomy does not");
        }
        Assert.True(conflicts.Count == 0, string.Join(" | ", conflicts.Take(10)));
    }
}
