using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Certificate page sizes (D-361).
///
/// <para>The properties that carry weight. <b>A preset's dimensions come from the server</b>, never from
/// the request — otherwise one caller can assert that A4 is 500 mm wide and every certificate issued from
/// that template prints wrong while the label still reads A4. <b>Custom is bounded</b>, including against
/// the values a range check silently accepts: every comparison against NaN is false, so a naive
/// `&lt; min || &gt; max` lets NaN through to the renderer as a page of undefined size. And <b>existing
/// templates keep their page</b>, which is the whole reason the size is stored rather than derived.</para>
/// </summary>
public class CertificatePageSizeTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificatePageSizeTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OwnerId);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    private async Task<Fixture> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
        var owner = new User { Phone = Phone(), Name = "Owner " + suffix };
        db.Organizations.Add(org);
        db.Users.Add(owner);

        var ev = new Event
        {
            Title = "Event " + suffix,
            Slug = "event-" + suffix,
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d",
            VenueName = "v",
            RepresentingOrgId = org.Id,
            CreatedBy = owner.Id,
            CategoryId = await db.EventCategories.AsNoTracking()
                .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync(),
            StartsAt = DateTime.UtcNow.AddDays(30),
            EndsAt = DateTime.UtcNow.AddDays(31),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        db.Memberships.Add(new Membership { OrgId = org.Id, UserId = owner.Id, Role = OrgRole.Owner, IsVerified = true });
        await db.SaveChangesAsync();
        return new Fixture(ev.Id, owner.Id);
    }

    private ICertificateTemplateService Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateTemplateService>();

    // ── the catalogue ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_requested_preset_exists_in_both_orientations()
    {
        foreach (var family in new[] { "a4", "a5", "letter", "legal", "8x10", "11x14", "12x16" })
        {
            Assert.NotNull(CertificatePageSizes.BySlug($"{family}-portrait"));
            Assert.NotNull(CertificatePageSizes.BySlug($"{family}-landscape"));
        }
        Assert.Equal(14, CertificatePageSizes.All.Count);
    }

    [Theory]
    // Metric sizes are exact millimetres; the imperial ones are inches × 25.4 and are deliberately NOT
    // rounded — a fifth of a millimetre per edge is a visible misalignment on a printed border.
    [InlineData("a4-portrait", 210, 297)]
    [InlineData("a4-landscape", 297, 210)]
    [InlineData("a5-portrait", 148, 210)]
    [InlineData("a5-landscape", 210, 148)]
    [InlineData("letter-portrait", 215.9, 279.4)]
    [InlineData("letter-landscape", 279.4, 215.9)]
    [InlineData("legal-portrait", 215.9, 355.6)]
    [InlineData("legal-landscape", 355.6, 215.9)]
    [InlineData("8x10-portrait", 203.2, 254)]
    [InlineData("11x14-portrait", 279.4, 355.6)]
    [InlineData("12x16-portrait", 304.8, 406.4)]
    public void A_preset_measures_what_its_name_says(string slug, double widthMm, double heightMm)
    {
        var preset = CertificatePageSizes.BySlug(slug);
        Assert.NotNull(preset);
        Assert.Equal(widthMm, preset!.WidthMm, 3);
        Assert.Equal(heightMm, preset.HeightMm, 3);
    }

    [Fact]
    public void Landscape_is_its_portrait_turned_over_never_a_separate_measurement()
    {
        foreach (var family in CertificatePageSizes.All.Select(p => p.Family).Distinct())
        {
            var portrait = CertificatePageSizes.All.Single(p => p.Family == family && !p.Landscape);
            var landscape = CertificatePageSizes.All.Single(p => p.Family == family && p.Landscape);
            Assert.Equal(portrait.WidthMm, landscape.HeightMm, 3);
            Assert.Equal(portrait.HeightMm, landscape.WidthMm, 3);
            // A portrait page is taller than it is wide. If this ever fails the two are swapped.
            Assert.True(portrait.HeightMm > portrait.WidthMm, $"{family} portrait is not portrait");
        }
    }

    [Fact]
    public void Every_enum_member_except_custom_has_a_slug_and_back()
    {
        foreach (var size in Enum.GetValues<CertificatePageSize>())
        {
            var slug = CertificatePageSizes.SlugFor(size);
            if (size == CertificatePageSize.Custom) { Assert.Equal("custom", slug); continue; }
            Assert.Equal(size, CertificatePageSizes.BySlug(slug)!.Size);
        }
    }

    // ── creating and changing ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a5-portrait", 148, 210)]
    [InlineData("letter-landscape", 279.4, 215.9)]
    [InlineData("12x16-portrait", 304.8, 406.4)]
    public async Task A_template_can_be_created_on_any_preset(string slug, double w, double h)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", slug), false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(slug, result.Value!.PageSize);
        Assert.Equal(w, result.Value.PageWidthMm, 3);
        Assert.Equal(h, result.Value.PageHeightMm, 3);
    }

    [Fact]
    public async Task Existing_a4_templates_are_untouched()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        // The two sizes that existed before D-361 must round-trip exactly as they always did, or every
        // template already saved changes size on deploy.
        foreach (var (slug, w, h) in new[] { ("a4-landscape", 297d, 210d), ("a4-portrait", 210d, 297d) })
        {
            var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
                new CertificateTemplateInput("Design", slug), false);
            Assert.Equal(slug, result.Value!.PageSize);
            Assert.Equal(w, result.Value.PageWidthMm, 3);
            Assert.Equal(h, result.Value.PageHeightMm, 3);
        }
    }

    [Fact]
    public async Task An_omitted_page_size_still_defaults_to_a4_landscape()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design"), false);

        Assert.Equal("a4-landscape", result.Value!.PageSize);
        Assert.Equal(297, result.Value.PageWidthMm, 3);
    }

    [Fact]
    public async Task Switching_size_replaces_both_the_label_and_the_measurements()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);

        var created = await svc.CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", "a4-portrait"), false);
        var updated = await svc.UpdateAsync(f.OwnerId, created.Value!.Id,
            new CertificateTemplateInput(PageSize: "legal-landscape"), false);

        Assert.True(updated.Ok, updated.Error);
        Assert.Equal("legal-landscape", updated.Value!.PageSize);
        Assert.Equal(355.6, updated.Value.PageWidthMm, 3);
        Assert.Equal(215.9, updated.Value.PageHeightMm, 3);
    }

    // ── custom ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_custom_page_stores_the_dimensions_it_was_given()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", "custom", PageWidthMm: 320, PageHeightMm: 240), false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("custom", result.Value!.PageSize);
        Assert.Equal(320, result.Value.PageWidthMm, 3);
        Assert.Equal(240, result.Value.PageHeightMm, 3);
    }

    [Theory]
    [InlineData(0, 200, "page_size_out_of_range")]
    [InlineData(-210, 297, "page_size_out_of_range")]
    [InlineData(200, -1, "page_size_out_of_range")]
    [InlineData(49, 200, "page_size_out_of_range")]      // just under the floor
    [InlineData(1001, 200, "page_size_out_of_range")]    // just over the ceiling
    [InlineData(double.NaN, 200, "invalid_page_size")]
    [InlineData(200, double.PositiveInfinity, "invalid_page_size")]
    public async Task An_impossible_custom_page_is_refused(double w, double h, string expected)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", "custom", PageWidthMm: w, PageHeightMm: h), false);

        Assert.False(result.Ok);
        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [InlineData(50, 50)]      // exactly the floor
    [InlineData(1000, 1000)]  // exactly the ceiling
    public async Task The_bounds_themselves_are_accepted(double w, double h)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", "custom", PageWidthMm: w, PageHeightMm: h), false);

        Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public async Task Custom_without_dimensions_is_refused_rather_than_silently_defaulted()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        // Quietly falling back to A4 would print a certificate at the wrong size under a label saying
        // Custom, and nobody would learn that until it came off the printer.
        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", "custom"), false);

        Assert.False(result.Ok);
        Assert.Equal("page_size_required", result.Error);
    }

    [Fact]
    public async Task A_preset_ignores_dimensions_supplied_by_the_caller()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        // The security property: a request cannot redefine what A4 measures. Were this to pass through,
        // the template would claim A4 on its face and print at 500 mm.
        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", "a4-portrait", PageWidthMm: 500, PageHeightMm: 500), false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(210, result.Value!.PageWidthMm, 3);
        Assert.Equal(297, result.Value.PageHeightMm, 3);
    }

    [Fact]
    public async Task An_unknown_page_name_is_refused()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Design", "a3-portrait"), false);

        Assert.False(result.Ok);
        Assert.Equal("invalid_page_size", result.Error);
    }
}
