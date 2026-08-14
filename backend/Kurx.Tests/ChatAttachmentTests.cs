using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Chat;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Phase 4 (D-110): attachments are metadata on a ChatMessage, and every authoritative check happens
/// on confirm — the first moment the server can look at the actual bytes.
///
/// The tests that matter most are the ones asserting that a lie is caught: a declared content type
/// that the magic bytes contradict, an extension that does not match, a key from another room, a
/// file someone else uploaded.
/// </summary>
public class ChatAttachmentTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _roomId, _eventId, _hostId, _memberId, _otherId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ChatAttachmentTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var host = new User { Name = "Att Host", Phone = "919810033001" };
            var member = new User { Name = "Att Member", Phone = "919810033002" };
            var other = new User { Name = "Att Other", Phone = "919810033003" };
            db.Users.AddRange(host, member, other);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Att", Slug = "chat-attach" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _hostId = host.Id; _memberId = member.Id; _otherId = other.Id;
            var orgId = factory.SeedVerifiedOrg(host.Id, "Chat Attach Org");

            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = host.Id, CategoryId = category.Id,
                Title = "Attach Event", Slug = "attach-event", ShortCode = "ATT001",
                Description = "d", VenueName = "v",
                StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            db.SaveChanges();
            _eventId = ev.Id;

            var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
            db.ChatRooms.Add(room);
            db.ChatMembers.AddRange(
                new ChatMember { RoomId = room.Id, UserId = host.Id, Role = ChatMemberRole.Host },
                new ChatMember { RoomId = room.Id, UserId = member.Id, Role = ChatMemberRole.Member },
                new ChatMember { RoomId = room.Id, UserId = other.Id, Role = ChatMemberRole.Member });
            db.SaveChanges();
            _roomId = room.Id;
            _reset = true;
        }
    }

    // ── fixtures ───────────────────────────────────────────────────────────────────────────────

    /// <summary>A real 1x1 PNG. Byte-accurate because the whole point is that the server decodes it.</summary>
    private static byte[] Png() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static byte[] Pdf() => "%PDF-1.4\n%%EOF\n"u8.ToArray();
    private static byte[] Text() => "hello, attachment\n"u8.ToArray();
    private static byte[] Executable() => [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];  // MZ — a PE binary

    private IChatService Chat(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IChatService>();

    /// <summary>Runs the real flow: presign, PUT the bytes through IStorage, confirm.</summary>
    private async Task<ServiceResult<ChatAttachmentView>> UploadAsync(
        IServiceScope scope, byte[] bytes, string contentType, string fileName, Guid? asUser = null)
    {
        var chat = Chat(scope);
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var userId = asUser ?? _memberId;

        var ticket = await chat.PresignAttachmentAsync(_roomId, userId, fileName, contentType, bytes.Length);
        if (!ticket.Ok) return ServiceResult<ChatAttachmentView>.Fail(ticket.Error ?? "presign_failed");

        await storage.PutAsync(ticket.Value!.Key, bytes, contentType);
        return await chat.ConfirmAttachmentAsync(_roomId, userId, ticket.Value.Key);
    }

    // ── happy path ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_image_upload_is_confirmed_with_server_extracted_dimensions()
    {
        using var scope = _factory.Services.CreateScope();
        var result = await UploadAsync(scope, Png(), "image/png", "photo.png");

        Assert.True(result.Ok, result.Error);
        Assert.Equal("image/png", result.Value!.ContentType);
        // Dimensions come from decoding the bytes, never from the client.
        Assert.Equal(1, result.Value.Width);
        Assert.Equal(1, result.Value.Height);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Url));
    }

    [Fact]
    public async Task The_original_filename_survives_presign_and_confirm()
    {
        using var scope = _factory.Services.CreateScope();
        var result = await UploadAsync(scope, Pdf(), "application/pdf", "Q3 Report (final).pdf");

        Assert.True(result.Ok, result.Error);
        // Regression (D-113): confirm receives only a storage key, so the name has to be carried in
        // it. This previously rendered as a GUID on both clients.
        Assert.Equal("Q3 Report (final).pdf", result.Value!.FileName);
    }

    [Fact]
    public async Task A_filename_containing_path_separators_cannot_escape_the_room_prefix()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var ticket = await chat.PresignAttachmentAsync(
            _roomId, _memberId, "../../etc/passwd.txt", "text/plain", 32);

        Assert.True(ticket.Ok, ticket.Error);
        // Sanitisation strips the traversal before the key is built, so the key stays inside the room.
        Assert.StartsWith($"chat/{_roomId}/", ticket.Value!.Key);
        Assert.DoesNotContain("..", ticket.Value.Key);
    }

    [Fact]
    public async Task Documents_and_text_are_accepted_without_dimensions()
    {
        using var scope = _factory.Services.CreateScope();

        var pdf = await UploadAsync(scope, Pdf(), "application/pdf", "invoice.pdf");
        Assert.True(pdf.Ok, pdf.Error);
        Assert.Null(pdf.Value!.Width);

        var text = await UploadAsync(scope, Text(), "text/plain", "notes.txt");
        Assert.True(text.Ok, text.Error);
    }

    [Fact]
    public async Task Confirm_is_idempotent_for_the_same_storage_key()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var ticket = await chat.PresignAttachmentAsync(_roomId, _memberId, "a.png", "image/png", Png().Length);
        await storage.PutAsync(ticket.Value!.Key, Png(), "image/png");

        var first = await chat.ConfirmAttachmentAsync(_roomId, _memberId, ticket.Value.Key);
        var second = await chat.ConfirmAttachmentAsync(_roomId, _memberId, ticket.Value.Key);

        Assert.True(first.Ok, first.Error);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(first.Value!.Id, second.Value!.Id);   // one row, not two over the same bytes
    }

    // ── content validation: the client is not trusted ──────────────────────────────────────────

    [Fact]
    public async Task An_executable_renamed_as_an_image_is_refused()
    {
        using var scope = _factory.Services.CreateScope();

        // Declares image/png with a .png extension; the bytes are a PE binary. Only the magic-byte
        // check catches this, which is why the allow-list alone is not enough.
        var result = await UploadAsync(scope, Executable(), "image/png", "totally-a.png");

        Assert.False(result.Ok);
        Assert.Equal("unsupported_file_type", result.Error);
    }

    [Fact]
    public async Task A_disallowed_content_type_is_refused_at_presign()
    {
        using var scope = _factory.Services.CreateScope();
        var result = await Chat(scope).PresignAttachmentAsync(
            _roomId, _memberId, "payload.exe", "application/x-msdownload", 1024);

        Assert.False(result.Ok);
        Assert.Equal("unsupported_file_type", result.Error);
    }

    [Fact]
    public async Task An_extension_that_contradicts_the_content_type_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var result = await Chat(scope).PresignAttachmentAsync(
            _roomId, _memberId, "invoice.pdf.exe", "application/pdf", 1024);

        Assert.False(result.Ok);
        Assert.Equal("extension_mismatch", result.Error);
    }

    [Fact]
    public async Task A_file_over_the_size_ceiling_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        var result = await Chat(scope).PresignAttachmentAsync(
            _roomId, _memberId, "huge.png", "image/png", AttachmentPolicy.MaxBytes + 1);

        Assert.False(result.Ok);
        Assert.Equal("file_too_large", result.Error);
    }

    [Fact]
    public void Policy_rejects_a_binary_wearing_a_text_extension()
    {
        // Text has no magic number, so the NUL check is what distinguishes it from a binary.
        Assert.Null(AttachmentPolicy.Inspect([0x00, 0x01, 0x02], "text/plain"));
        Assert.NotNull(AttachmentPolicy.Inspect(Text(), "text/plain"));
    }

    // ── authorization and ownership ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_non_member_cannot_presign_an_upload()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var outsider = new User { Name = "Outsider", Phone = "919810033099" };
        db.Users.Add(outsider);
        db.SaveChanges();

        var result = await Chat(scope).PresignAttachmentAsync(_roomId, outsider.Id, "a.png", "image/png", 100);
        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    [Fact]
    public async Task A_storage_key_from_another_room_cannot_be_confirmed()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        // A well-formed key that simply belongs to a different room.
        var foreignKey = $"chat/{Guid.NewGuid()}/{Guid.NewGuid():N}";
        await storage.PutAsync(foreignKey, Png(), "image/png");

        var result = await Chat(scope).ConfirmAttachmentAsync(_roomId, _memberId, foreignKey);
        Assert.False(result.Ok);
        Assert.Equal("invalid_storage_key", result.Error);
    }

    [Fact]
    public async Task Another_users_upload_cannot_be_attached_to_your_message()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var theirs = await UploadAsync(scope, Png(), "image/png", "theirs.png", asUser: _otherId);
        Assert.True(theirs.Ok, theirs.Error);

        var stolen = await chat.SendMessageAsync(
            _roomId, _memberId, "look at this", null, Guid.NewGuid(), [theirs.Value!.Id]);

        Assert.False(stolen.Ok);
        Assert.Equal("invalid_attachment", stolen.Error);
    }

    [Fact]
    public async Task A_banned_member_cannot_fetch_an_attachment_url()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var uploaded = await UploadAsync(scope, Png(), "image/png", "shared.png");
        Assert.True(uploaded.Ok, uploaded.Error);

        var viewer = new User { Name = "Banned Viewer", Phone = "919810033010" };
        db.Users.Add(viewer);
        db.SaveChanges();
        db.ChatMembers.Add(new ChatMember { RoomId = _roomId, UserId = viewer.Id, Role = ChatMemberRole.Member });
        db.SaveChanges();

        Assert.True((await chat.GetAttachmentUrlAsync(uploaded.Value!.Id, viewer.Id)).Ok);
        Assert.True((await chat.BanMemberAsync(_roomId, _hostId, viewer.Id)).Ok);

        // Membership is re-checked on every URL mint, so a ban revokes file access immediately.
        var afterBan = await chat.GetAttachmentUrlAsync(uploaded.Value.Id, viewer.Id);
        Assert.False(afterBan.Ok);
        Assert.Equal("forbidden", afterBan.Error);
    }

    // ── message integration ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_attachment_is_claimed_by_the_message_and_appears_on_it()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var uploaded = await UploadAsync(scope, Png(), "image/png", "claimed.png");
        var sent = await chat.SendMessageAsync(_roomId, _memberId, "with a photo", null, Guid.NewGuid(),
            [uploaded.Value!.Id]);

        Assert.True(sent.Ok, sent.Error);
        Assert.Single(sent.Value!.Attachments);
        Assert.Equal(sent.Value.Id, db.ChatAttachments.AsNoTracking().First(a => a.Id == uploaded.Value.Id).MessageId);

        // And it comes back on the read path with a fresh signed URL.
        var page = await chat.GetMessagesAsync(_roomId, _memberId, null, null, 50);
        var view = page.Value!.Messages.Single(m => m.Id == sent.Value.Id);
        Assert.Single(view.Attachments);
        Assert.False(string.IsNullOrWhiteSpace(view.Attachments[0].Url));
    }

    [Fact]
    public async Task An_attachment_can_only_be_claimed_once()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var uploaded = await UploadAsync(scope, Png(), "image/png", "once.png");
        Assert.True((await chat.SendMessageAsync(_roomId, _memberId, "first", null, Guid.NewGuid(), [uploaded.Value!.Id])).Ok);

        var second = await chat.SendMessageAsync(_roomId, _memberId, "second", null, Guid.NewGuid(), [uploaded.Value.Id]);
        Assert.False(second.Ok);
        Assert.Equal("invalid_attachment", second.Error);
    }

    [Fact]
    public async Task A_message_carrying_a_file_may_have_an_empty_body()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var uploaded = await UploadAsync(scope, Png(), "image/png", "wordless.png");
        var sent = await chat.SendMessageAsync(_roomId, _memberId, "", null, Guid.NewGuid(), [uploaded.Value!.Id]);

        Assert.True(sent.Ok, sent.Error);   // the attachment IS the content
        Assert.Single(sent.Value!.Attachments);

        // A message with neither body nor attachment is still refused.
        Assert.Equal("body_required",
            (await chat.SendMessageAsync(_roomId, _memberId, "   ", null, Guid.NewGuid())).Error);
    }

    // ── lifecycle: deletion and cleanup ────────────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_message_hides_its_attachments_and_the_sweep_removes_the_objects()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var uploaded = await UploadAsync(scope, Png(), "image/png", "doomed.png");
        var sent = await chat.SendMessageAsync(_roomId, _memberId, "delete me", null, Guid.NewGuid(),
            [uploaded.Value!.Id]);
        var storageKey = db.ChatAttachments.AsNoTracking().First(a => a.Id == uploaded.Value.Id).StorageKey;
        Assert.True(await storage.ExistsAsync(storageKey));

        Assert.True((await chat.DeleteMessageAsync(sent.Value!.Id, _memberId)).Ok);

        // Hidden immediately, even though the object is still there.
        var page = await chat.GetMessagesAsync(_roomId, _memberId, null, null, 50);
        Assert.Empty(page.Value!.Messages.Single(m => m.Id == sent.Value.Id).Attachments);

        await scope.ServiceProvider.GetRequiredService<ChatAttachmentCleanupJob>().RunAsync(CancellationToken.None);

        // No orphaned object, and no orphaned row.
        Assert.False(await storage.ExistsAsync(storageKey));
        Assert.Empty(db.ChatAttachments.AsNoTracking().Where(a => a.Id == uploaded.Value.Id).ToList());
    }

    [Fact]
    public async Task An_upload_never_attached_to_a_message_is_swept_once_it_ages_out()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var uploaded = await UploadAsync(scope, Png(), "image/png", "orphan.png");
        var row = db.ChatAttachments.First(a => a.Id == uploaded.Value!.Id);
        var storageKey = row.StorageKey;

        // A fresh orphan is inside the grace window — an upload still on its way to a send must not
        // be swept out from under the user.
        await scope.ServiceProvider.GetRequiredService<ChatAttachmentCleanupJob>().RunAsync(CancellationToken.None);
        Assert.True(await storage.ExistsAsync(storageKey));

        row.CreatedAt = DateTime.UtcNow.AddDays(-2);
        db.SaveChanges();

        await scope.ServiceProvider.GetRequiredService<ChatAttachmentCleanupJob>().RunAsync(CancellationToken.None);
        Assert.False(await storage.ExistsAsync(storageKey));
    }

    [Fact]
    public async Task The_cleanup_sweep_is_safe_to_re_run()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<ChatAttachmentCleanupJob>();

        await job.RunAsync(CancellationToken.None);
        await job.RunAsync(CancellationToken.None);   // must not throw on an empty sweep
    }

    // ── capability + notification compatibility ────────────────────────────────────────────────

    [Fact]
    public async Task CanUpload_follows_the_ability_to_post()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);

        var before = await chat.GetRoomAsync(_eventId, _memberId);
        Assert.True(before.Value!.Capabilities.CanUpload);

        Assert.True((await chat.UpdateRoomAsync(_roomId, _hostId, "HostsOnly", null)).Ok);
        var underHostsOnly = await chat.GetRoomAsync(_eventId, _memberId);

        // A member who may not post may not upload — there is no separate upload permission.
        Assert.False(underHostsOnly.Value!.Capabilities.CanPost);
        Assert.False(underHostsOnly.Value.Capabilities.CanUpload);
        Assert.Equal("upload_not_allowed",
            (await chat.PresignAttachmentAsync(_roomId, _memberId, "a.png", "image/png", 100)).Error);

        await chat.UpdateRoomAsync(_roomId, _hostId, "Everyone", null);
    }

    [Fact]
    public async Task A_message_with_an_attachment_notifies_exactly_like_any_other()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var uploaded = await UploadAsync(scope, Png(), "image/png", "notify.png");
        var sent = await chat.SendMessageAsync(_roomId, _memberId, "see this", null, Guid.NewGuid(),
            [uploaded.Value!.Id]);
        Assert.True(sent.Ok, sent.Error);

        // No attachment-specific notification path: the existing fan-out handles it unchanged.
        await scope.ServiceProvider.GetRequiredService<ChatNotificationJob>()
            .RunAsync(sent.Value!.Id, CancellationToken.None);

        var notified = db.Notifications.AsNoTracking().Where(n => n.UserId == _hostId).ToList();
        Assert.Contains(notified, n => n.Kind == "chat_message" || n.Kind == "chat_announcement");
    }
}
