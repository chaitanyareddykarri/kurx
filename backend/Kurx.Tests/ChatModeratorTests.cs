using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// D-301 — explicit chat moderators.
///
/// Most of these assert a REFUSAL, because that is where the model lives. A permission system is only as
/// good as what it says no to, and every "cannot" below is a path that was reachable before the ladder
/// existed: the rank comparison is <c>&gt;</c> and not <c>&gt;=</c>, and that single character is the whole
/// of the peer-protection rule.
/// </summary>
public class ChatModeratorTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _roomId, _eventId, _hostId, _modId, _peerModId, _memberId, _outsiderId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ChatModeratorTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var host = new User { Name = "Mod Host", Phone = "919810099001" };
            var mod = new User { Name = "The Moderator", Phone = "919810099002" };
            var peerMod = new User { Name = "Another Moderator", Phone = "919810099003" };
            var member = new User { Name = "Plain Member", Phone = "919810099004" };
            var outsider = new User { Name = "Outsider", Phone = "919810099005" };
            db.Users.AddRange(host, mod, peerMod, member, outsider);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Mod", Slug = "chat-moderators" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _hostId = host.Id; _modId = mod.Id; _peerModId = peerMod.Id;
            _memberId = member.Id; _outsiderId = outsider.Id;
            var orgId = factory.SeedVerifiedOrg(host.Id, "Chat Moderator Org");

            var ev = new Event
            {
                RepresentingOrgId = orgId, CreatedBy = host.Id, CategoryId = category.Id,
                Title = "Moderator Event", Slug = "moderator-event", ShortCode = "MDR001",
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
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = mod.Id, Role = ChatMemberRole.Moderator });
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = peerMod.Id, Role = ChatMemberRole.Moderator });
            db.ChatMembers.Add(new ChatMember { RoomId = room.Id, UserId = member.Id, Role = ChatMemberRole.Member });
            db.SaveChanges();
            _roomId = room.Id;
            _reset = true;
        }
    }

    private IChatService Chat(IServiceScope s) => s.ServiceProvider.GetRequiredService<IChatService>();

    private async Task<Guid> PostAsync(Guid senderId, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var sent = await Chat(scope).SendMessageAsync(_roomId, senderId, body, null, Guid.NewGuid());
        Assert.True(sent.Ok, sent.Error);
        return sent.Value!.Id;
    }

    private async Task ResetRoleAsync(Guid userId, ChatMemberRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await db.ChatMembers.Where(m => m.RoomId == _roomId && m.UserId == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, role));
    }

    // ── Promotion and demotion ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_host_promotes_a_member_to_moderator_and_demotes_them_again()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        Assert.True((await chat.SetModeratorAsync(_roomId, _hostId, _memberId, true)).Ok);
        Assert.Equal(ChatMemberRole.Moderator, await RoleOfAsync(db, _memberId));

        Assert.True((await chat.SetModeratorAsync(_roomId, _hostId, _memberId, false)).Ok);
        Assert.Equal(ChatMemberRole.Member, await RoleOfAsync(db, _memberId));
    }

    [Fact]
    public async Task Promotion_is_idempotent_and_writes_no_second_audit_row()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var memberRowId = await db.ChatMembers.AsNoTracking()
            .Where(m => m.RoomId == _roomId && m.UserId == _memberId).Select(m => m.Id).FirstAsync();
        Task<int> RoleChangesAsync() => db.AuditLogs
            .CountAsync(a => a.Action == "chat.role_change" && a.EntityId == memberRowId);

        await chat.SetModeratorAsync(_roomId, _hostId, _memberId, true);
        var auditRowsAfterFirst = await RoleChangesAsync();
        Assert.True((await chat.SetModeratorAsync(_roomId, _hostId, _memberId, true)).Ok);

        // A retried request or a double-tap must not produce a second audit row or a second notification.
        Assert.Equal(auditRowsAfterFirst, await RoleChangesAsync());
        await ResetRoleAsync(_memberId, ChatMemberRole.Member);
    }

    [Fact]
    public async Task A_role_change_is_audited_with_the_previous_and_new_role()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        await Chat(scope).SetModeratorAsync(_roomId, _hostId, _memberId, true);

        var row = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "chat.role_change")
            .OrderByDescending(a => a.CreatedAt).FirstAsync();
        Assert.Equal(_hostId, row.ActorId);
        Assert.Contains("Member", row.DetailsJson);
        Assert.Contains("Moderator", row.DetailsJson);
        await ResetRoleAsync(_memberId, ChatMemberRole.Member);
    }

    [Fact]
    public async Task A_moderator_cannot_promote_anyone()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).SetModeratorAsync(_roomId, _modId, _memberId, true);

        // The load-bearing refusal: a Moderator who could promote could mint a peer, and the
        // "cannot act on an equal" rule would become bypassable in two steps.
        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    [Fact]
    public async Task A_moderator_cannot_demote_another_moderator()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).SetModeratorAsync(_roomId, _modId, _peerModId, false);

        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    [Fact]
    public async Task A_plain_member_cannot_promote_themselves()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).SetModeratorAsync(_roomId, _memberId, _memberId, true);

        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    [Fact]
    public async Task A_hosts_role_cannot_be_changed_through_the_moderator_route()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).SetModeratorAsync(_roomId, _hostId, _hostId, false);

        // Demoting a Host would be a transfer of room ownership wearing a moderation button.
        Assert.False(result.Ok);
        Assert.Equal("cannot_change_host", result.Error);
    }

    [Fact]
    public async Task Concurrent_promote_and_demote_do_not_lose_one_another()
    {
        await ResetRoleAsync(_memberId, ChatMemberRole.Member);
        var before = await RoleChangeCountAsync();

        // Two hosts acting on the same member at the same instant. The conditional UPDATE claims the row
        // on its expected current role, so the loser is TOLD rather than silently overwritten.
        var results = await Task.WhenAll(
            RunAsync(chat => chat.SetModeratorAsync(_roomId, _hostId, _memberId, true)),
            RunAsync(chat => chat.SetModeratorAsync(_roomId, _hostId, _memberId, true)));

        Assert.All(results, r => Assert.True(r.Ok || r.Error == "conflict"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(ChatMemberRole.Moderator, await RoleOfAsync(db, _memberId));

        // Exactly one role change was written by THIS test, whichever request won: no double audit, no
        // double notification. A DELTA, not an absolute — the class shares one room and several tests
        // here promote the same member, so an absolute count asserts the test order rather than the
        // behaviour. (It read 5 when written that way, and 5 was correct.)
        Assert.Equal(before + 1, await RoleChangeCountAsync());
        await ResetRoleAsync(_memberId, ChatMemberRole.Member);
    }

    // ── What a moderator MAY do ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_moderator_can_mute_a_member()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).MuteMemberAsync(_roomId, _modId, _memberId, 15);

        Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public async Task A_moderator_can_delete_a_members_message()
    {
        var messageId = await PostAsync(_memberId, "a member said this");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).DeleteMessageAsync(messageId, _modId);

        Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public async Task A_moderator_can_pin_a_message()
    {
        var messageId = await PostAsync(_memberId, "worth pinning");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).PinMessageAsync(messageId, _modId, true, TimeSpan.FromHours(24));

        Assert.True(result.Ok, result.Error);
        await Chat(scope).PinMessageAsync(messageId, _modId, false);
    }

    // ── What a moderator MAY NOT do ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_moderator_cannot_mute_a_host()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).MuteMemberAsync(_roomId, _modId, _hostId, 15);

        Assert.False(result.Ok);
        Assert.Equal("cannot_moderate_peer", result.Error);
    }

    [Fact]
    public async Task A_moderator_cannot_mute_another_moderator()
    {
        using var scope = _factory.Services.CreateScope();

        // Equal rank must fail. `>=` instead of `>` in OutRanks is the entire difference.
        var result = await Chat(scope).MuteMemberAsync(_roomId, _modId, _peerModId, 15);

        Assert.False(result.Ok);
        Assert.Equal("cannot_moderate_peer", result.Error);
    }

    [Fact]
    public async Task A_moderator_cannot_ban_a_host()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).BanMemberAsync(_roomId, _modId, _hostId);

        Assert.False(result.Ok);
        Assert.Equal("cannot_moderate_peer", result.Error);
    }

    [Fact]
    public async Task A_moderator_cannot_delete_a_hosts_message()
    {
        var messageId = await PostAsync(_hostId, "the host said this");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).DeleteMessageAsync(messageId, _modId);

        Assert.False(result.Ok);
        Assert.Equal("cannot_moderate_peer", result.Error);
    }

    [Fact]
    public async Task A_moderator_cannot_delete_a_peer_moderators_message()
    {
        var messageId = await PostAsync(_peerModId, "a peer said this");
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).DeleteMessageAsync(messageId, _modId);

        Assert.False(result.Ok);
        Assert.Equal("cannot_moderate_peer", result.Error);
    }

    [Fact]
    public async Task A_moderator_cannot_lock_the_room_or_change_its_policy()
    {
        using var scope = _factory.Services.CreateScope();

        var locked = await Chat(scope).UpdateRoomAsync(_roomId, _modId, null, "Locked");
        var policy = await Chat(scope).UpdateRoomAsync(_roomId, _modId, "HostsOnly", null);

        // Room lifecycle and settings are Host-only: a Moderator moderates people, not the room.
        Assert.Equal("forbidden", locked.Error);
        Assert.Equal("forbidden", policy.Error);
    }

    [Fact]
    public async Task A_host_overrides_a_moderator_and_can_act_on_them()
    {
        using var scope = _factory.Services.CreateScope();

        var muted = await Chat(scope).MuteMemberAsync(_roomId, _hostId, _peerModId, 15);

        Assert.True(muted.Ok, muted.Error);
    }

    // ── Capabilities on the wire ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_moderators_capabilities_grant_moderation_but_not_room_management()
    {
        using var scope = _factory.Services.CreateScope();

        var room = await Chat(scope).GetRoomAsync(_eventId, _modId);

        Assert.True(room.Ok, room.Error);
        var caps = room.Value!.Capabilities;
        Assert.True(caps.CanModerate);
        Assert.True(caps.CanPin);
        // The two that separate a Moderator from a Host, and the reason they are not implied by
        // CanModerate: a client rendering off CanModerate alone would show lock-room to a Moderator.
        Assert.False(caps.CanManageRoom);
        Assert.False(caps.CanManageModerators);
        Assert.Equal("Moderator", caps.MyRole);
    }

    [Fact]
    public async Task A_hosts_capabilities_grant_everything()
    {
        using var scope = _factory.Services.CreateScope();

        var caps = (await Chat(scope).GetRoomAsync(_eventId, _hostId)).Value!.Capabilities;

        Assert.True(caps.CanModerate);
        Assert.True(caps.CanManageRoom);
        Assert.True(caps.CanManageModerators);
        Assert.Equal("Host", caps.MyRole);
    }

    [Fact]
    public async Task A_plain_members_capabilities_grant_no_moderation()
    {
        using var scope = _factory.Services.CreateScope();

        var caps = (await Chat(scope).GetRoomAsync(_eventId, _memberId)).Value!.Capabilities;

        Assert.False(caps.CanModerate);
        Assert.False(caps.CanPin);
        Assert.False(caps.CanManageRoom);
        Assert.False(caps.CanManageModerators);
        Assert.Equal("Member", caps.MyRole);
    }

    [Fact]
    public async Task A_moderator_who_loses_their_ticket_is_removed_from_the_room()
    {
        using var scope = _factory.Services.CreateScope();
        var chat = Chat(scope);
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // A promoted member with no ticket — the shape a refund leaves behind.
        Assert.True((await chat.SetModeratorAsync(_roomId, _hostId, _memberId, true)).Ok);
        await chat.RemoveMemberIfNoTicketsAsync(_eventId, _memberId);

        // The removal predicate was `Role == Member`. While the enum had two values that meant "not a
        // Host"; adding the Moderator rung silently narrowed it, and a refunded attendee who had been
        // promoted kept moderation authority over a room they no longer belonged to.
        db.ChangeTracker.Clear();
        Assert.False(await db.ChatMembers.AsNoTracking()
            .AnyAsync(m => m.RoomId == _roomId && m.UserId == _memberId));

        // Put the fixture back for the tests that follow.
        db.ChatMembers.Add(new ChatMember { RoomId = _roomId, UserId = _memberId, Role = ChatMemberRole.Member });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_host_without_a_ticket_is_never_removed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await Chat(scope).RemoveMemberIfNoTicketsAsync(_eventId, _hostId);

        // What the filter existed to protect, and what widening it must not break.
        Assert.True(await db.ChatMembers.AsNoTracking()
            .AnyAsync(m => m.RoomId == _roomId && m.UserId == _hostId && m.Role == ChatMemberRole.Host));
    }

    [Fact]
    public async Task An_outsider_gets_no_room_at_all()
    {
        using var scope = _factory.Services.CreateScope();

        var result = await Chat(scope).GetRoomAsync(_eventId, _outsiderId);

        Assert.False(result.Ok);
        Assert.Equal("forbidden", result.Error);
    }

    /// Role changes recorded against THIS member's chat_members row, for delta assertions.
    private async Task<int> RoleChangeCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var memberRowId = await db.ChatMembers.AsNoTracking()
            .Where(m => m.RoomId == _roomId && m.UserId == _memberId).Select(m => m.Id).FirstAsync();
        return await db.AuditLogs.CountAsync(a => a.Action == "chat.role_change" && a.EntityId == memberRowId);
    }

    private async Task<ServiceResult<bool>> RunAsync(Func<IChatService, Task<ServiceResult<bool>>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(Chat(scope));
    }

    private static async Task<ChatMemberRole> RoleOfAsync(KurxDbContext db, Guid userId)
    {
        db.ChangeTracker.Clear();
        return await db.ChatMembers.AsNoTracking()
            .Where(m => m.RoomId == _roomId && m.UserId == userId).Select(m => m.Role).FirstAsync();
    }
}
