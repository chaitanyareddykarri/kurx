using Kurx.Application.Abstractions;

namespace Kurx.Api.Auth;

/// <summary>Keeps the JWT validation key set hot so the signing-key resolver never pays for a database
/// round-trip on a request thread (D-254).
///
/// <para><c>IssuerSigningKeyResolver</c> is a synchronous contract — <c>JwtBearerOptions</c> exposes no
/// async variant — so the resolver has no choice but to call
/// <c>GetValidationKeysAsync().GetAwaiter().GetResult()</c>. That is only a real block when the call has
/// work to do: <c>SigningKeyService</c> memory-caches the set, and a cache hit completes synchronously so
/// <c>GetResult()</c> returns without ever parking a thread-pool thread. The cost lands exactly at cache
/// expiry and at cold start — on whichever unlucky request arrives first, including the one that triggers
/// <c>CreateAndActivateAsync</c> on a fresh deployment.</para>
///
/// <para>Refreshing on a slightly shorter period than the cache TTL moves that cost off the request path
/// altogether: the entry is replaced before it can expire, so the resolver's call is always a hit. A
/// failed refresh is logged and retried on the next tick rather than rethrown — the resolver still falls
/// back to loading the keys itself, so a transient database blip must not kill the warmer.</para></summary>
public class SigningKeyCacheWarmer(IServiceScopeFactory scopeFactory, ILogger<SigningKeyCacheWarmer> log)
    : BackgroundService
{
    // Must stay under SigningKeyService.CacheSeconds (30). Not referenced directly: that constant is
    // internal to the Infrastructure assembly, and widening its visibility for a timer is not worth it.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISigningKeyService>()
                    .GetValidationKeysAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;     // shutdown, not a failure
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Signing-key cache warm failed; retrying in {Interval}.", RefreshInterval);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
