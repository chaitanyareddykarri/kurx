using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Chat;

/// <summary>
/// Direct messages (D-264).
///
/// <para>Deliberately thin. A DM room is a <see cref="ChatRoom"/>, so sending, history, attachments,
/// presence and read pointers are <see cref="IChatService"/>'s and stay there. What lives here is the
/// three things a 1:1 conversation has that an event room does not: a canonical pair, a request gate,
/// and a per-user archive.</para>
/// </summary>
public class DmService(KurxDbContext db, IChatService chat, IStorage storage) : IDmService
{
    /// <summary>The pair, ordered. Canonicalising here — and only here — is what lets the unique index
    /// do its job; a caller that ordered differently would create a second room for the same two people.</summary>
    private static (Guid Low, Guid High) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    public async Task<ServiceResult<ChatRoomView>> OpenAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        if (userId == otherUserId) return ServiceResult<ChatRoomView>.Fail("cannot_dm_self");
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == otherUserId, ct))
            return ServiceResult<ChatRoomView>.Fail("not_found");

        // Blocks cut both ways (D-263). Checked before anything is created, and again on delivery.
        if (await IsBlockedEitherWayAsync(userId, otherUserId, ct))
            return ServiceResult<ChatRoomView>.Fail("blocked");

        var (low, high) = Pair(userId, otherUserId);
        var existing = await db.ChatRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.DirectLowUserId == low && r.DirectHighUserId == high, ct);

        if (existing is null)
        {
            // Allies skip the request gate — they have already consented to each other once. Everyone
            // else lands Pending and notifies nobody until accepted; that is the spam control, and
            // without it an open DM endpoint is an abuse surface from the hour it ships.
            var areAllies = await db.AllyConnections.AsNoTracking()
                .AnyAsync(a => a.Status == AllyStatus.Accepted && a.UserLowId == low && a.UserHighId == high, ct);

            var room = new ChatRoom
            {
                EventId = null,
                Kind = ChatRoomKind.Direct,
                Status = ChatRoomStatus.Active,
                PostPolicy = ChatPostPolicy.Everyone,
                DirectLowUserId = low,
                DirectHighUserId = high,
                DmRequestState = areAllies ? DmRequestState.Accepted : DmRequestState.Pending,
                DmInitiatedBy = userId,
            };
            db.ChatRooms.Add(room);
            db.ChatMembers.AddRange(
                new ChatMember { RoomId = room.Id, UserId = low, Role = ChatMemberRole.Member },
                new ChatMember { RoomId = room.Id, UserId = high, Role = ChatMemberRole.Member });

            try
            {
                await db.SaveChangesAsync(ct);
                existing = room;
            }
            catch (DbUpdateException)
            {
                // Lost the race on the pair index — the winner's room is the room. This is the whole
                // reason the constraint exists: an application-level existence check cannot prevent
                // two rooms for one conversation, and the resulting split history is unrecoverable.
                db.ChangeTracker.Clear();
                existing = await db.ChatRooms.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.DirectLowUserId == low && r.DirectHighUserId == high, ct);
                if (existing is null) throw;
            }
        }

        // Reopening from the archive is implicit: the person you are writing to wants to see it again.
        await db.ChatMembers.Where(m => m.RoomId == existing.Id && m.UserId == userId && m.ArchivedAt != null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ArchivedAt, (DateTime?)null), ct);

        var view = await chat.GetRoomByIdAsync(existing.Id, userId, ct);
        return view.Ok
            ? ServiceResult<ChatRoomView>.Success(view.Value!)
            : ServiceResult<ChatRoomView>.Fail(view.Error ?? "not_found");
    }

    public async Task<IReadOnlyList<DmRoomView>> ListAsync(Guid userId, bool archived = false, CancellationToken ct = default)
        => await BuildAsync(userId,
            r => r.DmRequestState == DmRequestState.Accepted,
            archived,
            requestsOnly: false, ct);

    public async Task<IReadOnlyList<DmRoomView>> ListRequestsAsync(Guid userId, CancellationToken ct = default)
        // A request the caller SENT is their conversation, not their inbox — only the other party decides.
        => await BuildAsync(userId,
            r => r.DmRequestState == DmRequestState.Pending && r.DmInitiatedBy != userId,
            archived: false,
            requestsOnly: true, ct);

    public async Task<ServiceResult<bool>> RespondToRequestAsync(Guid roomId, Guid userId, bool accept, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.FirstOrDefaultAsync(r => r.Id == roomId && r.Kind == ChatRoomKind.Direct, ct);
        // A room the caller is not in does not exist to them (D-018).
        if (room is null || !await IsMemberAsync(roomId, userId, ct))
            return ServiceResult<bool>.Fail("not_found");

        // Only the recipient decides. The initiator "accepting" their own request would defeat the gate
        // entirely, which is the one thing this state exists to prevent.
        if (room.DmInitiatedBy == userId) return ServiceResult<bool>.Fail("not_recipient");
        if (room.DmRequestState != DmRequestState.Pending) return ServiceResult<bool>.Fail("already_answered");

        room.DmRequestState = accept ? DmRequestState.Accepted : DmRequestState.Declined;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>Delegates to <see cref="IChatService.SetArchivedAsync"/> (D-292). The behaviour is not
    /// DM-specific — it writes <c>ChatMember</c> and works on any room — so it moved to the service that
    /// owns that row. Kept on this interface so the existing <c>/v1/dm/{roomId}/archive</c> routes and
    /// their clients are unaffected.</summary>
    public Task<ServiceResult<bool>> SetArchivedAsync(Guid roomId, Guid userId, bool archived, CancellationToken ct = default)
        => chat.SetArchivedAsync(roomId, userId, archived, ct);

    // ── Helpers ────────────────────────────────────────────────────────────────

    private Task<bool> IsBlockedEitherWayAsync(Guid a, Guid b, CancellationToken ct)
        => db.UserBlocks.AsNoTracking().AnyAsync(
            x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a), ct);

    private Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct)
        => db.ChatMembers.AsNoTracking().AnyAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

    /// <summary>Builds the list in a fixed number of queries regardless of how many conversations the
    /// user has — the same set-based shape <c>GetMyChatsAsync</c> adopted in D-113 after the per-room
    /// version cost 3N round trips.</summary>
    private async Task<IReadOnlyList<DmRoomView>> BuildAsync(
        Guid userId, System.Linq.Expressions.Expression<Func<ChatRoom, bool>> statePredicate,
        bool archived, bool requestsOnly, CancellationToken ct)
    {
        var memberRooms = db.ChatMembers.AsNoTracking().Where(m => m.UserId == userId);
        memberRooms = archived
            ? memberRooms.Where(m => m.ArchivedAt != null)
            : memberRooms.Where(m => m.ArchivedAt == null);

        var rooms = await db.ChatRooms.AsNoTracking()
            .Where(r => r.Kind == ChatRoomKind.Direct)
            .Where(statePredicate)
            .Where(r => memberRooms.Any(m => m.RoomId == r.Id))
            // Blocked in either direction: the conversation disappears from both sides' lists rather
            // than sitting there un-openable.
            .Where(r => !db.UserBlocks.Any(x =>
                (x.BlockerId == r.DirectLowUserId && x.BlockedId == r.DirectHighUserId)
                || (x.BlockerId == r.DirectHighUserId && x.BlockedId == r.DirectLowUserId)))
            .ToListAsync(ct);
        if (rooms.Count == 0) return [];

        var roomIds = rooms.Select(r => r.Id).ToList();
        var otherIds = rooms
            .Select(r => r.DirectLowUserId == userId ? r.DirectHighUserId!.Value : r.DirectLowUserId!.Value)
            .Distinct().ToList();

        var others = await db.Users.AsNoTracking().Where(u => otherIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Username, u.AvatarKey })
            .ToDictionaryAsync(u => u.Id, ct);

        var members = await db.ChatMembers.AsNoTracking()
            .Where(m => m.UserId == userId && roomIds.Contains(m.RoomId))
            .ToDictionaryAsync(m => m.RoomId, ct);

        // D-293: the same hide filter the event-chat list applies, for the same reason — a conversation
        // whose last message this user hid must not preview it back to them.
        var messages = await db.ChatMessages.AsNoTracking()
            .Where(m => roomIds.Contains(m.RoomId) && !m.IsDeleted
                && !db.ChatMessageHides.Any(h => h.MessageId == m.Id && h.UserId == userId))
            .Select(m => new { m.RoomId, m.CreatedAt, m.Body })
            .ToListAsync(ct);
        var byRoom = messages.GroupBy(m => m.RoomId).ToDictionary(g => g.Key, g => g.OrderBy(m => m.CreatedAt).ToList());

        var markIds = members.Values.Where(m => m.LastReadMessageId != null)
            .Select(m => m.LastReadMessageId!.Value).ToList();
        var marks = await db.ChatMessages.AsNoTracking()
            .Where(m => markIds.Contains(m.Id))
            .Select(m => new { m.Id, m.CreatedAt })
            .ToDictionaryAsync(x => x.Id, x => x.CreatedAt, ct);

        var result = new List<DmRoomView>();
        foreach (var room in rooms)
        {
            var otherId = room.DirectLowUserId == userId ? room.DirectHighUserId!.Value : room.DirectLowUserId!.Value;
            if (!others.TryGetValue(otherId, out var other)) continue;
            members.TryGetValue(room.Id, out var me);

            byRoom.TryGetValue(room.Id, out var msgs);
            msgs ??= [];

            DateTime? readUpTo = me?.LastReadMessageId is not null
                && marks.TryGetValue(me.LastReadMessageId.Value, out var at) ? at : null;
            var unread = readUpTo is null ? msgs.Count : msgs.Count(m => m.CreatedAt > readUpTo.Value);

            result.Add(new DmRoomView(
                room.Id, otherId, other.Name, other.Username, other.AvatarKey,
                OtherAvatarUrl: await storage.PresignOrNullAsync(other.AvatarKey, ct),
                RequestState: (room.DmRequestState ?? DmRequestState.Accepted).ToString().ToLowerInvariant(),
                IsRequest: room.DmRequestState == DmRequestState.Pending && room.DmInitiatedBy != userId,
                Archived: me?.ArchivedAt is not null,
                LastMessagePreview: msgs.Count > 0 ? Preview(msgs[^1].Body) : null,
                UnreadCount: requestsOnly ? 0 : unread,
                LastActivity: msgs.Count > 0 ? msgs[^1].CreatedAt : room.CreatedAt,
                Pinned: me?.PinnedAt is not null,
                NotificationsMuted: me?.NotificationsMutedUntil > DateTime.UtcNow));
        }

        // D-295: pinned first, same rule as the event-chat list.
        return result.OrderByDescending(r => r.Pinned).ThenByDescending(r => r.LastActivity).ToList();
    }

    private static string Preview(string body)
        => body.Length <= 120 ? body : body[..120] + "…";
}
