namespace Kurx.Application.Abstractions;

// Response records live in this namespace: SnakeCaseResponseConverter (D-259) keys off it and nothing else.

/// <summary>One conversation in the Messages list (D-264). Shaped around the OTHER person, because that
/// is what a DM is to the reader — unlike <see cref="MyChatView"/>, which is shaped around an event.</summary>
public record DmRoomView(
    Guid RoomId,
    Guid OtherUserId,
    // `OtherName` rather than `OtherUserName`: the latter differs from `OtherUsername` only in the case
    // of one letter, and the serializer rejects the pair as colliding property names — a 500 on every
    // read of this list, which is exactly the kind of break that only appears at runtime.
    string OtherName,
    string? OtherUsername,
    string? OtherAvatarKey,
    string RequestState,          // pending | accepted | declined
    bool IsRequest,               // pending AND awaiting THIS caller's decision
    bool Archived,
    string? LastMessagePreview,
    int UnreadCount,
    DateTime? LastActivity,
    /// <summary>D-295 — pinned by this member; the list orders pinned conversations first.</summary>
    bool Pinned = false,
    /// <summary>D-295 — this member silenced their own notifications for this conversation.</summary>
    bool NotificationsMuted = false);

/// <summary>
/// Direct messages (D-264). A DM is a <see cref="Kurx.Domain.Entities.ChatRoom"/> with no event, so
/// everything downstream — messages, attachments, presence, read pointers, the hub, the mobile offline
/// outbox — already works on it. This service only owns what is genuinely new: opening the room,
/// the request gate, and the archive folder.
/// </summary>
public interface IDmService
{
    /// <summary>Opens (or returns) the one room for this pair. Idempotent by database constraint, not by
    /// a prior existence check — two simultaneous taps must not produce two rooms.
    ///
    /// <para>Refuses with <c>blocked</c> if either party has blocked the other (D-263), and
    /// <c>cannot_dm_self</c> for the obvious case.</para></summary>
    Task<ServiceResult<ChatRoomView>> OpenAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);

    /// <summary>Accepted conversations, newest activity first. Archived ones are excluded unless
    /// <paramref name="archived"/> is true — the archive is a folder, not a deletion.</summary>
    Task<IReadOnlyList<DmRoomView>> ListAsync(Guid userId, bool archived = false, CancellationToken ct = default);

    /// <summary>Pending requests awaiting THIS user's decision. A request the caller sent themselves is
    /// not in here — it is their conversation, not their inbox.</summary>
    Task<IReadOnlyList<DmRoomView>> ListRequestsAsync(Guid userId, CancellationToken ct = default);

    Task<ServiceResult<bool>> RespondToRequestAsync(Guid roomId, Guid userId, bool accept, CancellationToken ct = default);

    Task<ServiceResult<bool>> SetArchivedAsync(Guid roomId, Guid userId, bool archived, CancellationToken ct = default);

    // Delivery gating is deliberately NOT here. Blocks and a declined request are re-checked inside
    // ChatService.SendMessageAsync, next to the room-state, mute and ban gates it already owns — one
    // place that decides whether a message may land, rather than two that can disagree.
}
