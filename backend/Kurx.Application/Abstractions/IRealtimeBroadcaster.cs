namespace Kurx.Application.Abstractions;

/// <summary>
/// Push events to connected clients over SignalR. The hub implementations live in
/// Kurx.Api (SignalR needs the ASP.NET Core shared framework); Infrastructure services
/// only depend on this interface so they stay framework-agnostic and unit-testable.
/// </summary>
public interface IRealtimeBroadcaster
{
    /// <summary>Group "event:{eventId}" — scanner + dashboard clients watching one event.</summary>
    Task BroadcastScanAsync(Guid eventId, object payload, CancellationToken ct = default);

    /// <summary>Group "org:{orgId}" — host dashboard watching live sales.</summary>
    Task BroadcastSaleAsync(Guid orgId, object payload, CancellationToken ct = default);

    /// <summary>Group "chat:{roomId}" — connected chat room clients. Sent as a single versioned
    /// envelope on the "chat" method (D-104): <c>{ v, type, roomId, id, payload }</c>. <paramref name="id"/>
    /// is the message the event concerns, so a reconnecting client can detect a gap and reconcile via
    /// <c>?after=</c>. Clients must ignore unknown types and unknown fields.</summary>
    Task BroadcastChatAsync(Guid roomId, string eventName, object payload, Guid? id = null, CancellationToken ct = default);

    /// <summary>Force a user out of a chat room's SignalR group (D-106). Removing their membership row
    /// only stops the next request — an already-connected client keeps receiving group broadcasts until
    /// it reconnects, which is the actual leak this closes.
    ///
    /// <para><paramref name="eventName"/> names the cause on the wire (D-294). A ban and a refund both
    /// have to evict, but they are not the same event and the payload should not claim they are.</para></summary>
    Task EvictFromChatAsync(Guid roomId, Guid userId, string eventName = "MemberBanned", CancellationToken ct = default);

    /// <summary>User "user:{userId}" — individual notifications pipeline.</summary>
    Task BroadcastNotificationAsync(Guid userId, object payload, CancellationToken ct = default);

    /// <summary>User "user:{userId}" — count changes.</summary>
    Task BroadcastUnreadCountAsync(Guid userId, int count, CancellationToken ct = default);

    /// <summary>User "user:{userId}" — badge updates.</summary>
    Task BroadcastBadgeUnlockAsync(Guid userId, object payload, CancellationToken ct = default);

    /// <summary>Group "org:{orgId}" — wallet updates.</summary>
    Task BroadcastWalletAsync(Guid orgId, object payload, CancellationToken ct = default);

    /// <summary>Group "org:{orgId}" — live dashboard updates.</summary>
    Task BroadcastAnalyticsAsync(Guid orgId, object payload, CancellationToken ct = default);

    /// <summary>Group "login:{challengeId}" — the waiting sign-in screen (AM9). Carries the <b>status only,
    /// never tokens</b>: minting stays on the single-issue HTTP path so that guarantee lives in one place.</summary>
    Task BroadcastLoginStatusAsync(Guid challengeId, string status, CancellationToken ct = default);
}
