using Hangfire;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Chat;

public class ChatService(
    KurxDbContext db,
    IRealtimeBroadcaster broadcaster,
    IReportService reports,
    INotificationService notifications,
    IAuditWriter audit,
    IBackgroundJobClient jobs,
    IStorage storage,
    IFileScanner scanner,
    IPresenceService presence,
    IEventAuthority authority) : IChatService
{
    /// <summary>Read-only window between an event ending and its chat being archived (D-122).</summary>
    private const int ArchiveAfterEndDays = ChatLifecycle.ArchiveAfterEndDays;

    private const int MaxPinnedMessages = 3;

    /// <summary>D-296 pin windows. Seven days is the default because a pin is about the current week of an
    /// event; the 30-day ceiling exists so "pinned" never quietly becomes permanent.</summary>
    private static readonly TimeSpan DefaultPinDuration = TimeSpan.FromDays(7);
    private static readonly TimeSpan MinPinDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxPinDuration = TimeSpan.FromDays(30);

    private const int DeleteWindowMinutes = 15;
    private const int MaxAttachmentsPerMessage = 10;

    // ── Room lifecycle ─────────────────────────────────────────────────────────

    public async Task EnsureRoomExistsAsync(Guid eventId, CancellationToken ct = default)
    {
        if (!await db.ChatRooms.AnyAsync(r => r.EventId == eventId && r.Kind == ChatRoomKind.General, ct))
        {
            db.ChatRooms.Add(new ChatRoom { EventId = eventId, Kind = ChatRoomKind.General });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task AddMemberAsync(Guid roomId, Guid userId, string role, CancellationToken ct = default)
    {
        // An archived room's participant list is frozen (D-122). A late ticket transfer or a
        // replayed job must not quietly add someone to a conversation that is over.
        if (await IsArchivedAsync(roomId, ct)) return;

        var roleEnum = Enum.TryParse<ChatMemberRole>(role, true, out var r) ? r : ChatMemberRole.Member;
        var existing = await db.ChatMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        var isNew = existing is null;
        if (existing is null)
        {
            db.ChatMembers.Add(new ChatMember { RoomId = roomId, UserId = userId, Role = roleEnum });
        }
        else if (roleEnum == ChatMemberRole.Host && existing.Role != ChatMemberRole.Host)
        {
            existing.Role = ChatMemberRole.Host; // upgrade
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (isNew)
        {
            // Concurrent add lost the race against the unique (RoomId, UserId) index. The other writer's
            // row is equivalent, so converge on it rather than failing the caller's lifecycle operation.
            db.ChangeTracker.Clear();
            return;
        }

        // Only announce an actual join. Re-running a lifecycle hook (a republish, a retried webhook)
        // must not post a second "X joined" for a member who was already present.
        if (isNew) await MaybePostJoinMessageAsync(roomId, userId, ct);
    }

    public async Task AddMemberByEventAsync(Guid eventId, Guid userId, string role, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.FirstOrDefaultAsync(r => r.EventId == eventId && r.Kind == ChatRoomKind.General, ct);
        if (room is null) return;
        await AddMemberAsync(room.Id, userId, role, ct);
    }

    public async Task RemoveMemberIfNoTicketsAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var hasTicket = await db.Tickets.AnyAsync(t => t.EventId == eventId && t.UserId == userId && t.State != TicketState.Void, ct);
        if (hasTicket) return;
        var room = await db.ChatRooms.FirstOrDefaultAsync(r => r.EventId == eventId && r.Kind == ChatRoomKind.General, ct);
        if (room is null) return;
        // `!= Host`, NOT `== Member` (D-301). While the enum had two values those were the same
        // predicate, and the intent was always "remove anyone who is not a Host". Adding the Moderator
        // rung silently narrowed it: a refunded attendee who had been promoted to Moderator was no
        // longer removed from the room, and kept moderation authority over a conversation they were no
        // longer part of. Hosts stay protected, which is what the filter existed for.
        var removed = await db.ChatMembers
            .Where(m => m.RoomId == room.Id && m.UserId == userId && m.Role != ChatMemberRole.Host)
            .ExecuteDeleteAsync(ct);

        // D-294 — deleting the row stops the next REQUEST; it does nothing to a socket already in the
        // group. A refunded attendee kept receiving every message in the event chat until they happened
        // to disconnect. Ban has evicted since D-106; this path never did, and it is the one a refund
        // takes. Only when a row actually went, so a re-run of an idempotent job evicts nobody twice.
        if (removed > 0)
            await broadcaster.EvictFromChatAsync(room.Id, userId, "MemberRemoved", ct);
    }

    public async Task AddAcceptedStaffAsMembersAsync(Guid eventId, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Kind == ChatRoomKind.General, ct);
        if (room is null) return;

        var staffIds = await db.EventAssignments.AsNoTracking()
            .Where(a => a.EventId == eventId && a.Status == AssignmentStatus.Accepted)
            .Select(a => a.UserId).Distinct().ToListAsync(ct);

        // D-300 — Member, not Host. This was `AddAcceptedStaffAsHostsAsync`, and it made every accepted
        // EventAssignment a Host: Volunteer, Videographer, Media Team, Support Team — the whole free-text
        // assignment catalogue. Renamed rather than quietly changed, because the old name asserted the
        // behaviour that was wrong, and a caller reading it would have believed it.
        foreach (var uid in staffIds)
            await AddMemberAsync(room.Id, uid, "Member", ct);
    }

    public async Task RemoveStaffMemberAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Kind == ChatRoomKind.General, ct);
        if (room is null) return;

        var member = await db.ChatMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.UserId == userId, ct);
        if (member is null) return;

        // Host can be held two ways: by this staff assignment, or by standing on the event (seeded at
        // publish). Dropping the assignment must not strip the standing — so ask the authority rather
        // than the roster the seeding happened to leave behind (D-272).
        //
        // D-300 — the bar is Manager, not ModerateAudience (Staff). A Staff seat no longer confers chat
        // Host, so it must no longer protect one either; otherwise revoking an assignment from someone
        // holding a Staff seat would leave a Host row this model says should not exist.
        var access = await authority.ResolveAsync(userId, eventId, isAdmin: false, ct);
        if (access.Level >= EventAuthorityLevel.Manager) return;

        if (member.Role == ChatMemberRole.Host)
        {
            member.Role = ChatMemberRole.Member;
            await db.SaveChangesAsync(ct);
        }
        await RemoveMemberIfNoTicketsAsync(eventId, userId, ct);
    }

    /// <summary>D-304 — see <see cref="IChatService.SyncOrgAuthorityAsync"/>.</summary>
    public async Task SyncOrgAuthorityAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        // Every live room this organization represents. Archived rooms are excluded rather than iterated and
        // skipped: their participant list is frozen (D-122), and AddMemberAsync already refuses them, so
        // including them would be a no-op wearing the shape of work.
        var rooms = await db.ChatRooms.AsNoTracking()
            .Where(r => r.Kind == ChatRoomKind.General
                && r.EventId != null
                && r.Status != ChatRoomStatus.Archived
                && db.Events.Any(e => e.Id == r.EventId && e.RepresentingOrgId == orgId && e.DeletedAt == null))
            .Select(r => new { r.Id, EventId = r.EventId!.Value })
            .ToListAsync(ct);

        foreach (var room in rooms)
        {
            var before = await RoleInRoomAsync(room.Id, userId, ct);

            // Resolved live, per event, exactly as every request resolves it (D-015) — so a seat revoked a
            // moment ago stops conferring Host here too, and the event's own creator keeps it with no
            // organization seat at all (D-268). This is also what makes the operation idempotent: the answer
            // comes from committed state, never from the message that triggered it.
            var access = await authority.ResolveAsync(userId, room.EventId, isAdmin: false, ct);

            if (access.Level >= EventAuthorityLevel.Manager)
            {
                // Upgrade-only and insert-safe: never downgrades an existing row, and converges rather than
                // throwing when two dispatches race the unique (RoomId, UserId) index.
                await AddMemberAsync(room.Id, userId, "Host", ct);
            }
            else if (access.HasStanding)
            {
                // Two things, in this order, because the caller may be arriving OR stepping down.
                //
                // First: admit them. A Staff seat granted after publication is exactly what publish-time
                // seeding would have made a Member (D-300), and this is the only path left that can still do
                // it — without this the whole sub-Manager half of D-304 silently does nothing, which is what
                // the tests caught. Upgrade-only, so it never touches an existing Host or Moderator row.
                await AddMemberAsync(room.Id, userId, "Member", ct);

                // Then: withdraw the Host rung if they held it, keeping their place in the room. Deliberately
                // NOT RemoveStaffMemberAsync — that ends in RemoveMemberIfNoTicketsAsync, which tests only
                // for a TICKET, so a demoted Manager still holding a Staff seat would be evicted from a room
                // D-300 says they belong in.
                //
                // Conditional UPDATE on the Host rung, so a redelivery matches no rows and writes nothing,
                // and an explicit Moderator who never held org Host is left untouched.
                var demoted = await db.ChatMembers
                    .Where(m => m.RoomId == room.Id && m.UserId == userId && m.Role == ChatMemberRole.Host)
                    .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, ChatMemberRole.Member), ct);
                if (demoted > 0) db.ChangeTracker.Clear();   // ExecuteUpdate bypasses the tracker
            }
            else
            {
                // No standing left at all. Demotes and then drops them, re-checking authority itself, so it
                // stays safe for a user who never held Host.
                await RemoveStaffMemberAsync(room.EventId, userId, ct);
            }

            // Only when something actually moved. A redelivery resolves to the same role, changes nothing,
            // and must therefore produce no second broadcast — which is what keeps "retry the outbox" free.
            var after = await RoleInRoomAsync(room.Id, userId, ct);
            if (before != after)
                await broadcaster.BroadcastChatAsync(room.Id, "RoomUpdated",
                    new { userId, role = after?.ToString() }, null, ct);
        }
    }

    /// <summary>This user's role in one room, or null when they are not a member. Read fresh rather than from
    /// the tracker so it reflects what the convergence above actually wrote.</summary>
    private async Task<ChatMemberRole?> RoleInRoomAsync(Guid roomId, Guid userId, CancellationToken ct)
        => await db.ChatMembers.AsNoTracking()
            .Where(m => m.RoomId == roomId && m.UserId == userId)
            .Select(m => (ChatMemberRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    public async Task LockRoomForEventAsync(Guid eventId, string reason, CancellationToken ct = default)
    {
        var roomId = await db.ChatRooms.AsNoTracking()
            .Where(r => r.EventId == eventId && r.Kind == ChatRoomKind.General)
            .Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        if (roomId is null) return;

        // Only from Active: idempotent, and never un-archives.
        await TryAdvanceAsync(roomId.Value, ChatRoomStatus.Active, ChatRoomStatus.Locked, reason, ct);
    }

    /// <summary>
    /// Moves a room one step along the lifecycle. The single definition of a lifecycle transition —
    /// the sweep, event cancellation and the lazy first-access path all go through this (D-124).
    /// </summary>
    /// <remarks>
    /// Concurrency-safe by construction: the UPDATE carries the expected current status, so the
    /// database decides the winner and exactly one caller sees a row affected. Only that caller
    /// writes the system message, broadcasts and notifies — which is what stops two API instances
    /// sweeping simultaneously, or a cancellation racing the sweep, from telling members twice.
    ///
    /// Read-then-write would not be enough here: both callers would observe <c>Active</c>, and
    /// nothing on <c>ChatRoom</c> would make the second save fail.
    /// </remarks>
    /// <returns>True when this caller performed the transition.</returns>
    private async Task<bool> TryAdvanceAsync(
        Guid roomId, ChatRoomStatus from, ChatRoomStatus to, string reason, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = to == ChatRoomStatus.Locked
            ? await db.ChatRooms.Where(r => r.Id == roomId && r.Status == from)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(r => r.Status, to)
                    .SetProperty(r => r.LockedAt, now), ct)
            : await db.ChatRooms.Where(r => r.Id == roomId && r.Status == from)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, to), ct);

        if (rows == 0) return false;   // lost the race; the winner owns the side effects

        db.ChatMessages.Add(new ChatMessage
        {
            RoomId = roomId, Kind = ChatMessageKind.System, Body = reason,
        });
        await db.SaveChangesAsync(ct);

        var policy = await db.ChatRooms.AsNoTracking()
            .Where(r => r.Id == roomId).Select(r => r.PostPolicy).FirstOrDefaultAsync(ct);
        await broadcaster.BroadcastChatAsync(roomId, "RoomUpdated",
            new { PostPolicy = policy.ToString(), Status = to.ToString() }, null, ct);

        // Locking is worth telling people about; archiving a room nobody has used for a week is not
        // (D-122), and an archived room sends no notifications at all.
        if (to == ChatRoomStatus.Locked)
            jobs.Enqueue<Jobs.ChatNotificationJob>(j => j.NotifyRoomLockedAsync(roomId, CancellationToken.None));

        return true;
    }

    /// <summary>
    /// Persists whatever the clock has already decided, at first access after it happened (D-124).
    /// </summary>
    /// <remarks>
    /// Enforcement never depended on this — <see cref="EffectiveStatus"/> already refuses the send.
    /// What this adds is the *explanation*: the system message and the broadcast arrive when the
    /// first person opens the room, instead of up to an hour later when the sweep runs.
    ///
    /// Steps one state at a time so the lifecycle never skips <c>Locked</c>, and writes only on the
    /// transition itself — once persisted, stored and effective agree and this is a no-op.
    /// </remarks>
    private async Task PersistLifecycleAsync(
        Guid roomId, ChatRoomStatus stored, ChatRoomStatus effective, CancellationToken ct)
    {
        if (stored == effective) return;

        if (stored == ChatRoomStatus.Active)
        {
            await TryAdvanceAsync(roomId, ChatRoomStatus.Active, ChatRoomStatus.Locked,
                "This event has ended. Chat is now read-only.", ct);
            stored = ChatRoomStatus.Locked;
        }

        if (effective == ChatRoomStatus.Archived && stored == ChatRoomStatus.Locked)
        {
            await TryAdvanceAsync(roomId, ChatRoomStatus.Locked, ChatRoomStatus.Archived,
                "This chat has been archived. History remains available.", ct);
        }
    }

    public async Task AddEventHostsAsync(Guid eventId, CancellationToken ct = default)
    {
        await EnsureRoomExistsAsync(eventId, ct);
        var room = await db.ChatRooms.FirstOrDefaultAsync(r => r.EventId == eventId && r.Kind == ChatRoomKind.General, ct);
        if (room is null) return;

        var ev = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => new { e.RepresentingOrgId, e.CreatedBy }).FirstOrDefaultAsync(ct);
        if (ev is null) return;

        // The owner holds Host by owning the event, with no organization seat behind it (D-268). Seeding
        // this from membership was the last place a personally-represented event's creator depended on the
        // self-representation row to reach their own chat room (D-272).
        var hostIds = new List<Guid> { ev.CreatedBy };

        // Plus the seats D-300 recognises as Hosts: Owner, Manager and Representative — the manage-capable
        // rungs. ChatHostRoles is derived from the same LevelFor the per-caller resolve uses, so the two
        // cannot disagree. Finance is money authority over the organization and reaches neither list.
        hostIds.AddRange(await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == ev.RepresentingOrgId && EventAuthority.ChatHostRoles.Contains(m.Role))
            .Select(m => m.UserId).ToListAsync(ct));

        foreach (var uid in hostIds.Distinct())
            await AddMemberAsync(room.Id, uid, "Host", ct);

        // D-300 — a Staff seat joins the room, as a MEMBER. It used to arrive as a Host, because this
        // seeded from ModeratorRoles (Staff and above): holding an operational seat silently conferred the
        // power to ban attendees and delete anyone's messages.
        var staffIds = await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == ev.RepresentingOrgId
                && EventAuthority.ModeratorRoles.Contains(m.Role)
                && !EventAuthority.ChatHostRoles.Contains(m.Role))
            .Select(m => m.UserId).ToListAsync(ct);

        foreach (var uid in staffIds.Except(hostIds))
            await AddMemberAsync(room.Id, uid, "Member", ct);
    }

    // ── REST API ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ChatRoomView>> GetRoomAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        // D-104: rooms are keyed (EventId, Kind); only General is created today.
        var room = await db.ChatRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Kind == ChatRoomKind.General, ct);
        if (room is null) return ServiceResult<ChatRoomView>.Fail("not_found");
        return await GetRoomByIdAsync(room.Id, userId, ct);
    }

    /// <summary>The same view, addressed by room id rather than by event (D-264). Direct rooms have no
    /// event to look up, so they need this door — and event chat now enters through it too, because two
    /// builders for one view is two chances for the capability flags to disagree.</summary>
    public async Task<ServiceResult<ChatRoomView>> GetRoomByIdAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return ServiceResult<ChatRoomView>.Fail("not_found");
        var member = await db.ChatMembers.AsNoTracking().FirstOrDefaultAsync(m => m.RoomId == room.Id && m.UserId == userId, ct);
        if (member is null) return ServiceResult<ChatRoomView>.Fail("forbidden");

        var unread = await UnreadCountAsync(room.Id, userId, member.LastReadMessageId, ct);

        // D-293: hidden applies to pins as well. A pin is room-level, but "hide this for me" has to
        // mean everywhere, or the message the reader removed reappears at the top of the room.
        var pinned = await VisibleTo(room.Id, userId)
            // D-296: an expired pin drops out here with no sweeper involved — see ChatMessage.PinnedUntil.
            .Where(m => m.IsPinned && !m.IsDeleted && (m.PinnedUntil == null || m.PinnedUntil > DateTime.UtcNow))
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(MaxPinnedMessages)
            .ToListAsync(ct);

        var senderIds = pinned.Where(m => m.SenderId.HasValue).Select(m => m.SenderId!.Value).Distinct().ToList();
        var senderNames = await db.Users.Where(u => senderIds.Contains(u.Id)).Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var memberRoles = await db.ChatMembers.Where(m => m.RoomId == room.Id && senderIds.Contains(m.UserId)).Select(m => new { m.UserId, m.Role }).ToDictionaryAsync(m => m.UserId, m => m.Role, ct);

        var pinnedAttachments = await LoadAttachmentsAsync(pinned.Select(m => m.Id).ToList(), ct);
        // One Redis round trip for the whole roster, not one lookup per member.
        var online = await presence.OnlineUsersAsync(room.Id, ct);

        // Reported as effective, not stored: a room whose event ended two minutes ago is read-only
        // now, and saying "Active" until the next sweep would make the client offer a composer the
        // server would then refuse.
        var effective = await EffectiveStatusAsync(room.Id, ct) ?? room.Status;

        // First access after the event ended is where the explanation gets written: the system
        // message and broadcast land now rather than whenever the sweep next runs. A no-op once
        // persisted, and safe against concurrent openers because the transition is a compare-and-set.
        await PersistLifecycleAsync(room.Id, room.Status, effective, ct);

        return ServiceResult<ChatRoomView>.Success(new(
            room.Id, room.Kind.ToString(), effective.ToString(), room.PostPolicy.ToString(),
            member.Role.ToString(), member.MutedUntil, unread, Capabilities(room, member, effective),
            pinnedAttachments is null
                ? pinned.Select(m => ToMessageView(m, senderNames, memberRoles)).ToList()
                : pinned.Select(m => ToMessageView(m, senderNames, memberRoles, pinnedAttachments)).ToList(),
            online.ToList(),
            presence.IsEnabled,
            // D-292. Null for event rooms, so the field is meaningful only where a request gate exists.
            room.DmRequestState?.ToString().ToLowerInvariant(),
            room.DmInitiatedBy));
    }

    /// <summary>D-104: permissions are resolved once, server-side, so neither client re-derives them
    /// from role + policy. A new room mode changes only this method.</summary>
    private static ChatCapabilities Capabilities(ChatRoom room, ChatMember member, ChatRoomStatus effective)
    {
        var active = effective == ChatRoomStatus.Active;
        // Archived keeps history readable but freezes everything else, including participant changes
        // (D-122). Moderation itself survives, because a report filed on the last day of the
        // read-only window still has to be actionable afterwards.
        var archived = effective == ChatRoomStatus.Archived;
        var isHost = member.Role == ChatMemberRole.Host;
        // D-301 — Host OR Moderator. Everything below that used `isHost` for a *moderation* decision now
        // uses this; the flags that stay `isHost` are the room-level ones a Moderator must not reach.
        var canModerate = CanModerate(member.Role);
        var silenced = member.IsBanned || (member.MutedUntil.HasValue && member.MutedUntil > DateTime.UtcNow);
        // D-292: a declined request refuses every send (see SendMessageAsync), so it must not advertise
        // CanPost. Without this the client rendered a working composer over a room that rejected every
        // message with `dm_declined` — the user typed, sent, and got an error with nothing explaining why.
        var dmDeclined = room.Kind == ChatRoomKind.Direct && room.DmRequestState == DmRequestState.Declined;
        var canPost = active && !silenced && !dmDeclined && (room.PostPolicy != ChatPostPolicy.HostsOnly || isHost);

        return new ChatCapabilities(
            CanPost: canPost,
            CanReply: canPost,
            // Uploading is posting: the same room state, mute, ban and policy gates apply. There is
            // deliberately no separate upload permission — a member who may not post may not upload.
            CanUpload: canPost,
            CanPin: active && canModerate,
            // A moderator can still remove an offending message from an archived room; an ordinary member
            // can only delete their own, and only while the room is live.
            CanDelete: (active && !member.IsBanned) || (canModerate && !archived),
            CanModerate: canModerate,
            // Mention-all stays Host: it is a broadcast to everyone, not a moderation act.
            CanMentionAll: isHost,
            CanManageRoom: isHost,
            CanManageModerators: isHost,
            MyRole: member.Role.ToString());
    }

    /// <summary>
    /// The room's real state: the stored status, advanced by anything the clock has already decided
    /// (D-123).
    /// </summary>
    /// <remarks>
    /// An event ending is not an action anybody takes — it is a timestamp passing — so there is no
    /// transition to hook. Persisting the state is the hourly sweep's job, but waiting for it would
    /// leave a room writable for up to an hour after its event finished. Deriving the state here
    /// closes that window to zero while keeping one definition: the sweep calls this too, so the
    /// persisted status can never disagree with the enforced one.
    ///
    /// Only ever moves forward. A room locked early by cancellation stays locked even though its
    /// event has not ended yet.
    /// </remarks>
    private static ChatRoomStatus EffectiveStatus(ChatRoomStatus stored, DateTime eventEndsAt, DateTime now) =>
        ChatLifecycle.EffectiveStatus(stored, eventEndsAt, now);

    /// <summary>Effective status for one room, resolving the event it belongs to.</summary>
    private async Task<ChatRoomStatus?> EffectiveStatusAsync(Guid roomId, CancellationToken ct)
    {
        var row = await db.ChatRooms.AsNoTracking()
            .Where(r => r.Id == roomId)
            .Join(db.Events, r => r.EventId, e => e.Id, (r, e) => new { r.Status, e.EndsAt })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : EffectiveStatus(row.Status, row.EndsAt, DateTime.UtcNow);
    }

    private async Task<bool> IsArchivedAsync(Guid roomId, CancellationToken ct) =>
        await EffectiveStatusAsync(roomId, ct) == ChatRoomStatus.Archived;

    /// <summary>Messages in a room as ONE member sees them (D-293): everything except the ones they hid.
    ///
    /// <para>Every per-member read goes through here — history, unread counts, previews, pins — so a
    /// "delete for me" cannot be complete on one surface and leak on another. Translates to a
    /// NOT EXISTS, served by the (MessageId, UserId) unique index.</para></summary>
    /// <para><c>AsNoTracking</c> because every caller here only reads. It also keeps these reads honest
    /// next to the conditional <c>ExecuteUpdateAsync</c> writes this service uses: those bypass the
    /// change tracker, so a tracked read taken earlier in the same scope would keep serving the value
    /// from before the update — a pin or a mute that was just changed reading back as unchanged.</para>
    private IQueryable<ChatMessage> VisibleTo(Guid roomId, Guid userId) =>
        db.ChatMessages.AsNoTracking().Where(m => m.RoomId == roomId
            && !db.ChatMessageHides.Any(h => h.MessageId == m.Id && h.UserId == userId));

    /// <summary>Unread is counted from the authoritative read pointer (D-104), never from LastReadAt.
    /// A null or dangling pointer means "never read" — count everything.
    ///
    /// <para>Hidden messages are excluded (D-293): a message the reader chose to remove from their own
    /// view must not keep their badge lit.</para></summary>
    private async Task<int> UnreadCountAsync(Guid roomId, Guid userId, Guid? lastReadMessageId, CancellationToken ct)
    {
        var q = VisibleTo(roomId, userId).Where(m => !m.IsDeleted);
        if (lastReadMessageId is null) return await q.CountAsync(ct);

        var mark = await db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == lastReadMessageId.Value)
            .Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync(ct);
        if (mark is null) return await q.CountAsync(ct);

        return await q.CountAsync(
            m => EF.Functions.GreaterThan(
                ValueTuple.Create(m.CreatedAt, m.Id),
                ValueTuple.Create(mark.CreatedAt, mark.Id)), ct);
    }

    public async Task<ServiceResult<ChatMessagePage>> GetMessagesAsync(Guid roomId, Guid userId, string? before, string? after, int limit, CancellationToken ct = default)
    {
        var member = await db.ChatMembers.AsNoTracking().FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<ChatMessagePage>.Fail("forbidden");
        if (member.IsBanned) return ServiceResult<ChatMessagePage>.Fail("banned");
        if (before is not null && after is not null) return ServiceResult<ChatMessagePage>.Fail("cursor_conflict");

        limit = Math.Clamp(limit, 1, 50);
        // D-293: the caller's own hidden messages are absent from their history. Applied to the base
        // query so it composes with the keyset predicates below rather than filtering a fetched page —
        // filtering after Take would silently return short pages and break "load more".
        var q = VisibleTo(roomId, userId);

        // Keyset pagination on the (CreatedAt, Id) pair (D-104). Ordering on CreatedAt alone is not a
        // total order — DateTime.UtcNow has ~15ms resolution, so same-tick messages were previously
        // skipped or duplicated across pages. Npgsql's row-wise comparison maps to PostgreSQL
        // (a, b) > (c, d), which the (RoomId, CreatedAt, Id) index serves directly.
        var pivot = await ResolveCursorAsync(before ?? after, ct);
        if ((before ?? after) is not null && pivot is null)
            return ServiceResult<ChatMessagePage>.Fail("invalid_cursor");

        List<ChatMessage> msgs;
        if (after is not null)
        {
            // Forward (delta sync): oldest-first from the cursor, then flipped so the page always
            // reads newest-first like every other page.
            msgs = await q.Where(m => EF.Functions.GreaterThan(
                        ValueTuple.Create(m.CreatedAt, m.Id),
                        ValueTuple.Create(pivot!.Value.CreatedAt, pivot.Value.Id)))
                .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
                .Take(limit).ToListAsync(ct);
            msgs.Reverse();
        }
        else
        {
            if (pivot is not null)
                q = q.Where(m => EF.Functions.GreaterThan(
                    ValueTuple.Create(pivot.Value.CreatedAt, pivot.Value.Id),
                    ValueTuple.Create(m.CreatedAt, m.Id)));
            msgs = await q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Take(limit).ToListAsync(ct);
        }

        var senderIds = msgs.Where(m => m.SenderId.HasValue).Select(m => m.SenderId!.Value).Distinct().ToList();
        var senderNames = await db.Users.Where(u => senderIds.Contains(u.Id)).Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var memberRoles = await db.ChatMembers.Where(m => m.RoomId == roomId && senderIds.Contains(m.UserId)).Select(m => new { m.UserId, m.Role }).ToDictionaryAsync(m => m.UserId, m => m.Role, ct);

        var attachments = await LoadAttachmentsAsync(msgs.Select(m => m.Id).ToList(), ct);
        // D-295: both batched for the whole page. Loading either per message would put this read back
        // at O(page size) round trips, which is exactly what D-113 removed from the room lists.
        var reactions = await ReactionsForAsync(msgs.Select(m => m.Id).ToList(), userId, ct);
        var forwardNames = await ForwardSenderNamesAsync(msgs, ct);
        var views = msgs.Select(m => ToMessageView(m, senderNames, memberRoles, attachments, reactions, forwardNames)).ToList();
        // Cursors are the boundary message ids, opaque to clients (D-104).
        return ServiceResult<ChatMessagePage>.Success(new ChatMessagePage(
            views,
            OlderCursor: views.Count > 0 ? views[^1].Id.ToString() : null,
            NewerCursor: views.Count > 0 ? views[0].Id.ToString() : null));
    }

    private async Task<(DateTime CreatedAt, Guid Id)?> ResolveCursorAsync(string? cursor, CancellationToken ct)
    {
        if (cursor is null || !Guid.TryParse(cursor, out var id)) return null;
        var row = await db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == id).Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : (row.CreatedAt, row.Id);
    }

    public async Task<ServiceResult<ChatMessageView>> SendMessageAsync(Guid roomId, Guid senderId, string body, Guid? replyToId, Guid? clientMessageId, IReadOnlyList<Guid>? attachmentIds = null, ChatLinkPreviewInput? linkPreview = null, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return ServiceResult<ChatMessageView>.Fail("not_found");

        var member = await db.ChatMembers.AsNoTracking().FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == senderId, ct);
        if (member is null) return ServiceResult<ChatMessageView>.Fail("forbidden");

        // Idempotency is checked before the state gates (D-104): a retry of a message that already
        // landed must succeed even if the room has since locked or the sender has since been muted,
        // otherwise an offline outbox flush reports spurious failures for messages that were accepted.
        if (clientMessageId.HasValue)
        {
            var existing = await db.ChatMessages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.RoomId == roomId && m.ClientMessageId == clientMessageId.Value, ct);
            if (existing is not null)
                return ServiceResult<ChatMessageView>.Success(await ViewWithSenderAsync(existing, ct));
        }

        // Anything but Active refuses. Naming the state matters: "archived" and "read-only" are
        // different answers to "why can I not post", and the client shows the reason verbatim.
        // Effective, not stored: an event that ended a minute ago is closed now, not at the next sweep.
        var effective = await EffectiveStatusAsync(roomId, ct) ?? room.Status;
        if (effective == ChatRoomStatus.Archived) return ServiceResult<ChatMessageView>.Fail("room_archived");
        if (effective != ChatRoomStatus.Active) return ServiceResult<ChatMessageView>.Fail("room_locked");
        if (member.IsBanned) return ServiceResult<ChatMessageView>.Fail("banned");
        if (member.MutedUntil.HasValue && member.MutedUntil > DateTime.UtcNow) return ServiceResult<ChatMessageView>.Fail("muted");
        if (room.PostPolicy == ChatPostPolicy.HostsOnly && member.Role != ChatMemberRole.Host) return ServiceResult<ChatMessageView>.Fail("hosts_only");

        // Direct rooms carry two gates event rooms do not (D-264): a block in either direction, and a
        // request the recipient declined. Checked live here rather than trusted from room creation —
        // a block has to stop the very next message, not the next session.
        if (room.Kind == ChatRoomKind.Direct)
        {
            if (room.DmRequestState == DmRequestState.Declined) return ServiceResult<ChatMessageView>.Fail("dm_declined");
            if (room.DirectLowUserId is { } low && room.DirectHighUserId is { } high
                && await db.UserBlocks.AsNoTracking().AnyAsync(
                    x => (x.BlockerId == low && x.BlockedId == high) || (x.BlockerId == high && x.BlockedId == low), ct))
                return ServiceResult<ChatMessageView>.Fail("blocked");

            // D-292 — the recipient replying IS accepting the request, the rule every messenger uses.
            //
            // Without this the room stayed Pending forever, and Pending is exactly the state
            // ChatNotificationJob refuses to notify on: the recipient answered, the sender was never
            // told, and the conversation died silently with both people believing they had sent it.
            // The room also never left the Requests tab, because ListAsync only shows Accepted.
            //
            // Conditional so it claims the transition once: a burst of replies accepts on the first and
            // no-ops on the rest. Only the recipient can accept — the initiator sending again is just
            // another unanswered message, which is what keeps the gate a gate.
            if (room.DmRequestState == DmRequestState.Pending && senderId != room.DmInitiatedBy)
                await db.ChatRooms
                    .Where(r => r.Id == roomId && r.DmRequestState == DmRequestState.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.DmRequestState, DmRequestState.Accepted), ct);
        }
        // A message carrying files may have an empty body — the attachment IS the content.
        var hasAttachments = attachmentIds is { Count: > 0 };
        if (string.IsNullOrWhiteSpace(body) && !hasAttachments) return ServiceResult<ChatMessageView>.Fail("body_required");
        if (body.Length > 1000) return ServiceResult<ChatMessageView>.Fail("body_too_long");

        List<ChatAttachment> claimed = [];
        if (hasAttachments)
        {
            if (!Capabilities(room, member, effective).CanUpload) return ServiceResult<ChatMessageView>.Fail("upload_not_allowed");
            if (attachmentIds!.Count > MaxAttachmentsPerMessage) return ServiceResult<ChatMessageView>.Fail("too_many_attachments");

            // Only this sender's own unclaimed uploads in this room. That triple check is what stops
            // a caller attaching someone else's file, a file from another room, or re-attaching one
            // that already belongs to a message.
            claimed = await db.ChatAttachments
                .Where(a => attachmentIds.Contains(a.Id)
                         && a.RoomId == roomId
                         && a.UploadedBy == senderId
                         && a.MessageId == null
                         && a.DeletedAt == null)
                .ToListAsync(ct);

            if (claimed.Count != attachmentIds.Count) return ServiceResult<ChatMessageView>.Fail("invalid_attachment");
        }

        // D-295: only an absolute http(s) URL is stored. Anything else — a relative path, javascript:,
        // data: — is dropped rather than rejected: the message itself is fine, and a bad card is not a
        // reason to refuse someone's words. The host every client renders is derived from this on read.
        var link = linkPreview is { } lp
            && Uri.TryCreate(lp.Url, UriKind.Absolute, out var linkUri)
            && (linkUri.Scheme == Uri.UriSchemeHttp || linkUri.Scheme == Uri.UriSchemeHttps)
                ? lp : null;

        var msg = new ChatMessage
        {
            RoomId = roomId, SenderId = senderId, Kind = ChatMessageKind.Text,
            Body = body, ReplyToMessageId = replyToId, ClientMessageId = clientMessageId,
            LinkUrl = link?.Url,
            LinkTitle = Truncate(link?.Title, 200),
            LinkDescription = Truncate(link?.Description, 500),
            LinkImageUrl = link?.ImageUrl,
        };
        db.ChatMessages.Add(msg);
        // Claimed in the SAME transaction as the message, so a failure cannot leave an attachment
        // pointing at a message that does not exist.
        foreach (var a in claimed) a.MessageId = msg.Id;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (clientMessageId.HasValue)
        {
            // Concurrent retry of the same clientMessageId lost the race against the unique index.
            // The winner's row is the correct answer, so return it rather than surfacing a conflict.
            db.Entry(msg).State = EntityState.Detached;
            var winner = await db.ChatMessages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.RoomId == roomId && m.ClientMessageId == clientMessageId.Value, ct);
            if (winner is null) throw;
            return ServiceResult<ChatMessageView>.Success(await ViewWithSenderAsync(winner, ct));
        }

        var sender = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == senderId, ct);
        var memberRoles = new Dictionary<Guid, ChatMemberRole> { [senderId] = member.Role };
        var senderNames = new Dictionary<Guid, string> { [senderId] = sender?.Name ?? "" };
        // The claimed files must be on the response and on the broadcast, or the sender's own
        // optimistic row would reconcile onto a message that appears to have no attachments.
        var sentAttachments = claimed.Count == 0 ? null : await LoadAttachmentsAsync([msg.Id], ct);
        var view = ToMessageView(msg, senderNames, memberRoles, sentAttachments);

        await broadcaster.BroadcastChatAsync(roomId, "MessageReceived", view, msg.Id, ct);

        // D-107: notification fan-out runs out-of-band, after the message is committed and broadcast.
        // Enqueuing rather than awaiting is what structurally guarantees a push failure can neither
        // delay the sender nor roll back a persisted message.
        jobs.Enqueue<Jobs.ChatNotificationJob>(j => j.RunAsync(msg.Id, CancellationToken.None));

        return ServiceResult<ChatMessageView>.Success(view);
    }

    // ── Attachments (D-110) ────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<AttachmentUploadTicket>> PresignAttachmentAsync(
        Guid roomId, Guid userId, string fileName, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return ServiceResult<AttachmentUploadTicket>.Fail("not_found");

        var member = await db.ChatMembers.AsNoTracking().FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<AttachmentUploadTicket>.Fail("forbidden");
        var presignEffective = await EffectiveStatusAsync(roomId, ct) ?? room.Status;
        if (!Capabilities(room, member, presignEffective).CanUpload) return ServiceResult<AttachmentUploadTicket>.Fail("upload_not_allowed");

        // Cheap pre-checks only. Everything here is client-declared, so the same checks run again on
        // confirm against the real bytes — this pass exists to fail fast, not to be trusted.
        var safeName = AttachmentPolicy.SanitizeFileName(fileName);
        if (!AttachmentPolicy.IsAllowedContentType(contentType)) return ServiceResult<AttachmentUploadTicket>.Fail("unsupported_file_type");
        if (!AttachmentPolicy.ExtensionMatches(contentType, safeName)) return ServiceResult<AttachmentUploadTicket>.Fail("extension_mismatch");
        if (sizeBytes <= 0 || sizeBytes > AttachmentPolicy.MaxBytes) return ServiceResult<AttachmentUploadTicket>.Fail("file_too_large");

        // Server-generated key, with the sanitized filename as the final segment.
        //
        // The name has to live somewhere: confirm receives only a storage key, so without this the
        // original filename is lost and every attachment renders as a GUID. Carrying it in the key
        // keeps the confirm contract unchanged. The GUID segment still guarantees uniqueness, and
        // SanitizeFileName has already stripped every path character, so the client cannot influence
        // where the bytes land.
        var key = $"chat/{roomId}/{Guid.CreateVersion7():N}/{safeName}";
        var upload = await storage.PresignPutAsync(key, contentType, AttachmentPolicy.MaxBytes, ct);
        return ServiceResult<AttachmentUploadTicket>.Success(
            new AttachmentUploadTicket(upload.Key, upload.Url, upload.Headers));
    }

    public async Task<ServiceResult<ChatAttachmentView>> ConfirmAttachmentAsync(
        Guid roomId, Guid userId, string storageKey, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return ServiceResult<ChatAttachmentView>.Fail("not_found");

        var member = await db.ChatMembers.AsNoTracking().FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<ChatAttachmentView>.Fail("forbidden");
        var confirmEffective = await EffectiveStatusAsync(roomId, ct) ?? room.Status;
        if (!Capabilities(room, member, confirmEffective).CanUpload) return ServiceResult<ChatAttachmentView>.Fail("upload_not_allowed");

        // The key must be one this room could have issued. Without this a member of any room could
        // confirm an object belonging to another room by guessing its key.
        if (!storageKey.StartsWith($"chat/{roomId}/", StringComparison.Ordinal))
            return ServiceResult<ChatAttachmentView>.Fail("invalid_storage_key");

        // Idempotent: a retried confirm returns the row the first one created.
        var existing = await db.ChatAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.StorageKey == storageKey, ct);
        if (existing is not null)
        {
            if (existing.UploadedBy != userId) return ServiceResult<ChatAttachmentView>.Fail("forbidden");
            return ServiceResult<ChatAttachmentView>.Success(await ToAttachmentViewAsync(existing, ct));
        }

        if (!await storage.ExistsAsync(storageKey, ct)) return ServiceResult<ChatAttachmentView>.Fail("upload_not_found");

        // The authoritative pass: the first moment the server can look at the actual bytes. The
        // extra read is deliberate — correctness and dimensions beat saving one fetch.
        var bytes = await storage.GetAsync(storageKey, ct);
        // No declared type is available here and none is needed: the early return above proves no row
        // exists yet, and the presign ticket is not persisted. The bytes are authoritative anyway —
        // InspectAgainstAllowList derives the real type from them.
        var inspected = InspectAgainstAllowList(bytes, declaredContentType: null);
        if (inspected is null) return ServiceResult<ChatAttachmentView>.Fail("unsupported_file_type");

        // Always scanned, even when the configured scanner is a no-op — that is what keeps this call
        // site stable when a real scanner is wired.
        var verdict = await scanner.ScanAsync(storageKey, ct);
        if (verdict != FileScanResult.Clean)
        {
            audit.Write(new AuditEvent("chat.attachment_rejected", "chat_attachments", Guid.Empty,
                ActorId: userId,
                Before: null,
                After: new { storage_key = storageKey, verdict = verdict.ToString(), room_id = roomId }));
            await db.SaveChangesAsync(ct);

            // Infected or unverifiable content must never become reachable. Removing it now also
            // means the sweep has nothing to find later.
            await TryDeleteObjectAsync(storageKey, ct);
            return ServiceResult<ChatAttachmentView>.Fail(
                verdict == FileScanResult.Infected ? "file_infected" : "scan_unavailable");
        }

        var attachment = new ChatAttachment
        {
            RoomId = roomId,
            StorageKey = storageKey,
            FileName = AttachmentPolicy.SanitizeFileName(Path.GetFileName(storageKey)),
            ContentType = inspected.ContentType,
            SizeBytes = inspected.SizeBytes,
            Width = inspected.Width,
            Height = inspected.Height,
            UploadedBy = userId,
        };
        db.ChatAttachments.Add(attachment);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the race against the unique StorageKey index — the winner's row is equivalent.
            db.ChangeTracker.Clear();
            var winner = await db.ChatAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.StorageKey == storageKey, ct);
            if (winner is null) throw;
            return ServiceResult<ChatAttachmentView>.Success(await ToAttachmentViewAsync(winner, ct));
        }

        return ServiceResult<ChatAttachmentView>.Success(await ToAttachmentViewAsync(attachment, ct));
    }

    public async Task<ServiceResult<string>> GetAttachmentUrlAsync(Guid attachmentId, Guid userId, CancellationToken ct = default)
    {
        var attachment = await db.ChatAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment is null || attachment.DeletedAt is not null) return ServiceResult<string>.Fail("not_found");

        // Room membership is re-checked here, not inherited from whenever the message was fetched:
        // a removed or banned member must stop being able to pull the file.
        var member = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == attachment.RoomId && m.UserId == userId, ct);
        if (member is null || member.IsBanned) return ServiceResult<string>.Fail("forbidden");

        return ServiceResult<string>.Success(
            await storage.PresignGetAsync(attachment.StorageKey, TimeSpan.FromMinutes(15), ct));
    }

    /// <summary>Re-derives the true content type from the bytes. The declared type is only a
    /// starting guess; a file that matches nothing on the allow-list is refused outright.</summary>
    private static InspectedFile? InspectAgainstAllowList(byte[] bytes, string? declaredContentType)
    {
        if (declaredContentType is not null)
        {
            var direct = AttachmentPolicy.Inspect(bytes, declaredContentType);
            if (direct is not null) return direct;
        }
        foreach (var candidate in AttachmentPolicy.AllowedContentTypes)
        {
            var match = AttachmentPolicy.Inspect(bytes, candidate);
            if (match is not null) return match;
        }
        return null;
    }

    private async Task<ChatAttachmentView> ToAttachmentViewAsync(ChatAttachment a, CancellationToken ct)
        => new(a.Id, await storage.PresignGetAsync(a.StorageKey, TimeSpan.FromMinutes(15), ct),
            a.FileName, a.ContentType, a.SizeBytes, a.Width, a.Height);

    private async Task TryDeleteObjectAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            if (storage is Providers.LocalDiskStorage disk) await disk.DeleteAsync(storageKey, ct);
            // Other providers gain deletion when they are built; the row stays marked so a later
            // sweep can still remove the object rather than losing track of it.
        }
        catch
        {
            // Never fail a user-facing operation because cleanup could not reach storage.
        }
    }

    public async Task<ServiceResult<bool>> MarkReadAsync(Guid roomId, Guid userId, Guid lastReadMessageId, CancellationToken ct = default)
    {
        var member = await db.ChatMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<bool>.Fail("forbidden");

        // The pointer must name a message in this room — otherwise a client could park it on an
        // unrelated id and silently corrupt its own unread count (D-104).
        var mark = await db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == lastReadMessageId && m.RoomId == roomId)
            .Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync(ct);
        if (mark is null) return ServiceResult<bool>.Fail("not_found");

        // Monotonic: read position only ever moves forward, so out-of-order marks from a second
        // device cannot rewind it. This is what makes cross-device merge a plain max().
        var current = member.LastReadMessageId is null ? null : await db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == member.LastReadMessageId.Value)
            .Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync(ct);

        // Monotonic: only a forward move writes, so a duplicate or out-of-order mark from a second
        // device costs one read and no write at all (D-114). That is the batching — the pointer is
        // room-level, so there is never a per-message write to coalesce.
        if (current is null || (mark.CreatedAt, mark.Id).CompareTo((current.CreatedAt, current.Id)) > 0)
        {
            member.LastReadMessageId = mark.Id;
            member.LastReadAt = DateTime.UtcNow;   // analytics only (D-104)
            await db.SaveChangesAsync(ct);

            // Read receipts reuse the existing persisted pointer entirely — no new table, no new
            // column, and no dependency on presence, so they keep working when presence is off.
            await broadcaster.BroadcastChatAsync(roomId, "ReadReceiptChanged",
                new { userId, lastReadMessageId = mark.Id }, mark.Id, ct);
        }
        return ServiceResult<bool>.Success(true);
    }

    private async Task<ChatMessageView> ViewWithSenderAsync(ChatMessage msg, CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();
        var roles = new Dictionary<Guid, ChatMemberRole>();
        if (msg.SenderId.HasValue)
        {
            var name = await db.Users.AsNoTracking().Where(u => u.Id == msg.SenderId.Value).Select(u => u.Name).FirstOrDefaultAsync(ct);
            if (name is not null) names[msg.SenderId.Value] = name;
            var role = await db.ChatMembers.AsNoTracking()
                .Where(m => m.RoomId == msg.RoomId && m.UserId == msg.SenderId.Value)
                .Select(m => (ChatMemberRole?)m.Role).FirstOrDefaultAsync(ct);
            if (role.HasValue) roles[msg.SenderId.Value] = role.Value;
        }
        return ToMessageView(msg, names, roles);
    }

    /// <summary>D-293 — see <see cref="IChatService.EditMessageAsync"/>.</summary>
    public async Task<ServiceResult<ChatMessageView>> EditMessageAsync(Guid messageId, Guid userId, string body,
        CancellationToken ct = default)
    {
        var msg = await db.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return ServiceResult<ChatMessageView>.Fail("not_found");

        var member = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == msg.RoomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<ChatMessageView>.Fail("forbidden");

        // Only the sender, and there is deliberately no host override. A host may REMOVE an offending
        // message (DeleteMessageAsync) but must never be able to put different words in someone's
        // mouth under their name — that is a different power, and not one moderation needs.
        if (msg.SenderId != userId || msg.Kind == ChatMessageKind.System)
            return ServiceResult<ChatMessageView>.Fail("forbidden");
        if (msg.IsDeleted) return ServiceResult<ChatMessageView>.Fail("message_deleted");

        // Same right as posting: an edit is speech, so a locked room, a ban, a mute or a hosts-only
        // policy refuse it exactly as they refuse a send. Effective status, not stored (D-124).
        var room = await db.ChatRooms.AsNoTracking().FirstAsync(r => r.Id == msg.RoomId, ct);
        var effective = await EffectiveStatusAsync(msg.RoomId, ct) ?? room.Status;
        if (!Capabilities(room, member, effective).CanPost)
            return ServiceResult<ChatMessageView>.Fail(
                effective == ChatRoomStatus.Archived ? "room_archived"
                : effective != ChatRoomStatus.Active ? "room_locked"
                : member.IsBanned ? "banned"
                : member.MutedUntil > DateTime.UtcNow ? "muted"
                : "hosts_only");

        // Bounded by the same window as delete: a conversation that has already been read by everyone
        // must not be quietly rewritten hours later.
        if ((DateTime.UtcNow - msg.CreatedAt).TotalMinutes > DeleteWindowMinutes)
            return ServiceResult<ChatMessageView>.Fail("edit_window_expired");

        var trimmed = (body ?? "").Trim();
        // An attachment-only message has an empty body by design, so emptiness is only invalid when
        // there is nothing else carrying the message.
        var hasAttachments = await db.ChatAttachments
            .AnyAsync(a => a.MessageId == messageId && a.DeletedAt == null, ct);
        if (trimmed.Length == 0 && !hasAttachments) return ServiceResult<ChatMessageView>.Fail("body_required");
        if (trimmed.Length > 1000) return ServiceResult<ChatMessageView>.Fail("body_too_long");
        // A PATCH that changes nothing would still stamp EditedAt and mark the message "edited" — a
        // visible change for a request that made none. Same refusal the Posts editor makes.
        if (trimmed == msg.Body) return ServiceResult<ChatMessageView>.Fail("no_change");

        var before = msg.Body;
        msg.Body = trimmed;
        msg.EditedAt = DateTime.UtcNow;

        // Staged on this unit of work so the audit row commits with the edit, exactly as the delete
        // path does. The previous body is recorded: "edited" is only meaningful to a moderator if what
        // it replaced is still recoverable.
        audit.Write(new AuditEvent("chat.message_edit", "chat_messages", messageId,
            ActorId: userId,
            Before: new { body = before },
            After: new { body = trimmed }));

        await db.SaveChangesAsync(ct);

        var sender = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        var view = ToMessageView(msg,
            new Dictionary<Guid, string> { [userId] = sender?.Name ?? "" },
            new Dictionary<Guid, ChatMemberRole> { [userId] = member.Role },
            await LoadAttachmentsAsync([msg.Id], ct));

        // The whole view, not just the body: a client that missed an earlier event reconciles onto a
        // complete row rather than patching a partial one. Ordering is untouched — CreatedAt does not
        // move, so an edited message stays exactly where it was in everyone's history.
        await broadcaster.BroadcastChatAsync(msg.RoomId, "MessageEdited", view, msg.Id, ct);
        return ServiceResult<ChatMessageView>.Success(view);
    }

    /// <summary>D-293 — see <see cref="IChatService.HideMessageAsync"/>.</summary>
    public async Task<ServiceResult<bool>> HideMessageAsync(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        var roomId = await db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == messageId).Select(m => (Guid?)m.RoomId).FirstOrDefaultAsync(ct);
        if (roomId is null) return ServiceResult<bool>.Fail("not_found");

        // Membership is the only right needed — hiding affects nobody else, so there is no window, no
        // sender check and no host path. A non-member must still not learn the message exists.
        if (!await db.ChatMembers.AsNoTracking().AnyAsync(m => m.RoomId == roomId && m.UserId == userId, ct))
            return ServiceResult<bool>.Fail("forbidden");

        db.ChatMessageHides.Add(new ChatMessageHide { MessageId = messageId, UserId = userId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Already hidden. Idempotent by the unique pair rather than by a prior existence check,
            // which two taps could both pass.
            db.ChangeTracker.Clear();
        }

        // Deliberately NOT broadcast: nobody else's view changed, and announcing it would leak a
        // private filing decision to the room.
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        var msg = await db.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return ServiceResult<bool>.Fail("not_found");

        var member = await db.ChatMembers.AsNoTracking().FirstOrDefaultAsync(m => m.RoomId == msg.RoomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<bool>.Fail("forbidden");

        bool isSender = msg.SenderId == userId;
        if (!isSender)
        {
            // Deleting someone else's message is a moderation act, so it goes through the ladder (D-301).
            // A Moderator may remove a Member's message; a Host may remove anyone's. Resolved from the
            // SENDER's current role — a Moderator promoted after posting is protected from that moment,
            // which is the same rule every other action here follows.
            if (!CanModerate(member.Role)) return ServiceResult<bool>.Fail("forbidden");

            // A system message has no sender to outrank; only a Host may remove one.
            var senderRole = msg.SenderId is { } sid
                ? await db.ChatMembers.AsNoTracking()
                    .Where(m => m.RoomId == msg.RoomId && m.UserId == sid)
                    .Select(m => (ChatMemberRole?)m.Role).FirstOrDefaultAsync(ct)
                : ChatMemberRole.Host;

            // A sender who has since left the room outranks nobody — their message stays moderatable.
            if (!OutRanks(member.Role, senderRole ?? ChatMemberRole.Member))
                return ServiceResult<bool>.Fail("cannot_moderate_peer");
        }
        else if ((DateTime.UtcNow - msg.CreatedAt).TotalMinutes > DeleteWindowMinutes
                 && !CanModerate(member.Role))
        {
            return ServiceResult<bool>.Fail("delete_window_expired");
        }

        var wasPinned = msg.IsPinned;
        // Attachments follow the message's lifecycle. Soft delete here keeps deletion fast and
        // reversible-looking to the reader; the sweep removes the objects.
        var attachmentCount = await db.ChatAttachments
            .Where(a => a.MessageId == messageId && a.DeletedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.DeletedAt, DateTime.UtcNow), ct);

        msg.IsDeleted = true;
        msg.Body = "";
        msg.DeletedBy = userId;
        if (msg.IsPinned) msg.IsPinned = false;

        // D-102 typed spine: staged on this unit of work so the audit row commits in the SAME
        // transaction as the deletion. The previous code saved twice, so a failure between the two
        // left a deleted message with no audit trail.
        audit.Write(new AuditEvent("chat.message_delete", "chat_messages", messageId,
            ActorId: userId,
            Before: new { is_deleted = false, is_pinned = wasPinned },
            // D-301 — the actor's role rather than a bool: "deleted by a Moderator" and "deleted by a
            // Host" are different facts to a reviewer reading the trail, and by_host could not say which.
            After: new
            {
                is_deleted = true, is_pinned = false, attachments_removed = attachmentCount,
                by_role = member.Role.ToString(), was_own_message = isSender,
            }));

        await db.SaveChangesAsync(ct);

        await broadcaster.BroadcastChatAsync(msg.RoomId, "MessageDeleted", new { messageId }, messageId, ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<List<MyChatView>>> GetMyChatsAsync(
        Guid userId, bool archived = false, CancellationToken ct = default)
    {
        // D-292: archived rooms are filed away, not listed. The column existed and was honoured by the DM
        // list from the day it shipped; this list never read it, so an event chat could be archived and
        // still sat in Messages. Ordering is applied at the end, over last-message time.
        //
        // D-306: and then it read the column but only ever one way. Filtering to `ArchivedAt == null` with
        // no way to ask for the other side made archiving an event room unrecoverable — the row left this
        // list and no endpoint could return it. The predicate now selects a SIDE rather than excluding
        // one, which is what `IDmService.ListAsync` has always done.
        // AsNoTracking throughout: this builds a read model and mutates none of it. It also matters for
        // correctness, not just cost — pin and mute are written with ExecuteUpdateAsync, which does not
        // refresh the change tracker, so a tracked membership read in the same scope would report the
        // state from before the write.
        var memberships = await db.ChatMembers.AsNoTracking()
            .Where(m => m.UserId == userId && (archived ? m.ArchivedAt != null : m.ArchivedAt == null))
            .ToListAsync(ct);

        var roomIds = memberships.Select(m => m.RoomId).ToList();
        var rooms = await db.ChatRooms.AsNoTracking()
            .Where(r => roomIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var events = await db.Events.AsNoTracking()
            .Where(e => rooms.Values.Select(r => r.EventId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        // Two set-based queries for the whole list rather than three per room (D-113). The previous
        // shape cost 3N round trips — a user in 20 rooms issued 60 queries to paint one screen.

        // Read marks for every room at once, so unread can be computed without a lookup per room.
        var markIds = memberships.Where(m => m.LastReadMessageId != null)
            .Select(m => m.LastReadMessageId!.Value).ToList();
        var marks = await db.ChatMessages.AsNoTracking()
            .Where(msg => markIds.Contains(msg.Id))
            .Select(msg => new { msg.Id, msg.CreatedAt })
            .ToDictionaryAsync(x => x.Id, x => x.CreatedAt, ct);

        // One pass over the rooms' messages; unread and last-preview are both derived from it.
        //
        // Counting from the mark's timestamp rather than the (CreatedAt, Id) pair can differ by at
        // most the messages sharing that exact instant — an acceptable trade for a list badge, and
        // GetRoomAsync still counts exactly when the room is opened.
        // D-293: excludes messages this member hid, so a hidden last message neither previews in the
        // list nor keeps the unread badge lit. One NOT EXISTS for the whole list, not per room.
        var counted = await db.ChatMessages.AsNoTracking()
            .Where(msg => roomIds.Contains(msg.RoomId) && !msg.IsDeleted
                && !db.ChatMessageHides.Any(h => h.MessageId == msg.Id && h.UserId == userId))
            .Select(msg => new { msg.RoomId, msg.CreatedAt, msg.Body })
            .ToListAsync(ct);

        var byRoom = counted.GroupBy(x => x.RoomId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<MyChatView>();
        foreach (var m in memberships)
        {
            if (!rooms.TryGetValue(m.RoomId, out var room)) continue;
            // Direct rooms have no event and no place in this list — MyChatView is event-shaped, and
            // DMs are served by /v1/me/dm (D-264).
            if (room.EventId is not { } roomEventId || !events.TryGetValue(roomEventId, out var ev)) continue;

            byRoom.TryGetValue(m.RoomId, out var msgs);
            msgs ??= [];

            DateTime? readUpTo = m.LastReadMessageId is not null
                && marks.TryGetValue(m.LastReadMessageId.Value, out var at) ? at : null;

            var unread = readUpTo is null ? msgs.Count : msgs.Count(x => x.CreatedAt > readUpTo);
            var latest = msgs.OrderByDescending(x => x.CreatedAt).FirstOrDefault();

            // D-292: LastActivity is the last MESSAGE, not the event row's UpdatedAt. The two are
            // unrelated — editing an event's description bumped its chat to the top of Messages while a
            // room with genuinely new messages did not move. Falls back to the event's timestamp only
            // for a room nobody has posted in yet, which is the one case with no message to date from.
            result.Add(new(m.RoomId, ev.Id, ev.Title, ev.BannerKey, latest?.Body, unread,
                latest?.CreatedAt ?? ev.UpdatedAt,
                Pinned: m.PinnedAt is not null,
                NotificationsMuted: m.NotificationsMutedUntil > DateTime.UtcNow,
                // D-306. Constant within one call — the query already selected a side — but carried per
                // row so a client can render an un-archive control from the row it has, without inferring
                // state from which request it happened to make.
                Archived: m.ArchivedAt is not null));
        }
        // D-292: newest conversation first, the way every messenger orders an inbox. This used to sort by
        // the member's own LastReadAt, so opening a quiet room pushed it to the top while a room with an
        // unread message sank — exactly backwards from what the unread badge beside it was saying.
        // D-295: pinned first, then newest activity within each group. Ordering by activity alone
        // would let a busy room push a pinned one down, which is precisely what pinning exists to stop.
        return ServiceResult<List<MyChatView>>.Success(
            result.OrderByDescending(r => r.Pinned).ThenByDescending(r => r.LastActivity).ToList());
    }

    /// <summary>D-292 — see <see cref="IChatService.SetArchivedAsync"/>. Membership is the authorization:
    /// the update matches only the caller's own row, so a caller who is not in the room affects nothing
    /// and is told "not_found" rather than that the room exists (D-018).</summary>
    public async Task<ServiceResult<bool>> SetArchivedAsync(Guid roomId, Guid userId, bool archived, CancellationToken ct = default)
    {
        var updated = await db.ChatMembers.Where(m => m.RoomId == roomId && m.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ArchivedAt, archived ? DateTime.UtcNow : (DateTime?)null), ct);
        return updated > 0 ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.Fail("not_found");
    }

    // ── D-295 ──────────────────────────────────────────────────────────────────

    /// <summary>Reaction emoji are echoed to every member, so this is not a free text field. A single
    /// rendered emoji is a handful of code points at most; the cap sits above that and below anything
    /// that could be used to smuggle a payload.</summary>
    private const int MaxEmojiLength = 16;

    /// <summary>Is this actually an emoji, rather than short text wearing the field's name?
    ///
    /// <para>The length cap alone let a member put up to 16 characters of arbitrary text under every
    /// message, echoed to the whole room — speech that skipped the message pipeline entirely, so it
    /// could not be reported, moderated or deleted for everyone. The rule: at least one non-ASCII rune
    /// (every emoji has one) and no ASCII letter. That admits skin tones, ZWJ sequences, variation
    /// selectors and keycaps like 1️⃣, and rejects anything readable as a word.</para></summary>
    private static bool IsEmoji(string value) =>
        value.Any(ch => ch > 0x7F) && !value.Any(char.IsAsciiLetter);

    public async Task<ServiceResult<IReadOnlyList<ChatReactionView>>> ToggleReactionAsync(
        Guid messageId, Guid userId, string emoji, CancellationToken ct = default)
    {
        emoji = (emoji ?? "").Trim();
        if (emoji.Length == 0 || emoji.Length > MaxEmojiLength || !IsEmoji(emoji))
            return ServiceResult<IReadOnlyList<ChatReactionView>>.Fail("invalid_emoji");

        var msg = await db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == messageId).Select(m => new { m.RoomId, m.IsDeleted }).FirstOrDefaultAsync(ct);
        if (msg is null) return ServiceResult<IReadOnlyList<ChatReactionView>>.Fail("not_found");
        if (msg.IsDeleted) return ServiceResult<IReadOnlyList<ChatReactionView>>.Fail("message_deleted");

        var member = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == msg.RoomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<IReadOnlyList<ChatReactionView>>.Fail("forbidden");
        // Reacting is participation, so a ban silences it exactly as it silences posting. A mute does
        // not: a muted member may still read, and a reaction is closer to reading than to speaking.
        if (member.IsBanned) return ServiceResult<IReadOnlyList<ChatReactionView>>.Fail("banned");

        // Toggle by DELETE-first: if a row went, the tap was a removal and we are done. Deciding by a
        // prior read instead would let two taps both see "absent" and both insert — the unique triple
        // would then reject one with an error, when the honest answer is that it is simply on.
        var removed = await db.ChatMessageReactions
            .Where(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji)
            .ExecuteDeleteAsync(ct);

        if (removed == 0)
        {
            db.ChatMessageReactions.Add(new ChatMessageReaction
            {
                MessageId = messageId, UserId = userId, Emoji = emoji,
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Lost the race to another tap of the same emoji by the same person. The winner's row
                // says what this call wanted to say, so converge on it.
                db.ChangeTracker.Clear();
            }
        }

        var summary = await ReactionsForAsync([messageId], userId, ct);
        var view = summary.GetValueOrDefault(messageId) ?? [];
        // The whole summary, not a delta: a client that missed an earlier event reconciles onto a
        // complete row rather than patching a partial one — the same rule the edit broadcast follows.
        await broadcaster.BroadcastChatAsync(msg.RoomId, "ReactionChanged",
            new { messageId, reactions = view }, messageId, ct);
        return ServiceResult<IReadOnlyList<ChatReactionView>>.Success(view);
    }

    /// <summary>Reaction summaries for a page of messages, aggregated per emoji. One query for the whole
    /// page — a per-message load would make every history read O(page size) round trips.</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<ChatReactionView>>> ReactionsForAsync(
        IReadOnlyList<Guid> messageIds, Guid viewerId, CancellationToken ct)
    {
        if (messageIds.Count == 0) return [];
        var rows = await db.ChatMessageReactions.AsNoTracking()
            .Where(r => messageIds.Contains(r.MessageId))
            .Select(r => new { r.MessageId, r.Emoji, r.UserId })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.MessageId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ChatReactionView>)g
                    .GroupBy(r => r.Emoji, StringComparer.Ordinal)
                    // Most-reacted first, then alphabetical so the order is stable between reads
                    // rather than shifting with whatever the database happened to return.
                    .OrderByDescending(e => e.Count()).ThenBy(e => e.Key, StringComparer.Ordinal)
                    .Select(e => new ChatReactionView(
                        e.Key, e.Count(), e.Any(r => r.UserId == viewerId),
                        e.Select(r => r.UserId).ToList()))
                    .ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<ChatSearchHit>>> SearchMessagesAsync(
        Guid userId, string query, Guid? roomId, int limit, CancellationToken ct = default)
    {
        query = (query ?? "").Trim();
        if (query.Length < 2) return ServiceResult<IReadOnlyList<ChatSearchHit>>.Fail("query_too_short");
        limit = Math.Clamp(limit, 1, 50);

        // Membership is part of the query, not a filter afterwards. Applied later, the RESULT COUNT
        // would still have leaked whether a phrase appears in a room the caller cannot see.
        var myRooms = db.ChatMembers.AsNoTracking()
            .Where(m => m.UserId == userId && !m.IsBanned)
            .Select(m => m.RoomId);

        if (roomId is { } only)
        {
            if (!await myRooms.AnyAsync(r => r == only, ct))
                return ServiceResult<IReadOnlyList<ChatSearchHit>>.Fail("forbidden");
            myRooms = myRooms.Where(r => r == only);
        }

        // websearch_to_tsquery takes what a person actually types — quoted phrases, OR, leading minus —
        // and never throws on malformed input, unlike to_tsquery which rejects a bare apostrophe.
        var hits = await db.ChatMessages.AsNoTracking()
            .Where(m => myRooms.Contains(m.RoomId)
                && !m.IsDeleted
                // The caller's own hidden messages stay hidden here too (D-293) — search must not be
                // the one surface that hands back what "delete for me" removed.
                && !db.ChatMessageHides.Any(h => h.MessageId == m.Id && h.UserId == userId)
                && EF.Functions.ToTsVector("english", m.Body)
                    .Matches(EF.Functions.WebSearchToTsQuery("english", query)))
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(limit)
            .Select(m => new { m.Id, m.RoomId, m.SenderId, m.Body, m.CreatedAt })
            .ToListAsync(ct);

        if (hits.Count == 0) return ServiceResult<IReadOnlyList<ChatSearchHit>>.Success([]);

        // Two batched lookups for labels rather than one per hit.
        var senderIds = hits.Where(h => h.SenderId != null).Select(h => h.SenderId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => senderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var labels = await RoomLabelsAsync(hits.Select(h => h.RoomId).Distinct().ToList(), userId, ct);

        IReadOnlyList<ChatSearchHit> result = hits.Select(h => new ChatSearchHit(
            h.Id, h.RoomId, labels.GetValueOrDefault(h.RoomId, "Conversation"),
            h.SenderId, h.SenderId is { } s ? names.GetValueOrDefault(s) : null,
            h.Body, h.CreatedAt)).ToList();
        return ServiceResult<IReadOnlyList<ChatSearchHit>>.Success(result);
    }

    /// <summary>A human label per room for search results: the event's title, or for a direct room the
    /// OTHER person's name — which is what that conversation is called from this reader's side.</summary>
    private async Task<Dictionary<Guid, string>> RoomLabelsAsync(
        IReadOnlyList<Guid> roomIds, Guid viewerId, CancellationToken ct)
    {
        var rooms = await db.ChatRooms.AsNoTracking()
            .Where(r => roomIds.Contains(r.Id))
            .Select(r => new { r.Id, r.EventId, r.Kind, r.DirectLowUserId, r.DirectHighUserId })
            .ToListAsync(ct);

        var eventIds = rooms.Where(r => r.EventId != null).Select(r => r.EventId!.Value).Distinct().ToList();
        var titles = await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Title }).ToDictionaryAsync(e => e.Id, e => e.Title, ct);

        var otherIds = rooms
            .Where(r => r.EventId == null)
            .Select(r => r.DirectLowUserId == viewerId ? r.DirectHighUserId : r.DirectLowUserId)
            .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        var otherNames = await db.Users.AsNoTracking().Where(u => otherIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return rooms.ToDictionary(r => r.Id, r =>
        {
            if (r.EventId is { } ev) return titles.GetValueOrDefault(ev, "Event");
            var other = r.DirectLowUserId == viewerId ? r.DirectHighUserId : r.DirectLowUserId;
            return other is { } o ? otherNames.GetValueOrDefault(o, "Conversation") : "Conversation";
        });
    }

    public async Task<ServiceResult<bool>> MarkDeliveredAsync(
        Guid roomId, Guid userId, Guid messageId, CancellationToken ct = default)
    {
        var member = await db.ChatMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<bool>.Fail("forbidden");

        var mark = await db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == messageId && m.RoomId == roomId)
            .Select(m => new { m.CreatedAt, m.Id }).FirstOrDefaultAsync(ct);
        // A pointer naming a message in another room is refused rather than silently stored — the same
        // rule MarkReadAsync applies, for the same reason.
        if (mark is null) return ServiceResult<bool>.Fail("not_found");

        if (member.LastDeliveredMessageId is { } currentId)
        {
            var current = await db.ChatMessages.AsNoTracking()
                .Where(m => m.Id == currentId).Select(m => new { m.CreatedAt, m.Id }).FirstOrDefaultAsync(ct);
            // Forward-only. An out-of-order ack from a second device must not walk the receipt back.
            if (current is not null &&
                (current.CreatedAt, current.Id).CompareTo((mark.CreatedAt, mark.Id)) >= 0)
                return ServiceResult<bool>.Success(true);
        }

        member.LastDeliveredMessageId = mark.Id;
        await db.SaveChangesAsync(ct);

        await broadcaster.BroadcastChatAsync(roomId, "DeliveryReceiptChanged",
            new { userId, messageId = mark.Id }, mark.Id, ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<ChatMessageView>> ForwardMessageAsync(
        Guid messageId, Guid userId, Guid targetRoomId, Guid? clientMessageId, CancellationToken ct = default)
    {
        var source = await db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (source is null || source.IsDeleted) return ServiceResult<ChatMessageView>.Fail("not_found");

        // The caller must be able to SEE the source, not merely name its id — otherwise forwarding
        // becomes a read primitive for any room whose message ids leak.
        if (!await db.ChatMembers.AsNoTracking()
                .AnyAsync(m => m.RoomId == source.RoomId && m.UserId == userId && !m.IsBanned, ct))
            return ServiceResult<ChatMessageView>.Fail("forbidden");

        if (string.IsNullOrWhiteSpace(source.Body))
            // Attachments are not carried (see IChatService), so a file-only message has nothing to
            // forward. Refusing is honest; sending an empty message would not be.
            return ServiceResult<ChatMessageView>.Fail("nothing_to_forward");

        // Destination rights are re-derived from scratch by SendMessageAsync: room state, ban, mute and
        // post policy all apply to a forward exactly as to an original, because it IS an original there.
        // Named ct: `linkPreview` now sits before it, and a positional token would bind to the
        // wrong slot — the same trap the D-294 eviction call site hit.
        var sent = await SendMessageAsync(targetRoomId, userId, source.Body, null, clientMessageId, null, ct: ct);
        if (!sent.Ok) return sent;

        // Stamped after the send rather than passed into it, so the forward provenance cannot change
        // any of the gates SendMessageAsync applies.
        await db.ChatMessages.Where(m => m.Id == sent.Value!.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ForwardedFromMessageId, messageId), ct);

        return ServiceResult<ChatMessageView>.Success(sent.Value! with
        {
            ForwardedFrom = new ChatForwardView(messageId, await SenderNameAsync(source.SenderId, ct)),
        });
    }

    /// <summary>Original senders for whatever on this page was forwarded (D-295), in one query.
    ///
    /// <para>Resolves the name from the SOURCE message even when it now lives in a room the reader
    /// cannot see. That is deliberate and bounded: a forward already reproduces the body, so naming who
    /// wrote it discloses nothing further, and withholding it would make every forward read
    /// "Forwarded" from nobody. A source that has since been deleted resolves to null.</para></summary>
    private async Task<Dictionary<Guid, string?>> ForwardSenderNamesAsync(
        IReadOnlyList<ChatMessage> page, CancellationToken ct)
    {
        var sourceIds = page.Where(m => m.ForwardedFromMessageId != null)
            .Select(m => m.ForwardedFromMessageId!.Value).Distinct().ToList();
        if (sourceIds.Count == 0) return [];

        var sources = await db.ChatMessages.AsNoTracking()
            .Where(m => sourceIds.Contains(m.Id) && !m.IsDeleted)
            .Select(m => new { m.Id, m.SenderId }).ToListAsync(ct);
        var senderIds = sources.Where(s => s.SenderId != null).Select(s => s.SenderId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => senderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return sources.ToDictionary(
            s => s.Id,
            s => s.SenderId is { } id ? names.GetValueOrDefault(id) : null);
    }

    private async Task<string?> SenderNameAsync(Guid? senderId, CancellationToken ct) =>
        senderId is not { } id ? null
        : await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Name).FirstOrDefaultAsync(ct);

    public async Task<ServiceResult<bool>> SetPinnedAsync(
        Guid roomId, Guid userId, bool pinned, CancellationToken ct = default)
    {
        var updated = await db.ChatMembers.Where(m => m.RoomId == roomId && m.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.PinnedAt, pinned ? DateTime.UtcNow : (DateTime?)null), ct);
        return updated > 0 ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.Fail("not_found");
    }

    public async Task<ServiceResult<bool>> SetNotificationsMutedAsync(
        Guid roomId, Guid userId, DateTime? until, CancellationToken ct = default)
    {
        // A past instant is the same as no mute; normalising here means every reader can test one thing
        // (`> now`) rather than each deciding what a stale timestamp means.
        var effective = until is { } u && u > DateTime.UtcNow ? u : (DateTime?)null;
        var updated = await db.ChatMembers.Where(m => m.RoomId == roomId && m.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.NotificationsMutedUntil, effective), ct);
        return updated > 0 ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.Fail("not_found");
    }

    public async Task<ServiceResult<IReadOnlyList<ChatAttachmentView>>> RoomMediaAsync(
        Guid roomId, Guid userId, int limit, CancellationToken ct = default)
    {
        var member = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        if (member is null) return ServiceResult<IReadOnlyList<ChatAttachmentView>>.Fail("forbidden");
        if (member.IsBanned) return ServiceResult<IReadOnlyList<ChatAttachmentView>>.Fail("banned");
        limit = Math.Clamp(limit, 1, 100);

        // Only attachments still attached to a live, visible message. A deleted message's files are
        // swept later, so filtering on DeletedAt alone would surface them in the meantime; and the
        // caller's hidden messages stay hidden here as everywhere else (D-293).
        var attachments = await db.ChatAttachments.AsNoTracking()
            .Where(a => a.RoomId == roomId && a.DeletedAt == null && a.MessageId != null
                && db.ChatMessages.Any(m => m.Id == a.MessageId && !m.IsDeleted)
                && !db.ChatMessageHides.Any(h => h.MessageId == a.MessageId && h.UserId == userId))
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var views = new List<ChatAttachmentView>(attachments.Count);
        foreach (var a in attachments) views.Add(await ToAttachmentViewAsync(a, ct));
        return ServiceResult<IReadOnlyList<ChatAttachmentView>>.Success(views);
    }

    // ── Host moderation ────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> UpdateRoomAsync(Guid roomId, Guid userId, string? postPolicy, string? status, CancellationToken ct = default)
    {
        var room = await db.ChatRooms.FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return ServiceResult<bool>.Fail("not_found");
        if (!await IsHostAsync(roomId, userId, ct)) return ServiceResult<bool>.Fail("forbidden");

        // An archived room is frozen outright (D-122/D-123): not its policy, not its status. The
        // lifecycle is one-way, so there is no host action that reopens a finished conversation.
        if (await IsArchivedAsync(roomId, ct)) return ServiceResult<bool>.Fail("room_archived");

        if (postPolicy is not null && Enum.TryParse<ChatPostPolicy>(postPolicy, true, out var pp)) room.PostPolicy = pp;
        // Archive is a lifecycle transition owned by the sweep, not a room setting — letting a host
        // set it by hand would create a state nothing ever moves out of.
        if (status is not null && Enum.TryParse<ChatRoomStatus>(status, true, out var st)
            && st != ChatRoomStatus.Archived)
        {
            room.Status = st;
            if (st == ChatRoomStatus.Locked) room.LockedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await broadcaster.BroadcastChatAsync(roomId, "RoomUpdated", new { PostPolicy = room.PostPolicy.ToString(), Status = room.Status.ToString() }, null, ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> MuteMemberAsync(Guid roomId, Guid actorId, Guid targetUserId, int minutes, CancellationToken ct = default)
    {
        // D-301 — the ladder, not a host check: a Moderator may mute Members, and nobody may mute a peer
        // or a Host.
        var (denied, member) = await AuthorizeModerationAsync(roomId, actorId, targetUserId, ct);
        if (denied is not null) return ServiceResult<bool>.Fail(denied);
        // Nobody can post in an archived room, so muting is meaningless there — and D-122 freezes
        // participant state outright.
        if (await IsArchivedAsync(roomId, ct)) return ServiceResult<bool>.Fail("room_archived");
        minutes = Math.Clamp(minutes, 1, 10080);
        var previousMutedUntil = member!.MutedUntil;
        member.MutedUntil = DateTime.UtcNow.AddMinutes(minutes);
        audit.Write(new AuditEvent("chat.mute", "chat_members", member.Id,
            ActorId: actorId,
            Before: new { muted_until = previousMutedUntil },
            After: new { muted_until = member.MutedUntil, target_user_id = targetUserId, minutes }));
        await db.SaveChangesAsync(ct);
        await broadcaster.BroadcastChatAsync(roomId, "MemberMuted", new { userId = targetUserId, mutedUntil = member.MutedUntil }, null, ct);

        // Only the affected member is told (D-107) — a mute is not room-wide news.
        await NotifyModerationAsync(roomId, targetUserId, "chat_muted",
            $"You have been muted in this chat until {member.MutedUntil:g} UTC.", ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> BanMemberAsync(Guid roomId, Guid actorId, Guid targetUserId, CancellationToken ct = default)
    {
        var (denied, member) = await AuthorizeModerationAsync(roomId, actorId, targetUserId, ct);   // D-301
        if (denied is not null) return ServiceResult<bool>.Fail(denied);
        if (await IsArchivedAsync(roomId, ct)) return ServiceResult<bool>.Fail("room_archived");
        member!.IsBanned = true;
        audit.Write(new AuditEvent("chat.ban", "chat_members", member.Id,
            ActorId: actorId,
            Before: new { is_banned = false },
            After: new { is_banned = true, target_user_id = targetUserId }));
        await db.SaveChangesAsync(ct);

        // Setting the flag only stops the *next* request. An already-connected client stays in the
        // SignalR group and keeps receiving every message until it happens to reconnect, so the ban
        // has to evict the live connection too (D-106).
        // Named, because `eventName` now sits before the token — the default "MemberBanned" is exactly
        // what this path means, and passing ct positionally would silently bind it to the wrong slot.
        await broadcaster.EvictFromChatAsync(roomId, targetUserId, ct: ct);

        await NotifyModerationAsync(roomId, targetUserId, "chat_banned",
            "You have been removed from this event's chat.", ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> UnbanMemberAsync(Guid roomId, Guid actorId, Guid targetUserId, CancellationToken ct = default)
    {
        var (denied, member) = await AuthorizeModerationAsync(roomId, actorId, targetUserId, ct);   // D-301
        if (denied is not null) return ServiceResult<bool>.Fail(denied);
        member!.IsBanned = false;
        audit.Write(new AuditEvent("chat.unban", "chat_members", member.Id,
            ActorId: actorId,
            Before: new { is_banned = true },
            After: new { is_banned = false, target_user_id = targetUserId }));
        await db.SaveChangesAsync(ct);

        await NotifyModerationAsync(roomId, targetUserId, "chat_unbanned",
            "You can take part in this event's chat again.", ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> PinMessageAsync(Guid messageId, Guid userId, bool pin, TimeSpan? duration = null, CancellationToken ct = default)
    {
        var msg = await db.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null || msg.IsDeleted) return ServiceResult<bool>.Fail("not_found");
        // D-301 — Moderators may pin and unpin. No rank comparison: pinning acts on a MESSAGE, not on the
        // person who wrote it, so there is no peer to protect.
        var pinner = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == msg.RoomId && m.UserId == userId, ct);
        if (pinner is null || !CanModerate(pinner.Role)) return ServiceResult<bool>.Fail("forbidden");

        DateTime? until = null;
        if (pin)
        {
            var window = duration ?? DefaultPinDuration;
            if (window < MinPinDuration || window > MaxPinDuration) return ServiceResult<bool>.Fail("invalid_pin_duration");
            until = DateTime.UtcNow + window;

            // The three slots are for pins that still APPLY (D-296). Counting expired ones would let a
            // room fill with lapsed pins and refuse every new one — a pin nobody can see blocking a pin
            // everyone needs.
            var live = await ActivePins(msg.RoomId).Where(m => m.Id != messageId).ToListAsync(ct);
            foreach (var stale in live.OrderBy(m => m.CreatedAt).Take(Math.Max(0, live.Count - (MaxPinnedMessages - 1))))
                stale.IsPinned = false;
        }

        msg.IsPinned = pin;
        msg.PinnedUntil = until;
        await db.SaveChangesAsync(ct);
        await broadcaster.BroadcastChatAsync(msg.RoomId, "MessagePinned",
            new { messageId, isPinned = pin, pinnedUntil = until }, messageId, ct);
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>Pins that are still in force (D-296) — the only ones that occupy a slot or get rendered.</summary>
    private IQueryable<ChatMessage> ActivePins(Guid roomId) =>
        db.ChatMessages.Where(m => m.RoomId == roomId && m.IsPinned && !m.IsDeleted
            && (m.PinnedUntil == null || m.PinnedUntil > DateTime.UtcNow));

    public async Task<ServiceResult<bool>> SetModeratorAsync(
        Guid roomId, Guid actorId, Guid targetUserId, bool moderator, CancellationToken ct = default)
    {
        // HOSTS ONLY, and deliberately not the ladder. Promotion is the one action a Moderator must never
        // reach: a Moderator who could promote could mint a peer, and a Moderator who could demote could
        // strip one — either way the "cannot act on an equal" rule becomes bypassable in two steps (D-301).
        if (!await IsHostAsync(roomId, actorId, ct)) return ServiceResult<bool>.Fail("forbidden");

        var target = await db.ChatMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == targetUserId, ct);
        if (target is null) return ServiceResult<bool>.Fail("not_found");

        // A Host's rank is not this operation's to change. Demoting a Host would be a change of room
        // ownership wearing a moderation button, and promoting one is meaningless.
        if (target.Role == ChatMemberRole.Host) return ServiceResult<bool>.Fail("cannot_change_host");

        var previous = target.Role;
        var next = moderator ? ChatMemberRole.Moderator : ChatMemberRole.Member;

        // Idempotent: re-issuing a promotion is a no-op that still succeeds, so a retried request or a
        // double-tap cannot produce a second audit row or a second notification.
        if (previous == next) return ServiceResult<bool>.Success(true);

        // Conditional UPDATE, not a tracked write: two Hosts promoting and demoting the same member at the
        // same instant must not lose one another's decision silently. The row is claimed on its expected
        // current role, so the loser is told rather than overwritten (D-240 idiom).
        var changed = await db.ChatMembers
            .Where(m => m.Id == target.Id && m.Role == previous)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, next), ct);
        if (changed == 0) return ServiceResult<bool>.Fail("conflict");
        db.ChangeTracker.Clear();   // ExecuteUpdate bypasses the tracker; the entity above is now stale

        audit.Write(new AuditEvent("chat.role_change", "chat_members", target.Id,
            ActorId: actorId,
            Before: new { role = previous.ToString() },
            After: new
            {
                role = next.ToString(), target_user_id = targetUserId, room_id = roomId,
            }));
        await db.SaveChangesAsync(ct);

        // Room-level, so EVERY client re-reads capabilities — which is what makes the change take effect
        // without a reconnect, and what makes the promoted member's moderation controls appear at once.
        // Both clients already treat RoomUpdated this way, so no client-side event handling is added here.
        await broadcaster.BroadcastChatAsync(roomId, "RoomUpdated",
            new { userId = targetUserId, role = next.ToString() }, null, ct);

        await NotifyModerationAsync(roomId, targetUserId,
            moderator ? "chat_moderator_granted" : "chat_moderator_revoked",
            moderator
                ? "You are now a moderator in this event's chat."
                : "You are no longer a moderator in this event's chat.", ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ReportMessageAsync(Guid messageId, Guid reporterId, string reason, CancellationToken ct = default)
    {
        var msg = await db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return ServiceResult<bool>.Fail("not_found");

        // D-106: file into the platform moderation queue rather than an audit row. `chat_message` was
        // already a whitelisted entity type in ReportService — chat simply never used it, so reports
        // could not be listed, resolved or dismissed by anyone.
        var report = await reports.CreateAsync(reporterId, "chat_message", messageId, reason, null, ct);
        if (!report.Ok) return ServiceResult<bool>.Fail(report.Error ?? "report_failed");

        // Hosts are notified through the single production notification pipeline (NotifyAsync), which
        // writes the in-app row, broadcasts over SignalR, refreshes the unread badge and fans out to
        // FCM. The previous raw Notifications.Add did only the first of those.
        var hosts = await db.ChatMembers.AsNoTracking()
            .Where(m => m.RoomId == msg.RoomId && m.Role == ChatMemberRole.Host)
            .Select(m => m.UserId).ToListAsync(ct);

        foreach (var hostId in hosts)
            await notifications.NotifyAsync(hostId, "chat_report",
                "Message reported",
                "A chat message was reported for review.",
                new { room_id = msg.RoomId, message_id = messageId, report_id = report.Value!.Id }, ct);

        return ServiceResult<bool>.Success(true);
    }

    // ── Lifecycle job ──────────────────────────────────────────────────────────

    /// <summary>
    /// Advances rooms along the lifecycle (D-122): Active → Locked when the event ends, Locked →
    /// Archived seven days after that.
    /// </summary>
    /// <remarks>
    /// Both phases are driven from the event's <c>EndsAt</c> rather than from when the previous
    /// transition happened, so a sweep that fails to run for a week still lands rooms in the right
    /// state instead of shifting every subsequent deadline. Selecting only rooms in the state being
    /// left makes the whole thing idempotent.
    /// </remarks>
    public async Task LockExpiredRoomsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var archiveCutoff = now.AddDays(-ArchiveAfterEndDays);

        // Phase 1 — the event is over, so the conversation stops. Enforcement is already immediate via
        // EffectiveStatus; this is what makes it durable — the system message, the broadcast and the
        // notification that tell members the room closed. Same thresholds as EffectiveStatus, so the
        // persisted state can never disagree with the enforced one.
        var toLock = await db.ChatRooms
            .Where(r => r.Status == ChatRoomStatus.Active)
            .Join(db.Events, r => r.EventId, e => e.Id, (r, e) => new { room = r, ev = e })
            .Where(x => x.ev.EndsAt < now || x.ev.Status == EventStatus.Archived)
            .Select(x => x.room.Id)
            .ToListAsync(ct);

        foreach (var roomId in toLock)
            await TryAdvanceAsync(roomId, ChatRoomStatus.Active, ChatRoomStatus.Locked,
                "This event has ended. Chat is now read-only.", ct);

        // Phase 2 — the read-only window has expired. History stays; everything live stops.
        // Each transition above committed as it happened, so this query sees them and a single sweep
        // converges Active -> Archived for a room whose event ended long ago.
        var toArchive = await db.ChatRooms
            .Where(r => r.Status == ChatRoomStatus.Locked)
            .Join(db.Events, r => r.EventId, e => e.Id, (r, e) => new { room = r, ev = e })
            .Where(x => x.ev.EndsAt < archiveCutoff)
            .Select(x => x.room.Id)
            .ToListAsync(ct);

        foreach (var roomId in toArchive)
            await TryAdvanceAsync(roomId, ChatRoomStatus.Locked, ChatRoomStatus.Archived,
                "This chat has been archived. History remains available.", ct);
    }

    /// <summary>
    /// Removes storage objects that no live message references (D-110).
    ///
    /// Two sources, both unavoidable consequences of the upload flow:
    ///   * attachments soft-deleted with their message;
    ///   * uploads confirmed but never claimed by a message — the user closed the composer, the send
    ///     failed, the app was killed. Without this they would accumulate in storage forever.
    ///
    /// Idempotent: rows are hard-deleted only after their object is gone, so a re-run finds nothing.
    /// </summary>
    public async Task CleanupAttachmentsAsync(CancellationToken ct = default)
    {
        // Orphans get a grace window so an upload still in flight toward its send is never swept.
        var orphanCutoff = DateTime.UtcNow.AddHours(-24);

        var doomed = await db.ChatAttachments
            .Where(a => a.DeletedAt != null || (a.MessageId == null && a.CreatedAt < orphanCutoff))
            .Take(500)   // bounded per run so a large backlog cannot monopolise a worker
            .ToListAsync(ct);
        if (doomed.Count == 0) return;

        foreach (var a in doomed) await TryDeleteObjectAsync(a.StorageKey, ct);

        db.ChatAttachments.RemoveRange(doomed);
        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task MaybePostJoinMessageAsync(Guid roomId, Guid userId, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        var recentJoins = await db.ChatMessages
            .CountAsync(m => m.RoomId == roomId && m.Kind == ChatMessageKind.System && m.CreatedAt >= since, ct);
        if (recentJoins >= 20) return; // suppress during on-sale rush

        var name = await db.Users.Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        if (name is null) return;
        db.ChatMessages.Add(new ChatMessage { RoomId = roomId, Kind = ChatMessageKind.System, Body = $"{name} joined" });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Moderation notices go to the affected member only, through the shared pipeline.
    /// Never allowed to fail the moderation action that triggered it — the mute or ban is already
    /// committed by the time this runs.</summary>
    private async Task NotifyModerationAsync(Guid roomId, Guid userId, string kind, string body, CancellationToken ct)
    {
        try
        {
            var room = await db.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
            if (room is null) return;
            var ev = await db.Events.AsNoTracking().Where(e => e.Id == room.EventId)
                .Select(e => new { e.Id, e.Title }).FirstOrDefaultAsync(ct);
            if (ev is null) return;

            await notifications.NotifyAsync(userId, kind, ev.Title, body,
                new
                {
                    notificationType = "chat",
                    eventId = ev.Id,
                    roomId,
                    messageId = (Guid?)null,
                    senderId = (Guid?)null,
                }, ct);
        }
        catch
        {
            // Swallowed by design: a failed notice must not undo a completed moderation action.
        }
    }

    private async Task<bool> IsHostAsync(Guid roomId, Guid userId, CancellationToken ct)
        => await db.ChatMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.Role == ChatMemberRole.Host, ct);

    // ── The moderation ladder (D-301) ──────────────────────────────────────────────────────────
    //
    // Three predicates, and every moderation guard in this file goes through them. Written once because
    // the interesting rule is not "may this person moderate" but "may this person moderate THIS person",
    // and a per-call-site answer to that is exactly how a Moderator ends up able to ban a Host.

    /// <summary>Holds moderation authority at all: Host or Moderator.</summary>
    private static bool CanModerate(ChatMemberRole role) => role >= ChatMemberRole.Moderator;

    /// <summary>May <paramref name="actor"/> act on <paramref name="target"/>?
    ///
    /// <para><b>Strictly</b> greater, never <c>&gt;=</c>. Equal rank must fail, and that single character is
    /// the whole of D-301's protection model: a Moderator cannot demote, remove, mute or delete the
    /// messages of another Moderator, and neither can touch a Host. A Host outranks everyone, so nothing
    /// special-cases them.</para>
    ///
    /// <para>It also means no one can act on themselves through a moderation path — self-service actions
    /// (leave, delete-own-message) have their own routes and do not come through here.</para></summary>
    private static bool OutRanks(ChatMemberRole actor, ChatMemberRole target) => actor > target;

    /// <summary>Resolves the actor and target rows and applies the ladder. Returns the error code the
    /// endpoint should surface, or null when the action is allowed.</summary>
    private async Task<(string? Error, ChatMember? Target)> AuthorizeModerationAsync(
        Guid roomId, Guid actorId, Guid targetUserId, CancellationToken ct)
    {
        var actor = await db.ChatMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == actorId, ct);
        if (actor is null || !CanModerate(actor.Role)) return ("forbidden", null);

        var target = await db.ChatMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == targetUserId, ct);
        if (target is null) return ("not_found", null);

        // A distinct code from `forbidden`: "you may moderate, but not this person" is a different fact
        // from "you may not moderate", and a client that cannot tell them apart shows the wrong message.
        if (!OutRanks(actor.Role, target.Role)) return ("cannot_moderate_peer", target);
        return (null, target);
    }

    /// <summary>Builds attachment views for a page of messages in one query, minting a fresh signed
    /// download URL per attachment. URLs are short-lived and never stored: access is revoked by not
    /// issuing another one, so a leaked link expires on its own.</summary>
    private async Task<Dictionary<Guid, List<ChatAttachmentView>>> LoadAttachmentsAsync(
        IReadOnlyList<Guid> messageIds, CancellationToken ct)
    {
        if (messageIds.Count == 0) return [];

        var rows = await db.ChatAttachments.AsNoTracking()
            .Where(a => a.MessageId != null && messageIds.Contains(a.MessageId!.Value) && a.DeletedAt == null)
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var result = new Dictionary<Guid, List<ChatAttachmentView>>();
        foreach (var a in rows)
        {
            var url = await storage.PresignGetAsync(a.StorageKey, TimeSpan.FromMinutes(15), ct);
            var view = new ChatAttachmentView(a.Id, url, a.FileName, a.ContentType, a.SizeBytes, a.Width, a.Height);
            if (!result.TryGetValue(a.MessageId!.Value, out var list))
                result[a.MessageId!.Value] = list = [];
            list.Add(view);
        }
        return result;
    }

    /// <summary>Derives the host from a stored link URL (D-295). This is the one field of a link card a
    /// malicious sender cannot fake, which is why clients render it: the preview may misdescribe the
    /// page, but the host tells the reader truthfully where the tap goes. An unparseable URL yields no
    /// card at all rather than a card with a blank destination.</summary>
    /// <summary>Caps a sender-supplied card field. These are echoed to every member of the room, so
    /// they are bounded here rather than trusted at whatever length a client sends.</summary>
    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static ChatLinkPreviewView? ToLinkPreview(ChatMessage m)
    {
        if (string.IsNullOrWhiteSpace(m.LinkUrl)) return null;
        if (!Uri.TryCreate(m.LinkUrl, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        return new ChatLinkPreviewView(m.LinkUrl, uri.Host, m.LinkTitle, m.LinkDescription, m.LinkImageUrl);
    }

    private static ChatMessageView ToMessageView(ChatMessage m, Dictionary<Guid, string> names, Dictionary<Guid, ChatMemberRole> roles,
        IReadOnlyDictionary<Guid, List<ChatAttachmentView>>? attachments = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ChatReactionView>>? reactions = null,
        IReadOnlyDictionary<Guid, string?>? forwardSenderNames = null)
    {
        var senderName = m.SenderId.HasValue && names.TryGetValue(m.SenderId.Value, out var n) ? n : null;
        var senderRole = m.SenderId.HasValue && roles.TryGetValue(m.SenderId.Value, out var r) ? r.ToString() : null;
        return new(m.Id, m.ClientMessageId, m.RoomId, m.SenderId, senderName, senderRole,
            m.Kind.ToString(), m.IsDeleted ? "" : m.Body, m.ReplyToMessageId,
            // D-296 — an expired pin reads as unpinned. Computed here rather than left to each client so
            // every surface agrees on the moment it lapses, and so a client that never heard of expiry
            // still stops showing the badge.
            m.IsPinned && (m.PinnedUntil is null || m.PinnedUntil > DateTime.UtcNow), m.IsDeleted,
            // A deleted message shows no attachments even before the sweep removes the objects.
            m.IsDeleted || attachments is null || !attachments.TryGetValue(m.Id, out var files)
                ? []
                : files,
            m.CreatedAt,
            // A deleted message reports no edit history: the body is gone, so "edited" describes
            // nothing the reader can see and would only draw attention to a tombstone (D-293).
            m.IsDeleted ? null : m.EditedAt,
            // Same rule for everything below (D-295): a tombstone carries no reactions, no forward
            // provenance and no link card — there is nothing left for any of them to describe.
            m.IsDeleted || reactions is null || !reactions.TryGetValue(m.Id, out var reacts) ? null : reacts,
            m.IsDeleted || m.ForwardedFromMessageId is not { } src
                ? null
                // A null name is the honest fallback: the source may be deleted, or in a room this
                // reader cannot see, and the card then reads simply "Forwarded".
                : new ChatForwardView(src, forwardSenderNames?.GetValueOrDefault(src)),
            m.IsDeleted ? null : ToLinkPreview(m),
            m.IsPinned && !m.IsDeleted ? m.PinnedUntil : null);
    }
}
