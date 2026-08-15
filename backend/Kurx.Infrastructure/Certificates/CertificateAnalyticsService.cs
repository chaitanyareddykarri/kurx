using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// What happened to an event's certificates (D-344, Phase 11).
///
/// <para><b>Counting events, not people.</b> Nothing written here identifies who did anything. An organiser
/// needs to know their certificates are being checked; they do not need to know who is checking a
/// particular person's credential, and storing that would make a verification log into a surveillance
/// record of the holder's job applications.</para>
/// </summary>
public class CertificateAnalyticsService(
    KurxDbContext db,
    IEventAuthority authority,
    ICorrelationAccessor correlation,
    ICertificateVerificationLinks links,
    ILogger<CertificateAnalyticsService> log) : ICertificateAnalyticsService
{
    /// <summary>How far back the daily series runs.</summary>
    private const int TrailingDays = 30;

    /// <summary>Ceiling on an export. Large enough for any real event; small enough that one request
    /// cannot build a hundred-megabyte string in memory. Truncation is reported.</summary>
    private const int MaxExportRows = 20_000;

    // ── Recording ───────────────────────────────────────────────────────────────────────────────

    public async Task RecordAsync(string certificateId, string type, CancellationToken ct = default)
    {
        try
        {
            if (!Enum.TryParse<CertificateEventType>(type, ignoreCase: true, out var parsed)) return;

            // Only the two the platform can honestly observe. Downloads happen browser-to-storage and are
            // never seen here; Shared has no mechanism at all. Writing either would be inventing a number.
            if (parsed is not (CertificateEventType.Verified or CertificateEventType.Viewed)) return;

            var rowId = await db.IssuedCertificates.AsNoTracking()
                .Where(c => c.CertificateId == certificateId)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);
            if (rowId is null) return;

            db.CertificateEvents.Add(new CertificateEvent
            {
                CertificateId = rowId.Value,
                Type = parsed,
                // Identifies a REQUEST, not a requester — enough to trace an incident through logs, and
                // nothing about the person holding the certificate.
                CorrelationId = correlation.CorrelationId,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Never throws. Verification and participant access must not fail because a counter could not
            // be written — the observation is worth less than the thing being observed.
            log.LogWarning(ex, "Could not record certificate event {Type} for {CertificateId}",
                type, certificateId);
        }
    }

    // ── Dashboard ───────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateDashboard>> DashboardAsync(
        Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists) return ServiceResult<CertificateDashboard>.Fail("not_found");
        // ViewAnalytics rather than ManageContent: seeing how an event's certificates are doing is a
        // weaker thing than being able to issue or withdraw them.
        if (!access.Can(EventPermission.ViewAnalytics))
            return ServiceResult<CertificateDashboard>.Fail("forbidden");

        var templates = await db.CertificateTemplates.CountAsync(t => t.EventId == eventId, ct);

        var batches = await db.CertificateBatches.AsNoTracking()
            .Where(b => b.EventId == eventId)
            .Select(b => b.Status)
            .ToListAsync(ct);

        var statuses = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .Select(c => new { c.Id, c.Status, c.RecipientId })
            .ToListAsync(ct);

        var certificateIds = statuses.Select(s => s.Id).ToList();

        // Per certificate, taking its latest attempt — a certificate that failed once and succeeded on a
        // resend has been sent, and counting it in both columns would make the totals not add up.
        var deliveries = await db.CertificateDeliveries.AsNoTracking()
            .Where(d => certificateIds.Contains(d.CertificateId))
            .Select(d => new { d.CertificateId, d.Status, d.CreatedAt })
            .ToListAsync(ct);
        var latest = deliveries
            .GroupBy(d => d.CertificateId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreatedAt).First().Status);

        var recipientIds = statuses.Select(s => s.RecipientId).Distinct().ToList();
        var addressable = await db.CertificateRecipients.AsNoTracking()
            .Where(r => recipientIds.Contains(r.Id) && (r.NormalizedEmail != null || r.UserId != null))
            .Select(r => r.Id)
            .ToListAsync(ct);
        var addressableSet = new HashSet<Guid>(addressable);

        var since = DateTime.UtcNow.Date.AddDays(-(TrailingDays - 1));
        var events = await db.CertificateEvents.AsNoTracking()
            .Where(e => certificateIds.Contains(e.CertificateId))
            .Select(e => new { e.Type, e.OccurredAt })
            .ToListAsync(ct);

        // Every day in the window, including the empty ones — a sparse series renders as a chart with
        // gaps that read as missing data rather than as quiet days.
        var byDay = events
            .Where(e => e.Type == CertificateEventType.Verified && e.OccurredAt >= since)
            .GroupBy(e => DateOnly.FromDateTime(e.OccurredAt.Date))
            .ToDictionary(g => g.Key, g => g.Count());

        var series = Enumerable.Range(0, TrailingDays)
            .Select(offset => DateOnly.FromDateTime(since.AddDays(offset)))
            .Select(day => new CertificateDailyCount(day, byDay.TryGetValue(day, out var n) ? n : 0))
            .ToList();

        return ServiceResult<CertificateDashboard>.Success(new CertificateDashboard(
            templates,
            batches.Count,
            batches.Count(s => s is CertificateBatchStatus.Approved or CertificateBatchStatus.Generating),
            statuses.Count(s => s.Status == IssuedCertificateStatus.Issued),
            statuses.Count(s => s.Status == IssuedCertificateStatus.Revoked),
            statuses.Count(s => s.Status == IssuedCertificateStatus.Superseded),
            latest.Count(kv => kv.Value == CertificateDeliveryStatus.Sent),
            latest.Count(kv => kv.Value == CertificateDeliveryStatus.Pending),
            latest.Count(kv => kv.Value is CertificateDeliveryStatus.Failed or CertificateDeliveryStatus.Bounced),
            statuses.Count(s => !addressableSet.Contains(s.RecipientId)),
            events.Count(e => e.Type == CertificateEventType.Verified),
            events.Count(e => e.Type == CertificateEventType.Viewed),
            series));
    }

    // ── Export ──────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateExport>> ExportAsync(
        Guid userId, Guid eventId, Guid? batchId, bool isAdmin, CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists) return ServiceResult<CertificateExport>.Fail("not_found");
        // The export carries participant names and email addresses, so it needs the stronger permission —
        // this is the personal data, not a count of it.
        if (!access.Can(EventPermission.ManageContent))
            return ServiceResult<CertificateExport>.Fail("forbidden");

        if (batchId is Guid id)
        {
            var owned = await db.CertificateBatches.AsNoTracking()
                .AnyAsync(b => b.Id == id && b.EventId == eventId, ct);
            if (!owned) return ServiceResult<CertificateExport>.Fail("not_found");
        }

        var query = db.IssuedCertificates.AsNoTracking().Where(c => c.EventId == eventId);
        if (batchId is Guid batch) query = query.Where(c => c.BatchId == batch);

        var total = await query.CountAsync(ct);
        var certificates = await query
            .OrderBy(c => c.IssuedAt)
            .Take(MaxExportRows)
            .ToListAsync(ct);

        var recipientIds = certificates.Select(c => c.RecipientId).Distinct().ToList();
        var recipients = await db.CertificateRecipients.AsNoTracking()
            .Where(r => recipientIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r, ct);

        var certificateIds = certificates.Select(c => c.Id).ToList();

        var deliveries = (await db.CertificateDeliveries.AsNoTracking()
                .Where(d => certificateIds.Contains(d.CertificateId))
                .ToListAsync(ct))
            .GroupBy(d => d.CertificateId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreatedAt).First());

        var revocations = (await db.CertificateRevocations.AsNoTracking()
                .Where(r => certificateIds.Contains(r.CertificateId))
                .ToListAsync(ct))
            .GroupBy(r => r.CertificateId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RevokedAt).First());

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', new[]
        {
            "certificate_id", "participant_name", "email", "status", "issued_at",
            "source_row", "verification_url", "delivery_status", "sent_at", "reason",
        }));

        foreach (var certificate in certificates)
        {
            recipients.TryGetValue(certificate.RecipientId, out var recipient);
            deliveries.TryGetValue(certificate.Id, out var delivery);
            revocations.TryGetValue(certificate.Id, out var revocation);

            csv.AppendLine(string.Join(',', new[]
            {
                Cell(certificate.CertificateId),
                Cell(recipient?.FullName),
                Cell(recipient?.Email),
                Cell(certificate.Status.ToString().ToLowerInvariant()),
                Cell(certificate.IssuedAt.ToString("O")),
                Cell(recipient?.SourceRowNumber?.ToString()),
                Cell(links.VerificationUrl(certificate.CertificateId)),
                Cell(delivery?.Status.ToString().ToLowerInvariant()),
                Cell(delivery?.SentAt?.ToString("O")),
                Cell(revocation?.Reason),
            }));
        }

        var name = batchId is null ? $"certificates-{eventId:N}.csv" : $"certificates-batch-{batchId:N}.csv";

        // A BOM, so Excel opens a UTF-8 file as UTF-8. Without it an Indian participant list comes back as
        // mojibake in the one application most organisers will open it with.
        //
        // Prepended explicitly: `new UTF8Encoding(true)` decides what GetPreamble() returns and has no
        // effect on GetBytes(), so constructing the encoding that way and expecting a BOM in the output
        // silently produces a file without one.
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(csv.ToString())).ToArray();

        return ServiceResult<CertificateExport>.Success(
            new CertificateExport(name, bytes, certificates.Count, total > certificates.Count));
    }

    /// <summary>Escapes one CSV cell, and neutralises spreadsheet formula injection.
    ///
    /// <para>Participant names come from a file the organiser uploaded, and a cell beginning <c>=</c>,
    /// <c>+</c>, <c>-</c>, <c>@</c> or a control character is executed as a formula when the export is
    /// reopened in Excel — the classic path to <c>=HYPERLINK</c> phishing or a shell call. Prefixing a
    /// single quote is the standard neutralisation: the value still reads correctly, and the spreadsheet
    /// treats it as text.</para>
    ///
    /// <para>Note the symmetry with the reader in Phase 6, which never <i>evaluates</i> a formula. Both
    /// halves of this module refuse to let an uploaded string become executable.</para></summary>
    private static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var text = value;
        if (text[0] is '=' or '+' or '-' or '@' or '\t' or '\r') text = "'" + text;

        // RFC 4180: quote anything containing a delimiter, a quote or a newline, doubling inner quotes.
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
