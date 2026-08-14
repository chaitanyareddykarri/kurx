namespace Kurx.Application.Abstractions;

/// <summary>
/// Redis key shapes for chat connection tracking. Shared because two layers need the same format:
/// <c>ChatHub</c> (Api) writes the set on join, <c>SignalRBroadcaster</c> reads it to evict a banned
/// user (D-106), and <c>ChatNotificationJob</c> (Infrastructure) reads it to decide who is offline and
/// therefore worth pushing to (D-107).
/// </summary>
public static class ChatPresenceKeys
{
    /// <summary>Live SignalR connection ids for one (room, user). Absent = that user has no connection
    /// to this room, which is the signal used to treat them as offline.</summary>
    public static string ConnectionSet(Guid roomId, Guid userId) => $"chat:conns:{roomId}:{userId}";
}
