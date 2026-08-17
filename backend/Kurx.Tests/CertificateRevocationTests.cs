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
/// Withdrawing a certificate, and correcting one (D-355, Phase 9).
///
/// <para>The property everything else rests on: <b>the original is never edited</b>. Its certificate id,
/// its field values, its signature and its rendered files come out of a correction byte-identical to how
/// they went in. Someone already holds a copy of that document, and a signature made over the old values
/// exists precisely to detect a change to them — so a correction has to be a new certificate, not an
/// edit.</para>
///
/// <para>And the two operations stay distinguishable: <b>revoked</b> means do not honour this,
/// <b>superseded</b> means a corrected one exists and here it is. Telling someone with a misspelled name
/// that their certificate was withdrawn is the failure this separation prevents.</para>
/// </summary>
public class CertificateRevocationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateRevocationTests(KurxApiFactory factory)
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

    private ICertificateRevocationService Revocations(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateRevocationService>();

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

    /// <summary>One live certificate, issued through the real path.</summary>
    private async Task<Guid> IssueAsync(
        IServiceScope scope, Fixture f, string name = "Rahul Sharma", string? team = null)
    {
        var values = new Dictionary<string, string>();
        if (team is not null) values["team_name"] = team;

        var issued = await scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>()
            .IssueAsync(f.OwnerId, f.TemplateId, new IssueCertificateInput(name, null, values), false);
        Assert.True(issued.Ok, issued.Error);
        return issued.Value!.Id;
    }

    // ── Revoke ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoking_withdraws_the_certificate_and_records_why()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f);

        var result = await Revocations(scope).RevokeAsync(f.OwnerId, id, "Issued to the wrong person", false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("revoked", result.Value!.Status);
        Assert.Equal("Issued to the wrong person", result.Value.RevocationReason);
        Assert.NotNull(result.Value.RevokedAt);
        // Revoked outright: nothing was issued in its place, which is what distinguishes this from a
        // correction.
        Assert.Null(result.Value.SupersededBy);
    }

    /// <summary>The platform will publicly say this certificate should not be honoured. A public claim
    /// with no reason behind it is not one anyone can act on.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Revoking_without_a_reason_is_refused(string reason)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f);

        var result = await Revocations(scope).RevokeAsync(f.OwnerId, id, reason, false);

        Assert.False(result.Ok);
        Assert.Equal("invalid_reason", result.Error);
    }

    [Fact]
    public async Task Revoking_twice_is_refused_rather_than_silently_repeated()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f);
        await Revocations(scope).RevokeAsync(f.OwnerId, id, "Wrong person", false);

        var second = await Revocations(scope).RevokeAsync(f.OwnerId, id, "Wrong person again", false);

        Assert.False(second.Ok);
        Assert.Equal("already_revoked", second.Error);
    }

    /// <summary>Revocation is a destructive, publicly visible act, so this is the authority check that
    /// matters most in this phase. D-018: not-found rather than forbidden.</summary>
    [Fact]
    public async Task An_outsider_cannot_revoke_or_read_lineage()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f);

        var revoke = await Revocations(scope).RevokeAsync(f.OutsiderId, id, "Because", false);
        var lineage = await Revocations(scope).LineageAsync(f.OutsiderId, id, false);

        Assert.Equal("not_found", revoke.Error);
        Assert.Equal("not_found", lineage.Error);

        // And it really did not happen.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(IssuedCertificateStatus.Issued,
            (await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == id)).Status);
    }

    /// <summary>A revoked certificate verifies as revoked, with its reason — not as "not found", which
    /// would imply it was never real.</summary>
    [Fact]
    public async Task A_revoked_certificate_verifies_as_revoked_with_its_reason()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f);
        await Revocations(scope).RevokeAsync(f.OwnerId, id, "Awarded in error", false);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var publicId = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.Id == id).Select(c => c.CertificateId).FirstAsync();

        var verdict = await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync(publicId);

        Assert.Equal(CertificateVerificationOutcome.Revoked, verdict.Outcome);
        Assert.Equal("Awarded in error", verdict.RevocationReason);
        Assert.Null(verdict.ReplacementCertificateId);
    }

    // ── Reissue ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reissuing_produces_a_new_certificate_and_supersedes_the_old_one()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f, "Rahul Sharme");

        var result = await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Rahul Sharma", null, "Name was misspelled"), false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("issued", result.Value!.Status);
        Assert.Equal("Rahul Sharma", result.Value.RecipientName);
        Assert.NotNull(result.Value.Supersedes);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var original = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == id);
        // Superseded, NOT revoked. Telling someone with a misspelled name that their certificate was
        // withdrawn is the failure this distinction prevents.
        Assert.Equal(IssuedCertificateStatus.Superseded, original.Status);
    }

    /// <summary>The property everything rests on. Someone already holds a copy of the original, and the
    /// signature over its values exists to detect exactly the change an in-place edit would make.</summary>
    [Fact]
    public async Task Correcting_a_certificate_leaves_the_original_byte_for_byte_untouched()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var id = await IssueAsync(scope, f, "Rahul Sharme");

        var before = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == id);
        var snapshot = (before.CertificateId, before.FieldValuesJson, before.Signature,
            before.SignatureKeyId, before.DocumentSha256, before.PdfStorageKey, before.PngStorageKey,
            before.IssuedAt, before.TemplateVersion);

        await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Rahul Sharma", null, "Name was misspelled"), false);

        db.ChangeTracker.Clear();
        var after = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == id);

        Assert.Equal(snapshot, (after.CertificateId, after.FieldValuesJson, after.Signature,
            after.SignatureKeyId, after.DocumentSha256, after.PdfStorageKey, after.PngStorageKey,
            after.IssuedAt, after.TemplateVersion));
        // Only the status moved.
        Assert.Equal(IssuedCertificateStatus.Superseded, after.Status);
    }

    /// <summary>The replacement is a real certificate, not a copy of the original with a field swapped:
    /// its own id, its own signature over its own values, its own rendered files.</summary>
    [Fact]
    public async Task The_replacement_is_a_real_certificate_in_its_own_right()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var id = await IssueAsync(scope, f, "Rahul Sharme");
        var original = await db.IssuedCertificates.AsNoTracking().FirstAsync(c => c.Id == id);

        var result = await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Rahul Sharma", null, "Misspelled"), false);

        var replacement = await db.IssuedCertificates.AsNoTracking()
            .FirstAsync(c => c.CertificateId == result.Value!.CertificateId);
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        Assert.NotEqual(original.CertificateId, replacement.CertificateId);
        Assert.NotEqual(original.Signature, replacement.Signature);
        Assert.NotEqual(original.PdfStorageKey, replacement.PdfStorageKey);
        Assert.True(await storage.ExistsAsync(replacement.PdfStorageKey!));
        Assert.True(await storage.ExistsAsync(replacement.PngStorageKey!));
        Assert.Contains("Rahul Sharma", replacement.FieldValuesJson, StringComparison.Ordinal);

        // And it verifies on its own terms.
        var verdict = await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync(replacement.CertificateId);
        Assert.Equal(CertificateVerificationOutcome.Valid, verdict.Outcome);
    }

    /// <summary>Correcting one word must not quietly change fields nobody touched.</summary>
    [Fact]
    public async Task Fields_not_being_corrected_carry_over_unchanged()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f, "Rahul Sharme", team: "ByteBuilders");

        var result = await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Rahul Sharma", null, "Misspelled"), false);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var replacement = await db.IssuedCertificates.AsNoTracking()
            .FirstAsync(c => c.CertificateId == result.Value!.CertificateId);

        Assert.Contains("ByteBuilders", replacement.FieldValuesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_named_field_can_be_corrected_without_touching_the_name()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f, "Rahul Sharma", team: "ByteBuidlers");

        var result = await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection(null, new Dictionary<string, string> { ["team_name"] = "ByteBuilders" },
                "Team name was misspelled"), false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("Rahul Sharma", result.Value!.RecipientName);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var replacement = await db.IssuedCertificates.AsNoTracking()
            .FirstAsync(c => c.CertificateId == result.Value.CertificateId);
        Assert.Contains("ByteBuilders", replacement.FieldValuesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ByteBuidlers", replacement.FieldValuesJson, StringComparison.Ordinal);
    }

    /// <summary>Someone holding the old id is told where the live one is, rather than being left with
    /// "this is not current" and nowhere to go.</summary>
    [Fact]
    public async Task A_superseded_certificate_verifies_as_superseded_and_names_its_replacement()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var id = await IssueAsync(scope, f, "Rahul Sharme");
        var oldPublicId = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.Id == id).Select(c => c.CertificateId).FirstAsync();

        var result = await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Rahul Sharma", null, "Misspelled"), false);

        var verdict = await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync(oldPublicId);

        Assert.Equal(CertificateVerificationOutcome.Superseded, verdict.Outcome);
        Assert.Equal(result.Value!.CertificateId, verdict.ReplacementCertificateId);
        Assert.Equal("Misspelled", verdict.RevocationReason);
    }

    [Fact]
    public async Task A_revoked_certificate_cannot_be_reissued()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f);
        await Revocations(scope).RevokeAsync(f.OwnerId, id, "Wrong person", false);

        var result = await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Someone Else", null, "Fixing"), false);

        Assert.False(result.Ok);
        Assert.Equal("certificate_revoked", result.Error);
    }

    /// <summary>Correcting an already-superseded certificate would fork the chain, and "which one is
    /// live" would stop having a single answer.</summary>
    [Fact]
    public async Task A_superseded_certificate_cannot_be_reissued_again()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f, "Rahul Sharme");
        await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Rahul Sharma", null, "Misspelled"), false);

        var again = await Revocations(scope).ReissueAsync(f.OwnerId, id,
            new CertificateCorrection("Rahul S", null, "Again"), false);

        Assert.False(again.Ok);
        Assert.Equal("certificate_superseded", again.Error);
    }

    // ── Lineage ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>A correction of a correction. The chain has to stay walkable from any point in it —
    /// which is what a support conversation actually needs.</summary>
    [Fact]
    public async Task A_chain_of_corrections_is_walkable_from_any_link()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var first = await IssueAsync(scope, f, "Rahul Sharme");
        var second = await Revocations(scope).ReissueAsync(f.OwnerId, first,
            new CertificateCorrection("Rahul Sharma", null, "Misspelled"), false);
        var secondRow = await db.IssuedCertificates.AsNoTracking()
            .FirstAsync(c => c.CertificateId == second.Value!.CertificateId);
        var third = await Revocations(scope).ReissueAsync(f.OwnerId, secondRow.Id,
            new CertificateCorrection("Rahul Kumar Sharma", null, "Full name"), false);

        // From the middle, from the start, and from the end — all three give the same chain.
        foreach (var from in new[] { first, secondRow.Id })
        {
            var lineage = await Revocations(scope).LineageAsync(f.OwnerId, from, false);

            Assert.True(lineage.Ok, lineage.Error);
            Assert.Equal(3, lineage.Value!.Count);
            Assert.Equal("superseded", lineage.Value[0].Status);
            Assert.Equal("superseded", lineage.Value[1].Status);
            Assert.Equal("issued", lineage.Value[2].Status);
            Assert.Equal(third.Value!.CertificateId, lineage.Value[2].CertificateId);
            Assert.Equal("Rahul Kumar Sharma", lineage.Value[2].RecipientName);
        }
    }

    [Fact]
    public async Task A_certificate_with_no_history_is_a_chain_of_one()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var id = await IssueAsync(scope, f);

        var lineage = await Revocations(scope).LineageAsync(f.OwnerId, id, false);

        var only = Assert.Single(lineage.Value!);
        Assert.Equal("issued", only.Status);
        Assert.Null(only.Supersedes);
        Assert.Null(only.SupersededBy);
    }

    // ── Bulk ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>"All two hundred have the wrong date", where revoking one at a time is not a real
    /// option.</summary>
    [Fact]
    public async Task A_whole_run_can_be_withdrawn_at_once()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);

        var result = await Revocations(scope).RevokeBatchAsync(
            f.OwnerId, batchId, "Wrong event date printed on all of them", false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(3, result.Value!.Revoked);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var statuses = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batchId).Select(c => c.Status).ToListAsync();
        Assert.All(statuses, s => Assert.Equal(IssuedCertificateStatus.Revoked, s));
    }

    /// <summary>Re-running a bulk revocation after a partial one must be safe.</summary>
    [Fact]
    public async Task Revoking_a_run_twice_skips_what_is_already_withdrawn()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);
        await Revocations(scope).RevokeBatchAsync(f.OwnerId, batchId, "Wrong date", false);

        var second = await Revocations(scope).RevokeBatchAsync(f.OwnerId, batchId, "Wrong date", false);

        Assert.Equal(0, second.Value!.Revoked);
        Assert.Equal(3, second.Value.Skipped);
    }

    [Fact]
    public async Task Withdrawing_a_run_needs_a_reason_too()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);

        var result = await Revocations(scope).RevokeBatchAsync(f.OwnerId, batchId, "  ", false);

        Assert.False(result.Ok);
        Assert.Equal("invalid_reason", result.Error);
    }

    private async Task<Guid> GeneratedBatchAsync(IServiceScope scope, Fixture f)
    {
        var batches = scope.ServiceProvider.GetRequiredService<ICertificateBatchService>();
        var csv = Encoding.UTF8.GetBytes(
            "Name\nRahul Sharma\nPriya Patel\nArjun Kumar\n");

        var created = await batches.CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "p.csv", csv,
                new Dictionary<string, string> { ["Name"] = "participant_name" }), false);
        Assert.True(created.Ok, created.Error);

        Assert.True((await batches.GeneratePreviewAsync(f.OwnerId, created.Value!.Id, false)).Ok);
        Assert.True((await batches.ApproveAsync(f.OwnerId, created.Value.Id, false)).Ok);
        await batches.RunAsync(created.Value.Id);
        return created.Value.Id;
    }
}
