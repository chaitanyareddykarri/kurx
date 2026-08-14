using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Kurx.Tests;

/// <summary>Covers the "safe Guid parsing" fix: a syntactically valid JWT whose sub claim is not a Guid
/// (e.g. forged or from a future token format) must 401, not crash into a 500.</summary>
public class ClaimSafetyTests : IClassFixture<KurxApiFactory>
{
    private readonly HttpClient _client;

    public ClaimSafetyTests(KurxApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Must match appsettings.Development.json / KurxApiFactory's Development environment.
    private const string JwtSecret = "dev-only-secret-change-me-0123456789abcdef";
    private const string Issuer = "kurx";
    private const string Audience = "kurx-app";

    private static string TokenWithSub(string sub)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, sub),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var jwt = new JwtSecurityToken(Issuer, Audience, claims,
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    [Fact]
    public async Task Non_guid_sub_claim_on_me_returns_401_not_500()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenWithSub("not-a-guid"));
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Non_guid_sub_claim_on_orgs_returns_401_not_500()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/me/representations");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenWithSub("not-a-guid"));
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
