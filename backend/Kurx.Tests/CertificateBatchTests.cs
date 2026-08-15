using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Generating certificates for a whole participant list (D-344, Phase 7).
///
/// <para>Three properties carry this phase. <b>Nothing generates at scale without an approval</b> — the
/// gate is the only thing standing between a mistyped event name and two hundred certificates that have
/// already been emailed. <b>A retry finishes a run rather than duplicating it</b>, which is what makes the
/// job safe to replay after a crash. <b>One bad row does not cost the other four hundred.</b></para>
///
/// <para>The batch path is asserted to produce the SAME artefact as the single-issue path — signed, stored,
/// verifiable — because "bulk" must not quietly mean "a cheaper certificate".</para>
/// </summary>
public class CertificateBatchTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateBatchTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private sealed record Fixture(Guid EventId, Guid OwnerId, Guid OutsiderId, Guid TemplateId);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    private static byte[] Csv(string content) => Encoding.UTF8.GetBytes(content);

    private const string ThreeRows =
        "Name,Team\nRahul Sharma,ByteBuilders\nPriya Patel,CodeCrafters\nArjun Kumar,DataDivers\n";

    /// <summary>Real PNG bytes. The renderer decodes the artwork, so a placeholder four-byte array would
    /// fail at compose time rather than exercising the path a real batch takes.</summary>
    private static byte[] Artwork()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(600, 424);
        using var output = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, output);
        return output.ToArray();
    }

    private static readonly Dictionary<string, string> Mapping =
        new() { ["Name"] = "participant_name", ["Team"] = "team_name" };

    private ICertificateBatchService Batches(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateBatchService>();

    /// <summary>Seeds an event with a ready-to-render template: artwork uploaded through the real storage
    /// path, one name field placed on it.</summary>
    private async Task<Fixture> SeedAsync()
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
        var templates = s2.ServiceProvider.GetRequiredService<ICertificateTemplateService>();

        var created = await templates.CreateAsync(ownerId, eventId,
            new CertificateTemplateInput("Participation", "a4-landscape"), false);
        Assert.True(created.Ok, created.Error);
        var templateId = created.Value!.Id;

        var artwork = Artwork();
        var presigned = await templates.PresignBackgroundAsync(
            ownerId, templateId, "image/png", artwork.Length, false);
        await s2.ServiceProvider.GetRequiredService<IStorage>()
            .PutAsync(presigned.Value!.Key, artwork, "image/png");
        await templates.SetBackgroundAsync(ownerId, templateId,
            new CertificateBackgroundInput(presigned.Value.Key, "image/png", 3508, 2480), false);

        await templates.ReplaceFieldsAsync(ownerId, templateId, [
            new("dynamicfield", "participant_name", "Participant name", null, 10, 40, 80, 10,
                HorizontalAlignment: "center", FontSizePt: 32, Color: "#0F172A", IsRequired: true)
        ], false);

        return new Fixture(eventId, ownerId, outsiderId, templateId);
    }

    private async Task<Guid> CreateBatchAsync(
        IServiceScope scope, Fixture f, string csv = ThreeRows, Dictionary<string, string>? mapping = null)
    {
        var result = await Batches(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "participants.csv", Csv(csv),
                mapping ?? Mapping), false);
        Assert.True(result.Ok, result.Error);
        return result.Value!.Id;
    }

    /// <summary>Walks a batch to the point where the full run may legally start.</summary>
    private async Task<Guid> ApprovedBatchAsync(IServiceScope scope, Fixture f, string csv = ThreeRows)
    {
        var batchId = await CreateBatchAsync(scope, f, csv);
        Assert.True((await Batches(scope).GeneratePreviewAsync(f.OwnerId, batchId, false)).Ok);
        var approved = await Batches(scope).ApproveAsync(f.OwnerId, batchId, false);
        Assert.True(approved.Ok, approved.Error);
        return batchId;
    }

    // ── Creation ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_run_starts_from_a_participant_list()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Batches(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "participants.csv", Csv(ThreeRows), Mapping),
            false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(3, result.Value!.RowCount);
        Assert.Equal("mapping", result.Value.Status);
        Assert.Equal(0, result.Value.IssuedCount);
        Assert.Null(result.Value.ApprovedAt);
    }

    /// <summary>One recipient per row, carrying the row number in the organiser's coordinates so "row 47
    /// failed" points at something they can find in their own spreadsheet.</summary>
    [Fact]
    public async Task Every_row_becomes_a_recipient_that_remembers_its_row_number()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await CreateBatchAsync(scope, f);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var recipients = await db.CertificateRecipients.AsNoTracking()
            .Where(r => r.BatchId == batchId).OrderBy(r => r.SourceRowNumber).ToListAsync();

        Assert.Equal(3, recipients.Count);
        Assert.Equal([1, 2, 3], recipients.Select(r => r.SourceRowNumber!.Value));
        Assert.Equal("Rahul Sharma", recipients[0].FullName);
    }

    /// <summary>Refused before anything renders. A run that discovers this on row 180 has already produced
    /// 179 certificates it should not have.</summary>
    [Fact]
    public async Task A_required_field_with_no_column_behind_it_is_refused_up_front()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Batches(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "participants.csv", Csv(ThreeRows),
                new Dictionary<string, string> { ["Team"] = "team_name" }), false);

        Assert.False(result.Ok);
        Assert.Equal("participant_name_not_mapped", result.Error);
    }

    [Fact]
    public async Task A_row_with_a_blank_name_is_refused_before_anything_renders()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Batches(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "participants.csv",
                Csv("Name,Team\nRahul,ByteBuilders\n,CodeCrafters\n"), Mapping), false);

        Assert.False(result.Ok);
        Assert.Equal("rows_missing_participant_name", result.Error);
    }

    [Fact]
    public async Task A_template_from_another_event_cannot_be_used()
    {
        var f = await SeedAsync();
        var other = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var result = await Batches(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", other.TemplateId, "p.csv", Csv(ThreeRows), Mapping), false);

        Assert.False(result.Ok);
        Assert.Equal("template_not_found", result.Error);
    }

    [Fact]
    public async Task An_outsider_cannot_create_or_read_a_run()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var created = await Batches(scope).CreateAsync(f.OutsiderId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "p.csv", Csv(ThreeRows), Mapping), false);
        Assert.False(created.Ok);

        var batchId = await CreateBatchAsync(scope, f);
        var read = await Batches(scope).GetAsync(f.OutsiderId, batchId, false);

        // D-018: not-found rather than forbidden — a 403 would confirm it exists.
        Assert.False(read.Ok);
        Assert.Equal("not_found", read.Error);
    }

    // ── The approval gate ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_preview_renders_real_rows_from_the_file()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await CreateBatchAsync(scope, f);

        var result = await Batches(scope).GeneratePreviewAsync(f.OwnerId, batchId, false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("previewready", result.Value!.Status);
        Assert.Equal(3, result.Value.PreviewCount);
        Assert.NotEmpty(result.Value.PreviewUrls);
        // Still nothing issued: a preview is not a certificate.
        Assert.Equal(0, result.Value.IssuedCount);
    }

    /// <summary>The gate. Approving something nobody has looked at defeats the entire point of it.</summary>
    [Fact]
    public async Task A_run_cannot_be_approved_before_it_has_been_previewed()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await CreateBatchAsync(scope, f);

        var result = await Batches(scope).ApproveAsync(f.OwnerId, batchId, false);

        Assert.False(result.Ok);
        Assert.Equal("preview_required", result.Error);
    }

    /// <summary>The gate again, from the other side: the job itself refuses to generate anything for a
    /// batch nobody approved. A queued job outlives the request that queued it.</summary>
    [Fact]
    public async Task An_unapproved_run_generates_nothing_even_if_the_job_fires()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await CreateBatchAsync(scope, f);

        var result = await Batches(scope).RunAsync(batchId);

        Assert.Equal(0, result.Issued);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(0, await db.IssuedCertificates.CountAsync(c => c.BatchId == batchId));
    }

    [Fact]
    public async Task Approval_pins_the_template_version()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var batch = await db.CertificateBatches.AsNoTracking().FirstAsync(b => b.Id == batchId);
        var template = await db.CertificateTemplates.AsNoTracking().FirstAsync(t => t.Id == f.TemplateId);

        Assert.Equal(template.Version, batch.TemplateVersion);
        Assert.NotNull(batch.ApprovedAt);
        Assert.Equal(CertificateBatchStatus.Approved, batch.Status);
    }

    // ── The run ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_approved_run_issues_one_certificate_per_row()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);

        var result = await Batches(scope).RunAsync(batchId);

        Assert.Equal(3, result.Issued);
        Assert.Equal(0, result.Failed);

        var view = await Batches(scope).GetAsync(f.OwnerId, batchId, false);
        Assert.Equal("completed", view.Value!.Status);
        Assert.Equal(3, view.Value.IssuedCount);
        Assert.Empty(view.Value.FailedRows);
    }

    /// <summary>A certificate produced in bulk is the same artefact as one produced singly: allocated id,
    /// stored PDF and PNG, a signature, and a hash pinning the exact bytes.</summary>
    [Fact]
    public async Task Batch_certificates_are_real_certificates()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        await Batches(scope).RunAsync(batchId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificates = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batchId).ToListAsync();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        Assert.Equal(3, certificates.Count);
        foreach (var certificate in certificates)
        {
            Assert.False(string.IsNullOrWhiteSpace(certificate.CertificateId));
            Assert.NotNull(certificate.Signature);
            Assert.NotNull(certificate.SignatureKeyId);
            Assert.NotNull(certificate.DocumentSha256);
            Assert.True(await storage.ExistsAsync(certificate.PdfStorageKey!));
            Assert.True(await storage.ExistsAsync(certificate.PngStorageKey!));
        }

        // Platform-wide unique, as they must be — verification is by this value alone.
        Assert.Equal(3, certificates.Select(c => c.CertificateId).Distinct().Count());
    }

    /// <summary>Every certificate must verify. A batch that produces unverifiable certificates has
    /// produced nothing of value.</summary>
    [Fact]
    public async Task Every_certificate_from_a_run_verifies()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        await Batches(scope).RunAsync(batchId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var verification = scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>();

        foreach (var id in await db.IssuedCertificates.AsNoTracking()
                     .Where(c => c.BatchId == batchId).Select(c => c.CertificateId).ToListAsync())
        {
            var verdict = await verification.VerifyAsync(id);
            Assert.Equal(CertificateVerificationOutcome.Valid, verdict.Outcome);
        }
    }

    /// <summary>The property that makes the job safe to replay: a retry finishes a run rather than
    /// duplicating the half that already worked.</summary>
    [Fact]
    public async Task Running_a_finished_batch_again_issues_nothing_more()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);

        var first = await Batches(scope).RunAsync(batchId);
        var second = await Batches(scope).RunAsync(batchId);

        Assert.Equal(3, first.Issued);
        Assert.Equal(0, second.Issued);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(3, await db.IssuedCertificates.CountAsync(c => c.BatchId == batchId));
    }

    /// <summary>Resumption, not just deduplication: a run interrupted after some rows finishes the rest on
    /// retry rather than starting over or refusing.</summary>
    [Fact]
    public async Task A_run_interrupted_partway_is_finished_by_the_retry()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Simulate a crash after the first row: run fully, then delete two certificates and reopen the
        // batch, leaving exactly the state an interrupted worker would.
        await Batches(scope).RunAsync(batchId);
        var survivors = await db.IssuedCertificates
            .Where(c => c.BatchId == batchId).OrderBy(c => c.IssuedAt).Skip(1).ToListAsync();
        db.IssuedCertificates.RemoveRange(survivors);
        var batch = await db.CertificateBatches.FirstAsync(b => b.Id == batchId);
        batch.Status = CertificateBatchStatus.Generating;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var resumed = await Batches(scope).RunAsync(batchId);

        Assert.Equal(2, resumed.Issued);   // the two that were lost
        Assert.Equal(1, resumed.Skipped);  // the one that survived
        Assert.Equal(3, await db.IssuedCertificates.CountAsync(c => c.BatchId == batchId));
    }

    /// <summary>The database, not the loop, is what makes a retry safe — a skip-list read at the top of a
    /// loop stops being true the moment two workers pick up the same job.</summary>
    [Fact]
    public async Task The_database_refuses_a_second_certificate_for_the_same_row()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        await Batches(scope).RunAsync(batchId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var existing = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.BatchId == batchId);

        db.IssuedCertificates.Add(new IssuedCertificate
        {
            CertificateId = "DUPLICATE-" + Guid.NewGuid().ToString("N")[..8],
            EventId = existing.EventId,
            TemplateId = existing.TemplateId,
            TemplateVersion = existing.TemplateVersion,
            BatchId = batchId,
            RecipientId = existing.RecipientId,
            Status = IssuedCertificateStatus.Issued,
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
    }

    /// <summary>Two certificates for one recipient are fine when neither came from a batch — the index is
    /// filtered for exactly that reason.</summary>
    [Fact]
    public async Task Hand_issued_certificates_are_not_constrained_by_the_batch_index()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var issuing = scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>();

        var first = await issuing.IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("Rahul Sharma", null, new Dictionary<string, string>()), false);
        var second = await issuing.IssueAsync(f.OwnerId, f.TemplateId,
            new IssueCertificateInput("Rahul Sharma", null, new Dictionary<string, string>()), false);

        Assert.True(first.Ok, first.Error);
        Assert.True(second.Ok, second.Error);
        Assert.NotEqual(first.Value!.CertificateId, second.Value!.CertificateId);
    }

    // ── Partial failure ─────────────────────────────────────────────────────────────────────────

    /// <summary>One row that cannot be issued must not cost the other rows, and the run must not report
    /// itself complete when part of it did not happen.</summary>
    [Fact]
    public async Task A_row_that_cannot_be_issued_does_not_stop_the_others()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Remove the middle row's recipient, so row 2 has nothing to issue against.
        var orphan = await db.CertificateRecipients
            .FirstAsync(r => r.BatchId == batchId && r.SourceRowNumber == 2);
        db.CertificateRecipients.Remove(orphan);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Batches(scope).RunAsync(batchId);

        Assert.Equal(2, result.Issued);
        Assert.Equal(1, result.Failed);

        // Not "completed": reporting a partial run as complete is how nobody finds out someone never got
        // their certificate.
        var view = await Batches(scope).GetAsync(f.OwnerId, batchId, false);
        Assert.Equal("failed", view.Value!.Status);
        Assert.Equal(2, view.Value.IssuedCount);
    }

    // ── Cancellation ────────────────────────────────────────────────────────────────────────────

    /// <summary>Certificates already issued stay issued. They exist, they are signed, and some may already
    /// have been sent — pretending otherwise would leave the database disagreeing with reality.</summary>
    [Fact]
    public async Task Cancelling_a_run_does_not_unmake_the_certificates_it_already_produced()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        await Batches(scope).RunAsync(batchId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var batch = await db.CertificateBatches.FirstAsync(b => b.Id == batchId);
        batch.Status = CertificateBatchStatus.Generating;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var cancelled = await Batches(scope).CancelAsync(f.OwnerId, batchId, false);

        Assert.True(cancelled.Ok, cancelled.Error);
        Assert.Equal("cancelled", cancelled.Value!.Status);
        Assert.Equal(3, await db.IssuedCertificates.CountAsync(c => c.BatchId == batchId));
    }

    [Fact]
    public async Task A_cancelled_run_generates_nothing_when_the_job_fires()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        await Batches(scope).CancelAsync(f.OwnerId, batchId, false);

        var result = await Batches(scope).RunAsync(batchId);

        Assert.Equal(0, result.Issued);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(0, await db.IssuedCertificates.CountAsync(c => c.BatchId == batchId));
    }

    [Fact]
    public async Task A_completed_run_cannot_be_cancelled()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f);
        await Batches(scope).RunAsync(batchId);

        var result = await Batches(scope).CancelAsync(f.OwnerId, batchId, false);

        Assert.False(result.Ok);
        Assert.Equal("batch_not_cancellable", result.Error);
    }

    // ── Listing ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Runs_are_listed_newest_first_and_scoped_to_their_event()
    {
        var f = await SeedAsync();
        var other = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        await CreateBatchAsync(scope, f);
        await CreateBatchAsync(scope, other);

        var result = await Batches(scope).ListAsync(f.OwnerId, f.EventId, false);

        Assert.True(result.Ok, result.Error);
        Assert.Single(result.Value!);
        Assert.Equal(f.EventId, result.Value![0].EventId);
    }

    [Fact]
    public async Task Values_from_the_spreadsheet_reach_the_certificate_unchanged()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await ApprovedBatchAsync(scope, f,
            "Name,Team\nअनन्या राव,007\n");
        await Batches(scope).RunAsync(batchId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificate = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.BatchId == batchId);

        Assert.Contains("अनन्या राव", certificate.FieldValuesJson, StringComparison.Ordinal);
        // 007, not 7 — the whole point of the reader, asserted where it finally matters.
        Assert.Contains("\"007\"", certificate.FieldValuesJson, StringComparison.Ordinal);
    }
}
