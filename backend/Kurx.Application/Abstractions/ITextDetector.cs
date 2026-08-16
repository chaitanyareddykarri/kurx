namespace Kurx.Application.Abstractions;

/// <summary>
/// Finding the text already printed on a certificate design (D-355, Phase 12).
///
/// <para><b>An extension point, not a feature.</b> OCR is deferred by decision, and the editor is required
/// to work without it — every field is placed by hand today and that path is unchanged. This boundary
/// exists so that adding an engine later is a registration change rather than a rewrite of the editor.</para>
///
/// <para><b>Deliberately vendor-neutral.</b> Nothing here mentions a page, an engine, a language model or a
/// confidence scale anyone in particular uses. Tesseract, Textract, Vision and Azure all differ in what
/// they return; what they agree on is "here is some text, and here is where it was". That agreement is the
/// whole interface, and the reason it can be implemented by any of them without this file changing.</para>
///
/// <para><b>Capability is a question you can ask.</b> <see cref="IsAvailable"/> exists so a caller can find
/// out that detection is off <i>without</i> submitting an image and interpreting a failure. An editor
/// needs to decide whether to offer a button before it has anything to detect.</para>
/// </summary>
public interface ITextDetector
{
    /// <summary>Whether an engine is configured. False on every build that has not registered one, and
    /// checked rather than inferred: "no regions found" and "nothing looked" are different answers, and a
    /// caller that cannot tell them apart will report a blank design as having no text on it.</summary>
    bool IsAvailable { get; }

    /// <summary>Looks for text in an image.
    ///
    /// <para>Never throws for the unavailable case — that is an ordinary answer, returned as
    /// <see cref="TextDetectionResult.Unavailable"/>. A design being un-analysable must not be able to
    /// break the editor that is showing it.</para></summary>
    /// <param name="image">The artwork bytes, as uploaded.</param>
    /// <param name="contentType">The image's media type, e.g. <c>image/png</c>.</param>
    Task<TextDetectionResult> DetectAsync(
        byte[] image, string contentType, CancellationToken ct = default);
}

/// <param name="Available">False when no engine is configured. When false, <see cref="Regions"/> is empty
/// and carries no meaning — it is not a claim that the design has no text on it.</param>
/// <param name="Reason">Why detection did not run, for the caller to show or log. Null when it did.</param>
public sealed record TextDetectionResult(
    bool Available,
    IReadOnlyList<DetectedTextRegion> Regions,
    string? Reason = null)
{
    /// <summary>The answer every build gives today.</summary>
    public static TextDetectionResult Unavailable(string reason) => new(false, [], reason);

    /// <summary>A completed detection, including one that genuinely found nothing.</summary>
    public static TextDetectionResult Detected(IReadOnlyList<DetectedTextRegion> regions) =>
        new(true, regions);
}

/// <summary>One piece of text and where it sits on the design.
///
/// <para>Coordinates are <b>percentages of the page</b>, 0–100, matching
/// <see cref="CertificateFieldInput"/> exactly. Engines report pixels against whatever resolution they
/// were handed; converting at the boundary is what lets a detected region be dropped straight onto the
/// canvas as a field without the editor learning anything about image dimensions.</para></summary>
/// <param name="Confidence">0–1. Normalised here because every engine scales this differently, and a
/// caller filtering on 0.8 should mean the same thing whichever one is installed.</param>
/// <param name="Lines">How many lines of the original the region covers. A caller sizing type from the
/// box needs this: a three-line paragraph is three times as tall as its type, and treating the block
/// height as one line renders the first few words at enormous size.</param>
public sealed record DetectedTextRegion(
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    double Confidence,
    int Lines = 1);
