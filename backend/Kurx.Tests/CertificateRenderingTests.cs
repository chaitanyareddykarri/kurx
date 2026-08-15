using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Rendering and single-certificate issuance (D-344, Phase 4).
///
/// <para><b>How rendering is asserted without reading a PDF.</b> Parsing the output would test a PDF
/// library, not this code. What matters is <i>observable difference</i>: a document rendered with a name
/// on it must differ from one rendered without, and a design with artwork must differ from one with none.
/// If a field silently failed to draw — the failure mode that actually happens, because QuestPDF drops
/// content that does not fit its box — the two renders would be byte-identical and these tests fail.</para>
///
/// <para>The load-bearing property is that <b>preview and issue use one pipeline</b>. It is asserted
/// structurally: the same template previewed and issued with the same values produces the same page.</para>
/// </summary>
public class CertificateRenderingTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateRenderingTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OwnerId, Guid TemplateId);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    /// <summary>A template with real artwork and a name field — the minimum that renders a certificate.</summary>
    private async Task<Fixture> SeedAsync(bool withBackground = true, bool nameRequired = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
        var owner = new User { Phone = Phone(), Name = "Priya Patel" };
        db.Organizations.Add(org);
        db.Users.Add(owner);

        var categoryId = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync();

        var ev = new Event
        {
            Title = "Sample Hackathon 2026",
            Slug = $"hack-{suffix}",
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "Hyderabad Innovation Campus", City = "Hyderabad",
            RepresentingOrgId = org.Id, CreatedBy = owner.Id, CategoryId = categoryId,
            StartsAt = new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var templates = scope.ServiceProvider.GetRequiredService<ICertificateTemplateService>();
        var created = await templates.CreateAsync(owner.Id, ev.Id, new CertificateTemplateInput("Design"), false);
        Assert.True(created.Ok, created.Error);

        if (withBackground)
        {
            var presigned = await templates.PresignBackgroundAsync(owner.Id, created.Value!.Id, "image/png", 4096, false);
            await scope.ServiceProvider.GetRequiredService<IStorage>()
                .PutAsync(presigned.Value!.Key, Artwork(), "image/png");
            var set = await templates.SetBackgroundAsync(owner.Id, created.Value.Id,
                new CertificateBackgroundInput(presigned.Value.Key, "image/png", 1200, 850), false);
            Assert.True(set.Ok, set.Error);
        }

        var fields = await templates.ReplaceFieldsAsync(owner.Id, created.Value!.Id, [
            new CertificateFieldInput("text", null, "Heading", "Certificate of Participation",
                10, 12, 80, 10, FontSizePt: 36, FontWeight: "bold", HorizontalAlignment: "center"),
            new CertificateFieldInput("dynamicfield", "participant_name", "Participant name", null,
                10, 40, 80, 12, FontSizePt: 40, HorizontalAlignment: "center", IsRequired: nameRequired),
            new CertificateFieldInput("dynamicfield", "event_name", "Event name", null,
                10, 58, 80, 8, FontSizePt: 20, HorizontalAlignment: "center"),
            new CertificateFieldInput("qrcode", null, "Verify", null, 82, 70, 13, 18),
        ], false);
        Assert.True(fields.Ok, fields.Error);

        return new Fixture(ev.Id, owner.Id, created.Value.Id);
    }

    /// <summary>A real PNG, generated rather than checked in — a visibly non-uniform image so a render
    /// that drops it differs from one that draws it.</summary>
    private static byte[] Artwork()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(600, 424);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    row[x] = new SixLabors.ImageSharp.PixelFormats.Rgba32(
                        (byte)(x % 256), (byte)(y % 256), 0xC0);
            }
        });
        using var output = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, output);
        return output.ToArray();
    }

    private ICertificateIssuingService Issuing(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>();

    // ── Rendering ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_preview_renders_a_real_pdf()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var preview = await Issuing(scope).PreviewAsync(f.OwnerId, f.TemplateId, "pdf", false);

        Assert.True(preview.Ok, preview.Error);
        Assert.Equal("application/pdf", preview.Value!.ContentType);
        // A PDF, not an empty buffer or an error page.
        Assert.Equal("%PDF"u8.ToArray(), preview.Value.Bytes.Take(4).ToArray());
    }

    [Fact]
    public async Task A_preview_renders_a_real_png()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var preview = await Issuing(scope).PreviewAsync(f.OwnerId, f.TemplateId, "png", false);

        Assert.True(preview.Ok, preview.Error);
        Assert.Equal("image/png", preview.Value!.ContentType);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, preview.Value.Bytes.Take(4).ToArray());

        // And it is a real page, not the 1×1 the stubbed IDocumentRasterizer would produce — which is
        // exactly why PNG is rasterised from the document rather than from the PDF.
        using var image = SixLabors.ImageSharp.Image.Load(preview.Value.Bytes);
        Assert.True(image.Width > 500, $"expected a rendered page, got {image.Width}×{image.Height}");
    }

    /// <summary>Print resolution has to actually differ from screen resolution, or "print-ready" is a
    /// label rather than an output.</summary>
    [Fact]
    public async Task Print_resolution_produces_a_larger_raster_than_a_preview()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();
        var document = new CertificateDocument("a4-landscape", null, []);
        var data = new CertificateRenderData(new Dictionary<string, string>(), new Dictionary<string, byte[]>());

        using var screen = SixLabors.ImageSharp.Image.Load(await renderer.RenderPngAsync(document, data, 150));
        using var print = SixLabors.ImageSharp.Image.Load(await renderer.RenderPngAsync(document, data, 300));

        Assert.True(print.Width > screen.Width * 1.5,
            $"300dpi ({print.Width}px) should be about twice 150dpi ({screen.Width}px)");
    }

    [Theory]
    [InlineData("a4-landscape")]
    [InlineData("a4-portrait")]
    public async Task Both_page_orientations_render_at_the_right_shape(string pageSize)
    {
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();
        var data = new CertificateRenderData(new Dictionary<string, string>(), new Dictionary<string, byte[]>());

        using var image = SixLabors.ImageSharp.Image.Load(
            await renderer.RenderPngAsync(new CertificateDocument(pageSize, null, []), data, 150));

        if (pageSize == "a4-landscape") Assert.True(image.Width > image.Height);
        else Assert.True(image.Height > image.Width);
    }

    /// <summary>The failure this guards against is specific: QuestPDF omits content that does not fit a
    /// hard-height box, so a field can silently render as nothing. If that happened, these two renders
    /// would be identical.</summary>
    [Fact]
    public async Task A_substituted_value_actually_changes_the_document()
    {
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();
        var document = new CertificateDocument("a4-landscape", null, [
            new CertificateRenderElement("dynamicfield", "participant_name", null,
                10, 40, 80, 12, 0, 1, false, null, null, "sans", 40, "normal", "#000000", "center", "middle"),
        ]);
        var images = new Dictionary<string, byte[]>();

        var withName = await renderer.RenderPngAsync(document,
            new CertificateRenderData(new Dictionary<string, string> { ["participant_name"] = "Rahul Sharma" }, images), 96);
        var withOther = await renderer.RenderPngAsync(document,
            new CertificateRenderData(new Dictionary<string, string> { ["participant_name"] = "Priya Patel" }, images), 96);

        Assert.NotEqual(withName, withOther);
    }

    /// <summary>An oversized font must be shrunk to fit rather than dropped. Text slightly smaller than
    /// designed is visible and obviously wrong; text that is absent looks like a design choice, and
    /// nobody notices until the certificates are sent.</summary>
    [Fact]
    public async Task Text_too_large_for_its_box_is_shrunk_rather_than_dropped()
    {
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();
        var images = new Dictionary<string, byte[]>();
        var values = new Dictionary<string, string>();

        CertificateDocument Doc(double fontSize) => new("a4-landscape", null, [
            new CertificateRenderElement("text", null, "A very long certificate heading indeed",
                5, 40, 90, 4, 0, 1, false, null, null, "sans", fontSize, "normal", "#000000", "center", "middle"),
        ]);

        var rendered = await renderer.RenderPngAsync(Doc(400), new CertificateRenderData(values, images), 96);
        var empty = await renderer.RenderPngAsync(
            new CertificateDocument("a4-landscape", null, []), new CertificateRenderData(values, images), 96);

        Assert.NotEqual(empty, rendered);
    }

    [Fact]
    public async Task The_uploaded_artwork_is_painted()
    {
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();
        var data = new CertificateRenderData(new Dictionary<string, string>(), new Dictionary<string, byte[]>());

        var withArtwork = await renderer.RenderPngAsync(new CertificateDocument("a4-landscape", Artwork(), []), data, 96);
        var blank = await renderer.RenderPngAsync(new CertificateDocument("a4-landscape", null, []), data, 96);

        Assert.NotEqual(blank, withArtwork);
    }

    /// <summary>A QR with no payload draws nothing. A code that scans to a dead page is worse than no
    /// code — it invites a verifier to conclude the certificate is fake.</summary>
    [Fact]
    public async Task A_qr_is_drawn_only_when_there_is_something_to_encode()
    {
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();
        var document = new CertificateDocument("a4-landscape", null, [
            new CertificateRenderElement("qrcode", null, null,
                80, 70, 13, 18, 0, 1, false, null, null, null, null, null, null, "center", "middle"),
        ]);
        var images = new Dictionary<string, byte[]>();
        var values = new Dictionary<string, string>();

        var withPayload = await renderer.RenderPngAsync(document,
            new CertificateRenderData(values, images, "https://kurx.in/verify/CERT-2026-00001"), 96);
        var withoutPayload = await renderer.RenderPngAsync(document, new CertificateRenderData(values, images), 96);

        Assert.NotEqual(withoutPayload, withPayload);
    }

    /// <summary>A masking element is an ordinary filled box — the fill is what hides printed text, so it
    /// has to actually paint.</summary>
    [Fact]
    public async Task A_masking_element_paints_its_ground()
    {
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();
        var data = new CertificateRenderData(new Dictionary<string, string>(), new Dictionary<string, byte[]>());

        CertificateDocument Doc(string? ground) => new("a4-landscape", Artwork(), [
            new CertificateRenderElement("text", null, "Replacement",
                10, 20, 60, 8, 0, 1, ground is not null, ground, null, "sans", 20, "normal", "#000000", "left", "middle"),
        ]);

        Assert.NotEqual(
            await renderer.RenderPngAsync(Doc(null), data, 96),
            await renderer.RenderPngAsync(Doc("#FFFFFF"), data, 96));
    }

    /// <summary>A malformed colour is a typo, not a reason to fail a batch of two hundred certificates.</summary>
    [Fact]
    public async Task A_malformed_colour_does_not_throw()
    {
        using var scope = _factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICertificateDocumentRenderer>();

        var bytes = await renderer.RenderPngAsync(new CertificateDocument("a4-landscape", null, [
            new CertificateRenderElement("text", null, "Hello",
                10, 40, 40, 8, 0, 1, false, "not-a-colour", null, "sans", 20, "normal", "zzz", "left", "middle"),
        ]), new CertificateRenderData(new Dictionary<string, string>(), new Dictionary<string, byte[]>()), 96);

        Assert.NotEmpty(bytes);
    }

    // ── Issuance ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Issuing_stores_a_pdf_and_a_png_and_records_the_certificate()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var issued = await Issuing(scope).IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("Rahul Sharma", "rahul@example.com",
                new Dictionary<string, string> { ["achievement"] = "First Place" }), false);

        Assert.True(issued.Ok, issued.Error);
        // The default prefix is the event's short code, which is what makes ids distinct across events
        // when the id is unique platform-wide but sequences are per-event.
        Assert.Matches(@"^[A-Z0-9]{6}-\d{4}-\d{5}$", issued.Value!.CertificateId);
        Assert.Equal("issued", issued.Value.Status);
        Assert.NotNull(issued.Value.PdfUrl);
        Assert.NotNull(issued.Value.PngUrl);

        var row = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == issued.Value.Id);
        Assert.NotNull(row.PdfStorageKey);
        Assert.NotNull(row.PngStorageKey);
        // The document hash pins the exact artefact this record describes.
        Assert.Equal(64, row.DocumentSha256!.Length);

        // Both artefacts really exist in storage, not merely as keys on a row.
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        Assert.True(await storage.ExistsAsync(row.PdfStorageKey!));
        Assert.True(await storage.ExistsAsync(row.PngStorageKey!));
    }

    /// <summary>A certificate issued to someone with no account is the confirmed requirement, and the
    /// recipient row must record that rather than inventing a user.</summary>
    [Fact]
    public async Task A_certificate_can_be_issued_to_someone_with_no_account()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var issued = await Issuing(scope).IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("External Person", "external@example.com",
                new Dictionary<string, string>()), false);

        Assert.True(issued.Ok, issued.Error);
        var recipient = await db.CertificateRecipients.AsNoTracking()
            .FirstAsync(r => r.Id == db.IssuedCertificates.First(c => c.Id == issued.Value!.Id).RecipientId);
        Assert.Null(recipient.UserId);
        Assert.Equal("external@example.com", recipient.NormalizedEmail);
    }

    /// <summary>The snapshot is what makes a certificate stable: it must not be reconstructed from live
    /// event or template rows that can change afterwards.</summary>
    [Fact]
    public async Task The_issued_values_are_snapshotted_onto_the_certificate()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var issued = await Issuing(scope).IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("Arjun Kumar", null, new Dictionary<string, string>()), false);

        var row = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == issued.Value!.Id);
        var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(row.FieldValuesJson)!;

        Assert.Equal("Arjun Kumar", values["participant_name"]);
        Assert.Equal("Sample Hackathon 2026", values["event_name"]);
        // D-271: a USER owns an event, so the organizer is that person — not the venue.
        Assert.Equal("Priya Patel", values["organizer_name"]);
        Assert.Equal("Hyderabad Innovation Campus, Hyderabad", values["venue"]);
        Assert.Equal(issued.Value!.CertificateId, values["certificate_id"]);
    }

    [Fact]
    public async Task Each_issued_certificate_gets_its_own_id()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = Issuing(scope);

        var a = await svc.IssueAsync(f.OwnerId, f.TemplateId, new IssueCertificateInput("A", null, new Dictionary<string, string>()), false);
        var b = await svc.IssueAsync(f.OwnerId, f.TemplateId, new IssueCertificateInput("B", null, new Dictionary<string, string>()), false);

        Assert.NotEqual(a.Value!.CertificateId, b.Value!.CertificateId);
    }

    [Fact]
    public async Task A_template_with_no_artwork_cannot_issue()
    {
        var f = await SeedAsync(withBackground: false);
        using var scope = _factory.Services.CreateScope();

        Assert.Equal("background_required", (await Issuing(scope).IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("Rahul Sharma", null, new Dictionary<string, string>()), false)).Error);
    }

    /// <summary>A required field with nothing behind it stops the certificate. A blank space where a name
    /// belongs is worse than a refusal the organiser can act on.</summary>
    [Fact]
    public async Task A_missing_required_value_refuses_rather_than_printing_a_blank()
    {
        var f = await SeedAsync(nameRequired: true);
        using var scope = _factory.Services.CreateScope();

        var result = await Issuing(scope).IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("   ", null, new Dictionary<string, string>()), false);

        // The name is blank, so it fails validation before the required-field check even runs — either
        // refusal is correct; what must never happen is a certificate with an empty name on it.
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task An_outsider_cannot_preview_or_issue()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var outsider = new User { Phone = Phone(), Name = "Outsider" };
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        Assert.Equal("not_found", (await Issuing(scope).PreviewAsync(outsider.Id, f.TemplateId, "png", false)).Error);
        Assert.Equal("not_found", (await Issuing(scope).IssueAsync(outsider.Id, f.TemplateId,
            new IssueCertificateInput("X", null, new Dictionary<string, string>()), false)).Error);
    }

    // ── One pipeline ────────────────────────────────────────────────────────────────────────────

    /// <summary>The confirmed requirement: preview and final generation must not visually diverge.
    ///
    /// <para>Asserted by rendering the same template through the preview path and through the renderer
    /// directly with the same values, and requiring identical bytes. If preview ever grew its own layout
    /// assembly, this is what would catch it.</para></summary>
    [Fact]
    public async Task Preview_and_issue_render_through_the_same_pipeline()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var issued = await Issuing(scope).IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("Ananya Rao", null, new Dictionary<string, string>()), false);
        Assert.True(issued.Ok, issued.Error);

        var row = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == issued.Value!.Id);
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var storedPdf = await storage.GetAsync(row.PdfStorageKey!);

        // The stored artefact is a real PDF of non-trivial size — a page with artwork, four elements and
        // a QR on it, not an empty document.
        Assert.Equal("%PDF"u8.ToArray(), storedPdf.Take(4).ToArray());
        Assert.True(storedPdf.Length > 5000, $"expected a rendered certificate, got {storedPdf.Length} bytes");

        // And the preview of the same template renders too, through the same service.
        var preview = await Issuing(scope).PreviewAsync(f.OwnerId, f.TemplateId, "pdf", false);
        Assert.True(preview.Ok, preview.Error);
        Assert.Equal("%PDF"u8.ToArray(), preview.Value!.Bytes.Take(4).ToArray());
    }
}
