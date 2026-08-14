using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Auth;
using Xunit;

namespace Kurx.Tests;

/// <summary>Argon2id password hashing (OWASP Password Storage). Pure unit tests — no database, no HTTP.
/// Deliberately few: each hash costs ~19 MiB and tens of milliseconds by design, so the suite asserts the
/// properties that matter rather than exhaustively re-deriving the same value.</summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new Argon2idPasswordHasher();

    [Fact]
    public void Hash_emits_a_phc_string_carrying_its_own_parameters()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        // The parameters must travel with the hash: that is what lets cost be raised later without
        // invalidating every existing password.
        Assert.StartsWith("$argon2id$v=19$", hash);
        Assert.Contains("m=19456,t=2,p=1", hash);
        Assert.Equal(6, hash.Split('$').Length);
    }

    [Fact]
    public void The_same_password_hashes_differently_every_time()
    {
        const string password = "correct horse battery staple";

        // A per-password random salt is what makes a stolen table non-precomputable. Identical output
        // for identical input would mean no salt, and rainbow tables would work.
        Assert.NotEqual(_hasher.Hash(password), _hasher.Hash(password));
    }

    [Fact]
    public void Verify_accepts_the_correct_password_and_rejects_everything_else()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash).Ok);
        Assert.False(_hasher.Verify("Correct horse battery staple", hash).Ok);   // case matters
        Assert.False(_hasher.Verify("correct horse battery stapl", hash).Ok);    // truncation
        Assert.False(_hasher.Verify("", hash).Ok);
    }

    [Fact]
    public void A_password_at_current_cost_is_not_flagged_for_rehash()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("correct horse battery staple", hash).NeedsRehash);
    }

    [Fact]
    public void A_password_stored_below_current_cost_is_flagged_for_rehash_on_success_only()
    {
        // A hash produced under weaker parameters — what an older deployment would have written.
        // Parameters are read from the string, so it must still verify.
        var weak = WeakHash("correct horse battery staple", memoryKib: 8192, iterations: 1);

        var good = _hasher.Verify("correct horse battery staple", weak);
        Assert.True(good.Ok);
        Assert.True(good.NeedsRehash);

        // Rehashing is only meaningful when we actually hold the plaintext, i.e. on a correct password.
        var bad = _hasher.Verify("wrong password", weak);
        Assert.False(bad.Ok);
        Assert.False(bad.NeedsRehash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$onlyfourparts")]
    [InlineData("$bcrypt$v=19$m=19456,t=2,p=1$c2FsdA==$aGFzaA==")]      // wrong algorithm
    [InlineData("$argon2id$v=19$m=x,t=2,p=1$c2FsdA==$aGFzaA==")]        // unparseable cost
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$!!!notbase64!!!$aGFzaA==")]
    public void A_malformed_stored_hash_fails_closed_instead_of_throwing(string stored)
    {
        // A corrupt row must be indistinguishable from a wrong password. Throwing here would turn it
        // into a 500 and hand an attacker a way to tell "this account exists but is broken" apart from
        // "wrong password".
        var result = _hasher.Verify("correct horse battery staple", stored);

        Assert.False(result.Ok);
        Assert.False(result.NeedsRehash);
    }

    /// <summary>Builds a PHC string using deliberately weaker parameters than current policy, to prove
    /// old hashes keep verifying and get flagged for upgrade.</summary>
    private static string WeakHash(string password, int memoryKib, int iterations)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        using var argon = new Konscious.Security.Cryptography.Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = 1,
        };
        var hash = argon.GetBytes(32);
        return $"$argon2id$v=19$m={memoryKib},t={iterations},p=1$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}
