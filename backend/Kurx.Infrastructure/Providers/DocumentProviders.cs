using System.Diagnostics;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kurx.Infrastructure.Providers;

// QrCodeGenerator and CertificateRenderer are real (QRCoder / QuestPDF). DocumentRasterizerStub
// remains a stub — real PDF rasterization (Docnet.Core) is only needed for custom uploaded-PDF
// templates, which are deferred (D-035); system templates render PNG directly via QuestPDF's own
// GenerateImages, with no PDF→PNG rasterization step at all.

public class QrCodeGenerator(ILogger<QrCodeGenerator> log) : IQrCodeGenerator
{
    public byte[] GeneratePng(string data, int pixelSize = 512)
    {
        log.LogDebug("Generating QR code for data length={Length}", data.Length);
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(pixelSize / qrData.ModuleMatrix.Count);
    }
}

public class DocumentRasterizerStub(ILogger<DocumentRasterizerStub> log) : IDocumentRasterizer
{
    public byte[] RasterizePage(byte[] pdfBytes, int pageIndex = 0, int dpi = 150)
    {
        log.LogWarning("DocumentRasterizer is a stub — returning a blank 1x1 PNG, so certificate " +
                       "previews are empty. A real rasterizer is unimplemented platform work.");
        // Returning an empty 1x1 PNG so callers get a non-null byte[] without crashing.
        // Replace with Docnet.Core implementation when certificate rendering ships.
        return new byte[]
        {
            137, 80, 78, 71, 13, 10, 26, 10,    // PNG signature
            0, 0, 0, 13, 73, 72, 68, 82,         // IHDR chunk length + type
            0, 0, 0, 1, 0, 0, 0, 1,              // width=1, height=1
            8, 2, 0, 0, 0, 144, 119, 83, 222,    // bit depth=8, color=RGB, compression
            0, 0, 0, 12, 73, 68, 65, 84,         // IDAT chunk
            8, 215, 99, 248, 207, 192, 0, 0, 0, 2, 0, 1,
            226, 33, 188, 51,                     // IDAT CRC
            0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130  // IEND chunk
        };
    }
}

/// <summary>
/// Renders a certificate onto one of a small set of hardcoded system layouts (D-035), keyed by
/// <see cref="CertificateRenderRequest.TemplateLayoutKey"/>. Custom (org-uploaded image) backgrounds
/// are not implemented in this pass — only <c>BaseLayout</c>-keyed system templates render for now.
/// </summary>
public class CertificateRenderer : ICertificateRenderer
{
    static CertificateRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly IQrCodeGenerator _qrGenerator;

    public CertificateRenderer(IQrCodeGenerator qrGenerator)
    {
        _qrGenerator = qrGenerator;
    }

    public Task<CertificateRenderResult> RenderAsync(CertificateRenderRequest request, CancellationToken ct = default)
    {
        var qrPng = _qrGenerator.GeneratePng(request.QrDataUrl, 300);
        var accent = ParseAccent(request.AccentColor);
        var isModern = string.Equals(request.TemplateLayoutKey, "modern-certificate", StringComparison.OrdinalIgnoreCase);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri));

                if (isModern)
                    ComposeModern(page, request, accent, qrPng);
                else
                    ComposeClassic(page, request, accent, qrPng);
            });
        });

        var pdfBytes = document.GeneratePdf();
        var pngBytes = document.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 150 }).First();
        return Task.FromResult(new CertificateRenderResult(pdfBytes, pngBytes));
    }

    /// <summary>Centered layout, coloured border frame — the "Classic" system template.</summary>
    private static void ComposeClassic(PageDescriptor page, CertificateRenderRequest request, string accent, byte[] qrPng)
    {
        page.PageColor(accent);
        page.Content()
            .Padding(18)
            .Background(Colors.White)
            .Padding(48)
            .Column(col =>
            {
                col.Spacing(18);
                col.Item().AlignCenter().Text("Certificate of Participation").FontSize(28).Bold().FontColor(accent);
                col.Item().AlignCenter().Text("This is to certify that").FontSize(13);
                col.Item().AlignCenter().Text(request.RecipientName).FontSize(24).SemiBold();
                col.Item().AlignCenter().Text(text =>
                {
                    text.Span("participated in ").FontSize(13);
                    text.Span(request.EventName).FontSize(13).SemiBold();
                    text.Span($" on {request.EventDate}.").FontSize(13);
                });
                ComposeFooter(col, request, qrPng);
            });
    }

    /// <summary>Left-aligned layout with a solid colour header band — the "Modern" system template.</summary>
    private static void ComposeModern(PageDescriptor page, CertificateRenderRequest request, string accent, byte[] qrPng)
    {
        page.PageColor(Colors.White);
        page.Content().Column(outer =>
        {
            outer.Item().Background(accent).Padding(32).Text("Certificate of Participation")
                .FontSize(26).Bold().FontColor(Colors.White);
            outer.Item().Padding(40).Column(col =>
            {
                col.Spacing(16);
                col.Item().Text("This is to certify that").FontSize(13);
                col.Item().Text(request.RecipientName).FontSize(24).SemiBold().FontColor(accent);
                col.Item().Text(text =>
                {
                    text.Span("participated in ").FontSize(13);
                    text.Span(request.EventName).FontSize(13).SemiBold();
                    text.Span($" on {request.EventDate}.").FontSize(13);
                });
                ComposeFooter(col, request, qrPng);
            });
        });
    }

    private static void ComposeFooter(ColumnDescriptor col, CertificateRenderRequest request, byte[] qrPng)
    {
        col.Item().PaddingTop(24).Row(row =>
        {
            row.RelativeItem().Column(sig =>
            {
                if (!string.IsNullOrWhiteSpace(request.SignatoryName))
                {
                    sig.Item().PaddingTop(24).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    sig.Item().Text(request.SignatoryName).SemiBold();
                    if (!string.IsNullOrWhiteSpace(request.SignatoryTitle))
                        sig.Item().Text(request.SignatoryTitle).FontSize(10).FontColor(Colors.Grey.Darken1);
                }
            });
            row.ConstantItem(90).Column(qr =>
            {
                qr.Item().Image(qrPng);
                qr.Item().AlignCenter().Text("Scan to verify").FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static string ParseAccent(string? hex)
        => !string.IsNullOrWhiteSpace(hex) && System.Text.RegularExpressions.Regex.IsMatch(hex, "^#?[0-9A-Fa-f]{6}$")
            ? (hex.StartsWith('#') ? hex : $"#{hex}")
            : Colors.Blue.Darken2;
}

/// <summary>
/// D-199: <see cref="ICertificateRenderer"/> is a singleton, and its first call pays a one-time SkiaSharp/
/// QuestPDF font-resolution cost — measured at ~26.7s cold vs. ~293ms warm (D-198's finding), an ~91×
/// difference that proved this is a startup cost, not a per-certificate one. This runs one throwaway render
/// during app startup so a real user's first certificate request never pays it. Fire-and-forget by design:
/// a slow (or failing) warm-up must never delay the app from accepting traffic or becoming healthy — the
/// worst case on failure is exactly today's behavior, where the first real request pays the cost itself.
/// </summary>
public class CertificateRendererWarmupService(ICertificateRenderer renderer, ILogger<CertificateRendererWarmupService> log)
    : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        _ = WarmUpAsync(ct);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task WarmUpAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await renderer.RenderAsync(new CertificateRenderRequest(
                TemplateLayoutKey: "classic-certificate", RecipientName: "Warm-up", EventName: "Warm-up",
                EventDate: DateTime.UtcNow.ToString("d MMM yyyy"), SignatoryName: null, SignatoryTitle: null,
                LogoKey: null, BackgroundKey: null, AccentColor: null, QrDataUrl: "https://kurx.in/warmup"), ct);
            log.LogInformation("Certificate renderer warm-up completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Certificate renderer warm-up failed after {ElapsedMs}ms (non-fatal — the first " +
                "real certificate request pays the cold-start cost instead)", sw.ElapsedMilliseconds);
        }
    }
}
