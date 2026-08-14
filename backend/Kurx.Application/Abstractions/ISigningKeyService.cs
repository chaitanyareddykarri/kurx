namespace Kurx.Application.Abstractions;

/// <summary>The private half of the currently-signing key, plus the `kid` to stamp on the token.</summary>
public record ActiveSigningKey(string KeyId, string Algorithm, string PrivateKeyPkcs8);

/// <summary>A public key as published in JWKS. Only ever public material.</summary>
public record SigningKeyPublic(string KeyId, string Algorithm, string PublicKeySpki);

/// <summary>Asymmetric JWT signing-key lifecycle (AM10, D-099).
///
/// <para>The lifecycle exists to make rotation <b>zero-downtime</b>. Exactly one key is Active and
/// signs. Rotating moves it to Retiring — it stops signing but keeps <b>validating</b>, so tokens
/// minted a second before the rotation stay valid until they expire on their own. Only once no live
/// token can bear a key is it Retired and its private half destroyed.</para></summary>
public interface ISigningKeyService
{
    /// <summary>The key to sign with, creating and activating one on first use so a fresh
    /// deployment needs no manual bootstrap step.</summary>
    Task<ActiveSigningKey> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Every key a token might legitimately bear: the Active one plus those still in their
    /// grace period. This is exactly what JWKS publishes and what token validation trusts.
    /// A Compromised key is <b>never</b> included.</summary>
    Task<IReadOnlyList<SigningKeyPublic>> GetValidationKeysAsync(CancellationToken ct = default);

    /// <summary>Normal rotation: mint a new key, activate it, and move the previous one to Retiring.
    /// Tokens already issued keep working for the remainder of their lifetime.</summary>
    Task<SigningKeyPublic> RotateAsync(CancellationToken ct = default);

    /// <summary>Retires keys whose grace period has elapsed and destroys their private material.
    /// Idempotent; safe to run on a schedule.</summary>
    Task<int> RetireExpiredAsync(CancellationToken ct = default);

    /// <summary>Emergency replacement. The key is removed from JWKS <b>immediately</b> with no grace
    /// period, which invalidates every token it signed — deliberately, since the alternative is
    /// continuing to honour an attacker's forgeries. A replacement is activated in the same
    /// operation so the platform keeps issuing tokens.</summary>
    Task<SigningKeyPublic> MarkCompromisedAsync(string keyId, string reason, CancellationToken ct = default);
}
