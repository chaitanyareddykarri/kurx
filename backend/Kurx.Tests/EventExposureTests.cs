using System.Text.RegularExpressions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;

namespace Kurx.Tests;

/// <summary>D-266 M3 Step 4A — guards the canonical public-exposure rule.
///
/// <para>This suite exists because of what the Step 4 audit found: <c>Visibility != Private</c> admitted
/// Unlisted and InviteOnly events onto public profiles while <c>== Listed</c> kept them out of search, and
/// <b>204 tests passed either way</b>. The leak was never a product decision — it was uncovered. These tests
/// are what make reintroducing it a build failure rather than a silent regression.</para></summary>
public class EventExposureTests
{
    /// <summary>D-362 — <c>Status</c> is now part of the rule, so the fixture states it. It defaults to
    /// <c>Published</c> because these cases are about the OTHER two axes; the lifecycle axis has its own
    /// block below.</summary>
    private static Event Ev(EventProduct product, EventVisibility visibility, bool deleted = false,
        EventStatus status = EventStatus.Published) => new()
    {
        Status = status,
        Product = product,
        Visibility = visibility,
        DeletedAt = deleted ? DateTime.UtcNow : null,
    };

    // ── The rule itself: Published AND Product == Public AND Visibility == Listed. Nothing else. ──

    /*
     * D-362 — the lifecycle axis, which this predicate did not carry.
     *
     * It answered Product + Visibility + not-deleted and said nothing about STATUS, so a PendingReview
     * event was "publicly visible" by the one rule the class docs call "the one rule for public
     * exposure". Nothing leaked — the single production consumer wrote its own status check beside the
     * call — but a rule every caller must silently complete is two sources of truth wearing one name.
     */
    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.PendingReview)]
    [InlineData(EventStatus.UnderReview)]
    [InlineData(EventStatus.ChangesRequested)]
    [InlineData(EventStatus.Rejected)]
    [InlineData(EventStatus.Approved)]     // approval is PERMISSION to publish, never publication
    [InlineData(EventStatus.Scheduled)]    // precedes open_registration, which is what publishes
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Archived)]
    public void No_status_other_than_published_is_publicly_visible(EventStatus status)
    {
        var ev = Ev(EventProduct.Public, EventVisibility.Listed, status: status);
        Assert.False(EventExposure.IsPubliclyVisible(ev));
        Assert.False(EventExposure.PubliclyVisible.Compile()(ev));
    }

    /// <summary>The pre-publication question, asked deliberately by name: `ApprovalService`'s `IfExternal`
    /// condition must answer "will this face an external audience once live" WHILE the event is in review.
    /// Keeping it a separate, named predicate is what stops it being read as a forgotten status check.</summary>
    [Fact]
    public void External_exposure_ignores_status_on_purpose()
    {
        var pending = Ev(EventProduct.Public, EventVisibility.Listed, status: EventStatus.PendingReview);
        Assert.True(EventExposure.IsExternallyExposed(pending));
        Assert.False(EventExposure.IsPubliclyVisible(pending));

        // It is still the exposure rule on the other two axes.
        Assert.False(EventExposure.IsExternallyExposed(
            Ev(EventProduct.Private, EventVisibility.Listed, status: EventStatus.PendingReview)));
    }

    [Fact]
    public void Only_a_listed_public_product_is_publicly_visible()
    {
        Assert.True(EventExposure.IsPubliclyVisible(Ev(EventProduct.Public, EventVisibility.Listed)));
    }

    [Theory]
    [InlineData(EventVisibility.Unlisted)]     // link-only: hidden from every discoverable surface
    [InlineData(EventVisibility.InviteOnly)]   // invitation grants registration, never publicity
    public void No_other_visibility_is_publicly_visible(EventVisibility v)
    {
        Assert.False(EventExposure.IsPubliclyVisible(Ev(EventProduct.Public, v)));
    }

    /// <summary>Product outranks visibility: a Private product is never public, whatever its visibility
    /// says. Without this, setting Listed on a wedding would expose it.</summary>
    [Theory]
    [InlineData(EventVisibility.Listed)]
    [InlineData(EventVisibility.Unlisted)]
    [InlineData(EventVisibility.InviteOnly)]
    public void A_private_product_is_never_publicly_visible(EventVisibility v)
    {
        Assert.False(EventExposure.IsPubliclyVisible(Ev(EventProduct.Private, v)));
    }

    [Fact]
    public void A_deleted_event_is_never_publicly_visible()
    {
        Assert.False(EventExposure.IsPubliclyVisible(Ev(EventProduct.Public, EventVisibility.Listed, deleted: true)));
    }

    /// <summary>The expression and the in-memory form must agree on every combination — they are two
    /// spellings of one rule, and two spellings drifting apart is the exact defect this replaced.</summary>
    [Fact]
    public void The_query_predicate_and_the_in_memory_check_agree_on_every_combination()
    {
        var compiled = EventExposure.PubliclyVisible.Compile();
        foreach (var product in Enum.GetValues<EventProduct>())
            foreach (var visibility in Enum.GetValues<EventVisibility>())
                foreach (var deleted in new[] { false, true })
                {
                    var e = Ev(product, visibility, deleted);
                    Assert.Equal(compiled(e), EventExposure.IsPubliclyVisible(e));
                }
    }

    // ── Repository guards: nobody may reintroduce the legacy shortcuts ───────────────────────

    private static IEnumerable<(string Path, string Text)> ProductionSources()
    {
        var root = SolutionRoot();
        foreach (var proj in new[] { "Kurx.Api", "Kurx.Application", "Kurx.Domain", "Kurx.Infrastructure" })
        {
            var dir = Path.Combine(root, proj);
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                    f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")) continue;
                yield return (f, File.ReadAllText(f));
            }
        }
    }

    [Fact]
    public void No_production_code_tests_visibility_with_a_legacy_shortcut()
    {
        // `Visibility != <anything>` is always a legacy exclusion guard: the canonical rule is expressed
        // positively, as Product == Public && Visibility == Listed.
        var offenders = ProductionSources()
            .Where(s => Regex.IsMatch(s.Text, @"Visibility\s*!=\s*EventVisibility\."))
            .Select(s => Path.GetFileName(s.Path))
            .ToList();

        Assert.True(offenders.Count == 0,
            "legacy `Visibility != EventVisibility.X` exposure guard(s) in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_production_code_makes_a_public_exposure_decision_from_private()
    {
        // EventExposure itself names Private in documentation only; the enum member survives until Step 5.
        var offenders = ProductionSources()
            .Where(s => !s.Path.EndsWith("EventExposure.cs", StringComparison.Ordinal))
            .Where(s => Regex.IsMatch(s.Text, @"Visibility\s*==\s*EventVisibility\.Private"))
            .Select(s => Path.GetFileName(s.Path))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Private-specific public-exposure logic in: " + string.Join(", ", offenders));
    }

    /// <summary>Every service that gates on <c>Visibility == Listed</c> must also gate on the product, or it
    /// would expose a Private product that happened to be marked Listed. This is what caught the two sites
    /// the mechanical pass got wrong.</summary>
    [Fact]
    public void Every_listed_check_is_paired_with_a_product_check()
    {
        var offenders = new List<string>();
        foreach (var (path, text) in ProductionSources())
        {
            foreach (Match m in Regex.Matches(text, @"(\w+(?:\.\w+)?)\.Visibility\s*==\s*EventVisibility\.Listed"))
            {
                var receiver = m.Groups[1].Value;
                // The product term must appear on the same receiver somewhere in the same statement.
                var start = text.LastIndexOf(';', m.Index) + 1;
                var end = text.IndexOf(';', m.Index);
                var statement = text[start..(end < 0 ? text.Length : end)];
                if (!statement.Contains($"{receiver}.Product == EventProduct.Public", StringComparison.Ordinal))
                    offenders.Add($"{Path.GetFileName(path)}:{receiver}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Listed check with no paired Product check: " + string.Join(", ", offenders.Distinct()));
    }

    private static string SolutionRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => Directory.GetParent(Path.GetDirectoryName(here)!)!.FullName;
}
