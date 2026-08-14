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
