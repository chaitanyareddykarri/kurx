namespace Kurx.Application.Abstractions;

/// <summary>Protects private signing-key material at rest (AM10, D-102a).
///
/// <para>The signing key is the single most valuable secret in the platform: whoever holds it can
/// mint a token for any user. Storing it as plaintext in Postgres means a database dump — a backup
/// tape, a replica, an SQL-injection read, an over-broad IAM grant — is a complete authentication
/// bypass, silently and permanently, because a leaked key produces valid tokens indistinguishable
/// from real ones.</para>
///
/// <para>Wrapping moves the trust boundary: the database holds ciphertext, and turning it back into
/// a usable key requires a live call to KMS under an IAM identity. A stolen dump is then inert, and
/// the unwrap calls are logged in CloudTrail — so an attacker with database access must also hold
/// AWS credentials, and their use is visible.</para>
///
/// <para>Implementations must be safe to call concurrently.</para></summary>
public interface ISigningKeyProtector
{
    /// <summary>A short, stable identifier for the protection scheme, stored beside the ciphertext.
    /// It exists so a key wrapped under one scheme can still be unwrapped after the platform moves
    /// to another — without it, switching protection would strand every existing key.</summary>
    string SchemeId { get; }

    /// <summary>Wraps plaintext private key material for storage.</summary>
    Task<string> ProtectAsync(string plaintext, CancellationToken ct = default);

    /// <summary>Unwraps material previously produced by <see cref="ProtectAsync"/>.
    /// Throws rather than returning null: a key that cannot be unwrapped must fail loudly, never
    /// silently degrade to "no signing key".</summary>
    Task<string> UnprotectAsync(string protectedValue, CancellationToken ct = default);

    /// <summary>Verifies the protector can actually reach its backing service, so a misconfigured
    /// KMS key ARN or a missing IAM permission fails at startup rather than on the first login.</summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}
