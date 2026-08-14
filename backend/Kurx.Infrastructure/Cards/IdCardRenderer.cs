using Kurx.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kurx.Infrastructure.Cards;

/// <summary>Renders an ID card to PDF and PNG via QuestPDF (D-331).
///
/// <para><b>No rasterizer on this path.</b> QuestPDF produces the PNG itself through
/// <c>GenerateImages</c>, so <see cref="IDocumentRasterizer"/> — still a stub — is not involved. That
/// stub only matters for org-uploaded PDF templates, which are deferred (D-035).</para>
///
/// <para><b>Card geometry.</b> CR80 (85.6 × 54 mm) is the physical ID-card standard, so a printed card
/// fits a standard lanyard holder without scaling. Rendering at that size rather than cropping an A4
/// page is why the PNG is directly usable as a preview.</para>
///
/// <para><b>Templates are layout functions, not data.</b> Seven keys map to seven composers. They differ in
/// arrangement and palette only; every one renders the same field set, so a holder switching template
/// never loses a field and no template can quietly omit the validity line.</para></summary>
public class IdCardRenderer : IIdCardRenderer
{
    private const float CardWidthMm = 85.6f;
    private const float CardHeightMm = 54f;

    private record Palette(string Accent, string OnAccent, string Ink, string Muted);

    /// <summary>Per-template palette. Kept beside the composer choice so a new template cannot be added
    /// without deciding its colours.</summary>
    private static Palette PaletteFor(string key) => key switch
    {
        "ModernCollege" => new("#0F172A", "#FFFFFF", "#0F172A", "#64748B"),
        "TechFest" => new("#4338CA", "#FFFFFF", "#111827", "#6B7280"),
        "CulturalFest" => new("#BE185D", "#FFFFFF", "#111827", "#6B7280"),
        "EventParticipant" => new("#047857", "#FFFFFF", "#111827", "#6B7280"),
        "StaffFaculty" => new("#7C2D12", "#FFFFFF", "#111827", "#6B7280"),
        "Volunteer" => new("#B45309", "#FFFFFF", "#111827", "#6B7280"),
        _ => new("#1D4ED8", "#FFFFFF", "#0F172A", "#64748B"),   // StandardCollege
    };

    public Task<IdCardRenderResult> RenderAsync(IdCardRenderRequest r, CancellationToken ct = default)
    {
        var palette = PaletteFor(r.TemplateKey);
        // A band across the top is the "standard" arrangement; a full-height side rail is the "modern"
        // one. Every other template is one of these two shapes in its own palette, which is why adding
        // a festival template is a palette entry rather than a new layout.
        var sideRail = r.TemplateKey is "ModernCollege" or "TechFest" or "CulturalFest";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(CardWidthMm, CardHeightMm, Unit.Millimetre);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontSize(5).FontColor(palette.Ink));
                page.Content().Element(c => Compose(c, r, palette, sideRail));
            });
        });

        var pdf = document.GeneratePdf();
        // 300 dpi: the density a card is printed at, so the PNG is a print asset and not only a preview.
        var png = document.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = 300,
        }).First();

        return Task.FromResult(new IdCardRenderResult(pdf, png));
    }

    private static void Compose(IContainer root, IdCardRenderRequest r, Palette p, bool sideRail)
    {
        if (sideRail)
        {
            root.Row(row =>
            {
                row.ConstantItem(18, Unit.Millimetre).Background(p.Accent).Padding(3)
                    .Element(c => Rail(c, r, p));
                row.RelativeItem().Padding(4).Element(c => Body(c, r, p, showHeader: true));
            });
            return;
        }

        root.Column(col =>
        {
            col.Item().Background(p.Accent).Padding(3).Row(row =>
            {
                if (r.LogoBytes is not null)
                    row.ConstantItem(8, Unit.Millimetre).Image(r.LogoBytes).FitArea();
                row.RelativeItem().PaddingLeft(2).AlignMiddle().Text(r.OrgName)
                    .FontSize(6.5f).Bold().FontColor(p.OnAccent);
            });
            col.Item().Padding(4).Element(c => Body(c, r, p, showHeader: false));
        });
    }

    private static void Rail(IContainer c, IdCardRenderRequest r, Palette p) =>
        c.Column(col =>
        {
            if (r.LogoBytes is not null)
                col.Item().Height(10, Unit.Millimetre).Image(r.LogoBytes).FitArea();
            col.Item().PaddingTop(2).Text(r.OrgName).FontSize(5).Bold().FontColor(p.OnAccent);
            col.Item().PaddingTop(2).Text(r.CardNumber).FontSize(4).FontColor(p.OnAccent);
        });

    private static void Body(IContainer c, IdCardRenderRequest r, Palette p, bool showHeader) =>
        c.Row(row =>
        {
            // Photo column. Absent photo leaves the space rather than collapsing the layout, so a card
            // issued before an avatar exists still prints to the same geometry.
            row.ConstantItem(16, Unit.Millimetre).Column(col =>
            {
                if (r.PhotoBytes is not null)
                    col.Item().Height(20, Unit.Millimetre).Image(r.PhotoBytes).FitArea();
                else
                    col.Item().Height(20, Unit.Millimetre).Background("#E5E7EB");
                col.Item().PaddingTop(1).Height(9, Unit.Millimetre).Image(r.QrPng).FitArea();
            });

            row.RelativeItem().PaddingLeft(3).Column(col =>
            {
                if (showHeader)
                    col.Item().Text(r.OrgName).FontSize(5.5f).Bold().FontColor(p.Accent);

                col.Item().Text(r.HolderName).FontSize(8).Bold();

                Field(col, "ID", r.StudentId, p);
                Field(col, "Dept", r.Department, p);
                Field(col, "Course", Join(r.Course, r.Year), p);
                Field(col, "DOB", r.DateOfBirth, p);
                Field(col, "Blood", r.BloodGroup, p);
                Field(col, "Phone", r.Phone, p);
                Field(col, "Email", r.Email, p);
                Field(col, "Address", r.Address, p);
                Field(col, "ICE", r.EmergencyContact, p);
                // D-334 §8. Reads "Meals  B1 · L1 · D1 · S2". Field() omits the row when the value is
                // null, so a card without meals is byte-identical to one printed before this existed.
                Field(col, "Meals", r.MealLine, p);

                col.Item().PaddingTop(1).Text(r.ValidityLine).FontSize(4.5f).FontColor(p.Muted);

                if (r.SignatureBytes is not null)
                    col.Item().PaddingTop(1).Height(5, Unit.Millimetre).AlignRight()
                        .Image(r.SignatureBytes).FitArea();
            });
        });

    /// <summary>Omits the row entirely when the value is absent. A label with a blank beside it reads as
    /// missing data on a printed card; no row reads as not applicable, which is the truthful rendering
    /// of an optional field nobody supplied.</summary>
    private static void Field(ColumnDescriptor col, string label, string? value, Palette p)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().Text(t =>
        {
            t.Span($"{label}  ").FontSize(4).FontColor(p.Muted);
            t.Span(value).FontSize(4.5f);
        });
    }

    private static string? Join(string? a, string? b) =>
        string.Join(" · ", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } s ? s : null;
}
