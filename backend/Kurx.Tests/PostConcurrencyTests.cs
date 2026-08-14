using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// Counter correctness for Posts under real parallel requests (D-262, the D-240/D-261 class).
///
/// <para>These are the tests a sequential suite cannot write. Every one of these counters passes a
/// one-request-at-a-time test against a read-modify-write implementation, because each request sees the
/// previous one's committed write. Fire them together and the lost update appears: all racers read the
/// same value and all write value+1, so the counter advances roughly once per BATCH instead of once per
/// action. Postgres READ COMMITTED does not save you — the second writer blocks on the row lock and then
/// overwrites with a value computed before the first one committed.</para>
///
/// <para>It fails silently. Nothing errors, nothing deadlocks; the number is simply wrong, and on a
/// social feed a wrong like count is indistinguishable from an unpopular post.</para>
/// </summary>
public class PostConcurrencyTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static int _phoneSeq;

    public PostConcurrencyTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { factory.ResetDatabase(); _reset = true; }
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private string NextPhone() => $"9184{Interlocked.Increment(ref _phoneSeq):D6}";

    private async Task<HttpClient> LoginAsync()
    {
        var phone = NextPhone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return client;
    }

    private static async Task<Guid> CreatePostAsync(HttpClient client, string body, object? poll = null, Guid? sharedPostId = null)
    {
        var res = await client.PostAsJsonAsync("/v1/posts", new { body, visibility = "public", poll, sharedPostId });
        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"create failed: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    // ── likes ──────────────────────────────────────────────────────────────────

    /// <summary>Twenty different people liking the same post at the same instant. The stored counter must
    /// equal twenty — this is the exact shape that cost the wallet five of six concurrent payments before
    /// D-240.</summary>
    [Fact]
    public async Task Twenty_simultaneous_likes_from_different_users_all_count()
    {
        const int likers = 20;
        var author = await LoginAsync();
        var postId = await CreatePostAsync(author, "about to be liked");

        var clients = new List<HttpClient>();
        for (var i = 0; i < likers; i++) clients.Add(await LoginAsync());

        var responses = await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync($"/v1/posts/{postId}/like", new { })));
        foreach (var r in responses)
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"like returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");

        await AssertLikeCountAsync(postId, likers);
        // And the count the API serves agrees with the row — a counter that is right in the database and
        // wrong on the wire is still wrong.
        Assert.Equal(likers, (await Json(await author.GetAsync($"/v1/posts/{postId}"))).GetProperty("like_count").GetInt32());
    }

    /// <summary>The same person double-tapping. The unique index — not an application-level check — is
    /// what makes this exactly one like, because both requests read "no row" before either inserts.</summary>
    [Fact]
    public async Task Ten_simultaneous_likes_from_one_user_count_once()
    {
        var author = await LoginAsync();
        var liker = await LoginAsync();
        var postId = await CreatePostAsync(author, "double tapped");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => liker.PostAsJsonAsync($"/v1/posts/{postId}/like", new { })));
        foreach (var r in responses) Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        await AssertLikeCountAsync(postId, 1);
    }

    /// <summary>Likes and unlikes interleaved. The end state is whatever the like rows say, and the
    /// counter must agree with them — a decrement that fires without having removed a row is how a
    /// counter goes negative.</summary>
    [Fact]
    public async Task Interleaved_likes_and_unlikes_leave_the_counter_matching_the_rows()
    {
        var author = await LoginAsync();
        var postId = await CreatePostAsync(author, "churn");

        var clients = new List<HttpClient>();
        for (var i = 0; i < 12; i++) clients.Add(await LoginAsync());

        // Everyone likes, then half of them unlike while the other half like again.
        await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync($"/v1/posts/{postId}/like", new { })));
        await Task.WhenAll(clients.Select((c, i) => i % 2 == 0
            ? c.DeleteAsync($"/v1/posts/{postId}/like")
            : c.PostAsJsonAsync($"/v1/posts/{postId}/like", new { })));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var rows = await db.PostLikes.AsNoTracking().CountAsync(l => l.PostId == postId);
        var counter = await db.Posts.AsNoTracking().Where(p => p.Id == postId).Select(p => p.LikeCount).FirstAsync();

        Assert.Equal(6, rows);
        Assert.Equal(rows, counter);
    }

    private async Task AssertLikeCountAsync(Guid postId, int expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var counter = await db.Posts.AsNoTracking().Where(p => p.Id == postId).Select(p => p.LikeCount).FirstAsync();
        var rows = await db.PostLikes.AsNoTracking().CountAsync(l => l.PostId == postId);
        Assert.Equal(expected, rows);
        Assert.True(counter == expected, $"LikeCount is {counter}, expected {expected} — a lost update (D-240).");
    }

    // ── poll votes ─────────────────────────────────────────────────────────────

    private static object PollBody(bool allowMultiple) => new
    {
        question = "Which?",
        options = new[] { "A", "B", "C" },
        allowMultiple,
        closesAt = (DateTime?)null,
    };

    private static async Task<List<Guid>> OptionIdsAsync(HttpClient client, Guid postId)
    {
        var post = await Json(await client.GetAsync($"/v1/posts/{postId}"));
        return post.GetProperty("poll").GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task Twenty_simultaneous_votes_on_one_option_all_count()
    {
        const int voters = 20;
        var author = await LoginAsync();
        var postId = await CreatePostAsync(author, "vote now", poll: PollBody(allowMultiple: false));
        var optionIds = await OptionIdsAsync(author, postId);

        var clients = new List<HttpClient>();
        for (var i = 0; i < voters; i++) clients.Add(await LoginAsync());

        var responses = await Task.WhenAll(clients.Select(c =>
            c.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote", new { optionIds = new[] { optionIds[0] } })));
        foreach (var r in responses)
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"vote returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var option = await db.PostPollOptions.AsNoTracking().FirstAsync(o => o.Id == optionIds[0]);
        var poll = await db.PostPolls.AsNoTracking().FirstAsync(p => p.PostId == postId);

        Assert.True(option.VoteCount == voters, $"VoteCount is {option.VoteCount}, expected {voters} — a lost update (D-240).");
        Assert.True(poll.TotalVotes == voters, $"TotalVotes is {poll.TotalVotes}, expected {voters} — a lost update (D-240).");
        // The invariant that makes TotalVotes meaningful: it is the sum of the option counts, always.
        var sum = await db.PostPollOptions.AsNoTracking().Where(o => o.PollId == poll.Id).SumAsync(o => o.VoteCount);
        Assert.Equal(sum, poll.TotalVotes);
    }

    /// <summary>The ballot-stuffing race, and the reason <c>PostPollBallot</c> exists. Two requests from
    /// ONE account naming DIFFERENT options both pass the "have you voted?" check; the unique index on the
    /// vote rows only forbids the same option twice, so without a per-voter claim both would land and one
    /// person would have cast two ballots.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Simultaneous_votes_from_one_user_on_different_options_yield_exactly_one_ballot(bool allowMultiple)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var author = await LoginAsync();
            var voter = await LoginAsync();
            var postId = await CreatePostAsync(author, "contested", poll: PollBody(allowMultiple));
            var optionIds = await OptionIdsAsync(author, postId);

            var responses = await Task.WhenAll(
                voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote", new { optionIds = new[] { optionIds[0] } }),
                voter.PostAsJsonAsync($"/v1/posts/{postId}/poll/vote", new { optionIds = new[] { optionIds[1] } }));

            foreach (var r in responses)
                Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                    $"attempt {attempt}: vote returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var poll = await db.PostPolls.AsNoTracking().FirstAsync(p => p.PostId == postId);

            Assert.Equal(1, await db.PostPollBallots.AsNoTracking().CountAsync(b => b.PollId == poll.Id));
            Assert.Equal(1, await db.PostPollVotes.AsNoTracking().CountAsync(v => v.PollId == poll.Id));
            Assert.Equal(1, poll.TotalVotes);
            Assert.Equal(1, await db.PostPollOptions.AsNoTracking()
                .Where(o => o.PollId == poll.Id).SumAsync(o => o.VoteCount));
        }
    }

    // ── comments & shares ──────────────────────────────────────────────────────

    [Fact]
    public async Task Fifteen_simultaneous_comments_all_count()
    {
        const int commenters = 15;
        var author = await LoginAsync();
        var postId = await CreatePostAsync(author, "busy thread");

        var clients = new List<HttpClient>();
        for (var i = 0; i < commenters; i++) clients.Add(await LoginAsync());

        var responses = await Task.WhenAll(clients.Select((c, i) =>
            c.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = $"comment {i}" })));
        foreach (var r in responses) Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var counter = await db.Posts.AsNoTracking().Where(p => p.Id == postId).Select(p => p.CommentCount).FirstAsync();
        Assert.Equal(commenters, await db.PostComments.AsNoTracking().CountAsync(c => c.PostId == postId));
        Assert.True(counter == commenters, $"CommentCount is {counter}, expected {commenters} — a lost update (D-240).");
    }

    [Fact]
    public async Task Fifteen_simultaneous_reshares_all_count_against_the_original()
    {
        const int sharers = 15;
        var author = await LoginAsync();
        var originalId = await CreatePostAsync(author, "the original");

        var clients = new List<HttpClient>();
        for (var i = 0; i < sharers; i++) clients.Add(await LoginAsync());

        var responses = await Task.WhenAll(clients.Select(c =>
            c.PostAsJsonAsync("/v1/posts", new { body = "sharing", visibility = "public", sharedPostId = originalId })));
        foreach (var r in responses)
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"share returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var counter = await db.Posts.AsNoTracking().Where(p => p.Id == originalId).Select(p => p.ShareCount).FirstAsync();
        Assert.True(counter == sharers, $"ShareCount is {counter}, expected {sharers} — a lost update (D-240).");
    }

    /// <summary>Reshares created and deleted at the same time. Both directions of ShareCount move in SQL,
    /// so the end state must equal the number of live shares — and must never go negative, which is what
    /// the `> 0` guard in the decrement's WHERE is for.</summary>
    [Fact]
    public async Task Simultaneous_reshares_and_un_reshares_leave_the_counter_matching_the_live_shares()
    {
        const int sharers = 12;
        var author = await LoginAsync();
        var originalId = await CreatePostAsync(author, "churned");

        var clients = new List<HttpClient>();
        for (var i = 0; i < sharers; i++) clients.Add(await LoginAsync());

        var shareIds = new List<Guid>();
        foreach (var c in clients) shareIds.Add(await CreatePostAsync(c, "share", sharedPostId: originalId));

        // Half delete their share while the other half reshare a second time, all at once.
        await Task.WhenAll(clients.Select((c, i) => i % 2 == 0
            ? c.DeleteAsync($"/v1/posts/{shareIds[i]}")
            : c.PostAsJsonAsync("/v1/posts", new { body = "again", visibility = "public", sharedPostId = originalId })));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var live = await db.Posts.AsNoTracking()
            .CountAsync(p => p.SharedPostId == originalId && !p.IsDeleted);
        var counter = await db.Posts.AsNoTracking().Where(p => p.Id == originalId).Select(p => p.ShareCount).FirstAsync();

        Assert.Equal(sharers + sharers / 2 - sharers / 2, live);   // 12 created, 6 deleted, 6 re-added
        Assert.True(counter == live, $"ShareCount is {counter}, expected {live} — a lost update (D-240).");
        Assert.True(counter >= 0, "ShareCount went negative — the decrement's `> 0` guard is not holding.");
    }

    /// <summary>A root and its replies are deleted as one action, so CommentCount must drop by all of
    /// them at once. Concurrent deletions of different roots must not lose any of those decrements, and
    /// must not drive the counter below zero.</summary>
    [Fact]
    public async Task Simultaneous_thread_deletions_leave_the_comment_count_matching_the_live_rows()
    {
        var author = await LoginAsync();
        var commenter = await LoginAsync();
        var postId = await CreatePostAsync(author, "many threads");

        var roots = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var root = await Json(await commenter.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = $"root {i}" }));
            var rootId = root.GetProperty("id").GetGuid();
            roots.Add(rootId);
            for (var r = 0; r < 2; r++)
                await commenter.PostAsJsonAsync($"/v1/posts/{postId}/comments", new { body = $"reply {i}.{r}", parentCommentId = rootId });
        }
        // 6 roots + 12 replies
        Assert.Equal(18, (await Json(await author.GetAsync($"/v1/posts/{postId}"))).GetProperty("comment_count").GetInt32());

        // Delete half the threads at once — 3 roots plus their 6 replies, nine rows across three requests.
        var responses = await Task.WhenAll(roots.Take(3).Select(id => commenter.DeleteAsync($"/v1/comments/{id}")));
        foreach (var r in responses) Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var live = await db.PostComments.AsNoTracking().CountAsync(c => c.PostId == postId && !c.IsDeleted);
        var counter = await db.Posts.AsNoTracking().Where(p => p.Id == postId).Select(p => p.CommentCount).FirstAsync();

        Assert.Equal(9, live);
        Assert.True(counter == live, $"CommentCount is {counter}, expected {live} — a lost update (D-240).");
    }

    [Fact]
    public async Task Simultaneous_saves_from_one_user_leave_exactly_one_saved_row()
    {
        var author = await LoginAsync();
        var reader = await LoginAsync();
        var postId = await CreatePostAsync(author, "save race");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => reader.PostAsJsonAsync($"/v1/posts/{postId}/save", new { })));
        foreach (var r in responses) Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.Equal(1, await db.PostSaves.AsNoTracking().CountAsync(s => s.PostId == postId));
    }
}
