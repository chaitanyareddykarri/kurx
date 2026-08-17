using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <summary>
/// The page sizes a certificate can be printed on (D-361).
///
/// <para><b>One table, on the server.</b> The size used to live in two places — the renderer's
/// name → millimetre switch and the browser's name → aspect map — which had to agree and had no way of
/// saying so. This is the only place a preset's dimensions are written down; the browser is told them
/// over the wire rather than keeping its own copy.</para>
///
/// <para>Millimetres throughout, including for the imperial sizes, because the renderer measures pages in
/// millimetres and converting once here beats converting at every call site. 1 in = 25.4 mm exactly, so
/// US Letter is 215.9 × 279.4 — held to two decimals, which is finer than any printer resolves and keeps
/// binary floating-point noise out of the wire and the database.</para>
/// </summary>
public static class CertificatePageSizes
{
    /// <param name="Size">The enum member, which is what the database stores.</param>
    /// <param name="Slug">The wire value — `a4-portrait`, `letter-landscape`, `custom`.</param>
    /// <param name="Family">Groups the two orientations of one paper, so the UI can offer a paper and an
    /// orientation rather than fourteen unrelated entries.</param>
    /// <param name="Label">What a person calls it. "A4", not "A4Portrait".</param>
    /// <param name="Landscape">Which orientation this member is.</param>
    public sealed record Preset(
        CertificatePageSize Size, string Slug, string Family, string Label,
        bool Landscape, double WidthMm, double HeightMm);

    private const double Inch = 25.4;

    /// <summary>Portrait dimensions per family; the landscape member is the same paper turned over.</summary>
    private static readonly (string Family, string Label, CertificatePageSize Portrait, CertificatePageSize Landscape, double PortraitWidthMm, double PortraitHeightMm)[] Papers =
    [
        ("a4",     "A4",       CertificatePageSize.A4Portrait,        CertificatePageSize.A4Landscape,        210,          297),
        ("a5",     "A5",       CertificatePageSize.A5Portrait,        CertificatePageSize.A5Landscape,        148,          210),
        ("letter", "Letter",   CertificatePageSize.LetterPortrait,    CertificatePageSize.LetterLandscape,    8.5 * Inch,   11 * Inch),
        ("legal",  "Legal",    CertificatePageSize.LegalPortrait,     CertificatePageSize.LegalLandscape,     8.5 * Inch,   14 * Inch),
        ("8x10",   "8 × 10 in",  CertificatePageSize.Photo8x10Portrait,  CertificatePageSize.Photo8x10Landscape,  8 * Inch,  10 * Inch),
        ("11x14",  "11 × 14 in", CertificatePageSize.Photo11x14Portrait, CertificatePageSize.Photo11x14Landscape, 11 * Inch, 14 * Inch),
        ("12x16",  "12 × 16 in", CertificatePageSize.Photo12x16Portrait, CertificatePageSize.Photo12x16Landscape, 12 * Inch, 16 * Inch),
    ];

    /// <summary>Two decimals. `8.5 * 25.4` is 215.89999999999998 in binary floating point, and that
    /// reaches the wire, the database and any log that prints it. A tenth of a millimetre is already
    /// finer than any printer resolves, so rounding costs nothing and removes the noise everywhere
    /// downstream at once.</summary>
    private static double Mm(double value) => Math.Round(value, 2);

    public static readonly IReadOnlyList<Preset> All =
        Papers.SelectMany(p => new[]
        {
            new Preset(p.Portrait,  $"{p.Family}-portrait",  p.Family, p.Label, false, Mm(p.PortraitWidthMm), Mm(p.PortraitHeightMm)),
            new Preset(p.Landscape, $"{p.Family}-landscape", p.Family, p.Label, true,  Mm(p.PortraitHeightMm), Mm(p.PortraitWidthMm)),
        }).ToList();

    /// <summary>Custom pages are clamped to this range per side (D-361). Below the floor a verification QR
    /// cannot be printed large enough to scan; above the ceiling a single page at 300dpi is a
    /// multi-hundred-megabyte raster, and the render endpoint is reachable by request.</summary>
    public const double MinCustomMm = 50;
    public const double MaxCustomMm = 1000;

    public static Preset? BySlug(string? slug) =>
        All.FirstOrDefault(p => string.Equals(p.Slug, slug?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static Preset? BySize(CertificatePageSize size) => All.FirstOrDefault(p => p.Size == size);

    /// <summary>The wire value for a stored size. <c>custom</c> carries no dimensions of its own — the
    /// template's millimetres are the answer, and this only names which case it is.</summary>
    public static string SlugFor(CertificatePageSize size) =>
        size == CertificatePageSize.Custom ? "custom" : (BySize(size)?.Slug ?? "a4-landscape");

    /// <summary>Millimetres for a stored size, for rows written before the dimensions were stored
    /// alongside. Never used for <see cref="CertificatePageSize.Custom"/>, which has no fallback by
    /// definition — a custom page that lost its dimensions is not recoverable from its name.</summary>
    public static (double WidthMm, double HeightMm) FallbackMillimetres(CertificatePageSize size)
    {
        var preset = BySize(size);
        return preset is null ? (297, 210) : (preset.WidthMm, preset.HeightMm);
    }

    /// <summary>Validates a custom page. Returns an error code, or null when the page is acceptable.
    ///
    /// <para>NaN and infinity are rejected explicitly: they are not caught by a range comparison — every
    /// comparison against NaN is false — so a payload carrying one would sail through a naive check and
    /// reach QuestPDF as a page of undefined size.</para></summary>
    public static string? ValidateCustom(double widthMm, double heightMm)
    {
        foreach (var side in new[] { widthMm, heightMm })
        {
            if (double.IsNaN(side) || double.IsInfinity(side)) return "invalid_page_size";
            if (side < MinCustomMm || side > MaxCustomMm) return "page_size_out_of_range";
        }
        return null;
    }
}
