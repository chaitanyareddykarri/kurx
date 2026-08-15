using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// The text detector every build ships with today (D-344, Phase 12).
///
/// <para>It detects nothing, and says so. OCR is deferred by decision; the editor's requirement is that
/// manual placement works without it, and it does — this exists so the boundary is real rather than
/// hypothetical, and so registering an engine later is a one-line change in
/// <c>DependencyInjection</c> instead of a new dependency threaded through the editor.</para>
///
/// <para><b>It does not pretend.</b> No sample regions, no plausible-looking guesses, no empty success.
/// <see cref="TextDetectionResult.Available"/> is false and the caller can tell "nothing looked" apart
/// from "nothing found" — which matters, because the second is a statement about the design and the first
/// is a statement about this deployment.</para>
///
/// <para>Marked clearly as a stub, per the project rule that anything temporary or mocked says so.</para>
/// </summary>
public class UnavailableTextDetector : ITextDetector
{
    private const string Reason =
        "Automatic text detection is not enabled on this deployment. Place fields by hand.";

    public bool IsAvailable => false;

    public Task<TextDetectionResult> DetectAsync(
        byte[] image, string contentType, CancellationToken ct = default) =>
        // Deliberately does not look at the image at all, and deliberately does not throw. An
        // un-analysable design must not be able to break the editor showing it.
        Task.FromResult(TextDetectionResult.Unavailable(Reason));
}
