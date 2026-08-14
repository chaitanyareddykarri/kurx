using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Kurx.Api.Hubs;

[Authorize]
public class ChatHub(
    IChatService chat,
    KurxDbContext db,
    IPresenceService presence,
    IRealtimeBroadcaster broadcaster,
    IConnectionMultiplexer? redis = null) : Hub
{
    // Redis sliding window rate-limit: 10 messages / 60s per user per room
    private const int RateLimitWindow = 60;
    private const int RateLimitMax = 10;

    /// <summary>How long the server keeps believing someone is typing without a refresh. Clients
    /// re-send while the composer stays active; the receiver clears on this timeout regardless, so a
    /// dropped "stopped" event can never strand an indicator on screen.</summary>
    public const int TypingTtlSeconds = 8;

    public async Task JoinRoom(Guid roomId)
    {
        var userId = UserId();
        var member = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (member is null || member.IsBanned)
            throw new HubException("forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:{roomId}");

        // An archived room still joins the group — a moderator deleting a message there must still
        // reach whoever is reading the history — but it registers no presence at all (D-122).
        // Reading an archive is not attendance.
        //
        // Derived rather than read from the stored status (D-123): the sweep that persists Archived
        // runs hourly, and presence must stop the moment the room is actually archived.
        if (await EffectiveStatusAsync(roomId) == Domain.Enums.ChatRoomStatus.Archived) return;

        // Idempotent: a reconnect or a duplicate join re-registers the same connection id without
        // inflating the count, so presence cannot drift.
        var cameOnline = await presence.TrackAsync(roomId, userId, Context.ConnectionId);

        // Only the transition matters. A second tab does not re-announce the user, which is what
        // stops a reconnect flickering everyone else's UI.
        if (cameOnline)
            await broadcaster.BroadcastChatAsync(roomId, "PresenceChanged",
                new { userId, online = true }, null);
    }

    public async Task LeaveRoom(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:{roomId}");
        await ReleaseAsync();
    }

    /// <summary>
    /// Cleans up every room this connection joined (D-114).
    ///
    /// This is the normal cleanup path. Before it existed the registry relied on a 12-hour TTL, so a
    /// closed browser looked online for half a day — and, because the notification fan-out reads the
    /// same registry, that user's pushes were suppressed for just as long.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await ReleaseAsync();

        // D-295 — "last seen" is written here because this is the moment presence stops being able to
        // answer: while connected the answer is "online now", and the instant the socket closes there
        // is nothing left to derive it from. An in-SQL write on the user row, so two devices
        // disconnecting together cannot lose one another's update.
        if (Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var now = DateTime.UtcNow;
            try
            {
                await db.Users.Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, now));
            }
            catch
            {
                // A courtesy signal. A disconnect must complete and release presence even if this
                // write fails, so it never becomes the reason cleanup does not run.
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task ReleaseAsync()
    {
        // Idempotent by construction: a second release finds no memberships and announces nothing.
        var departures = await presence.ReleaseAsync(Context.ConnectionId);
        foreach (var gone in departures)
            await broadcaster.BroadcastChatAsync(gone.RoomId, "PresenceChanged",
                new { userId = gone.UserId, online = false }, null);
    }

    /// <summary>Keeps this connection's presence TTL alive.
    ///
    /// Not polling: presence *state* is only ever pushed as an event. This is a liveness ping that
    /// exists so the TTL can stay short enough to recover from a crashed process without expiring
    /// under a healthy idle connection.</summary>
    /// <summary>
    /// The room's real state, from the shared lifecycle rule (D-124) — never recomputed here.
    /// </summary>
    private async Task<Domain.Enums.ChatRoomStatus?> EffectiveStatusAsync(Guid roomId)
    {
        var row = await db.ChatRooms.AsNoTracking()
            .Where(r => r.Id == roomId)
            .Join(db.Events, r => r.EventId, e => e.Id, (r, e) => new { r.Status, e.EndsAt })
            .FirstOrDefaultAsync();
        return row is null
            ? null
            : Domain.Entities.ChatLifecycle.EffectiveStatus(row.Status, row.EndsAt, DateTime.UtcNow);
    }

    public Task Heartbeat() => presence.HeartbeatAsync(Context.ConnectionId);

    /// <summary>
    /// Broadcasts that this user started or stopped typing.
    ///
    /// Never persisted, never pushed, never audited — it exists only while the socket does. The
    /// posting capability is re-checked here rather than trusted from the client, so a muted, banned
    /// or read-only member cannot advertise typing they would not be allowed to finish.
    /// </summary>
    public async Task Typing(Guid roomId, bool isTyping)
    {
        if (!presence.IsEnabled) return;

        var userId = UserId();
        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
        var member = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (room is null || member is null) throw new HubException("forbidden");

        // Same gates as sending: locked room, ban, active mute, hosts-only policy. Effective, not
        // stored (D-124) — otherwise typing would keep broadcasting for an hour after the event ended,
        // in a room that already refuses messages.
        var silenced = member.IsBanned || (member.MutedUntil.HasValue && member.MutedUntil > DateTime.UtcNow);
        var mayPost = await EffectiveStatusAsync(roomId) == Domain.Enums.ChatRoomStatus.Active
                      && !silenced
                      && (room.PostPolicy != Domain.Enums.ChatPostPolicy.HostsOnly
                          || member.Role == Domain.Enums.ChatMemberRole.Host);
        if (!mayPost) return;   // silently ignored: not an error, just nothing to broadcast

        var name = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync();

        // Sent to the group including the sender; clients filter themselves out. Excluding here would
        // need a per-connection send and buys nothing.
        await broadcaster.BroadcastChatAsync(roomId, "TypingChanged",
            new { userId, userName = name, isTyping, ttlSeconds = TypingTtlSeconds }, null);
    }

    public async Task SendMessage(Guid roomId, string body, Guid? replyToId = null, Guid? clientMessageId = null)
    {
        var userId = UserId();
        if (!await CheckRateLimitAsync(roomId, userId))
            throw new HubException("RATE_LIMITED");

        // clientMessageId makes this idempotent per room (D-104), so a client that reconnects
        // mid-send can safely replay its outbox over the hub without duplicating messages.
        var result = await chat.SendMessageAsync(roomId, userId, body, replyToId, clientMessageId);
        if (!result.Ok) throw new HubException(result.Error);
        // Broadcast already performed by ChatService via IRealtimeBroadcaster
    }

    private async Task<bool> CheckRateLimitAsync(Guid roomId, Guid userId)
    {
        if (redis is null)
        {
            // In-memory fallback (single-instance dev): always allow
            return true;
        }
        var key = $"chat:rl:{userId}:{roomId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - RateLimitWindow;
        try
        {
            var db_ = redis.GetDatabase();
            var tx = db_.CreateTransaction();
            // Sliding window via sorted set — score = timestamp, value = uuid
            var member = Guid.NewGuid().ToString();
            _ = tx.SortedSetAddAsync(key, member, now);
            _ = tx.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, windowStart);
            _ = tx.KeyExpireAsync(key, TimeSpan.FromSeconds(RateLimitWindow + 5));
            await tx.ExecuteAsync();
            var count = await db_.SortedSetLengthAsync(key);
            return count <= RateLimitMax;
        }
        catch (RedisException)
        {
            // D-294 — degrade, do not fail. Redis being unreachable is an operational condition, and
            // letting it escape turned "the rate limiter is unavailable" into "you cannot send a
            // message": the exception propagated out of SendMessage and the send failed outright.
            //
            // Failing OPEN is safe here only because HubRateLimitFilter is a per-connection floor that
            // needs no external store, so an outage degrades the per-room limit to a per-connection one
            // rather than removing throttling altogether. Without that filter this would be a hole.
            return true;
        }
    }

    private Guid UserId()
        => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new HubException("invalid_token");
}
