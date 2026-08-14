using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>
/// The one definition of what state a chat room is actually in (D-124).
/// </summary>
/// <remarks>
/// Chat lifecycle is derived from the event's, and an event ending is not an action anybody takes —
/// it is a timestamp passing. There is therefore no transition to hook, and every gate has to work
/// out the current state for itself. Doing that arithmetic in more than one place is how the API,
/// the hub and the background jobs end up disagreeing about whether a room is closed.
///
/// Pure and framework-free: callers resolve the two inputs however suits them (a join, a cached row,
/// a projection) and the rule itself lives here.
/// </remarks>
public static class ChatLifecycle
{
    /// <summary>Read-only window between an event ending and its chat being archived.</summary>
    public const int ArchiveAfterEndDays = 7;

    /// <summary>
    /// The stored status advanced by whatever the clock has already decided.
    /// </summary>
    /// <remarks>
    /// Only ever moves forward. A room locked early — by cancellation, or by a host — stays locked
    /// even though its event has not ended yet, and an archived room never comes back.
    /// </remarks>
    public static ChatRoomStatus EffectiveStatus(ChatRoomStatus stored, DateTime eventEndsAt, DateTime nowUtc)
    {
        if (stored == ChatRoomStatus.Archived) return ChatRoomStatus.Archived;
        if (eventEndsAt <= nowUtc.AddDays(-ArchiveAfterEndDays)) return ChatRoomStatus.Archived;
        if (eventEndsAt <= nowUtc) return ChatRoomStatus.Locked;
        return stored;
    }
}
