using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using static Kurx.Infrastructure.Events.CapabilityResolver;

namespace Kurx.Tests;

/// <summary>D-266 M2 — the precedence rules of the capability engine. Pure resolution, so these need no
/// database: the point is that the same inputs give the same answer for backend, admin, web and Flutter
/// alike, which is only true if the decision lives in one deterministic function.</summary>
public class CapabilityResolverTests
{
    private const string Offline = "Offline";

    // ── The structural guarantees named in D13 §10 and the M2 approval ──────────────────────

    [Fact]
    public void A_workshop_cannot_enable_leaderboard()
    {
        // Learning is the Workshop archetype; D12 gives it no Leaderboard cell at all.
        Assert.Equal(CapabilityRule.Unsupported, StateOf("leaderboard", "learning", EventProduct.Public, Offline));
        Assert.Equal("capability_unsupported_for_archetype", Reject("leaderboard", "learning", EventProduct.Public, Offline));

        var r = Resolve("learning", EventProduct.Public, Offline, ["leaderboard"]);
        Assert.DoesNotContain("leaderboard", r.Enabled);
        Assert.Contains("leaderboard", r.Rejected);
    }

    [Fact]
    public void A_conference_cannot_enable_teams()
    {
        Assert.Equal(CapabilityRule.Unsupported, StateOf("teams", "conference", EventProduct.Public, Offline));
        var r = Resolve("conference", EventProduct.Public, Offline, ["teams"]);
        Assert.DoesNotContain("teams", r.Enabled);
        Assert.Contains("teams", r.Rejected);
    }

    [Fact]
    public void A_blood_drive_cannot_enable_tickets()
    {
        // Blood Drive is Civic; D12 gives Civic no Tickets cell — it is never monetised.
        Assert.Equal(CapabilityRule.Unsupported, StateOf("paid", "civic", EventProduct.Public, Offline));
        Assert.Contains("paid", Resolve("civic", EventProduct.Public, Offline, ["paid"]).Rejected);
    }

    [Fact]
    public void A_private_product_can_never_take_payment()
    {
        foreach (var money in new[] { "paid", "finance" })   // donations moved to Finance in M2
        {
            Assert.Equal(CapabilityRule.Unsupported,
                StateOf(money, "private-gathering", EventProduct.Private, Offline));
            Assert.Equal("private_product_cannot_take_payment",
                Reject(money, "private-gathering", EventProduct.Private, Offline));
        }
    }

    /// <summary>Product rules sit above the archetype matrix, so they must hold even for an archetype whose
    /// matrix row *does* allow the capability. Fundraising requires Finance; a Private product still cannot.</summary>
    [Fact]
    public void Product_rules_outrank_the_archetype_matrix()
    {
        Assert.Equal(CapabilityRule.Required, StateOf("finance", "fundraising", EventProduct.Public, Offline));
        Assert.Equal(CapabilityRule.Unsupported, StateOf("finance", "fundraising", EventProduct.Private, Offline));
    }

    // ── Precedence: Unsupported > Required > Default ────────────────────────────────────────

    [Fact]
    public void Unsupported_overrides_default()
    {
        // Feedback is a default (it was Universal), and A14 Private has no Feedback cell.
        Assert.Contains("feedback", DefaultCapabilities);
        Assert.Equal(CapabilityRule.Unsupported, StateOf("feedback", "private-gathering", EventProduct.Private, Offline));

        var r = Resolve("private-gathering", EventProduct.Private, Offline);
        Assert.DoesNotContain("feedback", r.Enabled);
    }

    [Fact]
    public void Required_is_enabled_even_when_nobody_asked_for_it()
    {
        var r = Resolve("learning", EventProduct.Public, Offline);
        Assert.Contains("agenda", r.Enabled);       // R for Learning
        Assert.Contains("certificates", r.Enabled); // R for Learning
    }

    [Fact]
    public void Defaults_never_override_the_archetype_matrix()
    {
        foreach (var archetype in CapabilityCatalog.ArchetypeDefaults.Keys)
        {
            var product = archetype == "private-gathering" ? EventProduct.Private : EventProduct.Public;
            var r = Resolve(archetype, product, Offline);
            foreach (var slug in r.Enabled)
                Assert.NotEqual(CapabilityRule.Unsupported, r.States[slug]);
        }
    }

    // ── Dependency graph ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Enabling_a_capability_pulls_in_its_dependencies()
    {
        // Tournament: Leaderboard is Required and depends on scoring, which is also Required.
        var r = Resolve("tournament", EventProduct.Public, Offline);
        Assert.Contains("leaderboard", r.Enabled);
        Assert.Contains("scoring", r.Enabled);
    }

    [Fact]
    public void A_dependency_the_matrix_forbids_takes_the_dependent_with_it()
    {
        // Finance depends on paid. A Private product can have neither, so asking for finance cannot
        // leave a half-enabled capability behind.
        var r = Resolve("private-gathering", EventProduct.Private, Offline, ["finance"]);
        Assert.DoesNotContain("finance", r.Enabled);
        Assert.DoesNotContain("paid", r.Enabled);
        Assert.Contains("finance", r.Rejected);
    }

    // ── Determinism ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Resolution_is_deterministic_and_order_independent()
    {
        var a = Resolve("competitive", EventProduct.Public, Offline, ["teams", "mentors", "leaderboard"]);
        var b = Resolve("competitive", EventProduct.Public, Offline, ["leaderboard", "mentors", "teams"]);
        Assert.Equal(a.Enabled.OrderBy(x => x), b.Enabled.OrderBy(x => x));
    }

    [Fact]
    public void An_event_with_no_archetype_resolves_nothing_rather_than_guessing()
    {
        var r = Resolve(null, EventProduct.Public, Offline, ["paid"]);
        Assert.Empty(r.Enabled);
        Assert.Contains("paid", r.Rejected);
    }

    /// <summary>Every slug the archetype matrix names must exist in the catalog. This is what would have
    /// caught X1 (10 modules with no slug) mechanically instead of by reading the matrix by eye.</summary>
    [Fact]
    public void Every_slug_named_by_the_matrix_exists_in_the_catalog()
    {
        var known = CapabilityCatalog.Capabilities.Select(c => c.Slug).ToHashSet(StringComparer.Ordinal);
        var unknown = CapabilityCatalog.ArchetypeDefaults
            .SelectMany(kv => kv.Value.Required.Concat(kv.Value.Optional))
            .Distinct().Where(s => !known.Contains(s)).OrderBy(s => s).ToList();

        Assert.True(unknown.Count == 0, "matrix names capabilities with no slug: " + string.Join(", ", unknown));
    }
}
