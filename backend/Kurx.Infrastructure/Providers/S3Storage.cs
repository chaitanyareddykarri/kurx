using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Kurx.Infrastructure.Providers;

/// <summary>
/// S3-compatible object storage (D-344, Phase 2). Works against real AWS S3 and against any
/// S3-compatible server (MinIO, Ceph, Garage) through <c>S3_ENDPOINT</c>.
///
/// <para><b>Two clients, and that is the whole design.</b> The server and the browser do not necessarily
/// reach storage at the same address: a container talks to <c>http://minio:9000</c> while the user's
/// browser must use <c>https://files.example.com</c>. SigV4 signs the host, so a URL generated for the
/// internal address cannot be rewritten to the public one afterwards without invalidating the signature —
/// which is why the split is made at signing time rather than patched up later. <see cref="_server"/>
/// performs API calls; <see cref="_presigner"/> exists only to compute URLs, and is configured with
/// <c>S3_PUBLIC_ENDPOINT</c> so the host that gets signed is the host that appears in the URL.</para>
///
/// <para>When <c>S3_PUBLIC_ENDPOINT</c> is unset the two are the same client — the ordinary case where
/// storage is reachable at one address from everywhere, including plain AWS S3.</para>
///
/// <para><b>Presigning is local.</b> <c>GetPreSignedURLAsync</c> computes an HMAC over the request; it
/// performs no network call, so minting a URL per row of a listing is cheap and the presigner client
/// never needs to reach the endpoint it is configured with.</para>
///
/// <para><b>Nothing here is public.</b> No ACL is ever set, so objects inherit the bucket's default —
/// private. Access is exclusively through time-limited presigned URLs.</para>
///
/// <para><b>Presigned URLs are always https, whatever the endpoint's scheme.</b> Measured, not assumed:
/// AWS SDK v4 emits <c>https://</c> for a presign even when <c>ServiceURL</c> is <c>http://…</c>, and
/// there is no knob to change it — <c>ClientConfig.UseHttp</c> documents itself as not applying once an
/// explicit <c>ServiceURL</c> is set, and <c>AmazonS3Config</c> exposes no scheme property. That is the
/// right behaviour for the browser-facing URL, which is why <c>S3_PUBLIC_ENDPOINT</c> is required to be
/// https rather than being silently upgraded to something the operator did not configure. Server-side
/// API calls are unaffected and use the configured endpoint as written, so a plaintext internal
/// <c>S3_ENDPOINT</c> remains supported.</para>
/// </summary>
public class S3Storage : IStorage, IDisposable
{
    private readonly IAmazonS3 _server;
    private readonly IAmazonS3 _presigner;
    private readonly bool _presignerIsSeparate;
    private readonly string _bucket;
    private readonly TimeSpan _putTtl;
    private readonly TimeSpan _getTtl;

    public S3Storage(IConfiguration config)
    {
        var options = S3StorageOptions.FromConfiguration(config);
        _bucket = options.Bucket;
        _putTtl = options.PutTtl;
        _getTtl = options.GetTtl;

        var credentials = options.AccessKeyId is { Length: > 0 } id && options.SecretAccessKey is { Length: > 0 } secret
            ? new BasicAWSCredentials(id, secret)
            // No explicit keys: fall through to the SDK's default chain (instance profile, environment,
            // shared config). That is how a deployment on AWS avoids holding long-lived keys at all.
            : (AWSCredentials?)null;

        _server = Build(credentials, options.Region, options.Endpoint);
        _presignerIsSeparate = options.PublicEndpoint is { Length: > 0 }
                               && !string.Equals(options.PublicEndpoint, options.Endpoint, StringComparison.OrdinalIgnoreCase);
        _presigner = _presignerIsSeparate
            ? Build(credentials, options.Region, options.PublicEndpoint)
            : _server;
    }

    private static IAmazonS3 Build(AWSCredentials? credentials, string region, string? endpoint)
    {
        var cfg = new AmazonS3Config();

        if (endpoint is { Length: > 0 })
        {
            cfg.ServiceURL = endpoint;
            // Path-style for a custom endpoint. Virtual-host style would put the bucket in the hostname
            // (`bucket.minio.internal`), which needs wildcard DNS the average S3-compatible deployment
            // does not have. Real AWS is left on the SDK's default virtual-host behaviour below.
            cfg.ForcePathStyle = true;
            // Still required with a custom ServiceURL: SigV4 signs the region, and the SDK cannot infer
            // one from a hostname it does not recognise.
            cfg.AuthenticationRegion = region;
        }
        else
        {
            cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
        }

        return credentials is null ? new AmazonS3Client(cfg) : new AmazonS3Client(credentials, cfg);
    }

    // ── Presigned URLs — the browser's only route to an object ──────────────────────────────────

    public async Task<PresignedUpload> PresignPutAsync(
        string key, string contentType, long maxBytes, CancellationToken ct = default)
    {
        var url = await _presigner.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(_putTtl),
            ContentType = contentType,
        });

        // Content-Type is signed into the URL, so the client MUST send the same value or S3 rejects the
        // upload. Returning it as a required header is what stops that being a surprise.
        return new PresignedUpload(key, url, new Dictionary<string, string> { ["Content-Type"] = contentType });
    }

    public Task<string> PresignGetAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default) =>
        _presigner.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl ?? _getTtl),
        });

    // ── Server-side operations ──────────────────────────────────────────────────────────────────

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content);
        await _server.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            // No CannedACL: the object inherits the bucket's policy, which for certificate storage is
            // private. Setting PublicRead here would make every certificate world-readable by its key.
        }, ct);
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken ct = default)
    {
        using var response = await _server.GetObjectAsync(_bucket, key, ct);
        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _server.GetObjectMetadataAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // The only exception that means "absent". Anything else — throttling, a network fault, a
            // credential problem — must propagate: reporting an unreachable bucket as "no such object"
            // would let a caller treat an outage as a missing file and act on it.
            return false;
        }
    }

    public void Dispose()
    {
        _server.Dispose();
        if (_presignerIsSeparate) _presigner.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Validated S3 configuration (D-344, Phase 2).
///
/// <para>Separated from the adapter so the rules are testable without constructing an S3 client, and so a
/// misconfiguration fails at startup with a sentence naming the variable rather than at the first upload
/// with an SDK exception.</para>
/// </summary>
public sealed record S3StorageOptions(
    string Bucket,
    string Region,
    string? Endpoint,
    string? PublicEndpoint,
    string? AccessKeyId,
    string? SecretAccessKey,
    TimeSpan PutTtl,
    TimeSpan GetTtl)
{
    public const int DefaultPutTtlSeconds = 900;
    public const int DefaultGetTtlSeconds = 3600;

    public static S3StorageOptions FromConfiguration(IConfiguration config)
    {
        var bucket = Trim(config["S3_BUCKET"]);
        if (bucket is null)
            throw new InvalidOperationException(
                "STORAGE_PROVIDER=s3 requires S3_BUCKET. See .env.example.");

        var region = Trim(config["S3_REGION"]) ?? Trim(config["AWS_REGION"]);
        if (region is null)
            throw new InvalidOperationException(
                "STORAGE_PROVIDER=s3 requires S3_REGION (or AWS_REGION). SigV4 signs the region, so it "
                + "cannot be defaulted. See .env.example.");

        var endpoint = Trim(config["S3_ENDPOINT"]);
        var publicEndpoint = Trim(config["S3_PUBLIC_ENDPOINT"]);

        // A public endpoint without a server endpoint is a configuration that cannot be honoured: there
        // would be nothing to talk to for server-side operations, and silently using the public one for
        // both would defeat the split the operator was clearly reaching for.
        if (publicEndpoint is not null && endpoint is null)
            throw new InvalidOperationException(
                "S3_PUBLIC_ENDPOINT is set but S3_ENDPOINT is not. The public endpoint is used only for "
                + "signing browser URLs; the server still needs an endpoint to call. Set both, or neither "
                + "for plain AWS S3.");

        // `Uri.TryCreate(…, Absolute)` alone is not enough: "minio:9000" parses happily as scheme
        // "minio" with path "9000", so a missing scheme would sail through and only fail at the first
        // request. The scheme has to be checked explicitly.
        foreach (var (name, value) in new[] { ("S3_ENDPOINT", endpoint), ("S3_PUBLIC_ENDPOINT", publicEndpoint) })
            if (value is not null
                && (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
                throw new InvalidOperationException(
                    $"{name}={value} must be an absolute http:// or https:// URL, e.g. "
                    + "https://files.example.com or http://minio:9000.");

        // The public endpoint is what a browser fetches private certificates from, and presigned URLs are
        // emitted as https regardless (see S3Storage) — so an http:// value here would be both insecure
        // and a lie about what the generated URLs will say. Refused rather than silently upgraded.
        if (publicEndpoint is not null && !publicEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"S3_PUBLIC_ENDPOINT={publicEndpoint} must be https://. It is the address a browser "
                + "fetches private certificate documents from, and presigned URLs are always issued as "
                + "https. S3_ENDPOINT (server-side) may remain http:// on a private network.");

        return new S3StorageOptions(
            bucket, region, endpoint, publicEndpoint,
            Trim(config["AWS_ACCESS_KEY_ID"]), Trim(config["AWS_SECRET_ACCESS_KEY"]),
            TimeSpan.FromSeconds(Seconds(config, "PRESIGN_PUT_TTL", DefaultPutTtlSeconds)),
            TimeSpan.FromSeconds(Seconds(config, "PRESIGN_GET_TTL", DefaultGetTtlSeconds)));
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int Seconds(IConfiguration config, string key, int fallback) =>
        int.TryParse(config[key], out var v) && v > 0 ? v : fallback;
}
