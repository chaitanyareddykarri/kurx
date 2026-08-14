namespace Kurx.Application.Abstractions;

/// <summary>Outcome of verifying a candidate password against a stored hash.</summary>
/// <param name="Ok">Whether the password matched.</param>
/// <param name="NeedsRehash">True when the stored hash used weaker parameters than the current
/// policy. The caller should re-hash and store on a successful sign-in — the only moment the
/// plaintext is available — so cost can be raised over time without a mass reset.</param>
public readonly record struct PasswordVerification(bool Ok, bool NeedsRehash);

/// <summary>Password hashing (OWASP Password Storage). Argon2id with per-password random salt.
///
/// <para>An interface rather than a static helper for one reason that matters: tests need to swap in a
/// cheap hasher. Argon2id at production cost is ~50-100ms by design, and a suite that signs in hundreds
/// of times would spend minutes burning memory — which pressures whoever runs it into weakening the
/// real parameters. Keeping the seam means production cost stays honest.</para></summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a password, returning a self-describing PHC string that embeds the algorithm,
    /// version, parameters and salt. Never returns a bare digest.</summary>
    string Hash(string password);

    /// <summary>Verifies a candidate against a stored PHC string in constant time. Returns
    /// <c>Ok=false</c> for malformed or unknown-format hashes rather than throwing — a corrupt stored
    /// hash must fail closed as "wrong password", never as a 500 that distinguishes it from one.</summary>
    PasswordVerification Verify(string password, string storedHash);
}
