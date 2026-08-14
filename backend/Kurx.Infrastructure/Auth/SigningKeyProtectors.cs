using System.Security.Cryptography;
using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>The one KMS operation set this application needs.
///
/// <para>Segregated from <see cref="IAmazonKeyManagementService"/> (~70 members covering key
/// creation, deletion, policy and grant management) for the same reason as the secrets client: the
/// application may <b>use</b> a key and nothing else. It cannot create, schedule deletion of, or
/// re-policy a CMK even by accident, and a test double is a few lines rather than a few hundred.</para></summary>
public interface IKmsClient
{
    Task<(byte[] Plaintext, byte[] Ciphertext)> GenerateDataKeyAsync(string keyId, CancellationToken ct);
    Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken ct);
    Task<string> DescribeKeyAsync(string keyId, CancellationToken ct);
}

/// <summary>Adapts the AWS SDK to <see cref="IKmsClient"/>.</summary>
public class AwsKmsClientAdapter(IAmazonKeyManagementService kms) : IKmsClient
{
    public async Task<(byte[] Plaintext, byte[] Ciphertext)> GenerateDataKeyAsync(string keyId, CancellationToken ct)
    {
        var response = await kms.GenerateDataKeyAsync(new GenerateDataKeyRequest
        {
            KeyId = keyId,
            KeySpec = DataKeySpec.AES_256,
        }, ct);
        return (response.Plaintext.ToArray(), response.CiphertextBlob.ToArray());
    }

    public async Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken ct)
    {
        var response = await kms.DecryptAsync(new DecryptRequest
        {
            CiphertextBlob = new MemoryStream(ciphertext),
        }, ct);
        return response.Plaintext.ToArray();
    }

    public async Task<string> DescribeKeyAsync(string keyId, CancellationToken ct)
    {
        var response = await kms.DescribeKeyAsync(new DescribeKeyRequest { KeyId = keyId }, ct);
        return response.KeyMetadata.KeyId;
    }
}

/// <summary>Development protector: stores the key as-is (AM10, D-102a).
///
/// <para>Honest about what it is. It provides <b>no protection</b> and exists so local development
/// needs no AWS account. Production selects the KMS protector; startup refuses to run this one in
/// Production (see <c>DependencyInjection</c>), because a protector that silently does nothing is
/// exactly the failure this whole abstraction is meant to prevent.</para></summary>
public class NullSigningKeyProtector : ISigningKeyProtector
{
    public string SchemeId => "none";

    public Task<string> ProtectAsync(string plaintext, CancellationToken ct = default)
        => Task.FromResult(plaintext);

    public Task<string> UnprotectAsync(string protectedValue, CancellationToken ct = default)
        => Task.FromResult(protectedValue);

    public Task<bool> HealthCheckAsync(CancellationToken ct = default) => Task.FromResult(true);
}

/// <summary>Envelope encryption of private signing keys with AWS KMS (AM10, D-102a).
///
/// ## Envelope encryption, and why not `kms:Encrypt` directly
/// KMS's direct `Encrypt` caps at 4 KB and bills per call. Envelope encryption instead asks KMS for
/// a **data key** — returned both in plaintext and wrapped under the CMK — encrypts the payload
/// locally with AES-GCM, and stores `wrapped data key ‖ nonce ‖ tag ‖ ciphertext`. Only the small
/// wrapped data key ever goes back to KMS. This is the standard AWS pattern and the one the SDK's
/// own encryption libraries implement.
///
/// ## Minimising plaintext lifetime
/// The plaintext data key is zeroed with <see cref="CryptographicOperations.ZeroMemory"/> in a
/// `finally` as soon as the AES operation completes, so it is not left for the GC to release at an
/// arbitrary later time. This is a real but **bounded** mitigation and it is worth being precise
/// about the limit: .NET strings are immutable and may be copied by the GC, so the unwrapped private
/// key itself cannot be reliably scrubbed. Eliminating that would mean never materialising the key
/// in managed memory — i.e. signing inside KMS or an HSM, which costs a network round trip per token
/// and is the correct next step only if the threat model demands it.
///
/// ## AES-GCM
/// Authenticated encryption, so a tampered ciphertext fails to decrypt rather than yielding garbage
/// that would be loaded as a "key". Nonce is 12 bytes from the CSPRNG, never reused: a repeat under
/// the same data key would be catastrophic for GCM, and each wrap generates a fresh data key anyway.
///
/// ⚠ <b>Never executed against AWS from this environment.</b> Reviewed and unit-tested against a
/// fake KMS client; the real API interaction, IAM policy and CMK configuration are
/// PENDING DEPLOYMENT CONFIGURATION (D-102a).
/// </summary>
public class KmsSigningKeyProtector(
    IKmsClient kms,
    IConfiguration config,
    ILogger<KmsSigningKeyProtector> log) : ISigningKeyProtector
{
    public string SchemeId => "aws-kms-v1";

    private const int NonceSize = 12;   // AES-GCM standard
    private const int TagSize = 16;

    private string KeyId => config["AWS_KMS_SIGNING_KEY_ID"]
        ?? throw new InvalidOperationException(
            "AWS_KMS_SIGNING_KEY_ID is not set; the KMS signing-key protector cannot start.");

    public async Task<string> ProtectAsync(string plaintext, CancellationToken ct = default)
    {
        var (dataKey, wrappedDataKey) = await kms.GenerateDataKeyAsync(KeyId, ct);
        try
        {
            var payload = Encoding.UTF8.GetBytes(plaintext);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ciphertext = new byte[payload.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Encrypt(nonce, payload, ciphertext, tag);

            // Length-prefixed so the wrapped key can be recovered without assuming its size —
            // KMS ciphertext blob length is not contractually fixed.
            using var output = new MemoryStream();
            using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(wrappedDataKey.Length);
                writer.Write(wrappedDataKey);
                writer.Write(nonce);
                writer.Write(tag);
                writer.Write(ciphertext);
            }
            return Convert.ToBase64String(output.ToArray());
        }
        finally
        {
            // Shortest practical plaintext lifetime for the data key.
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public async Task<string> UnprotectAsync(string protectedValue, CancellationToken ct = default)
    {
        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(protectedValue);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Protected signing key is not valid base64.", ex);
        }

        using var input = new MemoryStream(blob);
        using var reader = new BinaryReader(input);

        int wrappedLength;
        byte[] wrappedDataKey, nonce, tag, ciphertext;
        try
        {
            wrappedLength = reader.ReadInt32();
            if (wrappedLength <= 0 || wrappedLength > blob.Length)
                throw new InvalidOperationException("Protected signing key has an implausible header.");

            wrappedDataKey = reader.ReadBytes(wrappedLength);
            nonce = reader.ReadBytes(NonceSize);
            tag = reader.ReadBytes(TagSize);
            ciphertext = reader.ReadBytes((int)(input.Length - input.Position));
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidOperationException("Protected signing key is truncated.", ex);
        }

        var dataKey = await kms.DecryptAsync(wrappedDataKey, ct);
        try
        {
            var plaintext = new byte[ciphertext.Length];
            using (var aes = new AesGcm(dataKey, TagSize))
            {
                // Throws AuthenticationTagMismatchException if anything was tampered with — which is
                // the point of AEAD: a modified ciphertext must never decrypt to something that gets
                // loaded as a signing key.
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException ex)
        {
            log.LogCritical(ex, "Signing key failed authenticated decryption — possible tampering");
            throw new InvalidOperationException("Protected signing key failed integrity verification.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>Confirms the CMK is reachable and usable under the current IAM identity, so a wrong
    /// ARN, a missing `kms:GenerateDataKey`/`kms:Decrypt` grant, or a wrong region fails at startup
    /// instead of on the first token issuance.</summary>
    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await kms.DescribeKeyAsync(KeyId, ct);
            // Describe alone only proves the key exists. A round trip proves the IAM identity can
            // actually wrap AND unwrap — the two permissions that matter, and the ones most often
            // half-granted.
            var probe = await ProtectAsync("kurx-kms-healthcheck", ct);
            var recovered = await UnprotectAsync(probe, ct);
            return recovered == "kurx-kms-healthcheck";
        }
        catch (Exception ex)
        {
            log.LogCritical(ex, "KMS signing-key protector health check failed");
            return false;
        }
    }
}
