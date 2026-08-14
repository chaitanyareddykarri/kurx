using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kurx.Api.HealthChecks;

/// <summary>Verifies the localdisk storage root actually exists and is writable (STORAGE_PROVIDER=localdisk).</summary>
public class LocalDiskStorageHealthCheck(IConfiguration config) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var root = Path.GetFullPath(config["LOCALDISK_ROOT"] ?? ".localdata/storage");
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".healthcheck-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, [0]);
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy($"{root} is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"{root} is not writable.", ex));
        }
    }
}
