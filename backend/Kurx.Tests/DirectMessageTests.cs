using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Jobs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Direct messages (D-264).
///
/// <para>The load-bearing tests here are the two the design exists for: <b>one room per pair under
/// concurrent creation</b> (an application-level existence check cannot do this, and the failure —
/// two rooms each holding half a conversation — is silent and unrecoverable), and <b>a non-ally DM
/// notifies nobody until accepted</b>, which is the only thing standing between an open DM endpoint
/// and an abuse surface.</para>
///
/// <para>Event chat regressions are covered by the existing Chat* classes, which must keep passing
/// unchanged — that is the real proof that making <c>EventId</c> nullable cost nothing.</para>
/// </summary>
public class DirectMessageTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public DirectMessageTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9188{Interlocked.Increment(ref _phoneSeq):D6}";

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

    private static async Task AllyAsync(HttpClient a, HttpClient b, Guid bId)
    {
        var req = await Json(await a.PostAsJsonAsync("/v1/allies/requests", new { targetUserId = bId }));
        var id = req.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsJsonAsync($"/v1/allies/requests/{id}/accept", new { })).StatusCode);
    }

    private static async Task<Guid> OpenDmAsync(HttpClient client, Guid otherId)
    {
        var res = await client.PostAsJsonAsync($"/v1/dm/{otherId}", new { });
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"open dm: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return (await Json(res)).GetProperty("room_id").GetGuid();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Guid roomId, string body)
        => client.PostAsJsonAsync($"/v1/chat/rooms/{roomId}/messages", new { body });

    /// <summary>Reads a JSON array, failing with the actual body when the response was not one — an
    /// error page enumerated as an array otherwise reports a type mismatch instead of the real cause.</summary>
    private static async Task<List<JsonElement>> JsonArray(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Array, $"expected an array, got: {body}");
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    // ── Room creation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Opening_a_dm_twice_returns_the_same_room()
    {
        var (a, _) = await LoginAsync();
        var (_, bId) = await LoginAsync();

        var first = await OpenDmAsync(a, bId);
        var second = await OpenDmAsync(a, bId);
        Assert.Equal(first, second);
    }

    /// <summary>Either party opening it finds the one room — the pair is canonicalised, so who taps
    /// first cannot decide which room exists.</summary>
    [Fact]
    public async Task Both_parties_opening_a_dm_land_in_the_same_room()
    {
        var (a, aId) = await LoginAsync();
        var (b, bId) = await LoginAsync();

        Assert.Equal(await OpenDmAsync(a, bId), await OpenDmAsync(b, aId));
    }

    /// <summary>The race the unique index exists for. An application "does a room exist?" check cannot
    /// prevent this, and the resulting split history is unrecoverable once both rooms have traffic.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Simultaneous_opens_from_both_sides_create_exactly_one_room(bool reverseOrder)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var (a, aId) = await LoginAsync();
            var (b, bId) = await LoginAsync();

            var first = a.PostAsJsonAsync($"/v1/dm/{bId}", new { });
            var second = b.PostAsJsonAsync($"/v1/dm/{aId}", new { });
            var responses = reverseOrder ? await Task.WhenAll(second, first) : await Task.WhenAll(first, second);

            foreach (var r in responses)
                Assert.True(r.StatusCode == HttpStatusCode.OK,
                    $"attempt {attempt}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var (low, high) = aId.CompareTo(bId) < 0 ? (aId, bId) : (bId, aId);
            var rooms = await db.ChatRooms.AsNoTracking()
                .Where(r => r.DirectLowUserId == low && r.DirectHighUserId == high).ToListAsync();

            Assert.True(rooms.Count == 1, $"attempt {attempt}: expected 1 room for the pair, found {rooms.Count}");
            // And both racers agree on which room it is.
            var ids = new List<Guid>();
            foreach (var r in responses) ids.Add((await Json(r)).GetProperty("room_id").GetGuid());
            Assert.Single(ids.Distinct());
            Assert.Equal(rooms[0].Id, ids[0]);

            // Exactly two members, no duplicates from the losing insert.
            Assert.Equal(2, await db.ChatMembers.AsNoTracking().CountAsync(m => m.RoomId == rooms[0].Id));
        }
    }

    [Fact]
    public async Task You_cannot_dm_yourself()
    {
        var (a, aId) = await LoginAsync();
        var res = await a.PostAsJsonAsync($"/v1/dm/{aId}", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("cannot_dm_self", (await Json(res)).GetProperty("error").GetString());
    }

    // ── Requests: the spam control ─────────────────────────────────────────────

    /// <summary>A DM from a stranger lands Pending and notifies NOBODY. Without this an open DM
    /// endpoint is an abuse surface from the hour it ships.</summary>
    [Fact]
    public async Task A_dm_from_a_non_ally_is_a_request_and_notifies_nobody()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();

        var roomId = await OpenDmAsync(a, bId);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(a, roomId, "hello stranger")).StatusCode);
        await RunNotificationJobsAsync();

        Assert.Equal(0, await CountNotificationsAsync(bId, "dm_message"));

        // It is in the recipient's Requests tab, and nowhere else.
        var requests = await JsonArray(await b.GetAsync("/v1/me/dm/requests"));
        Assert.Single(requests);
        Assert.Equal(roomId, requests[0].GetProperty("room_id").GetGuid());
        Assert.True(requests[0].GetProperty("is_request").GetBoolean());
        Assert.Empty(await JsonArray(await b.GetAsync("/v1/me/dm")));

        // The sender sees their own outgoing conversation, not a request for themselves to accept.
        Assert.Empty(await JsonArray(await a.GetAsync("/v1/me/dm/requests")));
    }

    [Fact]
    public async Task Allies_skip_the_request_gate_and_are_notified()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        await AllyAsync(a, b, bId);

        var roomId = await OpenDmAsync(a, bId);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(a, roomId, "hi ally")).StatusCode);
        await RunNotificationJobsAsync();

        Assert.Equal(1, await CountNotificationsAsync(bId, "dm_message"));
        Assert.Single(await JsonArray(await b.GetAsync("/v1/me/dm")));
        Assert.Empty(await JsonArray(await b.GetAsync("/v1/me/dm/requests")));
    }

    [Fact]
    public async Task Accepting_a_request_moves_it_into_the_conversation_list_and_re_enables_notices()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();

        var roomId = await OpenDmAsync(a, bId);
        await SendAsync(a, roomId, "before accept");

        Assert.Equal(HttpStatusCode.NoContent,
            (await b.PostAsJsonAsync($"/v1/dm/{roomId}/requests/accept", new { })).StatusCode);

        Assert.Empty(await JsonArray(await b.GetAsync("/v1/me/dm/requests")));
        Assert.Single(await JsonArray(await b.GetAsync("/v1/me/dm")));

        await SendAsync(a, roomId, "after accept");
        await RunNotificationJobsAsync();
        Assert.True(await CountNotificationsAsync(bId, "dm_message") >= 1);
    }

    // ── D-292: the request gate's silent failure ───────────────────────────────

    /// <summary>The regression this class was missing. Replying to a request ACCEPTS it.
    ///
    /// <para>Before D-292 the recipient could reply — only <c>Declined</c> refused delivery — but the room
    /// stayed <c>Pending</c>, and Pending is exactly the state <c>ChatNotificationJob</c> returns early on.
    /// So the recipient answered, <b>the sender was never notified</b>, and the conversation died with both
    /// of them believing they had sent it. The room also never left the Requests tab, because
    /// <c>ListAsync</c> shows Accepted only. Every assertion below failed before the fix.</para></summary>
    [Fact]
    public async Task Replying_to_a_request_accepts_it_and_notifies_the_sender()
    {
        var (a, aId) = await LoginAsync();
        var (b, bId) = await LoginAsync();

        var roomId = await OpenDmAsync(a, bId);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(a, roomId, "hello stranger")).StatusCode);
        await RunNotificationJobsAsync();
        Assert.Equal(0, await CountNotificationsAsync(bId, "dm_message"));   // still a request

        // The recipient replies WITHOUT touching the accept endpoint.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(b, roomId, "who is this?")).StatusCode);
        await RunNotificationJobsAsync();

        // The reply reached the sender — the whole point. This was 0 before the fix.
        Assert.Equal(1, await CountNotificationsAsync(aId, "dm_message"));

        // And it is a conversation now, on both sides, in neither Requests tab.
        Assert.Empty(await JsonArray(await b.GetAsync("/v1/me/dm/requests")));
        Assert.Single(await JsonArray(await b.GetAsync("/v1/me/dm")));
        Assert.Single(await JsonArray(await a.GetAsync("/v1/me/dm")));
    }

    /// <summary>The other half of the rule: the INITIATOR sending again is just another unanswered
    /// message. If their own send accepted the request the gate would be decorative.</summary>
    [Fact]
    public async Task The_initiator_sending_again_does_not_accept_their_own_request()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();

        var roomId = await OpenDmAsync(a, bId);
        await SendAsync(a, roomId, "hello");
        await SendAsync(a, roomId, "hello again");
        await RunNotificationJobsAsync();

        Assert.Equal(0, await CountNotificationsAsync(bId, "dm_message"));
        Assert.Single(await JsonArray(await b.GetAsync("/v1/me/dm/requests")));
        Assert.Empty(await JsonArray(await b.GetAsync("/v1/me/dm")));
    }

    /// <summary>The room view carries the request state. Without it a client cannot tell a request from
    /// an ordinary conversation, which is why it rendered a composer over one.</summary>
    [Fact]
    public async Task A_pending_request_reports_its_state_and_who_opened_it()
    {
        var (a, aId) = await LoginAsync();
        var (_, bId) = await LoginAsync();

        var room = await Json(await a.PostAsJsonAsync($"/v1/dm/{bId}", new { }));
        Assert.Equal("pending", room.GetProperty("dm_request_state").GetString());
        Assert.Equal(aId, room.GetProperty("dm_initiated_by").GetGuid());
    }

    /// <summary>A declined room must not advertise a composer. It refused every send with
    /// <c>dm_declined</c> while reporting <c>can_post: true</c>, so the user typed, sent, and got an
    /// error with nothing on screen explaining why.</summary>
    [Fact]
    public async Task A_declined_request_reports_that_it_cannot_be_posted_to()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        Assert.Equal(HttpStatusCode.NoContent,
            (await b.PostAsJsonAsync($"/v1/dm/{roomId}/requests/decline", new { })).StatusCode);

        // Re-opening returns the existing room, so this is the view the client would render from.
        var room = await Json(await a.PostAsJsonAsync($"/v1/dm/{bId}", new { }));
        Assert.Equal("declined", room.GetProperty("dm_request_state").GetString());
        Assert.False(room.GetProperty("capabilities").GetProperty("can_post").GetBoolean());
    }

    /// <summary>Only the recipient decides. An initiator accepting their own request would defeat the
    /// gate entirely, which is the one thing this state exists to prevent.</summary>
    [Fact]
    public async Task The_sender_cannot_accept_their_own_request()
    {
        var (a, _) = await LoginAsync();
        var (_, bId) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        var res = await a.PostAsJsonAsync($"/v1/dm/{roomId}/requests/accept", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("not_recipient", (await Json(res)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Declining_a_request_stops_delivery()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        Assert.Equal(HttpStatusCode.NoContent,
            (await b.PostAsJsonAsync($"/v1/dm/{roomId}/requests/decline", new { })).StatusCode);

        var blocked = await SendAsync(a, roomId, "still there?");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Equal("dm_declined", (await Json(blocked)).GetProperty("error").GetString());
        Assert.NotEqual(Guid.Empty, bId);
    }

    [Fact]
    public async Task A_request_cannot_be_answered_twice()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        await b.PostAsJsonAsync($"/v1/dm/{roomId}/requests/accept", new { });
        var again = await b.PostAsJsonAsync($"/v1/dm/{roomId}/requests/decline", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_answered", (await Json(again)).GetProperty("error").GetString());
    }

    /// <summary>A room you are not in does not exist to you (D-018).</summary>
    [Fact]
    public async Task A_stranger_cannot_answer_someone_elses_request()
    {
        var (a, _) = await LoginAsync();
        var (_, bId) = await LoginAsync();
        var (outsider, _) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.PostAsJsonAsync($"/v1/dm/{roomId}/requests/accept", new { })).StatusCode);
    }

    // ── Blocks (D-263 enforced here) ───────────────────────────────────────────

    [Fact]
    public async Task A_blocked_user_cannot_open_a_dm_in_either_direction()
    {
        var (a, aId) = await LoginAsync();
        var (b, bId) = await LoginAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync($"/v1/me/blocks/{bId}", new { })).StatusCode);

        foreach (var (client, target) in new[] { (a, bId), (b, aId) })
        {
            var res = await client.PostAsJsonAsync($"/v1/dm/{target}", new { });
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("blocked", (await Json(res)).GetProperty("error").GetString());
        }
    }

    /// <summary>Blocking after the conversation exists stops delivery — checked live on every send, so
    /// it bites on the next message rather than the next login.</summary>
    [Fact]
    public async Task Blocking_an_existing_conversation_stops_delivery_and_hides_it()
    {
        var (a, aId) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        await AllyAsync(a, b, bId);
        var roomId = await OpenDmAsync(a, bId);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(a, roomId, "before block")).StatusCode);

        await b.PostAsJsonAsync($"/v1/me/blocks/{aId}", new { });

        var refused = await SendAsync(a, roomId, "after block");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("blocked", (await Json(refused)).GetProperty("error").GetString());

        // Gone from both lists rather than sitting there un-openable.
        Assert.Empty(await JsonArray(await a.GetAsync("/v1/me/dm")));
        Assert.Empty(await JsonArray(await b.GetAsync("/v1/me/dm")));
    }

    // ── Archive ────────────────────────────────────────────────────────────────

    /// <summary>Archiving is per-member and says nothing to the other party — merging it with the room's
    /// lifecycle status would let one person lock the conversation for both.</summary>
    [Fact]
    public async Task Archiving_is_per_user_and_reversible()
    {
        var (a, aId) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        await AllyAsync(a, b, bId);
        var roomId = await OpenDmAsync(a, bId);
        await SendAsync(a, roomId, "hello");

        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync($"/v1/dm/{roomId}/archive", new { })).StatusCode);

        Assert.Empty(await JsonArray(await a.GetAsync("/v1/me/dm")));
        Assert.Single(await JsonArray(await a.GetAsync("/v1/me/dm?archived=true")));
        // The other party is unaffected.
        Assert.Single(await JsonArray(await b.GetAsync("/v1/me/dm")));

        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/v1/dm/{roomId}/archive")).StatusCode);
        Assert.Single(await JsonArray(await a.GetAsync("/v1/me/dm")));

        // Room status itself never moved — archiving is a folder, not a lifecycle transition.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(ChatRoomStatus.Active, (await db.ChatRooms.AsNoTracking().FirstAsync(r => r.Id == roomId)).Status);
        Assert.NotEqual(Guid.Empty, aId);
    }

    [Fact]
    public async Task Writing_into_an_archived_conversation_brings_it_back()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        await AllyAsync(a, b, bId);
        var roomId = await OpenDmAsync(a, bId);

        await a.PostAsJsonAsync($"/v1/dm/{roomId}/archive", new { });
        Assert.Empty(await JsonArray(await a.GetAsync("/v1/me/dm")));

        await OpenDmAsync(a, bId);   // re-opening is an implicit un-archive
        Assert.Single(await JsonArray(await a.GetAsync("/v1/me/dm")));
    }

    // ── The DM room is a chat room ─────────────────────────────────────────────

    /// <summary>The point of D-264: a DM reuses the chat surface wholesale rather than duplicating it.
    /// If this passes, history, attachments, presence and read pointers all work too — they are the
    /// same endpoints.</summary>
    [Fact]
    public async Task A_dm_room_uses_the_existing_chat_endpoints_for_history()
    {
        var (a, aId) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        await AllyAsync(a, b, bId);
        var roomId = await OpenDmAsync(a, bId);

        await SendAsync(a, roomId, "first");
        await SendAsync(b, roomId, "second");

        var page = await Json(await b.GetAsync($"/v1/chat/rooms/{roomId}/messages"));
        var bodies = page.GetProperty("messages").EnumerateArray()
            .Select(m => m.GetProperty("body").GetString()).ToList();
        Assert.Contains("first", bodies);
        Assert.Contains("second", bodies);
        Assert.NotEqual(Guid.Empty, aId);
    }

    /// <summary>Event chat is unchanged by DMs sharing its table: an event room still has an event, and
    /// DM rooms never appear in the event-chat list.</summary>
    [Fact]
    public async Task Direct_rooms_never_appear_in_the_event_chat_list()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        await AllyAsync(a, b, bId);
        await OpenDmAsync(a, bId);

        var eventChats = await JsonArray(await a.GetAsync("/v1/me/chats"));
        Assert.Empty(eventChats);
    }

    // ── D-292: room-addressed navigation ───────────────────────────────────────

    /// <summary>A direct room loads by its OWN id. Before D-292 there was no such route —
    /// <c>GetRoomByIdAsync</c> was reachable only through <c>POST /v1/dm/{userId}</c> — so a client
    /// holding a roomId could not open the conversation, and web keyed its route on eventId instead.
    /// A DM has no event, so every DM row in the inbox was a dead link.</summary>
    [Fact]
    public async Task A_direct_room_loads_by_its_own_id()
    {
        var (a, aId) = await LoginAsync();
        var (_, bId) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        var res = await a.GetAsync($"/v1/chat/rooms/{roomId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var room = await Json(res);
        Assert.Equal(roomId, room.GetProperty("room_id").GetGuid());
        Assert.Equal("Direct", room.GetProperty("kind").GetString());
        // The request gate travels with the room, so the client can render Accept/Decline from it.
        Assert.Equal("pending", room.GetProperty("dm_request_state").GetString());
        Assert.Equal(aId, room.GetProperty("dm_initiated_by").GetGuid());
    }

    /// <summary>Both parties reach the same room through the same door — the point of keying
    /// navigation on ChatRoom identity rather than on an event.</summary>
    [Fact]
    public async Task Both_parties_load_the_same_direct_room_by_id()
    {
        var (a, _) = await LoginAsync();
        var (b, bId) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        foreach (var client in new[] { a, b })
        {
            var res = await client.GetAsync($"/v1/chat/rooms/{roomId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(roomId, (await Json(res)).GetProperty("room_id").GetGuid());
        }
    }

    /// <summary>A room you are not in does not load. Membership is enforced in the service, so the new
    /// route adds no authorization of its own and cannot drift from the one that already existed.
    ///
    /// <para><b>Behaviour recorded, not endorsed:</b> a non-member gets 403 and a non-existent room gets
    /// 404, so the pair is distinguishable — which is the opposite of the 404-not-403 rule D-018 applies
    /// to hidden resources. That is pre-existing <c>GetRoomByIdAsync</c> behaviour shared with
    /// <c>POST /v1/dm/{userId}</c>, not something this route introduced, and changing it is outside this
    /// change's scope. Room ids are unguessable v4/v7 GUIDs, so the practical leak is nil; the reason to
    /// pin it here is that a future fix should move BOTH callers, and this test will fail loudly when
    /// one moves alone.</para></summary>
    [Fact]
    public async Task A_non_member_cannot_load_a_room_by_id()
    {
        var (a, _) = await LoginAsync();
        var (_, bId) = await LoginAsync();
        var (outsider, _) = await LoginAsync();
        var roomId = await OpenDmAsync(a, bId);

        var res = await outsider.GetAsync($"/v1/chat/rooms/{roomId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var missing = await outsider.GetAsync($"/v1/chat/rooms/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>The factory records Hangfire enqueues instead of running them, so the notification job
    /// is invoked explicitly — the same approach ChatNotificationTests uses.</summary>
    private async Task RunNotificationJobsAsync()
    {
        var messageIds = _factory.BackgroundJobs.Jobs
            .Where(j => j.Method.Name == nameof(ChatNotificationJob.RunAsync) && j.Args.Count > 0 && j.Args[0] is Guid)
            .Select(j => (Guid)j.Args[0]!)
            .ToList();

        foreach (var messageId in messageIds)
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ChatNotificationJob>().RunAsync(messageId, CancellationToken.None);
        }
        _factory.BackgroundJobs.Clear();
    }

    private async Task<int> CountNotificationsAsync(Guid userId, string kind)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        return await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && n.Kind == kind);
    }
}
