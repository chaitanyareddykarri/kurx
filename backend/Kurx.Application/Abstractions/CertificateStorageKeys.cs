namespace Kurx.Application.Abstractions;

/// <summary>
/// Where the certificate module's objects live, and what it will accept back (D-344, Phase 2).
///
/// <para><b>The prefix is the authorization.</b> Every key is scoped to one event, and
/// <see cref="BelongsToEvent"/> is checked before any caller-supplied key is stored on a template or
/// certificate. Without that pairing, "render the design at this key" is a primitive for reading any
/// object on the platform — a caller could point a template at another event's uploads, or at a
/// government-ID scan, and the renderer would fetch it. Minting keys here and validating them here means
/// a key obtained any other way is refused.</para>
///
/// <para>Building and validating live together on purpose: they are one rule, and splitting them is how
/// they drift.</para>
/// </summary>
public static class CertificateStorageKeys
{
    /// <summary>Everything the module writes sits under this, so an operator can identify, lifecycle or
    /// bulk-remove certificate objects without pattern-matching individual paths.</summary>
    public static string EventPrefix(Guid eventId) => $"events/{eventId}/certificates/";

    /// <summary>The organiser's uploaded artwork. Versioned in the path, so replacing a background never
    /// overwrites the bytes a previously issued certificate was rendered from.</summary>
    public static string TemplateBackground(Guid eventId, Guid templateId, int version, string extension) =>
        $"{EventPrefix(eventId)}templates/{templateId:N}/v{version}/background{Normalise(extension)}";

    /// <summary>A template held in the creator's library rather than on an event. Keyed by owner for the
    /// same reason event assets are keyed by event: the prefix is what authorises access to it.</summary>
    public static string LibraryTemplateBackground(Guid ownerUserId, Guid templateId, int version, string extension) =>
        $"users/{ownerUserId}/certificate-templates/{templateId:N}/v{version}/background{Normalise(extension)}";

    /// <summary>The uploaded participant list for one run.</summary>
    public static string BatchSource(Guid eventId, Guid batchId, string extension) =>
        $"{EventPrefix(eventId)}batches/{batchId:N}/source{Normalise(extension)}";

    /// <summary>A rendered preview from the sample batch. Not the recipient's document — previews are
    /// disposable and are keyed apart from issued artefacts so they can be cleaned up independently.</summary>
    public static string BatchPreview(Guid eventId, Guid batchId, int rowNumber) =>
        $"{EventPrefix(eventId)}batches/{batchId:N}/preview/{rowNumber:D6}.png";

    /// <summary>An issued certificate's PDF. Immutable: a correction is a new certificate with a new id,
    /// so this key is never rewritten.</summary>
    public static string CertificatePdf(Guid eventId, Guid certificateRowId) =>
        $"{EventPrefix(eventId)}issued/{certificateRowId:N}/certificate.pdf";

    /// <summary>An issued certificate's print-resolution PNG.</summary>
    public static string CertificatePng(Guid eventId, Guid certificateRowId) =>
        $"{EventPrefix(eventId)}issued/{certificateRowId:N}/certificate.png";

    /// <summary>Whether a key sits under this event's certificate prefix.
    ///
    /// <para>Ordinal comparison, deliberately: a culture-aware or case-insensitive match would let
    /// <c>EVENTS/…</c> pass on a case-sensitive object store where it addresses a different object
    /// entirely.</para></summary>
    public static bool BelongsToEvent(string? key, Guid eventId) =>
        key is not null && key.StartsWith(EventPrefix(eventId), StringComparison.Ordinal);

    /// <summary>Whether a key sits under this creator's template library prefix.</summary>
    public static bool BelongsToLibrary(string? key, Guid ownerUserId) =>
        key is not null && key.StartsWith($"users/{ownerUserId}/certificate-templates/", StringComparison.Ordinal);

    /// <summary>Whether a caller-supplied key may be stored against a template.
    ///
    /// <para>A template is either an event's or a library entry, so exactly one of the two prefixes is
    /// acceptable — and which one depends on where the template currently lives, not on which the caller
    /// would prefer.</para></summary>
    public static bool IsAcceptableTemplateKey(string? key, Guid? eventId, Guid ownerUserId) =>
        eventId is Guid id ? BelongsToEvent(key, id) : BelongsToLibrary(key, ownerUserId);

    /// <summary>Normalises a file extension to a leading dot, or nothing when absent. Guards against a
    /// key ending in a bare dot, which some object stores accept and most tooling then mishandles.</summary>
    private static string Normalise(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return "";
        var trimmed = extension.Trim().TrimStart('.');
        return trimmed.Length == 0 ? "" : "." + trimmed.ToLowerInvariant();
    }
}
