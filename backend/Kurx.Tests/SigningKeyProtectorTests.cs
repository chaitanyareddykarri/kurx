using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kurx.Tests;

/// <summary>KMS envelope encryption of private signing keys (AM10, D-102a).
///
/// <para>Exercised against a fake KMS client, which verifies <b>our</b> envelope format, tamper
/// detection and error handling. It deliberately proves nothing about the real KMS API, IAM policy
/// or CMK configuration — that is PENDING DEPLOYMENT CONFIGURATION.</para></summary>
public class SigningKeyProtectorTests
{
    /// <summary>Stands in for KMS: "wrapping" is a reversible marker, which is enough to verify the
    /// envelope layout and that the wrapped key is what gets sent back for decryption.</summary>
    private sealed class FakeKms : IKmsClient
    {
        public int GenerateCalls;
        public int DecryptCalls;
        public bool Fail;
        public string? LastKeyId;

        private static readonly byte[] WrapMarker = "WRAPPED:"u8.ToArray();

        public Task<(byte[] Plaintext, byte[] Ciphertext)> GenerateDataKeyAsync(string keyId, CancellationToken ct)
        {
            GenerateCalls++;
            LastKeyId = keyId;
            if (Fail) throw new InvalidOperationException("kms unavailable");

            var dataKey = RandomNumberGenerator.GetBytes(32);
            return Task.FromResult((dataKey, WrapMarker.Concat(dataKey).ToArray()));
        }

        public Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken ct)
        {
            DecryptCalls++;
            if (Fail) throw new InvalidOperationException("kms unavailable");
            return Task.FromResult(ciphertext.Skip(WrapMarker.Length).ToArray());
        }

        public Task<string> DescribeKeyAsync(string keyId, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("kms unavailable");
            return Task.FromResult(keyId);
        }
    }

    private static KmsSigningKeyProtector Protector(FakeKms kms, string? keyId = "arn:aws:kms:test:key/abc")
        => new(kms,
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["AWS_KMS_SIGNING_KEY_ID"] = keyId }).Build(),
            NullLogger<KmsSigningKeyProtector>.Instance);

    /// <summary>A realistic payload: an actual PKCS#8 P-256 private key.</summary>
    private static string SamplePrivateKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
    }

    [Fact]
    public async Task A_wrapped_key_round_trips_intact()
    {
        var kms = new FakeKms();
        var protector = Protector(kms);
        var key = SamplePrivateKey();

        var wrapped = await protector.ProtectAsync(key);
        var recovered = await protector.UnprotectAsync(wrapped);

        Assert.Equal(key, recovered);
        // Round-trip must survive being usable as an actual signing key, not merely as a string.
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(recovered), out _);
        Assert.Equal(256, ecdsa.KeySize);
    }

    [Fact]
    public async Task The_stored_blob_never_contains_the_plaintext_key()
    {
        var kms = new FakeKms();
        var key = SamplePrivateKey();

        var wrapped = await Protector(kms).ProtectAsync(key);

        // This is the entire point: a database dump must not yield a usable signing key.
        Assert.DoesNotContain(key, wrapped);
        Assert.NotEqual(key, wrapped);
    }

    [Fact]
    public async Task Each_wrap_uses_a_fresh_data_key_and_nonce()
    {
        var kms = new FakeKms();
        var protector = Protector(kms);
        var key = SamplePrivateKey();

        var first = await protector.ProtectAsync(key);
        var second = await protector.ProtectAsync(key);

        // Identical plaintext must not produce identical ciphertext — a repeated nonce under the
        // same data key is catastrophic for AES-GCM.
        Assert.NotEqual(first, second);
        Assert.Equal(2, kms.GenerateCalls);
    }

    [Fact]
    public async Task A_tampered_ciphertext_is_rejected_rather_than_silently_decrypted()
    {
        var kms = new FakeKms();
        var protector = Protector(kms);
        var wrapped = await protector.ProtectAsync(SamplePrivateKey());

        // Flip a byte near the end — inside the AES-GCM ciphertext region.
        var bytes = Convert.FromBase64String(wrapped);
        bytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        // AEAD is what stops a modified blob decrypting to garbage that then gets loaded as a key.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => protector.UnprotectAsync(tampered));
        Assert.Contains("integrity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Malformed_input_fails_loudly_instead_of_returning_something_unusable()
    {
        var protector = Protector(new FakeKms());

        // A key that cannot be unwrapped must throw. Returning null or empty would degrade into
        // "no signing key", which is far harder to diagnose than an explicit failure.
        await Assert.ThrowsAsync<InvalidOperationException>(() => protector.UnprotectAsync("not-base64!!"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => protector.UnprotectAsync(Convert.ToBase64String([1, 2, 3])));
    }

    [Fact]
    public async Task A_kms_outage_propagates_rather_than_producing_an_unprotected_key()
    {
        var kms = new FakeKms { Fail = true };

        // Falling back to storing plaintext when KMS is down would defeat the whole mechanism at
        // exactly the moment it is least observable.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Protector(kms).ProtectAsync(SamplePrivateKey()));
    }

    [Fact]
    public async Task The_configured_cmk_is_the_one_used()
    {
        var kms = new FakeKms();
        await Protector(kms, "arn:aws:kms:ap-south-1:123:key/prod-signing").ProtectAsync(SamplePrivateKey());

        Assert.Equal("arn:aws:kms:ap-south-1:123:key/prod-signing", kms.LastKeyId);
    }

    [Fact]
    public async Task A_missing_key_id_fails_immediately_and_says_so()
    {
        var protector = Protector(new FakeKms(), keyId: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => protector.ProtectAsync("x"));
        Assert.Contains("AWS_KMS_SIGNING_KEY_ID", ex.Message);
    }

    [Fact]
    public async Task The_health_check_proves_both_wrap_and_unwrap_permissions()
    {
        var kms = new FakeKms();

        Assert.True(await Protector(kms).HealthCheckAsync());
        // Describe alone would only prove the key exists; a round trip proves the IAM identity holds
        // BOTH kms:GenerateDataKey and kms:Decrypt — the pair most often half-granted.
        Assert.True(kms.GenerateCalls >= 1);
        Assert.True(kms.DecryptCalls >= 1);
    }

    [Fact]
    public async Task The_health_check_reports_failure_instead_of_throwing()
    {
        // Startup should be able to report "KMS is not usable" cleanly rather than crashing inside
        // the check itself.
        Assert.False(await Protector(new FakeKms { Fail = true }).HealthCheckAsync());
    }

    [Fact]
    public void The_scheme_id_is_recorded_so_a_future_scheme_can_still_unwrap_old_keys()
    {
        // Without a stable scheme marker, changing protection would strand every existing key.
        Assert.Equal("aws-kms-v1", Protector(new FakeKms()).SchemeId);
        Assert.Equal("none", new NullSigningKeyProtector().SchemeId);
    }

    [Fact]
    public async Task The_development_protector_is_honest_about_providing_no_protection()
    {
        // It must not pretend: production selection refuses it (see DependencyInjection), and this
        // pins that it is a pass-through rather than something that looks like encryption.
        var protector = new NullSigningKeyProtector();
        const string key = "plaintext-key";

        Assert.Equal(key, await protector.ProtectAsync(key));
        Assert.Equal(key, await protector.UnprotectAsync(key));
    }
}
