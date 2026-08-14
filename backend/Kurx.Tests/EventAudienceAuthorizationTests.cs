using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>The authorization contract for every event <b>audience</b> surface, per authority level (D-272).
///
/// <para><b>Why this exists.</b> D-269 consolidated event <i>management</i> onto <c>IEventAuthority</c> and
/// stopped there. Three audience checks kept their own inline "does a Membership row exist" query: attaching
/// a post to an event, reading an event's feed, and holding chat <c>Host</c>. All three shared one defect —
/// they never granted the event's <b>creator</b> anything, and only worked at all because a personally
/// represented event fabricates a self-representation Membership for its creator (D-268). Delete that row and
/// an owner silently lost their own event's feed and chat. All three also accepted a <c>Finance</c> seat,
/// which holds no authority over events at any other surface.</para>
///
/// <para><see cref="EventAuthorizationTests"/> is the management half of this matrix; this is the audience
/// half. Kept apart because the two answer different questions and a permission must never migrate between
/// them by accident.</para></summary>
public class EventAudienceAuthorizationTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    // An org-represented, published, publicly listed event, plus the ticket type its tickets hang off.
    private static Guid _categoryId, _orgId, _eventId, _ticketTypeId, _ownerId;

    public EventAudienceAuthorizationTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var owner = new User { Name = "Audience Owner", Phone = "919830000001" };
            db.Users.Add(owner);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Audience", Slug = "audience-cat" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _ownerId = owner.Id;
            _categoryId = category.Id;
            _orgId = factory.SeedVerifiedOrg(owner.Id, "Audience Org");

            var ev = new Event
            {
                RepresentingOrgId = _orgId, CreatedBy = owner.Id, CategoryId = category.Id,
                Title = "Audience Event", Slug = "audience-event", ShortCode = "AUD001",
                Description = "d", VenueName = "v", City = "Hyderabad",
                StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4),
                Status = EventStatus.Published,
            };
            db.Events.Add(ev);
            db.SaveChanges();
            _eventId = ev.Id;

            var ticketType = new TicketType
            {
                EventId = ev.Id, Name = "General", PricePaise = 0, Quantity = 1000,
                SaleStarts = DateTime.UtcNow.AddDays(-1), SaleEnds = DateTime.UtcNow.AddDays(30),
            };
            db.TicketTypes.Add(ticketType);
            db.SaveChanges();
            _ticketTypeId = ticketType.Id;
            _reset = true;
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9183{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync()
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    /// <summary>Signs in as an already-seeded user by attaching a phone to them.</summary>
    private async Task<HttpClient> LoginAsAsync(Guid userId)
    {
        // The number the user TYPES and the number the platform STORES are not the same string: a bare
        // national number normalizes to E.164 digits (91 + the ten). Seeding the typed form left
        // `otp/verify` looking up "9183…" against its own normalized "919183…", finding nothing, and
        // **registering a second account** — so this client was a stranger, not the owner, and every
        // assertion that the owner can reach their own draft or their own post failed as 404/403.
        // `PostTests.LoginAsAsync` already carried this fix; this copy was missed. Seed what
        // NormalizePhone produces.
        var typed = NextPhone();
        var stored = AuthService.NormalizePhone(typed);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Phone, stored).SetProperty(u => u.PhoneE164, "+" + stored));
        }
        var phone = typed;
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private void SeatIn(Guid userId, OrgRole role, Guid? orgId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.Memberships.Add(new Membership { OrgId = orgId ?? _orgId, UserId = userId, Role = role, IsVerified = true });
        db.SaveChanges();
    }

    /// <summary>An accepted programme participation (V3 §5.1) — a speaker/judge/mentor/volunteer.</summary>
    private void MakeParticipant(Guid userId, Guid? eventId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.EventParticipants.Add(new EventParticipant
        {
            EventId = eventId ?? _eventId, SubjectType = ParticipantSubjectType.Person,
            SubjectId = userId, RoleSlug = "speaker", State = ParticipantState.Active,
        });
        db.SaveChanges();
    }

    /// <summary>A real non-void ticket through the full order → item → ticket chain, because the FK to
    /// <c>order_items</c> is enforced and a ticket is what the audience floor actually reads.</summary>
    private void GiveTicket(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var order = new Order
        {
            UserId = userId, EventId = _eventId, TicketTypeId = _ticketTypeId,
            Status = OrderStatus.Paid, AmountPaise = 0,
        };
        db.Orders.Add(order);
        var item = new OrderItem { OrderId = order.Id, TicketTypeId = _ticketTypeId, Qty = 1, UnitPricePaise = 0 };
        db.OrderItems.Add(item);
        db.Tickets.Add(new Ticket
        {
            OrderItemId = item.Id, EventId = _eventId, UserId = userId,
            HmacSig = "seed", State = TicketState.Issued,
        });
        db.SaveChanges();
    }

    private static Task<HttpResponseMessage> AttachPostAsync(HttpClient client, Guid eventId, string body = "hello")
        => client.PostAsJsonAsync("/v1/posts", new { body, visibility = "public", eventId });

    private static async Task AssertErrorAsync(HttpResponseMessage res, HttpStatusCode status, string error)
    {
        var raw = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == status, $"expected {status}, got {(int)res.StatusCode}: {raw}");
        using var doc = JsonDocument.Parse(raw);
        Assert.Equal(error, doc.RootElement.GetProperty("error").GetString());
    }

    private async Task<EventAccess> ResolveAsync(Guid? userId, Guid eventId, bool isAdmin = false)
    {
        using var scope = _factory.Services.CreateScope();
        var authority = scope.ServiceProvider.GetRequiredService<IEventAuthority>();
        return await authority.ResolveAsync(userId, eventId, isAdmin);
    }

    // ── the audience ladder ────────────────────────────────────────────────────

    /// <summary>The whole point of D-272 in one assertion: a ticket is standing. Before this, a ticket holder
    /// resolved to <c>None</c> and every audience surface had to re-derive participation from the tickets
    /// table itself — which is exactly how the creator came to be omitted from all three of them.</summary>
    [Fact]
    public async Task A_ticket_holder_resolves_to_Participant_and_no_further()
    {
        var (_, attendeeId) = await LoginAsync();
        Assert.Equal(EventAuthorityLevel.None, (await ResolveAsync(attendeeId, _eventId)).Level);

        GiveTicket(attendeeId);
        var access = await ResolveAsync(attendeeId, _eventId);

        Assert.Equal(EventAuthorityLevel.Participant, access.Level);
        Assert.True(access.Can(EventPermission.Participate));
        // A ticket buys standing, never authority: every management permission stays shut.
        Assert.False(access.Can(EventPermission.ModerateAudience));
        Assert.False(access.Can(EventPermission.ViewAttendees));
        Assert.False(access.Can(EventPermission.ManageContent));
        Assert.False(access.Can(EventPermission.ManageLifecycle));
    }

    /// <summary>Every caller class against the audience floor, in one table. A Finance seat is the case worth
    /// staring at: it is an organization member, and before D-272 that alone opened all three surfaces.</summary>
    [Fact]
    public async Task Participate_is_granted_by_standing_and_never_by_a_bare_membership()
    {
        var owner = await ResolveAsync(_ownerId, _eventId);

        var (_, repId) = await LoginAsync();
        SeatIn(repId, OrgRole.Representative);
        var (_, staffId) = await LoginAsync();
        SeatIn(staffId, OrgRole.Staff);
        var (_, financeId) = await LoginAsync();
        SeatIn(financeId, OrgRole.Finance);
        var (_, speakerId) = await LoginAsync();
        MakeParticipant(speakerId);
        var (_, attendeeId) = await LoginAsync();
        GiveTicket(attendeeId);
        var (_, strangerId) = await LoginAsync();

        Assert.True(owner.Can(EventPermission.Participate));                                        // creator/owner
        Assert.True((await ResolveAsync(repId, _eventId)).Can(EventPermission.Participate));        // representative
        Assert.True((await ResolveAsync(staffId, _eventId)).Can(EventPermission.Participate));      // front-of-house
        Assert.True((await ResolveAsync(speakerId, _eventId)).Can(EventPermission.Participate));    // programme participant
        Assert.True((await ResolveAsync(attendeeId, _eventId)).Can(EventPermission.Participate));   // ticket holder
        Assert.True((await ResolveAsync(strangerId, _eventId, isAdmin: true)).Can(EventPermission.Participate));

        // A seat in the organization is NOT audience standing (D-272). Finance holds money authority over
        // the org and nothing at all over its events — the same answer it already gave everywhere else.
        Assert.False((await ResolveAsync(financeId, _eventId)).Can(EventPermission.Participate));
        Assert.False((await ResolveAsync(strangerId, _eventId)).Can(EventPermission.Participate));
        Assert.False((await ResolveAsync(null, _eventId)).Can(EventPermission.Participate));        // anonymous
    }

    /// <summary>ModerateAudience is front-of-house and up. Deliberately a rung above Participate: a ticket
    /// holder belongs in the room, a ticket holder does not run it.</summary>
    [Fact]
    public async Task ModerateAudience_starts_at_Staff_not_at_Participant()
    {
        var (_, staffId) = await LoginAsync();
        SeatIn(staffId, OrgRole.Staff);
        var (_, attendeeId) = await LoginAsync();
        GiveTicket(attendeeId);
        var (_, speakerId) = await LoginAsync();
        MakeParticipant(speakerId);
        var (_, financeId) = await LoginAsync();
        SeatIn(financeId, OrgRole.Finance);

        Assert.True((await ResolveAsync(_ownerId, _eventId)).Can(EventPermission.ModerateAudience));
        Assert.True((await ResolveAsync(staffId, _eventId)).Can(EventPermission.ModerateAudience));
        Assert.False((await ResolveAsync(attendeeId, _eventId)).Can(EventPermission.ModerateAudience));
        Assert.False((await ResolveAsync(speakerId, _eventId)).Can(EventPermission.ModerateAudience));
        Assert.False((await ResolveAsync(financeId, _eventId)).Can(EventPermission.ModerateAudience));
    }

    // ── the hidden coupling ────────────────────────────────────────────────────

    /// <summary>The regression this whole decision exists for. A personally represented event fabricates a
    /// self-representation Membership for its creator (D-268); before D-272 that row was the ONLY thing
    /// granting the creator their own event's feed, post attachment and chat Host. Deleting it must change
    /// nothing — the creator's standing comes from <c>Event.CreatedBy</c>, not from a synthetic seat.</summary>
    [Fact]
    public async Task An_event_creator_keeps_full_audience_standing_with_no_membership_row_at_all()
    {
        var (creator, creatorId) = await LoginAsync();
        var created = await Json(await creator.CreateEventAsync(null, new
        {
            title = "Solo Meetup",
            description = "An event with plenty of detail for validation.",
            categoryId = _categoryId,
            venueName = "Main Hall",
            city = "Vizag",
            startsAt = DateTime.UtcNow.AddDays(20),
            endsAt = DateTime.UtcNow.AddDays(20).AddHours(4),
        }));
        var eventId = created.GetProperty("id").GetGuid();

        int removed;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var orgId = await db.Events.Where(e => e.Id == eventId).Select(e => e.RepresentingOrgId).FirstAsync();
            removed = await db.Memberships.Where(m => m.OrgId == orgId && m.UserId == creatorId).ExecuteDeleteAsync();
        }
        // Guard the guard: if the self-representation stops seeding a membership, this test must stop
        // claiming to prove anything about it.
        Assert.Equal(1, removed);

        var access = await ResolveAsync(creatorId, eventId);
        Assert.Equal(EventAuthorityLevel.Manager, access.Level);
        Assert.True(access.Can(EventPermission.Participate));
        Assert.True(access.Can(EventPermission.ModerateAudience));

        // …and over HTTP, on both audience surfaces.
        Assert.Equal(HttpStatusCode.OK, (await AttachPostAsync(creator, eventId, "my own event")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await creator.GetAsync($"/v1/events/{eventId}/posts")).StatusCode);
    }

    // ── post creation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Attaching_a_post_to_an_event_follows_the_audience_floor()
    {
        var (attendee, attendeeId) = await LoginAsync();
        GiveTicket(attendeeId);
        var (speaker, speakerId) = await LoginAsync();
        MakeParticipant(speakerId);
        var (staff, staffId) = await LoginAsync();
        SeatIn(staffId, OrgRole.Staff);
        var (finance, financeId) = await LoginAsync();
        SeatIn(financeId, OrgRole.Finance);
        var (stranger, _) = await LoginAsync();
        var ownerClient = await LoginAsAsync(_ownerId);

        Assert.Equal(HttpStatusCode.OK, (await AttachPostAsync(ownerClient, _eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AttachPostAsync(attendee, _eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AttachPostAsync(speaker, _eventId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AttachPostAsync(staff, _eventId)).StatusCode);

        await AssertErrorAsync(await AttachPostAsync(finance, _eventId),
            HttpStatusCode.Forbidden, "not_event_participant");
        await AssertErrorAsync(await AttachPostAsync(stranger, _eventId),
            HttpStatusCode.Forbidden, "not_event_participant");

        // Anonymous never reaches the service at all.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AttachPostAsync(_factory.CreateClient(), _eventId)).StatusCode);

        // A nonexistent event is 404 even for a caller who could post to a real one (D-018).
        await AssertErrorAsync(await AttachPostAsync(attendee, Guid.NewGuid()),
            HttpStatusCode.NotFound, "event_not_found");
    }

    // ── event feed ─────────────────────────────────────────────────────────────

    /// <summary>Two independent doors on the event feed, and they answer different questions: publicity is a
    /// property of the event, standing is a property of the caller. A listed public event stays readable by
    /// anyone signed in — including the Finance seat that holds no standing — because it is public, not
    /// because of who they are.</summary>
    [Fact]
    public async Task A_published_public_events_feed_stays_open_while_a_draft_needs_standing()
    {
        Guid draftId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var draft = new Event
            {
                RepresentingOrgId = _orgId, CreatedBy = _ownerId, CategoryId = _categoryId,
                Title = "Audience Draft", Slug = $"audience-draft-{Guid.NewGuid():N}"[..30], ShortCode = "AUD002",
                Description = "d", VenueName = "v",
                StartsAt = DateTime.UtcNow.AddDays(9), EndsAt = DateTime.UtcNow.AddDays(10),
                Status = EventStatus.Draft,
            };
            db.Events.Add(draft);
            await db.SaveChangesAsync();
            draftId = draft.Id;
        }

        var (stranger, _) = await LoginAsync();
        var (finance, financeId) = await LoginAsync();
        SeatIn(financeId, OrgRole.Finance);
        var (staff, staffId) = await LoginAsync();
        SeatIn(staffId, OrgRole.Staff);
        var ownerClient = await LoginAsAsync(_ownerId);

        // Published + public + listed: the publicity door.
        Assert.Equal(HttpStatusCode.OK, (await stranger.GetAsync($"/v1/events/{_eventId}/posts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await finance.GetAsync($"/v1/events/{_eventId}/posts")).StatusCode);

        // Draft: only standing gets in, and a miss is indistinguishable from a nonexistent id (D-018).
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/v1/events/{draftId}/posts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/v1/events/{draftId}/posts")).StatusCode);
        await AssertErrorAsync(await stranger.GetAsync($"/v1/events/{draftId}/posts"),
            HttpStatusCode.NotFound, "event_not_found");
        await AssertErrorAsync(await finance.GetAsync($"/v1/events/{draftId}/posts"),
            HttpStatusCode.NotFound, "event_not_found");
        await AssertErrorAsync(await stranger.GetAsync($"/v1/events/{Guid.NewGuid()}/posts"),
            HttpStatusCode.NotFound, "event_not_found");
    }

    // ── event_participants post visibility ─────────────────────────────────────

    /// <summary>The query-side mirror of the audience floor. <c>VisibleTo</c> cannot resolve one caller per
    /// row, so it re-expresses the four arms as a predicate; this pins the two encodings together. Before
    /// D-272 it read tickets only — so an event's own creator could not see a participants-only post on
    /// their own event, and neither could the staff running it.</summary>
    [Fact]
    public async Task Event_participants_visibility_matches_the_audience_floor_exactly()
    {
        var (author, authorId) = await LoginAsync();
        GiveTicket(authorId);
        var postId = (await Json(await author.PostAsJsonAsync("/v1/posts",
            new { body = "participants only", visibility = "event_participants", eventId = _eventId })))
            .GetProperty("id").GetGuid();

        var (attendee, attendeeId) = await LoginAsync();
        GiveTicket(attendeeId);
        var (speaker, speakerId) = await LoginAsync();
        MakeParticipant(speakerId);
        var (staff, staffId) = await LoginAsync();
        SeatIn(staffId, OrgRole.Staff);
        var (finance, financeId) = await LoginAsync();
        SeatIn(financeId, OrgRole.Finance);
        var (stranger, _) = await LoginAsync();
        var ownerClient = await LoginAsAsync(_ownerId);

        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/v1/posts/{postId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/v1/posts/{postId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await speaker.GetAsync($"/v1/posts/{postId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await attendee.GetAsync($"/v1/posts/{postId}")).StatusCode);

        // 404 not 403 (D-018): a 403 would confirm the post exists, which is the fact being withheld.
        await AssertErrorAsync(await finance.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
        await AssertErrorAsync(await stranger.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    /// <summary>Still live, never a claim (D-015): voiding the ticket revokes standing on the very next
    /// request. Asserted on the predicate path because that is where the check moved.</summary>
    [Fact]
    public async Task Voiding_a_ticket_immediately_revokes_audience_standing()
    {
        var (author, authorId) = await LoginAsync();
        GiveTicket(authorId);
        var (attendee, attendeeId) = await LoginAsync();
        GiveTicket(attendeeId);

        var postId = (await Json(await author.PostAsJsonAsync("/v1/posts",
            new { body = "see you there", visibility = "event_participants", eventId = _eventId })))
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await attendee.GetAsync($"/v1/posts/{postId}")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Tickets.Where(t => t.EventId == _eventId && t.UserId == attendeeId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.State, TicketState.Void));
        }

        Assert.Equal(EventAuthorityLevel.None, (await ResolveAsync(attendeeId, _eventId)).Level);
        await AssertErrorAsync(await attendee.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
        await AssertErrorAsync(await AttachPostAsync(attendee, _eventId),
            HttpStatusCode.Forbidden, "not_event_participant");
    }

    // ── chat ───────────────────────────────────────────────────────────────────

    /// <summary>Host seeding at publish. The creator is seeded by owning the event, with no membership read
    /// at all; a Finance seat is not seeded, because it is not front-of-house.</summary>
    [Fact]
    public async Task Publish_seeds_chat_Host_from_standing_not_from_the_membership_roster()
    {
        var (_, managerId) = await LoginAsync();
        SeatIn(managerId, OrgRole.Manager);
        var (_, staffId) = await LoginAsync();
        SeatIn(staffId, OrgRole.Staff);
        var (_, financeId) = await LoginAsync();
        SeatIn(financeId, OrgRole.Finance);
        var (_, attendeeId) = await LoginAsync();
        GiveTicket(attendeeId);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        await chat.AddEventHostsAsync(_eventId);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var roomId = await db.ChatRooms.Where(r => r.EventId == _eventId && r.Kind == ChatRoomKind.General)
            .Select(r => r.Id).FirstAsync();
        var roles = await db.ChatMembers.Where(m => m.RoomId == roomId)
            .ToDictionaryAsync(m => m.UserId, m => m.Role);

        Assert.Equal(ChatMemberRole.Host, roles[_ownerId]);
        Assert.Equal(ChatMemberRole.Host, roles[managerId]);
        // D-300 — a Staff seat joins the room as a MEMBER. It arrived as a Host while seeding read
        // ModeratorRoles, which silently made an operational seat carry the power to ban attendees and
        // delete anyone's messages. The seeding still comes from standing rather than the roster, which is
        // what this test is named for; only the rung a Staff seat reaches changed.
        Assert.Equal(ChatMemberRole.Member, roles[staffId]);
        Assert.False(roles.ContainsKey(financeId));
        Assert.False(roles.ContainsKey(attendeeId));   // a ticket joins the room by its own path, not as Host
    }

    /// <summary>Dropping a staff assignment must not strip Host from someone who holds it by standing, and
    /// must strip it from someone who only held it by the assignment.</summary>
    [Fact]
    public async Task Removing_a_staff_assignment_keeps_Host_only_where_standing_grants_it()
    {
        var (_, seatedId) = await LoginAsync();
        SeatIn(seatedId, OrgRole.Manager);
        var (_, externalId) = await LoginAsync();

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        await chat.EnsureRoomExistsAsync(_eventId);
        foreach (var uid in new[] { _ownerId, seatedId, externalId })
            await chat.AddMemberByEventAsync(_eventId, uid, "Host");

        foreach (var uid in new[] { _ownerId, seatedId, externalId })
            await chat.RemoveStaffMemberAsync(_eventId, uid);

        var roomId = await db.ChatRooms.Where(r => r.EventId == _eventId && r.Kind == ChatRoomKind.General)
            .Select(r => r.Id).FirstAsync();
        var roles = await db.ChatMembers.Where(m => m.RoomId == roomId)
            .ToDictionaryAsync(m => m.UserId, m => m.Role);

        Assert.Equal(ChatMemberRole.Host, roles[_ownerId]);     // by owning the event — no seat behind it
        Assert.Equal(ChatMemberRole.Host, roles[seatedId]);     // by the Manager seat
        Assert.False(roles.ContainsKey(externalId));            // held it only by the assignment, and no ticket
    }
}
