using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure;
using Kurx.Infrastructure.Configuration;
using Kurx.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>D-323 — the dev/test bypass for the identity proofs in the trust matrix.
///
/// <para>The flag defaults OFF, so every existing trust/verification test keeps proving the real rules and
/// nothing here weakens them. What needs its own cover is the two properties that make the bypass safe to
/// have at all: that it relaxes the PROOFS and nothing else, and that Production refuses it outright.</para>
///
/// <para><c>TrustService</c> is constructed directly rather than driven through <c>/v1/me</c> because the
/// flag is bound once at DI time — exercising both values over HTTP would mean booting a second host per
/// case. Persistence and the fraud service are still the real ones from the running app's scope against
/// real Postgres; only the options record, which is configuration, is supplied by hand.</para></summary>
public class IdentityVerificationBypassTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public IdentityVerificationBypassTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
        => await res.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<TrustCapabilities> CapabilitiesAsync(Guid userId, bool bypass)
    {
        using var scope = _factory.Services.CreateScope();
        var trust = new Kurx.Infrastructure.Trust.TrustService(
            scope.ServiceProvider.GetRequiredService<KurxDbContext>(),
            scope.ServiceProvider.GetRequiredService<IFraudService>(),
            new IdentityVerificationOptions(bypass));
        return await trust.GetUserCapabilitiesAsync(userId);
    }

    /// <summary>The bypass opens the three capability gates for a user who has proved nothing — and must
    /// leave the reported FACTS alone. <c>IdentityVerified</c>/<c>BankVerified</c> answer "what is on file";
    /// forging them would put a verified badge on a profile nobody checked, which is a different and much
    /// worse thing than letting a dev account publish a test event.</summary>
    [Fact]
    public async Task Bypass_opens_the_capability_gates_without_forging_the_facts()
    {
        var (_, userId) = await LoginAsync("9970000001");

        var enforced = await CapabilitiesAsync(userId, bypass: false);
        Assert.False(enforced.CanCreatePublicEvent);
        Assert.False(enforced.CanOrganizePaid);
        Assert.False(enforced.CanReceivePayout);

        var bypassed = await CapabilitiesAsync(userId, bypass: true);
        Assert.True(bypassed.CanCreatePublicEvent);
        Assert.True(bypassed.CanOrganizePaid);
        Assert.True(bypassed.CanReceivePayout);

        // The facts are unchanged in BOTH directions — the user proved nothing either way.
        Assert.False(enforced.IdentityVerified);
        Assert.False(bypassed.IdentityVerified);
        Assert.False(enforced.BankVerified);
        Assert.False(bypassed.BankVerified);

        // Free and private organizing never depended on the proofs, so the flag must not be what makes
        // them true — otherwise a later change could quietly move them under it.
        Assert.True(enforced.CanOrganizeFree);
        Assert.True(enforced.CanCreatePrivateEvent);
    }

    /// <summary>Fraud is deliberately outside the bypass. The blacklist and risk engine are real
    /// implementations with no mock behind them, so a bypass that swallowed them would hide the one part of
    /// the trust matrix that is finished. This is the assertion that pins `fraudClear` outside the flag.</summary>
    [Fact]
    public async Task Bypass_does_not_override_the_blacklist()
    {
        var (reviewerClient, reviewerId) = await LoginAsync("9970000002");
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPlatformRoleService>()
                .GrantAsync(reviewerId, Domain.Enums.PlatformRole.VerificationReviewer, grantedBy: null);

        var (_, userId) = await LoginAsync("9970000003");
        Assert.True((await CapabilitiesAsync(userId, bypass: true)).CanCreatePublicEvent);   // baseline

        await reviewerClient.PostAsJsonAsync("/v1/admin/blacklist",
            new { kind = "phone", value = "9970000003" });

        var bypassed = await CapabilitiesAsync(userId, bypass: true);
        Assert.False(bypassed.CanCreatePublicEvent);
        Assert.False(bypassed.CanOrganizePaid);
        Assert.False(bypassed.CanReceivePayout);
    }

    /// <summary>Fails closed. Same shape as <c>SIGNING_KEY_PROTECTION=none</c> and a missing
    /// <c>REDIS_CONNECTION</c>: the misconfiguration stops the process at startup rather than serving
    /// traffic with an open gate, because an open gate here looks completely healthy.</summary>
    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("TRUE")]
    public void Enabling_the_bypass_in_production_throws_at_startup(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddKurxInfrastructure(Config(value), isProduction: true));
        Assert.Contains(IdentityVerificationOptions.EnvKey, ex.Message);
    }

    /// <summary>The default and every unrecognised value must land on ENFORCED, never on open. A flag whose
    /// failure mode is "gate open" is the wrong way round.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("enabled")]
    public void Unset_or_unrecognised_values_leave_the_gate_enforced(string? value)
    {
        var services = new ServiceCollection().AddKurxInfrastructure(Config(value), isProduction: false);
        var options = services.BuildServiceProvider().GetRequiredService<IdentityVerificationOptions>();
        Assert.False(options.Bypass);
    }

    /// <summary>Production is the only environment that refuses it; dev and test must still be able to set
    /// it, which is the entire point.</summary>
    [Fact]
    public void Enabling_the_bypass_outside_production_is_permitted()
    {
        var services = new ServiceCollection().AddKurxInfrastructure(Config("true"), isProduction: false);
        var options = services.BuildServiceProvider().GetRequiredService<IdentityVerificationOptions>();
        Assert.True(options.Bypass);
    }

    /// <summary>Minimum configuration <c>AddKurxInfrastructure</c> needs to reach the bypass check. Nothing
    /// here connects to anything — registration is lazy and only the options singleton is ever resolved.
    ///
    /// <para>The password is deliberately NOT the committed dev one: <c>RequireProductionConnectionString</c>
    /// rejects <c>Password=kurx</c> and would throw before the bypass check is reached, which would make the
    /// Production case pass for the wrong reason. The message assertion below would catch that, but only
    /// after wasting a run.</para></summary>
    private static IConfiguration Config(string? bypass)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=kurx_bypass_cfg;Username=kurx;Password=not-the-committed-dev-password",
            ["JWT_SECRET"] = "a-unique-test-secret-that-is-definitely-long-enough-0123456789",
            ["TICKET_HMAC_SECRET"] = "a-unique-test-ticket-hmac-secret-0123456789-abcdef",
            ["OTP_PEPPER"] = "a-unique-test-otp-pepper-0123456789-abcdefghijklmnop",
        };
        if (bypass is not null) values[IdentityVerificationOptions.EnvKey] = bypass;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
