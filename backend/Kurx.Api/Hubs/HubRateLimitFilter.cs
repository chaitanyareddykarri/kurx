using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Kurx.Api.Hubs;

/// <summary>
/// Per-connection throttling for hub method invocations (D-294).
///
/// <para><b>Why this exists.</b> <c>UseRateLimiter</c> is HTTP middleware: it sees the SignalR
/// <c>/negotiate</c> request and nothing after it. Once the socket is up, every hub method is
/// unthrottled — so <see cref="ChatHub.Typing"/>, which costs three database queries and a broadcast to
/// every member of the room, could be invoked in a tight loop by any authenticated client, and
/// <see cref="LoginHub.Watch"/> could be by an anonymous one. The global 300/min per user bought
/// nothing here.</para>
///
/// <para><b>Why in-process rather than Redis.</b> A connection lives on exactly one instance for its
/// whole life and cannot move, so the count for that connection is complete on that instance. This is
/// the opposite of the HTTP limiter's situation, where consecutive requests from one user land
/// anywhere — which is why that one is Redis-backed and this one must not be. A Redis round trip per
/// typing event would also cost more than the work being protected.</para>
///
/// <para>Deliberately coarse: this is an anti-abuse floor, not a usage quota. The limits sit far above
/// what a real client sends, so tripping one means a bug or an attack, never ordinary use.</para>
/// </summary>
public class HubRateLimitFilter : IHubFilter
{
    /// <summary>Window length for every bucket. One window rather than per-method windows keeps the
    /// accounting to a single timestamp per method.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    /// <summary>Per-method ceilings within <see cref="Window"/>, chosen against what a correct client
    /// actually sends.</summary>
    private static readonly Dictionary<string, int> Limits = new(StringComparer.Ordinal)
    {
        // The composer debounces and re-sends only while the user keeps typing, so a real client
        // sends a handful per window at most. This is the expensive one — three queries plus a
        // room-wide fan-out per call.
        [nameof(ChatHub.Typing)] = 8,
        // The client pings every 45s against a 120s TTL; anything near this is a loop.
        [nameof(ChatHub.Heartbeat)] = 4,
        // Joining is idempotent, so a reconnect storm is the only legitimate source of repeats.
        [nameof(ChatHub.JoinRoom)] = 10,
        // Anonymous (AM9): the poll token is 32 CSPRNG bytes so this is not a guessing oracle, but it
        // is an unauthenticated database read and gets the tightest ceiling of the lot.
        [nameof(LoginHub.Watch)] = 5,
    };

    /// <summary>Everything not named above. SendMessage has its own per-room limiter in ChatHub; this
    /// is the floor beneath it, and beneath every future hub method that forgets to add one.</summary>
    private const int DefaultLimit = 30;

    /// <summary>Keyed by connection id, so entries are bounded by live connections and removed on
    /// disconnect. Not static: one instance per host, resolved as a singleton.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Counter>> _counters = new();

    private sealed class Counter
    {
        public DateTime WindowStart;
        public int Count;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var method = context.HubMethodName;
        var limit = Limits.GetValueOrDefault(method, DefaultLimit);

        if (!Allow(context.Context.ConnectionId, method, limit))
            // A HubException reaches the client as a normal method failure, which every client already
            // handles — the same shape ChatHub.SendMessage uses for its own limiter. The connection is
            // deliberately left open: dropping it would make a reconnect storm out of a burst.
            throw new HubException("rate_limited");

        return await next(context);
    }

    /// <summary>Fixed window per (connection, method). Fixed rather than sliding on purpose: a sliding
    /// window needs a timestamp list per bucket, and the burst a fixed window permits at a boundary is
    /// irrelevant at ceilings set this far above real traffic.</summary>
    private bool Allow(string connectionId, string method, int limit)
    {
        var perMethod = _counters.GetOrAdd(connectionId, _ => new ConcurrentDictionary<string, Counter>());
        var counter = perMethod.GetOrAdd(method, _ => new Counter { WindowStart = DateTime.UtcNow });

        // One connection's invocations of one method can interleave across threads, so the
        // read-modify-write is locked. Contention is per (connection, method) and therefore nil.
        lock (counter)
        {
            var now = DateTime.UtcNow;
            if (now - counter.WindowStart >= Window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }
            counter.Count++;
            return counter.Count <= limit;
        }
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        // Bounded by live connections only — without this the dictionary grows for the process's life.
        _counters.TryRemove(context.Context.ConnectionId, out _);
        await next(context, exception);
    }
}
