using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// D-295 and D-296: reactions, search, delivery receipts, forwarding, per-member filing, shared media,
/// link previews, and the pin window.
///
/// Every test here targets a rule that a happy-path check would pass while the rule was broken — a
/// toggle that converges, a search that cannot see another room, a receipt that refuses to walk
/// backwards, a forward that refuses to carry a file, a pin that lapses.
/// </summary>
public class ChatMessagingFeatureTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _roomId, _otherRoomId, _hostId, _memberId, _outsiderId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ChatMessagingFeatureTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var host = new User { Name = "Feature Host", Phone = "919810077001" };
            var member = new User { Name = "Feature Member", Phone = "919810077002" };
            var outsider = new User { Name = "Feature Outsider", Phone = "919810077003" };
            db.Users.AddRange(host, member, outsider);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Feat", Slug = "chat-features" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _hostId = host.Id; _memberId = member.Id; _outsiderId = outsider.Id;
            var orgId = factory.SeedVerifiedOrg(host.Id, "Chat Feature Org");

            _roomId = SeedRoom(db, orgId, category.Id, host.Id, member.Id, "feature-event-a", "FEA001");
            // A second room the member also belongs to, so forwarding has a legitimate target and search
            // has something it must NOT return when scoped to one room.
            _otherRoomId = SeedRoom(db, orgId, category.Id, host.Id, member.Id, "feature-event-b", "FEA002");
            _reset = true;
        }
    }

    private static Guid SeedRoom(KurxDbContext db, Guid orgId, Guid categoryId, Guid hostId, Guid memberId,
        string slug, string shortCode)
    {
        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = hostId, CategoryId = categoryId,
            Title = $"Feature Event {shortCode}", Slug = slug, ShortCode = shortCode,
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(5), EndsAt = DateTime.UtcNow.AddDays(6),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        db.SaveChanges();

        var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
        db.ChatRooms.Add(room);
        db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = hostId, Role = ChatMemberRole.Host });
        db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = memberId, Role = ChatMemberRole.Member });
        db.SaveChanges();
        return room.Id;
    }

    private async Task<Guid> PostAsync(string body, Guid? roomId = null, Guid? senderId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var sent = await chat.SendMessageAsync(
            roomId ?? _roomId, senderId ?? _memberId, body, null, Guid.NewGuid());
        Assert.True(sent.Ok, sent.Error);
        return sent.Value!.Id;
    }

    private IChatService Chat(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IChatService>();

    // ── Reactions ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reacting_twice_with_the_same_emoji_removes_it_rather_than_counting_it_twice()
    {
        var messageId = await PostAsync("react to me");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var added = await chat.ToggleReactionAsync(messageId, _hostId, "👍");
        Assert.True(added.Ok, added.Error);
        var one = Assert.Single(added.Value!);
        Assert.Equal(1, one.Count);
        Assert.True(one.Mine);

        // The toggle is the whole point: a client that taps twice must land back where it started, not
        // accumulate. A count-only assertion would pass on an implementation that inserted a duplicate.
        var removed = await chat.ToggleReactionAsync(messageId, _hostId, "👍");
        Assert.True(removed.Ok, removed.Error);
        Assert.Empty(removed.Value!);
    }

    [Fact]
    public async Task A_reaction_summary_reports_mine_per_caller_not_per_message()
    {
        var messageId = await PostAsync("who reacted");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        await chat.ToggleReactionAsync(messageId, _hostId, "🎉");
        var asMember = await chat.ToggleReactionAsync(messageId, _memberId, "🎉");

        var summary = Assert.Single(asMember.Value!);
        Assert.Equal(2, summary.Count);
        // Computed for the CALLER. If `Mine` were a property of the row rather than of the request, the
        // second reactor would see the first reactor's state and the toggle would render inverted.
        Assert.True(summary.Mine);
    }

    [Fact]
    public async Task A_non_emoji_reaction_is_refused_so_the_row_cannot_become_free_text()
    {
        var messageId = await PostAsync("emoji only");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).ToggleReactionAsync(messageId, _hostId, "not-an-emoji");

        Assert.False(result.Ok);
        Assert.Equal("invalid_emoji", result.Error);
    }

    // ── Search ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_returns_matches_from_the_callers_rooms()
    {
        await PostAsync("the keynote is about zygomorphic architecture");
        using var scope = _factory.Services.CreateScope();

        var hits = await Chat(scope).SearchMessagesAsync(_memberId, "zygomorphic", null, 25);

        Assert.True(hits.Ok, hits.Error);
        Assert.Contains(hits.Value!, h => h.Body.Contains("zygomorphic"));
    }

    [Fact]
    public async Task Search_never_reaches_a_room_the_caller_is_not_in()
    {
        await PostAsync("quixotic scheduling discussion");
        using var scope = _factory.Services.CreateScope();

        // The outsider is a real user with no membership anywhere. Membership is part of the query, so
        // this must be empty rather than "filtered afterwards" — a count alone would still have leaked
        // that a match exists somewhere they cannot see.
        var hits = await Chat(scope).SearchMessagesAsync(_outsiderId, "quixotic", null, 25);

        Assert.True(hits.Ok, hits.Error);
        Assert.Empty(hits.Value!);
    }

    [Fact]
    public async Task Search_scoped_to_one_room_excludes_a_match_in_another_room_the_caller_is_in()
    {
        await PostAsync("perspicacious agenda note", _otherRoomId);
        using var scope = _factory.Services.CreateScope();

        var scoped = await Chat(scope).SearchMessagesAsync(_memberId, "perspicacious", _roomId, 25);
        var unscoped = await Chat(scope).SearchMessagesAsync(_memberId, "perspicacious", null, 25);

        Assert.Empty(scoped.Value!);
        Assert.NotEmpty(unscoped.Value!);
    }

    [Fact]
    public async Task A_single_character_query_is_refused_rather_than_scanning_every_room()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).SearchMessagesAsync(_memberId, "a", null, 25);

        Assert.False(result.Ok);
        Assert.Equal("query_too_short", result.Error);
    }

    [Fact]
    public async Task A_deleted_message_stops_being_findable()
    {
        var messageId = await PostAsync("obstreperous claim to retract");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        Assert.NotEmpty((await chat.SearchMessagesAsync(_memberId, "obstreperous", null, 25)).Value!);
        Assert.True((await chat.DeleteMessageAsync(messageId, _memberId)).Ok);

        // Deletion redacts the body. If search read an index that was not maintained on delete, the
        // retracted text would remain findable — the exact leak deleting is meant to close.
        Assert.Empty((await chat.SearchMessagesAsync(_memberId, "obstreperous", null, 25)).Value!);
    }

    // ── Delivery receipts ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_delivered_pointer_moves_forward_and_refuses_to_move_back()
    {
        var first = await PostAsync("first");
        var second = await PostAsync("second");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        Assert.True((await chat.MarkDeliveredAsync(_roomId, _hostId, second)).Ok);
        Assert.True((await chat.MarkDeliveredAsync(_roomId, _hostId, first)).Ok);

        // Monotonic: a second device acknowledging an older message must not walk the receipt back, or
        // the sender would watch ✓✓ disappear. Both calls succeed; only the newer one applies.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var member = await db.ChatMembers.AsNoTracking()
            .SingleAsync(m => m.RoomId == _roomId && m.UserId == _hostId);
        Assert.Equal(second, member.LastDeliveredMessageId);
    }

    [Fact]
    public async Task Delivery_is_not_the_read_pointer()
    {
        var messageId = await PostAsync("arrived but unread");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var before = await db.ChatMembers.AsNoTracking()
            .SingleAsync(m => m.RoomId == _roomId && m.UserId == _hostId);
        await chat.MarkDeliveredAsync(_roomId, _hostId, messageId);
        db.ChangeTracker.Clear();
        var after = await db.ChatMembers.AsNoTracking()
            .SingleAsync(m => m.RoomId == _roomId && m.UserId == _hostId);

        // One map for both would mark everything read the instant it landed, which is the whole reason
        // these are separate columns.
        Assert.Equal(messageId, after.LastDeliveredMessageId);
        Assert.Equal(before.LastReadMessageId, after.LastReadMessageId);
    }

    // ── Forwarding ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Forwarding_copies_the_body_into_the_target_room_with_provenance()
    {
        var messageId = await PostAsync("worth passing on");
        using var scope = _factory.Services.CreateScope();

        var forwarded = await Chat(scope).ForwardMessageAsync(messageId, _memberId, _otherRoomId, Guid.NewGuid());

        Assert.True(forwarded.Ok, forwarded.Error);
        Assert.Equal(_otherRoomId, forwarded.Value!.RoomId);
        Assert.Equal("worth passing on", forwarded.Value.Body);
        Assert.Equal(messageId, forwarded.Value.ForwardedFrom!.MessageId);
    }

    [Fact]
    public async Task Forwarding_into_a_room_the_caller_is_not_in_is_refused()
    {
        var messageId = await PostAsync("not yours to move");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).ForwardMessageAsync(messageId, _outsiderId, _otherRoomId, Guid.NewGuid());

        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    [Fact]
    public async Task Forwarding_carries_no_attachments()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var messageId = await PostAsync("see the attached");

        // Attached directly: the point is what forward does with a file, not how it got there.
        db.ChatAttachments.Add(new ChatAttachment
        {
            MessageId = messageId, RoomId = _roomId, UploadedBy = _memberId,
            StorageKey = $"chat/{_roomId}/{Guid.NewGuid()}.pdf", FileName = "brief.pdf",
            ContentType = "application/pdf", SizeBytes = 1024,
        });
        await db.SaveChangesAsync();

        var forwarded = await Chat(scope).ForwardMessageAsync(messageId, _memberId, _otherRoomId, Guid.NewGuid());

        Assert.True(forwarded.Ok, forwarded.Error);
        // Re-pointing the row would let a forward smuggle a file into a room its uploader never had
        // access to, and the download endpoint checks membership of the ATTACHMENT's room.
        Assert.Empty(forwarded.Value!.Attachments);
    }

    // ── Per-member filing: pin, mute, archive ──────────────────────────────────────────────────

    [Fact]
    public async Task Pinning_a_conversation_is_reported_back_on_the_list_and_orders_it_first()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        Assert.True((await chat.SetPinnedAsync(_otherRoomId, _memberId, true)).Ok);
        var chats = await chat.GetMyChatsAsync(_memberId);

        Assert.True(chats.Ok, chats.Error);
        // Without `Pinned` on the wire nothing could render the marker, and without the ordering the pin
        // would change nothing the reader can see — the two together are what pinning IS.
        Assert.Equal(_otherRoomId, chats.Value![0].RoomId);
        Assert.True(chats.Value[0].Pinned);

        await chat.SetPinnedAsync(_otherRoomId, _memberId, false);   // leave the shared fixture as found
    }

    [Fact]
    public async Task Muting_notifications_is_reported_back_and_clears_again()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        await chat.SetNotificationsMutedAsync(_roomId, _memberId, DateTime.UtcNow.AddHours(8));
        var muted = (await chat.GetMyChatsAsync(_memberId)).Value!.Single(c => c.RoomId == _roomId);
        Assert.True(muted.NotificationsMuted);

        await chat.SetNotificationsMutedAsync(_roomId, _memberId, null);
        var unmuted = (await chat.GetMyChatsAsync(_memberId)).Value!.Single(c => c.RoomId == _roomId);
        Assert.False(unmuted.NotificationsMuted);
    }

    [Fact]
    public async Task A_mute_that_has_already_expired_reports_as_unmuted()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        await chat.SetNotificationsMutedAsync(_roomId, _memberId, DateTime.UtcNow.AddMinutes(-1));
        var chats = await chat.GetMyChatsAsync(_memberId);

        // Evaluated against now, not merely "a timestamp is set" — otherwise a room muted once would
        // read as muted forever and the reader would silently stop being notified.
        Assert.False(chats.Value!.Single(c => c.RoomId == _roomId).NotificationsMuted);
        await chat.SetNotificationsMutedAsync(_roomId, _memberId, null);
    }

    // ── Shared media ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Room_media_lists_the_rooms_attachments_and_refuses_a_non_member()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var messageId = await PostAsync("media carrier");
        db.ChatAttachments.Add(new ChatAttachment
        {
            MessageId = messageId, RoomId = _roomId, UploadedBy = _memberId,
            StorageKey = $"chat/{_roomId}/{Guid.NewGuid()}.png", FileName = "poster.png",
            ContentType = "image/png", SizeBytes = 2048,
        });
        await db.SaveChangesAsync();

        var mine = await Chat(scope).RoomMediaAsync(_roomId, _memberId, 60);
        Assert.True(mine.Ok, mine.Error);
        Assert.Contains(mine.Value!, a => a.FileName == "poster.png");

        var theirs = await Chat(scope).RoomMediaAsync(_roomId, _outsiderId, 60);
        Assert.False(theirs.Ok);
        Assert.Equal("forbidden", theirs.Error);
    }

    // ── Link previews ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_link_preview_is_stored_with_the_message_and_read_back_with_its_host()
    {
        using var scope = _factory.Services.CreateScope();

        var sent = await Chat(scope).SendMessageAsync(_roomId, _memberId, "have a look", null, Guid.NewGuid(),
            linkPreview: new ChatLinkPreviewInput(
                "https://example.org/schedule", "Schedule", "The full running order", null));

        Assert.True(sent.Ok, sent.Error);
        // The host is DERIVED server-side, never taken from the sender: it is the one part of a card a
        // sender cannot fake, which is why the clients render it most prominently.
        Assert.Equal("example.org", sent.Value!.LinkPreview!.Host);
        Assert.Equal("Schedule", sent.Value.LinkPreview.Title);
    }

    [Fact]
    public async Task A_non_http_link_preview_is_dropped_rather_than_rendered()
    {
        using var scope = _factory.Services.CreateScope();

        var sent = await Chat(scope).SendMessageAsync(_roomId, _memberId, "sketchy", null, Guid.NewGuid(),
            linkPreview: new ChatLinkPreviewInput("javascript:alert(1)", "Click", null, null));

        Assert.True(sent.Ok, sent.Error);
        // A card is a tappable link. Anything but http/https must not become one, whatever the sender
        // supplied — the message itself still sends, it simply carries no card.
        Assert.Null(sent.Value!.LinkPreview);
    }

    // ── D-296: the pin window ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pinning_a_message_without_a_duration_takes_the_default_window_not_forever()
    {
        var messageId = await PostAsync("pin me by default");
        using var scope = _factory.Services.CreateScope();

        Assert.True((await Chat(scope).PinMessageAsync(messageId, _hostId, true)).Ok);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var msg = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == messageId);
        Assert.True(msg.IsPinned);
        // The regression this closes: a null duration used to mean a permanent pin, and a permanent pin
        // is the one a host never comes back to clear.
        Assert.NotNull(msg.PinnedUntil);
        Assert.InRange(msg.PinnedUntil!.Value, DateTime.UtcNow.AddDays(6), DateTime.UtcNow.AddDays(8));

        await Chat(scope).PinMessageAsync(messageId, _hostId, false);
    }

    [Fact]
    public async Task A_pin_duration_outside_the_allowed_range_is_refused_rather_than_clamped()
    {
        var messageId = await PostAsync("pin me for a year");
        using var scope = _factory.Services.CreateScope();

        var tooLong = await Chat(scope).PinMessageAsync(messageId, _hostId, true, TimeSpan.FromDays(365));
        var tooShort = await Chat(scope).PinMessageAsync(messageId, _hostId, true, TimeSpan.FromMinutes(5));

        // Silently pinning for 30 days when a year was asked for is a worse answer than saying no: the
        // host would believe the pin outlives the event.
        Assert.Equal("invalid_pin_duration", tooLong.Error);
        Assert.Equal("invalid_pin_duration", tooShort.Error);
    }

    [Fact]
    public async Task An_expired_pin_stops_being_pinned_without_any_job_running()
    {
        var messageId = await PostAsync("this pin will lapse");
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.True((await chat.PinMessageAsync(messageId, _hostId, true, TimeSpan.FromHours(2))).Ok);
        var room = await chat.GetRoomAsync(RoomsEventId(db, _roomId), _memberId);
        Assert.Contains(room.Value!.PinnedMessages, m => m.Id == messageId);

        // Reach past the service to age the pin — there is no API for "pretend it is Friday", and the
        // rule under test is precisely that nothing has to run for the pin to lapse.
        await db.ChatMessages.Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.PinnedUntil, DateTime.UtcNow.AddMinutes(-1)));

        var later = await chat.GetRoomAsync(RoomsEventId(db, _roomId), _memberId);
        Assert.DoesNotContain(later.Value!.PinnedMessages, m => m.Id == messageId);

        var page = await chat.GetMessagesAsync(_roomId, _memberId, null, null, 50);
        // And the badge on the original bubble goes with it: `IsPinned` is computed against the window,
        // so a client that never heard of expiry still stops drawing the pin.
        Assert.False(page.Value!.Messages.Single(m => m.Id == messageId).IsPinned);
    }

    [Fact]
    public async Task An_expired_pin_does_not_occupy_one_of_the_three_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var stale = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = await PostAsync($"stale pin {i}");
            Assert.True((await chat.PinMessageAsync(id, _hostId, true, TimeSpan.FromHours(2))).Ok);
            stale.Add(id);
        }
        await db.ChatMessages.Where(m => stale.Contains(m.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.PinnedUntil, DateTime.UtcNow.AddMinutes(-1)));

        var fresh = await PostAsync("the pin that matters");
        Assert.True((await chat.PinMessageAsync(fresh, _hostId, true, TimeSpan.FromHours(24))).Ok);

        var room = await chat.GetRoomAsync(RoomsEventId(db, _roomId), _memberId);
        // Counting lapsed pins would let a room fill with pins nobody can see, blocking the one everyone
        // needs. The new pin must be there, and the stale ones must not.
        Assert.Contains(room.Value!.PinnedMessages, m => m.Id == fresh);
        Assert.DoesNotContain(room.Value.PinnedMessages, m => stale.Contains(m.Id));

        await chat.PinMessageAsync(fresh, _hostId, false);
    }

    [Fact]
    public async Task A_fourth_live_pin_pushes_out_the_oldest_rather_than_being_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var id = await PostAsync($"rolling pin {i}");
            Assert.True((await chat.PinMessageAsync(id, _hostId, true, TimeSpan.FromHours(24))).Ok);
            ids.Add(id);
        }

        var room = await chat.GetRoomAsync(RoomsEventId(db, _roomId), _memberId);
        var pinned = room.Value!.PinnedMessages.Select(m => m.Id).ToList();
        Assert.Equal(3, pinned.Count);
        Assert.DoesNotContain(ids[0], pinned);
        Assert.Contains(ids[3], pinned);

        foreach (var id in ids) await chat.PinMessageAsync(id, _hostId, false);
    }

    [Fact]
    public async Task Only_a_host_can_pin_a_message()
    {
        var messageId = await PostAsync("members cannot pin this");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).PinMessageAsync(messageId, _memberId, true, TimeSpan.FromHours(24));

        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    private static Guid RoomsEventId(KurxDbContext db, Guid roomId) =>
        db.ChatRooms.AsNoTracking().Single(r => r.Id == roomId).EventId!.Value;
}
