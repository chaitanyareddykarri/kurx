using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Covers the D-104 contract freeze: keyset pagination, send idempotency, the capabilities object,
/// and the monotonic read pointer. These run against the service over a real Postgres rather than
/// over HTTP — the assertions are about SQL translation and persistence semantics, and going through
/// the endpoints would add request volume without testing anything extra.
///
/// The pagination cases deliberately give several messages an <b>identical CreatedAt</b>. That is the
/// case the old CreatedAt-only ordering got wrong (DateTime.UtcNow has ~15ms resolution, so real
/// ties happen), and it is what exercises Npgsql's row-wise comparison for the (CreatedAt, Id) pair.
/// </summary>
public class ChatContractTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _roomId, _eventId, _memberUserId, _hostUserId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ChatContractTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var member = new User { Name = "Chat Member", Phone = "9810077001" };
            var host = new User { Name = "Chat Host", Phone = "9810077002" };
            db.Users.AddRange(member, host);

            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Chat", Slug = "chat-contract" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _memberUserId = member.Id;
            _hostUserId = host.Id;
            var orgId = factory.SeedVerifiedOrg(host.Id, "Chat Contract Org");

            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = host.Id, CategoryId = category.Id,
                Title = "Chat Contract Event", Slug = "chat-contract-event", ShortCode = "CHT001",
                StartsAt = DateTime.UtcNow.AddDays(7), EndsAt = DateTime.UtcNow.AddDays(8),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            db.SaveChanges();
            _eventId = ev.Id;

            var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
            db.ChatRooms.Add(room);
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = member.Id, Role = ChatMemberRole.Member });
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = host.Id, Role = ChatMemberRole.Host });
            db.SaveChanges();
            _roomId = room.Id;

            _reset = true;
        }
    }

    private IChatService Chat(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IChatService>();

    /// <summary>Seeds messages that all share one CreatedAt, so ordering can only be resolved by the
    /// Id half of the sort key. Returns them in ascending (CreatedAt, Id) order.</summary>
    private List<ChatMessage> SeedTiedMessages(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var sameInstant = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
        var msgs = Enumerable.Range(0, count)
            .Select(i => new ChatMessage
            {
                RoomId = _roomId, SenderId = _memberUserId, Kind = ChatMessageKind.Text,
                Body = $"tied-{i}", CreatedAt = sameInstant,
            }).ToList();
        db.ChatMessages.AddRange(msgs);
        db.SaveChanges();
        return msgs.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
    }

    [Fact]
    public async Task Keyset_pagination_does_not_skip_or_duplicate_messages_with_identical_timestamps()
    {
        var seeded = SeedTiedMessages(6);
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        // Page backwards in twos and collect everything we see.
        var seen = new List<Guid>();
        string? cursor = null;
        for (var page = 0; page < 5; page++)
        {
            var res = await chat.GetMessagesAsync(_roomId, _memberUserId, cursor, null, 2);
            Assert.True(res.Ok, res.Error);
            if (res.Value!.Messages.Count == 0) break;
            seen.AddRange(res.Value.Messages.Select(m => m.Id));
            cursor = res.Value.OlderCursor;
        }

        // Every tied message appears exactly once — the defect this replaces dropped or repeated them.
        Assert.Equal(seeded.Count, seen.Distinct().Count());
        foreach (var m in seeded) Assert.Contains(m.Id, seen);
    }

    [Fact]
    public async Task After_cursor_returns_only_newer_messages_for_delta_sync()
    {
        var seeded = SeedTiedMessages(4);
        using var scope = _factory.Services.CreateScope();

        // Everything strictly after the second message, across a CreatedAt tie.
        var res = await Chat(scope).GetMessagesAsync(_roomId, _memberUserId, null, seeded[1].Id.ToString(), 50);
        Assert.True(res.Ok, res.Error);

        var ids = res.Value!.Messages.Select(m => m.Id).ToHashSet();
        Assert.DoesNotContain(seeded[0].Id, ids);
        Assert.DoesNotContain(seeded[1].Id, ids);   // the cursor itself is exclusive
        Assert.Contains(seeded[2].Id, ids);
        Assert.Contains(seeded[3].Id, ids);
    }

    [Fact]
    public async Task Send_is_idempotent_on_client_message_id()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var clientId = Guid.NewGuid();

        var first = await chat.SendMessageAsync(_roomId, _memberUserId, "retry me", null, clientId);
        var second = await chat.SendMessageAsync(_roomId, _memberUserId, "retry me", null, clientId);

        Assert.True(first.Ok, first.Error);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(first.Value!.Id, second.Value!.Id);   // same row, not a duplicate

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.ChatMessages.CountAsync(m => m.RoomId == _roomId && m.ClientMessageId == clientId));
    }

    [Fact]
    public async Task Message_ids_are_uuid_v7_and_sort_in_creation_order()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var a = await chat.SendMessageAsync(_roomId, _memberUserId, "first", null, Guid.NewGuid());
        await Task.Delay(5);
        var b = await chat.SendMessageAsync(_roomId, _memberUserId, "second", null, Guid.NewGuid());

        // Version nibble of a UUIDv7 is 7 (byte 7, high nibble in RFC 4122 layout).
        Assert.Equal(7, (a.Value!.Id.ToByteArray()[7] >> 4) & 0x0F);
        Assert.Equal(7, (b.Value!.Id.ToByteArray()[7] >> 4) & 0x0F);
        Assert.True(a.Value.CreatedAt <= b.Value.CreatedAt);
    }

    [Fact]
    public async Task Capabilities_reflect_role_and_room_policy()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var asMember = await chat.GetRoomAsync(_eventId, _memberUserId);
        var asHost = await chat.GetRoomAsync(_eventId, _hostUserId);
        Assert.True(asMember.Ok, asMember.Error);
        Assert.True(asHost.Ok, asHost.Error);

        Assert.True(asMember.Value!.Capabilities.CanPost);
        Assert.False(asMember.Value.Capabilities.CanModerate);
        Assert.False(asMember.Value.Capabilities.CanPin);
        Assert.True(asHost.Value!.Capabilities.CanModerate);
        Assert.True(asHost.Value.Capabilities.CanPin);

        // Attachments shipped in D-110: uploading is posting, so CanUpload tracks CanPost rather
        // than being a separate permission. (It read False here while attachments did not exist.)
        Assert.Equal(asMember.Value.Capabilities.CanPost, asMember.Value.Capabilities.CanUpload);
        Assert.Equal("General", asMember.Value.Kind);

        // HostsOnly silences a plain member without touching their membership.
        var locked = await chat.UpdateRoomAsync(_roomId, _hostUserId, "HostsOnly", null);
        Assert.True(locked.Ok, locked.Error);
        var afterPolicy = await chat.GetRoomAsync(_eventId, _memberUserId);
        Assert.False(afterPolicy.Value!.Capabilities.CanPost);
        Assert.True((await chat.GetRoomAsync(_eventId, _hostUserId)).Value!.Capabilities.CanPost);

        await chat.UpdateRoomAsync(_roomId, _hostUserId, "Everyone", null);
    }

    [Fact]
    public async Task Read_pointer_is_monotonic_and_drives_unread()
    {
        var seeded = SeedTiedMessages(3);
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var atNewest = await chat.MarkReadAsync(_roomId, _memberUserId, seeded[^1].Id);
        Assert.True(atNewest.Ok, atNewest.Error);
        var unreadAtNewest = (await chat.GetRoomAsync(_eventId, _memberUserId)).Value!.UnreadCount;

        // Rewinding to an older message must not move the pointer backwards — that is what makes
        // cross-device merge a plain max() and stops a stale device resurrecting old unreads.
        var rewind = await chat.MarkReadAsync(_roomId, _memberUserId, seeded[0].Id);
        Assert.True(rewind.Ok, rewind.Error);
        Assert.Equal(unreadAtNewest, (await chat.GetRoomAsync(_eventId, _memberUserId)).Value!.UnreadCount);

        // A pointer naming a message in another room is refused rather than silently stored.
        Assert.False((await chat.MarkReadAsync(_roomId, _memberUserId, Guid.NewGuid())).Ok);
    }

    // ── D-293: edit + delete-for-me ────────────────────────────────────────────

    /// <summary>A message created NOW. <see cref="SeedTiedMessages"/> back-dates to a fixed instant,
    /// which is outside the edit window, so the edit tests need their own fresh row.</summary>
    private ChatMessage SeedFreshMessage(Guid senderId, string body, DateTime? at = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var msg = new ChatMessage
        {
            RoomId = _roomId, SenderId = senderId, Kind = ChatMessageKind.Text,
            Body = body, CreatedAt = at ?? DateTime.UtcNow,
        };
        db.ChatMessages.Add(msg);
        db.SaveChanges();
        return msg;
    }

    [Fact]
    public async Task A_sender_edits_their_own_message_and_it_is_stamped_edited()
    {
        var msg = SeedFreshMessage(_memberUserId, "teh meeting is at 4");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).EditMessageAsync(msg.Id, _memberUserId, "the meeting is at 4");
        Assert.True(result.Ok, result.Error);
        Assert.Equal("the meeting is at 4", result.Value!.Body);
        Assert.NotNull(result.Value.EditedAt);

        // The stored row changed, not merely the returned view.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var stored = await db.ChatMessages.AsNoTracking().FirstAsync(m => m.Id == msg.Id);
        Assert.Equal("the meeting is at 4", stored.Body);
        Assert.NotNull(stored.EditedAt);
    }

    /// <summary>A host may REMOVE an offending message but must never rewrite one under someone else's
    /// name. There is deliberately no host override on edit — a different power from moderation.</summary>
    [Fact]
    public async Task Nobody_can_edit_someone_elses_message_not_even_a_host()
    {
        var msg = SeedFreshMessage(_memberUserId, "mine to edit");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var byHost = await chat.EditMessageAsync(msg.Id, _hostUserId, "words I did not say");
        Assert.False(byHost.Ok);
        Assert.Equal("forbidden", byHost.Error);

        // The host CAN still delete it — moderation is unaffected by the rule above.
        Assert.True((await chat.DeleteMessageAsync(msg.Id, _hostUserId)).Ok);
    }

    [Fact]
    public async Task An_edit_that_changes_nothing_is_refused()
    {
        var msg = SeedFreshMessage(_memberUserId, "unchanged");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).EditMessageAsync(msg.Id, _memberUserId, "unchanged");
        Assert.False(result.Ok);
        Assert.Equal("no_change", result.Error);

        // Whitespace-only difference is still no change: the body is trimmed before comparison, so a
        // stray space cannot stamp a message "edited" while leaving it identical on screen.
        var padded = await Chat(scope).EditMessageAsync(msg.Id, _memberUserId, "  unchanged  ");
        Assert.Equal("no_change", padded.Error);
    }

    [Fact]
    public async Task A_deleted_message_cannot_be_edited()
    {
        var msg = SeedFreshMessage(_memberUserId, "about to go");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        Assert.True((await chat.DeleteMessageAsync(msg.Id, _memberUserId)).Ok);

        var result = await chat.EditMessageAsync(msg.Id, _memberUserId, "resurrected");
        Assert.False(result.Ok);
        Assert.Equal("message_deleted", result.Error);
    }

    /// <summary>An edit must never move a message. Ordering is (CreatedAt, Id); if an edit touched the
    /// sort key, a correction typed an hour later would jump to the bottom of everyone's history.</summary>
    [Fact]
    public async Task Editing_does_not_change_where_a_message_sits_in_history()
    {
        var first = SeedFreshMessage(_memberUserId, "first", DateTime.UtcNow.AddMinutes(-3));
        var second = SeedFreshMessage(_memberUserId, "second", DateTime.UtcNow.AddMinutes(-2));
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var before = (await chat.GetMessagesAsync(_roomId, _memberUserId, null, null, 50)).Value!
            .Messages.Select(m => m.Id).ToList();
        Assert.True((await chat.EditMessageAsync(first.Id, _memberUserId, "first, corrected")).Ok);
        var after = (await chat.GetMessagesAsync(_roomId, _memberUserId, null, null, 50)).Value!
            .Messages.Select(m => m.Id).ToList();

        Assert.Equal(before, after);
        Assert.True(after.IndexOf(second.Id) < after.IndexOf(first.Id));   // newest-first, unchanged
    }

    [Fact]
    public async Task An_edit_after_the_window_closes_is_refused()
    {
        var old = SeedFreshMessage(_memberUserId, "long ago", DateTime.UtcNow.AddMinutes(-30));
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).EditMessageAsync(old.Id, _memberUserId, "too late");
        Assert.False(result.Ok);
        Assert.Equal("edit_window_expired", result.Error);
    }

    /// <summary>The defining property: hiding is MINE only. The message is untouched for everyone else,
    /// which is the whole distinction from delete-for-everyone.</summary>
    [Fact]
    public async Task Hiding_a_message_removes_it_from_my_history_only()
    {
        var msg = SeedFreshMessage(_hostUserId, "you can hide this");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        Assert.True((await chat.HideMessageAsync(msg.Id, _memberUserId)).Ok);

        var mine = (await chat.GetMessagesAsync(_roomId, _memberUserId, null, null, 50)).Value!.Messages;
        var theirs = (await chat.GetMessagesAsync(_roomId, _hostUserId, null, null, 50)).Value!.Messages;
        Assert.DoesNotContain(mine, m => m.Id == msg.Id);
        Assert.Contains(theirs, m => m.Id == msg.Id);

        // The message itself is intact — moderation and the audit trail still have the body.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var stored = await db.ChatMessages.AsNoTracking().FirstAsync(m => m.Id == msg.Id);
        Assert.False(stored.IsDeleted);
        Assert.Equal("you can hide this", stored.Body);
    }

    [Fact]
    public async Task Hiding_the_same_message_twice_is_idempotent()
    {
        var msg = SeedFreshMessage(_hostUserId, "hide me twice");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        Assert.True((await chat.HideMessageAsync(msg.Id, _memberUserId)).Ok);
        Assert.True((await chat.HideMessageAsync(msg.Id, _memberUserId)).Ok);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var rows = await db.ChatMessageHides.CountAsync(h => h.MessageId == msg.Id && h.UserId == _memberUserId);
        Assert.Equal(1, rows);
    }

    /// <summary>A message the reader removed from their own view must not keep their badge lit.</summary>
    [Fact]
    public async Task A_hidden_message_does_not_count_toward_my_unread()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var marker = SeedFreshMessage(_hostUserId, "marker");
        Assert.True((await chat.MarkReadAsync(_roomId, _memberUserId, marker.Id)).Ok);

        SeedFreshMessage(_hostUserId, "unread one");
        var hidden = SeedFreshMessage(_hostUserId, "unread two");

        var before = (await chat.GetRoomAsync(_eventId, _memberUserId)).Value!.UnreadCount;
        Assert.True((await chat.HideMessageAsync(hidden.Id, _memberUserId)).Ok);
        var after = (await chat.GetRoomAsync(_eventId, _memberUserId)).Value!.UnreadCount;

        Assert.Equal(before - 1, after);
    }

    /// <summary>A non-member must not be able to hide, and must not learn the message exists.</summary>
    [Fact]
    public async Task A_non_member_cannot_hide_a_message()
    {
        var msg = SeedFreshMessage(_hostUserId, "not yours to hide");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var outsider = new User { Name = "Chat Outsider", Phone = "9810078" + Random.Shared.Next(100, 999) };
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var result = await Chat(scope).HideMessageAsync(msg.Id, outsider.Id);
        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    // ── D-292: the Messages list ───────────────────────────────────────────────

    /// <summary>Seeds an isolated user in two rooms of their own, each with one message at a chosen time.
    /// Isolated deliberately: <c>GetMyChatsAsync</c> returns every room a user is in, so reusing the
    /// class's shared member would couple these assertions to whatever else has run.</summary>
    private static int _listSeq;

    private (Guid UserId, Guid OlderRoomId, Guid NewerRoomId) SeedTwoRooms(string tag)
    {
        // A per-call sequence, not a hash of `tag`: string.GetHashCode is randomised per process in
        // .NET Core, so a hash-derived phone or short code is a different value on every run and two
        // tags can collide on the unique index. Interlocked because xUnit may parallelise the class.
        var n = Interlocked.Increment(ref _listSeq);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var user = new User { Name = $"List {tag}", Phone = $"981010{n:D4}" };
        db.Users.Add(user);
        var category = db.EventCategories.First();
        // Saved before SeedVerifiedOrg: it writes a Membership whose UserId is a real FK, so the row
        // has to exist first — the same order the constructor above uses.
        db.SaveChanges();
        var orgId = _factory.SeedVerifiedOrg(user.Id, $"List Org {tag}");

        Guid Room(string slug, DateTime messageAt)
        {
            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = user.Id, CategoryId = category.Id,
                Title = $"List {slug}", Slug = $"list-{slug}-{n}",
                ShortCode = $"L{slug[0]}{n:D4}".ToUpperInvariant(),
                StartsAt = DateTime.UtcNow.AddDays(7), EndsAt = DateTime.UtcNow.AddDays(8),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
            db.ChatRooms.Add(room);
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = user.Id, Role = ChatMemberRole.Member });
            db.ChatMessages.Add(new ChatMessage
            {
                RoomId = room.Id, SenderId = user.Id, Kind = ChatMessageKind.Text,
                Body = $"{slug} message", CreatedAt = messageAt,
            });
            db.SaveChanges();
            return room.Id;
        }

        var older = Room("older", new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc));
        var newer = Room("newer", new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc));
        return (user.Id, older, newer);
    }

    /// <summary>The list is ordered by the last MESSAGE. It used to be ordered by the member's own
    /// <c>LastReadAt</c>, so opening a quiet room floated it to the top while a room holding an unread
    /// message sank — the opposite of what the unread badge beside it said.</summary>
    [Fact]
    public async Task The_chat_list_is_ordered_by_the_latest_message()
    {
        var (userId, olderRoomId, newerRoomId) = SeedTwoRooms("order");
        using var scope = _factory.Services.CreateScope();

        // Read the OLDER room most recently. Under the old LastReadAt ordering this alone put it first.
        var chat = Chat(scope);
        var older = await chat.GetMessagesAsync(olderRoomId, userId, null, null, 10);
        var marked = await chat.MarkReadAsync(olderRoomId, userId, older.Value!.Messages[0].Id);
        Assert.True(marked.Ok, marked.Error);

        var list = await chat.GetMyChatsAsync(userId);
        Assert.True(list.Ok, list.Error);
        Assert.Equal(2, list.Value!.Count);
        Assert.Equal(newerRoomId, list.Value[0].RoomId);
        Assert.Equal(olderRoomId, list.Value[1].RoomId);

        // LastActivity is the message time, not the event row's UpdatedAt — editing an event's
        // description used to reorder Messages.
        Assert.Equal(new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc), list.Value[0].LastActivity);
    }

    /// <summary>Archiving files a room out of the list, and un-archiving brings it back. The column
    /// shipped with D-264 and the DM list honoured it from day one; this list never read it, so an event
    /// chat could be archived and still sat there.</summary>
    [Fact]
    public async Task An_archived_room_leaves_the_chat_list_and_comes_back()
    {
        var (userId, olderRoomId, newerRoomId) = SeedTwoRooms("archive");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        Assert.Equal(2, (await chat.GetMyChatsAsync(userId)).Value!.Count);

        Assert.True((await chat.SetArchivedAsync(newerRoomId, userId, true)).Ok);
        var afterArchive = await chat.GetMyChatsAsync(userId);
        Assert.Equal(olderRoomId, Assert.Single(afterArchive.Value!).RoomId);

        Assert.True((await chat.SetArchivedAsync(newerRoomId, userId, false)).Ok);
        Assert.Equal(2, (await chat.GetMyChatsAsync(userId)).Value!.Count);
    }

    /// <summary>D-306 — an archived event room must be RETRIEVABLE, or archiving it is deletion.
    ///
    /// <para>The test above proved the room leaves the active list, and that was the whole contract:
    /// nothing could ask for the other side. `/v1/me/chats` took no `archived` parameter and
    /// `MyChatView` carried no `Archived`, so a client offering an Archive button on an event chat was
    /// offering a one-way trip. DMs had the return path from the day they shipped.</para></summary>
    [Fact]
    public async Task An_archived_event_room_is_returned_by_the_archived_list_and_flagged_as_archived()
    {
        var (userId, olderRoomId, newerRoomId) = SeedTwoRooms("archive-retrieval");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        // Nothing archived yet: the archived side is empty, the active side has both.
        Assert.Empty((await chat.GetMyChatsAsync(userId, archived: true)).Value!);
        Assert.Equal(2, (await chat.GetMyChatsAsync(userId)).Value!.Count);

        Assert.True((await chat.SetArchivedAsync(newerRoomId, userId, true)).Ok);

        // The room is gone from the active list — and findable, which is the half that was missing.
        Assert.Equal(olderRoomId, Assert.Single((await chat.GetMyChatsAsync(userId)).Value!).RoomId);
        var archivedRow = Assert.Single((await chat.GetMyChatsAsync(userId, archived: true)).Value!);
        Assert.Equal(newerRoomId, archivedRow.RoomId);
        // Carried per row so a client can render "move to inbox" from the row it already holds, rather
        // than inferring the state from which request it happened to make.
        Assert.True(archivedRow.Archived);

        // And the trip back works, leaving the archived side empty again.
        Assert.True((await chat.SetArchivedAsync(newerRoomId, userId, false)).Ok);
        Assert.Empty((await chat.GetMyChatsAsync(userId, archived: true)).Value!);
        Assert.All((await chat.GetMyChatsAsync(userId)).Value!, r => Assert.False(r.Archived));
    }

    /// <summary>Archiving is membership-scoped: a caller who is not in the room changes nothing and is
    /// told "not_found" rather than that the room exists (D-018).</summary>
    [Fact]
    public async Task A_non_member_cannot_archive_a_room()
    {
        var (_, _, newerRoomId) = SeedTwoRooms("outsider");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).SetArchivedAsync(newerRoomId, _memberUserId, true);
        Assert.False(result.Ok);
        Assert.Equal("not_found", result.Error);
    }
}
