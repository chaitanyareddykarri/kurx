using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Kurx.Infrastructure.Presence;

/// <summary>
/// Redis-backed presence (D-114).
///
/// Three key shapes, and the reverse index is the one that matters:
///
///   <c>chat:conns:{room}:{user}</c>   → connection ids. Its size is the per-user refcount, which is
///                                       how a second tab or device avoids marking someone offline.
///   <c>chat:online:{room}</c>          → user ids with at least one connection. One round trip for a
///                                       roster, instead of one lookup per candidate.
///   <c>chat:conn-rooms:{connId}</c>    → the rooms a connection joined. **Without this, disconnect
///                                       has no idea what to clean**, which is exactly why D-106's
///                                       registry could report someone online for 12 hours after they
///                                       closed the tab — and why push was suppressed for them.
///
/// The TTL is a crash backstop, not the cleanup path: normal cleanup is <c>ReleaseAsync</c> from the
/// hub's disconnect handler. The TTL only matters when a process dies without running it.
/// </summary>
public class RedisPresenceService(IConnectionMultiplexer redis, ILogger<RedisPresenceService> log) : IPresenceService
{
    /// <summary>Short enough that a crashed server stops reporting phantom users quickly, long enough
    /// that a heartbeat at half this interval never races it.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(120);

    public bool IsEnabled => true;

    private IDatabase Db => redis.GetDatabase();

    private static string ConnKey(Guid roomId, Guid userId) => ChatPresenceKeys.ConnectionSet(roomId, userId);
    private static string OnlineKey(Guid roomId) => $"chat:online:{roomId}";
    private static string ConnRoomsKey(string connectionId) => $"chat:conn-rooms:{connectionId}";
    private static string Membership(Guid roomId, Guid userId) => $"{roomId}:{userId}";

    public async Task<bool> TrackAsync(Guid roomId, Guid userId, string connectionId, CancellationToken ct = default)
    {
        var db = Db;
        var connKey = ConnKey(roomId, userId);

        // SADD reports whether the id was *newly* added. Both halves matter: size == 1 alone would
        // report "came online" again on every re-join of an already-registered connection, which is
        // precisely the reconnect flicker presence is supposed to avoid.
        var added = await db.SetAddAsync(connKey, connectionId);
        await db.SetAddAsync(ConnRoomsKey(connectionId), Membership(roomId, userId));

        var connections = await db.SetLengthAsync(connKey);
        var wasFirst = added && connections == 1;
        if (wasFirst) await db.SetAddAsync(OnlineKey(roomId), userId.ToString());

        await RefreshAsync(db, roomId, userId, connectionId);
        return wasFirst;
    }

    public async Task<IReadOnlyList<PresenceDeparture>> ReleaseAsync(string connectionId, CancellationToken ct = default)
    {
        var db = Db;
        var reverseKey = ConnRoomsKey(connectionId);

        var memberships = await db.SetMembersAsync(reverseKey);
        if (memberships.Length == 0) return [];

        var departures = new List<PresenceDeparture>();
        foreach (var entry in memberships)
        {
            var parts = entry.ToString().Split(':');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var roomId) || !Guid.TryParse(parts[1], out var userId))
                continue;

            var connKey = ConnKey(roomId, userId);
            await db.SetRemoveAsync(connKey, connectionId);

            // Offline only when nothing of this user is left anywhere — another tab or device keeps
            // them online, which is the whole point of counting connections rather than sessions.
            if (await db.SetLengthAsync(connKey) == 0)
            {
                await db.KeyDeleteAsync(connKey);
                await db.SetRemoveAsync(OnlineKey(roomId), userId.ToString());
                departures.Add(new PresenceDeparture(roomId, userId));
            }
        }

        // Deleting the reverse index last makes the whole operation idempotent: a repeated release
        // finds nothing and reports nothing, so a retried disconnect cannot emit a second offline.
        await db.KeyDeleteAsync(reverseKey);
        return departures;
    }

    public async Task HeartbeatAsync(string connectionId, CancellationToken ct = default)
    {
        var db = Db;
        var memberships = await db.SetMembersAsync(ConnRoomsKey(connectionId));
        foreach (var entry in memberships)
        {
            var parts = entry.ToString().Split(':');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var roomId) || !Guid.TryParse(parts[1], out var userId))
                continue;
            await RefreshAsync(db, roomId, userId, connectionId);
        }
    }

    public async Task<IReadOnlySet<Guid>> OnlineUsersAsync(Guid roomId, CancellationToken ct = default)
    {
        try
        {
            var members = await Db.SetMembersAsync(OnlineKey(roomId));
            var result = new HashSet<Guid>();
            foreach (var m in members)
                if (Guid.TryParse(m.ToString(), out var id)) result.Add(id);
            return result;
        }
        catch (RedisException ex)
        {
            // Presence is an optimisation. An unreachable Redis must degrade to "nobody known to be
            // online" — which over-notifies rather than silently dropping notifications.
            log.LogWarning(ex, "Presence lookup failed for room {RoomId}; treating everyone as offline", roomId);
            return new HashSet<Guid>();
        }
    }

    public async Task<IReadOnlyList<string>> ConnectionsAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var members = await Db.SetMembersAsync(ConnKey(roomId, userId));
        return members.Select(m => m.ToString()).ToList();
    }

    public async Task ForgetAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        await db.KeyDeleteAsync(ConnKey(roomId, userId));
        await db.SetRemoveAsync(OnlineKey(roomId), userId.ToString());
    }

    /// <summary>Refreshes the TTL on everything a connection owns. The online set is refreshed too so
    /// a busy room never expires out from under its members.</summary>
    private static async Task RefreshAsync(IDatabase db, Guid roomId, Guid userId, string connectionId)
    {
        await db.KeyExpireAsync(ConnKey(roomId, userId), Ttl);
        await db.KeyExpireAsync(ConnRoomsKey(connectionId), Ttl);
        await db.KeyExpireAsync(OnlineKey(roomId), Ttl);
    }
}
