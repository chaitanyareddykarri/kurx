using Kurx.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Finding where a design already has something printed on it (D-355).
///
/// <para>This exists to warn a creator before they place a field over their own artwork's text — the
/// mistake that renders <c>[Recipient's Full Name] John Doe</c> on a finished certificate, where the
/// placeholder is pixels in the uploaded image and the value is drawn on top of it.</para>
///
/// <para>The property that matters is not precision but the direction of its errors. A missed region
/// costs a warning nobody sees; a false one fires on every design and teaches people to ignore the
/// warning entirely, which is worse than not having it.</para>
/// </summary>
public class CertificateArtworkMapTests
{
    private static readonly System.Reflection.MethodInfo Analyse =
        typeof(Kurx.Infrastructure.Certificates.CertificateTemplateService)
            .GetMethod("Analyse", System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Static)!;

    private static CertificateArtworkMap MapOf(Image<Rgba32> image)
    {
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return (CertificateArtworkMap)Analyse.Invoke(null, [buffer.ToArray()])!;
    }

    /// <summary>Fraction of a box's cells that are printed, the same calculation the editor makes.</summary>
    private static double Coverage(CertificateArtworkMap map, double x, double y, double w, double h)
    {
        int busy = 0, total = 0;
        for (var row = 0; row < map.Rows; row++)
        {
            for (var col = 0; col < map.Columns; col++)
            {
                var cx = (col + 0.5) / map.Columns * 100.0;
                var cy = (row + 0.5) / map.Rows * 100.0;
                if (cx < x || cx > x + w || cy < y || cy > y + h) continue;
                total++;
                if (map.Cells[row * map.Columns + col] == '1') busy++;
            }
        }
        return total == 0 ? 0 : busy / (double)total;
    }

    private static Image<Rgba32> Page(Rgba32 background) => new(800, 566, background);

    /// <summary>Fills a rectangle by writing pixels. ImageSharp's drawing APIs live in a separate package,
    /// and a fixture this simple is not worth a dependency.</summary>
    private static void Fill(Image<Rgba32> image, int x, int y, int w, int h, Rgba32 colour)
    {
        for (var row = y; row < y + h && row < image.Height; row++)
            for (var col = x; col < x + w && col < image.Width; col++)
                image[col, row] = colour;
    }

    // ── The signal ──────────────────────────────────────────────────────────────────────────────

    /// <summary>A band of print in the upper third is found there, and nowhere else.</summary>
    [Fact]
    public void Printed_content_is_located_where_it_actually_is()
    {
        using var page = Page(new Rgba32(255, 255, 255));
        // A dark bar standing in for a line of text, occupying x 20–80%, y 30–40%.
        Fill(page, 160, 170, 480, 57, new Rgba32(0, 0, 0));

        var map = MapOf(page);

        Assert.True(map.Analysed);
        Assert.True(Coverage(map, 20, 30, 60, 10) > 0.8, "the printed band should be found");
        Assert.True(Coverage(map, 20, 60, 60, 10) < 0.05, "empty space should stay empty");
    }

    /// <summary>The whole point: a field placed over the design's own text is detectable before anything
    /// is generated.</summary>
    [Fact]
    public void A_field_placed_over_printed_text_overlaps_it()
    {
        using var page = Page(new Rgba32(255, 255, 255));
        Fill(page, 160, 170, 480, 57, new Rgba32(0, 0, 0));

        // A name field dropped straight onto that line.
        Assert.True(Coverage(MapOf(page), 15, 29, 70, 12) > 0.4);
    }

    [Fact]
    public void A_field_placed_on_clear_space_does_not()
    {
        using var page = Page(new Rgba32(255, 255, 255));
        Fill(page, 160, 170, 480, 57, new Rgba32(0, 0, 0));

        Assert.True(Coverage(MapOf(page), 15, 55, 70, 12) < 0.05);
    }

    // ── Not crying wolf ─────────────────────────────────────────────────────────────────────────

    /// <summary>A background is whatever colour the design uses, not white. Assuming white would mark
    /// every cream, navy or dark certificate as printed edge to edge.</summary>
    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(253, 246, 227)]   // cream
    [InlineData(11, 30, 60)]      // navy
    public void A_plain_page_of_any_colour_is_entirely_clear(byte r, byte g, byte b)
    {
        using var page = Page(new Rgba32(r, g, b));

        var map = MapOf(page);

        Assert.True(map.Analysed);
        Assert.DoesNotContain('1', map.Cells);
    }

    /// <summary>Certificate stock carries watermarks and texture. Flagging those would fire the warning
    /// on every design and teach people to ignore it.</summary>
    [Fact]
    public void A_faint_watermark_is_not_mistaken_for_print()
    {
        using var page = Page(new Rgba32(255, 255, 255));
        // Barely-there pattern: a few shades off white, which is what a security tint looks like.
        Fill(page, 100, 100, 600, 300, new Rgba32(248, 248, 250));

        Assert.DoesNotContain('1', MapOf(page).Cells);
    }

    /// <summary>Dark text on a dark background is still text.</summary>
    [Fact]
    public void Light_print_on_a_dark_page_is_found()
    {
        using var page = Page(new Rgba32(11, 30, 60));
        Fill(page, 160, 170, 480, 57, new Rgba32(255, 255, 255));

        Assert.True(Coverage(MapOf(page), 20, 30, 60, 10) > 0.8);
    }

    // ── Shape of the answer ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_map_is_one_character_per_cell()
    {
        using var page = Page(new Rgba32(255, 255, 255));

        var map = MapOf(page);

        Assert.Equal(map.Columns * map.Rows, map.Cells.Length);
        Assert.All(map.Cells, c => Assert.True(c is '0' or '1'));
    }
}

/// <summary>
/// Sampling the paper colour behind a region, so a field can cover printed text without leaving a patch
/// (D-355).
///
/// <para>The failure this guards against is subtle and ugly: sampling the middle of the region averages
/// in the very ink being covered, producing a grey box over black text — a defect placed exactly where
/// it was meant to be invisible.</para>
/// </summary>
public class CertificateArtworkColourTests
{
    private static readonly System.Reflection.MethodInfo Sample =
        typeof(Kurx.Infrastructure.Certificates.CertificateTemplateService)
            .GetMethod("SampleGround", System.Reflection.BindingFlags.NonPublic
                                     | System.Reflection.BindingFlags.Static)!;

    private static string Ground(Image<Rgba32> image, double x, double y, double w, double h)
    {
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return (string)Sample.Invoke(null, [buffer.ToArray(), new CertificateRegion(x, y, w, h)])!;
    }

    private static Image<Rgba32> Page(Rgba32 background) => new(800, 566, background);

    private static void Fill(Image<Rgba32> image, int x, int y, int w, int h, Rgba32 colour)
    {
        for (var row = y; row < y + h && row < image.Height; row++)
            for (var col = x; col < x + w && col < image.Width; col++)
                image[col, row] = colour;
    }

    /// <summary>White paper must come back as white.
    ///
    /// <para>The histogram buckets colour at 5-bit precision, and it used to return the BUCKET's floor as
    /// the answer: 255 >> 3 << 3 = 248, so every mask on a white certificate was painted #F8F8F8. On
    /// screen that is a visible grey band sitting exactly where the recipient's name goes — the covering
    /// announcing itself as an edit, which is the one thing it must not do.</para></summary>
    [Fact]
    public void SamplesWhitePaperAsExactlyWhite()
    {
        using var page = Page(new Rgba32(255, 255, 255));
        Fill(page, 200, 250, 400, 40, new Rgba32(0, 0, 0));   // the printed text being covered

        Assert.Equal("#FFFFFF", Ground(page, 25, 44.2, 50, 7.1));
    }

    /// <summary>On textured stock the fill matches what the eye reads, not just the base tone.
    ///
    /// <para>A watermark makes the paper a mix of its base colour and slightly darker strokes. Filling a
    /// patch with the base alone leaves a rectangle visibly LIGHTER than everything around it — the
    /// texture stopping in a straight line, which is precisely the "this was edited" tell. The answer has
    /// to sit between the two.</para></summary>
    [Fact]
    public void SamplesTexturedPaperBetweenItsBaseAndItsPattern()
    {
        using var page = Page(new Rgba32(255, 255, 255));
        // A watermark: every third row a few shades darker, over the whole page.
        for (var y = 0; y < page.Height; y += 3)
            for (var x = 0; x < page.Width; x++)
                page[x, y] = new Rgba32(235, 235, 235);
        Fill(page, 200, 250, 400, 40, new Rgba32(0, 0, 0));   // the text being covered

        var sampled = System.Drawing.ColorTranslator.FromHtml(Ground(page, 25, 44.2, 50, 7.1));

        // Darker than the bare base, and nowhere near the ink.
        Assert.InRange(sampled.R, 236, 254);
    }

    /// <summary>And a tinted paper comes back as its own tint, not a quantised neighbour.</summary>
    [Fact]
    public void SamplesTintedPaperAsItsOwnColour()
    {
        var cream = new Rgba32(253, 250, 241);
        using var page = Page(cream);
        Fill(page, 200, 250, 400, 40, new Rgba32(0, 0, 0));

        Assert.Equal("#FDFAF1", Ground(page, 25, 44.2, 50, 7.1));
    }

    /// <summary>The paper colour, not the ink — even when the region is almost entirely ink.</summary>
    [Fact]
    public void The_ink_being_covered_does_not_tint_the_sample()
    {
        using var page = Page(new Rgba32(255, 255, 255));
        Fill(page, 160, 170, 480, 57, new Rgba32(0, 0, 0));   // a solid black line of "text"

        Assert.Equal("#FFFFFF", Ground(page, 20, 30, 60, 10));
    }

    /// <summary>Certificate stock is rarely white. Assuming it is puts a white smear on every cream,
    /// grey or navy design.</summary>
    [Theory]
    [InlineData(253, 246, 227)]   // cream
    [InlineData(11, 30, 60)]      // navy
    [InlineData(240, 240, 245)]   // cool grey
    public void The_page_colour_is_measured_not_assumed(byte r, byte g, byte b)
    {
        using var page = Page(new Rgba32(r, g, b));
        Fill(page, 160, 170, 480, 57, new Rgba32(0, 0, 0));

        var hex = Ground(page, 20, 30, 60, 10);

        // Exact. Bucketing groups near-identical pixels, but the colour returned is the average of the
        // real ones — a uniform page comes back as the colour it actually is, with no patch to see.
        var sampled = System.Drawing.ColorTranslator.FromHtml(hex);
        Assert.Equal((r, g, b), ((byte)sampled.R, (byte)sampled.G, (byte)sampled.B));
    }

    /// <summary>A region against the page edge still yields the paper colour rather than falling off the
    /// image and returning nothing.</summary>
    [Fact]
    public void A_region_at_the_very_edge_still_samples()
    {
        using var page = Page(new Rgba32(250, 250, 240));

        Assert.StartsWith("#", Ground(page, 0, 0, 20, 8));
        Assert.StartsWith("#", Ground(page, 80, 92, 20, 8));
    }

    [Fact]
    public void The_result_is_always_a_hex_colour()
    {
        using var page = Page(new Rgba32(200, 210, 220));

        var hex = Ground(page, 25, 40, 50, 12);

        Assert.Matches("^#[0-9A-F]{6}$", hex);
    }
}
