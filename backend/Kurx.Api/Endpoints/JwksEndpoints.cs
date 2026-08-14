using System.Security.Cryptography;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>Publishes the JSON Web Key Set (AM10, D-099).
///
/// <para>Anonymous and cacheable by design — this is <b>public</b> key material. Publishing it is
/// what lets any other service verify a Kurx access token without holding a secret that would also
/// let it mint one. That separation is the entire reason for moving off HS256.</para>
///
/// <para>Only Active and Retiring keys appear. A key pulled for compromise is dropped from this
/// document immediately, which is the mechanism by which its tokens stop being accepted.</para></summary>
public static class JwksEndpoints
{
    public static void MapJwksEndpoints(this WebApplication app)
    {
        app.MapGet("/.well-known/jwks.json", async (ISigningKeyService keys, HttpContext http, CancellationToken ct) =>
        {
            var published = await keys.GetValidationKeysAsync(ct);

            // Short cache: long enough to spare the database on a hot path, short enough that a
            // compromised key stops being honoured quickly. Consumers that see an unknown `kid`
            // are expected to re-fetch immediately rather than wait this out.
            http.Response.Headers.CacheControl = "public, max-age=300";

            return Results.Ok(new JwksDocument(published.Select(k =>
            {
                var (x, y) = ExtractP256Coordinates(k.PublicKeySpki);
                return new JsonWebKey("EC", "P-256", k.Algorithm, "sig", k.KeyId, x, y);
            }).ToArray()));
        }).WithTags("well-known").AllowAnonymous().Produces<JwksDocument>();
    }

    /// <summary>Turns a stored SPKI into the raw `x`/`y` coordinates JWKS requires.
    ///
    /// JWKS does not carry DER — an EC key is published as its two base64url coordinates. Emitting
    /// the SPKI blob instead is a silent interop failure: standard verifiers reject the key without
    /// ever explaining why.</summary>
    private static (string X, string Y) ExtractP256Coordinates(string publicKeySpkiBase64)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeySpkiBase64), out _);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        return (Base64Url(parameters.Q.X!), Base64Url(parameters.Q.Y!));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
