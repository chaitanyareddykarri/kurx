using Kurx.Api.Hubs;
using Kurx.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Kurx.Api.Realtime;

public class SignalRBroadcaster(
    IHubContext<ScanHub> scanHub,
    IHubContext<SalesHub> salesHub,
    IHubContext<ChatHub> chatHub,
    IHubContext<NotificationHub> notificationHub,
    IHubContext<LoginHub> loginHub,
    IPresenceService presence,
    ILogger<SignalRBroadcaster> log) : IRealtimeBroadcaster
{
    /// <summary>Delivers one realtime hint, and never lets its failure escape (D-301).
    ///
    /// <para>Every method on this class is a <b>live hint on top of state that is already committed</b> — a
    /// role change, a ban, a persisted notification row, a paid order. The authoritative write has happened
    /// by the time we get here, and every client recomputes from the server on its next read, reconnect or
    /// fetch. So a backplane failure is a missed nudge, never lost truth.</para>
    ///
    /// <para>Letting it propagate was strictly worse than dropping it. <c>SendAsync</c> goes through the
    /// Redis backplane when one is configured, so a Redis blip threw straight out of the service call that
    /// had already committed: <c>ChatService.SetModeratorAsync</c> answered 500 for a promotion that had
    /// succeeded, and its notification — the line after the broadcast — never ran at all, so the promoted
    /// member was told nothing. <see cref="EvictFromChatAsync"/> was worse still; see there.</para>
    ///
    /// <para>D-294 settled this rule one layer down, where ChatHub's rate limiter catches
    /// <c>RedisException</c> and degrades rather than turning "the limiter is unavailable" into "you cannot
    /// send a message". This is that same rule at the broadcast boundary, which never learned it.</para>
    ///
    /// <para>Cancellation is rethrown, not swallowed: a cancelled request is the caller going away, not a
    /// delivery failure, and reporting it as one would hide real cancellation from the pipeline.</para></summary>
    private async Task SendSafeAsync(Func<Task> send, string what, CancellationToken ct)
    {
        try
        {
            await send();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Warning, not Error: the system is behaving correctly under a degraded dependency, and the
            // clients converge on their own. It is logged because a sustained run of these means the
            // backplane is down, which is worth paging on from the metric rather than from one line.
            log.LogWarning(ex, "Realtime {What} was not delivered; clients refresh on their next read.", what);
        }
    }

    public async Task EvictFromChatAsync(Guid roomId, Guid userId, string eventName = "MemberBanned",
        CancellationToken ct = default)
    {
        // Each step is guarded independently, and that is the whole point here rather than a nicety.
        // Unguarded, a throw on the line below skipped every line under it — so a backplane blip during a
        // ban or a refund left the member's sockets still in the group and they kept receiving every
        // message in a room they had just been removed from. The announcement is the least important step
        // of the three; it used to be the one that could cancel the other two.
        await SendSafeAsync(
            () => chatHub.Clients.Group($"chat:{roomId}")
                .SendAsync("chat", new { v = 1, type = eventName, roomId, id = (Guid?)null, payload = new { userId } }, ct),
            $"chat/{eventName}", ct);

        // Reads the same registry presence maintains (D-114), so eviction and presence can never
        // disagree about which sockets a banned user still holds. Removing a connection that has
        // since died is a harmless no-op.
        await SendSafeAsync(
            async () =>
            {
                foreach (var connectionId in await presence.ConnectionsAsync(roomId, userId, ct))
                    await chatHub.Groups.RemoveFromGroupAsync(connectionId, $"chat:{roomId}", ct);

                // Forget them entirely: a banned member must stop appearing online, not linger until TTL.
                await presence.ForgetAsync(roomId, userId, ct);
            },
            "chat/evict", ct);

        await BroadcastChatAsync(roomId, "PresenceChanged", new { userId, online = false }, null, ct);
    }

    public Task BroadcastScanAsync(Guid eventId, object payload, CancellationToken ct = default)
        => SendSafeAsync(() => scanHub.Clients.Group($"event:{eventId}").SendAsync("scan", payload, ct), "scan", ct);

    public Task BroadcastSaleAsync(Guid orgId, object payload, CancellationToken ct = default)
        => SendSafeAsync(() => salesHub.Clients.Group($"org:{orgId}").SendAsync("sale", payload, ct), "sale", ct);

    // D-104: one method name carrying a versioned envelope, rather than one SignalR method per event
    // type — so a new event type ships server-first without a client release.
    public Task BroadcastChatAsync(Guid roomId, string eventName, object payload, Guid? id = null, CancellationToken ct = default)
        => SendSafeAsync(
            () => chatHub.Clients.Group($"chat:{roomId}")
                .SendAsync("chat", new { v = 1, type = eventName, roomId, id, payload }, ct),
            $"chat/{eventName}", ct);

    public Task BroadcastNotificationAsync(Guid userId, object payload, CancellationToken ct = default)
        => SendSafeAsync(
            () => notificationHub.Clients.Group($"user:{userId}").SendAsync("notification", payload, ct),
            "notification", ct);

    public Task BroadcastUnreadCountAsync(Guid userId, int count, CancellationToken ct = default)
        => SendSafeAsync(
            () => notificationHub.Clients.Group($"user:{userId}").SendAsync("unread_count", new { count }, ct),
            "unread_count", ct);

    public Task BroadcastBadgeUnlockAsync(Guid userId, object payload, CancellationToken ct = default)
        => SendSafeAsync(
            () => notificationHub.Clients.Group($"user:{userId}").SendAsync("badge_unlock", payload, ct),
            "badge_unlock", ct);

    public Task BroadcastWalletAsync(Guid orgId, object payload, CancellationToken ct = default)
        => SendSafeAsync(() => salesHub.Clients.Group($"org:{orgId}").SendAsync("wallet", payload, ct), "wallet", ct);

    public Task BroadcastAnalyticsAsync(Guid orgId, object payload, CancellationToken ct = default)
        => SendSafeAsync(() => salesHub.Clients.Group($"org:{orgId}").SendAsync("analytics", payload, ct), "analytics", ct);

    public Task BroadcastLoginStatusAsync(Guid challengeId, string status, CancellationToken ct = default)
        => SendSafeAsync(
            () => loginHub.Clients.Group($"login:{challengeId}").SendAsync("login_status", new { status }, ct),
            "login_status", ct);
}
