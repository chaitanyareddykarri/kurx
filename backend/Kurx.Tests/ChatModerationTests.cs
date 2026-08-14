using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Phase 2B: chat moderation must use the platform's single moderation queue, the single notification
/// pipeline, and the typed audit spine — not private copies of any of them.
/// </summary>
public class ChatModerationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _roomId, _eventId, _hostId, _memberId, _reporterId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ChatModerationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var host = new User { Name = "Mod Host", Phone = "919810055001" };
            var member = new User { Name = "Mod Member", Phone = "919810055002" };
            var reporter = new User { Name = "Mod Reporter", Phone = "919810055003" };
            db.Users.AddRange(host, member, reporter);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Mod", Slug = "chat-moderation" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _hostId = host.Id; _memberId = member.Id; _reporterId = reporter.Id;
            var orgId = factory.SeedVerifiedOrg(host.Id, "Chat Moderation Org");

            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = host.Id, CategoryId = category.Id,
                Title = "Moderation Event", Slug = "moderation-event", ShortCode = "MOD001",
                Description = "d", VenueName = "v",
                StartsAt = DateTime.UtcNow.AddDays(5), EndsAt = DateTime.UtcNow.AddDays(6),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            db.SaveChanges();
            _eventId = ev.Id;

            var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
            db.ChatRooms.Add(room);
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = host.Id, Role = ChatMemberRole.Host });
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = member.Id, Role = ChatMemberRole.Member });
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = reporter.Id, Role = ChatMemberRole.Member });
            db.SaveChanges();
            _roomId = room.Id;
            _reset = true;
        }
    }

    private async Task<Guid> PostMessageAsync(string body)
    {
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var sent = await chat.SendMessageAsync(_roomId, _memberId, body, null, Guid.NewGuid());
        Assert.True(sent.Ok, sent.Error);
        return sent.Value!.Id;
    }

    // ── Reporting goes to the platform moderation queue ────────────────────────────────────────

    [Fact]
    public async Task Reporting_a_message_files_into_the_platform_moderation_queue()
    {
        var messageId = await PostMessageAsync("something rude");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();

        var reported = await chat.ReportMessageAsync(messageId, _reporterId, "Harassment");
        Assert.True(reported.Ok, reported.Error);

        // The regression: chat used to write an audit row nobody could triage, leaving the
        // already-whitelisted chat_message entity type unused.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var report = Assert.Single(db.Reports.AsNoTracking()
            .Where(r => r.EntityType == "chat_message" && r.EntityId == messageId).ToList());
        Assert.Equal("open", report.Status);
        Assert.Equal(_reporterId, report.ReporterId);

        // And it is visible to moderation staff through the normal triage queue.
        var queue = await scope.ServiceProvider.GetRequiredService<IReportService>().ListAsync("open", 50);
        Assert.Contains(queue, r => r.EntityId == messageId);
    }

    [Fact]
    public async Task Reporting_the_same_message_twice_is_refused_rather_than_flooding_the_queue()
    {
        var messageId = await PostMessageAsync("duplicate report target");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();

        Assert.True((await chat.ReportMessageAsync(messageId, _reporterId, "Spam")).Ok);
        var second = await chat.ReportMessageAsync(messageId, _reporterId, "Spam");

        Assert.False(second.Ok);
        Assert.Equal("already_reported", second.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, db.Reports.AsNoTracking().Count(r => r.EntityId == messageId));
    }

    [Fact]
    public async Task Reporting_notifies_hosts_through_the_shared_notification_pipeline()
    {
        var messageId = await PostMessageAsync("notify the hosts");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        Assert.True((await chat.ReportMessageAsync(messageId, _reporterId, "Inappropriate")).Ok);

        // NotifyAsync is the single pipeline: it writes this row, broadcasts over SignalR, refreshes
        // the unread badge and fans out to FCM. The old raw Notifications.Add did only the first.
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var inbox = await notifications.ListAsync(_hostId, 1, 50);
        // Scoped to this message — sibling tests in this class report other messages to the same host.
        var alert = Assert.Single(inbox.Where(n =>
            n.Kind == "chat_report" && n.DataJson != null && n.DataJson.Contains(messageId.ToString())));

        // Unread count is maintained, which the bypassed path never did.
        Assert.True(await notifications.GetUnreadCountAsync(_hostId) > 0);

        // Data payload is serialized, not string-interpolated — a quote in user input cannot break it.
        Assert.NotNull(alert.DataJson);
        Assert.Contains(messageId.ToString(), alert.DataJson);

        // A plain member is not told about reports.
        Assert.Empty((await notifications.ListAsync(_memberId, 1, 50)).Where(n => n.Kind == "chat_report"));
    }

    [Fact]
    public async Task A_reason_containing_quotes_cannot_corrupt_the_stored_record()
    {
        var messageId = await PostMessageAsync("injection target");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();

        // The old code interpolated this straight into a JSON string literal.
        var hostile = "Spam\", \"injected\": \"yes";
        Assert.True((await chat.ReportMessageAsync(messageId, _reporterId, hostile)).Ok);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var report = Assert.Single(db.Reports.AsNoTracking().Where(r => r.EntityId == messageId).ToList());
        Assert.Equal(hostile, report.Reason);   // stored verbatim as data, never as structure

        var alert = Assert.Single((await scope.ServiceProvider.GetRequiredService<INotificationService>()
            .ListAsync(_hostId, 1, 50)).Where(n => n.DataJson != null && n.DataJson.Contains(messageId.ToString())));
        using var parsed = System.Text.Json.JsonDocument.Parse(alert.DataJson!);
        Assert.False(parsed.RootElement.TryGetProperty("injected", out _));
    }

    // ── Audit spine ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Moderation_actions_are_written_through_the_typed_audit_spine()
    {
        var messageId = await PostMessageAsync("to be deleted");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Mute a throwaway user: muting the shared _memberId would silence it for sibling tests,
        // which run in the same class against the same seeded room.
        var muteTarget = new User { Name = "Mute Target", Phone = "919810055008" };
        db.Users.Add(muteTarget);
        db.SaveChanges();
        db.ChatMembers.Add(new ChatMember { RoomId = _roomId, UserId = muteTarget.Id, Role = ChatMemberRole.Member });
        db.SaveChanges();

        Assert.True((await chat.DeleteMessageAsync(messageId, _hostId)).Ok);
        Assert.True((await chat.MuteMemberAsync(_roomId, _hostId, muteTarget.Id, 30)).Ok);

        foreach (var action in new[] { "chat.message_delete", "chat.mute" })
        {
            var row = db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == action).OrderByDescending(a => a.CreatedAt).First();
            Assert.NotNull(row.DetailsJson);

            // D-102 envelope: { v, correlation_id?, before, after }. Before/after is what makes the
            // trail readable; the old hand-rolled rows had neither.
            using var doc = System.Text.Json.JsonDocument.Parse(row.DetailsJson!);
            Assert.True(doc.RootElement.TryGetProperty("v", out _), $"{action} missing envelope version");
            Assert.True(doc.RootElement.TryGetProperty("before", out _), $"{action} missing before");
            Assert.True(doc.RootElement.TryGetProperty("after", out _), $"{action} missing after");
        }
    }

    [Fact]
    public async Task Deleting_a_message_commits_its_audit_row_in_the_same_transaction()
    {
        var messageId = await PostMessageAsync("audited delete");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.True((await chat.DeleteMessageAsync(messageId, _hostId)).Ok);

        // Both effects are present; previously these were two separate SaveChanges calls, so a
        // failure between them left a deleted message with no audit trail.
        Assert.True(db.ChatMessages.AsNoTracking().First(m => m.Id == messageId).IsDeleted);
        Assert.Contains(db.AuditLogs.AsNoTracking().Where(a => a.Action == "chat.message_delete").ToList(),
            a => a.EntityId == messageId);
    }

    // ── Ban ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Banning_blocks_posting_and_reading_and_is_reversible()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var target = new User { Name = "Banned One", Phone = "919810055009" };
        db.Users.Add(target);
        db.SaveChanges();
        db.ChatMembers.Add(new ChatMember { RoomId = _roomId, UserId = target.Id, Role = ChatMemberRole.Member });
        db.SaveChanges();

        Assert.True((await chat.BanMemberAsync(_roomId, _hostId, target.Id)).Ok);

        Assert.Equal("banned", (await chat.SendMessageAsync(_roomId, target.Id, "let me in", null, Guid.NewGuid())).Error);
        Assert.Equal("banned", (await chat.GetMessagesAsync(_roomId, target.Id, null, null, 10)).Error);

        // Ban and unban are both audited.
        Assert.Contains(db.AuditLogs.AsNoTracking().Where(a => a.Action == "chat.ban").ToList(),
            a => a.ActorId == _hostId);

        Assert.True((await chat.UnbanMemberAsync(_roomId, _hostId, target.Id)).Ok);
        Assert.True((await chat.GetMessagesAsync(_roomId, target.Id, null, null, 10)).Ok);
        Assert.Contains(db.AuditLogs.AsNoTracking().Where(a => a.Action == "chat.unban").ToList(),
            a => a.ActorId == _hostId);
    }

    [Fact]
    public async Task Only_hosts_can_moderate()
    {
        var messageId = await PostMessageAsync("member cannot moderate this");
        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();

        Assert.Equal("forbidden", (await chat.BanMemberAsync(_roomId, _memberId, _reporterId)).Error);
        Assert.Equal("forbidden", (await chat.MuteMemberAsync(_roomId, _memberId, _reporterId, 10)).Error);
        Assert.Equal("forbidden", (await chat.PinMessageAsync(messageId, _reporterId, true)).Error);
        Assert.Equal("forbidden", (await chat.UpdateRoomAsync(_roomId, _memberId, "HostsOnly", null)).Error);
    }
}
