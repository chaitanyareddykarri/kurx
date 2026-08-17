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

        // The element's own ground, painted before its content. This is also the whole mechanism behind
        // cover-and-replace: a masking element is an ordinary filled box that happens to sit over
        // printed text, and the fill is what hides it.
        if (!string.IsNullOrWhiteSpace(element.BackgroundColor))
            box = box.Background(Hex(element.BackgroundColor, Colors.Transparent));

        switch (element.Kind?.Trim().ToLowerInvariant())
        {
            case "text":
                DrawText(box, element, element.StaticText ?? "", h);
                break;

            case "dynamicfield":
            {
                // A key with no value renders as its own placeholder rather than as nothing, so a
                // mistake shows on the preview instead of as a blank space on the finished document.
                var value = data.Values.TryGetValue(element.FieldKey ?? "", out var v) && !string.IsNullOrEmpty(v)
                    ? v
                    : $"{{{element.FieldKey}}}";
                DrawText(box, element, value, h);
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
    private static void DrawText(IContainer box, CertificateRenderElement element, string value, float boxHeightPoints)
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
        // 0.8 leaves room for the line box around the glyphs; above that QuestPDF starts dropping the
        // line. Only ever reduces — a design whose type already fits renders exactly as authored.
        var requested = (float)(element.FontSizePt ?? 16);
        span.FontSize(Math.Max(4f, Math.Min(requested, boxHeightPoints * 0.8f)));
        span.FontColor(Hex(element.Color, Colors.Black));
        if (string.Equals(element.FontWeight, "bold", StringComparison.OrdinalIgnoreCase)) span.Bold();
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
