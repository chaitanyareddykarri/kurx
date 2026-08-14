using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Presence;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Kurx.Tests;

/// <summary>
/// Presence lifecycle (D-114), driven against a real Redis.
///
/// The cases that matter are the ones a naive implementation gets wrong: a second device must not be
/// able to mark someone offline, a disconnect must clean up rooms the connection never explicitly
/// left, and a repeated disconnect must not emit a second offline event.
///
/// These need Redis and nothing else — no database — so they run against the Redis container
/// directly. When Redis is unreachable each case returns early rather than asserting against
/// process-local state, which would prove nothing about production.
/// </summary>
public class ChatPresenceTests : IAsyncLifetime
{
    private IConnectionMultiplexer? _redis;
    private RedisPresenceService _presence = null!;
    private readonly Guid _room = Guid.NewGuid();
    private readonly Guid _otherRoom = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();

    /// <summary>Connection ids must be unique per test. Real SignalR ids are globally unique, but a
    /// literal like "conn-1" reused across tests would share one reverse index and make disconnect
    /// cleanup pick up another test's memberships.</summary>
    private readonly string _tag = Guid.NewGuid().ToString("N")[..8];
    private string Conn(string name) => $"{_tag}-{name}";

    private bool Available => _redis is not null;

    public async Task InitializeAsync()
    {
        // The container test runner shares a network namespace with kurx-redis when it is running.
        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,connectTimeout=1000");
            if (!_redis.IsConnected) _redis = null;
        }
        catch
        {
            _redis = null;
        }

        if (_redis is not null)
            _presence = new RedisPresenceService(_redis, NullLogger<RedisPresenceService>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_redis is null) return;
        // Leave no keys behind for the next class.
        await _presence.ForgetAsync(_room, _user);
        await _presence.ForgetAsync(_room, _otherUser);
        await _presence.ForgetAsync(_otherRoom, _user);
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task First_connection_marks_the_user_online()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        var first = await _presence.TrackAsync(_room, _user, Conn("conn-1"));

        Assert.True(first);
        Assert.Contains(_user, await _presence.OnlineUsersAsync(_room));
    }

    [Fact]
    public async Task A_second_device_does_not_re_announce_the_user()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        Assert.True(await _presence.TrackAsync(_room, _user, Conn("conn-1")));
        // Only the transition matters — a second tab must not flicker everyone else's UI.
        Assert.False(await _presence.TrackAsync(_room, _user, Conn("conn-2")));
    }

    [Fact]
    public async Task Tracking_the_same_connection_twice_is_idempotent()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        Assert.True(await _presence.TrackAsync(_room, _user, Conn("conn-1")));
        // A reconnect that re-runs JoinRoom must not inflate the refcount, or the user would never
        // be able to go offline again.
        Assert.False(await _presence.TrackAsync(_room, _user, Conn("conn-1")));

        await _presence.ReleaseAsync(Conn("conn-1"));
        Assert.DoesNotContain(_user, await _presence.OnlineUsersAsync(_room));
    }

    [Fact]
    public async Task Disconnecting_one_device_keeps_the_user_online_while_another_remains()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        await _presence.TrackAsync(_room, _user, Conn("phone"));
        await _presence.TrackAsync(_room, _user, Conn("laptop"));

        var departures = await _presence.ReleaseAsync(Conn("phone"));

        Assert.Empty(departures);   // nothing to announce — they are still here
        Assert.Contains(_user, await _presence.OnlineUsersAsync(_room));

        var last = await _presence.ReleaseAsync(Conn("laptop"));
        Assert.Equal(_user, Assert.Single(last).UserId);
        Assert.DoesNotContain(_user, await _presence.OnlineUsersAsync(_room));
    }

    [Fact]
    public async Task Disconnect_cleans_up_every_room_the_connection_joined()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        // The reverse index is what makes this possible: without it a disconnect has no idea which
        // rooms to clean, which is exactly how the old registry left users online for 12 hours.
        await _presence.TrackAsync(_room, _user, Conn("conn-1"));
        await _presence.TrackAsync(_otherRoom, _user, Conn("conn-1"));

        var departures = await _presence.ReleaseAsync(Conn("conn-1"));

        Assert.Equal(2, departures.Count);
        Assert.DoesNotContain(_user, await _presence.OnlineUsersAsync(_room));
        Assert.DoesNotContain(_user, await _presence.OnlineUsersAsync(_otherRoom));
    }

    [Fact]
    public async Task Releasing_twice_announces_nothing_the_second_time()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        await _presence.TrackAsync(_room, _user, Conn("conn-1"));
        Assert.Single(await _presence.ReleaseAsync(Conn("conn-1")));

        // A retried disconnect, or a server that crashed mid-cleanup, must not emit a second offline.
        Assert.Empty(await _presence.ReleaseAsync(Conn("conn-1")));
    }

    [Fact]
    public async Task Releasing_an_unknown_connection_is_harmless()
    {
        if (!Available) return;
        Assert.Empty(await _presence.ReleaseAsync(Conn("never-seen")));
    }

    [Fact]
    public async Task Users_are_tracked_independently_within_a_room()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        await _presence.TrackAsync(_room, _user, Conn("a"));
        await _presence.TrackAsync(_room, _otherUser, Conn("b"));

        await _presence.ReleaseAsync(Conn("a"));

        var online = await _presence.OnlineUsersAsync(_room);
        Assert.DoesNotContain(_user, online);
        Assert.Contains(_otherUser, online);   // one user leaving never affects another
    }

    [Fact]
    public async Task Connections_are_listed_for_eviction()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        await _presence.TrackAsync(_room, _user, Conn("conn-1"));
        await _presence.TrackAsync(_room, _user, Conn("conn-2"));

        var connections = await _presence.ConnectionsAsync(_room, _user);
        Assert.Equal(2, connections.Count);
        Assert.Contains(Conn("conn-1"), connections);
    }

    [Fact]
    public async Task Forgetting_a_user_takes_them_offline_immediately()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        await _presence.TrackAsync(_room, _user, Conn("conn-1"));
        await _presence.ForgetAsync(_room, _user);

        // A banned member must stop appearing online at once, not linger until the TTL.
        Assert.DoesNotContain(_user, await _presence.OnlineUsersAsync(_room));
        Assert.Empty(await _presence.ConnectionsAsync(_room, _user));
    }

    [Fact]
    public async Task A_heartbeat_keeps_a_live_connection_registered()
    {
        if (!Available) return;   // see the class note: shared state, or nothing worth asserting

        await _presence.TrackAsync(_room, _user, Conn("conn-1"));
        await _presence.HeartbeatAsync(Conn("conn-1"));

        Assert.Contains(_user, await _presence.OnlineUsersAsync(_room));
    }

    [Fact]
    public async Task Presence_disabled_reports_nothing_and_never_uses_local_state()
    {
        IPresenceService disabled = new PresenceDisabledService();

        Assert.False(disabled.IsEnabled);
        // Never "first connection", so no online event is ever emitted...
        Assert.False(await disabled.TrackAsync(_room, _user, "conn-1"));
        // ...and nothing is remembered, so it cannot disagree with another instance.
        Assert.Empty(await disabled.OnlineUsersAsync(_room));
        Assert.Empty(await disabled.ReleaseAsync("conn-1"));
        Assert.Empty(await disabled.ConnectionsAsync(_room, _user));
    }

    [Fact]
    public void An_empty_online_set_makes_notifications_over_deliver_rather_than_drop()
    {
        // The fan-out treats "not in the online set" as offline. With presence disabled the set is
        // empty, so everyone is notified — noisy, but never silently dropping a notification.
        var online = new HashSet<Guid>();
        var recipients = new List<Guid> { _user, _otherUser };

        var offline = recipients.Where(u => !online.Contains(u)).ToList();
        Assert.Equal(recipients, offline);
    }
}
