using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using System.Linq;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Phase 2A: chat membership must always reflect actual eligibility. Every lifecycle hook wired in this
/// phase is exercised here, and each one is run <b>twice</b> — the bugs being guarded against are
/// duplicate members, duplicate system messages, and evicting a member who is still entitled.
/// </summary>
public class ChatLifecycleTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _orgId, _ownerId, _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ChatLifecycleTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var owner = new User { Name = "Lifecycle Owner", Phone = "919810066001" };
            db.Users.Add(owner);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Life", Slug = "chat-lifecycle" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _ownerId = owner.Id;
            _categoryId = category.Id;
            _orgId = factory.SeedVerifiedOrg(owner.Id, "Chat Lifecycle Org");
            _reset = true;
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private static int _shortCodeSeq;

    private Guid NewDraftEvent(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // events.ShortCode is globally unique — slug prefixes collide, so number them.
        var shortCode = $"CL{Interlocked.Increment(ref _shortCodeSeq):D4}";
        var ev = new Event
        {
            RepresentingOrgId = _orgId, CreatedBy = _ownerId, CategoryId = _categoryId,
            Title = slug, Slug = slug, ShortCode = shortCode,
            Description = "Publishable.", VenueName = "Main Hall",
            StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(11),
            Status = EventStatus.Draft,
        };
        db.Events.Add(ev);
        db.SaveChanges();
        return ev.Id;
    }

    /// <summary>Stores the phone in the normalised form AuthService.NormalizePhone produces, so
    /// lookups by the 10-digit form used in assignment calls resolve.</summary>
    private Guid NewUser(string phone, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var u = new User { Name = name, Phone = "91" + phone };
        db.Users.Add(u);
        db.SaveChanges();
        return u.Id;
    }

    private async Task<ServiceResult<EventDetail>> TransitionAsync(Guid eventId, string action)
    {
        // D-266 M5: a Public event representing a non-personal org needs an approved institutional
        // authorization to publish. Every case in this class is about the CHAT ROOM's lifecycle following
        // the event's, so the authorization is fixture setup rather than the subject under test.
        if (action is "publish") _factory.SeedApprovedEventAuthorization(eventId);

        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEventService>()
            .TransitionAsync(_ownerId, eventId, false, false, action);
    }

    private (ChatRoom? Room, List<ChatMember> Members) ChatState(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var room = db.ChatRooms.AsNoTracking().FirstOrDefault(r => r.EventId == eventId);
        var members = room is null
            ? []
            : db.ChatMembers.AsNoTracking().Where(m => m.RoomId == room.Id).ToList();
        return (room, members);
    }

    /// <summary>
    /// How many times a specific lifecycle message was posted. A published room already has system
    /// messages of its own, so a raw system-message count would measure the wrong thing — and these
    /// assertions are about a transition happening exactly once.
    /// </summary>
    private int LifecycleMessageCount(Guid roomId, string contains)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.ChatMessages.AsNoTracking()
            .Count(m => m.RoomId == roomId && m.Kind == ChatMessageKind.System && m.Body.Contains(contains));
    }

    private int SystemMessageCount(Guid roomId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.ChatMessages.AsNoTracking().Count(m => m.RoomId == roomId && m.Kind == ChatMessageKind.System);
    }

    /// <summary>tickets.OrderItemId is a real FK, so a ticket needs a ticket type, order and order
    /// item behind it. Free (0-paise) order, which is the path most attendees actually take.</summary>
    private Guid IssueTicket(Guid eventId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var ticketType = db.TicketTypes.FirstOrDefault(t => t.EventId == eventId);
        if (ticketType is null)
        {
            ticketType = new TicketType { EventId = eventId, Name = "General", PricePaise = 0, Quantity = 100 };
            db.TicketTypes.Add(ticketType);
            db.SaveChanges();
        }

        var order = new Order
        {
            UserId = userId, EventId = eventId, TicketTypeId = ticketType.Id,
            Status = OrderStatus.Paid, AmountPaise = 0,
        };
        db.Orders.Add(order);
        db.SaveChanges();

        var item = new OrderItem { OrderId = order.Id, TicketTypeId = ticketType.Id, Qty = 1, UnitPricePaise = 0 };
        db.OrderItems.Add(item);
        db.SaveChanges();

        var ticket = new Ticket
        {
            EventId = eventId, UserId = userId, OrderItemId = item.Id,
            Code = Guid.NewGuid(), HmacSig = "sig", State = TicketState.Issued,
        };
        db.Tickets.Add(ticket);
        db.SaveChanges();
        return ticket.Id;
    }

    // ── Host lifecycle ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publishing_an_event_creates_the_room_and_seeds_org_members_as_hosts()
    {
        var eventId = NewDraftEvent("publish-seeds-hosts");
        Assert.Null(ChatState(eventId).Room);          // no room while the event is a draft

        var published = await TransitionAsync(eventId, "publish");
        Assert.True(published.Ok, published.Error);

        var (room, members) = ChatState(eventId);
        Assert.NotNull(room);
        Assert.Equal(ChatRoomKind.General, room!.Kind);
        Assert.Equal(ChatRoomStatus.Active, room.Status);

        // The regression this guards: before Phase 2A no code path ever produced a Host, so every
        // moderation capability was unreachable in production.
        var owner = Assert.Single(members, m => m.UserId == _ownerId);
        Assert.Equal(ChatMemberRole.Host, owner.Role);
    }

    [Fact]
    public async Task Republishing_is_idempotent_for_members_and_system_messages()
    {
        var eventId = NewDraftEvent("republish-idempotent");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);

        var afterFirst = ChatState(eventId);
        var systemMessagesAfterFirst = SystemMessageCount(afterFirst.Room!.Id);

        // unpublish -> publish runs the whole seeding hook a second time.
        Assert.True((await TransitionAsync(eventId, "unpublish")).Ok);
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);

        var afterSecond = ChatState(eventId);
        Assert.Equal(afterFirst.Room.Id, afterSecond.Room!.Id);               // room not recreated
        Assert.Equal(afterFirst.Members.Count, afterSecond.Members.Count);    // no duplicate members
        Assert.Equal(systemMessagesAfterFirst, SystemMessageCount(afterSecond.Room.Id)); // no repeat "joined"
        Assert.All(afterSecond.Members, m => Assert.Equal(ChatMemberRole.Host, m.Role));
    }

    // ── Staff lifecycle ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Staff_join_as_members_on_acceptance_and_are_evicted_on_removal()
    {
        var eventId = NewDraftEvent("staff-lifecycle");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        var staffId = NewUser("9810066010", "Staffer");

        using var scope = _factory.Services.CreateScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IEventAssignmentService>();

        var assigned = await assignments.AssignAsync(_ownerId, _orgId, eventId, "9810066010", "Volunteer", null, null);
        Assert.True(assigned.Ok, assigned.Error);

        // An invitation alone must not grant chat access.
        Assert.DoesNotContain(ChatState(eventId).Members, m => m.UserId == staffId);

        var accepted = await assignments.RespondAsync(staffId, assigned.Value!.Id, true);
        Assert.True(accepted.Ok, accepted.Error);
        // D-300 — a Volunteer joins as a MEMBER, not a Host. This asserted Host until D-300 drew the line
        // between operational authority over an event and moderation authority over the conversation in it:
        // accepting a working role must not confer the power to ban attendees and delete anyone's messages.
        // The test was not updated when the behaviour changed, so it kept asserting the model D-300 removed.
        Assert.Equal(ChatMemberRole.Member, Assert.Single(ChatState(eventId).Members, m => m.UserId == staffId).Role);

        // Removal: this staffer is not an org member and holds no ticket, so they leave the room.
        Assert.True((await assignments.RemoveAsync(_ownerId, _orgId, eventId, assigned.Value.Id)).Ok);
        Assert.DoesNotContain(ChatState(eventId).Members, m => m.UserId == staffId);
    }

    [Fact]
    public async Task Removing_staff_who_hold_a_ticket_demotes_them_instead_of_evicting()
    {
        var eventId = NewDraftEvent("staff-with-ticket");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        var staffId = NewUser("9810066011", "Staffer With Ticket");
        IssueTicket(eventId, staffId);

        using var scope = _factory.Services.CreateScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IEventAssignmentService>();
        var assigned = await assignments.AssignAsync(_ownerId, _orgId, eventId, "9810066011", "Judge", null, null);
        await assignments.RespondAsync(staffId, assigned.Value!.Id, true);
        Assert.True((await assignments.RemoveAsync(_ownerId, _orgId, eventId, assigned.Value.Id)).Ok);

        // Still an attendee — they keep access, but lose moderation.
        var member = Assert.Single(ChatState(eventId).Members, m => m.UserId == staffId);
        Assert.Equal(ChatMemberRole.Member, member.Role);
    }

    [Fact]
    public async Task Removing_a_staff_assignment_never_strips_an_org_members_host_role()
    {
        var eventId = NewDraftEvent("org-member-staff");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);

        using var scope = _factory.Services.CreateScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IEventAssignmentService>();
        var assigned = await assignments.AssignAsync(_ownerId, _orgId, eventId, "9810066001", "Judge", null, null);
        await assignments.RespondAsync(_ownerId, assigned.Value!.Id, true);
        Assert.True((await assignments.RemoveAsync(_ownerId, _orgId, eventId, assigned.Value.Id)).Ok);

        // The owner holds Host through the org, not through the assignment.
        Assert.Equal(ChatMemberRole.Host, Assert.Single(ChatState(eventId).Members, m => m.UserId == _ownerId).Role);
    }

    [Fact]
    public async Task Staff_accepted_before_publish_are_seeded_at_publish()
    {
        var eventId = NewDraftEvent("staff-before-publish");
        var staffId = NewUser("9810066012", "Early Staffer");

        using var scope = _factory.Services.CreateScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IEventAssignmentService>();
        var assigned = await assignments.AssignAsync(_ownerId, _orgId, eventId, "9810066012", "Security", null, null);
        await assignments.RespondAsync(staffId, assigned.Value!.Id, true);

        // No room existed when they accepted, so the acceptance can only take effect at publish.
        Assert.Null(ChatState(eventId).Room);
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        // D-300 — seeded as a MEMBER, for the same reason as the acceptance path above. What publish
        // preserves is that the acceptance takes effect at all, not the rank it used to confer.
        Assert.Equal(ChatMemberRole.Member, Assert.Single(ChatState(eventId).Members, m => m.UserId == staffId).Role);
    }

    // ── Participant lifecycle: transfer ────────────────────────────────────────────────────────

    [Fact]
    public async Task Ticket_transfer_moves_chat_access_and_is_safe_to_replay()
    {
        var eventId = NewDraftEvent("transfer-moves-chat");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        var sellerId = NewUser("9810066020", "Seller");
        var buyerId = NewUser("9810066021", "Buyer");
        var ticketId = IssueTicket(eventId, sellerId);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var transfers = scope.ServiceProvider.GetRequiredService<ITicketTransferService>();

        await chat.AddMemberByEventAsync(eventId, sellerId, "Member");
        Assert.Contains(ChatState(eventId).Members, m => m.UserId == sellerId);

        var initiated = await transfers.InitiateAsync(sellerId, ticketId, "9810066021");
        Assert.True(initiated.Ok, initiated.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var code = db.TicketTransfers.AsNoTracking().First(t => t.TicketId == ticketId).TransferCode;

        var claimed = await transfers.ClaimAsync(buyerId, "9810066021", code);
        Assert.True(claimed.Ok, claimed.Error);

        var after = ChatState(eventId).Members;
        Assert.Contains(after, m => m.UserId == buyerId);       // buyer gained access
        Assert.DoesNotContain(after, m => m.UserId == sellerId); // seller lost it — no orphan

        // A replayed claim is refused, and must not disturb the settled membership.
        Assert.False((await transfers.ClaimAsync(buyerId, "9810066021", code)).Ok);
        var afterReplay = ChatState(eventId).Members;
        Assert.Equal(after.Count, afterReplay.Count);
        Assert.Single(afterReplay, m => m.UserId == buyerId);
    }

    // ── Participant lifecycle: refund ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Refund_removes_chat_access_and_repeating_it_is_harmless()
    {
        var eventId = NewDraftEvent("refund-removes-chat");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        var buyerId = NewUser("9810066030", "Refunded Buyer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();

        var ticketId = IssueTicket(eventId, buyerId);
        await chat.AddMemberByEventAsync(eventId, buyerId, "Member");
        Assert.Contains(ChatState(eventId).Members, m => m.UserId == buyerId);

        // Voiding the ticket is what makes the member ineligible; RemoveMemberIfNoTicketsAsync is the
        // exact complement of the state RefundService writes.
        var ticket = db.Tickets.First(t => t.Id == ticketId);
        ticket.State = TicketState.Void;
        db.SaveChanges();

        await chat.RemoveMemberIfNoTicketsAsync(eventId, buyerId);
        Assert.DoesNotContain(ChatState(eventId).Members, m => m.UserId == buyerId);

        // Replay converges rather than throwing.
        await chat.RemoveMemberIfNoTicketsAsync(eventId, buyerId);
        Assert.DoesNotContain(ChatState(eventId).Members, m => m.UserId == buyerId);
    }

    [Fact]
    public async Task Cleanup_never_evicts_a_member_who_still_holds_a_valid_ticket()
    {
        var eventId = NewDraftEvent("cleanup-keeps-valid");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        var buyerId = NewUser("9810066031", "Two Ticket Buyer");

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var voided = IssueTicket(eventId, buyerId);
        IssueTicket(eventId, buyerId);                     // a second, still-valid ticket
        await chat.AddMemberByEventAsync(eventId, buyerId, "Member");

        db.Tickets.First(t => t.Id == voided).State = TicketState.Void;
        db.SaveChanges();

        await chat.RemoveMemberIfNoTicketsAsync(eventId, buyerId);
        Assert.Contains(ChatState(eventId).Members, m => m.UserId == buyerId);
    }

    // ── Room lifecycle ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_an_event_locks_the_room_exactly_once()
    {
        var eventId = NewDraftEvent("cancel-locks-room");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        Assert.True((await TransitionAsync(eventId, "cancel")).Ok);

        var (room, _) = ChatState(eventId);
        Assert.Equal(ChatRoomStatus.Locked, room!.Status);
        Assert.NotNull(room.LockedAt);
        var messagesAfterCancel = SystemMessageCount(room.Id);

        // Archiving an already-locked room must not post a second lock notice.
        Assert.True((await TransitionAsync(eventId, "archive")).Ok);
        var (afterArchive, _) = ChatState(eventId);
        Assert.Equal(ChatRoomStatus.Locked, afterArchive!.Status);
        Assert.Equal(room.LockedAt, afterArchive.LockedAt);
        Assert.Equal(messagesAfterCancel, SystemMessageCount(room.Id));
    }

    [Fact]
    public async Task Archiving_a_closed_event_locks_the_room()
    {
        var eventId = NewDraftEvent("archive-locks-room");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        Assert.True((await TransitionAsync(eventId, "close")).Ok);
        Assert.Equal(ChatRoomStatus.Active, ChatState(eventId).Room!.Status);  // closed != locked

        Assert.True((await TransitionAsync(eventId, "archive")).Ok);
        Assert.Equal(ChatRoomStatus.Locked, ChatState(eventId).Room!.Status);
    }

    /// <summary>
    /// The event is over but still inside the seven-day window: read-only, not archived (D-122).
    /// </summary>
    [Fact]
    public async Task Sweep_locks_a_room_as_soon_as_its_event_ends()
    {
        var eventId = NewDraftEvent("sweep-locks-at-end");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 3, endsDaysAgo: 1);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IChatService>().LockExpiredRoomsAsync();

        Assert.Equal(ChatRoomStatus.Locked, ChatState(eventId).Room!.Status);
    }

    /// <summary>A live event is untouched — the sweep must not close a running conversation.</summary>
    [Fact]
    public async Task Sweep_leaves_a_running_event_active()
    {
        var eventId = NewDraftEvent("sweep-skips-running");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 1, endsDaysAgo: -1);   // started yesterday, ends tomorrow

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IChatService>().LockExpiredRoomsAsync();

        Assert.Equal(ChatRoomStatus.Active, ChatState(eventId).Room!.Status);
    }

    /// <summary>
    /// Seven days after the end the room archives, and further sweeps change nothing. One sweep has
    /// to reach Archived from Active, which is why phase 1 saves before phase 2 queries.
    /// </summary>
    [Fact]
    public async Task Sweep_archives_after_the_read_only_window_and_is_idempotent()
    {
        var eventId = NewDraftEvent("sweep-archives");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 20, endsDaysAgo: 10);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();

        await chat.LockExpiredRoomsAsync();
        var (room, _) = ChatState(eventId);
        Assert.Equal(ChatRoomStatus.Archived, room!.Status);
        var afterFirstSweep = SystemMessageCount(room.Id);

        await chat.LockExpiredRoomsAsync();               // selects nothing in either phase
        Assert.Equal(afterFirstSweep, SystemMessageCount(room.Id));
        Assert.Equal(ChatRoomStatus.Archived, ChatState(eventId).Room!.Status);
    }

    /// <summary>A room inside the window stays read-only rather than archiving early.</summary>
    [Fact]
    public async Task Sweep_does_not_archive_inside_the_read_only_window()
    {
        var eventId = NewDraftEvent("sweep-window");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 8, endsDaysAgo: 6);   // ended 6 days ago; day 7 is the line

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IChatService>().LockExpiredRoomsAsync();

        Assert.Equal(ChatRoomStatus.Locked, ChatState(eventId).Room!.Status);
    }

    /// <summary>Archived refuses sends with its own reason, not the read-only one.</summary>
    [Fact]
    public async Task Archived_room_rejects_new_messages_with_room_archived()
    {
        var eventId = NewDraftEvent("archived-rejects");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 20, endsDaysAgo: 10);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        await chat.LockExpiredRoomsAsync();

        var roomId = ChatState(eventId).Room!.Id;
        var result = await chat.SendMessageAsync(roomId, _ownerId, "still here?", null, null, null);

        Assert.False(result.Ok);
        Assert.Equal("room_archived", result.Error);
    }

    /// <summary>
    /// History, moderation and reporting survive archiving; posting and participant changes do not.
    /// </summary>
    [Fact]
    public async Task Archived_room_keeps_history_and_moderation_but_freezes_participants()
    {
        var eventId = NewDraftEvent("archived-caps");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var roomId = ChatState(eventId).Room!.Id;
        Assert.True((await chat.SendMessageAsync(roomId, _ownerId, "before the end", null, null, null)).Ok);

        // A second, lower-ranked member, added while the room is still live — participants freeze on
        // archive, so this cannot be done afterwards. The mute below needs a target the owner actually
        // outranks; see there.
        var attendeeId = NewUser("9810066040", "Archived Attendee");
        await chat.AddMemberAsync(roomId, attendeeId, "Member");

        ShiftEvent(eventId, startsDaysAgo: 20, endsDaysAgo: 10);
        await chat.LockExpiredRoomsAsync();

        var view = await chat.GetRoomAsync(eventId, _ownerId);
        Assert.True(view.Ok);
        Assert.False(view.Value!.Capabilities.CanPost);
        Assert.False(view.Value.Capabilities.CanUpload);
        // A report filed on the last day of the window still has to be actionable afterwards.
        Assert.True(view.Value.Capabilities.CanModerate);

        var page = await chat.GetMessagesAsync(roomId, _ownerId, null, null, 50);
        Assert.True(page.Ok);
        Assert.Contains(page.Value!.Messages, m => m.Body == "before the end");

        // Participant state is frozen.
        //
        // The target is the attendee, not the owner themselves. This muted `_ownerId` as `_ownerId` until
        // D-301, which is now refused one step earlier — `OutRanks` is strictly greater, so a Host does not
        // outrank a Host and self-moderation returns `cannot_moderate_peer` before the archive check is
        // ever reached. The assertion still passed the day D-301 shipped only because the error string
        // happened to be compared after the ladder ran; once it changed, the test was asserting the rank
        // rule while claiming to assert the freeze. A Host muting a Member clears the ladder, so the
        // archive check is genuinely the thing under test again.
        var muted = await chat.MuteMemberAsync(roomId, _ownerId, attendeeId, 10);
        Assert.False(muted.Ok);
        Assert.Equal("room_archived", muted.Error);
    }

    /// <summary>Archive is lifecycle-driven; a host cannot set it by hand and strand the room.</summary>
    [Fact]
    public async Task A_host_cannot_archive_a_room_by_updating_it()
    {
        var eventId = NewDraftEvent("no-manual-archive");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var roomId = ChatState(eventId).Room!.Id;

        await chat.UpdateRoomAsync(roomId, _ownerId, null, "Archived");

        Assert.Equal(ChatRoomStatus.Active, ChatState(eventId).Room!.Status);
    }

    /// <summary>
    /// The whole point of D-123: an event that ended a moment ago is closed *now*, without waiting
    /// for the hourly sweep. The stored status is still Active at this point.
    /// </summary>
    [Fact]
    public async Task Chat_is_read_only_the_moment_the_event_ends_without_a_sweep()
    {
        var eventId = NewDraftEvent("immediate-lock");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var roomId = ChatState(eventId).Room!.Id;
        Assert.True((await chat.SendMessageAsync(roomId, _ownerId, "during", null, null, null)).Ok);

        ShiftEvent(eventId, startsDaysAgo: 1, endsDaysAgo: 0.01);   // ended ~15 minutes ago
        // Deliberately no sweep.

        var send = await chat.SendMessageAsync(roomId, _ownerId, "after the end", null, null, null);
        Assert.False(send.Ok);
        Assert.Equal("room_locked", send.Error);

        // Stored status has not moved — enforcement is derived, persistence is the sweep's job.
        Assert.Equal(ChatRoomStatus.Active, ChatState(eventId).Room!.Status);

        // And the client is told the truth rather than "Active".
        var view = await chat.GetRoomAsync(eventId, _ownerId);
        Assert.True(view.Ok);
        Assert.Equal("Locked", view.Value!.Status);
        Assert.False(view.Value.Capabilities.CanPost);
        Assert.False(view.Value.Capabilities.CanUpload);
    }

    /// <summary>Seven days on, the room is archived on read even if the sweep never ran.</summary>
    [Fact]
    public async Task Chat_reports_archived_after_the_window_without_a_sweep()
    {
        var eventId = NewDraftEvent("immediate-archive");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 20, endsDaysAgo: 10);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var roomId = ChatState(eventId).Room!.Id;

        // Sending is refused before anything has persisted the transition — enforcement is derived,
        // not stored. The send path deliberately does not write; only opening the room does.
        var send = await chat.SendMessageAsync(roomId, _ownerId, "hello?", null, null, null);
        Assert.False(send.Ok);
        Assert.Equal("room_archived", send.Error);
        Assert.Equal(ChatRoomStatus.Active, ChatState(eventId).Room!.Status);   // still unswept

        // Opening the room is what persists it (D-124).
        var view = await chat.GetRoomAsync(eventId, _ownerId);
        Assert.Equal("Archived", view.Value!.Status);
        Assert.Equal(ChatRoomStatus.Archived, ChatState(eventId).Room!.Status);
    }

    /// <summary>
    /// The sweep is the recovery path: after downtime it persists everything the clock already
    /// decided, in one pass, and the durable state then matches what was being enforced all along.
    /// </summary>
    [Fact]
    public async Task Sweep_recovers_persisted_state_after_downtime()
    {
        var ended = NewDraftEvent("recover-ended");
        var archived = NewDraftEvent("recover-archived");
        Assert.True((await TransitionAsync(ended, "publish")).Ok);
        Assert.True((await TransitionAsync(archived, "publish")).Ok);

        ShiftEvent(ended, startsDaysAgo: 3, endsDaysAgo: 2);
        ShiftEvent(archived, startsDaysAgo: 30, endsDaysAgo: 20);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IChatService>().LockExpiredRoomsAsync();

        Assert.Equal(ChatRoomStatus.Locked, ChatState(ended).Room!.Status);
        Assert.Equal(ChatRoomStatus.Archived, ChatState(archived).Room!.Status);
    }

    /// <summary>An archived room never reopens — the lifecycle is strictly one-way.</summary>
    [Fact]
    public async Task An_archived_room_cannot_be_reopened()
    {
        var eventId = NewDraftEvent("no-reopen");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 20, endsDaysAgo: 10);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        await chat.LockExpiredRoomsAsync();
        var roomId = ChatState(eventId).Room!.Id;
        Assert.Equal(ChatRoomStatus.Archived, ChatState(eventId).Room!.Status);

        var reopen = await chat.UpdateRoomAsync(roomId, _ownerId, null, "Active");

        Assert.False(reopen.Ok);
        Assert.Equal("room_archived", reopen.Error);
        Assert.Equal(ChatRoomStatus.Archived, ChatState(eventId).Room!.Status);
    }

    /// <summary>History stays readable after archiving — archive freezes, it does not delete.</summary>
    [Fact]
    public async Task Archived_history_remains_readable()
    {
        var eventId = NewDraftEvent("archived-history");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var roomId = ChatState(eventId).Room!.Id;
        Assert.True((await chat.SendMessageAsync(roomId, _ownerId, "remember this", null, null, null)).Ok);

        ShiftEvent(eventId, startsDaysAgo: 20, endsDaysAgo: 10);
        await chat.LockExpiredRoomsAsync();

        var page = await chat.GetMessagesAsync(roomId, _ownerId, null, null, 50);
        Assert.True(page.Ok);
        Assert.Contains(page.Value!.Messages, m => m.Body == "remember this");
    }

    /// <summary>
    /// Opening a room after its event ended persists the transition there and then (D-124), so the
    /// explanation arrives with the first reader instead of whenever the sweep next runs.
    /// </summary>
    [Fact]
    public async Task First_access_after_the_event_ends_persists_the_transition()
    {
        var eventId = NewDraftEvent("first-access-persists");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 1, endsDaysAgo: 0.01);
        Assert.Equal(ChatRoomStatus.Active, ChatState(eventId).Room!.Status);   // sweep has not run

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var view = await chat.GetRoomAsync(eventId, _ownerId);

        Assert.True(view.Ok);
        Assert.Equal("Locked", view.Value!.Status);
        // Persisted by the read, not by a sweep.
        Assert.Equal(ChatRoomStatus.Locked, ChatState(eventId).Room!.Status);
        Assert.Equal(1, LifecycleMessageCount(ChatState(eventId).Room!.Id, "now read-only"));
    }

    /// <summary>
    /// Repeated opens must not each post a lifecycle message — the transition happens once.
    /// </summary>
    [Fact]
    public async Task Repeated_access_does_not_repeat_the_lifecycle_message()
    {
        var eventId = NewDraftEvent("access-idempotent");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 1, endsDaysAgo: 0.01);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        for (var i = 0; i < 3; i++) await chat.GetRoomAsync(eventId, _ownerId);

        Assert.Equal(1, LifecycleMessageCount(ChatState(eventId).Room!.Id, "now read-only"));
    }

    /// <summary>
    /// The transition is a compare-and-set, so simultaneous readers cannot both post the message.
    /// Read-then-write would let both observe Active and both write.
    /// </summary>
    [Fact]
    public async Task Concurrent_first_access_transitions_exactly_once()
    {
        var eventId = NewDraftEvent("concurrent-access");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 1, endsDaysAgo: 0.01);

        // Separate scopes: one DbContext each, exactly as concurrent requests would have.
        var opens = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IChatService>().GetRoomAsync(eventId, _ownerId);
        });
        await Task.WhenAll(opens);

        Assert.Equal(ChatRoomStatus.Locked, ChatState(eventId).Room!.Status);
        Assert.Equal(1, LifecycleMessageCount(ChatState(eventId).Room!.Id, "now read-only"));
    }

    /// <summary>
    /// A cancellation racing the sweep must also transition once — both go through the same
    /// compare-and-set.
    /// </summary>
    [Fact]
    public async Task A_sweep_racing_a_cancellation_transitions_exactly_once()
    {
        var eventId = NewDraftEvent("race-cancel-sweep");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 3, endsDaysAgo: 1);

        var sweep = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IChatService>().LockExpiredRoomsAsync();
        });
        var cancel = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IChatService>()
                .LockRoomForEventAsync(eventId, "This event was cancelled. Chat is now read-only.");
        });
        await Task.WhenAll(sweep, cancel);

        Assert.Equal(ChatRoomStatus.Locked, ChatState(eventId).Room!.Status);
        Assert.Equal(1, LifecycleMessageCount(ChatState(eventId).Room!.Id, "read-only"));
    }

    /// <summary>
    /// Reaching Archived from Active still passes through Locked, so both explanations are written
    /// in order and the lifecycle never skips a state.
    /// </summary>
    [Fact]
    public async Task Lazy_persistence_passes_through_locked_on_the_way_to_archived()
    {
        var eventId = NewDraftEvent("no-skip-locked");
        Assert.True((await TransitionAsync(eventId, "publish")).Ok);
        ShiftEvent(eventId, startsDaysAgo: 20, endsDaysAgo: 10);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var view = await chat.GetRoomAsync(eventId, _ownerId);

        Assert.Equal("Archived", view.Value!.Status);
        Assert.Equal(ChatRoomStatus.Archived, ChatState(eventId).Room!.Status);
        // Passed through Locked rather than jumping straight to Archived.
        Assert.Equal(1, LifecycleMessageCount(ChatState(eventId).Room!.Id, "now read-only"));
        Assert.Equal(1, LifecycleMessageCount(ChatState(eventId).Room!.Id, "been archived"));
    }

    /// <summary>Moves an event's dates so lifecycle sweeps can be exercised without waiting.</summary>
    private void ShiftEvent(Guid eventId, double startsDaysAgo, double endsDaysAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = db.Events.First(e => e.Id == eventId);
        ev.StartsAt = DateTime.UtcNow.AddDays(-startsDaysAgo);
        ev.EndsAt = DateTime.UtcNow.AddDays(-endsDaysAgo);
        db.SaveChanges();
    }
}
