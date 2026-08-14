namespace Kurx.Application.Abstractions;

/// <summary>Thrown when a secret the platform cannot operate without is missing.
///
/// <para>Deliberately fatal. A missing signing key or database password must stop the process, not
/// degrade it — an application that starts without its secrets and fails per-request is far harder
/// to diagnose than one that refuses to boot and says which secret is absent.</para></summary>
public class MissingSecretException(string name)
    : Exception($"Required secret '{name}' is not available from the configured secret provider.")
{
    public string SecretName { get; } = name;
}

/// <summary>Secret retrieval, behind the same provider boundary as every other external dependency
/// (AM10, D-101a). Local development reads configuration/environment; production reads AWS Secrets
/// Manager. No call site knows which.</summary>
public interface ISecretProvider
{
    /// <summary>Returns the secret, or null when it is not configured. Use for genuinely optional
    /// secrets only — anything the platform needs should use <see cref="GetRequiredAsync"/> so a
    /// misconfiguration fails loudly instead of silently disabling a feature.</summary>
    Task<string?> GetAsync(string name, CancellationToken ct = default);

    /// <summary>Returns the secret or throws <see cref="MissingSecretException"/>. Fail-closed.</summary>
    Task<string> GetRequiredAsync(string name, CancellationToken ct = default);

    /// <summary>Drops any cached copy so the next read fetches fresh.
    ///
    /// <para>This is what makes rotation survivable. A rotated secret makes every cached copy wrong,
    /// and the resulting failure looks exactly like a bad credential — so a caller that gets an
    /// auth error from a downstream dependency should invalidate and retry <b>once</b> before
    /// concluding the credential is genuinely wrong.</para></summary>
    Task InvalidateAsync(string name, CancellationToken ct = default);
}
