using System.Text.Json;
using Hangfire;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Generating certificates for a whole participant list (D-344, Phase 7).
///
/// <para><b>The approval gate is the design.</b> Everything before it is cheap and reversible — a parsed
/// file, some recipient rows, three sample renders. Everything after it is two hundred PDFs in object
/// storage and two hundred rows someone may already have been emailed. So the expensive half does not
/// start until a human has looked at real output, rendered by the same code that will render the rest,
/// and approved it.</para>
///
/// <para><b>The run is resumable.</b> Rows are issued one at a time, each committed on its own, and a row
/// that already has a certificate is skipped. A crash halfway through costs the row in flight; the retry
/// finishes the run instead of duplicating the half that worked. That property is enforced in the
/// database — a unique index on (batch, recipient) — not by this loop being careful, because a loop being
/// careful stops being true the moment two workers pick up the same job.</para>
/// </summary>
public class CertificateBatchService(
    KurxDbContext db,
    IEventAuthority authority,
    IStorage storage,
    ISpreadsheetService spreadsheets,
    ICertificateIssuingService issuing,
    ICertificateDocumentRenderer renderer,
    ICertificateVerificationLinks links,
    IBackgroundJobClient jobs,
    ILogger<CertificateBatchService> log) : ICertificateBatchService
{
    /// <summary>How many rows the preview renders. Enough to catch a layout that breaks on a long name;
    /// few enough that the organiser is not waiting on it.</summary>
    private const int PreviewRows = 3;

    /// <summary>Screen resolution for a preview — the same figure the single-certificate preview uses.</summary>
    private const int PreviewDpi = 150;

    /// <summary>How long a presigned preview link lives. Minutes, because it is only ever fetched by the
    /// page that just asked for it.</summary>
    private static readonly TimeSpan PreviewUrlTtl = TimeSpan.FromMinutes(15);

    // ── Create ──────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateBatchView>> CreateAsync(
        Guid userId, Guid eventId, CertificateBatchInput input, bool isAdmin, CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists) return Fail("not_found");
        if (!access.Can(EventPermission.ManageContent)) return Fail("forbidden");

        var name = (input.Name ?? "").Trim();
        if (name.Length is < 1 or > 120) return Fail("invalid_batch_name");

        var template = await db.CertificateTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == input.TemplateId, ct);

        // A template belonging to another event is not a template this event may render with — and it is
        // reported as not-found rather than forbidden, because its existence is not this caller's business.
        if (template is null || template.EventId != eventId) return Fail("template_not_found");
        if (template.BackgroundStorageKey is null) return Fail("background_required");

        var read = spreadsheets.Read(input.Content, input.FileName);
        if (!read.Ok) return Fail(read.Error!);
        var table = read.Value!;

        var mapping = Normalise(input.ColumnMapping);
        if (!mapping.Values.Contains("participant_name", StringComparer.Ordinal))
            return Fail("participant_name_not_mapped");

        // Every required field must have a column behind it before a single certificate is rendered. A run
        // that discovers this on row 180 has already produced 179 certificates it should not have.
        var required = await db.CertificateTemplateFields.AsNoTracking()
            .Where(f => f.TemplateId == template.Id && f.IsRequired && f.FieldKey != null)
            .Select(f => f.FieldKey!)
            .ToListAsync(ct);
        var unmapped = required.Where(k => !mapping.Values.Contains(k, StringComparer.Ordinal)).ToList();
        if (unmapped.Count > 0) return Fail("required_field_not_mapped");

        var rows = Resolve(table, mapping);

        // Row numbers are the organiser's coordinates, not ours: "row 47 is missing a name" has to point at
        // something they can find in their own spreadsheet.
        var blank = rows.Where(r => string.IsNullOrWhiteSpace(Name(r.Values)))
            .Select(r => r.RowNumber).Take(20).ToList();
        if (blank.Count > 0)
            return ServiceResult<CertificateBatchView>.Fail("rows_missing_participant_name");

        var batch = new CertificateBatch
        {
            EventId = eventId,
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            CreatedByUserId = userId,
            Name = name,
            Status = CertificateBatchStatus.Mapping,
            SourceFileName = input.FileName,
            ColumnMappingJson = JsonSerializer.Serialize(mapping),
            RowCount = rows.Count,
        };

        // The source file is stored so a finished run can be re-examined against the exact bytes it was
        // produced from — and so the job can re-read the rows without a second copy of them in the database.
        var extension = input.FileName.Contains('.') ? input.FileName[(input.FileName.LastIndexOf('.') + 1)..] : "csv";
        var sourceKey = CertificateStorageKeys.BatchSource(eventId, batch.Id, extension.ToLowerInvariant());
        await storage.PutAsync(sourceKey, input.Content, ContentTypeFor(extension), ct);
        batch.SourceFileStorageKey = sourceKey;

        db.CertificateBatches.Add(batch);
        db.CertificateRecipients.AddRange(rows.Select(r => new CertificateRecipient
        {
            EventId = eventId,
            BatchId = batch.Id,
            FullName = Name(r.Values),
            Email = Email(r.Values),
            NormalizedEmail = Email(r.Values)?.Trim().ToLowerInvariant(),
            SourceRowNumber = r.RowNumber,
        }));
        await db.SaveChangesAsync(ct);

        return await ProjectAsync(batch, ct);
    }

    // ── Preview ─────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateBatchView>> GeneratePreviewAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default)
    {
        var (batch, error) = await LoadAsync(userId, batchId, isAdmin, ct);
        if (error is not null) return Fail(error);

        if (batch!.Status is not (CertificateBatchStatus.Mapping or CertificateBatchStatus.PreviewReady))
            return Fail("batch_not_previewable");

        var prepared = await issuing.PrepareAsync(batch.TemplateId, ct);
        if (!prepared.Ok) return Fail(prepared.Error!);
        var plan = prepared.Value!;

        var rows = await ReadRowsAsync(batch, ct);
        if (rows is null) return Fail("source_file_unavailable");

        var rendered = 0;
        foreach (var row in rows.Take(PreviewRows))
        {
            // Rendered through the same plan and the same renderer the full run uses. A preview produced by
            // a second, simpler path would be a preview of something the organiser is not approving.
            var values = new Dictionary<string, string>(plan.EventValues, StringComparer.Ordinal)
            {
                ["participant_name"] = Name(row.Values),
                ["certificate_id"] = "PREVIEW",
                ["issue_date"] = DateTime.UtcNow.ToString("d MMMM yyyy"),
            };
            foreach (var (key, value) in row.Values) values[key] = value;

            var png = await renderer.RenderPngAsync(plan.Document,
                new CertificateRenderData(values, plan.Images, links.VerificationUrl("SAMPLE")), PreviewDpi, ct);

            await storage.PutAsync(
                CertificateStorageKeys.BatchPreview(batch.EventId, batch.Id, row.RowNumber), png, "image/png", ct);
            rendered++;
        }

        batch.PreviewCount = rendered;
        batch.Status = CertificateBatchStatus.PreviewReady;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await ProjectAsync(batch, ct);
    }

    // ── Approve ─────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateBatchView>> ApproveAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default)
    {
        var (batch, error) = await LoadAsync(userId, batchId, isAdmin, ct);
        if (error is not null) return Fail(error);

        // Approving something nobody has looked at defeats the entire gate.
        if (batch!.Status != CertificateBatchStatus.PreviewReady) return Fail("preview_required");
        if (batch.PreviewCount == 0) return Fail("preview_required");

        var template = await db.CertificateTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == batch.TemplateId, ct);
        if (template is null) return Fail("template_not_found");

        // Pinned at approval: editing the design afterwards must not retroactively change what this run
        // produces, and what was approved is what gets rendered.
        batch.TemplateVersion = template.Version;
        batch.ApprovedAt = DateTime.UtcNow;
        batch.Status = CertificateBatchStatus.Approved;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        jobs.Enqueue<Jobs.CertificateBatchJob>(j => j.RunAsync(batch.Id, CancellationToken.None));

        return await ProjectAsync(batch, ct);
    }

    // ── The run ─────────────────────────────────────────────────────────────────────────────────

    public async Task<CertificateBatchRunResult> RunAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await db.CertificateBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return new CertificateBatchRunResult(0, 0, 0);

        // Approval is re-checked here, not assumed from the fact that a job exists. A queued job outlives
        // the request that queued it, and a cancelled batch must not generate anything.
        if (batch.ApprovedAt is null) return new CertificateBatchRunResult(0, 0, 0);
        if (batch.Status is CertificateBatchStatus.Cancelled or CertificateBatchStatus.Completed)
            return new CertificateBatchRunResult(0, 0, 0);

        var prepared = await issuing.PrepareAsync(batch.TemplateId, ct);
        if (!prepared.Ok)
        {
            batch.Status = CertificateBatchStatus.Failed;
            batch.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new CertificateBatchRunResult(0, 0, 0);
        }

        var rows = await ReadRowsAsync(batch, ct);
        if (rows is null)
        {
            // The source file is gone. This is an infrastructure failure, not a bad batch — the run is
            // marked failed so it is visible, and nothing is silently reported as finished.
            batch.Status = CertificateBatchStatus.Failed;
            batch.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            log.LogError("Certificate batch {BatchId} source file is unreadable", batch.Id);
            return new CertificateBatchRunResult(0, 0, 0);
        }

        batch.Status = CertificateBatchStatus.Generating;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var recipients = await db.CertificateRecipients.AsNoTracking()
            .Where(r => r.BatchId == batch.Id && r.SourceRowNumber != null)
            .ToDictionaryAsync(r => r.SourceRowNumber!.Value, r => r, ct);

        var alreadyIssued = await db.IssuedCertificates.AsNoTracking()
            .Where(c => c.BatchId == batch.Id)
            .Select(c => c.RecipientId)
            .ToListAsync(ct);
        var issued = new HashSet<Guid>(alreadyIssued);

        int produced = 0, skipped = 0, failed = 0;
        var plan = prepared.Value!;

        foreach (var row in rows)
        {
            if (ct.IsCancellationRequested) break;
            if (!recipients.TryGetValue(row.RowNumber, out var recipient)) { failed++; continue; }

            // The idempotency path: a retry after a crash finishes the run rather than duplicating the
            // part that already succeeded.
            if (issued.Contains(recipient.Id)) { skipped++; continue; }

            try
            {
                var result = await issuing.IssuePreparedAsync(
                    plan, recipient.Id, batch.Id, recipient.FullName, row.Values, ct);

                if (result.Ok) produced++;
                else
                {
                    failed++;
                    log.LogWarning("Certificate batch {BatchId} row {Row} refused: {Error}",
                        batch.Id, row.RowNumber, result.Error);
                }
            }
            catch (Exception ex)
            {
                // One bad row must not cost the other four hundred. It is counted, logged and left for the
                // organiser to see rather than aborting a run that is otherwise succeeding.
                failed++;
                log.LogError(ex, "Certificate batch {BatchId} row {Row} failed", batch.Id, row.RowNumber);
            }

            // Tracked entities accumulate across thousands of rows and nothing here needs the previous
            // ones; without this a large run's memory grows with every certificate.
            if ((produced + failed) % 50 == 0) db.ChangeTracker.Clear();
        }

        batch = await db.CertificateBatches.FirstAsync(b => b.Id == batchId, ct);
        var total = await db.IssuedCertificates.CountAsync(c => c.BatchId == batch.Id, ct);

        // Completed means every row produced a certificate. Anything less stays Failed and visible —
        // reporting a partial run as complete is how nobody finds out four people never got theirs.
        batch.Status = total >= batch.RowCount ? CertificateBatchStatus.Completed : CertificateBatchStatus.Failed;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Certificate batch {BatchId}: {Issued} issued, {Skipped} skipped, {Failed} failed",
            batch.Id, produced, skipped, failed);

        return new CertificateBatchRunResult(produced, skipped, failed);
    }

    // ── Read ────────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateBatchView>> GetAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default)
    {
        var (batch, error) = await LoadAsync(userId, batchId, isAdmin, ct);
        return error is not null ? Fail(error) : await ProjectAsync(batch!, ct);
    }

    public async Task<ServiceResult<IReadOnlyList<CertificateBatchView>>> ListAsync(
        Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists) return ServiceResult<IReadOnlyList<CertificateBatchView>>.Fail("not_found");
        if (!access.Can(EventPermission.ManageContent))
            return ServiceResult<IReadOnlyList<CertificateBatchView>>.Fail("forbidden");

        var batches = await db.CertificateBatches.AsNoTracking()
            .Where(b => b.EventId == eventId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        var views = new List<CertificateBatchView>(batches.Count);
        foreach (var batch in batches) views.Add((await ProjectAsync(batch, ct, withPreviews: false)).Value!);

        return ServiceResult<IReadOnlyList<CertificateBatchView>>.Success(views);
    }

    public async Task<ServiceResult<CertificateBatchView>> CancelAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default)
    {
        var (batch, error) = await LoadAsync(userId, batchId, isAdmin, ct);
        if (error is not null) return Fail(error);

        if (batch!.Status is CertificateBatchStatus.Completed or CertificateBatchStatus.Cancelled)
            return Fail("batch_not_cancellable");

        // Certificates already issued stay issued. They exist, they are signed, and some may already have
        // been sent — pretending otherwise would leave the database disagreeing with storage and with the
        // recipients' inboxes. Revoking them is a separate, deliberate act.
        batch.Status = CertificateBatchStatus.Cancelled;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await ProjectAsync(batch, ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private sealed record ResolvedRow(int RowNumber, IReadOnlyDictionary<string, string> Values);

    private static ServiceResult<CertificateBatchView> Fail(string error) =>
        ServiceResult<CertificateBatchView>.Fail(error);

    private static string Name(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue("participant_name", out var v) ? v.Trim() : "";

    private static string? Email(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue("email", out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static Dictionary<string, string> Normalise(IReadOnlyDictionary<string, string> mapping)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (column, field) in mapping ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(column) || string.IsNullOrWhiteSpace(field)) continue;
            // One column per field: two columns feeding one placeholder means the second silently wins at
            // render time, which is not something anyone intends.
            if (result.Values.Contains(field, StringComparer.Ordinal)) continue;
            result[column] = field.Trim();
        }
        return result;
    }

    /// <summary>Turns a parsed sheet into rows of field values. Row numbers are 1-based over the DATA rows,
    /// matching what the organiser sees under their header.</summary>
    private static List<ResolvedRow> Resolve(SpreadsheetTable table, IReadOnlyDictionary<string, string> mapping)
    {
        var rows = new List<ResolvedRow>(table.Rows.Count);

        for (var i = 0; i < table.Rows.Count; i++)
        {
            var cells = table.Rows[i];
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var c = 0; c < table.Columns.Count; c++)
            {
                if (!mapping.TryGetValue(table.Columns[c], out var field)) continue;
                // A short row yields an empty string rather than nothing at all: a ragged tail is normal in
                // a hand-edited sheet, and a required field being empty is a decision made later.
                values[field] = c < cells.Count ? cells[c] : "";
            }

            rows.Add(new ResolvedRow(i + 1, values));
        }

        return rows;
    }

    /// <summary>Re-reads the batch's own source file. The rows are not duplicated into the database —
    /// the stored file IS the record of what this run was asked to produce.</summary>
    private async Task<List<ResolvedRow>?> ReadRowsAsync(CertificateBatch batch, CancellationToken ct)
    {
        if (batch.SourceFileStorageKey is null || batch.ColumnMappingJson is null) return null;

        byte[] bytes;
        try
        {
            if (!await storage.ExistsAsync(batch.SourceFileStorageKey, ct)) return null;
            bytes = await storage.GetAsync(batch.SourceFileStorageKey, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Certificate batch {BatchId} source file could not be read", batch.Id);
            return null;
        }

        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.ColumnMappingJson)
            ?? new Dictionary<string, string>();

        var read = spreadsheets.Read(bytes, batch.SourceFileName ?? "participants.csv");
        return read.Ok ? Resolve(read.Value!, mapping) : null;
    }

    private async Task<(CertificateBatch? Batch, string? Error)> LoadAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct)
    {
        var batch = await db.CertificateBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return (null, "not_found");

        var access = await authority.ResolveAsync(userId, batch.EventId, isAdmin, ct);
        // D-018: a batch on an event you may not see is indistinguishable from one that does not exist.
        if (!access.EventExists || !access.Can(EventPermission.ManageContent)) return (null, "not_found");

        return (batch, null);
    }

    private async Task<ServiceResult<CertificateBatchView>> ProjectAsync(
        CertificateBatch batch, CancellationToken ct, bool withPreviews = true)
    {
        var issuedCount = await db.IssuedCertificates.CountAsync(c => c.BatchId == batch.Id, ct);

        var previews = new List<string>();
        if (withPreviews && batch.PreviewCount > 0)
        {
            for (var row = 1; row <= batch.PreviewCount; row++)
            {
                var key = CertificateStorageKeys.BatchPreview(batch.EventId, batch.Id, row);
                try
                {
                    if (await storage.ExistsAsync(key, ct))
                        previews.Add(await storage.PresignGetAsync(key, PreviewUrlTtl, ct));
                }
                catch (Exception ex)
                {
                    // A preview that cannot be linked is a missing thumbnail, not a failed request.
                    log.LogWarning(ex, "Preview link failed for batch {BatchId} row {Row}", batch.Id, row);
                }
            }
        }

        // Which rows produced nothing, in the organiser's coordinates. Derived rather than stored, so it
        // cannot drift from what actually exists.
        var failedRows = new List<int>();
        if (batch.Status is CertificateBatchStatus.Completed or CertificateBatchStatus.Failed)
        {
            var issuedRecipients = await db.IssuedCertificates.AsNoTracking()
                .Where(c => c.BatchId == batch.Id).Select(c => c.RecipientId).ToListAsync(ct);

            failedRows = await db.CertificateRecipients.AsNoTracking()
                .Where(r => r.BatchId == batch.Id && r.SourceRowNumber != null
                            && !issuedRecipients.Contains(r.Id))
                .Select(r => r.SourceRowNumber!.Value)
                .OrderBy(r => r)
                .Take(100)
                .ToListAsync(ct);
        }

        return ServiceResult<CertificateBatchView>.Success(new CertificateBatchView(
            batch.Id, batch.EventId, batch.TemplateId, batch.TemplateVersion, batch.Name,
            batch.Status.ToString().ToLowerInvariant(), batch.SourceFileName, batch.RowCount,
            issuedCount, batch.PreviewCount, batch.ApprovedAt, batch.CreatedAt, previews, failedRows));
    }

    private static string ContentTypeFor(string extension) =>
        extension.Equals("xlsx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
}
