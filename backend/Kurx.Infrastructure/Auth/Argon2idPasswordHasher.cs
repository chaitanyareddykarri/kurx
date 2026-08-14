using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Auth;

/// <summary>Argon2id password hashing (OWASP Password Storage Cheat Sheet).
///
/// <para><b>Why Argon2id.</b> It is memory-hard: an attacker's advantage from custom silicon (GPU/ASIC)
/// is bounded by memory bandwidth rather than raw compute, which is exactly the asymmetry PBKDF2 and
/// bcrypt lack. The "id" variant resists both side-channel and time-memory-tradeoff attacks.</para>
///
/// <para><b>Parameters</b> are OWASP's balanced configuration: m=19456 KiB (19 MiB), t=2, p=1. They are
/// written into every hash in PHC format, so verification uses the parameters the hash was *created*
/// with. Raising the cost later therefore does not invalidate a single existing password — old hashes
/// keep verifying and are flagged <see cref="PasswordVerification.NeedsRehash"/> so they upgrade
/// silently on the next successful sign-in.</para>
///
/// <para><b>Format:</b> <c>$argon2id$v=19$m=19456,t=2,p=1$&lt;base64 salt&gt;$&lt;base64 hash&gt;</c> —
/// the standard PHC string, so the stored value is portable to any other Argon2 implementation.</para></summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    // Current policy. Only ever raise these — lowering them would silently mark strong existing
    // hashes as "needs rehash" and downgrade them on next sign-in.
    private const int MemoryKib = 19456;    // 19 MiB
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Version = 19;         // Argon2 v1.3

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, MemoryKib, Iterations, Parallelism, HashBytes);
        return $"$argon2id$v={Version}$m={MemoryKib},t={Iterations},p={Parallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public PasswordVerification Verify(string password, string storedHash)
    {
        // The underlying Argon2 implementation throws on an empty password. An empty submission is
        // simply a wrong password and must be answered as one — letting it throw would turn a trivial
        // request into a 500 that an attacker can tell apart from a normal rejection.
        if (string.IsNullOrEmpty(password))
            return new PasswordVerification(false, false);

        if (!TryParse(storedHash, out var p))
            return new PasswordVerification(false, false);

        var candidate = Derive(password, p.Salt, p.MemoryKib, p.Iterations, p.Parallelism, p.Hash.Length);

        // Fixed-time comparison: a byte-by-byte early exit would leak how much of the hash matched,
        // which is enough to reconstruct it one byte at a time given enough attempts.
        var ok = CryptographicOperations.FixedTimeEquals(candidate, p.Hash);

        // Only meaningful when the password was actually correct — there is nothing to upgrade otherwise.
        var needsRehash = ok && (p.MemoryKib < MemoryKib || p.Iterations < Iterations);
        return new PasswordVerification(ok, needsRehash);
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int parallelism, int outputBytes)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon.GetBytes(outputBytes);
    }

    private readonly record struct Parsed(byte[] Salt, byte[] Hash, int MemoryKib, int Iterations, int Parallelism);

    /// <summary>Parses a PHC string. Returns false for anything unexpected — a malformed stored hash is
    /// treated as a failed verification, never an exception, so a corrupted row cannot be distinguished
    /// from a wrong password by timing or by status code.</summary>
    private static bool TryParse(string stored, out Parsed parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(stored)) return false;

        // $argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>  →  ["", "argon2id", "v=19", "m=..,t=..,p=..", salt, hash]
        var parts = stored.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id") return false;

        var costs = parts[3].Split(',');
        if (costs.Length != 3) return false;

        if (!TryReadCost(costs[0], "m=", out var memoryKib) ||
            !TryReadCost(costs[1], "t=", out var iterations) ||
            !TryReadCost(costs[2], "p=", out var parallelism))
            return false;

        try
        {
            parsed = new Parsed(Convert.FromBase64String(parts[4]), Convert.FromBase64String(parts[5]),
                memoryKib, iterations, parallelism);
        }
        catch (FormatException)
        {
            return false;
        }

        return parsed.Salt.Length > 0 && parsed.Hash.Length > 0;
    }

    private static bool TryReadCost(string segment, string prefix, out int value)
    {
        value = 0;
        return segment.StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(segment.AsSpan(prefix.Length), out value)
               && value > 0;
    }
}
