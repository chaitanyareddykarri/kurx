using SixLabors.ImageSharp;

namespace Kurx.Infrastructure.Chat;

/// <summary>What a stored object turned out to be, once the server actually looked at the bytes.</summary>
public record InspectedFile(string ContentType, long SizeBytes, int? Width, int? Height);

/// <summary>
/// Server-authoritative validation for chat attachments (D-110).
///
/// Client-declared metadata is a hint and nothing more: filename, extension and Content-Type are all
/// attacker-controlled. Everything here re-derives the truth from the stored bytes, which is why
/// validation runs on **confirm** — after the object exists — rather than on presign.
///
/// The allow-list is deliberate. A deny-list of dangerous types is a losing game: every new
/// executable container, archive format or polyglot is a bypass. Anything not named here is refused.
/// </summary>
public static class AttachmentPolicy
{
    public const long MaxBytes = 25 * 1024 * 1024;   // matches the existing media ceiling

    /// <summary>Allowed content types, each mapped to the extensions it may legitimately carry.
    /// Both must agree with the magic bytes before a file is accepted.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/gif"] = [".gif"],
        ["image/webp"] = [".webp"],
        ["application/pdf"] = [".pdf"],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = [".pptx"],
        ["text/plain"] = [".txt", ".csv", ".log", ".md"],
        ["text/csv"] = [".csv"],
        ["application/zip"] = [".zip"],

        // ── Voice notes and video (D-295) ─────────────────────────────────────
        // The magic-byte checks below are a FORMAT gate, not a malware gate — they prove a file is
        // shaped like the type it claims, nothing more. Malware is `IFileScanner`'s job, and a real
        // one exists as of D-298 (`FILE_SCANNER=clamav`). Media is the highest-risk category to accept
        // from arbitrary attendees, so running with `FILE_SCANNER=none` in production leaves these
        // formats unscanned; the startup summary says so on every boot.
        ["audio/mpeg"] = [".mp3"],
        ["audio/mp4"] = [".m4a"],
        ["audio/ogg"] = [".ogg", ".opus"],
        ["audio/webm"] = [".weba"],
        ["video/mp4"] = [".mp4"],
        // D-298: iOS camera capture produces QuickTime, not MP4. Without this entry, recording a video
        // on an iPhone failed as an unsupported type — the one platform where video capture matters
        // most. Same ISO-BMFF container as MP4, so the magic-byte check below covers it unchanged.
        ["video/quicktime"] = [".mov"],
        ["video/webm"] = [".webm"],
    };

    /// <summary>Every accepted content type. Used on confirm to re-derive the true type from the
    /// bytes when the client's declaration turns out to be wrong.</summary>
    public static IReadOnlyCollection<string> AllowedContentTypes => Allowed.Keys;

    public static bool IsAllowedContentType(string contentType) => Allowed.ContainsKey(contentType.Trim());

    public static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Extension must be one this content type is allowed to use — blocks `invoice.pdf.exe`
    /// and a .zip masquerading as a .docx alike.</summary>
    public static bool ExtensionMatches(string contentType, string fileName)
    {
        if (!Allowed.TryGetValue(contentType.Trim(), out var extensions)) return false;
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Strips any path the client put in the filename and bounds its length. A filename is
    /// display text and nothing else — it never reaches the filesystem, because storage keys are
    /// server-generated.</summary>
    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        if (string.IsNullOrEmpty(name)) name = "file";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Length > 120 ? name[^120..] : name;
    }

    /// <summary>
    /// Confirms the bytes really are what was claimed, and extracts image dimensions while they are
    /// already in hand.
    ///
    /// Magic-byte checking is what makes the allow-list meaningful — without it a client simply
    /// declares `image/png` and uploads anything. Office formats and .zip share the PK signature
    /// because .docx/.xlsx/.pptx *are* zip containers; distinguishing them further would require
    /// parsing the archive, which buys little against an allow-list that already refuses executables.
    /// </summary>
    public static InspectedFile? Inspect(byte[] bytes, string declaredContentType)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;
        var contentType = declaredContentType.Trim();
        if (!Allowed.ContainsKey(contentType)) return null;
        if (!MagicMatches(bytes, contentType)) return null;

        if (IsImage(contentType))
        {
            try
            {
                // Also a decode check: a file that ImageSharp cannot read is not an image, whatever
                // its header claims.
                var info = Image.Identify(bytes);
                return new InspectedFile(contentType, bytes.Length, info.Width, info.Height);
            }
            catch
            {
                return null;
            }
        }

        return new InspectedFile(contentType, bytes.Length, null, null);
    }

    private static bool MagicMatches(byte[] b, string contentType) => contentType switch
    {
        "image/jpeg" => Starts(b, [0xFF, 0xD8, 0xFF]),
        "image/png" => Starts(b, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "image/gif" => Starts(b, "GIF87a"u8) || Starts(b, "GIF89a"u8),
        // RIFF....WEBP — the size field sits between the two markers.
        "image/webp" => b.Length > 12 && Starts(b, "RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        "application/pdf" => Starts(b, "%PDF-"u8),
        "application/zip"
            or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            or "application/vnd.openxmlformats-officedocument.presentationml.presentation"
            => Starts(b, [0x50, 0x4B]),   // PK — zip container
        // Text has no signature. Reject embedded NULs, which is what separates real text from a
        // binary wearing a .txt extension.
        "text/plain" or "text/csv" => !b.AsSpan(0, Math.Min(b.Length, 8000)).Contains((byte)0),

        // ── Media (D-295) ─────────────────────────────────────────────────────
        // MP3 is either an ID3 tag or a raw frame sync (0xFF 0xEx/0xFx) — a file that starts straight
        // into audio carries no ID3 at all, so testing only for "ID3" would reject valid recordings.
        "audio/mpeg" => Starts(b, "ID3"u8) || (b.Length > 1 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0),
        // ISO-BMFF: "ftyp" at offset 4. Shared by .m4a and .mp4 because they are the same container —
        // which is exactly why the extension must agree with the declared type as well.
        "audio/mp4" or "video/mp4" or "video/quicktime" =>
            b.Length > 12 && b.AsSpan(4, 4).SequenceEqual("ftyp"u8),
        "audio/ogg" => Starts(b, "OggS"u8),
        // Matroska/WebM share the EBML header; the codec distinction is not a container-level fact.
        "audio/webm" or "video/webm" => Starts(b, [0x1A, 0x45, 0xDF, 0xA3]),

        _ => false,
    };

    private static bool Starts(byte[] value, ReadOnlySpan<byte> prefix)
        => value.Length >= prefix.Length && value.AsSpan(0, prefix.Length).SequenceEqual(prefix);
}
