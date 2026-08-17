using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kurx.Infrastructure.IdCards;

/// <summary>
/// Lays rendered badges onto shared A4 pages with cut guides (D-362) — the pre-print run.
///
/// <para><b>Why the badges arrive as rasters.</b> Each badge is rendered once, by the same renderer that
/// produces a single badge, and pasted here as a PNG. Re-laying the elements at sheet scale would mean the
/// sheet and the single-badge download could disagree about a font metric or a rounding, and the failure
/// would surface as two hundred misprinted lanyards. Rendering once and placing the result cannot drift.
/// 300dpi because these are printed, not viewed.</para>
///
/// <para><b>Cut guides, not borders.</b> Hairlines in the gutter outside each badge, so a guillotine has
/// something to line up on and no line is left printed on the badge itself.</para>
/// </summary>
public static class BadgeSheetComposer
{
    static BadgeSheetComposer() => QuestPDF.Settings.License = LicenseType.Community;

    private const float A4WidthMm = 210f;
    private const float A4HeightMm = 297f;

    /// <summary>Page margin in mm. Most consumer printers cannot print closer than ~5mm to the edge, so
    /// the grid is inset past that rather than relying on the driver to scale — scaling would silently
    /// change the badge's physical size, which is the one thing a print run must get right.</summary>
    private const float MarginMm = 8f;

    /// <summary>Gutter between badges. Wide enough for a blade and for the cut guides to be visible.</summary>
    private const float GutterMm = 4f;

    public static byte[] Compose(IReadOnlyList<byte[]> badgePngs, double badgeWidthMm, double badgeHeightMm)
    {
        var bw = (float)badgeWidthMm;
        var bh = (float)badgeHeightMm;

        var usableW = A4WidthMm - (2 * MarginMm);
        var usableH = A4HeightMm - (2 * MarginMm);

        // At least one per page even if a badge is wider than the usable area — a clipped badge is a
        // visible problem the operator can act on; a zero-column grid is a division by zero.
        var cols = Math.Max(1, (int)Math.Floor((usableW + GutterMm) / (bw + GutterMm)));
        var rows = Math.Max(1, (int)Math.Floor((usableH + GutterMm) / (bh + GutterMm)));
        var perPage = cols * rows;

        return Document.Create(container =>
        {
            foreach (var pageBadges in Chunk(badgePngs, perPage))
            {
                container.Page(page =>
                {
                    page.Size(A4WidthMm, A4HeightMm, Unit.Millimetre);
                    page.Margin(MarginMm, Unit.Millimetre);
                    page.DefaultTextStyle(t => t.FontSize(7).FontColor("#9ca3af"));

                    page.Content().Column(col =>
                    {
                        col.Spacing(GutterMm, Unit.Millimetre);

                        foreach (var rowBadges in Chunk(pageBadges, cols))
                        {
                            col.Item().Row(row =>
                            {
                                row.Spacing(GutterMm, Unit.Millimetre);
                                foreach (var png in rowBadges)
                                    row.ConstantItem(bw, Unit.Millimetre)
                                        .Height(bh, Unit.Millimetre)
                                        .Image(png).FitArea();

                                // Keep a short final row left-aligned at the true badge width rather than
                                // letting the row stretch its members to fill the line — a stretched badge
                                // is the wrong physical size.
                                for (var i = rowBadges.Count; i < cols; i++)
                                    row.ConstantItem(bw, Unit.Millimetre).Height(bh, Unit.Millimetre);
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Cut along the gutters · ");
                        t.Span($"{bw:0.#} × {bh:0.#} mm");
                        t.Span("  ·  page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            }
        }).GeneratePdf();
    }

    private static List<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        var chunks = new List<List<T>>();
        for (var i = 0; i < source.Count; i += size)
            chunks.Add(source.Skip(i).Take(size).ToList());
        return chunks;
    }
}
