using System.Text.RegularExpressions;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>
/// Fans a chat event out to the platform notification pipeline (D-107).
///
/// Runs as a Hangfire job rather than inline so that notification work can never delay or fail message
/// persistence — the message is already committed and broadcast by the time this starts. That is the
/// structural reason a push failure cannot affect chat delivery, rather than a try/catch around it.
///
/// Everything here goes through <see cref="INotificationService.NotifyAsync"/>: the in-app row, the
/// unread-count refresh, the SignalR push and the FCM fan-out with invalid-token cleanup are all
/// inherited. This job never touches <c>IPushSender</c> or the Devices table directly.
/// </summary>
public partial class ChatNotificationJob(
    KurxDbContext db,
    INotificationService notifications,
    IPresenceService presence,
    ILogger<ChatNotificationJob> log)
{
    private const int PreviewMaxChars = 120;

    // Usernames are lowercase public handles (User.Username). Bounded length keeps a pathological
    // body from producing thousands of candidate matches.
    [GeneratedRegex(@"@([a-z0-9_]{3,30})", RegexOptions.IgnoreCase)]
    private static partial Regex MentionPattern();

    /// <summary>Reserved handles that address the whole room instead of one person (D-292). Compared
    /// lowercase, because <see cref="MentionPattern"/> matches case-insensitively.
    ///
    /// <para>A user could in principle hold the username "all". That is why this check runs BEFORE the
    /// handle lookup and only for a host: for everyone else "@all" stays an ordinary handle and resolves
    /// to that account if it exists, exactly as it did before.</para></summary>
    private static readonly string[] RoomWideHandles = ["all", "everyone"];

    public async Task RunAsync(Guid messageId, CancellationToken ct)
    {
        var msg = await db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null || msg.IsDeleted || msg.SenderId is null) return;   // system messages notify via NotifyRoomLockedAsync

        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == msg.RoomId, ct);
        if (room is null) return;
        // An archived room sends nothing (D-122). Posting is already impossible there, but this job
        // is queued, so a message sent moments before the sweep can still land here afterwards.
        // Derived from the shared lifecycle rule (D-124), not the stored status — the sweep that
        // persists Archived may not have run yet, and a message queued moments before the archive
        // must not be delivered after it.
        var eventEndsAt = await db.Events.AsNoTracking()
            .Where(e => e.Id == room.EventId).Select(e => (DateTime?)e.EndsAt).FirstOrDefaultAsync(ct);
        if (eventEndsAt is not null
            && Domain.Entities.ChatLifecycle.EffectiveStatus(room.Status, eventEndsAt.Value, DateTime.UtcNow)
               == Domain.Enums.ChatRoomStatus.Archived) return;

        // A direct room has no event, so the event lookup below finds nothing — without this branch a
        // DM would silently notify nobody, which is the failure mode of bolting 1:1 onto an event-shaped
        // notifier (D-264). A DM that is still a Pending request notifies nobody ON PURPOSE: that is
        // the spam control, and it is the whole reason requests exist.
        var isDirect = room.Kind == Domain.Enums.ChatRoomKind.Direct;
        if (isDirect && room.DmRequestState != Domain.Enums.DmRequestState.Accepted) return;

        var ev = isDirect
            ? null
            : await db.Events.AsNoTracking()
                .Where(e => e.Id == room.EventId)
                .Select(e => new { e.Id, e.Title })
                .FirstOrDefaultAsync(ct);
        if (!isDirect && ev is null) return;

        var senderName = await db.Users.AsNoTracking()
            .Where(u => u.Id == msg.SenderId.Value).Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Someone";

        var senderIsHost = await db.ChatMembers.AsNoTracking()
            .AnyAsync(m => m.RoomId == msg.RoomId && m.UserId == msg.SenderId.Value
                        && m.Role == ChatMemberRole.Host, ct);

        var recipients = await EligibleRecipientsAsync(msg.RoomId, msg.SenderId.Value, ct);
        if (recipients.Count == 0) return;

        var mentioned = await ResolveMentionsAsync(msg.Body, recipients, senderIsHost, ct);
        var offline = await FilterToOfflineAsync(msg.RoomId, recipients, mentioned, ct);

        var preview = Preview(msg.Body);
        var data = new
        {
            notificationType = "chat",
            eventId = ev?.Id,
            roomId = msg.RoomId,
            messageId = msg.Id,
            senderId = msg.SenderId.Value,
            // A DM has no event to route through, so the deep link is the conversation itself.
            route = isDirect ? $"/chats/{msg.RoomId}" : $"/chats/{msg.RoomId}",
        };

        // A DM's title is who is talking to you; an event room's is which event.
        var title = ev?.Title ?? senderName;

        var sent = 0;
        foreach (var userId in offline)
        {
            var isMention = mentioned.Contains(userId);
            var kind = isDirect ? "dm_message"
                     : isMention ? "chat_mention"
                     : senderIsHost ? "chat_announcement"
                     : "chat_message";
            var body = isMention
                ? $"{senderName} mentioned you: {preview}"
                : isDirect ? preview
                : $"{senderName}: {preview}";

            try
            {
                await notifications.NotifyAsync(userId, kind, title, body, data, ct);
                sent++;
            }
            catch (Exception ex)
            {
                // One bad recipient must not abort the fan-out for everyone else. Per-device isolation
                // already lives inside NotifyAsync; this is the per-recipient equivalent.
                log.LogWarning(ex, "Chat notification failed for user {UserId}, message {MessageId}", userId, messageId);
            }
        }

        log.LogDebug("Chat message {MessageId}: notified {Sent}/{Eligible} eligible recipients", messageId, sent, recipients.Count);
    }

    /// <summary>Notifies a room once that it has gone read-only. Callers only invoke this on the
    /// transition into Locked (the lock itself is idempotent), so members cannot be told twice.</summary>
    public async Task NotifyRoomLockedAsync(Guid roomId, CancellationToken ct)
    {
        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return;

        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == room.EventId).Select(e => new { e.Id, e.Title }).FirstOrDefaultAsync(ct);
        if (ev is null) return;

        var recipients = await EligibleRecipientsAsync(roomId, senderId: null, ct);
        var data = new
        {
            notificationType = "chat",
            eventId = ev.Id,
            roomId,
            messageId = (Guid?)null,
            senderId = (Guid?)null,
        };

        foreach (var userId in recipients)
        {
            try
            {
                await notifications.NotifyAsync(userId, "chat_room_locked", ev.Title,
                    "This event's chat is now read-only.", data, ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Room-locked notification failed for user {UserId}, room {RoomId}", userId, roomId);
            }
        }
    }

    /// <summary>One query for the whole room — the eligibility rules are expressed in SQL rather than
    /// by loading members and filtering in memory, so recipient discovery stays O(1) queries.</summary>
    private async Task<List<Guid>> EligibleRecipientsAsync(Guid roomId, Guid? senderId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.ChatMembers.AsNoTracking()
            .Where(m => m.RoomId == roomId
                     && (senderId == null || m.UserId != senderId)   // never notify the sender
                     && !m.IsBanned                                   // banned members are not in the room
                     && (m.MutedUntil == null || m.MutedUntil <= now))
            .Select(m => m.UserId)
            .ToListAsync(ct);
    }

    /// <summary>Maps @handles in the body to user ids, restricted to people already established as
    /// eligible recipients — so a mention cannot be used to notify a non-member.</summary>
    private async Task<HashSet<Guid>> ResolveMentionsAsync(string body, List<Guid> recipients,
        bool senderIsHost, CancellationToken ct)
    {
        var handles = MentionPattern().Matches(body)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct().Take(20).ToList();
        if (handles.Count == 0) return [];

        // D-292 — "@all" / "@everyone" address the room. Host-only, matching the CanMentionAll flag that
        // ChatCapabilities has advertised since D-104 with nothing behind it: a host typing @all notified
        // NOBODY, because the handle simply failed to resolve to a user. The flag is now a promise the
        // server keeps.
        //
        // Mentions bypass the online filter below, so this pushes to every member of the room —
        // deliberately, since that is what the word means. It is host-gated and otherwise unbounded; a
        // per-room cooldown belongs here if an organiser ever uses it as a megaphone.
        if (senderIsHost && handles.Any(h => RoomWideHandles.Contains(h, StringComparer.Ordinal)))
            return recipients.ToHashSet();

        var ids = await db.Users.AsNoTracking()
            .Where(u => u.Username != null && handles.Contains(u.Username) && recipients.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>Drops recipients who currently have a live connection to this room — they already saw
    /// the message over SignalR, so pushing to them is duplicate noise. Mentions are always delivered.
    ///
    /// Uses <see cref="IPresenceService"/> (D-114) rather than reading Redis directly. That matters
    /// for correctness, not just tidiness: the old registry had no disconnect handling and a 12-hour
    /// TTL, so a user who closed the app still looked connected and **their pushes were suppressed
    /// for up to half a day**. Presence now releases on disconnect, so "offline" means offline.
    ///
    /// When presence is unavailable the online set is empty, so everyone is notified — over-notifying
    /// rather than silently dropping, which is the safer direction to fail.</summary>
    private async Task<List<Guid>> FilterToOfflineAsync(
        Guid roomId, List<Guid> recipients, HashSet<Guid> alwaysNotify, CancellationToken ct)
    {
        var online = await presence.OnlineUsersAsync(roomId, ct);   // one round trip for the room
        if (online.Count == 0) return recipients;
        return recipients.Where(u => alwaysNotify.Contains(u) || !online.Contains(u)).ToList();
    }

    /// <summary>Never ship a full body in a push payload — truncate on a word boundary where one is
    /// close enough to the limit to be worth it.</summary>
    private static string Preview(string body)
    {
        body = body.Trim();
        if (body.Length <= PreviewMaxChars) return body;
        var cut = body[..PreviewMaxChars];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > PreviewMaxChars - 20) cut = cut[..lastSpace];
        return cut.TrimEnd() + "…";
    }
}
