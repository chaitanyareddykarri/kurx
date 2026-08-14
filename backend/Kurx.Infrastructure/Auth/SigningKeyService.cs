using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>ES256 signing-key lifecycle (AM10, D-099).
///
/// ## Why asymmetric
/// HS256 means one shared secret that every verifier must hold — and anything able to verify is
/// equally able to <b>forge</b>. ES256 splits that: only this service holds the private half, so a
/// public JWKS is safe to publish and other services can verify without being trusted to mint.
///
/// ## Zero-downtime rotation
/// Signing and validation are separate sets. One key signs (Active); several may validate (Active +
/// Retiring). Rotation therefore never invalidates a live token: a token minted a second before the
/// switch still names a key that JWKS publishes, until it expires on its own.
///
/// ## Multi-instance coherence
/// Keys live in Postgres, so every instance sees the same set. A partial unique index makes
/// "at most one Active key" a database invariant — two instances rotating concurrently cannot both
/// promote, because the second insert violates the index rather than silently winning.
///
/// The in-memory cache is deliberately short-lived: an instance may lag a rotation by at most
/// <see cref="CacheSeconds"/>. That is safe in one direction only, and the direction matters —
/// a lagging instance still holds the *previous* key, which is Retiring and therefore still valid,
/// so it accepts tokens from the new signer as soon as it refreshes and never rejects a legitimate
/// one it has already seen. A compromise bypasses the cache entirely (see below).
/// </summary>
public class SigningKeyService(KurxDbContext db, IMemoryCache cache, ISigningKeyProtector protector,
    IConfiguration config, ILogger<SigningKeyService> log) : ISigningKeyService
{
    /// <summary>How long a rotated key keeps validating. Must exceed the access-token lifetime, or a
    /// rotation would invalidate tokens that have not expired yet — the exact thing this design
    /// exists to prevent. Guarded at runtime below.</summary>
    private TimeSpan GracePeriod => TimeSpan.FromHours(
        int.TryParse(config["JWT_KEY_GRACE_HOURS"], out var h) ? h : 24);

    private TimeSpan AccessTokenLifetime => TimeSpan.FromHours(1);   // mirrors JwtOptions.AccessTtl

    internal const int CacheSeconds = 30;
    private const string ValidationCacheKey = "signing_keys:validation";
    private const string ActiveCacheKey = "signing_keys:active";

    public async Task<ActiveSigningKey> GetActiveAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue<ActiveSigningKey>(ActiveCacheKey, out var cached) && cached is not null)
            return cached;

        var active = await db.SigningKeys
            .FirstOrDefaultAsync(k => k.State == SigningKeyState.Active, ct);

        // First boot (or immediately after an emergency retirement): create one rather than failing.
        // A deployment that cannot mint tokens because nobody ran a bootstrap command is an outage
        // with no upside.
        active ??= await CreateAndActivateAsync(ct);

        // Unwrap only at the point of use (D-102a). The cached copy is the unwrapped key, which is
        // what keeps a KMS call off the token-issuance hot path; the TTL bounds how long it lives.
        var privateKey = await protector.UnprotectAsync(active.PrivateKeyPkcs8!, ct);
        var result = new ActiveSigningKey(active.KeyId, active.Algorithm, privateKey);
        cache.Set(ActiveCacheKey, result, TimeSpan.FromSeconds(CacheSeconds));
        return result;
    }

    public async Task<IReadOnlyList<SigningKeyPublic>> GetValidationKeysAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue<IReadOnlyList<SigningKeyPublic>>(ValidationCacheKey, out var cached) && cached is not null)
            return cached;

        // Active + Retiring only. Retired keys are gone; Compromised keys are excluded on purpose —
        // publishing one would keep honouring tokens an attacker can mint.
        var keys = await db.SigningKeys.AsNoTracking()
            .Where(k => k.State == SigningKeyState.Active || k.State == SigningKeyState.Retiring)
            .OrderByDescending(k => k.ActivatedAt)
            .Select(k => new SigningKeyPublic(k.KeyId, k.Algorithm, k.PublicKeySpki))
            .ToListAsync(ct);

        if (keys.Count == 0)
        {
            var created = await CreateAndActivateAsync(ct);
            keys = [new SigningKeyPublic(created.KeyId, created.Algorithm, created.PublicKeySpki)];
        }

        cache.Set(ValidationCacheKey, keys, TimeSpan.FromSeconds(CacheSeconds));
        return keys;
    }

    public async Task<SigningKeyPublic> RotateAsync(CancellationToken ct = default)
    {
        if (GracePeriod <= AccessTokenLifetime)
        {
            // Refuse rather than quietly invalidating live tokens.
            throw new InvalidOperationException(
                $"JWT_KEY_GRACE_HOURS ({GracePeriod.TotalHours}h) must exceed the access-token " +
                $"lifetime ({AccessTokenLifetime.TotalHours}h), or rotation would invalidate live tokens.");
        }

        var now = DateTime.UtcNow;
        var previous = await db.SigningKeys.FirstOrDefaultAsync(k => k.State == SigningKeyState.Active, ct);
        if (previous is not null)
        {
            // Demote first: the unique index permits only one Active row, so the new key cannot be
            // inserted as Active until this one has moved on.
            previous.State = SigningKeyState.Retiring;
            previous.RetiringAt = now;
            await db.SaveChangesAsync(ct);
        }

        var created = await CreateAndActivateAsync(ct);
        InvalidateCache();

        log.LogInformation("Signing key rotated: {Previous} -> {Current}", previous?.KeyId ?? "(none)", created.KeyId);
        return new SigningKeyPublic(created.KeyId, created.Algorithm, created.PublicKeySpki);
    }

    public async Task<int> RetireExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - GracePeriod;
        var expired = await db.SigningKeys
            .Where(k => k.State == SigningKeyState.Retiring && k.RetiringAt != null && k.RetiringAt <= cutoff)
            .ToListAsync(ct);
        if (expired.Count == 0) return 0;

        foreach (var key in expired)
        {
            key.State = SigningKeyState.Retired;
            key.RetiredAt = DateTime.UtcNow;
            // Destroy the private half. A key that can no longer be used to sign anything should not
            // remain a secret worth stealing.
            key.PrivateKeyPkcs8 = null;
        }

        db.SecurityEvents.Add(new SecurityEvent
        {
            Type = "signing_key.retired", Severity = "info",
            ContextJson = $"{{\"count\":{expired.Count}}}",
        });
        await db.SaveChangesAsync(ct);
        InvalidateCache();

        log.LogInformation("Retired {Count} expired signing key(s)", expired.Count);
        return expired.Count;
    }

    public async Task<SigningKeyPublic> MarkCompromisedAsync(string keyId, string reason, CancellationToken ct = default)
    {
        var key = await db.SigningKeys.FirstOrDefaultAsync(k => k.KeyId == keyId, ct)
            ?? throw new InvalidOperationException($"Unknown signing key '{keyId}'");

        var now = DateTime.UtcNow;
        key.State = SigningKeyState.Compromised;
        key.CompromisedAt = now;
        key.CompromiseReason = reason;
        // No grace period, and the private half goes immediately: every token this key signed becomes
        // invalid the moment caches refresh. That is the intended, disruptive behaviour — the
        // alternative is continuing to accept whatever the attacker mints.
        key.PrivateKeyPkcs8 = null;

        db.SecurityEvents.Add(new SecurityEvent
        {
            Type = "signing_key.compromised", Severity = "critical",
            ContextJson = $"{{\"keyId\":\"{keyId}\"}}",
        });
        await db.SaveChangesAsync(ct);

        // Replace immediately so the platform keeps issuing tokens through the incident.
        var replacement = await CreateAndActivateAsync(ct);
        InvalidateCache();

        log.LogCritical("Signing key {KeyId} marked COMPROMISED ({Reason}); replaced by {Replacement}",
            keyId, reason, replacement.KeyId);
        return new SigningKeyPublic(replacement.KeyId, replacement.Algorithm, replacement.PublicKeySpki);
    }

    private async Task<SigningKey> CreateAndActivateAsync(CancellationToken ct)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);   // P-256 => ES256

        var key = new SigningKey
        {
            KeyId = Base64Url(RandomNumberGenerator.GetBytes(16)),
            Algorithm = "ES256",
            PublicKeySpki = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
            // Wrapped before it is ever written. A database dump therefore contains ciphertext,
            // not a key that can mint tokens for any user (D-102a).
            PrivateKeyPkcs8 = await protector.ProtectAsync(
                Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey()), ct),
            State = SigningKeyState.Active,
            ActivatedAt = DateTime.UtcNow,
        };
        db.SigningKeys.Add(key);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the race to another instance's bootstrap/rotation — the partial unique index did
            // exactly its job. Adopt the winner rather than retrying and creating a third key.
            db.Entry(key).State = EntityState.Detached;
            var winner = await AnotherInstanceWonAsync(ct);
            if (winner is not null) return winner;
            throw;
        }

        InvalidateCache();
        return key;
    }

    private async Task<SigningKey?> AnotherInstanceWonAsync(CancellationToken ct)
        => await db.SigningKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.State == SigningKeyState.Active, ct);

    private void InvalidateCache()
    {
        cache.Remove(ActiveCacheKey);
        cache.Remove(ValidationCacheKey);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
