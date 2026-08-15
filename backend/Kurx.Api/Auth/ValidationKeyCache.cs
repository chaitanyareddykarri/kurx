using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Kurx.Api.Auth;

/// <summary>Turns a published SPKI into an <see cref="ECDsaSecurityKey"/> once per key instead of once
/// per request (D-344).
///
/// <para>The resolver in <c>Program.cs</c> used to call <c>ECDsa.Create()</c> inline and never dispose it.
/// <see cref="ECDsaSecurityKey"/> does not take ownership of the <see cref="ECDsa"/> it wraps — every other
/// call site in this repository disposes its own, <c>TokenService</c> included, which wraps one in an
/// <c>ECDsaSecurityKey</c> and still disposes it at scope exit. So every request reaching this branch
/// abandoned a native crypto handle to the finalizer queue and re-ran a base64 decode and an SPKI import
/// that produce the same answer every time.</para>
///
/// <para><b>Dormant today, deliberately fixed anyway.</b> Nothing reaches this branch in production: all six
/// issuance sites call the synchronous HS256 <c>CreateAccessToken</c>, so real access tokens carry no
/// <c>kid</c> and return at the legacy line above (verified 2026-08-15 — a live token's header reads
/// <c>{"alg":"HS256"}</c>). That is D-099's documented "built but OFF" state, not drift. The cost of
/// leaving it is that the defect goes live with the ES256 cut-over, on the hottest path in the
/// application, as part of a change whose reviewers will be looking at issuance rather than at
/// validation.</para>
///
/// <para><b>This is a parse cache, never an authority.</b> The caller still resolves <c>kid</c> against the
/// live published key set before asking for a key here, so retiring a key (D-099,
/// <c>SigningKeyMaintenanceJob</c>) still rejects tokens signed with it — a cached entry can never
/// resurrect a retired key, because a retired key never reaches this class. The cache key includes the SPKI
/// so re-publishing a different public key under an existing <c>kid</c> parses afresh rather than serving
/// the old material.</para>
///
/// <para>Growth is bounded by the number of distinct keys this process ever sees published — rotation is
/// periodic (D-099) and a deploy restarts the process, so there is nothing to evict. Entries are only ever
/// created for keys the database published, so no caller-supplied <c>kid</c> can grow it.</para></summary>
internal static class ValidationKeyCache
{
    // Lazy, not a bare factory: ConcurrentDictionary.GetOrAdd may run the factory on several threads for
    // the same key and discard all but one winner. Discarding a loser here would abandon exactly the
    // ECDsa handle this class exists to stop abandoning.
    private static readonly ConcurrentDictionary<string, Lazy<ECDsaSecurityKey>> Parsed =
        new(StringComparer.Ordinal);

    public static ECDsaSecurityKey Get(string keyId, string publicKeySpki) =>
        Parsed.GetOrAdd(
            $"{keyId}\n{publicKeySpki}",
            _ => new Lazy<ECDsaSecurityKey>(
                () => Create(keyId, publicKeySpki),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static ECDsaSecurityKey Create(string keyId, string publicKeySpki)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeySpki), out _);
        // Deliberately not disposed: this instance is the cached value and lives for the process. Every
        // other ECDsa.Create() in the codebase is scoped to one operation and disposed there.
        return new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
    }
}
