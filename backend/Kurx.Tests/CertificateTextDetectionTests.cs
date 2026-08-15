using System.Text;
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
/// The OCR boundary, with no engine behind it (D-355, Phase 12).
///
/// <para>OCR is deferred by decision. What this phase ships is the seam and nothing else, so the property
/// under test is mostly <b>absence</b>: no engine runs, no regions are invented, and every path that
/// matters — placing fields, saving, previewing, approving, issuing — behaves exactly as it did before
/// this file existed.</para>
///
/// <para>The distinction the tests keep returning to is between <i>nothing looked</i> and <i>nothing
/// found</i>. The first is a statement about this deployment; the second would be a statement about the
/// design. A stub that reported an empty success would collapse them and quietly tell a creator their
/// certificate has no text on it.</para>
/// </summary>
public class CertificateTextDetectionTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateTextDetectionTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OwnerId, Guid OutsiderId, Guid TemplateId);

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

    private static CertificateFieldInput NameField(string key = "participant_name") =>
        new("dynamicfield", key, "Participant name", null, 10, 40, 80, 10,
            HorizontalAlignment: "center", FontSizePt: 32, Color: "#0F172A", IsRequired: true);

    /// <summary>An event with a template that has artwork on it — the state an editor is open in.</summary>
    private async Task<Fixture> SeedAsync(bool withArtwork = true)
    {
        Guid eventId, ownerId, outsiderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
            var owner = new User { Phone = Phone(), Name = "Creator " + suffix };
            var outsider = new User { Phone = Phone(), Name = "Outsider " + suffix };
            db.Organizations.Add(org);
            db.Users.AddRange(owner, outsider);

            var categoryId = await db.EventCategories.AsNoTracking()
                .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync();

            var ev = new Event
            {
                Title = "Hackathon " + suffix,
                Slug = "hackathon-" + suffix,
                ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
                Description = "d", VenueName = "v",
                RepresentingOrgId = org.Id, CreatedBy = owner.Id, CategoryId = categoryId,
                StartsAt = DateTime.UtcNow.AddDays(30), EndsAt = DateTime.UtcNow.AddDays(31),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            (eventId, ownerId, outsiderId) = (ev.Id, owner.Id, outsider.Id);
        }

        using var s2 = _factory.Services.CreateScope();
        var templates = Templates(s2);

        var created = await templates.CreateAsync(ownerId, eventId,
            new CertificateTemplateInput("Participation", "a4-landscape"), false);
        var templateId = created.Value!.Id;

        if (withArtwork)
        {
            var artwork = Artwork();
            var presigned = await templates.PresignBackgroundAsync(
                ownerId, templateId, "image/png", artwork.Length, false);
            await s2.ServiceProvider.GetRequiredService<IStorage>()
                .PutAsync(presigned.Value!.Key, artwork, "image/png");
            await templates.SetBackgroundAsync(ownerId, templateId,
                new CertificateBackgroundInput(presigned.Value.Key, "image/png", 3508, 2480), false);
        }

        return new Fixture(eventId, ownerId, outsiderId, templateId);
    }

    // ── The contract ────────────────────────────────────────────────────────────────────────────

    /// <summary>The seam is real: something is registered, and it is resolvable by the interface rather
    /// than by a concrete type.</summary>
    [Fact]
    public void A_text_detector_is_registered_behind_the_interface()
    {
        using var scope = _factory.Services.CreateScope();

        var detector = scope.ServiceProvider.GetRequiredService<ITextDetector>();

        Assert.NotNull(detector);
        Assert.IsAssignableFrom<ITextDetector>(detector);
    }

    /// <summary>No engine is configured, and the boundary says so without being asked to analyse
    /// anything — which is what lets a caller decide whether to offer a button before it has an image.</summary>
    [Fact]
    public void Capability_can_be_asked_without_submitting_an_image()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.False(scope.ServiceProvider.GetRequiredService<ITextDetector>().IsAvailable);
    }

    // ── The stub ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_stub_reports_unavailable_with_a_reason_and_no_regions()
    {
        var detector = new UnavailableTextDetector();

        var result = await detector.DetectAsync(Artwork(), "image/png");

        Assert.False(result.Available);
        Assert.Empty(result.Regions);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    /// <summary>"Nothing looked" and "nothing found" are different answers. A stub returning an empty
    /// SUCCESS would tell a creator their design has no text on it.</summary>
    [Fact]
    public async Task Unavailable_is_not_the_same_as_an_empty_success()
    {
        var stub = await new UnavailableTextDetector().DetectAsync(Artwork(), "image/png");
        var foundNothing = TextDetectionResult.Detected([]);

        Assert.NotEqual(foundNothing.Available, stub.Available);
        Assert.Empty(foundNothing.Regions);
        Assert.Empty(stub.Regions);
        Assert.Null(foundNothing.Reason);
        Assert.NotNull(stub.Reason);
    }

    /// <summary>Never fabricates. The absence of invented regions is the whole point of a stub that is
    /// honest about being one.</summary>
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("application/octet-stream")]
    public async Task The_stub_never_invents_a_region(string contentType)
    {
        var result = await new UnavailableTextDetector().DetectAsync(Artwork(), contentType);

        Assert.Empty(result.Regions);
    }

    /// <summary>An un-analysable design must not be able to break the editor showing it, so the stub
    /// tolerates anything it is handed rather than validating input it never reads.</summary>
    [Fact]
    public async Task The_stub_does_not_throw_on_junk_input()
    {
        var detector = new UnavailableTextDetector();

        Assert.False((await detector.DetectAsync([], "image/png")).Available);
        Assert.False((await detector.DetectAsync(Encoding.UTF8.GetBytes("not an image"), "")).Available);
    }

    // ── The editor's integration point ──────────────────────────────────────────────────────────

    /// <summary>Unavailable is an ordinary answer, not an error: the call succeeds and the editor renders
    /// a capability from it.</summary>
    [Fact]
    public async Task The_editor_gets_a_successful_unavailable_answer_rather_than_a_failure()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Templates(scope).DetectBackgroundTextAsync(f.OwnerId, f.TemplateId, false);

        Assert.True(result.Ok, result.Error);
        Assert.False(result.Value!.Available);
        Assert.Empty(result.Value.Regions);
        Assert.NotNull(result.Value.Reason);
    }

    /// <summary>A design with nothing on it yet is still not an error — the editor is open on it.</summary>
    [Fact]
    public async Task A_design_with_no_artwork_answers_unavailable_rather_than_failing()
    {
        var f = await SeedAsync(withArtwork: false);
        using var scope = _factory.Services.CreateScope();

        var result = await Templates(scope).DetectBackgroundTextAsync(f.OwnerId, f.TemplateId, false);

        Assert.True(result.Ok, result.Error);
        Assert.False(result.Value!.Available);
    }

    /// <summary>The extension point is behind the same authority as everything else on a template.
    /// D-018: not-found rather than forbidden.</summary>
    [Fact]
    public async Task An_outsider_cannot_use_the_detection_endpoint()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Templates(scope).DetectBackgroundTextAsync(f.OutsiderId, f.TemplateId, false);

        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }

    // ── Nothing else changed ────────────────────────────────────────────────────────────────────

    /// <summary>The requirement this phase is built around: fields are placed by hand and that path is
    /// untouched by OCR being absent.</summary>
    [Fact]
    public async Task Manual_field_placement_still_works_with_no_detector_behind_the_boundary()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var saved = await Templates(scope).ReplaceFieldsAsync(f.OwnerId, f.TemplateId, [
            NameField(),
            new("text", null, null, "Certificate of Participation", 10, 20, 80, 8,
                HorizontalAlignment: "center", FontSizePt: 40),
        ], false);

        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(2, saved.Value!.Fields.Count);
        Assert.Contains(saved.Value.Fields, x => x.FieldKey == "participant_name");
    }

    /// <summary>Asking for detection first must not disturb the editor's own save.</summary>
    [Fact]
    public async Task Placing_fields_after_a_failed_detection_behaves_identically()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        await Templates(scope).DetectBackgroundTextAsync(f.OwnerId, f.TemplateId, false);
        var saved = await Templates(scope).ReplaceFieldsAsync(f.OwnerId, f.TemplateId, [NameField()], false);

        Assert.True(saved.Ok, saved.Error);
        Assert.Single(saved.Value!.Fields);
    }

    /// <summary>Preview, issue, and verification all the way through — with no OCR anywhere in the
    /// chain, because none of them consult it.</summary>
    [Fact]
    public async Task Preview_and_issuance_are_unaffected_by_ocr_being_absent()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await Templates(scope).ReplaceFieldsAsync(f.OwnerId, f.TemplateId, [NameField()], false);

        var issuing = scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>();

        var preview = await issuing.PreviewAsync(f.OwnerId, f.TemplateId, "png", false);
        Assert.True(preview.Ok, preview.Error);
        Assert.NotEmpty(preview.Value!.Bytes);

        var issued = await issuing.IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("Rahul Sharma", null, new Dictionary<string, string>()), false);
        Assert.True(issued.Ok, issued.Error);

        var verdict = await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync(issued.Value!.CertificateId);
        Assert.Equal(CertificateVerificationOutcome.Valid, verdict.Outcome);
    }

    /// <summary>A whole run — the approve gate included — with no engine configured.</summary>
    [Fact]
    public async Task A_batch_can_be_previewed_approved_and_generated_with_no_detector()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await Templates(scope).ReplaceFieldsAsync(f.OwnerId, f.TemplateId, [NameField()], false);

        var batches = scope.ServiceProvider.GetRequiredService<ICertificateBatchService>();
        var created = await batches.CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "p.csv",
                Encoding.UTF8.GetBytes("Name\nRahul Sharma\nPriya Patel\n"),
                new Dictionary<string, string> { ["Name"] = "participant_name" }), false);
        Assert.True(created.Ok, created.Error);

        Assert.True((await batches.GeneratePreviewAsync(f.OwnerId, created.Value!.Id, false)).Ok);
        Assert.True((await batches.ApproveAsync(f.OwnerId, created.Value.Id, false)).Ok);
        var run = await batches.RunAsync(created.Value.Id);

        Assert.Equal(2, run.Issued);
        Assert.Equal(0, run.Failed);
    }

    // ── No engine was added ─────────────────────────────────────────────────────────────────────

    /// <summary>Phase 12 adds a boundary, not a dependency. This fails the moment somebody quietly
    /// references an OCR package, which is exactly when it should.</summary>
    [Theory]
    [InlineData("Tesseract")]
    [InlineData("TesseractOCR")]
    [InlineData("AWSSDK.Textract")]
    [InlineData("Google.Cloud.Vision")]
    [InlineData("Azure.AI.Vision")]
    [InlineData("Azure.AI.FormRecognizer")]
    [InlineData("IronOcr")]
    [InlineData("PaddleOCR")]
    public void No_ocr_engine_is_referenced_by_the_build(string package)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetName().Name ?? "")
            .ToList();

        Assert.DoesNotContain(loaded,
            name => name.StartsWith(package, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The project files are the authority on what was installed — an assembly can simply not be
    /// loaded yet.</summary>
    [Fact]
    public void No_ocr_package_is_referenced_by_any_project_file()
    {
        var root = RepoRoot();
        var suspects = new[]
        {
            "tesseract", "textract", "cloud.vision", "ai.vision", "formrecognizer",
            "ironocr", "paddleocr", "ocr.net",
        };

        var projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);

        // Asserted first, so a wrong root cannot make this test pass by scanning nothing — the failure
        // mode of a "nothing found" test is that it finds nothing because it looked nowhere.
        Assert.True(projects.Length >= 4, $"Expected to scan the solution's projects, found {projects.Length}.");

        foreach (var project in projects)
        {
            var text = File.ReadAllText(project);
            foreach (var suspect in suspects)
                Assert.DoesNotContain(suspect, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Walks up from this source file rather than from the test binary's directory, which moves
    /// under <c>-p:ArtifactsPath</c>.</summary>
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string path = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
        while (directory is not null && !directory.GetFiles("*.sln").Any())
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
}
