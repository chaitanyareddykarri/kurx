using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Kurx.Api.RateLimiting;

/// <summary>A fixed-window rate limiter whose counter lives in Redis, so a limit means the same thing
/// across every replica (D-255).
///
/// <para>The built-in <see cref="FixedWindowRateLimiter"/> and <see cref="SlidingWindowRateLimiter"/> keep
/// their counters in process memory. With N replicas behind a load balancer that silently multiplies every
/// configured limit by N — including the per-IP shield in front of OTP issuance, where the whole point is
/// to bound how fast one host can enumerate phone numbers.</para>
///
/// <para>The increment and its expiry are applied in one Lua script so they are atomic: a bare
/// INCR-then-EXPIRE pair leaks a permanent key whenever the process dies between the two calls, and that
/// key then rejects the partition forever.</para>
///
/// <para><b>Fails open.</b> If Redis is unreachable the request is allowed and a warning is logged. The
/// alternative — rejecting — turns a cache blip into a total outage. This is safe because the limits that
/// actually protect credentials are not these: OTP issuance is additionally capped per destination and per
/// IP in Postgres by <c>OtpService</c>/<c>AuthService</c> (D-005), which is durable and unaffected by Redis
/// being down. These edge limits are a throughput shield, never the authoritative control.</para></summary>
internal sealed class RedisFixedWindowRateLimiter : RateLimiter
{
    // Returns the post-increment count, setting the expiry only on the write that created the key.
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
        return count
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger _log;
    private readonly string _partitionKey;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private long _lastUsedTicks = DateTime.UtcNow.Ticks;

    public RedisFixedWindowRateLimiter(
        IConnectionMultiplexer redis, ILogger log, string partitionKey, int permitLimit, TimeSpan window)
    {
        _redis = redis;
        _log = log;
        _partitionKey = partitionKey;
        _permitLimit = permitLimit;
        _window = window;
    }

    /// <summary>Drives partition reclamation in <see cref="PartitionedRateLimiter"/>: this object holds no
    /// counter state (Redis does), but without an idle age one instance per distinct key would accumulate.</summary>
    public override TimeSpan? IdleDuration => TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastUsedTicks));

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _lastUsedTicks, DateTime.UtcNow.Ticks);
        try
        {
            var count = (long)await _redis.GetDatabase().ScriptEvaluateAsync(
                IncrementScript, [CurrentWindowKey()], [(long)_window.TotalMilliseconds]);
            return LeaseFor(count);
        }
        catch (RedisException ex)
        {
            return FailOpen(ex);
        }
        catch (ObjectDisposedException ex)      // multiplexer torn down during shutdown
        {
            return FailOpen(ex);
        }
    }

    /// <summary>The synchronous path blocks on Redis rather than guessing. ASP.NET Core's rate-limiting
    /// middleware only ever calls <see cref="AcquireAsyncCore"/>, so this is here for contract completeness
    /// — returning a fabricated answer would silently disable the limit for any caller that did use it.</summary>
    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        Interlocked.Exchange(ref _lastUsedTicks, DateTime.UtcNow.Ticks);
        try
        {
            var count = (long)_redis.GetDatabase().ScriptEvaluate(
                IncrementScript, [CurrentWindowKey()], [(long)_window.TotalMilliseconds]);
            return LeaseFor(count);
        }
        catch (RedisException ex)
        {
            return FailOpen(ex);
        }
    }

    // The hash tag keeps a partition's key on one slot if this ever runs against Redis Cluster; the window
    // ordinal is what makes the window fixed rather than rolling.
    private RedisKey CurrentWindowKey()
        => $"rl:{{{_partitionKey}}}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / (long)_window.TotalMilliseconds}";

    private RateLimitLease LeaseFor(long count)
        => count <= _permitLimit ? new Lease(true, null) : new Lease(false, _window);

    private RateLimitLease FailOpen(Exception ex)
    {
        _log.LogWarning(ex, "Rate-limit backend unavailable for partition {Partition}; allowing the request.", _partitionKey);
        return new Lease(true, null);
    }

    private sealed class Lease(bool acquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => acquired;

        public override IEnumerable<string> MetadataNames =>
            retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter is { } value && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = value;
                return true;
            }
            metadata = null;
            return false;
        }
    }
}
