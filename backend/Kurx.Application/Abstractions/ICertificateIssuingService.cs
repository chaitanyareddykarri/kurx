namespace Kurx.Application.Abstractions;

/// <summary>
/// Producing one certificate (D-344, Phase 4).
///
/// <para><b>Preview and issue share a single path.</b> Both assemble the same
/// <see cref="CertificateDocument"/> from the same template and hand it to the same renderer; they differ
/// only in the values substituted and in whether anything is persisted. That identity is what the
/// confirmed "preview must match the final output" requirement actually rests on — two code paths that
/// merely agree today would drift the first time one is touched.</para>
///
/// <para>Bulk generation arrives in a later phase. It will call the same assembly and the same renderer,
/// from a background job, for the same reason.</para>
/// </summary>
public interface ICertificateIssuingService
{
    /// <summary>Renders the template with sample values, without issuing anything or writing any row.
    ///
    /// <para>Sample rather than blank: a preview is judged on whether the layout survives a real name, and
    /// an empty box always fits.</para></summary>
    Task<ServiceResult<CertificatePreview>> PreviewAsync(
        Guid userId, Guid templateId, string format, bool isAdmin, CancellationToken ct = default);

    /// <summary>Issues one certificate: allocates its id, renders the PDF and a print-resolution PNG,
    /// stores both, and records the row.</summary>
    Task<ServiceResult<IssuedCertificateView>> IssueAsync(
        Guid userId, Guid templateId, IssueCertificateInput input, bool isAdmin, CancellationToken ct = default);

    /// <summary>Assembles a template ONCE for a run of many certificates (D-344, Phase 7).
    ///
    /// <para>The assembly is the expensive part — it reads the artwork out of storage and resolves every
    /// field — and it produces the same document for every row. Doing it per row would mean one storage
    /// fetch of a multi-megabyte background per certificate, which is the difference between a batch that
    /// takes a minute and one that takes an hour.</para>
    ///
    /// <para>Authority is <b>not</b> checked here: this is called by the background job, after approval,
    /// where there is no user. The caller owns that check — which is exactly why approval is a gate.</para></summary>
    Task<ServiceResult<CertificateRenderPlan>> PrepareAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>Issues one certificate against a prepared plan, for a recipient row that already exists.
    ///
    /// <para>Renders through the same document and the same renderer as <see cref="IssueAsync"/>, because
    /// a certificate produced in bulk and one produced singly must be the same artefact.</para></summary>
    Task<ServiceResult<IssuedCertificateView>> IssuePreparedAsync(
        CertificateRenderPlan plan, Guid recipientId, Guid? batchId, string recipientName,
        IReadOnlyDictionary<string, string> values, CancellationToken ct = default);
}

/// <summary>A template assembled and ready to render, reused across every row of a batch.
///
/// <para>Immutable and passed back in rather than cached inside the service: hidden state keyed by
/// template id would be a correctness problem the first time two batches ran at once against templates
/// that had been edited in between.</para></summary>
/// <param name="EventValues">The event's own facts, already resolved. Identical for every row.</param>
/// <param name="RequiredKeys">Field keys that refuse to render empty.</param>
public sealed record CertificateRenderPlan(
    Guid TemplateId,
    int TemplateVersion,
    Guid EventId,
    CertificateDocument Document,
    IReadOnlyDictionary<string, byte[]> Images,
    IReadOnlyDictionary<string, string> EventValues,
    IReadOnlySet<string> RequiredKeys);

/// <param name="RecipientName">Printed as given. The organiser's spelling is authoritative — nothing here
/// title-cases or otherwise corrects it.</param>
/// <param name="Email">Optional: a certificate may be issued to someone with no account and no address.</param>
/// <param name="Values">Field key → value for everything else the design places.</param>
public sealed record IssueCertificateInput(
    string RecipientName,
    string? Email,
    IReadOnlyDictionary<string, string> Values);

/// <param name="ContentType">`application/pdf` or `image/png`.</param>
public sealed record CertificatePreview(byte[] Bytes, string ContentType, string FileName);

public sealed record IssuedCertificateView(
    Guid Id,
    string CertificateId,
    Guid EventId,
    Guid TemplateId,
    int TemplateVersion,
    string RecipientName,
    string Status,
    DateTime IssuedAt,
    /// <summary>Presigned and short-lived. A storage key is not fetchable, and a URL must never be stored
    /// on a row that outlives it.</summary>
    string? PdfUrl,
    string? PngUrl);
