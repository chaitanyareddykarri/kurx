using Kurx.Infrastructure.Certificates;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Turning Tesseract's word-by-word output into text a creator can click (D-355).
///
/// <para>The binary is not involved here. What is tested is the part that decides what a creator sees:
/// how words become lines, which lines are confident enough to offer, and how pixel boxes become the
/// percentage coordinates the editor and the renderer both speak.</para>
///
/// <para>The bias is deliberate. A missed line costs one manual text box. A garbage line costs the
/// creator a deletion AND makes them distrust every other box on the page — so the thresholds lean
/// toward offering less.</para>
/// </summary>
public class TesseractTextDetectorTests
{
    /// <summary>Tesseract's TSV: level, page, block, par, line, word, left, top, width, height, conf, text.
    /// Level 5 is a word; coarser levels are boxes drawn around other boxes.</summary>
    private static string Tsv(params string[] rows) =>
        "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n"
        + string.Join("\n", rows);

    private static string Word(int block, int par, int line, int word,
        int left, int top, int width, int height, double conf, string text) =>
        $"5\t1\t{block}\t{par}\t{line}\t{word}\t{left}\t{top}\t{width}\t{height}\t{conf}\t{text}";

    // ── Words become lines ──────────────────────────────────────────────────────────────────────

    /// <summary>A creator wants to click "Certificate of Completion", not three separate boxes.</summary>
    [Fact]
    public void Words_on_one_line_become_one_region()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 100, 200, 120, 30, 96, "Certificate"),
            Word(1, 1, 1, 2, 230, 200, 30, 30, 95, "of"),
            Word(1, 1, 1, 3, 270, 200, 130, 30, 94, "Completion"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));

        Assert.Equal("Certificate of Completion", region.Text);
    }

    /// <summary>The box has to wrap every word, or the editable layer clips the text it replaced.</summary>
    [Fact]
    public void The_region_spans_all_of_its_words()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 100, 200, 120, 30, 96, "Certificate"),
            Word(1, 1, 1, 3, 270, 190, 130, 45, 94, "Completion"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));

        // x 100..400 of 1000; y 190..235 of 700.
        Assert.Equal(10, region.X, 3);
        Assert.Equal(30, region.Width, 3);
        Assert.Equal(190.0 / 700 * 100, region.Y, 3);
        Assert.Equal(45.0 / 700 * 100, region.Height, 3);
    }

    /// <summary>Different lines stay different, or a whole paragraph becomes one unclickable block.</summary>
    [Fact]
    public void Separate_lines_stay_separate()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 100, 100, 80, 20, 95, "First"),
            Word(1, 1, 2, 1, 100, 200, 90, 20, 95, "Second"),
            Word(2, 1, 1, 1, 100, 300, 80, 20, 95, "Third"));

        var regions = TesseractTextDetector.ParseTsv(tsv, 1000, 700);

        Assert.Equal(3, regions.Count);
        Assert.Equal(["First", "Second", "Third"], regions.Select(r => r.Text));
    }

    /// <summary>Reading order, so the editor's list matches the page rather than a hash ordering.</summary>
    [Fact]
    public void Regions_come_back_in_reading_order()
    {
        var tsv = Tsv(
            Word(3, 1, 1, 1, 100, 500, 80, 20, 95, "Bottom"),
            Word(1, 1, 1, 1, 100, 100, 80, 20, 95, "Top"),
            Word(2, 1, 1, 1, 600, 300, 80, 20, 95, "MiddleRight"),
            Word(2, 1, 2, 1, 100, 300, 80, 20, 95, "MiddleLeft"));

        var regions = TesseractTextDetector.ParseTsv(tsv, 1000, 700);

        Assert.Equal(["Top", "MiddleLeft", "MiddleRight", "Bottom"], regions.Select(r => r.Text));
    }

    // ── What gets refused ───────────────────────────────────────────────────────────────────────

    /// <summary>Paper texture, border edges and JPEG artefacts all read as low-confidence "words". Handing
    /// those to a creator as boxes to delete is worse than missing them.</summary>
    [Fact]
    public void Low_confidence_words_are_dropped()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 100, 200, 120, 30, 96, "Real"),
            Word(1, 1, 1, 2, 230, 200, 30, 30, 4, "|"),
            Word(1, 1, 1, 3, 270, 200, 130, 30, 92, "Text"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));

        Assert.Equal("Real Text", region.Text);
    }

    /// <summary>A line that is mostly noise is not text, however many words it has.</summary>
    [Fact]
    public void A_line_with_poor_average_confidence_is_not_offered()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 100, 200, 40, 30, 45, "~~"),
            Word(1, 1, 1, 2, 150, 200, 40, 30, 44, "``"));

        Assert.Empty(TesseractTextDetector.ParseTsv(tsv, 1000, 700));
    }

    [Fact]
    public void Non_word_rows_and_blanks_are_ignored()
    {
        var tsv = Tsv(
            "1\t1\t1\t1\t1\t1\t0\t0\t1000\t700\t-1\t",            // page level
            "4\t1\t1\t1\t1\t1\t100\t200\t300\t40\t-1\t",          // line level
            Word(1, 1, 1, 1, 100, 200, 120, 30, 95, "   "),        // whitespace only
            Word(1, 1, 1, 2, 230, 200, 120, 30, 95, "Kept"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));
        Assert.Equal("Kept", region.Text);
    }

    [Fact]
    public void Malformed_rows_do_not_throw()
    {
        var tsv = Tsv("nonsense", "5\t1\t1", "", Word(1, 1, 1, 1, 10, 10, 50, 20, 95, "Fine"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));
        Assert.Equal("Fine", region.Text);
    }

    [Fact]
    public void Empty_output_yields_no_regions()
    {
        Assert.Empty(TesseractTextDetector.ParseTsv("", 1000, 700));
        Assert.Empty(TesseractTextDetector.ParseTsv(Tsv(), 1000, 700));
    }

    // ── The contract the editor relies on ───────────────────────────────────────────────────────

    /// <summary>Coordinates are percentages of the page — the same units fields are stored in, so a
    /// detected region drops straight onto the canvas with no conversion to get wrong.</summary>
    [Fact]
    public void Coordinates_are_percentages_of_the_page()
    {
        var tsv = Tsv(Word(1, 1, 1, 1, 500, 350, 250, 70, 95, "Half"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));

        Assert.Equal(50, region.X, 3);
        Assert.Equal(50, region.Y, 3);
        Assert.Equal(25, region.Width, 3);
        Assert.Equal(10, region.Height, 3);
    }

    /// <summary>Tesseract speaks 0–100; the boundary's contract is 0–1, so a caller filtering on 0.8
    /// means the same thing whichever engine is installed.</summary>
    [Fact]
    public void Confidence_is_normalised_to_zero_one()
    {
        var tsv = Tsv(Word(1, 1, 1, 1, 100, 100, 80, 20, 88, "Word"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));

        Assert.Equal(0.88, region.Confidence, 3);
        Assert.InRange(region.Confidence, 0, 1);
    }

    /// <summary>A real certificate's text, end to end: the title, the lead-in, the name placeholder and
    /// the footer each become their own clickable line.</summary>
    [Fact]
    public void A_realistic_certificate_becomes_clickable_lines()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 120, 100, 700, 50, 93, "Certificate"),
            Word(1, 1, 1, 2, 840, 100, 60, 50, 92, "of"),
            Word(1, 1, 1, 3, 920, 100, 300, 50, 91, "Completion"),
            Word(2, 1, 1, 1, 400, 240, 500, 30, 89, "This"),
            Word(2, 1, 1, 2, 910, 240, 200, 30, 90, "certifies"),
            Word(3, 1, 1, 1, 330, 300, 640, 60, 87, "[Recipient's"),
            Word(3, 1, 1, 2, 980, 300, 300, 60, 86, "Full"),
            Word(3, 1, 1, 3, 1290, 300, 200, 60, 88, "Name]"));

        var regions = TesseractTextDetector.ParseTsv(tsv, 2000, 1400);

        Assert.Equal(3, regions.Count);
        Assert.Equal("Certificate of Completion", regions[0].Text);
        Assert.Equal("This certifies", regions[1].Text);
        Assert.Equal("[Recipient's Full Name]", regions[2].Text);
        Assert.All(regions, r => Assert.InRange(r.X, 0, 100));
        Assert.All(regions, r => Assert.InRange(r.Y, 0, 100));
    }

    // ── Merging lines into paragraphs ───────────────────────────────────────────────────────────

    /// <summary>Each region becomes a patch over the artwork, and many small patches read as damage where
    /// one large patch reads as a panel. A three-line body paragraph covered by three rectangles shows its
    /// seams — this is the real certificate's body text.</summary>
    [Fact]
    public void Lines_of_one_paragraph_become_a_single_region()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 300, 500, 400, 24, 95, "Ensuring expertise in confined space entry,"),
            Word(1, 1, 2, 1, 300, 530, 400, 24, 95, "rescue procedures, and emergency response,"),
            Word(1, 1, 3, 1, 310, 560, 380, 24, 95, "this 8-hour training awarded 8 CPD points"));

        var region = Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700));

        Assert.Contains("Ensuring expertise", region.Text, StringComparison.Ordinal);
        Assert.Contains("8 CPD points", region.Text, StringComparison.Ordinal);
        // One box covering all three lines.
        Assert.Equal(500 / 700.0 * 100, region.Y, 3);
        Assert.Equal((584 - 500) / 700.0 * 100, region.Height, 3);
    }

    /// <summary>The case merging must NOT swallow: two dates at the same height, side by side. Same line,
    /// no horizontal overlap, so they stay the two separate things a creator will want to edit apart.</summary>
    [Fact]
    public void Side_by_side_text_at_the_same_height_stays_separate()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 200, 600, 250, 24, 96, "Date of Issuance: 01/02/2025"),
            Word(2, 1, 1, 1, 600, 600, 250, 24, 96, "Date of validity: 01/02/2028"));

        var regions = TesseractTextDetector.ParseTsv(tsv, 1000, 700);

        Assert.Equal(2, regions.Count);
    }

    /// <summary>A gap much larger than the type is a new paragraph, not a wrapped line.</summary>
    [Fact]
    public void Text_separated_by_a_real_gap_stays_separate()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 300, 200, 400, 24, 95, "This certificate acknowledges that"),
            Word(1, 1, 2, 1, 300, 400, 400, 24, 95, "has successfully completed"));

        Assert.Equal(2, TesseractTextDetector.ParseTsv(tsv, 1000, 700).Count);
    }

    // ── The misreads that reached a real certificate ────────────────────────────────────────────

    /// <summary>Every one of these was produced by the engine on a real uploaded design, scored above the
    /// old bar, and printed onto the finished certificate. A lone word is the shape almost every misread
    /// takes, so it now has to clear a much higher bar than a phrase does.</summary>
    [Theory]
    [InlineData(75, "\u2018our")]   // "Your Company Name" clipped by the logo beside it
    [InlineData(85, "\\")]          // a rule read as a character
    [InlineData(67, "5")]            // the edge of a seal
    public void Lone_words_the_engine_is_unsure_of_are_refused(double confidence, string text)
    {
        var tsv = Tsv(Word(1, 1, 1, 1, 100, 100, 60, 24, confidence, text));

        Assert.Empty(TesseractTextDetector.ParseTsv(tsv, 1000, 700));
    }

    /// <summary>But a lone word the engine is sure of is real text — "NIOSH" scored 0.96 on that same
    /// certificate and belongs on the page.</summary>
    [Fact]
    public void A_confident_lone_word_is_still_offered()
    {
        var tsv = Tsv(Word(1, 1, 1, 1, 100, 100, 90, 24, 96, "NIOSH"));

        Assert.Equal("NIOSH", Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700)).Text);
    }

    /// <summary>A phrase validates itself: the engine does not hallucinate "has successfully completed"
    /// out of paper texture, so a middling score there is still trustworthy.</summary>
    [Fact]
    public void A_multi_word_phrase_is_kept_at_a_lower_score()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 100, 100, 90, 24, 74, "has"),
            Word(1, 1, 1, 2, 200, 100, 90, 24, 73, "successfully"),
            Word(1, 1, 1, 3, 300, 100, 90, 24, 75, "completed"));

        Assert.Equal("has successfully completed",
            Assert.Single(TesseractTextDetector.ParseTsv(tsv, 1000, 700)).Text);
    }

    [Fact]
    public void Marks_with_no_real_characters_are_refused()
    {
        var tsv = Tsv(
            Word(1, 1, 1, 1, 100, 100, 20, 20, 99, "--"),
            Word(2, 1, 1, 1, 200, 100, 20, 20, 99, "*"));

        Assert.Empty(TesseractTextDetector.ParseTsv(tsv, 1000, 700));
    }
}
