using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kurx.Tests;

/// <summary>Platform-role authorization (M2, D-040). Platform authority is read LIVE from
/// platform_roles per request, never trusted from the token, so: a forged claim is ignored, a real
/// grant works, a revocation is effective on the very next request (same token), and a non-SuperAdmin
/// role cannot reach a SuperAdmin-only endpoint. The SuperAdmin-only endpoint under test is category
/// admin CRUD (POST /v1/categories, "KurxAdmin" policy).</summary>
public class PlatformRoleTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public PlatformRoleTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private const string JwtSecret = "dev-only-secret-change-me-0123456789abcdef";
    private const string Issuer = "kurx";
    private const string Audience = "kurx-app";

    private static string TokenFor(Guid userId, params (string Type, string Value)[] extra)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(extra.Select(c => new Claim(c.Type, c.Value)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var jwt = new JwtSecurityToken(Issuer, Audience, claims,
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private async Task<Guid> CreateUserAsync(PlatformRole? grant = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User { Phone = $"9197{Random.Shared.Next(100000, 999999)}", Name = "PR User" };
        db.Users.Add(user);
        if (grant is not null)
            db.PlatformRoles.Add(new PlatformRoleAssignment { UserId = user.Id, Role = grant.Value });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private HttpClient ClientFor(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return c;
    }

    private static Task<HttpResponseMessage> CreateCategory(HttpClient c) => c.PostAsJsonAsync(
        "/v1/categories", new { level = "Category", name = "PR " + Guid.NewGuid().ToString("N")[..8] });

    [Fact]
    public async Task Forged_admin_claim_without_a_db_grant_is_rejected()
    {
        var userId = await CreateUserAsync(grant: null);
        // Token carries a forged kurx_admin claim — the transformation strips it and re-reads the DB.
        var res = await CreateCategory(ClientFor(TokenFor(userId, ("kurx_admin", "true"))));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Granted_superadmin_can_reach_admin_endpoint()
    {
        var userId = await CreateUserAsync(grant: PlatformRole.SuperAdmin);
        var res = await CreateCategory(ClientFor(TokenFor(userId)));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Revoking_superadmin_is_effective_on_the_next_request_with_the_same_token()
    {
        var userId = await CreateUserAsync(grant: PlatformRole.SuperAdmin);
        var client = ClientFor(TokenFor(userId));   // one token, reused across both requests

        Assert.Equal(HttpStatusCode.OK, (await CreateCategory(client)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
                .RevokeAsync(userId, PlatformRole.SuperAdmin);

        // Same still-valid JWT, but authority is re-read live — now denied.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateCategory(client)).StatusCode);
    }

    [Fact]
    public async Task Reviewer_role_cannot_reach_a_superadmin_only_endpoint()
    {
        var userId = await CreateUserAsync(grant: PlatformRole.VerificationReviewer);
        var res = await CreateCategory(ClientFor(TokenFor(userId)));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
