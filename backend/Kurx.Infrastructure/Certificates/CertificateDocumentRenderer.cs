using Kurx.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Renders a certificate with QuestPDF (D-355, Phase 4).
///
/// <para><b>Percentages become points here, once.</b> The document carries no resolution; this is where a
/// page size is chosen and every coordinate multiplied by it. Nothing upstream knows about points and
/// nothing downstream sees percentages.</para>
///
/// <para><b>PNG comes from the same document, not from the PDF.</b> QuestPDF rasterises its own layout, so
/// the two outputs are the same render at different resolutions. Rasterising the PDF instead would route
/// this through <c>IDocumentRasterizer</c>, whose only implementation is a stub returning a blank 1×1
/// image — every certificate PNG would be an empty square, and the stub's own log line says so.</para>
/// </summary>
public class CertificateDocumentRenderer(IQrCodeGenerator qr) : ICertificateDocumentRenderer
{
    static CertificateDocumentRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>The page in millimetres (D-361). Physical size, so a certificate prints at the size it
    /// was designed for rather than whatever the printer guesses.
    ///
    /// <para>Taken from the document itself. The name → millimetre switch this replaced could only ever
    /// answer for the sizes it had been told about, which is why a custom page was not expressible; the
    /// slug is consulted only as a fallback for a document written before the dimensions travelled with
    /// it.</para></summary>
    private static (float W, float H) PageMillimetres(CertificateDocument document)
    {
        if (document.PageWidthMm > 0 && document.PageHeightMm > 0)
            return ((float)document.PageWidthMm, (float)document.PageHeightMm);
        return document.PageSize?.Trim().ToLowerInvariant() == "a4-portrait" ? (210f, 297f) : (297f, 210f);
    }

    private const float MmToPoints = 72f / 25.4f;

    /// <summary>Clamped because the caller is ultimately a request parameter: an A4 page at 4000dpi is a
    /// multi-gigabyte allocation.</summary>
    private const int MinDpi = 72;
    private const int MaxDpi = 600;

    public Task<byte[]> RenderPdfAsync(
        CertificateDocument document, CertificateRenderData data, CancellationToken ct = default) =>
        Task.FromResult(Build(document, data).GeneratePdf());

    public Task<byte[]> RenderPngAsync(
        CertificateDocument document, CertificateRenderData data, int dpi = 150, CancellationToken ct = default) =>
        Task.FromResult(Build(document, data)
            .GenerateImages(new ImageGenerationSettings
            {
                ImageFormat = ImageFormat.Png,
                RasterDpi = Math.Clamp(dpi, MinDpi, MaxDpi),
            })
            .First());

    /// <summary>The single layout both outputs come from. Its existence is the guarantee that a preview
    /// cannot diverge from the certificate it previews.</summary>
    private IDocument Build(CertificateDocument document, CertificateRenderData data)
    {
        var (mmW, mmH) = PageMillimetres(document);
        var ptW = mmW * MmToPoints;
        var ptH = mmH * MmToPoints;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(mmW, mmH, Unit.Millimetre);
                page.Margin(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri));

                page.Content().Layers(layers =>
                {
                    // The primary layer is the uploaded artwork; everything else is painted over it in
                    // ascending depth. Depth is the ordering, and it is dense and tie-free by the time it
                    // reaches here (the editor renumbers), so no two elements can claim the same plane.
                    layers.PrimaryLayer().Element(e => DrawBackground(e, document.Background, mmW / mmH));

                    foreach (var element in document.Elements.OrderBy(x => x.ZOrder))
                        layers.Layer().Element(e => Draw(e, element, data, ptW, ptH));
                });
            });
        });
    }

    /// <summary>Paints the uploaded design.
    ///
    /// <para><c>contain</c>, never a stretch. The upload IS the design, so reshaping it to fill the page
    /// damages the only artefact the creator authored — and the damage is invisible until a printed
    /// certificate comes back with an oval seal on it. Centred so any remainder is shared between both
    /// edges rather than pooling at one corner, where it reads as a misplaced design rather than a
    /// margin.</para></summary>
    private static void DrawBackground(IContainer root, byte[]? background, float pageAspect)
    {
        if (background is null) { root.Container(); return; }
        root.AlignCenter().AlignMiddle().Image(background).FitArea();
    }

    private void Draw(
        IContainer root, CertificateRenderElement element, CertificateRenderData data, float ptW, float ptH)
    {
        // An element that mirrors the artwork is a handle in the editor, not a mark on the page. The
        // design already prints these words; covering them to redraw them identically would erase the
        // watermark and texture under the box and leave a smooth rectangle with hard edges — visible
        // exactly as "this was edited". Nothing here, deliberately.
        if (element.MirrorsArtwork) { root.Container(); return; }

        var x = (float)(element.X / 100.0) * ptW;
        var y = (float)(element.Y / 100.0) * ptH;
        var w = Math.Max(1f, (float)(element.Width / 100.0) * ptW);
        var h = Math.Max(1f, (float)(element.Height / 100.0) * ptH);

        var box = root.AlignLeft().AlignTop().OffsetX(x).OffsetY(y).Width(w).Height(h);

        // Quarter turns. The editor offers ±90° and nothing else, so the set of rotations a design can
        // carry is {0, 90, 180, 270} — and QuestPDF rotates in quarter turns. An arbitrary angle would
        // silently round here, which is worse than not offering it.
        var turns = ((int)Math.Round(element.Rotation / 90.0) % 4 + 4) % 4;
        for (var i = 0; i < turns; i++) box = box.RotateRight();

        // A quarter turn swaps which dimension the text runs along, so the width a line has to fit into
        // is the box's height on an odd number of turns.
        var (textW, textH) = turns % 2 == 1 ? (h, w) : (w, h);

        // The element's own ground, painted before its content. This is also the whole mechanism behind
        // cover-and-replace: a masking element is an ordinary filled box that happens to sit over
        // printed text, and the fill is what hides it.
        if (!string.IsNullOrWhiteSpace(element.BackgroundColor))
            box = box.Background(Hex(element.BackgroundColor, Colors.Transparent));

        switch (element.Kind?.Trim().ToLowerInvariant())
        {
            case "text":
                DrawText(box, element, element.StaticText ?? "", textW, textH);
                break;

            case "dynamicfield":
            {
                // A key with no value renders as its own placeholder rather than as nothing, so a
                // mistake shows on the preview instead of as a blank space on the finished document.
                var value = data.Values.TryGetValue(element.FieldKey ?? "", out var v) && !string.IsNullOrEmpty(v)
                    ? v
                    : $"{{{element.FieldKey}}}";
                DrawText(box, element, value, textW, textH);
                break;
            }

            case "qrcode":
                // No payload means no QR. A code that scans to a dead page is worse than no code: it
                // invites a verifier to conclude the certificate is fake.
                if (!string.IsNullOrWhiteSpace(data.QrPayload))
                    box.Image(qr.GeneratePng(data.QrPayload, 600)).FitArea();
                break;

            case "image":
            {
                if (element.ImageKey is null || !data.Images.TryGetValue(element.ImageKey, out var bytes)) break;
                // An element whose upload never landed draws nothing rather than an empty bordered box,
                // which would print as a visible defect.
                box.AlignCenter().AlignMiddle().Image(bytes).FitArea();
                break;
            }
        }
    }

    /// <summary>Draws text inside its box.
    ///
    /// <para><b>Text is never silently dropped.</b> The box is a hard <c>Height</c>, and QuestPDF omits
    /// content that does not fit one — so a 40pt name in a box 30pt tall rendered as nothing at all. The
    /// size is therefore clamped to what the box can hold. Text slightly smaller than designed is visible
    /// and obviously wrong; text that is absent looks like a design choice, and nobody notices until the
    /// certificates are sent.</para></summary>
    /// <param name="boxWidthPoints">Used to shrink a single-line field that would otherwise overrun its
    /// box (D-386). A real badge printed "Ananya Krishnamurthy-Venkataraghavan" as
    /// "Ananya Krishnamurthy-": the name wrapped, and the second line fell outside a box only tall enough
    /// for one.</param>
    private static void DrawText(
        IContainer box, CertificateRenderElement element, string value,
        float boxWidthPoints, float boxHeightPoints)
    {
        var aligned = (element.VerticalAlignment?.Trim().ToLowerInvariant()) switch
        {
            "top" => box.AlignTop(),
            "bottom" => box.AlignBottom(),
            _ => box.AlignMiddle(),
        };
        aligned = (element.HorizontalAlignment?.Trim().ToLowerInvariant()) switch
        {
            "center" => aligned.AlignCenter(),
            "right" => aligned.AlignRight(),
            _ => aligned.AlignLeft(),
        };

        var span = aligned.Text(value);
        if (element.LineHeight is { } leading && leading > 0) span.LineHeight((float)leading);
        var requested = (float)(element.FontSizePt ?? 16);
        var bold = string.Equals(element.FontWeight, "bold", StringComparison.OrdinalIgnoreCase);
        span.FontSize(FitSize(requested, value, boxWidthPoints, boxHeightPoints, bold));
        span.FontColor(Hex(element.Color, Colors.Black));
        if (bold) span.Bold();
        if (string.Equals(element.FontStyle, "italic", StringComparison.OrdinalIgnoreCase)) span.Italic();
        if (element.Underline) span.Underline();

        // Tracking is expressed in ems by the editor, because that is the unit that survives a font-size
        // change; QuestPDF takes points, so it is resolved against the size actually being drawn.
        if (element.LetterSpacing is { } tracking && Math.Abs(tracking) > 0.0001)
            span.LetterSpacing((float)tracking);
        span.FontFamily((element.FontFamily?.Trim().ToLowerInvariant()) switch
        {
            "serif" => Fonts.TimesNewRoman,
            "mono" => Fonts.Consolas,
            _ => Fonts.Calibri,
        });
    }

    /// <summary>The point size to actually draw at. **Only ever reduces** — a design whose type already
    /// fits renders exactly as authored.
    ///
    /// <para><b>Height</b> is the long-standing rule: 0.8 leaves room for the line box around the glyphs,
    /// and above that QuestPDF starts dropping the line.</para>
    ///
    /// <para><b>Width applies only to a box that cannot hold two lines</b> (D-386). Such a box is a
    /// single-line field by construction — a name, a role, a card number — and QuestPDF wraps rather than
    /// shrinks, so the overflow is clipped and invisible until it is on paper. A tall box is left alone:
    /// wrapping is what a certificate's body paragraph is *for*, and shrinking it to one line would be the
    /// worse bug.</para>
    ///
    /// <para>The width estimate is deliberately crude — mean advance width as a fraction of the em, 0.5
    /// for Calibri and 0.55 bold. Measuring exactly would mean loading the font here and asking it, which
    /// buys precision this does not need: the only job is to stop a long value overrunning, and erring
    /// small costs a point or two of type on the handful of values that trip it.</para></summary>
    private static float FitSize(float requested, string value, float boxWidth, float boxHeight, bool bold)
    {
        // 0.8 leaves room for the line box around the glyphs; above that QuestPDF starts dropping the line.
        var size = Math.Min(requested, boxHeight * 0.8f);

        if (!string.IsNullOrEmpty(value) && boxWidth > 0 && boxHeight < size * TwoLines)
        {
            var perChar = bold ? 0.55f : 0.50f;
            size = Math.Min(size, boxWidth / (perChar * value.Length));
        }

        return Math.Max(4f, size);
    }

    /// <summary>How tall a box must be, relative to its type, before it is treated as able to wrap: two
    /// lines at a ~1.25 line box.
    ///
    /// <para>Measured against the size actually being drawn rather than the authored one, so the test does
    /// not flip on a one-point change. The badge case is why: a name field 8% tall on a lanyard is 31.7pt,
    /// and a bare <c>height &lt; size × 2</c> caught it at 16pt type and missed it at 15pt — the same
    /// field, clipping or not depending on a point.</para>
    ///
    /// <para>Erring inclusive is deliberate. A genuinely two-line field caught by this shrinks to one
    /// line, which is visible and adjustable; a single-line field missed by it clips, which is neither
    /// until it is on paper.</para></summary>
    private const float TwoLines = 2.5f;

    /// <summary>`#RGB`, `#RRGGBB` or `#RRGGBBAA`, else the fallback. A malformed colour must not throw
    /// mid-render: the design is user data, and a bad hex is a typo rather than a reason to fail a batch
    /// of two hundred certificates.</summary>
    private static string Hex(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var v = value.Trim();
        if (!v.StartsWith('#')) v = "#" + v;
        return v.Length is 4 or 7 or 9 && v[1..].All(Uri.IsHexDigit) ? v : fallback;
    }
}
