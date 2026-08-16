using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Reusing a design across events (D-355, Phase 13).
///
/// <para><b>A copy, never a reference.</b> That is the property everything here defends. Two designs that
/// shared artwork could not be independently archived, would put an object under <c>events/{id}/…</c>
/// inside a personal library, and — worst — would let an edit made months later in someone's library
/// silently change what an event's already-issued certificates claim to look like.</para>
///
/// <para>The subtle failure mode a copy has is <i>incompleteness</i>: a field property nobody remembered
/// to carry over produces a design that looks right in a list and wrong on the page. So the layout is
/// asserted property by property rather than by counting fields.</para>
/// </summary>
public class CertificateTemplateReuseTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateTemplateReuseTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OtherEventId, Guid OwnerId, Guid OutsiderId);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    private static byte[] Artwork()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(600, 424);
        using var output = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, output);
        return output.ToArray();
    }

    private ICertificateTemplateService Templates(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateTemplateService>();

    /// <summary>A layout that exercises every property a copy could forget.</summary>
    private static IReadOnlyList<CertificateFieldInput> RichLayout() =>
    [
        new("dynamicfield", "participant_name", "Participant name", null, 12.5, 41.25, 75, 10.5,
            Rotation: 0, ZOrder: 0, IsRequired: true, FontFamily: "serif", FontSizePt: 32,
            FontWeight: "bold", Color: "#0F172A",
            HorizontalAlignment: "center", VerticalAlignment: "middle"),
        new("text", null, null, "Certificate of Participation", 10, 18, 80, 8,
            Rotation: 0, ZOrder: 1, FontFamily: "sans", FontSizePt: 40, Color: "#334155",
            HorizontalAlignment: "center"),
        new("qrcode", null, null, null, 80, 78, 14, 14, Rotation: 0, ZOrder: 2),
        // A detected line the creator has not changed: a handle on the artwork's own words, drawing and
        // covering nothing (D-356). Carried through a copy like any other state.
        new("text", null, null, "has successfully completed", 10, 55, 80, 5,
            Rotation: 0, ZOrder: 3, IsMasking: false, MirrorsArtwork: true,
            BackgroundColor: "#FDF6E3", FontSizePt: 12, HorizontalAlignment: "center"),
    ];

    private async Task<Fixture> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
        var owner = new User { Phone = Phone(), Name = "Creator " + suffix };
        var outsider = new User { Phone = Phone(), Name = "Outsider " + suffix };
        db.Organizations.Add(org);
        db.Users.AddRange(owner, outsider);

        var categoryId = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync();

        Event NewEvent(string title, Guid creator) => new()
        {
            Title = title + " " + suffix,
            Slug = $"{title.ToLowerInvariant()}-{suffix}",
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            RepresentingOrgId = org.Id, CreatedBy = creator, CategoryId = categoryId,
            StartsAt = DateTime.UtcNow.AddDays(30), EndsAt = DateTime.UtcNow.AddDays(31),
            Status = EventStatus.Published,
        };

        var mine = NewEvent("Hackathon", owner.Id);
        var next = NewEvent("Nextyear", owner.Id);
        db.Events.AddRange(mine, next);
        await db.SaveChangesAsync();

        return new Fixture(mine.Id, next.Id, owner.Id, outsider.Id);
    }

    /// <summary>A finished design on an event: artwork uploaded through the real path, fields placed.</summary>
    private async Task<Guid> DesignAsync(IServiceScope scope, Fixture f, Guid? eventId = null)
    {
        var templates = Templates(scope);
        var created = await templates.CreateAsync(f.OwnerId, eventId ?? f.EventId,
            new CertificateTemplateInput("Participation", "a4-landscape"), false);
        Assert.True(created.Ok, created.Error);
        var id = created.Value!.Id;

        var artwork = Artwork();
        var presigned = await templates.PresignBackgroundAsync(f.OwnerId, id, "image/png", artwork.Length, false);
        await scope.ServiceProvider.GetRequiredService<IStorage>()
            .PutAsync(presigned.Value!.Key, artwork, "image/png");
        await templates.SetBackgroundAsync(f.OwnerId, id,
            new CertificateBackgroundInput(presigned.Value.Key, "image/png", 3508, 2480), false);

        Assert.True((await templates.ReplaceFieldsAsync(f.OwnerId, id, RichLayout(), false)).Ok);
        return id;
    }

    // ── Saving into the library ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_event_design_can_be_saved_into_the_library()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);

        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        Assert.True(copy.Ok, copy.Error);
        Assert.Null(copy.Value!.EventId);
        Assert.Equal(f.OwnerId, copy.Value.OwnerUserId);
        Assert.Equal("House style", copy.Value.Name);
        Assert.NotEqual(source, copy.Value.Id);
    }

    [Fact]
    public async Task A_copy_keeps_the_original_name_when_none_is_given()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);

        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, null, false);

        Assert.Equal("Participation", copy.Value!.Name);
    }

    [Fact]
    public async Task A_saved_design_appears_in_the_library_listing()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        var library = await Templates(scope).ListLibraryAsync(f.OwnerId);

        Assert.Contains(library.Value!, t => t.Name == "House style");
        // The event's own design is not in the library — the copy is a separate thing.
        Assert.DoesNotContain(library.Value!, t => t.Id == source);
    }

    // ── Using one on an event ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_library_design_can_be_used_on_another_event()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var saved = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        var used = await Templates(scope).CopyToEventAsync(
            f.OwnerId, saved.Value!.Id, f.OtherEventId, null, false);

        Assert.True(used.Ok, used.Error);
        Assert.Equal(f.OtherEventId, used.Value!.EventId);
        Assert.Equal(RichLayout().Count, used.Value.Fields.Count);
    }

    /// <summary>The copy starts fresh. Version tracks what a given certificate was rendered from, so
    /// inheriting the source's would attach history to a design that has issued nothing.</summary>
    [Fact]
    public async Task A_copy_starts_at_version_one_and_in_draft()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);

        // Push the source past version 1 by issuing from it and then changing what renders.
        await Templates(scope).UpdateAsync(f.OwnerId, source,
            new CertificateTemplateInput(null, null, "ready"), false);
        var issued = await scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>()
            .IssueAsync(f.OwnerId, source,
                new IssueCertificateInput("Rahul Sharma", null, new Dictionary<string, string>()), false);
        Assert.True(issued.Ok, issued.Error);
        await Templates(scope).ReplaceFieldsAsync(f.OwnerId, source, RichLayout(), false);

        var sourceNow = await Templates(scope).GetAsync(f.OwnerId, source, false);
        Assert.True(sourceNow.Value!.Version > 1);

        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        Assert.Equal(1, copy.Value!.Version);
        Assert.Equal("draft", copy.Value.Status);
    }

    // ── What a copy must carry ──────────────────────────────────────────────────────────────────

    /// <summary>Property by property rather than by counting: a field attribute nobody remembered to
    /// carry over produces a design that looks right in a list and wrong on the page.</summary>
    [Fact]
    public async Task The_layout_is_copied_property_for_property()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        var before = await db.CertificateTemplateFields.AsNoTracking()
            .Where(x => x.TemplateId == source).OrderBy(x => x.ZOrder).ToListAsync();
        var after = await db.CertificateTemplateFields.AsNoTracking()
            .Where(x => x.TemplateId == copy.Value!.Id).OrderBy(x => x.ZOrder).ToListAsync();

        Assert.Equal(before.Count, after.Count);
        foreach (var (original, duplicate) in before.Zip(after))
        {
            Assert.Equal(
                (original.Kind, original.FieldKey, original.Label, original.StaticText,
                 original.X, original.Y, original.Width, original.Height, original.Rotation,
                 original.ZOrder, original.IsRequired, original.IsMasking, original.MirrorsArtwork,
                 original.BackgroundColor,
                 original.FontFamily, original.FontSizePt, original.FontWeight, original.Color,
                 original.HorizontalAlignment, original.VerticalAlignment,
                 original.FontStyle, original.Underline, original.LineHeight, original.LetterSpacing),
                (duplicate.Kind, duplicate.FieldKey, duplicate.Label, duplicate.StaticText,
                 duplicate.X, duplicate.Y, duplicate.Width, duplicate.Height, duplicate.Rotation,
                 duplicate.ZOrder, duplicate.IsRequired, duplicate.IsMasking, duplicate.MirrorsArtwork,
                 duplicate.BackgroundColor,
                 duplicate.FontFamily, duplicate.FontSizePt, duplicate.FontWeight, duplicate.Color,
                 duplicate.HorizontalAlignment, duplicate.VerticalAlignment,
                 duplicate.FontStyle, duplicate.Underline, duplicate.LineHeight, duplicate.LetterSpacing));

            // New rows, not the same rows re-pointed.
            Assert.NotEqual(original.Id, duplicate.Id);
        }
    }

    /// <summary>Sharing a key would put an object under <c>events/{id}/…</c> inside a personal library and
    /// make the two designs impossible to archive independently.</summary>
    [Fact]
    public async Task The_artwork_is_duplicated_under_the_copys_own_prefix()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        var original = await db.CertificateTemplates.AsNoTracking().FirstAsync(t => t.Id == source);
        var duplicate = await db.CertificateTemplates.AsNoTracking()
            .FirstAsync(t => t.Id == copy.Value!.Id);

        Assert.NotEqual(original.BackgroundStorageKey, duplicate.BackgroundStorageKey);
        Assert.StartsWith($"users/{f.OwnerId}/certificate-templates/", duplicate.BackgroundStorageKey!);
        // Both objects exist: the copy is real bytes, not a pointer at someone else's.
        Assert.True(await storage.ExistsAsync(original.BackgroundStorageKey!));
        Assert.True(await storage.ExistsAsync(duplicate.BackgroundStorageKey!));
        Assert.Equal(
            await storage.GetAsync(original.BackgroundStorageKey!),
            await storage.GetAsync(duplicate.BackgroundStorageKey!));
    }

    [Fact]
    public async Task A_copy_onto_an_event_lands_under_that_events_prefix()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var saved = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        var used = await Templates(scope).CopyToEventAsync(
            f.OwnerId, saved.Value!.Id, f.OtherEventId, null, false);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var duplicate = await db.CertificateTemplates.AsNoTracking()
            .FirstAsync(t => t.Id == used.Value!.Id);

        Assert.StartsWith($"events/{f.OtherEventId}/certificates/templates/", duplicate.BackgroundStorageKey!);
    }

    // ── Independence ────────────────────────────────────────────────────────────────────────────

    /// <summary>The reason a copy exists at all. An edit made in a library months later must not change
    /// what an event's already-issued certificates claim to look like.</summary>
    [Fact]
    public async Task Editing_the_copy_does_not_touch_the_original()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        await Templates(scope).ReplaceFieldsAsync(f.OwnerId, copy.Value!.Id,
            [new("text", null, null, "Totally different", 0, 0, 50, 5)], false);
        await Templates(scope).UpdateAsync(f.OwnerId, copy.Value.Id,
            new CertificateTemplateInput("Renamed", null, null), false);

        var original = await Templates(scope).GetAsync(f.OwnerId, source, false);

        Assert.Equal("Participation", original.Value!.Name);
        Assert.Equal(RichLayout().Count, original.Value.Fields.Count);
    }

    [Fact]
    public async Task Editing_the_original_does_not_touch_the_copy()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        await Templates(scope).ReplaceFieldsAsync(f.OwnerId, source,
            [new("text", null, null, "Changed", 0, 0, 50, 5)], false);

        var saved = await Templates(scope).GetAsync(f.OwnerId, copy.Value!.Id, false);

        Assert.Equal(RichLayout().Count, saved.Value!.Fields.Count);
    }

    /// <summary>Archiving one leaves the other alone — impossible if they shared storage.</summary>
    [Fact]
    public async Task Archiving_the_original_leaves_the_copy_usable()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        await Templates(scope).ArchiveAsync(f.OwnerId, source, false);

        var saved = await Templates(scope).GetAsync(f.OwnerId, copy.Value!.Id, false);
        Assert.Equal("draft", saved.Value!.Status);

        var used = await Templates(scope).CopyToEventAsync(
            f.OwnerId, copy.Value.Id, f.OtherEventId, null, false);
        Assert.True(used.Ok, used.Error);
    }

    /// <summary>End to end: a design saved once and used on a second event issues real certificates
    /// there.</summary>
    [Fact]
    public async Task A_reused_design_issues_certificates_on_the_new_event()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var saved = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);
        var used = await Templates(scope).CopyToEventAsync(
            f.OwnerId, saved.Value!.Id, f.OtherEventId, null, false);

        var issued = await scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>()
            .IssueAsync(f.OwnerId, used.Value!.Id,
                new IssueCertificateInput("Priya Patel", null, new Dictionary<string, string>()), false);

        Assert.True(issued.Ok, issued.Error);
        Assert.Equal(f.OtherEventId, issued.Value!.EventId);

        var verdict = await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync(issued.Value.CertificateId);
        Assert.Equal(CertificateVerificationOutcome.Valid, verdict.Outcome);
    }

    // ── Authority ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_outsider_cannot_copy_a_design_they_cannot_see()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);

        var toLibrary = await Templates(scope).CopyToLibraryAsync(f.OutsiderId, source, null, false);
        var toEvent = await Templates(scope).CopyToEventAsync(
            f.OutsiderId, source, f.OtherEventId, null, false);

        // D-018: not-found rather than forbidden.
        Assert.Equal("not_found", toLibrary.Error);
        Assert.Equal("not_found", toEvent.Error);
    }

    /// <summary>Being allowed to read a design says nothing about being allowed to add content to a
    /// destination event — two entitlements, checked separately.</summary>
    [Fact]
    public async Task A_design_cannot_be_copied_onto_an_event_the_caller_does_not_run()
    {
        var f = await SeedAsync();
        var theirs = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);

        var result = await Templates(scope).CopyToEventAsync(
            f.OwnerId, source, theirs.EventId, null, false);

        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    /// <summary>A creator's private library is not event content, and platform admins are deliberately
    /// not given a way into it.</summary>
    [Fact]
    public async Task An_admin_cannot_reach_someone_elses_library_design()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var saved = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        var asAdmin = await Templates(scope).CopyToLibraryAsync(
            f.OutsiderId, saved.Value!.Id, null, isAdmin: true);

        Assert.Equal("not_found", asAdmin.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_copy_needs_a_usable_name(string name)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);

        var result = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, name, false);

        Assert.False(result.Ok);
        Assert.Equal("invalid_name", result.Error);
    }

    /// <summary>Copying a design whose artwork has gone would produce a template that renders as a blank
    /// page, discovered at generation time. Refused instead.</summary>
    [Fact]
    public async Task A_design_whose_artwork_is_missing_is_not_copied()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var source = await DesignAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var template = await db.CertificateTemplates.FirstAsync(t => t.Id == source);
        template.BackgroundStorageKey = "events/gone/certificates/templates/nothing/v1/background.png";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Templates(scope).CopyToLibraryAsync(f.OwnerId, source, "House style", false);

        Assert.False(result.Ok);
        Assert.Equal("artwork_unavailable", result.Error);
    }

    /// <summary>A design with no artwork yet is copyable — it is a work in progress, not a broken one.</summary>
    [Fact]
    public async Task A_design_with_no_artwork_yet_can_still_be_copied()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var created = await Templates(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Work in progress", "a4-portrait"), false);

        var copy = await Templates(scope).CopyToLibraryAsync(f.OwnerId, created.Value!.Id, null, false);

        Assert.True(copy.Ok, copy.Error);
        Assert.Null(copy.Value!.BackgroundStorageKey);
        Assert.Equal("a4-portrait", copy.Value.PageSize);
    }
}
