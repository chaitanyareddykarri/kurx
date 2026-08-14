using Kurx.Domain.Enums;
using SixLabors.ImageSharp;

namespace Kurx.Infrastructure.Posts;

/// <summary>What a stored object turned out to be, once the server actually looked at the bytes.</summary>
public record InspectedMedia(PostMediaKind Kind, string ContentType, long SizeBytes, int? Width, int? Height);

/// <summary>
/// Server-authoritative validation for post media (D-262). Same shape and same reasoning as
/// <see cref="Chat.AttachmentPolicy"/> — allow-list, magic bytes, extension agreement, validation on
/// confirm rather than presign — but a different list: posts carry video, which chat does not, and posts
/// do not carry .zip, which is a file to send someone rather than something to publish on a feed.
///
/// <para>Extending chat's list instead would have changed what is accepted in every event chat room as a
/// side effect of shipping a feed. Two lists is the smaller change.</para>
/// </summary>
public static class PostMediaPolicy
{
    public const long MaxImageBytes = 25 * 1024 * 1024;
    public const long MaxDocumentBytes = 25 * 1024 * 1024;

    /// <summary>
    /// Video gets the same 25 MB ceiling as everything else, and that is a platform limit rather than a
    /// product choice.
    ///
    /// <para>The only storage provider implemented today is <c>LocalDiskStorage</c>, whose presigned URL
    /// points back at this API — so an upload is an ordinary Kestrel request, capped by
    /// <c>MaxRequestBodySize</c> at ~28.6 MB. A higher ceiling here would be a promise the upload path
    /// cannot keep: the client would presign successfully and then take an opaque 413 from Kestrel
    /// part-way through the bytes, instead of an <c>invalid_media</c> it can explain. Chat's 25 MB was
    /// picked against the same wall.</para>
    ///
    /// <para><b>Raise this when real object storage lands</b> — the client then PUTs straight to the
    /// bucket and Kestrel is not in the path at all.</para>
    /// </summary>
    public const long MaxVideoBytes = 25 * 1024 * 1024;

    // Per-post caps (D-262). One video because a feed post is a unit of attention, not an album.
    public const int MaxImages = 10;
    public const int MaxVideos = 1;
    public const int MaxDocuments = 5;

    private static readonly Dictionary<string, (PostMediaKind Kind, string[] Extensions)> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = (PostMediaKind.Image, [".jpg", ".jpeg"]),
            ["image/png"] = (PostMediaKind.Image, [".png"]),
            ["image/gif"] = (PostMediaKind.Image, [".gif"]),
            ["image/webp"] = (PostMediaKind.Image, [".webp"]),
            ["video/mp4"] = (PostMediaKind.Video, [".mp4", ".m4v"]),
            ["video/webm"] = (PostMediaKind.Video, [".webm"]),
            ["application/pdf"] = (PostMediaKind.Document, [".pdf"]),
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = (PostMediaKind.Document, [".docx"]),
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = (PostMediaKind.Document, [".xlsx"]),
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = (PostMediaKind.Document, [".pptx"]),
            ["text/plain"] = (PostMediaKind.Document, [".txt", ".md", ".log"]),
            ["text/csv"] = (PostMediaKind.Document, [".csv"]),
        };

    public static IReadOnlyCollection<string> AllowedContentTypes => Allowed.Keys;

    public static bool IsAllowedContentType(string contentType) => Allowed.ContainsKey(contentType.Trim());

    public static PostMediaKind? KindOf(string contentType)
        => Allowed.TryGetValue(contentType.Trim(), out var entry) ? entry.Kind : null;

    public static long MaxBytesFor(PostMediaKind kind) => kind switch
    {
        PostMediaKind.Video => MaxVideoBytes,
        PostMediaKind.Document => MaxDocumentBytes,
        _ => MaxImageBytes,
    };

    /// <summary>Extension must be one this content type is allowed to use — blocks `clip.mp4.exe` and a
    /// .zip masquerading as a .docx alike.</summary>
    public static bool ExtensionMatches(string contentType, string fileName)
    {
        if (!Allowed.TryGetValue(contentType.Trim(), out var entry)) return false;
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && entry.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Confirms the bytes really are what was claimed, and extracts image dimensions while they
    /// are already in hand. Video dimensions and duration are deliberately not derived: no probe is wired,
    /// and a fabricated number is worse than a null the contract already allows.</summary>
    public static InspectedMedia? Inspect(byte[] bytes, string declaredContentType)
    {
        var contentType = declaredContentType.Trim();
        if (bytes.Length == 0 || !Allowed.TryGetValue(contentType, out var entry)) return null;
        if (bytes.Length > MaxBytesFor(entry.Kind)) return null;
        if (!MagicMatches(bytes, contentType)) return null;

        if (entry.Kind == PostMediaKind.Image)
        {
            try
            {
                // Also a decode check: a file ImageSharp cannot read is not an image, whatever its
                // header claims.
                var info = Image.Identify(bytes);
                return new InspectedMedia(entry.Kind, contentType, bytes.Length, info.Width, info.Height);
            }
            catch
            {
                return null;
            }
        }

        return new InspectedMedia(entry.Kind, contentType, bytes.Length, null, null);
    }

    /// <summary>Re-derives the true content type from the bytes, trying the declared one first. A file that
    /// matches nothing on the allow-list is refused outright.</summary>
    public static InspectedMedia? InspectAgainstAllowList(byte[] bytes, string? declaredContentType)
    {
        if (declaredContentType is not null)
        {
            var direct = Inspect(bytes, declaredContentType);
            if (direct is not null) return direct;
        }
        foreach (var candidate in Allowed.Keys)
        {
            var match = Inspect(bytes, candidate);
            if (match is not null) return match;
        }
        return null;
    }

    private static bool MagicMatches(byte[] b, string contentType) => contentType switch
    {
        "image/jpeg" => Starts(b, [0xFF, 0xD8, 0xFF]),
        "image/png" => Starts(b, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "image/gif" => Starts(b, "GIF87a"u8) || Starts(b, "GIF89a"u8),
        // RIFF....WEBP — the size field sits between the two markers.
        "image/webp" => b.Length > 12 && Starts(b, "RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        // ISO-BMFF: a 4-byte box size, then the 'ftyp' box type. The brand that follows distinguishes
        // mp4 from mov/3gp, which this list does not accept separately, so the box marker is enough.
        "video/mp4" => b.Length > 12 && b.AsSpan(4, 4).SequenceEqual("ftyp"u8),
        "video/webm" => Starts(b, [0x1A, 0x45, 0xDF, 0xA3]),   // EBML header, shared with .mkv
        "application/pdf" => Starts(b, "%PDF-"u8),
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            or "application/vnd.openxmlformats-officedocument.presentationml.presentation"
            => Starts(b, [0x50, 0x4B]),   // PK — the OOXML zip container
        // Text has no signature. Rejecting embedded NULs is what separates real text from a binary
        // wearing a .txt extension.
        "text/plain" or "text/csv" => !b.AsSpan(0, Math.Min(b.Length, 8000)).Contains((byte)0),
        _ => false,
    };

    private static bool Starts(byte[] value, ReadOnlySpan<byte> prefix)
        => value.Length >= prefix.Length && value.AsSpan(0, prefix.Length).SequenceEqual(prefix);
}
