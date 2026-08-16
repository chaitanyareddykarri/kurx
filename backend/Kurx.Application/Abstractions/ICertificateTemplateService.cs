namespace Kurx.Application.Abstractions;

/// <summary>
/// Certificate designs: the uploaded artwork and the fields placed on it (D-355, Phase 3).
///
/// <para><b>Authority has two shapes here, not one.</b> A template attached to an event is governed by
/// <c>IEventAuthority</c> like any other event content. A template in a creator's reusable library belongs
/// to no event, so there is no event authority to consult — it is governed by ownership. Collapsing the
/// two would mean either library templates are unreachable or event templates are editable by their
/// original author after they have handed the event over.</para>
///
/// <para><b>A template that is not yours is <c>not_found</c>, never <c>forbidden</c></b> — a 403 confirms
/// the id exists, which is the enumeration leak D-018 closes everywhere on this platform.</para>
///
/// <para><b>No OCR anywhere in this contract.</b> Fields are placed by the creator. Detection arrives in a
/// later phase as a source of *suggestions*, and the editor has to remain fully usable without it.</para>
/// </summary>
public interface ICertificateTemplateService
{
    /// <summary>Templates attached to one event.</summary>
    Task<ServiceResult<IReadOnlyList<CertificateTemplateView>>> ListForEventAsync(
        Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>The caller's own reusable library — templates attached to no event.</summary>
    Task<ServiceResult<IReadOnlyList<CertificateTemplateView>>> ListLibraryAsync(
        Guid userId, CancellationToken ct = default);

    Task<ServiceResult<CertificateTemplateView>> GetAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);

    /// <param name="eventId">Null creates a library template owned by the caller.</param>
    Task<ServiceResult<CertificateTemplateView>> CreateAsync(
        Guid userId, Guid? eventId, CertificateTemplateInput input, bool isAdmin, CancellationToken ct = default);

    /// <summary>Partial update — a null field means "leave unchanged", the convention every other update
    /// body on this platform follows.</summary>
    Task<ServiceResult<CertificateTemplateView>> UpdateAsync(
        Guid userId, Guid templateId, CertificateTemplateInput input, bool isAdmin, CancellationToken ct = default);

    /// <summary>A presigned upload for the design artwork.
    ///
    /// <para>The key is minted under the template's own prefix and validated on the way back in, so a
    /// caller can never attach an object it was not issued a slot for.</para></summary>
    Task<ServiceResult<PresignedUpload>> PresignBackgroundAsync(
        Guid userId, Guid templateId, string contentType, long maxBytes, bool isAdmin, CancellationToken ct = default);

    /// <summary>Attaches an uploaded design after the bytes have landed. Separate from presigning because
    /// nothing may be recorded until the upload actually succeeded — a template pointing at a key that was
    /// never written renders as a blank page.</summary>
    Task<ServiceResult<CertificateTemplateView>> SetBackgroundAsync(
        Guid userId, Guid templateId, CertificateBackgroundInput input, bool isAdmin, CancellationToken ct = default);

    /// <summary>Replaces the whole field set in one call.
    ///
    /// <para>Whole-set rather than per-field CRUD because that is what an editor save actually is: the
    /// canvas holds the truth, and reconciling adds/moves/deletes into individual calls invents ordering
    /// problems that a single replace does not have.</para></summary>
    Task<ServiceResult<CertificateTemplateView>> ReplaceFieldsAsync(
        Guid userId, Guid templateId, IReadOnlyList<CertificateFieldInput> fields, bool isAdmin,
        CancellationToken ct = default);

    /// <summary>Archives a template. Never a hard delete: certificates already issued from it record its
    /// id and version, and a document someone holds must not lose its provenance because a list was
    /// tidied.</summary>
    Task<ServiceResult<bool>> ArchiveAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Saves a copy of a design into the caller's reusable library (D-355, Phase 13).
    ///
    /// <para>A <b>copy</b>, never a reference. The artwork bytes are duplicated under the library's own
    /// storage prefix, so the two designs share nothing: editing or archiving the event's copy cannot
    /// reach into the library's, and the prefix each object lives under still matches who owns it.</para></summary>
    /// <param name="name">A new name, or null to keep the original's.</param>
    Task<ServiceResult<CertificateTemplateView>> CopyToLibraryAsync(
        Guid userId, Guid templateId, string? name, bool isAdmin, CancellationToken ct = default);

    /// <summary>Uses a saved design on an event (D-355, Phase 13).
    ///
    /// <para>Also a copy, for the same reasons and one more: an event's certificates record the template
    /// id and version they were rendered from, and a template that could still be edited from a library
    /// elsewhere would make that provenance a lie.</para></summary>
    Task<ServiceResult<CertificateTemplateView>> CopyToEventAsync(
        Guid userId, Guid templateId, Guid eventId, string? name, bool isAdmin, CancellationToken ct = default);

    /// <summary>A coarse map of where the artwork already has something printed on it (D-355).
    ///
    /// <para>Exists so the editor can warn that a field is being placed over the design's own text —
    /// the failure that produces <c>[Recipient's Full Name] John Doe</c> on a finished certificate,
    /// where the placeholder is pixels in the uploaded image and the value is drawn on top of it.</para>
    ///
    /// <para>Computed once per template and checked in the browser as a field moves, rather than asked
    /// per drag: a round trip per mouse move would be unusable, and reading the artwork's pixels in the
    /// browser is blocked by canvas tainting whenever storage is a different origin.</para></summary>
    Task<ServiceResult<CertificateArtworkMap>> ArtworkMapAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);

    /// <summary>The artwork's own colour behind a region of the design (D-355).
    ///
    /// <para>What a field needs in order to cover printed text convincingly. Sampled rather than assumed
    /// white: certificate stock is cream, grey, navy or textured as often as not, and a white patch on
    /// any of those is a visible smear exactly where it was meant to be invisible.</para>
    ///
    /// <para>The modal colour of the region's <i>edges</i>, not its middle — the middle is the text being
    /// covered, and averaging it in would tint the patch toward the ink.</para></summary>
    Task<ServiceResult<string>> ArtworkColourAsync(
        Guid userId, Guid templateId, CertificateRegion region, bool isAdmin, CancellationToken ct = default);

    /// <summary>Asks whether the design's artwork has recognisable text on it (D-355, Phase 12).
    ///
    /// <para>The editor's OCR extension point. It goes through <see cref="ITextDetector"/>, which no build
    /// currently backs with an engine, so today this always answers "not available" — and that answer is a
    /// normal response, not an error. Manual placement is unaffected either way: nothing in the save,
    /// preview, approve or issue paths consults this.</para></summary>
    Task<ServiceResult<CertificateTextScan>> DetectBackgroundTextAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default);
}

/// <summary>What was found on a design, ready to become editable text.</summary>
/// <param name="Available">False when no engine is configured or the artwork could not be read. An empty
/// <see cref="Regions"/> with this false is NOT a claim that the design has no text on it.</param>
public sealed record CertificateTextScan(
    bool Available,
    IReadOnlyList<CertificateDetectedText> Regions,
    string? Reason = null);

/// <param name="Ground">The artwork's own colour behind this text, sampled from its margins. Carried with
/// the region so a creator does not have to cover each block by hand: the editor can place editable text
/// AND hide the printed characters underneath in one step, which is the whole point of detecting them.</param>
/// <param name="Confidence">0–1. Low-confidence lines are still returned — the creator can see the text
/// and judge it — but a caller may want to present them differently.</param>
public sealed record CertificateDetectedText(
    string Text,
    double X, double Y, double Width, double Height,
    double Confidence,
    string Ground,
    int Lines = 1);

/// <summary>A rectangle on the page, in percentages, matching how fields are stored.</summary>
public sealed record CertificateRegion(double X, double Y, double Width, double Height);

/// <summary>Which cells of a coarse grid over the artwork contain something.</summary>
/// <param name="Cells">Row-major, one character per cell: <c>1</c> printed, <c>0</c> clear. A string
/// rather than a boolean array because the grid is over a thousand cells and <c>"false,"</c> repeated a
/// thousand times is a lot of JSON to say very little.</param>
/// <param name="Analysed">False when there is no artwork to look at, or it could not be read. The editor
/// then warns about nothing — an absent map must not be mistaken for a clear one.</param>
public sealed record CertificateArtworkMap(int Columns, int Rows, string Cells, bool Analysed);

/// <param name="Name">1–200 characters.</param>
/// <param name="PageSize">A preset slug from <see cref="CertificatePageSizes"/> — `a4-portrait`,
/// `letter-landscape`, `12x16-portrait` … — or `custom`, in which case
/// <paramref name="PageWidthMm"/> and <paramref name="PageHeightMm"/> carry the size (D-361).</param>
/// <param name="PageWidthMm">Required with `custom`, ignored otherwise: a preset's dimensions come from
/// the catalogue, never from the caller, so a client cannot claim A4 is 500 mm wide.</param>
/// <param name="Status">`draft` | `ready`. Archiving goes through <c>ArchiveAsync</c>.</param>
public sealed record CertificateTemplateInput(
    string? Name = null,
    string? PageSize = null,
    string? Status = null,
    double? PageWidthMm = null,
    double? PageHeightMm = null);

/// <param name="StorageKey">The key that was presigned and successfully written.</param>
/// <param name="WidthPx">Natural pixel size of the upload, so the editor knows the aspect ratio without
/// downloading the image.</param>
public sealed record CertificateBackgroundInput(
    string StorageKey, string ContentType, int WidthPx, int HeightPx);

/// <summary>One element to place. Geometry is percentages of the page — never pixels — so a design laid
/// out in a browser lands identically on A4 at 300dpi.</summary>
public sealed record CertificateFieldInput(
    string Kind,
    string? FieldKey,
    string? Label,
    string? StaticText,
    double X, double Y, double Width, double Height,
    double Rotation = 0,
    int ZOrder = 0,
    bool IsRequired = false,
    bool IsMasking = false,
    /// <summary>True while this element is only a handle on text the artwork already prints: nothing is
    /// drawn and nothing is covered until the creator actually changes the words.</summary>
    bool MirrorsArtwork = false,
    string? BackgroundColor = null,
    string? FontFamily = null,
    double? FontSizePt = null,
    string? FontWeight = null,
    string? FontStyle = null,
    bool Underline = false,
    double? LineHeight = null,
    double? LetterSpacing = null,
    string? Color = null,
    string? HorizontalAlignment = null,
    string? VerticalAlignment = null);

public sealed record CertificateTemplateView(
    Guid Id,
    Guid? EventId,
    Guid OwnerUserId,
    string Name,
    string PageSize,
    /// <summary>The page in millimetres — authoritative (D-361). The client derives its aspect ratio from
    /// these rather than keeping its own name → size table.</summary>
    double PageWidthMm,
    double PageHeightMm,
    string Status,
    int Version,
    string? BackgroundStorageKey,
    /// <summary>A fetchable URL for the artwork, or null. A storage key is not a URL (D-302) — without
    /// this the editor cannot draw the design it is placing fields onto.</summary>
    string? BackgroundUrl,
    int? BackgroundWidthPx,
    int? BackgroundHeightPx,
    /// <summary>True once at least one certificate has been issued from this template. The editor uses it
    /// to warn that further edits produce a new version rather than changing what was already issued.</summary>
    bool HasIssuedCertificates,
    IReadOnlyList<CertificateFieldView> Fields,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CertificateFieldView(
    Guid Id,
    string Kind,
    string? FieldKey,
    string? Label,
    string? StaticText,
    double X, double Y, double Width, double Height,
    double Rotation,
    int ZOrder,
    bool IsRequired,
    bool IsMasking,
    bool MirrorsArtwork,
    string? BackgroundColor,
    string? FontFamily,
    double? FontSizePt,
    string? FontWeight,
    string? FontStyle,
    bool Underline,
    double? LineHeight,
    double? LetterSpacing,
    string? Color,
    string HorizontalAlignment,
    string VerticalAlignment);
