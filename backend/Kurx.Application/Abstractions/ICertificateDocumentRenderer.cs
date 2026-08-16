namespace Kurx.Application.Abstractions;

/// <summary>
/// Turns a certificate design plus one recipient's values into a document (D-355, Phase 4).
///
/// <para><b>One renderer, used by everything.</b> The preview an organiser inspects and the certificate a
/// recipient receives come through here, differing only in the values handed in. That is what makes
/// "the preview matches the output" true by construction rather than by two implementations agreeing to
/// stay in step — the failure mode being avoided is a preview that looks right and two hundred
/// certificates that do not.</para>
///
/// <para><b>No IO.</b> Images arrive as bytes, resolved by the caller. A renderer that fetched its own
/// artwork would make every unit test need a storage provider, and would put a network call inside a
/// loop that already runs once per recipient.</para>
///
/// <para>Named distinctly from the pre-existing <see cref="ICertificateRenderer"/>, which belongs to the
/// older hardcoded-layout path and is not part of this module.</para>
/// </summary>
public interface ICertificateDocumentRenderer
{
    /// <summary>The print-ready artefact. A4 at the design's orientation.</summary>
    Task<byte[]> RenderPdfAsync(
        CertificateDocument document, CertificateRenderData data, CancellationToken ct = default);

    /// <summary>A raster of the same render.</summary>
    /// <param name="dpi">150 for something shown in a browser, 300 for something that will be printed.
    /// A parameter rather than a constant because the two callers want genuinely different things: a
    /// preview at 300dpi is a slow download nobody zooms into, and a certificate at 150dpi is visibly
    /// soft on paper.</param>
    Task<byte[]> RenderPngAsync(
        CertificateDocument document, CertificateRenderData data, int dpi = 150, CancellationToken ct = default);
}

/// <summary>A design ready to render: the page, the uploaded artwork, and the elements over it.
///
/// <para>Deliberately not the EF entity. The renderer takes exactly what it needs — no navigation
/// properties, no database types — which keeps it a pure function and keeps the module's rendering half
/// independent of its persistence half.</para></summary>
/// <param name="PageSize">`a4-landscape` | `a4-portrait`.</param>
/// <param name="Background">The uploaded artwork's bytes, or null. Painted beneath everything, `contain`
/// so the whole design is visible: it is the one thing the creator actually made, and cropping or
/// stretching it silently damages that.</param>
public sealed record CertificateDocument(
    string PageSize,
    byte[]? Background,
    IReadOnlyList<CertificateRenderElement> Elements);

/// <summary>One element to paint. Geometry is percentages of the page — the document carries no
/// resolution, so the same design lands identically on a 150dpi preview and a 300dpi print.</summary>
/// <param name="Kind">`text` | `dynamicfield` | `image` | `qrcode`.</param>
/// <param name="FieldKey">For `dynamicfield`: which value to substitute.</param>
/// <param name="IsMasking">Whether this element exists to cover something printed into the artwork. The
/// renderer treats it identically to any other filled element — the flag records intent for the editor,
/// and the fill is what actually hides the pixels.</param>
public sealed record CertificateRenderElement(
    string Kind,
    string? FieldKey,
    string? StaticText,
    double X, double Y, double Width, double Height,
    double Rotation,
    int ZOrder,
    bool IsMasking,
    string? BackgroundColor,
    string? ImageKey,
    string? FontFamily,
    double? FontSizePt,
    string? FontWeight,
    string? Color,
    string HorizontalAlignment,
    string VerticalAlignment,
    /// <summary><c>italic</c> or null. Separate from weight because a design can be bold AND italic.</summary>
    string? FontStyle = null,
    bool Underline = false,
    /// <summary>Multiplier on the line box; null uses the renderer's default.</summary>
    double? LineHeight = null,
    /// <summary>Tracking in ems. Small positive values are what make a title read as a title.</summary>
    double? LetterSpacing = null,
    /// <summary>True means the artwork already shows these words: draw nothing, fill nothing. Covering
    /// unchanged text to redraw it identically erases the design's texture for no gain.</summary>
    bool MirrorsArtwork = false);

/// <summary>Everything a render needs beyond the design.</summary>
/// <param name="Values">Field key → value. A key with no value renders as its own placeholder rather than
/// as nothing, so a mistake is visible on the preview instead of arriving as a blank space on two hundred
/// certificates.</param>
/// <param name="Images">Storage key → bytes, resolved by the caller.</param>
/// <param name="QrPayload">What a QR element encodes — the verification URL. Null renders no QR at all
/// rather than a QR pointing nowhere, because a code that scans to a dead page is worse than no code.</param>
public sealed record CertificateRenderData(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, byte[]> Images,
    string? QrPayload = null);
