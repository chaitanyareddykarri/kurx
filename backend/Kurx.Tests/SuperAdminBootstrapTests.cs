using System.Net;
using System.Net.Http.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kurx.Tests;

/// <summary>The two halves of D-274: the Developer Workspace is really gone from the running app, and the
/// first-SuperAdmin bootstrap that replaced its super-admin preset behaves. Asserted against real Postgres,
/// because the whole point of the bootstrap is that it grants real authority. The bootstrap case is one
/// test walking four states in order — the class shares a database, and each state is defined by what the
/// previous one left behind; the route tests touch no state, so they are order-independent.</summary>
public class SuperAdminBootstrapTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public SuperAdminBootstrapTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    /// <summary>The Developer Workspace is deleted, not disabled (D-274). Asserted against a booted host
    /// rather than by grepping, because "the routes are gone" is a claim about the running app: the module
    /// used to be present-but-guarded, and this is the test that tells those two states apart.</summary>
    [Theory]
    [InlineData("/v1/dev/ping")]
    [InlineData("/v1/dev/auth/options")]
    [InlineData("/v1/dev/auth/presets")]
    [InlineData("/v1/dev/auth/scenarios")]
    [InlineData("/v1/dev/auth/users")]
    public async Task DevEndpointsDoNotExist(string path)
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(path, new { preset = "super-admin" })).StatusCode);
    }

    [Fact]
    public async Task DevLoginAndImpersonateDoNotExist()
    {
        using var client = _factory.CreateClient();

        foreach (var path in new[] { "/v1/dev/auth/login", "/v1/dev/auth/impersonate", "/v1/dev/auth/preview" })
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsJsonAsync(path, new { preset = "super-admin", userId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task BootstrapsExactlyOnce_AndOnlyOntoARegisteredAccount()
    {
        // 1. Unset configuration is a no-op — the steady state of every deployment.
        await RunAsync(null);
        Assert.Equal(0, await SuperAdminCountAsync());

        // 2. A phone nobody has registered grants nothing AND creates nobody: the account must come
        //    from the real OTP flow, never from this seeder.
        await RunAsync("919111000001");
        Assert.Equal(0, await SuperAdminCountAsync());
        Assert.False(await QueryAsync(db => db.Users.AnyAsync(u => u.Phone == "919111000001")));

        // 3. Once that person has registered, the role is granted and audited.
        var first = SeedUser("919111000001");
        await RunAsync("+91 91110 00001");   // normalised exactly as the login path normalises it
        Assert.True(await QueryAsync(db =>
            db.PlatformRoles.AnyAsync(r => r.UserId == first && r.Role == PlatformRole.SuperAdmin)));
        var audit = await QueryAsync(db =>
            db.AuditLogs.SingleAsync(a => a.Action == "platform_role.bootstrap_granted"));
        Assert.Equal("system", audit.ActorType);
        Assert.Equal(first, audit.EntityId);

        // 4. With a SuperAdmin in place the door is shut: further grants go through the audited
        //    /v1/admin/staff/grant console path, not configuration.
        var second = SeedUser("919111000002");
        await RunAsync("919111000002");
        Assert.False(await QueryAsync(db =>
            db.PlatformRoles.AnyAsync(r => r.UserId == second && r.Role == PlatformRole.SuperAdmin)));
        Assert.Equal(1, await SuperAdminCountAsync());
    }

    /// <summary>Runs the bootstrap in its own scope with the given configured phone (null = unset).</summary>
    private async Task RunAsync(string? configuredPhone)
    {
        using var scope = _factory.Services.CreateScope();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [SuperAdminBootstrap.PhoneKey] = configuredPhone }).Build();

        await SuperAdminBootstrap.RunAsync(
            scope.ServiceProvider.GetRequiredService<KurxDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPlatformRoleService>(),
            config, NullLogger.Instance);
    }

    private Guid SeedUser(string phone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var user = new User { Phone = phone, Name = "Bootstrap Target" };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    private Task<int> SuperAdminCountAsync() =>
        QueryAsync(db => db.PlatformRoles.CountAsync(r => r.Role == PlatformRole.SuperAdmin));

    private async Task<T> QueryAsync<T>(Func<KurxDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<KurxDbContext>());
    }
}
