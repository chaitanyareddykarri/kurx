using System.Security.Cryptography;
using System.Text;

namespace Kurx.Infrastructure.Auth;

/// <summary>ES256 (ECDSA P-256 + SHA-256) signature verification for device-bound credentials (ADR-A3).
/// The public key is stored as base64 SubjectPublicKeyInfo (SPKI). Accepts both DER-encoded signatures
/// (Android Keystore / iOS Secure Enclave / OpenSSL) and IEEE-P1363 fixed-field (raw r‖s) form, so it
/// works regardless of which the client platform emits. Never throws — malformed input verifies false.</summary>
public static class DeviceSignatures
{
    public static bool VerifyEs256(string publicKeySpkiBase64, string message, string signatureBase64)
    {
        byte[] spki, sig;
        try
        {
            spki = Convert.FromBase64String(publicKeySpkiBase64);
            sig = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var data = Encoding.UTF8.GetBytes(message);
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
            return ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)
                || ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Stable identifier for a device public key — SHA-256 over the raw SPKI bytes. Stored on a
    /// refresh token to pin which key must sign to rotate it (sender-constrained refresh, AM5/D-081).</summary>
    public static string Thumbprint(string publicKeySpkiBase64)
    {
        var spki = Convert.FromBase64String(publicKeySpkiBase64);
        return Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();
    }
}
