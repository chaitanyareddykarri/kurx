using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>ES256 signing-key lifecycle and JWKS (AM10, D-099).
///
/// The properties under test are the ones that make rotation safe: exactly one signer, rotation
/// never invalidating live tokens, a compromised key disappearing immediately, and retirement
/// destroying private material.</summary>
public class SigningKeyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private readonly HttpClient _client;

    public SigningKeyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
        _client = factory.CreateClient();
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private async Task<T> WithServiceAsync<T>(Func<ISigningKeyService, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<ISigningKeyService>());
    }

    private async Task<T> WithDbAsync<T>(Func<KurxDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<KurxDbContext>());
    }

    [Fact]
    public async Task A_key_is_created_on_first_use_so_a_fresh_deployment_needs_no_bootstrap()
    {
        var active = await WithServiceAsync(s => s.GetActiveAsync());

        Assert.Equal("ES256", active.Algorithm);
        Assert.False(string.IsNullOrWhiteSpace(active.KeyId));

        // The private half must be a usable P-256 key, not a placeholder.
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(active.PrivateKeyPkcs8), out _);
        Assert.Equal(256, ecdsa.KeySize);
    }

    [Fact]
    public async Task Rotation_keeps_the_previous_key_valid_so_live_tokens_survive()
    {
        var before = await WithServiceAsync(s => s.GetActiveAsync());
        var rotated = await WithServiceAsync(s => s.RotateAsync());

        Assert.NotEqual(before.KeyId, rotated.KeyId);

        var validation = await WithServiceAsync(s => s.GetValidationKeysAsync());
        var ids = validation.Select(k => k.KeyId).ToList();

        // The whole point of the grace period: a token minted a moment before the rotation still
        // names a key that validation accepts.
        Assert.Contains(before.KeyId, ids);
        Assert.Contains(rotated.KeyId, ids);

        // ...but only the new key signs from now on.
        var nowActive = await WithServiceAsync(s => s.GetActiveAsync());
        Assert.Equal(rotated.KeyId, nowActive.KeyId);
    }

    [Fact]
    public async Task Only_one_key_is_ever_active()
    {
        await WithServiceAsync(s => s.RotateAsync());
        await WithServiceAsync(s => s.RotateAsync());

        var activeCount = await WithDbAsync(db => db.SigningKeys
            .CountAsync(k => k.State == SigningKeyState.Active));

        // Enforced by a partial unique index, not just by service logic — two signers would mean
        // tokens some instances could not yet validate.
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task Retirement_destroys_the_private_key()
    {
        var retiring = await WithServiceAsync(s => s.GetActiveAsync());
        await WithServiceAsync(s => s.RotateAsync());

        // Age the retiring key past its grace period.
        await WithDbAsync(async db =>
        {
            var key = await db.SigningKeys.SingleAsync(k => k.KeyId == retiring.KeyId);
            key.RetiringAt = DateTime.UtcNow.AddDays(-30);
            return await db.SaveChangesAsync();
        });

        var retired = await WithServiceAsync(s => s.RetireExpiredAsync());
        Assert.True(retired >= 1);

        var row = await WithDbAsync(db => db.SigningKeys.AsNoTracking()
            .SingleAsync(k => k.KeyId == retiring.KeyId));
        Assert.Equal(SigningKeyState.Retired, row.State);
        // A key that can no longer sign should not remain a secret worth stealing.
        Assert.Null(row.PrivateKeyPkcs8);

        // And it must no longer validate anything.
        var validation = await WithServiceAsync(s => s.GetValidationKeysAsync());
        Assert.DoesNotContain(retiring.KeyId, validation.Select(k => k.KeyId));
    }

    [Fact]
    public async Task A_compromised_key_is_dropped_immediately_with_no_grace_period()
    {
        var doomed = await WithServiceAsync(s => s.GetActiveAsync());

        var replacement = await WithServiceAsync(s => s.MarkCompromisedAsync(doomed.KeyId, "test incident"));

        Assert.NotEqual(doomed.KeyId, replacement.KeyId);

        var validation = await WithServiceAsync(s => s.GetValidationKeysAsync());
        // Unlike rotation, there is deliberately NO grace: every token this key signed is now
        // invalid, because the alternative is honouring the attacker's forgeries.
        Assert.DoesNotContain(doomed.KeyId, validation.Select(k => k.KeyId));
        Assert.Contains(replacement.KeyId, validation.Select(k => k.KeyId));

        var row = await WithDbAsync(db => db.SigningKeys.AsNoTracking().SingleAsync(k => k.KeyId == doomed.KeyId));
        Assert.Equal(SigningKeyState.Compromised, row.State);
        Assert.Null(row.PrivateKeyPkcs8);
        Assert.Equal("test incident", row.CompromiseReason);

        // The platform keeps issuing through the incident.
        var active = await WithServiceAsync(s => s.GetActiveAsync());
        Assert.Equal(replacement.KeyId, active.KeyId);
    }

    [Fact]
    public async Task Jwks_publishes_usable_public_keys_and_never_private_material()
    {
        await WithServiceAsync(s => s.GetActiveAsync());

        var res = await _client.GetAsync("/.well-known/jwks.json");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var raw = await res.Content.ReadAsStringAsync();
        // Nothing private may ever appear in a public document.
        Assert.DoesNotContain("PrivateKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"d\"", raw);      // the EC private scalar

        var body = JsonDocument.Parse(raw).RootElement;
        var key = body.GetProperty("keys").EnumerateArray().First();

        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("P-256", key.GetProperty("crv").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("kid").GetString()));

        // x/y must be real 32-byte P-256 coordinates — publishing the raw SPKI blob instead is a
        // silent interop failure that standard verifiers reject without explanation.
        foreach (var component in new[] { "x", "y" })
        {
            var value = key.GetProperty(component).GetString()!;
            Assert.Equal(32, FromBase64Url(value).Length);
        }
    }

    [Fact]
    public async Task A_jwks_key_can_actually_verify_a_signature_from_its_private_half()
    {
        var active = await WithServiceAsync(s => s.GetActiveAsync());

        var body = JsonDocument.Parse(await (await _client.GetAsync("/.well-known/jwks.json"))
            .Content.ReadAsStringAsync()).RootElement;
        var published = body.GetProperty("keys").EnumerateArray()
            .Single(k => k.GetProperty("kid").GetString() == active.KeyId);

        // Rebuild the public key from the published coordinates, exactly as a third-party verifier
        // would, and check it verifies something only the private half could have produced.
        using var publicKey = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = FromBase64Url(published.GetProperty("x").GetString()!),
                Y = FromBase64Url(published.GetProperty("y").GetString()!),
            },
        });

        using var privateKey = ECDsa.Create();
        privateKey.ImportPkcs8PrivateKey(Convert.FromBase64String(active.PrivateKeyPkcs8), out _);

        var payload = "kurx-signing-key-roundtrip"u8.ToArray();
        var signature = privateKey.SignData(payload, HashAlgorithmName.SHA256);

        Assert.True(publicKey.VerifyData(payload, signature, HashAlgorithmName.SHA256),
            "the published JWKS key must verify a signature made by the active private key");
    }

    [Fact]
    public async Task Legacy_hs256_sessions_keep_working_during_the_cut_over()
    {
        // D-099 is a staged migration: existing users must not be signed out by the deploy that
        // introduces ES256. The OTP login path still mints HS256 today, and /v1/me must accept it.
        // Still literally true as of 2026-08-15 — all six login paths call the synchronous
        // CreateAccessToken (HS256); CreateAccessTokenAsync (ES256) has no callers. This is the
        // documented "built but OFF" state, not drift — see AUTHENTICATION_ARCHITECTURE.md §4.8.
        const string phone = "9200000001";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await (await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", tokens.GetProperty("access_token").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/v1/me")).StatusCode);
    }

    /// <summary>D-344: the resolver caches the parsed <c>ECDsaSecurityKey</c> per key so it stops
    /// abandoning a native handle and re-importing the SPKI on every request that carries a <c>kid</c>.
    /// This test keeps that cache honest — it must never become the authority on which keys are valid.
    ///
    /// <para>The compromise test above proves it at the SERVICE layer (<c>GetValidationKeysAsync</c>
    /// stops listing the key). That would still pass if the HTTP resolver served a stale parsed key,
    /// because it never makes a request.</para>
    ///
    /// <para><b>The token is minted directly rather than by logging in</b>, because all six login paths
    /// call the synchronous HS256 <c>CreateAccessToken</c> — <c>CreateAccessTokenAsync</c> has no
    /// callers, so D-099's ES256 cut-over is built but not switched on. An OTP login therefore
    /// produces a token with no <c>kid</c> and never reaches the branch under test. When the cut-over
    /// lands this test keeps working unchanged; until then it is the only coverage the branch has.</para></summary>
    [Fact]
    public async Task An_es256_token_stops_being_accepted_once_its_signing_key_is_compromised()
    {
        const string phone = "9200000002";
        await _client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var login = await (await _client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var hs256 = _factory.CreateClient();
        hs256.DefaultRequestHeaders.Authorization =
            new("Bearer", login.GetProperty("access_token").GetString());
        var userId = (await (await hs256.GetAsync("/v1/me")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<KurxDbContext>()
            .Users.SingleAsync(u => u.Id == userId);
        var (token, _) = await scope.ServiceProvider
            .GetRequiredService<Kurx.Infrastructure.Auth.TokenService>().CreateAccessTokenAsync(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Accepted while the key is published — and this is what populates the parse cache for its kid.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/v1/me")).StatusCode);

        var signer = await WithServiceAsync(s => s.GetActiveAsync());
        await WithServiceAsync(s => s.MarkCompromisedAsync(signer.KeyId, "resolver cache test"));

        // Same token, same kid, now unpublished. The cache still holds parsed material for it; the
        // resolver must never reach that entry, because the published-key lookup runs first.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Jwks_is_anonymous_and_cacheable()
    {
        var res = await _client.GetAsync("/.well-known/jwks.json");

        // Public key material: any verifier must be able to fetch it without credentials.
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(res.Headers.CacheControl);
        Assert.True(res.Headers.CacheControl!.Public);
    }

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
