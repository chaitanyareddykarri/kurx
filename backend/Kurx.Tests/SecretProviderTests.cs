using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kurx.Tests;

/// <summary>Secrets abstraction (AM10, D-101a).
///
/// <para>The AWS provider is exercised against a fake <see cref="IAmazonSecretsManager"/>, which
/// verifies <b>our</b> logic — caching, rotation, error handling — but deliberately proves nothing
/// about the real API. That interaction is PENDING DEPLOYMENT CONFIGURATION.</para></summary>
public class SecretProviderTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    /// <summary>Implements only the narrow <see cref="ISecretsManagerClient"/> the provider needs.</summary>
    private sealed class FakeSecretsManager(string? value) : ISecretsManagerClient
    {
        public string? Value = value;
        public int Calls;
        public Exception? ThrowOnGet;
        public string? LastRequestedId;

        public Task<string?> GetSecretStringAsync(string secretId, CancellationToken ct)
        {
            Calls++;
            LastRequestedId = secretId;
            if (ThrowOnGet is not null) throw ThrowOnGet;
            if (Value is null) throw new ResourceNotFoundException("no such secret");
            return Task.FromResult<string?>(Value);
        }
    }

    // ── Configuration provider (local dev + env vars) ────────────────────────

    [Fact]
    public async Task The_configuration_provider_reads_from_configuration_and_environment()
    {
        // IConfiguration already layers environment variables, so this one provider covers local
        // dev AND deployments that inject secrets as env vars (ECS/Kubernetes).
        var provider = new ConfigurationSecretProvider(Config(new() { ["JWT_SECRET"] = "value-from-config" }));

        Assert.Equal("value-from-config", await provider.GetAsync("JWT_SECRET"));
        Assert.Equal("value-from-config", await provider.GetRequiredAsync("JWT_SECRET"));
    }

    [Fact]
    public async Task A_missing_optional_secret_is_null_but_a_required_one_throws()
    {
        var provider = new ConfigurationSecretProvider(Config([]));

        Assert.Null(await provider.GetAsync("NOT_SET"));

        // Fail closed: anything the platform needs must fail loudly, naming the secret.
        var ex = await Assert.ThrowsAsync<MissingSecretException>(() => provider.GetRequiredAsync("NOT_SET"));
        Assert.Equal("NOT_SET", ex.SecretName);
        Assert.Contains("NOT_SET", ex.Message);
    }

    [Fact]
    public async Task A_whitespace_only_secret_counts_as_missing()
    {
        // An empty env var is a misconfiguration, not a value — treating "" as present is how a
        // service ends up signing tokens with an empty key.
        var provider = new ConfigurationSecretProvider(Config(new() { ["JWT_SECRET"] = "   " }));

        Assert.Null(await provider.GetAsync("JWT_SECRET"));
        await Assert.ThrowsAsync<MissingSecretException>(() => provider.GetRequiredAsync("JWT_SECRET"));
    }

    // ── AWS provider: our logic, against a fake client ───────────────────────

    [Fact]
    public async Task Aws_secrets_are_cached_so_every_request_is_not_a_billed_api_call()
    {
        var aws = new FakeSecretsManager("s3cret");
        var provider = new AwsSecretsManagerProvider(aws, NewCache(), Config([]), NullLogger<AwsSecretsManagerProvider>.Instance);

        for (var i = 0; i < 5; i++) Assert.Equal("s3cret", await provider.GetAsync("JWT_SECRET"));

        // Secrets Manager is billed per call and rate-limited; fetching per request would be both
        // expensive and a throttling outage waiting to happen.
        Assert.Equal(1, aws.Calls);
    }

    [Fact]
    public async Task Invalidating_forces_a_refetch_so_a_rotated_secret_is_picked_up()
    {
        var aws = new FakeSecretsManager("old-password");
        var provider = new AwsSecretsManagerProvider(aws, NewCache(), Config([]), NullLogger<AwsSecretsManagerProvider>.Instance);

        Assert.Equal("old-password", await provider.GetAsync("DB_PASSWORD"));

        // AWS rotates the secret out from under the running process. The cached copy is now wrong,
        // and the resulting failure looks exactly like a bad credential.
        aws.Value = "new-password";
        Assert.Equal("old-password", await provider.GetAsync("DB_PASSWORD"));   // still cached

        await provider.InvalidateAsync("DB_PASSWORD");
        Assert.Equal("new-password", await provider.GetAsync("DB_PASSWORD"));
        Assert.Equal(2, aws.Calls);
    }

    [Fact]
    public async Task A_secret_that_does_not_exist_returns_null_rather_than_throwing()
    {
        var aws = new FakeSecretsManager(null);   // ResourceNotFound
        var provider = new AwsSecretsManagerProvider(aws, NewCache(), Config([]), NullLogger<AwsSecretsManagerProvider>.Instance);

        Assert.Null(await provider.GetAsync("ABSENT"));
        await Assert.ThrowsAsync<MissingSecretException>(() => provider.GetRequiredAsync("ABSENT"));
    }

    [Fact]
    public async Task A_transient_aws_failure_propagates_instead_of_masquerading_as_absent()
    {
        var aws = new FakeSecretsManager("value") { ThrowOnGet = new AmazonSecretsManagerException("throttled") };
        var provider = new AwsSecretsManagerProvider(aws, NewCache(), Config([]), NullLogger<AwsSecretsManagerProvider>.Instance);

        // "Secrets Manager is unreachable" is NOT "this secret does not exist". Collapsing the two
        // would let a transient AWS problem silently disable a security feature.
        await Assert.ThrowsAsync<AmazonSecretsManagerException>(() => provider.GetAsync("JWT_SECRET"));
    }

    [Fact]
    public async Task A_failed_read_is_not_cached()
    {
        var aws = new FakeSecretsManager("recovered-value") { ThrowOnGet = new AmazonSecretsManagerException("throttled") };
        var provider = new AwsSecretsManagerProvider(aws, NewCache(), Config([]), NullLogger<AwsSecretsManagerProvider>.Instance);

        await Assert.ThrowsAsync<AmazonSecretsManagerException>(() => provider.GetAsync("JWT_SECRET"));

        // A transient error must not pin the application into a broken state until the TTL expires.
        aws.ThrowOnGet = null;
        Assert.Equal("recovered-value", await provider.GetAsync("JWT_SECRET"));
    }

    [Fact]
    public async Task The_secret_name_prefix_scopes_one_aws_account_across_environments()
    {
        var aws = new FakeSecretsManager("v");
        var provider = new AwsSecretsManagerProvider(aws, NewCache(),
            Config(new() { ["AWS_SECRETS_PREFIX"] = "kurx/production/" }),
            NullLogger<AwsSecretsManagerProvider>.Instance);

        await provider.GetAsync("JWT_SECRET");

        Assert.Equal("kurx/production/JWT_SECRET", aws.LastRequestedId);
    }

    // ── Startup validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Production_refuses_to_start_when_a_required_secret_is_missing()
    {
        var provider = new ConfigurationSecretProvider(Config([]));

        // A missing secret found at startup is a boot failure naming the secret, which a pipeline
        // catches before traffic is routed. Found on first use, it is an intermittent login failure.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RequiredSecrets.ValidateAsync(provider, NullLogger.Instance, isProduction: true));

        Assert.Contains("JWT_SECRET", ex.Message);
    }

    [Fact]
    public async Task Development_warns_but_still_starts()
    {
        // A contributor without production secrets must still be able to run the app.
        var provider = new ConfigurationSecretProvider(Config([]));

        var exception = await Record.ExceptionAsync(() =>
            RequiredSecrets.ValidateAsync(provider, NullLogger.Instance, isProduction: false));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Validation_passes_when_every_required_secret_is_present()
    {
        // Production validation is presence AND strength (D-244), so "present" no longer suffices —
        // a short or placeholder value is now a boot failure in its own right. The value here is what a
        // real deployment would supply.
        var values = RequiredSecrets.Names.ToDictionary(
            n => n, n => (string?)$"a-unique-production-value-for-{n}-0123456789");
        var provider = new ConfigurationSecretProvider(Config(values));

        var exception = await Record.ExceptionAsync(() =>
            RequiredSecrets.ValidateAsync(provider, NullLogger.Instance, isProduction: true));

        Assert.Null(exception);
    }

    [Fact]
    public async Task A_provider_that_throws_is_treated_as_missing_not_as_healthy()
    {
        // In production, "the secret exists but we cannot reach it" is indistinguishable from
        // "absent" — both must stop the deploy.
        var aws = new FakeSecretsManager("v") { ThrowOnGet = new AmazonSecretsManagerException("no IAM permission") };
        var provider = new AwsSecretsManagerProvider(aws, NewCache(), Config([]), NullLogger<AwsSecretsManagerProvider>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RequiredSecrets.ValidateAsync(provider, NullLogger.Instance, isProduction: true));
    }
}
