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
/// What happened to an event's certificates (D-355, Phase 11).
///
/// <para>Two properties carry this phase, and both are about restraint rather than capability.</para>
///
/// <para><b>Telemetry counts events, not people.</b> A verification row records a certificate, a type, a
/// time and a request correlation id — nothing about who checked it. Storing more would turn an
/// organiser's dashboard into a record of where the holder has been applying for jobs.</para>
///
/// <para><b>The export cannot be turned into an attack.</b> Participant names come from a file a stranger
/// uploaded, and a cell beginning <c>=</c> executes as a formula when the CSV is reopened in Excel. This is
/// the mirror of the Phase 6 reader, which never evaluates one: neither half of this module lets an
/// uploaded string become executable.</para>
/// </summary>
public class CertificateAnalyticsTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateAnalyticsTests(KurxApiFactory factory)
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

    private ICertificateAnalyticsService Analytics(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateAnalyticsService>();

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

    private async Task<(Guid RowId, string PublicId)> IssueAsync(
        IServiceScope scope, Fixture f, string name = "Rahul Sharma", string? email = null)
    {
        var issued = await scope.ServiceProvider.GetRequiredService<ICertificateIssuingService>()
            .IssueAsync(f.OwnerId, f.TemplateId,
                new IssueCertificateInput(name, email, new Dictionary<string, string>()), false);
        Assert.True(issued.Ok, issued.Error);
        return (issued.Value!.Id, issued.Value.CertificateId);
    }

    // ── Telemetry ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verifying_a_certificate_is_counted()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, publicId) = await IssueAsync(scope, f);

        var verification = scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>();
        await verification.VerifyAsync(publicId);
        await verification.VerifyAsync(publicId);

        var dashboard = await Analytics(scope).DashboardAsync(f.OwnerId, f.EventId, false);

        Assert.True(dashboard.Ok, dashboard.Error);
        Assert.Equal(2, dashboard.Value!.Verifications);
    }

    /// <summary>Storing who checked a certificate would turn an organiser's dashboard into a record of
    /// where the holder has been applying for jobs.</summary>
    [Fact]
    public async Task A_verification_record_says_nothing_about_who_verified()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (rowId, publicId) = await IssueAsync(scope, f);

        await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync(publicId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var recorded = await db.CertificateEvents.AsNoTracking()
            .FirstAsync(e => e.CertificateId == rowId);

        // The entity has no field that could hold a person, which is the point — asserted so a later
        // addition has to argue with a test.
        Assert.Equal(CertificateEventType.Verified, recorded.Type);
        Assert.NotEqual(default, recorded.OccurredAt);
        Assert.DoesNotContain(
            typeof(CertificateEvent).GetProperties().Select(p => p.Name),
            name => name is "UserId" or "IpAddress" or "UserAgent" or "Email");
    }

    /// <summary>A miss records nothing — otherwise the counter measures guessing rather than use.</summary>
    [Fact]
    public async Task Verifying_something_that_does_not_exist_counts_nothing()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await IssueAsync(scope, f);

        await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync("NOT-A-REAL-CERTIFICATE");

        var dashboard = await Analytics(scope).DashboardAsync(f.OwnerId, f.EventId, false);
        Assert.Equal(0, dashboard.Value!.Verifications);
    }

    /// <summary>Downloads happen browser-to-storage and are never seen here; Shared has no mechanism at
    /// all. Writing either would be inventing a number, and an always-zero counter that looks like a
    /// feature is worse than an absent one.</summary>
    [Theory]
    [InlineData("Downloaded")]
    [InlineData("Shared")]
    [InlineData("NotAnEventType")]
    public async Task Only_what_the_platform_can_observe_is_recorded(string type)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (rowId, publicId) = await IssueAsync(scope, f);

        await Analytics(scope).RecordAsync(publicId, type);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(0, await db.CertificateEvents.CountAsync(e => e.CertificateId == rowId));
    }

    /// <summary>The observation is worth less than the thing being observed.</summary>
    [Fact]
    public async Task Recording_against_an_unknown_certificate_does_not_throw()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        await Analytics(scope).RecordAsync("NOT-A-REAL-CERTIFICATE", "Verified");
    }

    [Fact]
    public async Task Opening_a_capability_link_counts_as_a_view()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (rowId, _) = await IssueAsync(scope, f);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var recipientId = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.Id == rowId).Select(c => c.RecipientId).FirstAsync();

        var participants = scope.ServiceProvider.GetRequiredService<ICertificateParticipantService>();
        var link = await participants.CreateAccessLinkAsync(f.OwnerId, recipientId, false);
        var url = link.Value!.Url!;
        await participants.ResolveAccessAsync(url[(url.LastIndexOf('/') + 1)..]);

        var dashboard = await Analytics(scope).DashboardAsync(f.OwnerId, f.EventId, false);
        Assert.Equal(1, dashboard.Value!.Views);
    }

    // ── Dashboard ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_dashboard_counts_live_withdrawn_and_replaced_separately()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var revocations = scope.ServiceProvider.GetRequiredService<ICertificateRevocationService>();

        await IssueAsync(scope, f, "Live One");
        var (withdrawn, _) = await IssueAsync(scope, f, "Withdrawn One");
        var (replaced, _) = await IssueAsync(scope, f, "Replaced One");

        await revocations.RevokeAsync(f.OwnerId, withdrawn, "Wrong person", false);
        await revocations.ReissueAsync(f.OwnerId, replaced,
            new CertificateCorrection("Replaced Two", null, "Misspelled"), false);

        var dashboard = await Analytics(scope).DashboardAsync(f.OwnerId, f.EventId, false);

        // Live counts the original plus the replacement; the superseded one is not still good.
        Assert.Equal(2, dashboard.Value!.Live);
        Assert.Equal(1, dashboard.Value.Revoked);
        Assert.Equal(1, dashboard.Value.Superseded);
        Assert.Equal(1, dashboard.Value.Templates);
    }

    [Fact]
    public async Task The_dashboard_reports_delivery_and_addressability()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await IssueAsync(scope, f, "Has Address", $"has@{suffix}.example.com");
        await IssueAsync(scope, f, "No Address");

        var dashboard = await Analytics(scope).DashboardAsync(f.OwnerId, f.EventId, false);

        // Said out loud rather than silently dropped: one of these two can never be emailed.
        Assert.Equal(1, dashboard.Value!.NoDestination);
        Assert.Equal(0, dashboard.Value.Sent);
    }

    /// <summary>Every day in the window, including the empty ones — a sparse series renders as a chart
    /// with gaps that read as missing data rather than as quiet days.</summary>
    [Fact]
    public async Task The_daily_series_has_no_holes_in_it()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (_, publicId) = await IssueAsync(scope, f);
        await scope.ServiceProvider.GetRequiredService<ICertificateVerificationService>()
            .VerifyAsync(publicId);

        var dashboard = await Analytics(scope).DashboardAsync(f.OwnerId, f.EventId, false);
        var series = dashboard.Value!.RecentVerifications;

        Assert.Equal(30, series.Count);
        Assert.Equal(series.OrderBy(d => d.Day).Select(d => d.Day), series.Select(d => d.Day));
        Assert.Equal(1, series[^1].Count);   // today
        Assert.All(series.Take(29), day => Assert.Equal(0, day.Count));
    }

    [Fact]
    public async Task An_outsider_sees_neither_the_dashboard_nor_the_export()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await IssueAsync(scope, f);

        var dashboard = await Analytics(scope).DashboardAsync(f.OutsiderId, f.EventId, false);
        var export = await Analytics(scope).ExportAsync(f.OutsiderId, f.EventId, null, false);

        Assert.False(dashboard.Ok);
        Assert.False(export.Ok);
    }

    // ── Export ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_export_lists_every_certificate_with_its_standing()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, publicId) = await IssueAsync(scope, f, "Rahul Sharma", $"rahul@{suffix}.example.com");

        var export = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, null, false);

        Assert.True(export.Ok, export.Error);
        var csv = Text(export.Value!.Content);

        Assert.Contains("certificate_id,participant_name,email,status", csv, StringComparison.Ordinal);
        Assert.Contains(publicId, csv, StringComparison.Ordinal);
        Assert.Contains("Rahul Sharma", csv, StringComparison.Ordinal);
        Assert.Contains($"rahul@{suffix}.example.com", csv, StringComparison.Ordinal);
        Assert.Contains("issued", csv, StringComparison.Ordinal);
        Assert.Contains("/verify/", csv, StringComparison.Ordinal);
        Assert.Equal(1, export.Value.Rows);
        Assert.False(export.Value.Truncated);
    }

    /// <summary>The attack: a participant name that Excel executes when the organiser reopens the export.
    /// Prefixing a quote keeps the value readable and stops it being a formula.</summary>
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil.example\",\"Click\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(1:9)")]
    public async Task A_name_that_looks_like_a_formula_is_neutralised(string hostileName)
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await IssueAsync(scope, f, hostileName);

        var export = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, null, false);
        var csv = Text(export.Value!.Content);

        // The dangerous character is never the first thing in the cell.
        Assert.Contains("'" + hostileName[0], csv, StringComparison.Ordinal);
        Assert.DoesNotContain($",{hostileName[0]}", csv, StringComparison.Ordinal);
    }

    /// <summary>An ordinary name containing a comma must survive the round trip, so the escaping cannot
    /// just strip things.</summary>
    [Fact]
    public async Task A_name_containing_a_comma_or_quote_is_quoted_not_mangled()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await IssueAsync(scope, f, "Sharma, Rahul \"AJ\"");

        var export = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, null, false);
        var csv = Text(export.Value!.Content);

        Assert.Contains("\"Sharma, Rahul \"\"AJ\"\"\"", csv, StringComparison.Ordinal);
    }

    /// <summary>Excel opens a UTF-8 file as UTF-8 only if there is a BOM. Without it an Indian participant
    /// list comes back as mojibake in the one application most organisers will use.</summary>
    [Fact]
    public async Task The_export_carries_a_byte_order_mark_and_preserves_unicode()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await IssueAsync(scope, f, "अनन्या राव");

        var export = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, null, false);
        var bytes = export.Value!.Content;

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Contains("अनन्या राव", Text(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_export_can_be_narrowed_to_one_run()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        await IssueAsync(scope, f, "Hand Issued");
        var batchId = await GeneratedBatchAsync(scope, f);

        var all = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, null, false);
        var justTheRun = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, batchId, false);

        Assert.Equal(3, all.Value!.Rows);
        Assert.Equal(2, justTheRun.Value!.Rows);
        Assert.DoesNotContain("Hand Issued", Text(justTheRun.Value.Content), StringComparison.Ordinal);
    }

    /// <summary>A run belonging to a different event is not a filter this caller may apply.</summary>
    [Fact]
    public async Task An_export_cannot_be_narrowed_to_another_events_run()
    {
        var f = await SeedAsync();
        var other = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var theirBatch = await GeneratedBatchAsync(scope, other);

        var export = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, theirBatch, false);

        Assert.False(export.Ok);
        Assert.Equal("not_found", export.Error);
    }

    [Fact]
    public async Task An_export_of_an_event_with_nothing_issued_is_a_header_and_no_rows()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();

        var export = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, null, false);

        Assert.True(export.Ok, export.Error);
        Assert.Equal(0, export.Value!.Rows);
        Assert.Contains("certificate_id", Text(export.Value.Content), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_withdrawn_certificate_appears_in_the_export_with_its_reason()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var (rowId, _) = await IssueAsync(scope, f);
        await scope.ServiceProvider.GetRequiredService<ICertificateRevocationService>()
            .RevokeAsync(f.OwnerId, rowId, "Awarded in error", false);

        var export = await Analytics(scope).ExportAsync(f.OwnerId, f.EventId, null, false);
        var csv = Text(export.Value!.Content);

        Assert.Contains("revoked", csv, StringComparison.Ordinal);
        Assert.Contains("Awarded in error", csv, StringComparison.Ordinal);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('﻿');

    private async Task<Guid> GeneratedBatchAsync(IServiceScope scope, Fixture f)
    {
        var batches = scope.ServiceProvider.GetRequiredService<ICertificateBatchService>();
        var csv = Encoding.UTF8.GetBytes("Name\nRahul Sharma\nPriya Patel\n");

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
