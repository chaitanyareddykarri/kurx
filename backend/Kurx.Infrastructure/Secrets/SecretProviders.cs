using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Secrets;

/// <summary>The one operation the secrets provider actually needs.
///
/// <para><see cref="IAmazonSecretsManager"/> has ~40 members covering secret creation, deletion,
/// replication and rotation policy — none of which this application performs. Depending on the whole
/// surface would mean a test double had to implement all of it, and would let a future change
/// quietly start creating or deleting secrets from application code. Interface segregation here is
/// a security boundary as much as a testing convenience: the app can <b>read</b> secrets and
/// nothing else.</para></summary>
public interface ISecretsManagerClient
{
    Task<string?> GetSecretStringAsync(string secretId, CancellationToken ct);
}

/// <summary>Adapts the AWS SDK client to the narrow interface above.</summary>
public class AwsSecretsManagerClientAdapter(IAmazonSecretsManager client) : ISecretsManagerClient
{
    public async Task<string?> GetSecretStringAsync(string secretId, CancellationToken ct)
    {
        var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId }, ct);
        return response.SecretString;
    }
}

/// <summary>Reads secrets from <see cref="IConfiguration"/> — which in ASP.NET Core already layers
/// appsettings, user-secrets and <b>environment variables</b> (AM10, D-101a).
///
/// <para>This is the local/dev provider and also the correct provider for any deployment that
/// injects secrets as environment variables (ECS task secrets, Kubernetes secrets). It is not a
/// stub: for a large share of deployments it is the production answer, which is why it is a
/// first-class provider rather than a fallback.</para>
///
/// <para>No caching: <see cref="IConfiguration"/> is already in-memory, and caching a cached value
/// would only add a way for the two to disagree after a reload.</para></summary>
public class ConfigurationSecretProvider(IConfiguration config) : ISecretProvider
{
    public Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        var value = config[name];
        return Task.FromResult(string.IsNullOrWhiteSpace(value) ? null : value);
    }

    public async Task<string> GetRequiredAsync(string name, CancellationToken ct = default)
        => await GetAsync(name, ct) ?? throw new MissingSecretException(name);

    /// <summary>No-op: nothing is cached, so there is nothing to invalidate.</summary>
    public Task InvalidateAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Reads secrets from AWS Secrets Manager, with a short TTL cache (AM10, D-101a).
///
/// <para><b>Caching is not an optimisation here, it is a requirement.</b> Secrets Manager is billed
/// per API call and rate-limited; fetching a database password on every request would be both
/// expensive and a throttling outage waiting to happen. The TTL is deliberately short so a rotation
/// converges quickly without anyone intervening.</para>
///
/// <para><b>Rotation:</b> AWS rotates a secret out from under a running process, which makes every
/// cached copy wrong and produces failures that look exactly like a bad credential.
/// <see cref="InvalidateAsync"/> exists so a caller that sees an auth failure from a downstream
/// dependency can force a refresh and retry once before concluding the credential is genuinely
/// wrong. The TTL alone would eventually fix it; the explicit invalidation makes it immediate.</para>
///
/// <para><b>Failures are not cached.</b> A transient Secrets Manager error must not pin an
/// application into a broken state until the TTL expires — only successful reads are stored.</para>
///
/// ⚠ <b>Never executed against AWS from this environment</b> — see D-101a. Reviewed, compiled, and
/// unit-tested against a fake client, but the real API interaction is PENDING DEPLOYMENT CONFIGURATION.</summary>
public class AwsSecretsManagerProvider(
    ISecretsManagerClient client,
    IMemoryCache cache,
    IConfiguration config,
    ILogger<AwsSecretsManagerProvider> log) : ISecretProvider
{
    /// <summary>Optional prefix so one AWS account can host several environments
    /// (`kurx/production/JWT_SECRET`) without name collisions.</summary>
    private string Prefix => config["AWS_SECRETS_PREFIX"] ?? "";

    private TimeSpan Ttl => TimeSpan.FromMinutes(
        int.TryParse(config["AWS_SECRETS_CACHE_MINUTES"], out var m) ? m : 5);

    private static string CacheKey(string name) => $"secret:{name}";

    public async Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        if (cache.TryGetValue<string>(CacheKey(name), out var cached) && cached is not null)
            return cached;

        try
        {
            var value = await client.GetSecretStringAsync(Prefix + name, ct);
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Only successes are cached — a transient error must not pin the app into a broken
            // state until the TTL expires.
            cache.Set(CacheKey(name), value, Ttl);
            return value;
        }
        catch (ResourceNotFoundException)
        {
            // Genuinely absent, not an outage. Caller decides whether that is fatal.
            return null;
        }
        catch (Exception ex)
        {
            // Deliberately rethrown rather than returning null: a throttled or unreachable Secrets
            // Manager is NOT the same as "this secret does not exist", and collapsing the two would
            // let a transient AWS problem silently disable a security feature.
            log.LogError(ex, "Failed to read secret {SecretName} from AWS Secrets Manager", name);
            throw;
        }
    }

    public async Task<string> GetRequiredAsync(string name, CancellationToken ct = default)
        => await GetAsync(name, ct) ?? throw new MissingSecretException(name);

    public Task InvalidateAsync(string name, CancellationToken ct = default)
    {
        cache.Remove(CacheKey(name));
        return Task.CompletedTask;
    }
}


/// <summary>Builds an <see cref="ISecretProvider"/> outside the DI container (AM10, D-101a).
///
/// <para>Needed because some secrets are required <b>before</b> the container exists — the JWT
/// signing key is read while configuring bearer authentication, long before
/// <c>builder.Build()</c>. Without this, that consumer would have to read <see cref="IConfiguration"/>
/// directly, which silently bypasses the provider and makes AWS Secrets Manager unusable for the
/// single most important secret in the system.</para>
///
/// <para>Selection logic is identical to the DI registration, and both call this, so the two can
/// never drift apart.</para></summary>
public static class SecretProviderFactory
{
    public static ISecretProvider Create(IConfiguration config, ILoggerFactory? loggerFactory = null)
    {
        var provider = config["SECRETS_PROVIDER"] ?? "configuration";
        return provider switch
        {
            "configuration" => new ConfigurationSecretProvider(config),
            "aws" => new AwsSecretsManagerProvider(
                new AwsSecretsManagerClientAdapter(new AmazonSecretsManagerClient()),
                new MemoryCache(new MemoryCacheOptions()),
                config,
                (loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
                    .CreateLogger<AwsSecretsManagerProvider>()),
            // Fail closed: never silently fall back to reading secrets from somewhere unintended.
            _ => throw new NotSupportedException(
                $"SECRETS_PROVIDER={provider} is not supported; use 'configuration' or 'aws'."),
        };
    }
}
