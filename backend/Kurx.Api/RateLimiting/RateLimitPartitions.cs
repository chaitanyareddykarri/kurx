using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Kurx.Api.RateLimiting;

/// <summary>Chooses the backing store for a windowed rate-limit partition (D-255).
///
/// <para>When <c>REDIS_CONNECTION</c> is configured the counter is shared across replicas; when it is not
/// — single-instance dev, and the integration test host — it falls back to the in-process limiter, so
/// local behaviour is exactly what it was before. That fallback is the reason this is a helper rather than
/// an unconditional swap: dev must keep working with no Redis at all.</para></summary>
internal static class RateLimitPartitions
{
    /// <summary>A fixed window of <paramref name="permitLimit"/> permits, distributed when Redis is present.
    ///
    /// <para>Note the deliberate sliding→fixed change for the global tiers. A distributed sliding window
    /// costs a sorted-set write plus a range trim per request; a fixed window is a single atomic INCR. The
    /// trade is that a caller can spend two windows' permits either side of a boundary — acceptable for a
    /// throughput shield, and irrelevant for OTP, whose authoritative limits are durable in Postgres
    /// (D-005) rather than here.</para></summary>
    public static RateLimitPartition<string> Window(
        HttpContext context, string partitionKey, int permitLimit, TimeSpan window)
    {
        var redis = context.RequestServices.GetService<IConnectionMultiplexer>();
        if (redis is null)
        {
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            });
        }

        var log = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger<RedisFixedWindowRateLimiter>();
        // The factory runs once per distinct partition key, not per request.
        return RateLimitPartition.Get(partitionKey,
            key => new RedisFixedWindowRateLimiter(redis, log, key, permitLimit, window));
    }
}
