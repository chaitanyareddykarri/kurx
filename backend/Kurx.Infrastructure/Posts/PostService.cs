using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Posts;

/// <summary>
/// The Posts module (D-262).
///
/// <para><b>One visibility predicate, applied to every read.</b> <see cref="VisibleTo"/> is the only place
/// that decides whether a caller may see a post, and every listing and single-post read composes it.
/// A second implementation would be a second chance to disagree with the first, and the way that failure
/// shows up is a private post appearing in someone's feed.</para>
///
/// <para><b>Counters are mutated in SQL, never read-modify-write (D-240).</b> <c>LikeCount</c>,
/// <c>CommentCount</c>, <c>ShareCount</c>, <c>VoteCount</c> and <c>TotalVotes</c> are all contested by
/// definition — a popular post is one many people are liking at the same instant — so every one of them is
/// an <c>ExecuteUpdateAsync</c> whose new value Postgres computes under the row lock.</para>
/// </summary>
public partial class PostService(
    KurxDbContext db,
    IStorage storage,
    IFileScanner scanner,
    INotificationService notifications,
    IAuditWriter audit,
    IEventAuthority authority) : IPostService
{
    public const int MaxBodyChars = 3000;
    public const int MaxCommentChars = 1000;
    private const int TrendingWindowDays = 7;

    // ── Wire vocabulary ────────────────────────────────────────────────────────
    // Enum.ToString() would emit "EventParticipants"; the contract says "event_participants". Explicit
    // maps rather than a generic snake-caser so the wire values are greppable from both directions.

    private static string KindWire(PostKind kind) => kind switch
    {
        PostKind.Images => "images",
        PostKind.Video => "video",
        PostKind.Document => "document",
        PostKind.Poll => "poll",
        PostKind.Event => "event",
        PostKind.Share => "share",
        _ => "text",
    };

    private static string VisibilityWire(PostVisibility visibility) => visibility switch
    {
        PostVisibility.Connections => "connections",
        PostVisibility.EventParticipants => "event_participants",
        PostVisibility.OnlyMe => "only_me",
        _ => "public",
    };

    private static PostVisibility? ParseVisibility(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "" or "public" => PostVisibility.Public,
        "connections" => PostVisibility.Connections,
        "event_participants" => PostVisibility.EventParticipants,
        "only_me" => PostVisibility.OnlyMe,
        _ => null,
    };

    private static string MediaKindWire(PostMediaKind kind) => kind switch
    {
        PostMediaKind.Video => "video",
        PostMediaKind.Document => "document",
        _ => "image",
    };

    // ── Visibility ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The single gate. Deleted and moderator-hidden posts are invisible to everyone including their
    /// author on list paths; the author learns about a hide only on the single-post read, which answers
    /// <c>post_hidden</c>.
    ///
    /// <para>Written as correlated subqueries rather than pre-loaded id lists on purpose: an ally list is
    /// unbounded, and materializing one into an <c>IN (…)</c> parameter list turns a popular account's
    /// feed query into a multi-thousand-parameter statement. Postgres joins these itself.</para>
    ///
    /// <para><paramref name="viewerId"/> null is the anonymous public-profile route: Public only.</para>
    /// </summary>
    private IQueryable<Post> VisibleTo(IQueryable<Post> q, Guid? viewerId)
    {
        q = q.Where(p => !p.IsDeleted && !p.IsHidden);
        if (viewerId is null) return q.Where(p => p.Visibility == PostVisibility.Public);

        var me = viewerId.Value;

        // Blocks (D-263) cut BOTH ways from a one-way row: if either party blocked the other, neither
        // sees the other's posts. A block that only hid one direction would let the blocked person keep
        // reading, which is not what anyone means by the word.
        q = q.Where(p => !db.UserBlocks.Any(b =>
            (b.BlockerId == me && b.BlockedId == p.AuthorId) || (b.BlockerId == p.AuthorId && b.BlockedId == me)));

        return q.Where(p =>
            p.AuthorId == me
            || p.Visibility == PostVisibility.Public
            || (p.Visibility == PostVisibility.Connections
                && db.AllyConnections.Any(a => a.Status == AllyStatus.Accepted
                    && ((a.UserLowId == me && a.UserHighId == p.AuthorId)
                        || (a.UserHighId == me && a.UserLowId == p.AuthorId))))
            // The query-side mirror of EventPermission.Participate (D-272). It has to be a predicate:
            // a page spans many events, so this cannot be an async resolve per row. The four arms are
            // EventAuthorityService.ResolveAsync's four arms in the same order, and AudienceRoles is
            // DERIVED from EventAuthority.LevelFor rather than restated — the one thing that could drift
            // here is the role list, so it is not written down twice.
            //
            // Live per request, never a token claim (D-015): a refunded ticket and a removed seat both
            // stop granting access the moment they change, not at the next login.
            || (p.Visibility == PostVisibility.EventParticipants && p.EventId != null
                && db.Events.Any(e => e.Id == p.EventId && e.DeletedAt == null
                    && (e.CreatedBy == me
                        || db.Memberships.Any(m => m.OrgId == e.RepresentingOrgId && m.UserId == me
                            && EventAuthority.AudienceRoles.Contains(m.Role))
                        || db.EventParticipants.Any(ep => ep.EventId == e.Id
                            && ep.SubjectType == ParticipantSubjectType.Person
                            && ep.SubjectId == me && ep.State == ParticipantState.Active)
                        || db.Tickets.Any(t => t.EventId == e.Id && t.UserId == me && t.State != TicketState.Void)))));
    }

    /// <summary>Loads one post for a caller who may act on it. Distinguishes "hidden by a moderator" from
    /// "does not exist" for the author ONLY — to everyone else the two are the same 404 (D-018), which is
    /// what stops the status code from confirming that a private post exists.</summary>
    private async Task<(Post? Post, string Error)> LoadVisibleAsync(Guid postId, Guid? viewerId, CancellationToken ct)
    {
        var post = await VisibleTo(db.Posts.AsNoTracking(), viewerId).FirstOrDefaultAsync(p => p.Id == postId, ct);
        // Error is meaningful only when Post is null; "" spares every call site a null check it could
        // satisfy only by ignoring.
        if (post is not null) return (post, "");

        if (viewerId is not null)
        {
            var hiddenMine = await db.Posts.AsNoTracking()
                .AnyAsync(p => p.Id == postId && p.AuthorId == viewerId.Value && p.IsHidden && !p.IsDeleted, ct);
            if (hiddenMine) return (null, "post_hidden");
        }
        return (null, "post_not_found");
    }

    // ── Cursor ─────────────────────────────────────────────────────────────────
    // The cursor is the boundary row's id, opaque to clients (the rule chat set in D-104). Ordering is
    // the (CreatedAt, Id) PAIR: DateTime.UtcNow has ~15ms resolution, so CreatedAt alone is not a total
    // order and same-tick rows get skipped or duplicated across pages.

    private async Task<(DateTime CreatedAt, Guid Id)?> ResolvePostCursorAsync(string? cursor, CancellationToken ct)
    {
        if (cursor is null || !Guid.TryParse(cursor, out var id)) return null;
        var row = await db.Posts.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.CreatedAt, p.Id }).FirstOrDefaultAsync(ct);
        return row is null ? null : (row.CreatedAt, row.Id);
    }

    private static IQueryable<Post> Before(IQueryable<Post> q, (DateTime CreatedAt, Guid Id)? pivot)
        => pivot is null ? q : q.Where(p => EF.Functions.GreaterThan(
            ValueTuple.Create(pivot.Value.CreatedAt, pivot.Value.Id),
            ValueTuple.Create(p.CreatedAt, p.Id)));

    /// <summary>Runs a post listing as a keyset page and maps it. <paramref name="limit"/> is clamped
    /// here rather than trusted, so an endpoint cannot leak an unbounded query.</summary>
    private async Task<PostPage> PageAsync(IQueryable<Post> q, Guid? viewerId, string? cursor, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 50);
        var pivot = await ResolvePostCursorAsync(cursor, ct);
        // An unresolvable cursor means the row it named is gone (deleted, hidden, or never existed).
        // Starting from the top is the right answer: a client holding a stale cursor should see a fresh
        // page, not an error it cannot act on.
        var posts = await Before(q, pivot)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(limit).ToListAsync(ct);

        var items = await BuildViewsAsync(posts, viewerId, includeShared: true, ct);
        // Null once a page comes back short: there is provably nothing after it, so the client stops
        // without a final empty round-trip.
        return new PostPage(items, posts.Count == limit ? posts[^1].Id.ToString() : null);
    }

    // ── Read endpoints ─────────────────────────────────────────────────────────

    public async Task<ServiceResult<PostPage>> GetFeedAsync(Guid viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        // D-262: an explicit graph, not "everything public". Public posts from strangers reach the caller
        // through profile and hashtag pages instead — a feed of everyone's posts is a firehose, and the
        // moment it exists the ranking problem it creates is the product.
        var q = VisibleTo(db.Posts.AsNoTracking(), viewerId).Where(p =>
            p.AuthorId == viewerId
            || db.AllyConnections.Any(a => a.Status == AllyStatus.Accepted
                && ((a.UserLowId == viewerId && a.UserHighId == p.AuthorId)
                    || (a.UserHighId == viewerId && a.UserLowId == p.AuthorId)))
            // Orgs do not author posts; a followed org reaches the feed through posts attached to its
            // events, which is the only authorship relation an org actually has here.
            || (p.EventId != null && db.Events.Any(e => e.Id == p.EventId
                && db.OrganizationFollowers.Any(f => f.UserId == viewerId && f.OrgId == e.RepresentingOrgId)))
            || (p.EventId != null && db.Tickets.Any(t => t.EventId == p.EventId
                && t.UserId == viewerId && t.State != TicketState.Void)));

        return ServiceResult<PostPage>.Success(await PageAsync(q, viewerId, cursor, limit, ct));
    }

    public async Task<ServiceResult<PostView>> GetAsync(Guid postId, Guid? viewerId, CancellationToken ct = default)
    {
        var (post, error) = await LoadVisibleAsync(postId, viewerId, ct);
        if (post is null) return ServiceResult<PostView>.Fail(error);

        var views = await BuildViewsAsync([post], viewerId, includeShared: true, ct);
        return ServiceResult<PostView>.Success(views[0]);
    }

    public async Task<ServiceResult<PostPage>> GetMyPostsAsync(Guid viewerId, string? cursor, int limit, CancellationToken ct = default)
        => ServiceResult<PostPage>.Success(await PageAsync(
            db.Posts.AsNoTracking().Where(p => p.AuthorId == viewerId && !p.IsDeleted && !p.IsHidden),
            viewerId, cursor, limit, ct));

    public async Task<ServiceResult<PostPage>> GetSavedAsync(Guid viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        // Saved posts order by when they were SAVED, not when they were written — so the cursor resolves
        // against the save row. Re-filtering through VisibleTo matters here: a post saved while it was
        // public must disappear from the list if its author later restricts it.
        var saves = db.PostSaves.AsNoTracking().Where(s => s.UserId == viewerId);
        (DateTime CreatedAt, Guid PostId)? pivot = null;
        if (cursor is not null && Guid.TryParse(cursor, out var cursorId))
        {
            var row = await saves.Where(s => s.PostId == cursorId)
                .Select(s => new { s.CreatedAt, s.PostId }).FirstOrDefaultAsync(ct);
            if (row is not null) pivot = (row.CreatedAt, row.PostId);
        }
        if (pivot is not null)
            saves = saves.Where(s => EF.Functions.GreaterThan(
                ValueTuple.Create(pivot.Value.CreatedAt, pivot.Value.PostId),
                ValueTuple.Create(s.CreatedAt, s.PostId)));

        var ordered = await saves
            .Join(VisibleTo(db.Posts.AsNoTracking(), viewerId), s => s.PostId, p => p.Id, (s, p) => new { s.CreatedAt, Post = p })
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Post.Id)
            .Take(limit).ToListAsync(ct);

        var posts = ordered.Select(x => x.Post).ToList();
        var items = await BuildViewsAsync(posts, viewerId, includeShared: true, ct);
        return ServiceResult<PostPage>.Success(
            new PostPage(items, posts.Count == limit ? posts[^1].Id.ToString() : null));
    }

    public async Task<ServiceResult<PostPage>> GetByUsernameAsync(string username, Guid? viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        var handle = (username ?? "").Trim().ToLowerInvariant();
        var authorId = await db.Users.AsNoTracking().Where(u => u.Username == handle)
            .Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        // An unknown handle answers exactly as a handle with no visible posts would — the endpoint is
        // public, so a distinct 404 here would be a free username-enumeration oracle.
        if (authorId is null) return ServiceResult<PostPage>.Success(new PostPage([], null));

        return ServiceResult<PostPage>.Success(await PageAsync(
            VisibleTo(db.Posts.AsNoTracking(), viewerId).Where(p => p.AuthorId == authorId.Value),
            viewerId, cursor, limit, ct));
    }

    public async Task<ServiceResult<PostPage>> GetByEventAsync(Guid eventId, Guid viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        // The event has to be one this caller could already have found (D-018). A bare existence check
        // would answer 200-with-an-empty-page for a draft or unlisted event and 404 for a nonexistent
        // one — and that difference IS the leak, even though the posts below are visibility-filtered
        // either way.
        //
        // Two independent doors, and they answer different questions (D-272): publicity is a property of
        // the event, standing is a property of the caller. A listed public event is reachable by anybody;
        // anything else needs audience standing, which EventAuthority alone decides.
        var access = await authority.ResolveAsync(viewerId, eventId, isAdmin: false, ct);
        if (!access.EventExists) return ServiceResult<PostPage>.Fail("event_not_found");

        if (!access.Can(EventPermission.Participate)
            && !await db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId
                && e.Status == EventStatus.Published && e.Product == EventProduct.Public
                && e.Visibility == EventVisibility.Listed && !e.IsHidden, ct))
            return ServiceResult<PostPage>.Fail("event_not_found");

        return ServiceResult<PostPage>.Success(await PageAsync(
            VisibleTo(db.Posts.AsNoTracking(), viewerId).Where(p => p.EventId == eventId),
            viewerId, cursor, limit, ct));
    }

    public async Task<ServiceResult<PostPage>> GetByHashtagAsync(string tag, Guid viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        var normalized = (tag ?? "").Trim().TrimStart('#').ToLowerInvariant();
        if (normalized.Length == 0) return ServiceResult<PostPage>.Success(new PostPage([], null));

        return ServiceResult<PostPage>.Success(await PageAsync(
            VisibleTo(db.Posts.AsNoTracking(), viewerId)
                .Where(p => db.PostHashtags.Any(h => h.PostId == p.Id && h.Tag == normalized)),
            viewerId, cursor, limit, ct));
    }

    public async Task<ServiceResult<PostPage>> SearchAsync(string q, Guid viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        var term = (q ?? "").Trim();
        // An empty term is an empty result, never "every post": the alternative is a full-table keyset
        // scan handed out to anyone who submits a blank box.
        if (term.Length == 0) return ServiceResult<PostPage>.Success(new PostPage([], null));

        // VisibleTo FIRST, exactly as every other read path composes it: search must not become the one
        // route that surfaces a post the feed hides.
        //
        // Full-text rather than ILIKE, matching ChatService.SearchMessagesAsync (D-295) — the same
        // problem was solved in this repository one day earlier and a second search idiom here would be
        // drift. websearch_to_tsquery takes what a person actually types (quoted phrases, OR, a leading
        // minus) and never throws on malformed input, which also means no LIKE wildcards to escape.
        // Its GIN index is on the same expression; without that this recomputes the vector per row.
        return ServiceResult<PostPage>.Success(await PageAsync(
            VisibleTo(db.Posts.AsNoTracking(), viewerId)
                .Where(p => EF.Functions.ToTsVector("english", p.Body)
                    .Matches(EF.Functions.WebSearchToTsQuery("english", term))),
            viewerId, cursor, limit, ct));
    }

    public async Task<IReadOnlyList<TrendingHashtagView>> GetTrendingHashtagsAsync(Guid viewerId, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var since = DateTime.UtcNow.AddDays(-TrendingWindowDays);

        // Counted over posts the CALLER can see. Counting over everything would make the tag list itself
        // a side channel: a tag that trends but shows no posts tells you private posts exist and roughly
        // how many.
        var visible = VisibleTo(db.Posts.AsNoTracking(), viewerId).Where(p => p.CreatedAt >= since);
        var rows = await db.PostHashtags.AsNoTracking()
            .Where(h => visible.Any(p => p.Id == h.PostId))
            .GroupBy(h => h.Tag)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Tag)
            .Take(limit).ToListAsync(ct);

        return rows.Select(r => new TrendingHashtagView(r.Tag, r.Count)).ToList();
    }

    // ── View assembly ──────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a page of posts in a fixed number of queries regardless of page size — every related set
    /// (authors, media, polls, events, tags, mentions, the caller's own likes and saves) is loaded once
    /// for the whole page rather than per post.
    ///
    /// <para><paramref name="includeShared"/> is false on the recursive pass, which is what bounds reshare
    /// nesting at exactly one level in the response no matter what the data looks like.</para>
    /// </summary>
    private async Task<List<PostView>> BuildViewsAsync(List<Post> posts, Guid? viewerId, bool includeShared, CancellationToken ct)
    {
        if (posts.Count == 0) return [];

        var postIds = posts.Select(p => p.Id).ToList();

        var media = await db.PostMedia.AsNoTracking()
            .Where(m => m.PostId != null && postIds.Contains(m.PostId!.Value) && m.DeletedAt == null && m.IsConfirmed)
            .OrderBy(m => m.Sort).ToListAsync(ct);

        var mediaViews = await ToMediaViewsAsync(media, ct);

        var polls = await db.PostPolls.AsNoTracking().Where(p => postIds.Contains(p.PostId)).ToListAsync(ct);
        var pollIds = polls.Select(p => p.Id).ToList();
        var options = await db.PostPollOptions.AsNoTracking()
            .Where(o => pollIds.Contains(o.PollId)).OrderBy(o => o.Sort).ToListAsync(ct);
        HashSet<Guid> myVotes = viewerId is null
            ? []
            : (await db.PostPollVotes.AsNoTracking()
                .Where(v => pollIds.Contains(v.PollId) && v.UserId == viewerId.Value)
                .Select(v => v.OptionId).ToListAsync(ct)).ToHashSet();

        var eventIds = posts.Where(p => p.EventId.HasValue).Select(p => p.EventId!.Value).Distinct().ToList();
        var eventRows = await db.Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new PostEventRefView(e.Id, e.Title, e.Slug, e.BannerKey, e.StartsAt, e.City))
            .ToListAsync(ct);
        // Presign, exactly as this service already does for post media (D-262) — the attached event card
        // was the one place here still handing a client a raw storage key (D-302).
        var events = new List<PostEventRefView>(eventRows.Count);
        foreach (var e in eventRows)
            events.Add(string.IsNullOrWhiteSpace(e.BannerKey) ? e
                : e with { BannerUrl = await storage.PresignGetAsync(e.BannerKey!, TimeSpan.FromMinutes(15), ct) });

        var hashtags = await db.PostHashtags.AsNoTracking()
            .Where(h => postIds.Contains(h.PostId)).ToListAsync(ct);
        var mentions = await db.PostMentions.AsNoTracking()
            .Where(m => postIds.Contains(m.PostId)).ToListAsync(ct);

        var likedByMe = viewerId is null
            ? new HashSet<Guid>()
            : (await db.PostLikes.AsNoTracking()
                .Where(l => postIds.Contains(l.PostId) && l.UserId == viewerId.Value)
                .Select(l => l.PostId).ToListAsync(ct)).ToHashSet();
        var savedByMe = viewerId is null
            ? new HashSet<Guid>()
            : (await db.PostSaves.AsNoTracking()
                .Where(s => postIds.Contains(s.PostId) && s.UserId == viewerId.Value)
                .Select(s => s.PostId).ToListAsync(ct)).ToHashSet();

        // The originals behind any reshares on this page, re-filtered through VisibleTo: a share does not
        // grant access to something the sharer could see and the reader cannot.
        var sharedViews = new Dictionary<Guid, PostView>();
        if (includeShared)
        {
            var sharedIds = posts.Where(p => p.SharedPostId.HasValue).Select(p => p.SharedPostId!.Value).Distinct().ToList();
            if (sharedIds.Count > 0)
            {
                var shared = await VisibleTo(db.Posts.AsNoTracking(), viewerId)
                    .Where(p => sharedIds.Contains(p.Id)).ToListAsync(ct);
                foreach (var view in await BuildViewsAsync(shared, viewerId, includeShared: false, ct))
                    sharedViews[view.Id] = view;
            }
        }

        var authorIds = posts.Select(p => p.AuthorId)
            .Concat(mentions.Select(m => m.MentionedUserId))
            .Distinct().ToList();
        var authors = await LoadAuthorsAsync(authorIds, ct);

        return posts.Select(p =>
        {
            var poll = polls.FirstOrDefault(x => x.PostId == p.Id);
            return new PostView(
                p.Id,
                authors[p.AuthorId],
                KindWire(p.Kind),
                p.Body,
                VisibilityWire(p.Visibility),
                mediaViews.TryGetValue(p.Id, out var files) ? files : [],
                poll is null ? null : ToPollView(poll, options, myVotes),
                p.EventId.HasValue ? events.FirstOrDefault(e => e.Id == p.EventId.Value) : null,
                p.SharedPostId.HasValue && sharedViews.TryGetValue(p.SharedPostId.Value, out var s) ? s : null,
                hashtags.Where(h => h.PostId == p.Id).Select(h => h.Tag).ToList(),
                mentions.Where(m => m.PostId == p.Id).Select(m => authors[m.MentionedUserId]).ToList(),
                p.LikeCount, p.CommentCount, p.ShareCount,
                likedByMe.Contains(p.Id), savedByMe.Contains(p.Id),
                CanEdit: viewerId == p.AuthorId,
                CanDelete: viewerId == p.AuthorId,
                p.CreatedAt, p.EditedAt);
        }).ToList();
    }

    /// <summary>Author cards for a set of user ids. <c>IsVerified</c> is identity verification
    /// (<c>UserIdentity.Status == Approved</c>) — the same proof the Trust Center calls
    /// <c>IdentityVerified</c>, never a self-declared profile field.</summary>
    private async Task<Dictionary<Guid, PostAuthorView>> LoadAuthorsAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return [];

        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Username, u.AvatarKey }).ToListAsync(ct);
        var verified = (await db.UserIdentities.AsNoTracking()
            .Where(i => userIds.Contains(i.UserId) && i.Status == IdentityStatus.Approved)
            .Select(i => i.UserId).ToListAsync(ct)).ToHashSet();

        var map = users.ToDictionary(u => u.Id,
            u => new PostAuthorView(u.Id, u.Name, u.Username, u.AvatarKey, verified.Contains(u.Id)));

        // A referenced user that no longer exists would otherwise throw on lookup mid-page. A deleted
        // account is not a reason to fail someone else's feed.
        foreach (var id in userIds)
            map.TryAdd(id, new PostAuthorView(id, "Unknown", null, null, false));
        return map;
    }

    /// <summary>Media views grouped by post, minting a fresh signed download URL per file. URLs are
    /// short-lived and never stored: access is revoked by not issuing another one, so a link that leaks
    /// expires on its own.</summary>
    private async Task<Dictionary<Guid, List<PostMediaView>>> ToMediaViewsAsync(List<PostMedia> media, CancellationToken ct)
    {
        var result = new Dictionary<Guid, List<PostMediaView>>();
        foreach (var m in media)
        {
            var url = await storage.PresignGetAsync(m.StorageKey, TimeSpan.FromMinutes(15), ct);
            var view = new PostMediaView(m.Id, MediaKindWire(m.Kind), url, m.ContentType,
                m.SizeBytes, m.Width, m.Height, m.DurationSeconds, m.Sort);
            if (!result.TryGetValue(m.PostId!.Value, out var list)) result[m.PostId!.Value] = list = [];
            list.Add(view);
        }
        return result;
    }

    private async Task<PostMediaView> ToMediaViewAsync(PostMedia m, CancellationToken ct) => new(
        m.Id, MediaKindWire(m.Kind),
        await storage.PresignGetAsync(m.StorageKey, TimeSpan.FromMinutes(15), ct),
        m.ContentType, m.SizeBytes, m.Width, m.Height, m.DurationSeconds, m.Sort);

    private static PostPollView ToPollView(PostPoll poll, List<PostPollOption> options, HashSet<Guid> myVotes)
        => new(poll.Id, poll.Question, poll.AllowMultiple, poll.ClosesAt,
            poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow,
            poll.TotalVotes,
            options.Where(o => o.PollId == poll.Id)
                .Select(o => new PostPollOptionView(o.Id, o.Text, o.Sort, o.VoteCount, myVotes.Contains(o.Id)))
                .ToList());
}
