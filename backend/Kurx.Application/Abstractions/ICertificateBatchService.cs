namespace Kurx.Application.Abstractions;

/// <summary>
/// Generating certificates for a whole participant list (D-344, Phase 7).
///
/// <para>The confirmed flow is <b>upload → map → preview → approve → generate</b>, and the approval gate
/// is the point of it. A batch is the one operation here that is genuinely hard to undo: two hundred
/// certificates with a misspelled event name are two hundred revocations and two hundred apologies. So
/// nothing renders at scale until a human has looked at a real certificate — not a mockup, not the first
/// row rendered by different code — and said yes.</para>
///
/// <para><b>The full run is a background job.</b> Rendering hundreds of PDFs cannot happen inside a
/// request: the organiser would sit on a spinner for minutes and a dropped connection would leave the run
/// half-finished with nothing tracking it. The job is resumable and idempotent, so a crash mid-run costs
/// the rows in flight and nothing else.</para>
/// </summary>
public interface ICertificateBatchService
{
    /// <summary>Creates a run from an uploaded participant list, storing the file and the organiser's
    /// confirmed mapping and materialising one recipient per row.
    ///
    /// <para>Rows are validated here, before anything is rendered, and refused as a list of row numbers
    /// the organiser can find in their own spreadsheet.</para></summary>
    Task<ServiceResult<CertificateBatchView>> CreateAsync(
        Guid userId, Guid eventId, CertificateBatchInput input, bool isAdmin, CancellationToken ct = default);

    /// <summary>Renders the first few rows so the organiser can inspect real output before committing.</summary>
    Task<ServiceResult<CertificateBatchView>> GeneratePreviewAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Approves the preview and queues the full run. Pins the template version, so editing the
    /// design afterwards cannot retroactively change what this batch produces.</summary>
    Task<ServiceResult<CertificateBatchView>> ApproveAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<CertificateBatchView>> GetAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<CertificateBatchView>>> ListAsync(
        Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Stops a run that has not finished. Certificates already issued stay issued — they are real,
    /// and pretending otherwise would leave storage and the database disagreeing.</summary>
    Task<ServiceResult<CertificateBatchView>> CancelAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Runs the approved batch. Called by the background job, never from a request.
    ///
    /// <para>Idempotent and resumable: a row that already has a certificate is skipped, so a retry after a
    /// crash finishes the run rather than duplicating the part that succeeded.</para></summary>
    Task<CertificateBatchRunResult> RunAsync(Guid batchId, CancellationToken ct = default);
}

/// <param name="FileName">The uploaded file's name, used to choose a reader and kept for the record.</param>
/// <param name="Content">The raw bytes. Parsed here rather than re-uploaded, so what is validated is
/// exactly what is stored.</param>
/// <param name="ColumnMapping">Spreadsheet column name → template field key, as the organiser confirmed
/// it. Never inferred at this point — the guess happened at preview time and was accepted or corrected.</param>
public sealed record CertificateBatchInput(
    string Name,
    Guid TemplateId,
    string FileName,
    byte[] Content,
    IReadOnlyDictionary<string, string> ColumnMapping);

/// <param name="Issued">Certificates produced by this invocation.</param>
/// <param name="Skipped">Rows that already had a certificate — the idempotency path, and the reason a
/// retry is safe.</param>
/// <param name="Failed">Rows that could not be rendered. The run continues past them: one bad row must not
/// cost the other four hundred.</param>
public sealed record CertificateBatchRunResult(int Issued, int Skipped, int Failed);

/// <param name="PreviewUrls">Presigned and short-lived. Storage keys are not fetchable, and a URL must
/// never be stored on a row that outlives it.</param>
/// <param name="FailedRows">Row numbers that could not be rendered, in the organiser's coordinates.</param>
public sealed record CertificateBatchView(
    Guid Id,
    Guid EventId,
    Guid TemplateId,
    int TemplateVersion,
    string Name,
    string Status,
    string? SourceFileName,
    int RowCount,
    int IssuedCount,
    int PreviewCount,
    DateTime? ApprovedAt,
    DateTime CreatedAt,
    IReadOnlyList<string> PreviewUrls,
    IReadOnlyList<int> FailedRows);
