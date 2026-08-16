using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Certificates;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// The participant photo on a certificate (D-362).
///
/// <para>Before this, <c>ResolveImagesAsync</c> returned an empty dictionary and the field→element mapping
/// hardcoded <c>ImageKey: null</c> — an image field was structurally accepted and then rendered nothing.
/// These pin the two halves that make it real: an image field must name which picture it wants, and whose
/// photo the server is willing to print.</para>
/// </summary>
public class CertificatePhotoFieldTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public CertificatePhotoFieldTests(KurxApiFactory factory) => _factory = factory;

    /// <summary>An image field with no key has nothing to paint — the same failure a dynamic field with no
    /// key has, and it must be refused at the same boundary rather than rendering a blank box on every
    /// certificate.</summary>
    [Fact]
    public async Task An_image_field_without_a_key_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = await SeedTemplateAsync(scope);
        var svc = scope.ServiceProvider.GetRequiredService<ICertificateTemplateService>();

        var result = await svc.ReplaceFieldsAsync(seeded.OwnerId, seeded.TemplateId,
            [Field(kind: "image", fieldKey: null)], isAdmin: false);

        Assert.False(result.Ok);
        Assert.Equal("field_key_required", result.Error);
    }

    [Fact]
    public async Task An_image_field_naming_the_photo_slot_is_accepted()
    {
        using var scope = _factory.Services.CreateScope();
        var seeded = await SeedTemplateAsync(scope);
        var svc = scope.ServiceProvider.GetRequiredService<ICertificateTemplateService>();

        var result = await svc.ReplaceFieldsAsync(seeded.OwnerId, seeded.TemplateId,
            [Field(kind: "image", fieldKey: CertificateIssuingService.ParticipantPhotoSlot)], isAdmin: false);

        Assert.True(result.Ok, result.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var saved = await db.CertificateTemplateFields.AsNoTracking()
            .Where(f => f.TemplateId == seeded.TemplateId).SingleAsync();

        Assert.Equal(CertificateFieldKind.Image, saved.Kind);
        // The key is what becomes ImageKey on the render element — without it the renderer paints nothing.
        Assert.Equal(CertificateIssuingService.ParticipantPhotoSlot, saved.FieldKey);
    }

    /// <summary>The slot name is shared between the server that fills it and the editor that places it.
    /// If either side renames it, certificates silently print without photos — so it is asserted rather
    /// than assumed.</summary>
    [Fact]
    public void The_photo_slot_name_is_stable()
    {
        Assert.Equal("participant_photo", CertificateIssuingService.ParticipantPhotoSlot);
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────────

    private sealed record Seeded(Guid EventId, Guid OwnerId, Guid TemplateId);

    private async Task<Seeded> SeedTemplateAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var owner = new User { Name = "Photo Owner " + suffix, Phone = "+9193000" + suffix[..5] };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var orgId = _factory.SeedVerifiedOrg(owner.Id, "Photo Org " + suffix, OrgRole.Owner);

        var category = new EventCategory
        {
            Level = CategoryLevel.Category, Name = "Photo " + suffix, Slug = "photo-" + suffix,
        };
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();

        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = owner.Id, CategoryId = category.Id,
            Title = "Photo Event", Slug = "photo-event-" + suffix,
            ShortCode = suffix[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);

        var template = new CertificateTemplate
        {
            EventId = ev.Id,
            OwnerUserId = owner.Id,
            Name = "Photo template",
            PageSize = CertificatePageSize.A4Landscape,
        };
        db.CertificateTemplates.Add(template);
        await db.SaveChangesAsync();

        return new Seeded(ev.Id, owner.Id, template.Id);
    }

    private static CertificateFieldInput Field(string kind, string? fieldKey) => new(
        Kind: kind,
        FieldKey: fieldKey,
        Label: "Participant photo",
        StaticText: null,
        X: 10, Y: 10, Width: 20, Height: 20);
}
