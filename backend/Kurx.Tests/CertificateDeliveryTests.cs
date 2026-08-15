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
/// Getting certificates to the people they were issued to (D-355, Phase 8).
///
/// <para>What is asserted here is mostly about restraint. <b>Pressing send twice does not send twice</b>,
/// because a page that has not visibly changed yet is pressed twice by real people. <b>A revoked
/// certificate is never delivered</b>, because putting one in an inbox is worse than sending nothing — the
/// platform will publicly call it invalid. <b>A provider having a bad minute does not permanently fail four
/// hundred certificates</b>, but an address that is simply wrong is not retried forever either.</para>
///
/// <para>And the counts are honest: recipients with nowhere to send are reported, not quietly dropped, and
/// <c>Sent</c> is never treated as proof anyone received anything.</para>
/// </summary>
public class CertificateDeliveryTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public CertificateDeliveryTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }

        // The capturing sender is shared across the class, and a test that arms simulated failures does
        // not necessarily consume all of them — an unconsumed one would surface as an unexplained failure
        // in whichever test ran next. Reset per test so each starts from a known sender.
        factory.Email.Clear();
    }

    /// <summary>Unique per test. The class shares one database, so a dispatch drains every pending row in
    /// it — including ones other tests queued. Addressing each test's recipients to its own domain is what
    /// lets "was this certificate emailed" be answered without the answer depending on execution order.</summary>
    private readonly string _token = Guid.NewGuid().ToString("N")[..8];

    private string Address(string local) => $"{local}@{_token}.example.com";

    private sealed record Fixture(Guid EventId, Guid OwnerId, Guid OutsiderId, Guid TemplateId);

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    private static byte[] Csv(string content) => Encoding.UTF8.GetBytes(content);

    /// <summary>Two rows with addresses, one without — the mix every real participant list has.</summary>
    private string ThreeRows =>
        $"Name,Email\nRahul Sharma,{Address("rahul")}\nPriya Patel,{Address("priya")}\nArjun Kumar,\n";

    private static readonly Dictionary<string, string> Mapping =
        new() { ["Name"] = "participant_name", ["Email"] = "email" };

    private static byte[] Artwork()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(600, 424);
        using var output = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, output);
        return output.ToArray();
    }

    private ICertificateDeliveryService Deliveries(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateDeliveryService>();

    private ICertificateBatchService Batches(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICertificateBatchService>();

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

    /// <summary>A run that has actually generated its certificates — the only state from which sending is
    /// meaningful.</summary>
    private async Task<Guid> GeneratedBatchAsync(IServiceScope scope, Fixture f, string? csv = null)
    {
        var created = await Batches(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "participants.csv",
                Csv(csv ?? ThreeRows), Mapping), false);
        Assert.True(created.Ok, created.Error);
        var batchId = created.Value!.Id;

        Assert.True((await Batches(scope).GeneratePreviewAsync(f.OwnerId, batchId, false)).Ok);
        Assert.True((await Batches(scope).ApproveAsync(f.OwnerId, batchId, false)).Ok);
        await Batches(scope).RunAsync(batchId);
        return batchId;
    }

    // ── Queueing ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sending_queues_one_delivery_per_addressable_certificate()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);

        var result = await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(3, result.Value!.Total);
        Assert.Equal(2, result.Value.Pending);
        // Counted and surfaced, never silently dropped: "we sent 2 of your 3" is the organiser's problem
        // to solve, and they can only solve it if they are told.
        Assert.Equal(1, result.Value.NoDestination);
    }

    /// <summary>Queueing does not send. A request that tried to email four hundred people inline would
    /// time out, and its retry would double-send to everyone it had already reached.</summary>
    [Fact]
    public async Task Queueing_sends_nothing_by_itself()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);

        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        Assert.Empty(_factory.Email.MessagesTo(Address("rahul")));
    }

    /// <summary>A page that has not visibly changed yet gets its button pressed twice by real people.</summary>
    [Fact]
    public async Task Pressing_send_twice_does_not_queue_two_emails()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);

        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);
        var second = await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        Assert.Equal(2, second.Value!.Pending);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificateIds = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batchId).Select(c => c.Id).ToListAsync();
        Assert.Equal(2, await db.CertificateDeliveries
            .CountAsync(d => certificateIds.Contains(d.CertificateId)));
    }

    [Fact]
    public async Task An_outsider_cannot_send_or_read_deliveries()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);

        var queued = await Deliveries(scope).QueueBatchAsync(f.OutsiderId, batchId, false);
        var summary = await Deliveries(scope).SummariseBatchAsync(f.OutsiderId, batchId, false);

        // D-018: not-found rather than forbidden.
        Assert.Equal("not_found", queued.Error);
        Assert.Equal("not_found", summary.Error);
    }

    // ── Dispatch ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatching_emails_the_certificate_as_an_attachment()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        await Deliveries(scope).DispatchPendingAsync(100);

        Assert.Single(_factory.Email.MessagesTo(Address("priya")));
        var message = Assert.Single(_factory.Email.MessagesTo(Address("rahul")));
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.NotEmpty(attachment.Content);
    }

    /// <summary>The verification link travels in the body, not only inside the QR: the person who needs to
    /// check a certificate is often not the person holding the PDF.</summary>
    [Fact]
    public async Task The_email_carries_the_verification_link_and_the_certificate_id()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);
        await Deliveries(scope).DispatchPendingAsync(100);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // The certificate belonging to THIS address, not whichever of the three came back first.
        var certificateId = await (
            from c in db.IssuedCertificates.AsNoTracking()
            join r in db.CertificateRecipients.AsNoTracking() on c.RecipientId equals r.Id
            where c.BatchId == batchId && r.NormalizedEmail == Address("rahul")
            select c.CertificateId).FirstAsync();

        var message = Assert.Single(_factory.Email.MessagesTo(Address("rahul")));
        Assert.Contains("/verify/", message.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(certificateId, message.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dispatched_delivery_records_what_the_provider_said()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);
        await Deliveries(scope).DispatchPendingAsync(100);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var delivery = await db.CertificateDeliveries.AsNoTracking()
            .FirstAsync(d => d.Destination == Address("rahul"));

        Assert.Equal(CertificateDeliveryStatus.Sent, delivery.Status);
        Assert.NotNull(delivery.SentAt);
        // Kept so a later bounce can be correlated back. It confirms the provider ACCEPTED the message,
        // which is not the same as anyone receiving it.
        Assert.NotNull(delivery.ProviderMessageId);
        Assert.Equal(1, delivery.AttemptCount);
    }

    /// <summary>Draining is idempotent: a second run finds nothing to do rather than sending again.</summary>
    [Fact]
    public async Task Dispatching_twice_does_not_send_twice()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f);
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        await Deliveries(scope).DispatchPendingAsync(100);
        await Deliveries(scope).DispatchPendingAsync(100);

        Assert.Single(_factory.Email.MessagesTo(Address("rahul")));
    }

    // ── Failure ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>A provider having a bad minute must not permanently fail four hundred certificates.</summary>
    [Fact]
    public async Task A_transient_provider_failure_is_retried_rather_than_given_up_on()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, $"Name,Email\nRahul Sharma,{Address("rahul2")}\n");
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        _factory.Email.FailNext(1);
        await Deliveries(scope).DispatchPendingAsync(100);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var afterFailure = await db.CertificateDeliveries.AsNoTracking()
            .FirstAsync(d => d.Destination == Address("rahul2"));
        // Still pending, not given up on — one bad minute from a provider is not a permanent failure.
        Assert.Equal(CertificateDeliveryStatus.Pending, afterFailure.Status);
        Assert.Equal(1, afterFailure.AttemptCount);

        await Deliveries(scope).DispatchPendingAsync(100);

        Assert.Single(_factory.Email.MessagesTo(Address("rahul2")));
    }

    /// <summary>But it is not retried forever. A permanently unreachable address is parked for a human
    /// rather than consuming the queue indefinitely.</summary>
    [Fact]
    public async Task A_delivery_is_given_up_on_after_repeated_failures()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, $"Name,Email\nRahul Sharma,{Address("rahul3")}\n");
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        _factory.Email.FailNext(10);
        for (var i = 0; i < 4; i++) await Deliveries(scope).DispatchPendingAsync(100);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var delivery = await db.CertificateDeliveries.AsNoTracking()
            .FirstAsync(d => d.Destination == Address("rahul3"));

        Assert.Equal(CertificateDeliveryStatus.Failed, delivery.Status);
        Assert.NotNull(delivery.Error);
        Assert.Equal(3, delivery.AttemptCount);
    }

    /// <summary>Putting a revoked certificate in someone's inbox is worse than sending nothing: the
    /// platform will publicly declare that document invalid.</summary>
    [Fact]
    public async Task A_certificate_revoked_before_dispatch_is_never_sent()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, $"Name,Email\nRahul Sharma,{Address("rahul4")}\n");
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificate = await db.IssuedCertificates.FirstAsync(c => c.BatchId == batchId);
        certificate.Status = IssuedCertificateStatus.Revoked;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Deliveries(scope).DispatchPendingAsync(100);

        Assert.Empty(_factory.Email.MessagesTo(Address("rahul4")));
        var delivery = await db.CertificateDeliveries.AsNoTracking()
            .FirstAsync(d => d.Destination == Address("rahul4"));
        Assert.Equal(CertificateDeliveryStatus.Failed, delivery.Status);
        Assert.Equal("certificate_no_longer_issued", delivery.Error);
    }

    // ── Resend ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The case "already sent" must never block: the first address was wrong.</summary>
    [Fact]
    public async Task A_resend_to_a_corrected_address_is_always_allowed()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, $"Name,Email\nRahul Sharma,{Address("typo")}\n");
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);
        await Deliveries(scope).DispatchPendingAsync(100);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificateId = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batchId).Select(c => c.Id).FirstAsync();

        var resend = await Deliveries(scope).ResendAsync(
            f.OwnerId, certificateId, Address("correct"), false);
        Assert.True(resend.Ok, resend.Error);

        await Deliveries(scope).DispatchPendingAsync(100);

        Assert.Single(_factory.Email.MessagesTo(Address("correct")));
    }

    [Fact]
    public async Task A_resend_with_no_address_anywhere_is_refused()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, "Name,Email\nArjun Kumar,\n");

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificateId = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batchId).Select(c => c.Id).FirstAsync();

        var result = await Deliveries(scope).ResendAsync(f.OwnerId, certificateId, null, false);

        Assert.False(result.Ok);
        Assert.Equal("no_destination", result.Error);
    }

    [Fact]
    public async Task A_revoked_certificate_cannot_be_resent()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, $"Name,Email\nRahul Sharma,{Address("rahul5")}\n");

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificate = await db.IssuedCertificates.FirstAsync(c => c.BatchId == batchId);
        certificate.Status = IssuedCertificateStatus.Revoked;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Deliveries(scope).ResendAsync(f.OwnerId, certificate.Id, null, false);

        Assert.False(result.Ok);
        Assert.Equal("certificate_not_sendable", result.Error);
    }

    [Fact]
    public async Task A_malformed_address_is_refused_rather_than_queued()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, $"Name,Email\nRahul Sharma,{Address("rahul6")}\n");

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificateId = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batchId).Select(c => c.Id).FirstAsync();

        var result = await Deliveries(scope).ResendAsync(f.OwnerId, certificateId, "not an address", false);

        Assert.False(result.Ok);
        Assert.Equal("no_destination", result.Error);
    }

    // ── Summary honesty ─────────────────────────────────────────────────────────────────────────

    /// <summary>Counted per certificate, not per delivery row: a certificate that failed once and
    /// succeeded on a resend has been sent, and appearing in both columns would make the numbers not
    /// add up.</summary>
    [Fact]
    public async Task A_certificate_that_failed_then_succeeded_counts_once_as_sent()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, $"Name,Email\nRahul Sharma,{Address("typo2")}\n");
        await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);

        _factory.Email.FailNext(10);
        for (var i = 0; i < 4; i++) await Deliveries(scope).DispatchPendingAsync(100);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var certificateId = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batchId).Select(c => c.Id).FirstAsync();
        // The provider recovers before the resend — otherwise this asserts nothing about the resend, only
        // that a still-broken provider still fails.
        _factory.Email.FailNext(0);
        await Deliveries(scope).ResendAsync(f.OwnerId, certificateId, Address("fixed"), false);
        await Deliveries(scope).DispatchPendingAsync(100);

        var summary = await Deliveries(scope).SummariseBatchAsync(f.OwnerId, batchId, false);

        Assert.Equal(1, summary.Value!.Total);
        Assert.Equal(1, summary.Value.Sent);
        Assert.Equal(0, summary.Value.Failed);
    }

    [Fact]
    public async Task A_run_with_nothing_generated_has_nothing_to_send()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var created = await Batches(scope).CreateAsync(f.OwnerId, f.EventId,
            new CertificateBatchInput("Winners", f.TemplateId, "p.csv", Csv(ThreeRows), Mapping), false);

        var result = await Deliveries(scope).QueueBatchAsync(f.OwnerId, created.Value!.Id, false);

        Assert.False(result.Ok);
        Assert.Equal("nothing_to_send", result.Error);
    }

    /// <summary>A linked account is an availability, not a send — the certificate simply appears, with
    /// nothing transmitted and no email produced.</summary>
    [Fact]
    public async Task A_recipient_with_an_account_but_no_address_is_served_without_an_email()
    {
        var f = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var batchId = await GeneratedBatchAsync(scope, f, "Name,Email\nArjun Kumar,\n");

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var recipient = await db.CertificateRecipients.FirstAsync(r => r.BatchId == batchId);
        recipient.UserId = f.OutsiderId;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Deliveries(scope).QueueBatchAsync(f.OwnerId, batchId, false);
        await Deliveries(scope).DispatchPendingAsync(100);

        Assert.Equal(1, result.Value!.Sent);
        Assert.Equal(0, result.Value.Pending);
        Assert.Equal(0, result.Value.NoDestination);
        Assert.Empty(_factory.Email.Messages.Where(m => m.Subject.Contains("Hackathon")));
    }
}
