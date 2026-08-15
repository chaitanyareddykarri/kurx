using System.Net;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Getting certificates to the people they were issued to (D-344, Phase 8).
///
/// <para><b>Queue, then drain.</b> Nothing is sent inside the request that asks for it. A send loop over
/// four hundred recipients inside an HTTP call fails halfway with no record of how far it got, and the
/// organiser's only recourse is to press the button again and double-send to everyone who already received
/// one. Each intended send is a row instead, drained by a job — so "who has been sent what" is a query.</para>
///
/// <para><b>Sent means the provider accepted it, and nothing more.</b> There is no bounce pipeline, so
/// there is no honest state for "delivered" and none is offered.</para>
/// </summary>
public class CertificateDeliveryService(
    KurxDbContext db,
    IEventAuthority authority,
    IStorage storage,
    IEmailSender email,
    ICertificateVerificationLinks links,
    ILogger<CertificateDeliveryService> log) : ICertificateDeliveryService
{
    /// <summary>Attempts before a delivery is parked for a human. Three is enough to ride out a provider
    /// blip; more would keep retrying an address that is simply wrong.</summary>
    private const int MaxAttempts = 3;

    /// <summary>Ceiling on an attached certificate. Providers reject oversized messages outright, and a
    /// rejection at send time is indistinguishable from a bad address — better to refuse it here with a
    /// reason that names the actual problem.</summary>
    private const int MaxAttachmentBytes = 8 * 1024 * 1024;

    /// <summary>How many recent deliveries a summary carries. The counts are the answer; the list is for
    /// recognising a pattern in the failures.</summary>
    private const int RecentCount = 20;

    // ── Queueing ────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateDeliverySummary>> QueueBatchAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default)
    {
        var (batch, error) = await LoadBatchAsync(userId, batchId, isAdmin, ct);
        if (error is not null) return ServiceResult<CertificateDeliverySummary>.Fail(error);

        var certificates = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batch!.Id && c.Status == IssuedCertificateStatus.Issued)
            .ToListAsync(ct);

        if (certificates.Count == 0)
            return ServiceResult<CertificateDeliverySummary>.Fail("nothing_to_send");

        var recipientIds = certificates.Select(c => c.RecipientId).ToList();
        var recipients = await db.CertificateRecipients.AsNoTracking()
            .Where(r => recipientIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r, ct);

        // Anything already queued or already accepted is left alone. Pressing "send" twice is a normal
        // human reaction to a page that has not visibly changed yet, and it must not mean two emails.
        var certificateIds = certificates.Select(c => c.Id).ToList();
        var alreadyHandled = await db.CertificateDeliveries.AsNoTracking()
            .Where(d => certificateIds.Contains(d.CertificateId)
                        && d.Status != CertificateDeliveryStatus.Failed)
            .Select(d => d.CertificateId)
            .ToListAsync(ct);
        var handled = new HashSet<Guid>(alreadyHandled);

        var queued = new List<CertificateDelivery>();
        foreach (var certificate in certificates)
        {
            if (handled.Contains(certificate.Id)) continue;
            if (!recipients.TryGetValue(certificate.RecipientId, out var recipient)) continue;

            var destination = Destination(recipient);
            if (destination is not null)
            {
                queued.Add(new CertificateDelivery
                {
                    CertificateId = certificate.Id,
                    Channel = CertificateDeliveryChannel.Email,
                    Destination = destination,
                    Status = CertificateDeliveryStatus.Pending,
                });
            }
            else if (recipient.UserId is not null)
            {
                // An availability rather than a send: the certificate simply appears for a linked user,
                // with nothing transmitted. Recorded as Sent immediately because there is nothing to wait
                // for — the artefact is already there.
                queued.Add(new CertificateDelivery
                {
                    CertificateId = certificate.Id,
                    Channel = CertificateDeliveryChannel.Account,
                    Destination = null,
                    Status = CertificateDeliveryStatus.Sent,
                    SentAt = DateTime.UtcNow,
                });
            }
            // Certificates with neither an address nor an account produce no row at all. They are counted
            // in the summary as NoDestination, because a delivery row claiming to be pending forever would
            // be a lie about work that will never happen.
        }

        if (queued.Count > 0)
        {
            db.CertificateDeliveries.AddRange(queued);
            await db.SaveChangesAsync(ct);
        }

        return await SummariseAsync(batch!, ct);
    }

    public async Task<ServiceResult<CertificateDeliveryView>> ResendAsync(
        Guid userId, Guid certificateId, string? destination, bool isAdmin, CancellationToken ct = default)
    {
        var certificate = await db.IssuedCertificates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == certificateId, ct);
        if (certificate is null) return ServiceResult<CertificateDeliveryView>.Fail("not_found");

        var access = await authority.ResolveAsync(userId, certificate.EventId, isAdmin, ct);
        // D-018: a certificate on an event the caller may not see is reported as not-found.
        if (!access.EventExists || !access.Can(EventPermission.ManageContent))
            return ServiceResult<CertificateDeliveryView>.Fail("not_found");

        // A revoked certificate is not a thing to hand anyone. Sending one is worse than sending nothing:
        // it puts a document in an inbox that the platform will publicly declare invalid.
        if (certificate.Status != IssuedCertificateStatus.Issued)
            return ServiceResult<CertificateDeliveryView>.Fail("certificate_not_sendable");

        var recipient = await db.CertificateRecipients.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == certificate.RecipientId, ct);

        var address = string.IsNullOrWhiteSpace(destination)
            ? (recipient is null ? null : Destination(recipient))
            : destination.Trim();

        if (address is null || !LooksLikeEmail(address))
            return ServiceResult<CertificateDeliveryView>.Fail("no_destination");

        // Always a new row. A deliberate resend — usually because the first address was wrong — is exactly
        // the case where "already sent" must not block it.
        var delivery = new CertificateDelivery
        {
            CertificateId = certificate.Id,
            Channel = CertificateDeliveryChannel.Email,
            Destination = address,
            Status = CertificateDeliveryStatus.Pending,
        };
        db.CertificateDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);

        return ServiceResult<CertificateDeliveryView>.Success(Project(delivery));
    }

    public async Task<ServiceResult<CertificateDeliverySummary>> SummariseBatchAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default)
    {
        var (batch, error) = await LoadBatchAsync(userId, batchId, isAdmin, ct);
        return error is not null
            ? ServiceResult<CertificateDeliverySummary>.Fail(error)
            : await SummariseAsync(batch!, ct);
    }

    // ── Dispatch ────────────────────────────────────────────────────────────────────────────────

    public async Task<CertificateDispatchResult> DispatchPendingAsync(
        int max, CancellationToken ct = default)
    {
        var pending = await db.CertificateDeliveries
            .Where(d => d.Status == CertificateDeliveryStatus.Pending
                        && d.Channel == CertificateDeliveryChannel.Email
                        && d.AttemptCount < MaxAttempts)
            .OrderBy(d => d.CreatedAt)
            .Take(max)
            .ToListAsync(ct);

        if (pending.Count == 0) return new CertificateDispatchResult(0, 0, 0);

        int sent = 0, failed = 0, skipped = 0;

        foreach (var delivery in pending)
        {
            if (ct.IsCancellationRequested) break;

            var certificate = await db.IssuedCertificates.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == delivery.CertificateId, ct);

            // Revoked between queueing and sending. Not an error and not a send — the queued intent is
            // simply no longer valid, and the row says so rather than silently disappearing.
            if (certificate is null || certificate.Status != IssuedCertificateStatus.Issued)
            {
                delivery.Status = CertificateDeliveryStatus.Failed;
                delivery.Error = "certificate_no_longer_issued";
                skipped++;
                continue;
            }

            delivery.AttemptCount++;

            try
            {
                await SendAsync(delivery, certificate, ct);
                delivery.Status = CertificateDeliveryStatus.Sent;
                delivery.SentAt = DateTime.UtcNow;
                delivery.Error = null;
                sent++;
            }
            catch (CertificateDeliveryRefusedException ex)
            {
                // Retrying cannot help: the address is malformed, or the artefact is missing or too large.
                // Parked immediately with a reason the organiser can act on.
                delivery.Status = CertificateDeliveryStatus.Failed;
                delivery.Error = Truncate(ex.Message);
                failed++;
                log.LogWarning("Certificate delivery {Id} refused: {Reason}", delivery.Id, ex.Message);
            }
            catch (Exception ex)
            {
                // Transient until proven otherwise: left Pending so the next run retries, and only parked
                // once the attempts are spent. An email provider having a bad minute must not permanently
                // fail four hundred certificates.
                delivery.Error = Truncate(ex.Message);
                if (delivery.AttemptCount >= MaxAttempts)
                {
                    delivery.Status = CertificateDeliveryStatus.Failed;
                    failed++;
                    log.LogError(ex, "Certificate delivery {Id} failed after {Attempts} attempts",
                        delivery.Id, delivery.AttemptCount);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new CertificateDispatchResult(sent, failed, skipped);
    }

    /// <summary>Sends one certificate. Throws <see cref="CertificateDeliveryRefusedException"/> for
    /// anything a retry cannot fix.</summary>
    private async Task SendAsync(
        CertificateDelivery delivery, IssuedCertificate certificate, CancellationToken ct)
    {
        if (delivery.Destination is null || !LooksLikeEmail(delivery.Destination))
            throw new CertificateDeliveryRefusedException("invalid_destination");

        if (certificate.PdfStorageKey is null)
            throw new CertificateDeliveryRefusedException("certificate_file_missing");

        byte[] pdf;
        try
        {
            if (!await storage.ExistsAsync(certificate.PdfStorageKey, ct))
                throw new CertificateDeliveryRefusedException("certificate_file_missing");
            pdf = await storage.GetAsync(certificate.PdfStorageKey, ct);
        }
        catch (CertificateDeliveryRefusedException) { throw; }
        catch (Exception)
        {
            // Storage being unreachable is transient, and must not be recorded as a bad certificate.
            throw;
        }

        if (pdf.Length > MaxAttachmentBytes)
            throw new CertificateDeliveryRefusedException("certificate_too_large_to_email");

        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == certificate.EventId)
            .Select(e => new { e.Title })
            .FirstOrDefaultAsync(ct);

        var recipient = await db.CertificateRecipients.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == certificate.RecipientId, ct);

        var eventTitle = ev?.Title ?? "your event";
        var name = recipient?.FullName ?? "there";
        var verifyUrl = links.VerificationUrl(certificate.CertificateId);

        var body =
            $"<p>Hi {WebUtility.HtmlEncode(name)},</p>" +
            $"<p>Your certificate for <strong>{WebUtility.HtmlEncode(eventTitle)}</strong> is attached.</p>" +
            // The verification link is in the body, not only in the QR: the person who needs to check this
            // certificate is often not the person holding the PDF, and asking them to scan a code out of a
            // document they were forwarded is a worse experience than a link they can click.
            $"<p>Anyone can confirm it is genuine at <a href=\"{WebUtility.HtmlEncode(verifyUrl)}\">{WebUtility.HtmlEncode(verifyUrl)}</a>" +
            $" — certificate ID <strong>{WebUtility.HtmlEncode(certificate.CertificateId)}</strong>.</p>";

        var attachment = new EmailAttachment(
            $"certificate-{certificate.CertificateId}.pdf", "application/pdf", pdf);

        delivery.ProviderMessageId = await email.SendAsync(
            delivery.Destination, $"Your certificate for {eventTitle}", body, [attachment], ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private async Task<(CertificateBatch? Batch, string? Error)> LoadBatchAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct)
    {
        var batch = await db.CertificateBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return (null, "not_found");

        var access = await authority.ResolveAsync(userId, batch.EventId, isAdmin, ct);
        if (!access.EventExists || !access.Can(EventPermission.ManageContent)) return (null, "not_found");

        return (batch, null);
    }

    private async Task<ServiceResult<CertificateDeliverySummary>> SummariseAsync(
        CertificateBatch batch, CancellationToken ct)
    {
        var certificates = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .Select(c => new { c.Id, c.RecipientId })
            .ToListAsync(ct);

        var certificateIds = certificates.Select(c => c.Id).ToList();

        var deliveries = await db.CertificateDeliveries.AsNoTracking()
            .Where(d => certificateIds.Contains(d.CertificateId))
            .ToListAsync(ct);

        // Counted per CERTIFICATE, not per delivery row: a certificate that failed once and succeeded on a
        // resend has been sent, and reporting it in both columns would make the numbers not add up.
        var byCertificate = deliveries
            .GroupBy(d => d.CertificateId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreatedAt).First().Status);

        var recipientIds = certificates.Select(c => c.RecipientId).ToList();
        var addressable = await db.CertificateRecipients.AsNoTracking()
            .Where(r => recipientIds.Contains(r.Id)
                        && (r.NormalizedEmail != null || r.UserId != null))
            .Select(r => r.Id)
            .ToListAsync(ct);
        var addressableSet = new HashSet<Guid>(addressable);

        return ServiceResult<CertificateDeliverySummary>.Success(new CertificateDeliverySummary(
            batch.Id,
            certificates.Count,
            byCertificate.Count(kv => kv.Value == CertificateDeliveryStatus.Pending),
            byCertificate.Count(kv => kv.Value == CertificateDeliveryStatus.Sent),
            byCertificate.Count(kv => kv.Value is CertificateDeliveryStatus.Failed
                                              or CertificateDeliveryStatus.Bounced),
            certificates.Count(c => !addressableSet.Contains(c.RecipientId)),
            deliveries.OrderByDescending(d => d.CreatedAt).Take(RecentCount).Select(Project).ToList()));
    }

    /// <summary>The address the organiser gave for this person, normalised. Taken from the recipient row
    /// rather than from a linked account: the organiser said where to send it, and a stale address on an
    /// account they happen to match is not a better answer.</summary>
    private static string? Destination(CertificateRecipient recipient) =>
        !string.IsNullOrWhiteSpace(recipient.NormalizedEmail) && LooksLikeEmail(recipient.NormalizedEmail)
            ? recipient.NormalizedEmail
            : null;

    /// <summary>A shape check, not a validity check. Nothing short of sending proves an address works, so
    /// this only rejects what obviously cannot be one.</summary>
    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0
               && at == value.LastIndexOf('@')
               && at < value.Length - 1
               && value.IndexOf('.', at) > at + 1
               && !value.Contains(' ');
    }

    private static CertificateDeliveryView Project(CertificateDelivery d) => new(
        d.Id, d.CertificateId, d.Channel.ToString().ToLowerInvariant(), d.Destination,
        d.Status.ToString().ToLowerInvariant(), d.Error, d.AttemptCount, d.SentAt, d.CreatedAt);

    private static string Truncate(string message) => message.Length > 1000 ? message[..1000] : message;
}

/// <summary>A delivery that cannot succeed however many times it is retried — a malformed address, a
/// missing artefact, a message no provider will accept. Distinct from a transient failure, which is left
/// pending for the next run.</summary>
public class CertificateDeliveryRefusedException(string message) : Exception(message);
