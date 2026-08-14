using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Kurx.Infrastructure.Auth;

public class JwtOptions
{
    public string Secret { get; init; } = null!;
    /// <summary>Separate key for ticket QR HMAC signatures (M10, D-049) — decoupled from the JWT signing
    /// key so rotating one doesn't invalidate the other. Falls back to the JWT secret in development only;
    /// Production requires its own strong value (see <see cref="FromSecret"/>).</summary>
    public string TicketHmacSecret { get; init; } = null!;
    public string Issuer { get; init; } = "kurx";
    public string Audience { get; init; } = "kurx-app";
    public TimeSpan AccessTtl { get; init; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTtl { get; init; } = TimeSpan.FromDays(30);

    /// <param name="isProduction">When true, rejects a missing/weak/placeholder JWT_SECRET at startup
    /// instead of letting a dev-only value reach a real deployment.</param>
    public static JwtOptions FromConfiguration(IConfiguration config, bool isProduction = false)
    {
        var secret = config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is not set");
        return FromSecret(secret, config, isProduction);
    }

    /// <summary>Builds options from an already-resolved secret, so the caller can source it through
    /// <c>ISecretProvider</c> (AM10, D-101a) rather than reading configuration directly — which is
    /// what makes AWS Secrets Manager usable for the signing key.</summary>
    /// <param name="ticketHmacSecret">Resolved through <c>ISecretProvider</c> by the caller, for the same
    /// reason as <paramref name="secret"/> — reading it from configuration here would bypass AWS Secrets
    /// Manager. Falls back to configuration when the caller passes none (tests, dev hosts).</param>
    public static JwtOptions FromSecret(string secret, IConfiguration config, bool isProduction = false,
        string? ticketHmacSecret = null)
    {
        if (isProduction)
            SecretValidation.RequireStrongProductionSecret("JWT_SECRET", secret);

        // Ticket HMAC authenticates gate tickets — a DIFFERENT security domain from session signing.
        // This previously fell back to the JWT secret with no validation at all, so one key silently
        // served both domains and the committed dev placeholder could reach production even though
        // SecretValidation already lists it. Production must now set its own strong value.
        var ticketSecret = ticketHmacSecret ?? config["TICKET_HMAC_SECRET"];
        if (isProduction)
        {
            if (string.IsNullOrWhiteSpace(ticketSecret))
                throw new InvalidOperationException(
                    "TICKET_HMAC_SECRET is not set. It signs gate tickets and must be distinct from " +
                    "JWT_SECRET — reusing the session-signing key means a leak of either compromises both.");
            SecretValidation.RequireStrongProductionSecret("TICKET_HMAC_SECRET", ticketSecret);
        }

        return new JwtOptions
        {
            Secret = secret,
            TicketHmacSecret = ticketSecret ?? secret,   // dev-only fallback; Production validated above
            Issuer = config["JWT_ISSUER"] ?? "kurx",
            Audience = config["JWT_AUDIENCE"] ?? "kurx-app",
        };
    }

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Secret));
}

public class TokenService(JwtOptions options, IServiceScopeFactory? scopeFactory = null)
{
    /// <summary>Signs with ES256 when a signing-key service is available, else the legacy HS256
    /// secret (D-099). Both are accepted by validation during the cut-over, so tokens minted before
    /// a deploy keep working — the same staged-migration shape as D-089.</summary>
    public async Task<(string Token, DateTime ExpiresAt)> CreateAccessTokenAsync(User user, CancellationToken ct = default)
    {
        if (scopeFactory is null) return CreateAccessToken(user);

        // TokenService is a singleton; ISigningKeyService is scoped (it holds a DbContext), so it
        // must be resolved per call through a scope rather than captured — a captive dependency
        // here would hand every request the first request's DbContext.
        using var scope = scopeFactory.CreateScope();
        var signingKeys = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
        var key = await signingKeys.GetActiveAsync(ct);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(key.PrivateKeyPkcs8), out _);

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = key.KeyId };
        var expires = DateTime.UtcNow.Add(options.AccessTtl);
        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: StandardClaims(user),
            notBefore: DateTime.UtcNow,
            expires: expires,
            // The `kid` header travels with the token, so validation selects the exact key that
            // signed it instead of trying every published key in turn.
            signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    private static List<Claim> StandardClaims(User user) =>
    [
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    ];

    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var expires = DateTime.UtcNow.Add(options.AccessTtl);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        // Platform authority (admin/reviewer/finance) is deliberately NOT encoded in the token (M2,
        // D-040). It is read live from platform_roles per request by PlatformRoleClaimsTransformation,
        // so a revoked role is effective on the next request instead of surviving until token expiry.

        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(options.SigningKey, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    /// <returns>Opaque refresh token (raw, for the client) and its SHA-256 hash (for the DB).</returns>
    public (string Raw, string Hash, DateTime ExpiresAt) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return (raw, Sha256(raw), DateTime.UtcNow.Add(options.RefreshTtl));
    }

    public static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public string SignTicketCode(Guid code)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(options.TicketHmacSecret));
        return Convert.ToHexString(hmac.ComputeHash(code.ToByteArray())).ToLowerInvariant();
    }
}
