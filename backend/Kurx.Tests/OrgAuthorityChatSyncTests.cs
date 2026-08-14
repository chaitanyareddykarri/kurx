using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// D-304 — organization authority reaching rooms that already exist.
///
/// <para>The gap these pin: <c>AddEventHostsAsync</c> had exactly one caller, the publish transition, so
/// authority granted <b>after</b> publication reached nothing. A user made Manager on Monday, for an event
/// published on Sunday, was not a <c>ChatMember</c> at all and got <c>forbidden</c> opening the chat of an
/// event they now ran.</para>
///
/// <para>Every case drives the real producer (<c>IOrgService</c>, <c>IOrgInvitationService</c>,
/// <c>IMembershipVerificationService</c>) and then the real <c>OutboxDispatchJob</c>, rather than calling
/// the sync directly — the enqueue is half of what is under test, and a test that skipped it would still
/// have passed on the broken code.</para>
/// </summary>
public class OrgAuthorityChatSyncTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static Guid _orgId, _ownerId, _categoryId;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _seq;

    public OrgAuthorityChatSyncTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var owner = new User { Name = "Authority Owner", Phone = "919820077001" };
            db.Users.Add(owner);
            var category = new EventCategory
            {
                Level = CategoryLevel.Category, Name = "Auth", Slug = "org-authority-sync",
            };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _ownerId = owner.Id;
            _categoryId = category.Id;
            _orgId = factory.SeedVerifiedOrg(owner.Id, "Org Authority Sync Org");
            _reset = true;
        }
    }

    // ── 1-3: authority granted AFTER publication ───────────────────────────────────────────────

    [Theory]
    [InlineData(OrgRole.Owner)]
    [InlineData(OrgRole.Manager)]
    [InlineData(OrgRole.Representative)]
    public async Task A_host_level_role_granted_after_publication_reaches_the_room(OrgRole role)
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Late " + role);

        // The whole defect in one assertion: the room already exists, so nothing was ever going to add them.
        Assert.Null(RoleInRoom(eventId, userId));

        await AddOrgMemberAsync(phone, role);
        await DispatchAsync();

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task A_staff_seat_granted_after_publication_joins_as_a_member_not_a_host()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Late Staff");

        await AddOrgMemberAsync(phone, OrgRole.Staff);
        await DispatchAsync();

        // D-300 holds through the new path: a Staff seat is operational authority over an event, never
        // moderation authority over the conversation in it.
        Assert.Equal(ChatMemberRole.Member, RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task A_finance_seat_reaches_the_room_at_no_rung_at_all()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Late Finance");

        await AddOrgMemberAsync(phone, OrgRole.Finance);
        await DispatchAsync();

        // Finance holds money authority over the organization, not standing on its events (D-269).
        Assert.Null(RoleInRoom(eventId, userId));
    }

    // ── 2: authority held BEFORE publication is unaffected ─────────────────────────────────────

    [Fact]
    public async Task Authority_granted_before_publication_still_comes_from_publish_seeding()
    {
        var (userId, phone) = NewUser("Early Manager");
        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();                      // no event yet: nothing to converge

        var eventId = await PublishedEventAsync();  // publish-time seeding is what admits them

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
    }

    // ── 3-5: role changes on a published event ─────────────────────────────────────────────────

    [Fact]
    public async Task Promotion_from_staff_to_manager_raises_an_existing_member_to_host()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Promoted");

        await AddOrgMemberAsync(phone, OrgRole.Staff);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Member, RoleInRoom(eventId, userId));

        await ChangeRoleAsync(userId, OrgRole.Manager);
        await DispatchAsync();

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task Manager_and_representative_are_the_same_rung_so_swapping_them_changes_nothing()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Sidestep");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        await ChangeRoleAsync(userId, OrgRole.Representative);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        await ChangeRoleAsync(userId, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task Demotion_from_a_host_level_role_to_staff_keeps_them_in_the_room_as_a_member()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Demoted");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        await ChangeRoleAsync(userId, OrgRole.Staff);
        await DispatchAsync();

        // Member, NOT evicted. Routing this through RemoveStaffMemberAsync would have ended in
        // RemoveMemberIfNoTicketsAsync, which tests only for a ticket — so a demoted Manager still holding a
        // Staff seat would have been thrown out of a room D-300 says they belong in.
        Assert.Equal(ChatMemberRole.Member, RoleInRoom(eventId, userId));
    }

    // ── 6: revocation ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoking_the_membership_entirely_removes_them_from_the_room()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Revoked");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        await RemoveOrgMemberAsync(userId);
        await DispatchAsync();

        // No seat, no ticket, no participation — no standing, so no seat in the conversation either.
        Assert.Null(RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task The_events_own_creator_survives_every_organization_change()
    {
        var eventId = await PublishedEventAsync();

        // The owner IS the creator. Strip their organization seat entirely and they must keep Host: D-268
        // makes the creator the owner of the event, with no membership behind it (D-272).
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, _ownerId));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
            // Removed directly: RemoveMemberAsync refuses to drop the last Owner, and the point here is the
            // resolver's answer, not that guard.
            await db.Memberships.Where(m => m.OrgId == _orgId && m.UserId == _ownerId).ExecuteDeleteAsync();
            await chat.SyncOrgAuthorityAsync(_orgId, _ownerId);
        }

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, _ownerId));
        await RestoreOwnerAsync();
    }

    // ── 7-8: the other two producers ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Accepting_an_organization_invitation_reaches_the_room()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Invitee");

        using (var scope = _factory.Services.CreateScope())
        {
            var invitations = scope.ServiceProvider.GetRequiredService<IOrgInvitationService>();
            var created = await invitations.InviteAsync(_ownerId, _orgId, phone, OrgRole.Manager);
            Assert.True(created.Ok, created.Error);

            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var token = await db.OrgInvitations.AsNoTracking()
                .Where(i => i.Id == created.Value!.Id).Select(i => i.Token).FirstAsync();

            var accepted = await invitations.AcceptAsync(userId, token);
            Assert.True(accepted.Ok, accepted.Error);
        }

        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task Approving_a_membership_verification_reaches_the_room_as_a_member()
    {
        var eventId = await PublishedEventAsync();
        var (userId, _) = NewUser("Claimant");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var claim = new MembershipClaim
            {
                OrgId = _orgId, UserId = userId, ClaimedRole = MembershipClaimRole.Student,
                Status = MembershipClaimStatus.Submitted,
            };
            db.MembershipClaims.Add(claim);
            await db.SaveChangesAsync();

            var verification = scope.ServiceProvider.GetRequiredService<IMembershipVerificationService>();
            var reviewed = await verification.ReviewAsync(_ownerId, claim.Id, "approve", null, null);
            Assert.True(reviewed.Ok, reviewed.Error);
        }

        await DispatchAsync();

        // Approval grants a Staff seat, so the answer is Member. Verification establishes that someone
        // belongs to an institution; it is not authority over its events (D-304 case 8).
        Assert.Equal(ChatMemberRole.Member, RoleInRoom(eventId, userId));
    }

    // ── 9-11: fan-out, redelivery, concurrency ─────────────────────────────────────────────────

    [Fact]
    public async Task One_authority_change_reaches_every_published_event_the_organization_represents()
    {
        var first = await PublishedEventAsync();
        var second = await PublishedEventAsync();
        var third = await PublishedEventAsync();
        var (userId, phone) = NewUser("Fanout");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(first, userId));
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(second, userId));
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(third, userId));
    }

    [Fact]
    public async Task A_draft_event_gains_no_room_and_publish_seeds_it_afterwards()
    {
        var draftId = NewDraftEvent();
        var (userId, phone) = NewUser("Before Publish");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();

        // No room exists yet, so there is nothing to converge and nothing is invented.
        Assert.Null(RoomIdFor(draftId));

        await PublishAsync(draftId);
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(draftId, userId));
    }

    [Fact]
    public async Task Redelivering_the_same_message_changes_nothing_and_adds_no_second_row()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Redelivered");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        // Straight at the handler, three times: the dispatcher marks a row delivered, so re-running the job
        // would prove only that it does not re-send. What has to be idempotent is the SYNC itself.
        using (var scope = _factory.Services.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
            await chat.SyncOrgAuthorityAsync(_orgId, userId);
            await chat.SyncOrgAuthorityAsync(_orgId, userId);
            await chat.SyncOrgAuthorityAsync(_orgId, userId);
        }

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
        Assert.Equal(1, MemberRowCount(eventId, userId));
    }

    [Fact]
    public async Task Concurrent_syncs_converge_on_one_row_and_the_committed_role()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Concurrent");
        await AddOrgMemberAsync(phone, OrgRole.Manager);

        // Four simultaneous syncs against the unique (RoomId, UserId) index. The loser of the insert race
        // must converge on the winner's row rather than throwing, and no run may produce a duplicate.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IChatService>().SyncOrgAuthorityAsync(_orgId, userId);
        }));

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
        Assert.Equal(1, MemberRowCount(eventId, userId));
    }

    [Fact]
    public async Task The_final_state_follows_committed_authority_not_the_order_messages_were_written()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Last Writer");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await ChangeRoleAsync(userId, OrgRole.Staff);   // two changes, both staged, dispatched together

        await DispatchAsync();

        // The payload carries only WHO changed, so every message resolves the same live authority. Had it
        // carried `role`, the grant message could have applied after the demotion and left chat asserting a
        // rung the organization had already withdrawn.
        Assert.Equal(ChatMemberRole.Member, RoleInRoom(eventId, userId));
    }

    // ── 12-15: existing chat rows ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_existing_member_is_raised_in_place_without_a_second_join_message()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Existing Member");
        var roomId = RoomIdFor(eventId)!.Value;

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IChatService>()
                .AddMemberAsync(roomId, userId, "Member");

        var joinsBefore = SystemMessageCount(roomId);

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
        Assert.Equal(1, MemberRowCount(eventId, userId));
        // Raised in place: an upgrade is not an arrival, so it announces nothing.
        Assert.Equal(joinsBefore, SystemMessageCount(roomId));
    }

    [Fact]
    public async Task An_explicit_moderator_is_raised_to_host_when_they_gain_authority()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Moderator Rising");
        var roomId = RoomIdFor(eventId)!.Value;

        using (var scope = _factory.Services.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
            await chat.AddMemberAsync(roomId, userId, "Member");
            // Explicit D-301 promotion by the room's Host.
            Assert.True((await chat.SetModeratorAsync(roomId, _ownerId, userId, true)).Ok);
        }
        Assert.Equal(ChatMemberRole.Moderator, RoleInRoom(eventId, userId));

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task An_existing_host_stays_host_and_gains_nothing_new()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Already Host");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        await ChangeRoleAsync(userId, OrgRole.Owner);   // Host-level to Host-level
        await DispatchAsync();

        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));
        Assert.Equal(1, MemberRowCount(eventId, userId));
    }

    [Fact]
    public async Task An_explicit_moderator_shadowed_by_host_authority_lands_on_member_when_it_is_withdrawn()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Moderator Falling");
        var roomId = RoomIdFor(eventId)!.Value;

        using (var scope = _factory.Services.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
            await chat.AddMemberAsync(roomId, userId, "Member");
            Assert.True((await chat.SetModeratorAsync(roomId, _ownerId, userId, true)).Ok);
        }

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        await ChangeRoleAsync(userId, OrgRole.Staff);
        await DispatchAsync();

        // Member, not Moderator. D-304 case 15, decided explicitly: the pre-Host role is not stored, and
        // recovering it would cost a column and a migration to restore something a Host re-grants in one
        // click. The failure direction is the safe one — less authority, never more.
        Assert.Equal(ChatMemberRole.Member, RoleInRoom(eventId, userId));
    }

    [Fact]
    public async Task Someone_with_audience_standing_who_loses_their_seat_stays_as_a_member()
    {
        var eventId = await PublishedEventAsync();
        var (userId, phone) = NewUser("Standing Manager");

        await AddOrgMemberAsync(phone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, userId));

        // An accepted programme participation is audience standing in its own right, the same rung a live
        // ticket reaches (D-272) — so losing the organization seat costs the Host rung and nothing else.
        // Used here in preference to a Ticket because a ticket needs a whole order graph behind it, and the
        // rung is what this asserts, not how it was earned.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var role = await db.ParticipantRoles.AsNoTracking().FirstAsync(r => r.OrgId == null);
            db.EventParticipants.Add(new EventParticipant
            {
                EventId = eventId, RoleSlug = role.Slug,
                // Active, not Accepted: EventAuthorityService resolves standing on `State == Active`, so an
                // Accepted row confers nothing. Getting this wrong is what made this test claim the
                // implementation had dropped someone who in fact had no standing to keep.
                SubjectType = ParticipantSubjectType.Person, SubjectId = userId,
                State = ParticipantState.Active,
            });
            await db.SaveChangesAsync();
        }

        await RemoveOrgMemberAsync(userId);
        await DispatchAsync();

        Assert.Equal(ChatMemberRole.Member, RoleInRoom(eventId, userId));
    }

    // ── D-300 / D-301 must not regress through the new path ────────────────────────────────────

    [Fact]
    public async Task The_ladder_still_holds_after_authority_driven_role_changes()
    {
        var eventId = await PublishedEventAsync();
        var roomId = RoomIdFor(eventId)!.Value;
        var (hostViaOrg, hostPhone) = NewUser("Org Host");
        var (moderator, _) = NewUser("Ladder Mod");
        var (plain, _) = NewUser("Ladder Member");

        await AddOrgMemberAsync(hostPhone, OrgRole.Manager);
        await DispatchAsync();
        Assert.Equal(ChatMemberRole.Host, RoleInRoom(eventId, hostViaOrg));

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        await chat.AddMemberAsync(roomId, moderator, "Member");
        await chat.AddMemberAsync(roomId, plain, "Member");
        Assert.True((await chat.SetModeratorAsync(roomId, _ownerId, moderator, true)).Ok);

        // A Host minted by organization authority is a Host in every sense — including managing moderators.
        Assert.True((await chat.SetModeratorAsync(roomId, hostViaOrg, plain, true)).Ok);
        Assert.Equal(ChatMemberRole.Moderator, RoleInRoom(eventId, plain));

        // ...and the D-301 ladder is unchanged underneath it.
        Assert.Equal("cannot_moderate_peer", (await chat.MuteMemberAsync(roomId, moderator, plain, 10)).Error);
        Assert.Equal("cannot_moderate_peer", (await chat.MuteMemberAsync(roomId, moderator, hostViaOrg, 10)).Error);
        Assert.Equal("forbidden", (await chat.SetModeratorAsync(roomId, moderator, plain, false)).Error);
        Assert.True((await chat.MuteMemberAsync(roomId, hostViaOrg, plain, 10)).Ok);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    private Guid NewDraftEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var n = Interlocked.Increment(ref _seq);
        var ev = new Event
        {
            RepresentingOrgId = _orgId, CreatedBy = _ownerId, CategoryId = _categoryId,
            Title = $"Authority {n}", Slug = $"authority-sync-{n}", ShortCode = $"OA{n:D4}",
            Description = "Publishable.", VenueName = "Main Hall",
            StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(11),
            Status = EventStatus.Draft,
        };
        db.Events.Add(ev);
        db.SaveChanges();
        return ev.Id;
    }

    private async Task PublishAsync(Guid eventId)
    {
        _factory.SeedApprovedEventAuthorization(eventId);
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IEventService>()
            .TransitionAsync(_ownerId, eventId, false, false, "publish");
        Assert.True(result.Ok, result.Error);
    }

    private async Task<Guid> PublishedEventAsync()
    {
        var eventId = NewDraftEvent();
        await PublishAsync(eventId);
        return eventId;
    }

    private (Guid UserId, string Phone) NewUser(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var phone = $"98200{Interlocked.Increment(ref _seq) + 80000:D5}";
        var u = new User { Name = name, Phone = "91" + phone };
        db.Users.Add(u);
        db.SaveChanges();
        return (u.Id, phone);
    }

    private async Task AddOrgMemberAsync(string phone, OrgRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IOrgService>()
            .AddMemberAsync(_ownerId, _orgId, phone, role);
        Assert.True(result.Ok, result.Error);
    }

    private async Task ChangeRoleAsync(Guid userId, OrgRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IOrgService>()
            .ChangeRoleAsync(_ownerId, _orgId, userId, role);
        Assert.True(result.Ok, result.Error);
    }

    private async Task RemoveOrgMemberAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IOrgService>()
            .RemoveMemberAsync(_ownerId, _orgId, userId);
        Assert.True(result.Ok, result.Error);
    }

    private async Task RestoreOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        if (!await db.Memberships.AnyAsync(m => m.OrgId == _orgId && m.UserId == _ownerId))
        {
            db.Memberships.Add(new Membership
            {
                OrgId = _orgId, UserId = _ownerId, Role = OrgRole.Owner, IsVerified = true,
            });
            await db.SaveChangesAsync();
        }
    }

    private async Task DispatchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatchJob>().RunAsync(default);
    }

    private Guid? RoomIdFor(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.ChatRooms.AsNoTracking()
            .Where(r => r.EventId == eventId && r.Kind == ChatRoomKind.General)
            .Select(r => (Guid?)r.Id).FirstOrDefault();
    }

    private ChatMemberRole? RoleInRoom(Guid eventId, Guid userId)
    {
        var roomId = RoomIdFor(eventId);
        if (roomId is null) return null;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.ChatMembers.AsNoTracking()
            .Where(m => m.RoomId == roomId && m.UserId == userId)
            .Select(m => (ChatMemberRole?)m.Role).FirstOrDefault();
    }

    private int MemberRowCount(Guid eventId, Guid userId)
    {
        var roomId = RoomIdFor(eventId);
        if (roomId is null) return 0;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.ChatMembers.AsNoTracking().Count(m => m.RoomId == roomId && m.UserId == userId);
    }

    private int SystemMessageCount(Guid roomId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return db.ChatMessages.AsNoTracking().Count(m => m.RoomId == roomId && m.Kind == ChatMessageKind.System);
    }
}
