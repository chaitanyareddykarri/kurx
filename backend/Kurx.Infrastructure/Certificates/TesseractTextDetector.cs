using System.Diagnostics;
using System.Globalization;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Reads the text printed into an uploaded certificate, using the Tesseract CLI (D-355).
///
/// <para>Exists so a creator can click the words they can see. A design exported from Canva is a flat
/// raster — its title and its <c>[Recipient's Full Name]</c> are pixels — and without detection the only
/// way to edit them is to place a box by hand over each one and guess at the type.</para>
///
/// <para><b>The binary, not a managed wrapper.</b> A wrapper pins a native libtesseract ABI, and the two
/// drift; on arm64 that surfaces as a load failure at runtime rather than a build error. The CLI's TSV
/// output already carries per-word boxes and confidence, which is the shape this boundary wants, so a
/// wrapper would add a dependency and no capability.</para>
///
/// <para><b>Words are grouped into lines, not returned raw.</b> Tesseract emits one row per word; a
/// creator wants to click "Certificate of Completion", not six separate boxes. Tesseract's own
/// block/paragraph/line numbering does the grouping, so the line breaks are the ones it actually saw.</para>
/// </summary>
public class TesseractTextDetector(ILogger<TesseractTextDetector> log) : ITextDetector
{
    /// <summary>Words below this are noise — texture in the paper, edges of a border, JPEG artefacts.
    /// Tesseract reports 0–100. Set low enough to keep stylised certificate type, high enough that a
    /// creator is not handed a page of garbage boxes to delete.</summary>
    private const double MinWordConfidence = 40;

    /// <summary>A line needs this much average confidence to be offered at all. Higher than the per-word
    /// bar because one bad word among five good ones is recoverable; five bad ones is not text.</summary>
    private const double MinLineConfidence = 70;

    /// <summary>A LONE word has to clear a much higher bar than a phrase.
    ///
    /// <para>A multi-word phrase validates itself — "has successfully completed" is not something the
    /// engine hallucinates out of paper texture. A single word is the shape almost every misread takes:
    /// a logo clipped into <c>'our</c>, a rule read as <c>\</c>, a seal's edge read as <c>5</c>. Those
    /// all scored above the ordinary bar and still reached the page, so a lone word is only kept when the
    /// engine is genuinely sure of it.</para></summary>
    private const double MinLoneWordConfidence = 88;

    /// <summary>Text with fewer real characters than this is a mark, not a word.</summary>
    private const int MinAlphanumeric = 2;

    /// <summary>How close two lines must be, as a multiple of the taller one's height, to be one
    /// paragraph. A design's own line spacing sits well under 1; a gap to the NEXT paragraph sits well
    /// over it.</summary>
    private const double ParagraphGapRatio = 0.75;

    /// <summary>How different two lines' heights may be and still be one paragraph.
    ///
    /// <para>The discriminator that gap alone cannot provide. A wrapped paragraph is set in one size on
    /// every line; a heading followed by body text is not. Without this, "This certificate acknowledges
    /// that" merged with the much larger name beneath it — swallowing the one region that most needed to
    /// stay its own field.</para></summary>
    private const double ParagraphHeightTolerance = 0.35;

    /// <summary>How much two lines must overlap horizontally to belong together. A wrapped paragraph is
    /// near-fully overlapping; two things that merely happen to sit at the same height — a date on the
    /// left and another on the right — are not.</summary>
    private const double ParagraphOverlapRatio = 0.6;

    /// <summary>Detection is a convenience on an upload path a person is waiting on. Past this it is
    /// worth less than the wait.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(25);

    public bool IsAvailable => true;

    public async Task<TextDetectionResult> DetectAsync(
        byte[] image, string contentType, CancellationToken ct = default)
    {
        if (image.Length == 0) return TextDetectionResult.Unavailable("There is no artwork to read.");

        var path = Path.Combine(Path.GetTempPath(), $"kurx-ocr-{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllBytesAsync(path, image, ct);

            // `stdout tsv` keeps the geometry. The plain text output would lose exactly the coordinates
            // this whole feature is about.
            var start = new ProcessStartInfo("tesseract", $"\"{path}\" stdout --psm 11 -l eng tsv")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(start);
            if (process is null)
                return TextDetectionResult.Unavailable("Text detection could not be started.");

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(Timeout);

            var stdout = await process.StandardOutput.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(CancellationToken.None);
                log.LogWarning("Tesseract exited {Code}: {Error}", process.ExitCode, Truncate(error));
                return TextDetectionResult.Unavailable("The design could not be read.");
            }

            var (width, height) = Dimensions(image);
            if (width <= 0 || height <= 0)
                return TextDetectionResult.Unavailable("The design's size could not be read.");

            return TextDetectionResult.Detected(ParseTsv(stdout, width, height));
        }
        catch (OperationCanceledException)
        {
            // A slow read is not a broken one, but the creator is waiting. Unavailable is honest: we did
            // not finish looking.
            return TextDetectionResult.Unavailable("Reading the design took too long.");
        }
        catch (Exception ex)
        {
            // Never throws — the editor must open whether or not detection worked.
            log.LogWarning(ex, "Text detection failed");
            return TextDetectionResult.Unavailable("Text detection did not run. Place fields by hand.");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* a temp file, not worth failing over */ }
        }
    }

    /// <summary>
    /// Turns Tesseract's TSV into lines positioned as percentages of the page.
    ///
    /// <para>Static and pure so it can be tested without the binary, which is where the behaviour that
    /// matters actually lives: grouping, confidence filtering and the coordinate conversion. The columns
    /// are Tesseract's own — <c>level page block par line word left top width height conf text</c>.</para>
    /// </summary>
    internal static IReadOnlyList<DetectedTextRegion> ParseTsv(string tsv, int imageWidth, int imageHeight)
    {
        var lines = new Dictionary<(int Block, int Par, int Line), LineBuilder>();

        foreach (var row in tsv.Split('\n'))
        {
            var cell = row.Split('\t');
            if (cell.Length < 12) continue;
            // Level 5 is a word. Anything coarser is a box Tesseract drew around other boxes.
            if (cell[0] != "5") continue;

            var text = cell[11].Trim();
            if (text.Length == 0) continue;
            if (!double.TryParse(cell[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var conf)) continue;
            if (conf < MinWordConfidence) continue;

            if (!int.TryParse(cell[2], out var block) || !int.TryParse(cell[3], out var par)
                || !int.TryParse(cell[4], out var line)) continue;
            if (!int.TryParse(cell[6], out var left) || !int.TryParse(cell[7], out var top)
                || !int.TryParse(cell[8], out var w) || !int.TryParse(cell[9], out var h)) continue;

            var key = (block, par, line);
            if (!lines.TryGetValue(key, out var builder)) lines[key] = builder = new LineBuilder();
            builder.Add(text, left, top, w, h, conf);
        }

        // Lines that survive on their own merits, before any merging.
        var kept = lines.Values
            .Where(b => b.Words > 0)
            .Where(b => Alphanumeric(b.Text()) >= MinAlphanumeric)
            .Where(b => b.ConfidenceSum / b.Words >= MinLineConfidence)
            // A lone word is the shape almost every misread takes, so it needs the engine to be sure.
            .Where(b => b.Words > 1 || b.ConfidenceSum / b.Words >= MinLoneWordConfidence)
            .OrderBy(b => b.Top).ThenBy(b => b.Left)
            .ToList();

        var merged = MergeParagraphs(kept);

        return merged
            .Select(b => new DetectedTextRegion(
                b.Text(),
                b.Left / (double)imageWidth * 100.0,
                b.Top / (double)imageHeight * 100.0,
                (b.Right - b.Left) / (double)imageWidth * 100.0,
                (b.Bottom - b.Top) / (double)imageHeight * 100.0,
                // The boundary's contract is 0–1; Tesseract speaks 0–100.
                Math.Clamp(b.ConfidenceSum / b.Words / 100.0, 0, 1),
                b.Lines))
            // Reading order, so the editor's list matches the page rather than a hash ordering.
            .OrderBy(r => r.Y).ThenBy(r => r.X)
            .ToList();
    }

    /// <summary>
    /// Joins consecutive lines that are visibly one paragraph.
    ///
    /// <para>Each region becomes a patch over the artwork, and many small patches read as damage where one
    /// large patch reads as a panel. A three-line body paragraph covered by three rectangles shows its
    /// seams; covered by one, it does not.</para>
    ///
    /// <para>Two lines merge only when they are BOTH vertically adjacent and horizontally overlapping.
    /// Requiring both is what keeps "Date of Issuance" on the left and "Date of validity" on the right —
    /// same height, no overlap — as the two separate things they are.</para>
    /// </summary>
    private static List<LineBuilder> MergeParagraphs(List<LineBuilder> lines)
    {
        var merged = new List<LineBuilder>();

        foreach (var line in lines)
        {
            var previous = merged.Count > 0 ? merged[^1] : null;
            if (previous is not null && SameParagraph(previous, line)) previous.Absorb(line);
            else merged.Add(line);
        }
        return merged;
    }

    private static bool SameParagraph(LineBuilder a, LineBuilder b)
    {
        var aHeight = a.LastLineHeight;
        var bHeight = b.LastLineHeight;
        if (aHeight <= 0 || bHeight <= 0) return false;

        // Same size of type, or they are not the same paragraph.
        var taller = Math.Max(aHeight, bHeight);
        if (Math.Abs(aHeight - bHeight) / (double)taller > ParagraphHeightTolerance) return false;

        var gap = b.Top - a.Bottom;
        var height = taller;
        // Below the previous line, and close to it. A negative gap means they overlap vertically, which
        // is still one paragraph.
        if (gap > height * ParagraphGapRatio) return false;

        var overlap = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        if (overlap <= 0) return false;
        var narrower = Math.Min(a.Right - a.Left, b.Right - b.Left);
        return narrower > 0 && overlap / (double)narrower >= ParagraphOverlapRatio;
    }

    private static int Alphanumeric(string text) => text.Count(char.IsLetterOrDigit);

    private sealed class LineBuilder
    {
        private readonly List<string> _words = [];
        public int Left = int.MaxValue, Top = int.MaxValue, Right, Bottom;
        /// <summary>Height of the LAST line taken in, not of the accumulated block. Paragraph matching
        /// compares like with like: once two lines merge, the block is two lines tall, and comparing that
        /// against the next single line would break every paragraph at its third line.</summary>
        public int LastLineHeight;
        /// <summary>Lines absorbed, so a caller can size type per line rather than per block.</summary>
        public int Lines = 1;
        public double ConfidenceSum;
        public int Words;

        public void Add(string text, int left, int top, int width, int height, double confidence)
        {
            _words.Add(text);
            Left = Math.Min(Left, left);
            Top = Math.Min(Top, top);
            Right = Math.Max(Right, left + width);
            Bottom = Math.Max(Bottom, top + height);
            LastLineHeight = Math.Max(LastLineHeight, height);
            ConfidenceSum += confidence;
            Words++;
        }

        public string Text() => string.Join(' ', _words);

        /// <summary>Takes another line into this one, growing the box to hold both.</summary>
        public void Absorb(LineBuilder other)
        {
            _words.AddRange(other._words);
            Left = Math.Min(Left, other.Left);
            Top = Math.Min(Top, other.Top);
            Right = Math.Max(Right, other.Right);
            Bottom = Math.Max(Bottom, other.Bottom);
            LastLineHeight = other.LastLineHeight;
            Lines += other.Lines;
            ConfidenceSum += other.ConfidenceSum;
            Words += other.Words;
        }
    }

    /// <summary>Pixel size of the upload, needed to express boxes as percentages. Read with ImageSharp
    /// rather than asked of Tesseract, which reports its own working resolution.</summary>
    private static (int Width, int Height) Dimensions(byte[] image)
    {
        try
        {
            var info = SixLabors.ImageSharp.Image.Identify(image);
            return (info.Width, info.Height);
        }
        catch (Exception)
        {
            return (0, 0);
        }
    }

    private static string Truncate(string value) => value.Length > 300 ? value[..300] : value;
}
