using Amazon.S3;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Providers;
using Kurx.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// S3 storage configuration and presigned-URL behaviour (D-355, Phase 2).
///
/// <para><b>What these can and cannot prove.</b> Presigning is a local HMAC computation — the SDK
/// contacts nothing — so the URL a real deployment would hand a browser is fully assertable here without
/// a bucket, credentials or network. Round-tripping bytes is not: that needs a live S3-compatible server,
/// and asserting it against a fake would only prove the fake works. So these tests cover the part that is
/// both testable and historically where this goes wrong: which HOST ends up in the signed URL.</para>
///
/// <para>The failure being guarded against is concrete. Sign for the internal endpoint and every
/// certificate download 403s from the browser, because SigV4 covers the host and the URL cannot be
/// rewritten afterwards.</para>
/// </summary>
public class S3StorageConfigurationTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    /// <summary>The minimum an infrastructure registration needs before it reaches the storage switch.
    /// Without these, the guard tests would trip over an unrelated earlier validation and prove nothing
    /// about storage.</summary>
    private static IConfiguration Bootable(params (string Key, string? Value)[] extra) => Config(
    [
        // Deliberately not the localhost/kurx dev shape: Production validation rejects that before it
        // ever reaches the storage switch, and the guard tests would then assert the wrong refusal.
        ("ConnectionStrings:Default", "Host=db.internal;Port=5432;Database=kurx;Username=kurx_app;Password=Zx9Q2m7Lp4Tv8Kd1Rs6Wn3Yb"),
        ("JWT_SECRET", "test-only-secret-not-for-production-0123456789abcdef"),
        ("TICKET_HMAC_SECRET", "test-only-ticket-hmac-secret"),
        ("OTP_PEPPER", "test-only-otp-pepper"),
        ("REDIS_CONNECTION", "redis.internal:6379"),
        .. extra,
    ]);

    private static (string Key, string? Value)[] Valid(params (string Key, string? Value)[] extra) =>
    [
        ("S3_BUCKET", "kurx-certificates"),
        ("S3_REGION", "ap-south-1"),
        ("AWS_ACCESS_KEY_ID", "test-key-id"),
        ("AWS_SECRET_ACCESS_KEY", "test-secret-key"),
        .. extra,
    ];

    // ── Configuration validation ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_missing_bucket_is_refused_by_name()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => S3StorageOptions.FromConfiguration(Config(("S3_REGION", "ap-south-1"))));

        Assert.Contains("S3_BUCKET", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Region cannot be defaulted: SigV4 signs it, so a wrong guess produces signatures the
    /// server rejects with an error that names nothing useful.</summary>
    [Fact]
    public void A_missing_region_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => S3StorageOptions.FromConfiguration(Config(("S3_BUCKET", "b"))));

        Assert.Contains("S3_REGION", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Region_falls_back_to_the_shared_aws_region()
    {
        var options = S3StorageOptions.FromConfiguration(
            Config(("S3_BUCKET", "b"), ("AWS_REGION", "eu-west-1")));

        Assert.Equal("eu-west-1", options.Region);
    }

    /// <summary>A public endpoint with no server endpoint cannot be honoured — there would be nothing for
    /// server-side calls to reach, and quietly using the public one for both would defeat the split.</summary>
    [Fact]
    public void A_public_endpoint_without_a_server_endpoint_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => S3StorageOptions.FromConfiguration(
            Config(Valid(("S3_PUBLIC_ENDPOINT", "https://files.example.com")))));

        Assert.Contains("S3_ENDPOINT", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("S3_ENDPOINT")]
    [InlineData("S3_PUBLIC_ENDPOINT")]
    public void An_endpoint_without_a_scheme_is_refused(string variable)
    {
        var extra = variable == "S3_PUBLIC_ENDPOINT"
            ? new (string, string?)[] { ("S3_ENDPOINT", "http://minio:9000"), (variable, "files.example.com") }
            : [(variable, "minio:9000")];

        var ex = Assert.Throws<InvalidOperationException>(
            () => S3StorageOptions.FromConfiguration(Config(Valid(extra))));

        Assert.Contains(variable, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Presign_lifetimes_default_and_are_overridable()
    {
        var defaults = S3StorageOptions.FromConfiguration(Config(Valid()));
        Assert.Equal(S3StorageOptions.DefaultPutTtlSeconds, (int)defaults.PutTtl.TotalSeconds);
        Assert.Equal(S3StorageOptions.DefaultGetTtlSeconds, (int)defaults.GetTtl.TotalSeconds);

        var overridden = S3StorageOptions.FromConfiguration(
            Config(Valid(("PRESIGN_PUT_TTL", "60"), ("PRESIGN_GET_TTL", "120"))));
        Assert.Equal(60, (int)overridden.PutTtl.TotalSeconds);
        Assert.Equal(120, (int)overridden.GetTtl.TotalSeconds);

        // A nonsense value falls back rather than producing a zero-second URL that is expired on arrival.
        var nonsense = S3StorageOptions.FromConfiguration(Config(Valid(("PRESIGN_PUT_TTL", "-5"))));
        Assert.Equal(S3StorageOptions.DefaultPutTtlSeconds, (int)nonsense.PutTtl.TotalSeconds);
    }

    // ── Presigned URL host: the reason the split endpoint exists ────────────────────────────────

    /// <summary>The load-bearing assertion of this phase: the browser-facing URL is signed for, and
    /// addressed to, the PUBLIC host — never the internal one the server uses.</summary>
    [Fact]
    public async Task A_presigned_get_is_addressed_to_the_public_endpoint()
    {
        using var storage = new S3Storage(Config(Valid(
            ("S3_ENDPOINT", "http://minio:9000"),
            ("S3_PUBLIC_ENDPOINT", "https://files.example.com"))));

        var url = await storage.PresignGetAsync("events/e/certificates/issued/c/certificate.pdf");

        Assert.StartsWith("https://files.example.com/", url, StringComparison.Ordinal);
        Assert.DoesNotContain("minio:9000", url, StringComparison.Ordinal);
        // Signed, not merely addressed — a rewritten URL would carry no signature at all.
        Assert.Contains("X-Amz-Signature=", url, StringComparison.Ordinal);
        Assert.Contains("X-Amz-Credential=", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_presigned_put_is_addressed_to_the_public_endpoint()
    {
        using var storage = new S3Storage(Config(Valid(
            ("S3_ENDPOINT", "http://minio:9000"),
            ("S3_PUBLIC_ENDPOINT", "https://files.example.com"))));

        var upload = await storage.PresignPutAsync("events/e/certificates/templates/t/v1/background.png", "image/png", 1024);

        Assert.StartsWith("https://files.example.com/", upload.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("minio:9000", upload.Url, StringComparison.Ordinal);
        // Content-Type is signed into the URL, so the client has to send the same value back.
        Assert.Equal("image/png", upload.Headers["Content-Type"]);
    }

    /// <summary>With no public endpoint the two collapse to one — the ordinary single-address case.</summary>
    [Fact]
    public async Task Without_a_public_endpoint_the_server_endpoint_is_used_for_signing()
    {
        using var storage = new S3Storage(Config(Valid(("S3_ENDPOINT", "https://storage.example.com"))));

        var url = await storage.PresignGetAsync("events/e/certificates/issued/c/certificate.pdf");

        Assert.StartsWith("https://storage.example.com/", url, StringComparison.Ordinal);
    }

    /// <summary>A custom endpoint gets path-style addressing: the bucket appears in the PATH, not folded
    /// into the hostname, because virtual-host style needs wildcard DNS that MinIO-style deployments
    /// generally do not have.</summary>
    [Fact]
    public async Task A_custom_endpoint_uses_path_style_addressing()
    {
        using var storage = new S3Storage(Config(Valid(("S3_ENDPOINT", "http://minio:9000"))));

        var url = await storage.PresignGetAsync("some/key.pdf");

        // The bucket is in the PATH, not folded into the hostname.
        Assert.Contains("minio:9000/kurx-certificates/some/key.pdf", url, StringComparison.Ordinal);
    }

    /// <summary>Presigned URLs are https even for an http:// endpoint. Measured behaviour of AWS SDK v4,
    /// pinned here so an upgrade that changes it is caught: <c>ClientConfig.UseHttp</c> does not apply
    /// once <c>ServiceURL</c> is set, and <c>AmazonS3Config</c> has no scheme property. It is the correct
    /// outcome for a browser-facing URL carrying a private document — which is why S3_PUBLIC_ENDPOINT is
    /// required to be https rather than quietly upgraded.</summary>
    [Fact]
    public async Task Presigned_urls_are_always_https()
    {
        using var storage = new S3Storage(Config(Valid(("S3_ENDPOINT", "http://minio:9000"))));

        Assert.StartsWith("https://", await storage.PresignGetAsync("some/key.pdf"), StringComparison.Ordinal);
    }

    [Fact]
    public void An_insecure_public_endpoint_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => S3StorageOptions.FromConfiguration(
            Config(Valid(("S3_ENDPOINT", "http://minio:9000"), ("S3_PUBLIC_ENDPOINT", "http://files.example.com")))));

        Assert.Contains("https://", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Plain AWS S3 keeps the SDK's own virtual-host behaviour rather than being forced
    /// path-style, which AWS treats as deprecated.</summary>
    [Fact]
    public async Task Plain_aws_uses_the_sdk_default_host_style()
    {
        using var storage = new S3Storage(Config(Valid()));

        var url = await storage.PresignGetAsync("some/key.pdf");

        Assert.Contains("kurx-certificates.s3", url, StringComparison.Ordinal);
        Assert.Contains("amazonaws.com", url, StringComparison.Ordinal);
    }

    /// <summary>Credentials belong in the signature, never in a readable query parameter. The access key
    /// id legitimately appears inside X-Amz-Credential; the SECRET must appear nowhere.</summary>
    [Fact]
    public async Task A_presigned_url_never_carries_the_secret_key()
    {
        using var storage = new S3Storage(Config(Valid(("S3_ENDPOINT", "http://minio:9000"))));

        var url = await storage.PresignGetAsync("some/key.pdf");

        Assert.DoesNotContain("test-secret-key", url, StringComparison.Ordinal);
    }

    /// <summary>A shorter TTL must actually shorten the URL's life, or "expiring links" is decoration.</summary>
    [Fact]
    public async Task A_requested_ttl_is_honoured()
    {
        using var storage = new S3Storage(Config(Valid(("S3_ENDPOINT", "http://minio:9000"))));

        var shortLived = await storage.PresignGetAsync("k", TimeSpan.FromMinutes(1));
        var longLived = await storage.PresignGetAsync("k", TimeSpan.FromHours(6));

        Assert.Contains("X-Amz-Expires=60", shortLived, StringComparison.Ordinal);
        Assert.Contains("X-Amz-Expires=21600", longLived, StringComparison.Ordinal);
    }

    // ── Production guard ────────────────────────────────────────────────────────────────────────

    /// <summary>Production must refuse ephemeral local disk.
    ///
    /// <para>The failure this prevents is quiet and total: LocalDiskStorage writes into the container
    /// filesystem, so a deployment that simply forgot the variable comes up healthy, accepts uploads,
    /// issues certificates — and loses every stored document on the next release while Postgres keeps the
    /// rows. The certificate then resolves to a storage key that 404s, which reads as corruption rather
    /// than as a wipe. Same reasoning as FILE_SCANNER=none (D-338), which already fails closed.</para></summary>
    [Fact]
    public void Production_refuses_local_disk_storage()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddKurxInfrastructure(
            Bootable(("STORAGE_PROVIDER", "localdisk")), isProduction: true));

        Assert.Contains("STORAGE_PROVIDER=localdisk", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Production", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Development keeps it — the repo's whole local loop depends on storage that needs no
    /// bucket, and the dev default is deliberately localdisk.</summary>
    [Fact]
    public void Development_still_allows_local_disk_storage()
    {
        var services = new ServiceCollection();

        var ex = Record.Exception(() => services.AddKurxInfrastructure(
            Bootable(("STORAGE_PROVIDER", "localdisk")), isProduction: false));

        Assert.Null(ex);
    }

    /// <summary>An unrecognised value fails fast rather than silently degrading to local disk — the
    /// failure mode the provider-selection convention exists to prevent.</summary>
    [Fact]
    public void An_unknown_storage_provider_is_refused()
    {
        var services = new ServiceCollection();

        Assert.Throws<NotSupportedException>(() => services.AddKurxInfrastructure(
            Bootable(("STORAGE_PROVIDER", "azure-blob")), isProduction: false));
    }

    // ── Event-prefix ownership ──────────────────────────────────────────────────────────────────

    [Fact]
    public void An_event_key_belongs_only_to_its_own_event()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var key = CertificateStorageKeys.CertificatePdf(mine, Guid.NewGuid());

        Assert.True(CertificateStorageKeys.BelongsToEvent(key, mine));
        Assert.False(CertificateStorageKeys.BelongsToEvent(key, theirs));
        Assert.False(CertificateStorageKeys.BelongsToEvent(null, mine));
    }

    /// <summary>The check is ordinal. A case-insensitive match would accept a key that addresses a
    /// different object entirely on a case-sensitive object store.</summary>
    [Fact]
    public void Prefix_matching_is_case_sensitive()
    {
        var eventId = Guid.NewGuid();
        var key = CertificateStorageKeys.CertificatePdf(eventId, Guid.NewGuid()).ToUpperInvariant();

        Assert.False(CertificateStorageKeys.BelongsToEvent(key, eventId));
    }

    [Fact]
    public void A_key_from_outside_the_module_is_refused()
    {
        var eventId = Guid.NewGuid();

        Assert.False(CertificateStorageKeys.BelongsToEvent($"users/{Guid.NewGuid()}/avatar/x", eventId));
        Assert.False(CertificateStorageKeys.BelongsToEvent($"events/{eventId}/media/banner.jpg", eventId));
        Assert.False(CertificateStorageKeys.BelongsToEvent("../../etc/passwd", eventId));
    }

    /// <summary>An event template accepts only event-prefixed keys; a library template only
    /// owner-prefixed ones. Neither may borrow the other's prefix.</summary>
    [Fact]
    public void Template_keys_are_accepted_only_under_the_prefix_the_template_actually_lives_in()
    {
        var eventId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        var eventKey = CertificateStorageKeys.TemplateBackground(eventId, templateId, 1, "png");
        var libraryKey = CertificateStorageKeys.LibraryTemplateBackground(ownerId, templateId, 1, "png");

        Assert.True(CertificateStorageKeys.IsAcceptableTemplateKey(eventKey, eventId, ownerId));
        Assert.False(CertificateStorageKeys.IsAcceptableTemplateKey(libraryKey, eventId, ownerId));

        Assert.True(CertificateStorageKeys.IsAcceptableTemplateKey(libraryKey, null, ownerId));
        Assert.False(CertificateStorageKeys.IsAcceptableTemplateKey(eventKey, null, ownerId));
    }

    /// <summary>Replacing a background writes a new version path, so the bytes an already-issued
    /// certificate was rendered from are never overwritten.</summary>
    [Fact]
    public void A_new_template_version_gets_its_own_path()
    {
        var eventId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        Assert.NotEqual(
            CertificateStorageKeys.TemplateBackground(eventId, templateId, 1, "png"),
            CertificateStorageKeys.TemplateBackground(eventId, templateId, 2, "png"));
    }

    [Theory]
    [InlineData("png", ".png")]
    [InlineData(".PNG", ".png")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Extensions_are_normalised(string? extension, string expectedSuffix)
    {
        var key = CertificateStorageKeys.TemplateBackground(Guid.NewGuid(), Guid.NewGuid(), 1, extension!);

        Assert.EndsWith("background" + expectedSuffix, key, StringComparison.Ordinal);
    }
}
