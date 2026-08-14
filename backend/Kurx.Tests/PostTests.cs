using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kurx.Infrastructure.Auth;

namespace Kurx.Tests;

/// <summary>
/// The Posts module (D-262).
///
/// <para>The tests that matter most here are the visibility ones. A feed is the one surface where a
/// leak is invisible in code review and obvious to the person it happened to, so every tier is checked
/// on every read path, and every miss is asserted to be <b>404, not 403</b> (D-018) — a 403 would
/// confirm that the post exists, which is exactly the fact being withheld.</para>
/// </summary>
public class PostTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    // Shared fixture: an org running a published event (plus an unpublished one), the ticket type its
    // tickets hang off, and the org member who runs both.
    private static Guid _orgId, _eventId, _draftEventId, _ticketTypeId, _organizerId;

    public PostTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (_reset) return;
            factory.ResetDatabase();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

            var organizer = new User { Name = "Post Organizer", Phone = "919820000001" };
            db.Users.Add(organizer);
            var category = new EventCategory { Level = CategoryLevel.Category, Name = "Posts", Slug = "posts-cat" };
            db.EventCategories.Add(category);
            db.SaveChanges();

            _orgId = factory.SeedVerifiedOrg(organizer.Id, "Posts Org");

            var ev = new Event
            {
                RepresentingOrgId = _orgId, CreatedBy = organizer.Id, CategoryId = category.Id,
                Title = "Posts Event", Slug = "posts-event", ShortCode = "PST001",
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

            // An unpublished event, for the D-018 check that listing its posts does not confirm it exists.
            var draft = new Event
            {
                RepresentingOrgId = _orgId, CreatedBy = organizer.Id, CategoryId = category.Id,
                Title = "Draft Event", Slug = "posts-draft-event", ShortCode = "PST002",
                Description = "d", VenueName = "v",
                StartsAt = DateTime.UtcNow.AddDays(9), EndsAt = DateTime.UtcNow.AddDays(10),
                Status = EventStatus.Draft,
            };
            db.Events.Add(draft);
            db.SaveChanges();
            _ticketTypeId = ticketType.Id;
            _draftEventId = draft.Id;
            _organizerId = organizer.Id;
            _reset = true;
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9182{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string? username = null)
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        var userId = tokens.GetProperty("user_id").GetGuid();

        if (username is not null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.Username = username;
            await db.SaveChangesAsync();
        }
        return (client, userId);
    }

    /// <summary>Drives the real ally flow (request → accept) so the feed and Connections visibility read
    /// the same rows production writes.</summary>
    private static async Task AllyAsync(HttpClient a, HttpClient b, Guid bId)
    {
        var req = await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId }));
        var connectionId = req.GetProperty("id").GetGuid();
        var accept = await b.PostAsJsonAsync($"/v1/allies/requests/{connectionId}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }

    /// <summary>Issues a real, non-void ticket through the full order → item → ticket chain, because a
    /// ticket is what the live participation check actually reads and its FK to <c>order_items</c> is
    /// enforced.</summary>
    private void GiveTicket(Guid userId, Guid? eventId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var evId = eventId ?? _eventId;

        var order = new Order
        {
            UserId = userId, EventId = evId, TicketTypeId = _ticketTypeId,
            Status = OrderStatus.Paid, AmountPaise = 0,
        };
        db.Orders.Add(order);
        var item = new OrderItem { OrderId = order.Id, TicketTypeId = _ticketTypeId, Qty = 1, UnitPricePaise = 0 };
        db.OrderItems.Add(item);
        db.Tickets.Add(new Ticket
        {
            OrderItemId = item.Id, EventId = evId, UserId = userId,
            HmacSig = "seed", State = TicketState.Issued,
        });
        db.SaveChanges();
    }

    private static async Task<Guid> CreatePostAsync(HttpClient client, string body, string visibility = "public",
        Guid? eventId = null, Guid? sharedPostId = null, IReadOnlyList<Guid>? mediaIds = null, object? poll = null)
    {
        var res = await client.PostAsJsonAsync("/v1/posts", new
        {
            body, visibility, eventId, sharedPostId, mediaIds, poll,
        });
        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"create failed: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage res, HttpStatusCode status, string error)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == status, $"expected {status}, got {(int)res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(error, doc.RootElement.GetProperty("error").GetString());
    }

    // ── contract ───────────────────────────────────────────────────────────────

    /// <summary>The platform contract is camelCase in, snake_case out (D-259 addendum), and it holds only
    /// because every response record sits in Kurx.Application.Abstractions. A view declared anywhere else
    /// silently emits camelCase — which is precisely the class of break this asserts against.</summary>
    [Fact]
    public async Task A_created_post_is_returned_snake_case_with_every_contract_field()
    {
        var (author, authorId) = await LoginAsync();
        var res = await author.PostAsJsonAsync("/v1/posts", new { body = "hello world", visibility = "public" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var post = await Json(res);
        Assert.Equal("text", post.GetProperty("kind").GetString());
        Assert.Equal("public", post.GetProperty("visibility").GetString());
        Assert.Equal("hello world", post.GetProperty("body").GetString());
        Assert.Equal(authorId, post.GetProperty("author").GetProperty("id").GetGuid());
        Assert.Equal(0, post.GetProperty("like_count").GetInt32());
        Assert.Equal(0, post.GetProperty("comment_count").GetInt32());
        Assert.Equal(0, post.GetProperty("share_count").GetInt32());
        Assert.False(post.GetProperty("liked_by_me").GetBoolean());
        Assert.False(post.GetProperty("saved_by_me").GetBoolean());
        Assert.True(post.GetProperty("can_edit").GetBoolean());
        Assert.True(post.GetProperty("can_delete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, post.GetProperty("edited_at").ValueKind);
        Assert.Empty(post.GetProperty("media").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, post.GetProperty("poll").ValueKind);
        Assert.Equal(JsonValueKind.Null, post.GetProperty("shared_post").ValueKind);
        Assert.NotEqual(JsonValueKind.Undefined, post.GetProperty("created_at").ValueKind);
    }

    [Fact]
    public async Task An_empty_body_with_nothing_attached_is_rejected()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new { body = "   ", visibility = "public" }),
            HttpStatusCode.BadRequest, "body_required");
    }

    [Fact]
    public async Task A_body_over_the_ceiling_is_rejected_with_the_code_the_client_switches_on()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new { body = new string('a', 3001), visibility = "public" }),
            HttpStatusCode.BadRequest, "body_too_long");
    }

    [Fact]
    public async Task An_unknown_visibility_is_rejected()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new { body = "x", visibility = "friends-of-friends" }),
            HttpStatusCode.BadRequest, "invalid_visibility");
    }

    [Fact]
    public async Task Event_participants_visibility_without_an_event_is_rejected()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new { body = "x", visibility = "event_participants" }),
            HttpStatusCode.BadRequest, "invalid_visibility");
    }

    // ── visibility, every tier, every read path ────────────────────────────────

    [Fact]
    public async Task A_public_post_is_readable_by_a_stranger()
    {
        var (author, _) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "public thing");

        Assert.Equal(HttpStatusCode.OK, (await stranger.GetAsync($"/v1/posts/{postId}")).StatusCode);
    }

    [Fact]
    public async Task A_connections_post_is_404_to_a_stranger_and_readable_by_an_ally()
    {
        var (author, authorId) = await LoginAsync();
        var (ally, allyId) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        await AllyAsync(author, ally, allyId);

        var postId = await CreatePostAsync(author, "for my connections", visibility: "connections");

        Assert.Equal(HttpStatusCode.OK, (await ally.GetAsync($"/v1/posts/{postId}")).StatusCode);
        // 404, never 403 (D-018): a 403 would confirm the post exists, which is the withheld fact.
        await AssertErrorAsync(await stranger.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    [Fact]
    public async Task An_event_participants_post_is_404_until_the_reader_holds_a_ticket()
    {
        var (organizer, organizerId) = await LoginAsync();
        // The author must be entitled to attach to the event at all.
        GiveTicket(organizerId);
        var (attendee, attendeeId) = await LoginAsync();
        var (stranger, _) = await LoginAsync();

        var postId = await CreatePostAsync(organizer, "see you at the venue",
            visibility: "event_participants", eventId: _eventId);

        await AssertErrorAsync(await attendee.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");

        GiveTicket(attendeeId);
        Assert.Equal(HttpStatusCode.OK, (await attendee.GetAsync($"/v1/posts/{postId}")).StatusCode);
        await AssertErrorAsync(await stranger.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    /// <summary>The participation check is live (D-015), not a claim: voiding the ticket must revoke
    /// access on the very next request, not at the reader's next login.</summary>
    [Fact]
    public async Task Voiding_a_ticket_immediately_revokes_event_participant_visibility()
    {
        var (organizer, organizerId) = await LoginAsync();
        GiveTicket(organizerId);
        var (attendee, attendeeId) = await LoginAsync();
        GiveTicket(attendeeId);

        var postId = await CreatePostAsync(organizer, "participants only",
            visibility: "event_participants", eventId: _eventId);
        Assert.Equal(HttpStatusCode.OK, (await attendee.GetAsync($"/v1/posts/{postId}")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.Tickets.Where(t => t.EventId == _eventId && t.UserId == attendeeId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.State, TicketState.Void));
        }

        await AssertErrorAsync(await attendee.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    [Fact]
    public async Task An_only_me_post_is_404_to_everyone_but_its_author()
    {
        var (author, authorId) = await LoginAsync();
        var (ally, allyId) = await LoginAsync();
        await AllyAsync(author, ally, allyId);

        var postId = await CreatePostAsync(author, "note to self", visibility: "only_me");

        Assert.Equal(HttpStatusCode.OK, (await author.GetAsync($"/v1/posts/{postId}")).StatusCode);
        await AssertErrorAsync(await ally.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    /// <summary>Every listing composes the same predicate as the single-post read, so a tier that is
    /// hidden on one path cannot be visible on another.</summary>
    [Fact]
    public async Task Restricted_posts_are_absent_from_the_hashtag_profile_and_event_listings()
    {
        var (author, authorId) = await LoginAsync(username: $"vis{Guid.NewGuid():N}"[..12]);
        GiveTicket(authorId);
        var (stranger, _) = await LoginAsync();
        var anonymous = _factory.CreateClient();

        var tag = $"vis{Guid.NewGuid():N}"[..12];
        var publicId = await CreatePostAsync(author, $"open #{tag}");
        var privateId = await CreatePostAsync(author, $"closed #{tag}", visibility: "only_me");
        var eventPublicId = await CreatePostAsync(author, "event public", eventId: _eventId);
        var eventPrivateId = await CreatePostAsync(author, "event restricted",
            visibility: "event_participants", eventId: _eventId);

        var username = await UsernameOf(authorId);

        var hashtag = await Json(await stranger.GetAsync($"/v1/hashtags/{tag}/posts"));
        AssertContainsExactly(hashtag, [publicId], [privateId]);

        var profile = await Json(await anonymous.GetAsync($"/v1/public/users/{username}/posts"));
        AssertContainsExactly(profile, [publicId, eventPublicId], [privateId, eventPrivateId]);

        var eventFeed = await Json(await stranger.GetAsync($"/v1/events/{_eventId}/posts"));
        AssertContainsExactly(eventFeed, [eventPublicId], [eventPrivateId]);
    }

    private async Task<string> UsernameOf(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.Username!).FirstAsync();
    }

    private static void AssertContainsExactly(JsonElement page, Guid[] present, Guid[] absent)
    {
        var ids = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToHashSet();
        foreach (var id in present) Assert.Contains(id, ids);
        foreach (var id in absent) Assert.DoesNotContain(id, ids);
    }

    [Fact]
    public async Task An_unknown_username_returns_an_empty_page_rather_than_a_404_oracle()
    {
        var anonymous = _factory.CreateClient();
        var res = await anonymous.GetAsync($"/v1/public/users/nobody{Guid.NewGuid():N}/posts");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Empty((await Json(res)).GetProperty("items").EnumerateArray());
    }

    // ── search ─────────────────────────────────────────────────────────────────

    /// <summary>Search composes the same visibility predicate as every other read path. This is the whole
    /// risk of adding it: a term that matches a restricted post must return nothing rather than a preview
    /// of it, and the author must still find their own.</summary>
    [Fact]
    public async Task Search_finds_a_public_post_and_never_one_the_caller_cannot_see()
    {
        var (author, _) = await LoginAsync();
        var (stranger, _) = await LoginAsync();

        var term = $"zeb{Guid.NewGuid():N}"[..12];
        var openId = await CreatePostAsync(author, $"an open note about {term}");
        var closedId = await CreatePostAsync(author, $"a private note about {term}", visibility: "only_me");

        AssertContainsExactly(await Json(await stranger.GetAsync($"/v1/posts/search?q={term}")), [openId], [closedId]);
        AssertContainsExactly(await Json(await author.GetAsync($"/v1/posts/search?q={term}")), [openId, closedId], []);
    }

    /// <summary>A blank box is an empty result, not the whole table — the difference between a no-op and
    /// handing every caller an unbounded scan of every post they may see.</summary>
    [Fact]
    public async Task An_empty_search_term_returns_an_empty_page_rather_than_everything()
    {
        var (author, _) = await LoginAsync();
        await CreatePostAsync(author, $"something searchable {Guid.NewGuid():N}");

        foreach (var qs in new[] { "", "q=", "q=%20%20" })
        {
            var res = await author.GetAsync($"/v1/posts/search?{qs}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Empty((await Json(res)).GetProperty("items").EnumerateArray());
        }
    }

    /// <summary>The term is what a person types, not a query language they have to get right.
    /// <c>websearch_to_tsquery</c> is chosen over <c>to_tsquery</c> precisely because the latter throws on
    /// input like a bare apostrophe — a 500 on a search box is the failure this pins shut. A quoted phrase
    /// is also a phrase, not two loose words.</summary>
    [Fact]
    public async Task Search_accepts_what_a_person_types_including_punctuation_and_phrases()
    {
        var (author, _) = await LoginAsync();
        var marker = $"phr{Guid.NewGuid():N}"[..12];
        var inOrder = await CreatePostAsync(author, $"{marker} the closing keynote was excellent");
        var reversed = await CreatePostAsync(author, $"{marker} keynote closing notes, excellent");

        // A quoted phrase matches only the post carrying those words adjacently.
        AssertContainsExactly(
            await Json(await author.GetAsync($"/v1/posts/search?q=%22closing%20keynote%22")), [inOrder], [reversed]);

        // Unquoted, both match — the words are ANDed, order-independent.
        AssertContainsExactly(
            await Json(await author.GetAsync($"/v1/posts/search?q=closing%20keynote")), [inOrder, reversed], []);

        // Input that to_tsquery would reject outright answers 200 with a result, never a 500.
        foreach (var hostile in new[] { "'", "don't", "%", "&|!", "a:*" })
        {
            var res = await author.GetAsync($"/v1/posts/search?q={Uri.EscapeDataString(hostile)}");
            Assert.True(res.StatusCode == HttpStatusCode.OK,
                $"search choked on {hostile}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    // ── feed composition ───────────────────────────────────────────────────────

    /// <summary>The single most consequential call in the module (D-262): the feed is an explicit graph,
    /// not "everything public". A stranger's public post is reachable — through their profile and through
    /// hashtags — but it does not arrive uninvited.</summary>
    [Fact]
    public async Task The_feed_carries_own_ally_and_ticketed_event_posts_but_not_a_strangers()
    {
        var (me, myId) = await LoginAsync();
        var (ally, allyId) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        var (organizer, organizerId) = await LoginAsync();
        await AllyAsync(me, ally, allyId);
        GiveTicket(myId);
        GiveTicket(organizerId);

        var mine = await CreatePostAsync(me, "mine");
        var theirs = await CreatePostAsync(ally, "ally's");
        var strangers = await CreatePostAsync(stranger, "stranger's");
        var eventPost = await CreatePostAsync(organizer, "about the event", eventId: _eventId);

        var feed = await Json(await me.GetAsync("/v1/feed?limit=50"));
        AssertContainsExactly(feed, [mine, theirs, eventPost], [strangers]);
    }

    [Fact]
    public async Task A_followed_orgs_event_post_reaches_the_feed()
    {
        var (me, _) = await LoginAsync();
        var (organizer, organizerId) = await LoginAsync();
        GiveTicket(organizerId);
        Assert.Equal(HttpStatusCode.OK, (await me.PostAsJsonAsync($"/v1/orgs/{_orgId}/follow", new { })).StatusCode);

        var eventPost = await CreatePostAsync(organizer, "org update", eventId: _eventId);

        var feed = await Json(await me.GetAsync("/v1/feed?limit=50"));
        AssertContainsExactly(feed, [eventPost], []);
    }

    [Fact]
    public async Task Attaching_a_post_to_an_event_the_author_has_no_part_in_is_rejected()
    {
        var (outsider, _) = await LoginAsync();
        await AssertErrorAsync(
            await outsider.PostAsJsonAsync("/v1/posts", new { body = "hijack", visibility = "public", eventId = _eventId }),
            HttpStatusCode.Forbidden, "not_event_participant");
    }

    /// <summary>D-018 applied to the event feed: a bare existence check would answer 200-with-an-empty-page
    /// for a draft event and 404 for a nonexistent one, and that difference is itself the disclosure.
    /// The org that runs the event still gets its posts.</summary>
    [Fact]
    public async Task Listing_the_posts_of_an_unpublished_event_does_not_confirm_it_exists()
    {
        var (stranger, _) = await LoginAsync();
        await AssertErrorAsync(await stranger.GetAsync($"/v1/events/{_draftEventId}/posts"),
            HttpStatusCode.NotFound, "event_not_found");
        // Indistinguishable from an event id that was never issued.
        await AssertErrorAsync(await stranger.GetAsync($"/v1/events/{Guid.NewGuid()}/posts"),
            HttpStatusCode.NotFound, "event_not_found");

        // The org running it is not locked out of its own draft.
        var organizer = await LoginAsAsync(_organizerId);
        Assert.Equal(HttpStatusCode.OK, (await organizer.GetAsync($"/v1/events/{_draftEventId}/posts")).StatusCode);
    }

    /// <summary>Signs in as an existing seeded user by attaching a phone to them, so the fixture's org
    /// member can drive HTTP routes.</summary>
    private async Task<HttpClient> LoginAsAsync(Guid userId)
    {
        // The number the user TYPES, and the number the platform STORES, are not the same string: a bare
        // national number normalizes to E.164 digits (91 + the ten). Seeding the typed form left the login
        // looking up "919182…" against a row holding "9182…", finding nothing, and creating a second
        // account — so the assertion below saw a different user id. Seed what NormalizePhone produces.
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
        Assert.Equal(userId, tokens.GetProperty("user_id").GetGuid());
        return client;
    }

    [Fact]
    public async Task Attaching_a_post_to_an_unknown_event_is_rejected()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new { body = "x", visibility = "public", eventId = Guid.NewGuid() }),
            HttpStatusCode.NotFound, "event_not_found");
    }

    // ── edit / delete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task An_author_may_edit_body_and_visibility_and_the_edit_is_stamped()
    {
        var (author, _) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "first draft");

        var res = await author.PatchAsJsonAsync($"/v1/posts/{postId}", new { body = "second draft", visibility = "only_me" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await Json(res);
        Assert.Equal("second draft", updated.GetProperty("body").GetString());
        Assert.Equal("only_me", updated.GetProperty("visibility").GetString());
        Assert.NotEqual(JsonValueKind.Null, updated.GetProperty("edited_at").ValueKind);

        // The new visibility takes effect immediately on every read path.
        await AssertErrorAsync(await stranger.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    /// <summary>Media, poll, event attachment and kind are fixed at creation (D-262) — the PATCH contract
    /// carries no field for them, and this pins that an edit cannot reach them by any other route.</summary>
    [Fact]
    public async Task An_edit_cannot_change_the_kind_or_the_attached_event()
    {
        var (author, authorId) = await LoginAsync();
        GiveTicket(authorId);
        var postId = await CreatePostAsync(author, "about the event", eventId: _eventId);

        await author.PatchAsJsonAsync($"/v1/posts/{postId}", new { body = "still about the event" });

        var post = await Json(await author.GetAsync($"/v1/posts/{postId}"));
        Assert.Equal("event", post.GetProperty("kind").GetString());
        Assert.Equal(_eventId, post.GetProperty("event").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_non_author_can_neither_edit_nor_delete()
    {
        var (author, _) = await LoginAsync();
        var (other, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "mine alone");

        await AssertErrorAsync(await other.PatchAsJsonAsync($"/v1/posts/{postId}", new { body = "hijacked" }),
            HttpStatusCode.Forbidden, "not_post_author");
        await AssertErrorAsync(await other.DeleteAsync($"/v1/posts/{postId}"),
            HttpStatusCode.Forbidden, "not_post_author");
    }

    [Fact]
    public async Task Deleting_is_soft_and_the_post_stops_being_served_everywhere()
    {
        var (author, authorId) = await LoginAsync();
        var postId = await CreatePostAsync(author, "temporary");

        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"/v1/posts/{postId}")).StatusCode);
        await AssertErrorAsync(await author.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");

        var mine = await Json(await author.GetAsync("/v1/me/posts?limit=50"));
        AssertContainsExactly(mine, [], [postId]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.Posts.AsNoTracking().FirstAsync(p => p.Id == postId);
        Assert.True(row.IsDeleted);                 // soft — the row survives so reshares and reports keep their FK
        Assert.Equal(authorId, row.DeletedBy);
    }

    // ── reshare ────────────────────────────────────────────────────────────────

    /// <summary>Sharing a share re-targets the original (D-262). Nesting is bounded by the DATA, not by
    /// client discipline, so a client that renders one level can never be handed two.</summary>
    [Fact]
    public async Task Sharing_a_share_attaches_to_the_original_and_never_nests()
    {
        var (a, _) = await LoginAsync();
        var (b, _) = await LoginAsync();
        var (c, _) = await LoginAsync();

        var originalId = await CreatePostAsync(a, "the original");
        var firstShareId = await CreatePostAsync(b, "worth reading", sharedPostId: originalId);
        var secondShareId = await CreatePostAsync(c, "agreed", sharedPostId: firstShareId);

        var second = await Json(await c.GetAsync($"/v1/posts/{secondShareId}"));
        Assert.Equal("share", second.GetProperty("kind").GetString());
        var shared = second.GetProperty("shared_post");
        Assert.Equal(originalId, shared.GetProperty("id").GetGuid());
        // Exactly one level: the embedded original carries no reshare of its own.
        Assert.Equal(JsonValueKind.Null, shared.GetProperty("shared_post").ValueKind);

        // Both shares counted against the original, computed in SQL.
        var original = await Json(await a.GetAsync($"/v1/posts/{originalId}"));
        Assert.Equal(2, original.GetProperty("share_count").GetInt32());
    }

    [Fact]
    public async Task Sharing_a_share_whose_original_is_no_longer_visible_is_rejected()
    {
        var (a, _) = await LoginAsync();
        var (b, _) = await LoginAsync();
        var (c, _) = await LoginAsync();

        var originalId = await CreatePostAsync(a, "the original");
        var shareId = await CreatePostAsync(b, "passing this on", sharedPostId: originalId);

        // The original goes away; the share survives, but there is nothing left to re-target to.
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/v1/posts/{originalId}")).StatusCode);

        await AssertErrorAsync(
            await c.PostAsJsonAsync("/v1/posts", new { body = "me too", visibility = "public", sharedPostId = shareId }),
            HttpStatusCode.BadRequest, "cannot_share_a_share");
    }

    /// <summary>The increment on create needs a matching decrement, or a post shared and un-shared a
    /// hundred times reads as a hundred live shares forever.</summary>
    [Fact]
    public async Task Deleting_a_reshare_gives_the_share_back_to_the_original()
    {
        var (a, _) = await LoginAsync();
        var (b, _) = await LoginAsync();
        var originalId = await CreatePostAsync(a, "the original");

        var shareId = await CreatePostAsync(b, "passing on", sharedPostId: originalId);
        Assert.Equal(1, (await Json(await a.GetAsync($"/v1/posts/{originalId}"))).GetProperty("share_count").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await b.DeleteAsync($"/v1/posts/{shareId}")).StatusCode);
        Assert.Equal(0, (await Json(await a.GetAsync($"/v1/posts/{originalId}"))).GetProperty("share_count").GetInt32());

        // Deleting a plain post touches nobody else's counter.
        var plain = await CreatePostAsync(b, "not a share");
        await b.DeleteAsync($"/v1/posts/{plain}");
        Assert.Equal(0, (await Json(await a.GetAsync($"/v1/posts/{originalId}"))).GetProperty("share_count").GetInt32());
    }

    /// <summary>The video ceiling is the platform's, not a product choice: the only storage provider
    /// today presigns back to this API, so an upload is an ordinary Kestrel request. Presign must refuse
    /// an oversize file up front with a code the client can explain, rather than letting it die part-way
    /// through the bytes on an opaque 413.</summary>
    [Fact]
    public async Task A_video_over_the_ceiling_is_refused_at_presign()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts/media/presign",
                new { fileName = "clip.mp4", contentType = "video/mp4", sizeBytes = PostMediaPolicy.MaxVideoBytes + 1 }),
            HttpStatusCode.BadRequest, "invalid_media");

        // And the ceiling is one the upload path can actually honour.
        Assert.True(PostMediaPolicy.MaxVideoBytes <= 28_000_000,
            "MaxVideoBytes exceeds Kestrel's default MaxRequestBodySize, so LocalDiskStorage uploads " +
            "would 413 mid-transfer instead of being refused at presign.");
    }

    [Fact]
    public async Task A_post_that_cannot_be_seen_cannot_be_shared()
    {
        var (author, _) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "private", visibility: "only_me");

        await AssertErrorAsync(
            await stranger.PostAsJsonAsync("/v1/posts", new { body = "look", visibility = "public", sharedPostId = postId }),
            HttpStatusCode.NotFound, "post_not_found");
    }

    // ── hashtags & mentions ────────────────────────────────────────────────────

    /// <summary>Extraction is server-side and only server-side. The grammar is character-for-character the
    /// one the web client tokenizes with, so what is linked is what was stored.</summary>
    [Fact]
    public async Task Hashtags_and_mentions_are_extracted_from_the_body()
    {
        var handle = $"m{Guid.NewGuid():N}"[..12];
        var (mentioned, mentionedId) = await LoginAsync(username: handle);
        var (author, _) = await LoginAsync();

        var tag = $"t{Guid.NewGuid():N}"[..12];
        var res = await author.PostAsJsonAsync("/v1/posts", new
        {
            body = $"hi @{handle}, see #{tag} and #{tag.ToUpperInvariant()} — also @nobody{Guid.NewGuid():N} and me@example.com",
            visibility = "public",
        });
        var post = await Json(res);

        // Case-folded and de-duplicated; an email address is not a mention.
        var tags = post.GetProperty("hashtags").EnumerateArray().Select(t => t.GetString()).ToList();
        Assert.Equal([tag], tags);

        // An unresolvable handle is dropped silently — a mention with no account behind it is neither
        // a link nor a notification.
        var mentions = post.GetProperty("mentions").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([mentionedId], mentions);
    }

    [Fact]
    public async Task A_mention_notifies_the_mentioned_user_and_never_the_author()
    {
        var handle = $"n{Guid.NewGuid():N}"[..12];
        var (_, mentionedId) = await LoginAsync(username: handle);
        var (author, authorId) = await LoginAsync(username: $"a{Guid.NewGuid():N}"[..12]);
        var authorHandle = await UsernameOf(authorId);

        await CreatePostAsync(author, $"@{handle} and @{authorHandle} should see this");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Single(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == mentionedId && n.Kind == "post_mention").ToListAsync());
        // Never notified about your own action.
        Assert.Empty(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == authorId && n.Kind == "post_mention").ToListAsync());
    }

    /// <summary>A mention is the one notification whose recipient the author picks freely. Without this
    /// gate, <c>@victim</c> inside an <c>only_me</c> post is a notification channel to anyone on the
    /// platform, carrying a deep link the recipient will only ever get a 404 for.</summary>
    [Fact]
    public async Task A_mention_inside_a_post_the_recipient_cannot_see_notifies_nobody()
    {
        var handle = $"h{Guid.NewGuid():N}"[..12];
        var (_, mentionedId) = await LoginAsync(username: handle);
        var (author, _) = await LoginAsync();

        await CreatePostAsync(author, $"@{handle} will never read this", visibility: "only_me");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Empty(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == mentionedId && n.Kind == "post_mention").ToListAsync());

        // The mention row still exists — the author's own render links it; only the notice is withheld.
        Assert.True(await db.PostMentions.AsNoTracking().AnyAsync(m => m.MentionedUserId == mentionedId));
    }

    /// <summary>The same gate, one tier up: an ally is reachable by a connections-only post, a stranger
    /// is not.</summary>
    [Fact]
    public async Task A_mention_in_a_connections_post_reaches_an_ally_but_not_a_stranger()
    {
        var allyHandle = $"y{Guid.NewGuid():N}"[..12];
        var strangerHandle = $"z{Guid.NewGuid():N}"[..12];
        var (ally, allyId) = await LoginAsync(username: allyHandle);
        var (_, strangerId) = await LoginAsync(username: strangerHandle);
        var (author, _) = await LoginAsync();
        await AllyAsync(author, ally, allyId);

        await CreatePostAsync(author, $"@{allyHandle} @{strangerHandle} circle only", visibility: "connections");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Single(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == allyId && n.Kind == "post_mention").ToListAsync());
        Assert.Empty(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == strangerId && n.Kind == "post_mention").ToListAsync());
    }

    [Fact]
    public async Task Trending_hashtags_count_only_posts_the_caller_can_see()
    {
        var (author, _) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        var tag = $"tr{Guid.NewGuid():N}"[..12];

        await CreatePostAsync(author, $"one #{tag}");
        await CreatePostAsync(author, $"two #{tag}", visibility: "only_me");

        var trending = await Json(await stranger.GetAsync("/v1/hashtags/trending?limit=50"));
        var row = trending.EnumerateArray().FirstOrDefault(t => t.GetProperty("tag").GetString() == tag);
        Assert.Equal(1, row.GetProperty("post_count").GetInt32());
    }

    // ── media ──────────────────────────────────────────────────────────────────

    /// <summary>A real 1x1 PNG. Byte-accurate because the point is that the server decodes it.</summary>
    private static byte[] Png() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>Runs the real flow: presign over HTTP, PUT the bytes through IStorage, confirm over HTTP.</summary>
    private async Task<Guid> UploadImageAsync(HttpClient client, string fileName = "pic.png", byte[]? bytes = null)
    {
        bytes ??= Png();
        var presign = await Json(await client.PostAsJsonAsync("/v1/posts/media/presign",
            new { fileName, contentType = "image/png", sizeBytes = bytes.Length }));
        var mediaId = presign.GetProperty("media_id").GetGuid();
        var storageKey = presign.GetProperty("storage_key").GetString()!;

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IStorage>().PutAsync(storageKey, bytes, "image/png");

        var confirm = await client.PostAsJsonAsync("/v1/posts/media/confirm", new { mediaId, storageKey });
        Assert.True(confirm.StatusCode == HttpStatusCode.OK,
            $"confirm failed: {(int)confirm.StatusCode} {await confirm.Content.ReadAsStringAsync()}");
        return mediaId;
    }

    [Fact]
    public async Task An_image_post_carries_its_media_and_derives_the_images_kind()
    {
        var (author, _) = await LoginAsync();
        var mediaId = await UploadImageAsync(author);
        var postId = await CreatePostAsync(author, "look at this", mediaIds: [mediaId]);

        var post = await Json(await author.GetAsync($"/v1/posts/{postId}"));
        Assert.Equal("images", post.GetProperty("kind").GetString());
        var file = Assert.Single(post.GetProperty("media").EnumerateArray().ToList());
        Assert.Equal("image", file.GetProperty("kind").GetString());
        Assert.Equal("image/png", file.GetProperty("content_type").GetString());
        Assert.Equal(1, file.GetProperty("width").GetInt32());
        Assert.False(string.IsNullOrEmpty(file.GetProperty("url").GetString()));
    }

    [Fact]
    public async Task More_images_than_the_cap_are_rejected()
    {
        var (author, _) = await LoginAsync();
        var ids = new List<Guid>();
        for (var i = 0; i <= PostMediaPolicy.MaxImages; i++) ids.Add(await UploadImageAsync(author, $"pic{i}.png"));

        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new { body = "album", visibility = "public", mediaIds = ids }),
            HttpStatusCode.BadRequest, "too_many_media");
    }

    [Fact]
    public async Task Media_uploaded_by_someone_else_cannot_be_attached()
    {
        var (owner, _) = await LoginAsync();
        var (thief, _) = await LoginAsync();
        var mediaId = await UploadImageAsync(owner);

        await AssertErrorAsync(
            await thief.PostAsJsonAsync("/v1/posts", new { body = "not mine", visibility = "public", mediaIds = new[] { mediaId } }),
            HttpStatusCode.BadRequest, "invalid_media");
    }

    [Fact]
    public async Task Media_that_was_never_confirmed_cannot_be_attached()
    {
        var (author, _) = await LoginAsync();
        var presign = await Json(await author.PostAsJsonAsync("/v1/posts/media/presign",
            new { fileName = "unconfirmed.png", contentType = "image/png", sizeBytes = 64 }));
        var mediaId = presign.GetProperty("media_id").GetGuid();

        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new { body = "x", visibility = "public", mediaIds = new[] { mediaId } }),
            HttpStatusCode.BadRequest, "invalid_media");
    }

    /// <summary>The declared content type is a hint and nothing more. Confirm re-derives the truth from
    /// the bytes, which is what makes the allow-list mean anything.</summary>
    [Fact]
    public async Task A_declared_png_that_is_really_an_executable_is_rejected_on_confirm()
    {
        var (author, _) = await LoginAsync();
        byte[] exe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];   // MZ — a PE binary

        var presign = await Json(await author.PostAsJsonAsync("/v1/posts/media/presign",
            new { fileName = "payload.png", contentType = "image/png", sizeBytes = exe.Length }));
        var mediaId = presign.GetProperty("media_id").GetGuid();
        var storageKey = presign.GetProperty("storage_key").GetString()!;

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IStorage>().PutAsync(storageKey, exe, "image/png");

        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts/media/confirm", new { mediaId, storageKey }),
            HttpStatusCode.BadRequest, "invalid_media");
    }

    [Fact]
    public async Task An_extension_that_contradicts_the_content_type_is_refused_at_presign()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts/media/presign",
                new { fileName = "invoice.pdf.exe", contentType = "image/png", sizeBytes = 100 }),
            HttpStatusCode.BadRequest, "invalid_media");
    }

    /// <summary>PostMedia.PostId is nullable because confirm happens before the post exists — which is the
    /// entire reason a sweep is needed. Anything never claimed would otherwise sit in storage forever.</summary>
    [Fact]
    public async Task Media_no_post_ever_claimed_is_swept()
    {
        var (author, _) = await LoginAsync();
        var mediaId = await UploadImageAsync(author, "orphan.png");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        // Age it past the grace window that protects an upload still in flight toward its post.
        await db.PostMedia.Where(m => m.Id == mediaId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, DateTime.UtcNow.AddHours(-25)));

        await scope.ServiceProvider.GetRequiredService<PostMediaCleanupJob>().RunAsync(CancellationToken.None);

        Assert.False(await db.PostMedia.AsNoTracking().AnyAsync(m => m.Id == mediaId));
    }

    // ── likes & saves ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Liking_twice_leaves_one_like_and_unliking_twice_leaves_none()
    {
        var (author, _) = await LoginAsync();
        var (reader, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "likeable");

        var first = await Json(await reader.PostAsJsonAsync($"/v1/posts/{postId}/like", new { }));
        Assert.True(first.GetProperty("liked").GetBoolean());
        Assert.Equal(1, first.GetProperty("like_count").GetInt32());

        var second = await Json(await reader.PostAsJsonAsync($"/v1/posts/{postId}/like", new { }));
        Assert.True(second.GetProperty("liked").GetBoolean());
        Assert.Equal(1, second.GetProperty("like_count").GetInt32());   // idempotent, counter unmoved

        var off = await Json(await reader.DeleteAsync($"/v1/posts/{postId}/like"));
        Assert.False(off.GetProperty("liked").GetBoolean());
        Assert.Equal(0, off.GetProperty("like_count").GetInt32());

        var offAgain = await Json(await reader.DeleteAsync($"/v1/posts/{postId}/like"));
        Assert.Equal(0, offAgain.GetProperty("like_count").GetInt32());  // never driven negative
    }

    [Fact]
    public async Task A_post_the_caller_cannot_see_cannot_be_liked()
    {
        var (author, _) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "private", visibility: "only_me");

        await AssertErrorAsync(await stranger.PostAsJsonAsync($"/v1/posts/{postId}/like", new { }),
            HttpStatusCode.NotFound, "post_not_found");
    }

    [Fact]
    public async Task Saving_and_unsaving_is_reflected_in_the_saved_listing()
    {
        var (author, _) = await LoginAsync();
        var (reader, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "save me");

        Assert.Equal(HttpStatusCode.NoContent, (await reader.PostAsJsonAsync($"/v1/posts/{postId}/save", new { })).StatusCode);
        // Idempotent: a repeated save is not an error.
        Assert.Equal(HttpStatusCode.NoContent, (await reader.PostAsJsonAsync($"/v1/posts/{postId}/save", new { })).StatusCode);

        var saved = await Json(await reader.GetAsync("/v1/me/posts/saved?limit=50"));
        AssertContainsExactly(saved, [postId], []);
        Assert.True(saved.GetProperty("items").EnumerateArray().First().GetProperty("saved_by_me").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await reader.DeleteAsync($"/v1/posts/{postId}/save")).StatusCode);
        AssertContainsExactly(await Json(await reader.GetAsync("/v1/me/posts/saved?limit=50")), [], [postId]);
    }

    /// <summary>A post saved while it was public must leave the saved list if its author later restricts
    /// it — the save is a bookmark, not a grant.</summary>
    [Fact]
    public async Task A_saved_post_disappears_once_its_author_restricts_it()
    {
        var (author, _) = await LoginAsync();
        var (reader, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "public for now");
        await reader.PostAsJsonAsync($"/v1/posts/{postId}/save", new { });

        await author.PatchAsJsonAsync($"/v1/posts/{postId}", new { visibility = "only_me" });

        AssertContainsExactly(await Json(await reader.GetAsync("/v1/me/posts/saved?limit=50")), [], [postId]);
    }

    // ── comments ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Commenting_increments_the_count_and_notifies_the_post_author()
    {
        var (author, authorId) = await LoginAsync();
        var (reader, readerId) = await LoginAsync();
        var postId = await CreatePostAsync(author, "discuss");

        var comment = await Json(await reader.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "nice" }));
        Assert.Equal("nice", comment.GetProperty("body").GetString());
        Assert.Equal(readerId, comment.GetProperty("author").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, comment.GetProperty("parent_comment_id").ValueKind);

        var post = await Json(await author.GetAsync($"/v1/posts/{postId}"));
        Assert.Equal(1, post.GetProperty("comment_count").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Single(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == authorId && n.Kind == "post_comment").ToListAsync());
    }

    [Fact]
    public async Task Commenting_on_your_own_post_notifies_nobody()
    {
        var (author, authorId) = await LoginAsync();
        var postId = await CreatePostAsync(author, "talking to myself");
        await author.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "hi me" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Empty(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == authorId && n.Kind == "post_comment").ToListAsync());
    }

    /// <summary>Exactly one level of threading — a reply to a reply attaches to the same parent, the rule
    /// chat already uses.</summary>
    [Fact]
    public async Task A_reply_to_a_reply_attaches_to_the_same_parent()
    {
        var (author, _) = await LoginAsync();
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        var postId = await CreatePostAsync(author, "thread");

        var root = await Json(await a.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "root" }));
        var rootId = root.GetProperty("id").GetGuid();

        var reply = await Json(await b.PostAsJsonAsync($"/v1/posts/{postId}/comments",
            new { body = "reply", parentCommentId = rootId }));
        Assert.Equal(rootId, reply.GetProperty("parent_comment_id").GetGuid());

        var nested = await Json(await a.PostAsJsonAsync($"/v1/posts/{postId}/comments",
            new { body = "reply to the reply", parentCommentId = reply.GetProperty("id").GetGuid() }));
        Assert.Equal(rootId, nested.GetProperty("parent_comment_id").GetGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Single(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == bId && n.Kind == "post_reply").ToListAsync());
    }

    [Fact]
    public async Task The_post_author_may_delete_someone_elses_comment_but_a_bystander_may_not()
    {
        var (author, _) = await LoginAsync();
        var (commenter, _) = await LoginAsync();
        var (bystander, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "moderated by me");

        var comment = await Json(await commenter.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "spam" }));
        var commentId = comment.GetProperty("id").GetGuid();

        await AssertErrorAsync(await bystander.DeleteAsync($"/v1/comments/{commentId}"),
            HttpStatusCode.Forbidden, "not_post_author");
        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"/v1/comments/{commentId}")).StatusCode);

        var post = await Json(await author.GetAsync($"/v1/posts/{postId}"));
        Assert.Equal(0, post.GetProperty("comment_count").GetInt32());
        var comments = await Json(await author.GetAsync($"/v1/posts/{postId}/comments"));
        Assert.Empty(comments.GetProperty("items").EnumerateArray());
    }

    /// <summary>Deleting a root takes its replies with it. Leaving them would strand them: the listing
    /// filters deleted rows, so each orphan would come back naming a parent no longer in the page and
    /// the client would have nothing to group it under. <c>comment-thread.tsx</c> already assumes this
    /// cascade in its optimistic update.</summary>
    [Fact]
    public async Task Deleting_a_comment_deletes_its_replies_and_the_count_drops_by_all_of_them()
    {
        var (author, _) = await LoginAsync();
        var (a, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "thread");

        var root = await Json(await a.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "root" }));
        var rootId = root.GetProperty("id").GetGuid();
        await a.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "r1", parentCommentId = rootId });
        await a.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "r2", parentCommentId = rootId });
        var keep = await Json(await a.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "unrelated" }));

        Assert.Equal(4, (await Json(await author.GetAsync($"/v1/posts/{postId}"))).GetProperty("comment_count").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/v1/comments/{rootId}")).StatusCode);

        // Root + both replies gone in one action, and the counter dropped by three, not by one.
        var listed = (await Json(await author.GetAsync($"/v1/posts/{postId}/comments?limit=50")))
            .GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([keep.GetProperty("id").GetGuid()], listed);
        Assert.Equal(1, (await Json(await author.GetAsync($"/v1/posts/{postId}"))).GetProperty("comment_count").GetInt32());
    }

    /// <summary>A page is a page of ROOTS, and each root brings its replies. Paginating flat would put a
    /// reply before the parent it belongs to (newest-first), so a reply to an older comment would arrive
    /// on page 1 with its parent still on page 2 — and the client, which groups by parent, would render
    /// it nowhere.</summary>
    [Fact]
    public async Task A_comment_page_always_carries_each_roots_replies_with_it()
    {
        var (author, _) = await LoginAsync();
        var (a, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "threaded");

        // The OLDEST root gets the NEWEST reply — the exact ordering that strands a reply when the list
        // is paginated flat.
        var oldRoot = (await Json(await a.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "old root" })))
            .GetProperty("id").GetGuid();
        var newerRoots = new List<Guid>();
        for (var i = 0; i < 3; i++)
            newerRoots.Add((await Json(await a.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = $"root {i}" })))
                .GetProperty("id").GetGuid());
        var lateReply = (await Json(await a.PostAsJsonAsync($"/v1/posts/{postId}/comments",
                new { body = "late reply", parentCommentId = oldRoot })))
            .GetProperty("id").GetGuid();

        // Page of 3 roots: the late reply must NOT be here, because its parent is not.
        var page1 = await Json(await author.GetAsync($"/v1/posts/{postId}/comments?limit=3"));
        var ids1 = page1.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(3, ids1.Count);                       // three roots, no replies belong to them
        Assert.DoesNotContain(lateReply, ids1);
        Assert.DoesNotContain(oldRoot, ids1);

        // Page 2 delivers the old root AND its late reply together, so the client can group them.
        var page2 = await Json(await author.GetAsync(
            $"/v1/posts/{postId}/comments?limit=3&cursor={page1.GetProperty("next_cursor").GetString()}"));
        var ids2 = page2.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(oldRoot, ids2);
        Assert.Contains(lateReply, ids2);
        Assert.Empty(ids1.Intersect(ids2));                // no page re-serves a thread already delivered
    }

    [Fact]
    public async Task A_stranger_cannot_comment_on_or_read_the_comments_of_a_restricted_post()
    {
        var (author, _) = await LoginAsync();
        var (stranger, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "connections only", visibility: "connections");

        await AssertErrorAsync(await stranger.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "hi" }),
            HttpStatusCode.NotFound, "post_not_found");
        await AssertErrorAsync(await stranger.GetAsync($"/v1/posts/{postId}/comments"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    [Fact]
    public async Task Comment_likes_are_idempotent()
    {
        var (author, _) = await LoginAsync();
        var (reader, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "p");
        var comment = await Json(await author.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "c" }));
        var commentId = comment.GetProperty("id").GetGuid();

        await reader.PostAsJsonAsync($"/v1/comments/{commentId}/like", new { });
        var twice = await Json(await reader.PostAsJsonAsync($"/v1/comments/{commentId}/like", new { }));
        Assert.Equal(1, twice.GetProperty("like_count").GetInt32());

        var off = await Json(await reader.DeleteAsync($"/v1/comments/{commentId}/like"));
        Assert.Equal(0, off.GetProperty("like_count").GetInt32());
        Assert.False(off.GetProperty("liked").GetBoolean());
    }

    [Fact]
    public async Task An_empty_comment_is_rejected_with_the_contract_code()
    {
        var (author, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "p");
        await AssertErrorAsync(await author.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "  " }),
            HttpStatusCode.BadRequest, "body_required");
        await AssertErrorAsync(
            await author.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = new string('c', 1001) }),
            HttpStatusCode.BadRequest, "body_too_long");
    }

    // ── polls ──────────────────────────────────────────────────────────────────

    private static object Poll(bool allowMultiple = false, DateTime? closesAt = null) => new
    {
        question = "Best day?",
        options = new[] { "Saturday", "Sunday", "Neither" },
        allowMultiple,
        closesAt,
    };

    [Fact]
    public async Task A_poll_post_returns_its_options_and_a_vote_moves_the_counts()
    {
        var (author, _) = await LoginAsync();
        var (voter, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "pick one", poll: Poll());

        var post = await Json(await voter.GetAsync($"/v1/posts/{postId}"));
        Assert.Equal("poll", post.GetProperty("kind").GetString());
        var poll = post.GetProperty("poll");
        Assert.Equal("Best day?", poll.GetProperty("question").GetString());
        Assert.False(poll.GetProperty("is_closed").GetBoolean());
        Assert.Equal(0, poll.GetProperty("total_votes").GetInt32());
        var options = poll.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal(3, options.Count);

        var optionId = options[1].GetProperty("id").GetGuid();
        var voted = await Json(await voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote",
            new { optionIds = new[] { optionId } }));
        Assert.Equal(1, voted.GetProperty("total_votes").GetInt32());
        var chosen = voted.GetProperty("options").EnumerateArray().First(o => o.GetProperty("id").GetGuid() == optionId);
        Assert.Equal(1, chosen.GetProperty("vote_count").GetInt32());
        Assert.True(chosen.GetProperty("voted_by_me").GetBoolean());
    }

    [Fact]
    public async Task Voting_twice_is_rejected()
    {
        var (author, _) = await LoginAsync();
        var (voter, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "pick one", poll: Poll());
        var optionIds = await OptionIdsAsync(voter, postId);

        await voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote", new { optionIds = new[] { optionIds[0] } });
        await AssertErrorAsync(
            await voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote", new { optionIds = new[] { optionIds[1] } }),
            HttpStatusCode.Conflict, "already_voted");
    }

    [Fact]
    public async Task A_single_choice_poll_refuses_more_than_one_option()
    {
        var (author, _) = await LoginAsync();
        var (voter, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "pick one", poll: Poll());
        var optionIds = await OptionIdsAsync(voter, postId);

        await AssertErrorAsync(
            await voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote", new { optionIds = new[] { optionIds[0], optionIds[1] } }),
            HttpStatusCode.BadRequest, "invalid_option");
    }

    [Fact]
    public async Task A_multi_select_poll_accepts_several_options_and_counts_each()
    {
        var (author, _) = await LoginAsync();
        var (voter, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "pick any", poll: Poll(allowMultiple: true));
        var optionIds = await OptionIdsAsync(voter, postId);

        var voted = await Json(await voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote",
            new { optionIds = new[] { optionIds[0], optionIds[2] } }));
        // TotalVotes counts votes CAST, so it is exactly the sum of the option counts (D-262).
        Assert.Equal(2, voted.GetProperty("total_votes").GetInt32());
        Assert.Equal(2, voted.GetProperty("options").EnumerateArray().Count(o => o.GetProperty("voted_by_me").GetBoolean()));
    }

    [Fact]
    public async Task Voting_after_the_poll_closes_is_rejected()
    {
        var (author, _) = await LoginAsync();
        var (voter, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "closing soon", poll: Poll(closesAt: DateTime.UtcNow.AddMinutes(5)));
        var optionIds = await OptionIdsAsync(voter, postId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            await db.PostPolls.Where(p => p.PostId == postId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ClosesAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        await AssertErrorAsync(
            await voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote", new { optionIds = new[] { optionIds[0] } }),
            HttpStatusCode.BadRequest, "poll_closed");
        Assert.True((await Json(await voter.GetAsync($"/v1/posts/{postId}")))
            .GetProperty("poll").GetProperty("is_closed").GetBoolean());
    }

    [Fact]
    public async Task A_poll_with_fewer_than_two_options_is_rejected()
    {
        var (author, _) = await LoginAsync();
        await AssertErrorAsync(
            await author.PostAsJsonAsync("/v1/posts", new
            {
                body = "one option", visibility = "public",
                poll = new { question = "Yes?", options = new[] { "Yes" }, allowMultiple = false, closesAt = (DateTime?)null },
            }),
            HttpStatusCode.BadRequest, "poll_needs_two_options");
    }

    [Fact]
    public async Task An_option_from_another_poll_is_rejected()
    {
        var (author, _) = await LoginAsync();
        var (voter, _) = await LoginAsync();
        var mine = await CreatePostAsync(author, "mine", poll: Poll());
        var theirs = await CreatePostAsync(author, "theirs", poll: Poll());
        var foreignOption = (await OptionIdsAsync(voter, theirs))[0];

        await AssertErrorAsync(
            await voter.PostAsJsonAsync($"/v1/posts/{mine}/poll/vote", new { optionIds = new[] { foreignOption } }),
            HttpStatusCode.BadRequest, "invalid_option");
    }

    private static async Task<List<Guid>> OptionIdsAsync(HttpClient client, Guid postId)
    {
        var post = await Json(await client.GetAsync($"/v1/posts/{postId}"));
        return post.GetProperty("poll").GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid()).ToList();
    }

    // ── pagination ─────────────────────────────────────────────────────────────

    /// <summary>Keyset, not offset (D-104's rule applied to posts). A post arriving mid-page must not
    /// shift the next page — with OFFSET it would, and the reader would see one post twice and miss
    /// another entirely.</summary>
    [Fact]
    public async Task Cursor_pagination_stays_stable_when_a_post_is_inserted_mid_read()
    {
        var (author, _) = await LoginAsync();
        var created = new List<Guid>();
        for (var i = 0; i < 6; i++) created.Add(await CreatePostAsync(author, $"page item {i}"));

        var first = await Json(await author.GetAsync("/v1/me/posts?limit=3"));
        var firstIds = first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        var cursor = first.GetProperty("next_cursor").GetString();
        Assert.Equal(3, firstIds.Count);
        Assert.False(string.IsNullOrEmpty(cursor));

        // Newest-first, so the newest three came back.
        Assert.Equal(created.AsEnumerable().Reverse().Take(3), firstIds);

        // A post lands between the two reads. It belongs on a page the reader has already passed, so it
        // must not appear on, nor displace anything from, the next one.
        var inserted = await CreatePostAsync(author, "arrived mid-read");

        var second = await Json(await author.GetAsync($"/v1/me/posts?limit=3&cursor={cursor}"));
        var secondIds = second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

        Assert.Equal(created.Take(3).Reverse(), secondIds);
        Assert.DoesNotContain(inserted, secondIds);
        Assert.Empty(firstIds.Intersect(secondIds));
    }

    [Fact]
    public async Task A_page_that_comes_back_short_reports_no_further_cursor()
    {
        var (author, _) = await LoginAsync();
        await CreatePostAsync(author, "only one");

        var page = await Json(await author.GetAsync("/v1/me/posts?limit=50"));
        Assert.Equal(JsonValueKind.Null, page.GetProperty("next_cursor").ValueKind);
    }

    // ── moderation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_hidden_post_is_404_to_everyone_and_post_hidden_to_its_author()
    {
        var (author, _) = await LoginAsync();
        var (reader, _) = await LoginAsync();
        var moderator = await _factory.ReviewerClientAsync();
        var postId = await CreatePostAsync(author, "borderline");

        Assert.Equal(HttpStatusCode.NoContent,
            (await moderator.PostAsJsonAsync($"/v1/admin/posts/{postId}/hide", new { reason = "spam" })).StatusCode);

        await AssertErrorAsync(await reader.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
        // The author is told, so moderation is not silent to the person it happened to — and only to them.
        await AssertErrorAsync(await author.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_hidden");

        Assert.Equal(HttpStatusCode.NoContent,
            (await moderator.PostAsJsonAsync($"/v1/admin/posts/{postId}/unhide", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/v1/posts/{postId}")).StatusCode);
    }

    [Fact]
    public async Task An_ordinary_user_cannot_reach_the_moderation_routes()
    {
        var (author, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "mine");

        Assert.Equal(HttpStatusCode.Forbidden, (await author.GetAsync("/v1/admin/posts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await author.PostAsJsonAsync($"/v1/admin/posts/{postId}/hide", new { reason = "because" })).StatusCode);
    }

    [Fact]
    public async Task A_hide_without_a_reason_is_refused()
    {
        var (author, _) = await LoginAsync();
        var moderator = await _factory.ReviewerClientAsync();
        var postId = await CreatePostAsync(author, "mine");

        await AssertErrorAsync(
            await moderator.PostAsJsonAsync($"/v1/admin/posts/{postId}/hide", new { reason = "" }),
            HttpStatusCode.BadRequest, "validation_failed");
    }

    /// <summary>The moderation queue deliberately sees what the feed does not: a reported private post is
    /// exactly what a moderator has to be able to look at.</summary>
    [Fact]
    public async Task The_moderation_queue_filters_by_open_reports_and_sees_private_posts()
    {
        var (author, _) = await LoginAsync();
        var (reporter, _) = await LoginAsync();
        var moderator = await _factory.ReviewerClientAsync();

        var marker = $"queue{Guid.NewGuid():N}"[..14];
        var reportedId = await CreatePostAsync(author, $"{marker} reported", visibility: "only_me");
        var quietId = await CreatePostAsync(author, $"{marker} quiet");

        // Posts reuse the existing reports endpoint — no second report table (D-262).
        var report = await reporter.PostAsJsonAsync("/v1/reports",
            new { entityType = "post", entityId = reportedId, reason = "spam", details = (string?)null });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);

        var all = await Json(await moderator.GetAsync($"/v1/admin/posts?q={marker}&pageSize=50"));
        AssertContainsExactly(all, [reportedId, quietId], []);
        Assert.Equal(2, all.GetProperty("total").GetInt32());

        var onlyReported = await Json(await moderator.GetAsync($"/v1/admin/posts?q={marker}&reported=true&pageSize=50"));
        AssertContainsExactly(onlyReported, [reportedId], [quietId]);
    }

    [Fact]
    public async Task A_moderator_may_delete_a_post_that_is_not_theirs()
    {
        var (author, _) = await LoginAsync();
        var moderator = await _factory.ReviewerClientAsync();
        var postId = await CreatePostAsync(author, "to be removed");

        Assert.Equal(HttpStatusCode.NoContent, (await moderator.DeleteAsync($"/v1/posts/{postId}")).StatusCode);
        await AssertErrorAsync(await author.GetAsync($"/v1/posts/{postId}"),
            HttpStatusCode.NotFound, "post_not_found");
    }

    [Fact]
    public async Task A_post_comment_is_an_accepted_report_subject()
    {
        var (author, _) = await LoginAsync();
        var (reporter, _) = await LoginAsync();
        var postId = await CreatePostAsync(author, "p");
        var comment = await Json(await author.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = "c" }));

        var res = await reporter.PostAsJsonAsync("/v1/reports", new
        {
            entityType = "post_comment", entityId = comment.GetProperty("id").GetGuid(),
            reason = "abuse", details = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("post_comment", (await Json(res)).GetProperty("entity_type").GetString());
    }

    // ── auth ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_feed_requires_a_token_while_the_public_profile_route_does_not()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/v1/feed")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/v1/public/users/anyone/posts")).StatusCode);
    }
}
