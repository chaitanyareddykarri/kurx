using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Phase 2C: chat notifications must flow through the one production pipeline
/// (<see cref="INotificationService.NotifyAsync"/>) and respect the room's eligibility rules.
///
/// The fan-out job is invoked directly rather than via Hangfire so assertions are deterministic —
/// all the eligibility logic lives in the job, and enqueuing is what keeps it off the send path.
/// </summary>
public class ChatNotificationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _roomId, _eventId, _hostId, _senderId, _recipientId, _mentionedId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ChatNotificationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var host = new User { Name = "Notify Host", Phone = "919810044001", Username = "notifyhost" };
            var sender = new User { Name = "Notify Sender", Phone = "919810044002", Username = "notifysender" };
            var recipient = new User { Name = "Notify Recipient", Phone = "919810044003", Username = "notifyrecipient" };
            var mentioned = new User { Name = "Mentioned One", Phone = "919810044004", Username = "mentionee" };
            db.Users.AddRange(host, sender, recipient, mentioned);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Notif", Slug = "chat-notify" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _hostId = host.Id; _senderId = sender.Id; _recipientId = recipient.Id; _mentionedId = mentioned.Id;
            var orgId = factory.SeedVerifiedOrg(host.Id, "Chat Notify Org");

            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = host.Id, CategoryId = category.Id,
                Title = "Notify Event", Slug = "notify-event", ShortCode = "NTF001",
                Description = "d", VenueName = "v",
                StartsAt = DateTime.UtcNow.AddDays(4), EndsAt = DateTime.UtcNow.AddDays(5),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            db.SaveChanges();
            _eventId = ev.Id;

            var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
            db.ChatRooms.Add(room);
            db.ChatMembers.AddRange(
                new ChatMember { RoomId = room.Id, UserId = host.Id, Role = ChatMemberRole.Host },
                new ChatMember { RoomId = room.Id, UserId = sender.Id, Role = ChatMemberRole.Member },
                new ChatMember { RoomId = room.Id, UserId = recipient.Id, Role = ChatMemberRole.Member },
                new ChatMember { RoomId = room.Id, UserId = mentioned.Id, Role = ChatMemberRole.Member });
            db.SaveChanges();
            _roomId = room.Id;
            _reset = true;
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Persists a message WITHOUT going through ChatService, then runs the fan-out job once.
    /// ChatService.SendMessageAsync enqueues the same job on Hangfire, and the test host runs a real
    /// Hangfire server — so sending here would race a second fan-out and double every count. The
    /// enqueue path itself is covered separately by
    /// <see cref="Sending_a_message_enqueues_the_fan_out"/>.</summary>
    private async Task<Guid> SendAndFanOutAsync(string body, Guid? senderId = null)
    {
        Guid messageId;
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<KurxDbContext>();
            var msg = new ChatMessage
            {
                RoomId = _roomId, SenderId = senderId ?? _senderId,
                Kind = ChatMessageKind.Text, Body = body, ClientMessageId = Guid.NewGuid(),
            };
            db.ChatMessages.Add(msg);
            await db.SaveChangesAsync();
            messageId = msg.Id;
        }

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ChatNotificationJob>()
            .RunAsync(messageId, CancellationToken.None);
        return messageId;
    }

    private async Task<Guid> SeedMessageAsync(string body)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var msg = new ChatMessage
        {
            RoomId = _roomId, SenderId = _senderId,
            Kind = ChatMessageKind.Text, Body = body, ClientMessageId = Guid.NewGuid(),
        };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();
        return msg.Id;
    }

    private List<Notification> ChatNotificationsFor(Guid userId, Guid messageId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // DataJson is jsonb, so a substring match cannot be translated (`operator does not exist:
        // jsonb ~~ jsonb`). Filter by user in SQL, then match the payload in memory.
        return db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .ToList()
            .Where(n => n.DataJson is not null && n.DataJson.Contains(messageId.ToString()))
            .ToList();
    }

    private Guid AddMemberWithDevices(string phone, string name, int deviceCount, bool banned = false, DateTime? mutedUntil = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var u = new User { Name = name, Phone = phone };
        db.Users.Add(u);
        db.SaveChanges();
        db.ChatMembers.Add(new ChatMember
        {
            RoomId = _roomId, UserId = u.Id, Role = ChatMemberRole.Member,
            IsBanned = banned, MutedUntil = mutedUntil,
        });
        for (var i = 0; i < deviceCount; i++)
            db.Devices.Add(new Device { UserId = u.Id, FcmToken = $"tok-{u.Id}-{i}", Platform = "android", IsActive = true });
        db.SaveChanges();
        return u.Id;
    }

    // ── New message ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task New_message_notifies_eligible_members_and_never_the_sender()
    {
        var messageId = await SendAndFanOutAsync("hello room");

        Assert.Single(ChatNotificationsFor(_recipientId, messageId));
        Assert.Single(ChatNotificationsFor(_hostId, messageId));
        Assert.Empty(ChatNotificationsFor(_senderId, messageId));   // sender is always excluded
    }

    [Fact]
    public async Task Notification_payload_carries_the_deep_link_identifiers()
    {
        var messageId = await SendAndFanOutAsync("payload check");
        var notification = Assert.Single(ChatNotificationsFor(_recipientId, messageId));

        using var doc = System.Text.Json.JsonDocument.Parse(notification.DataJson!);
        var root = doc.RootElement;
        Assert.Equal("chat", root.GetProperty("notificationType").GetString());
        Assert.Equal(_eventId.ToString(), root.GetProperty("eventId").GetString());
        Assert.Equal(_roomId.ToString(), root.GetProperty("roomId").GetString());
        Assert.Equal(messageId.ToString(), root.GetProperty("messageId").GetString());
        Assert.Equal(_senderId.ToString(), root.GetProperty("senderId").GetString());

        Assert.Equal("Notify Event", notification.Title);          // event name
        Assert.Contains("Notify Sender", notification.Body);        // sender display name
    }

    [Fact]
    public async Task Long_bodies_are_truncated_to_a_preview()
    {
        var longBody = new string('a', 400) + " tail";
        var messageId = await SendAndFanOutAsync(longBody);
        var notification = Assert.Single(ChatNotificationsFor(_recipientId, messageId));

        Assert.DoesNotContain("tail", notification.Body);
        Assert.True(notification.Body.Length < 200, $"preview too long: {notification.Body.Length}");
        Assert.EndsWith("…", notification.Body);
    }

    // ── Mentions ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Mentioning_a_member_raises_the_notification_kind()
    {
        var messageId = await SendAndFanOutAsync("hey @mentionee take a look");

        var mention = Assert.Single(ChatNotificationsFor(_mentionedId, messageId));
        Assert.Equal("chat_mention", mention.Kind);
        Assert.Contains("mentioned you", mention.Body);

        // Everyone else still gets the ordinary kind.
        Assert.Equal("chat_message", Assert.Single(ChatNotificationsFor(_recipientId, messageId)).Kind);
    }

    [Fact]
    public async Task A_mention_cannot_notify_someone_who_is_not_in_the_room()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var outsider = new User { Name = "Outsider", Phone = "919810044099", Username = "outsider" };
        db.Users.Add(outsider);
        db.SaveChanges();

        var messageId = await SendAndFanOutAsync("hello @outsider you are not here");
        Assert.Empty(ChatNotificationsFor(outsider.Id, messageId));
    }

    // ── Host announcement ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_host_message_is_classified_as_an_announcement()
    {
        var messageId = await SendAndFanOutAsync("important notice from the organisers", senderId: _hostId);

        Assert.Equal("chat_announcement", Assert.Single(ChatNotificationsFor(_recipientId, messageId)).Kind);
        Assert.Empty(ChatNotificationsFor(_hostId, messageId));   // still not the sender
    }

    // ── Eligibility ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Banned_and_muted_members_are_not_notified()
    {
        var banned = AddMemberWithDevices("919810044010", "Banned Member", 1, banned: true);
        var muted = AddMemberWithDevices("919810044011", "Muted Member", 1, mutedUntil: DateTime.UtcNow.AddHours(1));
        var expiredMute = AddMemberWithDevices("919810044012", "Expired Mute", 1, mutedUntil: DateTime.UtcNow.AddHours(-1));

        var messageId = await SendAndFanOutAsync("eligibility check");

        Assert.Empty(ChatNotificationsFor(banned, messageId));
        Assert.Empty(ChatNotificationsFor(muted, messageId));
        Assert.Single(ChatNotificationsFor(expiredMute, messageId));   // a lapsed mute is not a mute
    }

    [Fact]
    public async Task A_member_removed_from_the_room_stops_being_notified()
    {
        var leaver = AddMemberWithDevices("919810044013", "Leaver", 1);
        Assert.Single(ChatNotificationsFor(leaver, await SendAndFanOutAsync("before removal")));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.ChatMembers.RemoveRange(db.ChatMembers.Where(m => m.RoomId == _roomId && m.UserId == leaver));
            db.SaveChanges();
        }

        Assert.Empty(ChatNotificationsFor(leaver, await SendAndFanOutAsync("after removal")));
    }

    // ── Delivery / failure isolation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task One_notification_is_produced_per_user_regardless_of_device_count()
    {
        var multiDevice = AddMemberWithDevices("919810044020", "Three Devices", 3);
        var messageId = await SendAndFanOutAsync("multi device check");

        // Fan-out to each device happens inside NotifyAsync; the user still gets a single inbox row.
        Assert.Single(ChatNotificationsFor(multiDevice, messageId));
    }

    [Fact]
    public async Task A_member_with_no_devices_still_gets_an_in_app_notification()
    {
        var noDevices = AddMemberWithDevices("919810044021", "No Devices", 0);
        var messageId = await SendAndFanOutAsync("no device check");

        // Push is one leg of the pipeline, not a precondition for it.
        Assert.Single(ChatNotificationsFor(noDevices, messageId));
    }

    [Fact]
    public async Task The_message_is_persisted_and_broadcast_even_if_notification_work_never_runs()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var sent = await chat.SendMessageAsync(_roomId, _senderId, "durable without push", null, Guid.NewGuid());
        Assert.True(sent.Ok, sent.Error);

        // The fan-out job is deliberately never invoked here: the message must stand on its own,
        // which is the point of enqueuing rather than awaiting notification work.
        var stored = db.ChatMessages.AsNoTracking().First(m => m.Id == sent.Value!.Id);
        Assert.Equal("durable without push", stored.Body);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Re_running_the_fan_out_re_notifies__documented_gap()
    {
        var messageId = await SeedMessageAsync("retry safety");
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<ChatNotificationJob>();

        await job.RunAsync(messageId, CancellationToken.None);
        Assert.Single(ChatNotificationsFor(_recipientId, messageId));

        // Hangfire retries a failed job, and the fan-out is NOT deduplicated: a job that fails partway
        // and retries re-notifies whoever it already reached. This asserts the real behaviour rather
        // than an aspiration — see the "remaining gaps" note in D-107.
        await job.RunAsync(messageId, CancellationToken.None);
        Assert.Equal(2, ChatNotificationsFor(_recipientId, messageId).Count);
    }

    [Fact]
    public async Task A_deleted_message_produces_no_notification()
    {
        var messageId = await SeedMessageAsync("deleted before fan-out");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        Assert.True((await chat.DeleteMessageAsync(messageId, _senderId)).Ok);

        await scope.ServiceProvider.GetRequiredService<ChatNotificationJob>()
            .RunAsync(messageId, CancellationToken.None);

        Assert.Empty(ChatNotificationsFor(_recipientId, messageId));
    }

    [Fact]
    public async Task Sending_a_message_enqueues_the_fan_out()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var sent = await chat.SendMessageAsync(_roomId, _senderId, "enqueued fan-out", null, Guid.NewGuid());
        Assert.True(sent.Ok, sent.Error);

        // Enqueued, not awaited — that is precisely what keeps notification work off the send path.
        Assert.True(_factory.BackgroundJobs.WasEnqueued(nameof(ChatNotificationJob.RunAsync), sent.Value!.Id),
            "sending a message should enqueue the chat notification fan-out");

        // And nothing was delivered synchronously as a side effect of sending.
        Assert.Empty(ChatNotificationsFor(_recipientId, sent.Value.Id));
    }

    // ── Moderation notices ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Moderation_notices_go_only_to_the_affected_member()
    {
        var target = AddMemberWithDevices("919810044030", "Mod Target", 1);
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.True((await chat.MuteMemberAsync(_roomId, _hostId, target, 15)).Ok);
        Assert.True((await chat.BanMemberAsync(_roomId, _hostId, target)).Ok);
        Assert.True((await chat.UnbanMemberAsync(_roomId, _hostId, target)).Ok);

        var kinds = db.Notifications.AsNoTracking().Where(n => n.UserId == target)
            .Select(n => n.Kind).ToList();
        Assert.Contains("chat_muted", kinds);
        Assert.Contains("chat_banned", kinds);
        Assert.Contains("chat_unbanned", kinds);

        // The rest of the room hears nothing about it.
        var others = db.Notifications.AsNoTracking()
            .Where(n => n.UserId != target && (n.Kind == "chat_muted" || n.Kind == "chat_banned" || n.Kind == "chat_unbanned"))
            .ToList();
        Assert.Empty(others);
    }

    // ── Room locked ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Locking_a_room_notifies_its_members_once()
    {
        // A dedicated event/room: locking the class-wide room would make every sibling test that
        // sends a message fail with room_locked.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();

        var member = new User { Name = "Lock Member", Phone = "919810044040" };
        db.Users.Add(member);
        var source = db.Events.AsNoTracking().First(e => e.Id == _eventId);
        var ev = new Event
        {
            RepresentingOrgId = source.RepresentingOrgId, CreatedBy = _hostId, CategoryId = source.CategoryId,
            Title = "Lock Event", Slug = "lock-event", ShortCode = "LCK001",
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        db.SaveChanges();

        var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
        db.ChatRooms.Add(room);
        db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = member.Id, Role = ChatMemberRole.Member });
        db.SaveChanges();

        await chat.LockRoomForEventAsync(ev.Id, "This event was cancelled. Chat is now read-only.");
        Assert.True(_factory.BackgroundJobs.WasEnqueued(nameof(ChatNotificationJob.NotifyRoomLockedAsync), room.Id));

        await scope.ServiceProvider.GetRequiredService<ChatNotificationJob>()
            .NotifyRoomLockedAsync(room.Id, CancellationToken.None);
        Assert.Contains(db.Notifications.AsNoTracking().Where(n => n.UserId == member.Id).ToList(),
            n => n.Kind == "chat_room_locked");

        var lockedAt = db.ChatRooms.AsNoTracking().First(r => r.Id == room.Id).LockedAt;
        Assert.NotNull(lockedAt);

        // A second lock attempt is a no-op, so the notification job is never enqueued twice —
        // that is what stops members being told the room closed more than once.
        _factory.BackgroundJobs.Clear();
        await chat.LockRoomForEventAsync(ev.Id, "second attempt");
        Assert.False(_factory.BackgroundJobs.WasEnqueued(nameof(ChatNotificationJob.NotifyRoomLockedAsync), room.Id));
        Assert.Equal(lockedAt, db.ChatRooms.AsNoTracking().First(r => r.Id == room.Id).LockedAt);
    }
}
