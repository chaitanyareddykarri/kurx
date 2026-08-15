using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Certificate designs (D-344, Phase 3).
///
/// <para>The properties that carry weight: <b>a template is reachable only by someone entitled to it</b>,
/// through whichever of the two authority rules applies; <b>artwork cannot be attached from outside the
/// template's own prefix</b>, which is what stops a design referencing another event's private objects;
/// and <b>editing a template that has already issued certificates produces a new version</b> rather than
/// retroactively changing what those certificates claim to look like.</para>
///
/// <para>No OCR is involved anywhere here — a creator places every field by hand, which is the confirmed
/// requirement that the editor must work without detection.</para>
/// </summary>
public class CertificateTemplateTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateTemplateTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OtherEventId, Guid OwnerId, Guid OutsiderId);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

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
            Title = title,
            Slug = $"{title.ToLowerInvariant()}-{suffix}",
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            RepresentingOrgId = org.Id, CreatedBy = creator, CategoryId = categoryId,
            StartsAt = DateTime.UtcNow.AddDays(30), EndsAt = DateTime.UtcNow.AddDays(31),
            Status = EventStatus.Published,
        };

        var mine = NewEvent("Mine", owner.Id);
        var theirs = NewEvent("Theirs", outsider.Id);
        db.Events.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        return new Fixture(mine.Id, theirs.Id, owner.Id, outsider.Id);
    }

    private ICertificateTemplateService Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateTemplateService>();

    private static CertificateFieldInput NameField(string key = "participant_name") =>
        new("dynamicfield", key, "Participant name", null, 10, 40, 80, 10,
            HorizontalAlignment: "center", FontSizePt: 32, Color: "#0F172A");

    /// <summary>Uploads real bytes through IStorage and attaches them, so tests that need artwork exercise
    /// the same presign → PUT → record path a browser does rather than poking the column.</summary>
    private async Task<CertificateTemplateView> WithBackgroundAsync(
        IServiceScope scope, Guid userId, Guid templateId)
    {
        var svc = Service(scope);
        var presigned = await svc.PresignBackgroundAsync(userId, templateId, "image/png", 1024, false);
        Assert.True(presigned.Ok, presigned.Error);

        await scope.ServiceProvider.GetRequiredService<IStorage>()
            .PutAsync(presigned.Value!.Key, [1, 2, 3, 4], "image/png");

        var set = await svc.SetBackgroundAsync(userId, templateId,
            new CertificateBackgroundInput(presigned.Value.Key, "image/png", 3508, 2480), false);
        Assert.True(set.Ok, set.Error);
        return set.Value!;
    }

    // ── Creation and authority ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_creator_can_create_a_template_on_their_event()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("Participation design", "a4-landscape"), false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(f.EventId, result.Value!.EventId);
        Assert.Equal(f.OwnerId, result.Value.OwnerUserId);
        Assert.Equal("draft", result.Value.Status);
        Assert.Equal(1, result.Value.Version);
        Assert.Empty(result.Value.Fields);
    }

    [Fact]
    public async Task An_outsider_cannot_create_or_list_on_someone_elses_event()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);

        Assert.Equal("forbidden", (await svc.ListForEventAsync(f.OutsiderId, f.EventId, false)).Error);
        Assert.Equal("forbidden", (await svc.CreateAsync(f.OutsiderId, f.EventId,
            new CertificateTemplateInput("Theirs"), false)).Error);
    }

    /// <summary>Another event's template is invisible, not forbidden: a 403 would confirm the id exists
    /// and turn the by-id routes into an enumeration oracle (D-018).</summary>
    [Fact]
    public async Task Another_events_template_is_not_found_rather_than_forbidden()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);

        var theirs = await svc.CreateAsync(f.OutsiderId, f.OtherEventId, new CertificateTemplateInput("Theirs"), false);

        Assert.Equal("not_found", (await svc.GetAsync(f.OwnerId, theirs.Value!.Id, false)).Error);
        Assert.Equal("not_found", (await svc.UpdateAsync(f.OwnerId, theirs.Value.Id,
            new CertificateTemplateInput("Stolen"), false)).Error);
        Assert.Equal("not_found", (await svc.ArchiveAsync(f.OwnerId, theirs.Value.Id, false)).Error);
    }

    [Theory]
    [InlineData("", "invalid_name")]
    [InlineData("   ", "invalid_name")]
    [InlineData("ok", null)]
    public async Task A_template_name_is_bounded(string name, string? expected)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Service(scope).CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput(name), false);

        Assert.Equal(expected is null, result.Ok);
        if (expected is not null) Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task An_unknown_page_size_is_refused()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        Assert.Equal("invalid_page_size", (await Service(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateTemplateInput("D", "a3-landscape"), false)).Error);
    }

    // ── The reusable library ────────────────────────────────────────────────────────────────────

    /// <summary>A library template has no event, so ownership is the only authority — and it is not
    /// visible to anyone else, including platform admins. A creator's private design library is not event
    /// content and nothing in the requirements asks staff to browse it.</summary>
    [Fact]
    public async Task A_library_template_is_owned_by_its_creator_alone()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);

        var mine = await svc.CreateAsync(f.OwnerId, null, new CertificateTemplateInput("My reusable design"), false);
        Assert.True(mine.Ok, mine.Error);
        Assert.Null(mine.Value!.EventId);

        Assert.Contains(mine.Value.Id, (await svc.ListLibraryAsync(f.OwnerId)).Value!.Select(t => t.Id));
        Assert.DoesNotContain(mine.Value.Id, (await svc.ListLibraryAsync(f.OutsiderId)).Value!.Select(t => t.Id));

        Assert.Equal("not_found", (await svc.GetAsync(f.OutsiderId, mine.Value.Id, false)).Error);
        // isAdmin is deliberately ignored for library templates.
        Assert.Equal("not_found", (await svc.GetAsync(f.OutsiderId, mine.Value.Id, isAdmin: true)).Error);
    }

    [Fact]
    public async Task A_library_template_does_not_appear_in_an_events_list()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);

        var library = await svc.CreateAsync(f.OwnerId, null, new CertificateTemplateInput("Library"), false);

        Assert.DoesNotContain(library.Value!.Id,
            (await svc.ListForEventAsync(f.OwnerId, f.EventId, false)).Value!.Select(t => t.Id));
    }

    // ── Artwork ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Uploaded_artwork_is_recorded_and_comes_back_as_a_fetchable_url()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var created = await Service(scope).CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        var view = await WithBackgroundAsync(scope, f.OwnerId, created.Value!.Id);

        Assert.NotNull(view.BackgroundStorageKey);
        // A key is not fetchable (D-302) — without a URL the editor cannot draw the design.
        Assert.NotNull(view.BackgroundUrl);
        Assert.Equal(3508, view.BackgroundWidthPx);
        Assert.Equal(2480, view.BackgroundHeightPx);
    }

    /// <summary>The prefix pairing. Without it, "use the image at this key" is a primitive for pulling any
    /// stored object on the platform into a document sent to hundreds of people.</summary>
    [Fact]
    public async Task Artwork_from_outside_the_templates_own_prefix_is_refused()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var mine = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("Mine"), false);

        foreach (var foreignKey in new[]
                 {
                     $"events/{f.OtherEventId}/certificates/templates/x/v1/background.png",
                     $"users/{f.OutsiderId}/avatar/portrait.png",
                     "orgs/x/verification/government-id.png",
                 })
        {
            var result = await svc.SetBackgroundAsync(f.OwnerId, mine.Value!.Id,
                new CertificateBackgroundInput(foreignKey, "image/png", 100, 100), false);
            Assert.Equal("invalid_storage_key", result.Error);
        }
    }

    /// <summary>A key that was presigned but never written must not be recorded: the template would
    /// render as a blank page and nobody would find out until generation.</summary>
    [Fact]
    public async Task Artwork_that_was_never_uploaded_is_refused()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        var presigned = await svc.PresignBackgroundAsync(f.OwnerId, created.Value!.Id, "image/png", 1024, false);

        // Presigned, deliberately not PUT.
        var result = await svc.SetBackgroundAsync(f.OwnerId, created.Value.Id,
            new CertificateBackgroundInput(presigned.Value!.Key, "image/png", 100, 100), false);

        Assert.Equal("upload_not_found", result.Error);
    }

    [Fact]
    public async Task An_undecodable_content_type_is_refused_at_presign_and_at_attach()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        Assert.Equal("invalid_content_type",
            (await svc.PresignBackgroundAsync(f.OwnerId, created.Value!.Id, "image/gif", 1024, false)).Error);
        Assert.Equal("invalid_content_type", (await svc.SetBackgroundAsync(f.OwnerId, created.Value.Id,
            new CertificateBackgroundInput("k", "application/pdf", 10, 10), false)).Error);
    }

    /// <summary>A template with no artwork cannot be marked ready: the certificate would be text on a
    /// blank page, which is never what an image-first design meant.</summary>
    [Fact]
    public async Task A_template_without_artwork_cannot_be_marked_ready()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        Assert.Equal("background_required", (await svc.UpdateAsync(f.OwnerId, created.Value!.Id,
            new CertificateTemplateInput(Status: "ready"), false)).Error);

        await WithBackgroundAsync(scope, f.OwnerId, created.Value.Id);

        var ready = await svc.UpdateAsync(f.OwnerId, created.Value.Id, new CertificateTemplateInput(Status: "ready"), false);
        Assert.True(ready.Ok, ready.Error);
        Assert.Equal("ready", ready.Value!.Status);
    }

    // ── Fields ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fields_are_saved_and_returned_in_paint_order()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        var result = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id, [
            new CertificateFieldInput("qrcode", null, "Verify", null, 80, 70, 12, 17, ZOrder: 3),
            NameField() with { ZOrder = 1 },
            new CertificateFieldInput("text", null, "Heading", "Certificate of Participation",
                10, 15, 80, 10, ZOrder: 2, FontSizePt: 40, FontWeight: "bold", HorizontalAlignment: "center"),
        ], false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal([1, 2, 3], result.Value!.Fields.Select(x => x.ZOrder));
        Assert.Equal(["dynamicfield", "text", "qrcode"], result.Value.Fields.Select(x => x.Kind));
    }

    /// <summary>Field keys are the organiser's vocabulary. A fixed enum would mean a code change for every
    /// new spreadsheet column.</summary>
    [Theory]
    [InlineData("participant_name")]
    [InlineData("employee_grade")]
    [InlineData("पद")]
    public async Task An_arbitrary_field_key_is_accepted(string key)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        var result = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id, [NameField(key)], false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(key, result.Value!.Fields[0].FieldKey);
    }

    [Fact]
    public async Task A_dynamic_field_without_a_key_is_refused()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        var result = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id,
            [new CertificateFieldInput("dynamicfield", null, "Nothing", null, 10, 10, 20, 5)], false);

        Assert.Equal("field_key_required", result.Error);
    }

    /// <summary>Cover-and-replace: the honest model for "editing" text baked into an image. The intent and
    /// the sampled ground both have to survive, because without the ground the patch is transparent and
    /// the original shows through whatever is written over it.</summary>
    [Fact]
    public async Task A_masking_field_records_its_intent_and_its_ground()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        var result = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id, [
            new CertificateFieldInput("text", null, "Replacement", "Certificate of Excellence",
                9, 18, 82, 9, IsMasking: true, BackgroundColor: "#FFFFFF"),
        ], false);

        Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.Fields[0].IsMasking);
        Assert.Equal("#FFFFFF", result.Value.Fields[0].BackgroundColor);
    }

    [Theory]
    [InlineData(900, 10, "field_out_of_bounds")]
    [InlineData(10, 900, "field_out_of_bounds")]
    public async Task Coordinates_far_outside_the_page_are_a_unit_mistake(double x, double y, string expected)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        var result = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id,
            [NameField() with { X = x, Y = y }], false);

        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task A_malformed_colour_is_refused_rather_than_silently_defaulted()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        // A typo must surface in the editor, not as an unexpectedly black heading on every certificate.
        Assert.Equal("invalid_colour", (await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id,
            [NameField() with { Color = "navy" }], false)).Error);
    }

    [Fact]
    public async Task Replacing_fields_replaces_the_whole_set()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id, [NameField(), NameField("team_name")], false);
        var after = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value.Id, [NameField()], false);

        Assert.Single(after.Value!.Fields);
        Assert.Equal("participant_name", after.Value.Fields[0].FieldKey);
    }

    // ── Versioning ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Before anything is issued, a template is still being drafted — bumping on every edit would
    /// make the version number meaningless.</summary>
    [Fact]
    public async Task Editing_a_template_with_nothing_issued_does_not_bump_the_version()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);

        await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id, [NameField()], false);
        var again = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value.Id, [NameField(), NameField("role")], false);

        Assert.Equal(1, again.Value!.Version);
        Assert.False(again.Value.HasIssuedCertificates);
    }

    /// <summary>The property template versioning exists for: once certificates have been issued, a
    /// render-affecting edit produces a NEW version, so what those certificates claim to look like cannot
    /// be rewritten underneath them.</summary>
    [Fact]
    public async Task Editing_a_template_that_has_issued_certificates_bumps_the_version()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);
        await svc.ReplaceFieldsAsync(f.OwnerId, created.Value!.Id, [NameField()], false);

        var recipient = new CertificateRecipient { EventId = f.EventId, FullName = "Ananya Rao" };
        db.CertificateRecipients.Add(recipient);
        db.IssuedCertificates.Add(new IssuedCertificate
        {
            CertificateId = $"CERT-2026-{Random.Shared.Next(10000, 99999)}",
            EventId = f.EventId,
            TemplateId = created.Value.Id,
            TemplateVersion = 1,
            RecipientId = recipient.Id,
        });
        await db.SaveChangesAsync();

        var edited = await svc.ReplaceFieldsAsync(f.OwnerId, created.Value.Id,
            [NameField(), NameField("achievement")], false);

        Assert.Equal(2, edited.Value!.Version);
        Assert.True(edited.Value.HasIssuedCertificates);

        // The already-issued certificate still points at version 1 — untouched.
        Assert.Equal(1, await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.TemplateId == created.Value.Id).Select(c => c.TemplateVersion).FirstAsync());
    }

    /// <summary>New artwork lands on a new version path, so the bytes an issued certificate was rendered
    /// from are never overwritten.</summary>
    [Fact]
    public async Task Replacement_artwork_is_presigned_under_the_next_version()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);
        var first = await WithBackgroundAsync(scope, f.OwnerId, created.Value!.Id);
        Assert.Contains("/v1/", first.BackgroundStorageKey!, StringComparison.Ordinal);

        var recipient = new CertificateRecipient { EventId = f.EventId, FullName = "Ananya Rao" };
        db.CertificateRecipients.Add(recipient);
        db.IssuedCertificates.Add(new IssuedCertificate
        {
            CertificateId = $"CERT-2026-{Random.Shared.Next(10000, 99999)}",
            EventId = f.EventId, TemplateId = created.Value.Id, TemplateVersion = 1, RecipientId = recipient.Id,
        });
        await db.SaveChangesAsync();

        var presigned = await svc.PresignBackgroundAsync(f.OwnerId, created.Value.Id, "image/png", 1024, false);

        Assert.Contains("/v2/", presigned.Value!.Key, StringComparison.Ordinal);
        Assert.NotEqual(first.BackgroundStorageKey, presigned.Value.Key);
    }

    // ── Archiving ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Archived, never hard-deleted: certificates record the template id and version, and a
    /// document someone holds must not lose its provenance because a list was tidied.</summary>
    [Fact]
    public async Task Archiving_hides_a_template_without_destroying_it()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var created = await svc.CreateAsync(f.OwnerId, f.EventId, new CertificateTemplateInput("D"), false);
        Assert.True((await svc.ArchiveAsync(f.OwnerId, created.Value!.Id, false)).Ok);

        Assert.DoesNotContain(created.Value.Id,
            (await svc.ListForEventAsync(f.OwnerId, f.EventId, false)).Value!.Select(t => t.Id));
        Assert.True(await db.CertificateTemplates.AsNoTracking().AnyAsync(t => t.Id == created.Value.Id));
    }
}
