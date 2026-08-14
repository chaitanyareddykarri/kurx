using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Kurx.Infrastructure.Providers;

/// <summary>
/// Dev storage on the local filesystem under LOCALDISK_ROOT.
///
/// Presigned URLs point at the API's own <c>/v1/storage/{key}</c> receiver, which is what makes this
/// provider behave like real object storage from a client's point of view: the client PUTs bytes to
/// a URL it was handed and never learns which backend is behind it.
///
/// **The signature is not decoration.** Before D-110 this provider returned a URL to a route that did
/// not exist, so no upload anywhere in the product ever completed. Adding the receiver without a
/// signature would have been worse than the bug — an unauthenticated PUT to an arbitrary storage key
/// is an open write primitive. The URL therefore carries an expiry and an HMAC over
/// <c>key|exp|maxBytes|contentType</c>, and verification is the only way in. That is the same
/// guarantee S3 presigning gives, built from the secret this deployment already has.
/// </summary>
public class LocalDiskStorage : IStorage
{
    private readonly string _root;
    private readonly string _publicBase;
    private readonly byte[] _signingKey;

    /// <summary>How long a presigned URL stays usable. Short by design — a presign is handed out at
    /// the moment of upload, never stored.</summary>
    public static readonly TimeSpan PresignTtl = TimeSpan.FromMinutes(15);

    public LocalDiskStorage(IConfiguration config)
    {
        _root = Path.GetFullPath(config["LOCALDISK_ROOT"] ?? ".localdata/storage");
        _publicBase = (config["KURX_API_BASE"] ?? "http://localhost:5080").TrimEnd('/');
        // Reuses the deployment's existing secret rather than introducing another one to manage.
        var secret = config["JWT_SECRET"] ?? "dev-only-secret-change-me-0123456789abcdef";
        _signingKey = Encoding.UTF8.GetBytes(secret);
        Directory.CreateDirectory(_root);
    }

    private string PathFor(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
            throw new ArgumentException($"Storage key escapes root: {key}", nameof(key));
        return full;
    }

    /// <summary>Percent-encodes a storage key for a URL **path**, one segment at a time.
    ///
    /// <para><c>Uri.EscapeDataString(key)</c> — what this replaces — encodes the whole key as a single
    /// data component, so the <c>/</c> separators in every real key (<c>events/…</c>, <c>avatars/…</c>,
    /// <c>chat/…</c>) became <c>%2F</c>. ASP.NET Core does not decode <c>%2F</c> back into a path
    /// separator for a catch-all route (it is a path-traversal defence), so the receiver bound
    /// <c>key</c> = <c>"events%2Fdemo%2Fbanner.jpg"</c> while the HMAC had been computed over
    /// <c>"events/demo/banner.jpg"</c>. Verification therefore failed and **every presigned URL for a
    /// multi-segment key answered 403** — which is every key the product actually issues.</para>
    ///
    /// <para>Nothing caught it because the signature is generated and verified by this class, and no
    /// test ever fetched a URL this class produced. Segment-wise encoding keeps the separators literal
    /// and still escapes anything inside a segment.</para></summary>
    private static string EncodeKeyPath(string key) =>
        string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

    public Task<PresignedUpload> PresignPutAsync(string key, string contentType, long maxBytes, CancellationToken ct = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(PresignTtl).ToUnixTimeSeconds();
        var signature = Sign(key, expires, maxBytes, contentType);
        var url = $"{_publicBase}/v1/storage/{EncodeKeyPath(key)}"
                + $"?exp={expires}&max={maxBytes}&ct={Uri.EscapeDataString(contentType)}&sig={signature}";

        return Task.FromResult(new PresignedUpload(key, url,
            new Dictionary<string, string> { ["Content-Type"] = contentType }));
    }

    public Task<string> PresignGetAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(ttl ?? PresignTtl).ToUnixTimeSeconds();
        var signature = Sign(key, expires, 0, "GET");
        return Task.FromResult(
            $"{_publicBase}/v1/storage/{EncodeKeyPath(key)}?exp={expires}&sig={signature}");
    }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
    }

    public Task<byte[]> GetAsync(string key, CancellationToken ct = default)
        => File.ReadAllBytesAsync(PathFor(key), ct);

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(PathFor(key)));

    /// <summary>Removes an object. Used by attachment cleanup so a deleted message cannot leave an
    /// orphaned file behind. Missing is success — cleanup must be safe to re-run.</summary>
    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    // ── Presign signature ──────────────────────────────────────────────────────────────────────

    private string Sign(string key, long expiresUnix, long maxBytes, string contentType)
    {
        using var hmac = new HMACSHA256(_signingKey);
        var payload = Encoding.UTF8.GetBytes($"{key}|{expiresUnix}|{maxBytes}|{contentType}");
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    /// <summary>Validates a presigned PUT: signature, expiry, and the declared size ceiling. The
    /// receiver must reject on any of them.</summary>
    public bool VerifyPutSignature(string key, long expiresUnix, long maxBytes, string contentType, string? signature)
        => Verify(key, expiresUnix, maxBytes, contentType, signature);

    /// <summary>Validates a presigned GET. The content-type slot holds the literal <c>GET</c>, so a
    /// download signature can never be replayed as an upload signature.</summary>
    public bool VerifyGetSignature(string key, long expiresUnix, string? signature)
        => Verify(key, expiresUnix, 0, "GET", signature);

    private bool Verify(string key, long expiresUnix, long maxBytes, string contentType, string? signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix) return false;
        var expected = Sign(key, expiresUnix, maxBytes, contentType);
        // Fixed-time so a valid signature cannot be recovered by measuring comparisons.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature ?? ""));
    }
}
