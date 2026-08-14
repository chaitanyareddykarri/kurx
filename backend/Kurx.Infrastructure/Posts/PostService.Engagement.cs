using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Posts;

public partial class PostService
{
    // ── Likes & saves ──────────────────────────────────────────────────────────
    //
    // Every counter here is an ExecuteUpdateAsync whose new value Postgres computes (D-240). A popular
    // post is by definition one that many people are liking in the same instant; loading the row and
    // writing `LikeCount++` loses all but one of those, silently.

    public async Task<ServiceResult<PostLikeResult>> SetLikeAsync(Guid postId, Guid userId, bool liked, CancellationToken ct = default)
    {
        var (post, error) = await LoadVisibleAsync(postId, userId, ct);
        if (post is null) return ServiceResult<PostLikeResult>.Fail(error);

        if (liked)
        {
            db.PostLikes.Add(new PostLike { PostId = postId, UserId = userId });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Lost the race against the unique (PostId, UserId) index, or the caller double-tapped.
                // Either way the like already exists — the counter must NOT move again.
                db.ChangeTracker.Clear();
                return await LikeResultAsync(postId, userId, ct);
            }
            await db.Posts.Where(p => p.Id == postId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LikeCount, p => p.LikeCount + 1), ct);
        }
        else
        {
            var removed = await db.PostLikes.Where(l => l.PostId == postId && l.UserId == userId).ExecuteDeleteAsync(ct);
            // Only the request that actually removed a row decrements. The `> 0` guard is evaluated under
            // the row lock, so a counter can never be driven negative by concurrent unlikes.
            if (removed > 0)
                await db.Posts.Where(p => p.Id == postId && p.LikeCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.LikeCount, p => p.LikeCount - 1), ct);
        }

        return await LikeResultAsync(postId, userId, ct);
    }

    private async Task<ServiceResult<PostLikeResult>> LikeResultAsync(Guid postId, Guid userId, CancellationToken ct)
    {
        var count = await db.Posts.AsNoTracking().Where(p => p.Id == postId).Select(p => p.LikeCount).FirstAsync(ct);
        var mine = await db.PostLikes.AsNoTracking().AnyAsync(l => l.PostId == postId && l.UserId == userId, ct);
        return ServiceResult<PostLikeResult>.Success(new PostLikeResult(mine, count));
    }

    public async Task<ServiceResult<bool>> SetSaveAsync(Guid postId, Guid userId, bool saved, CancellationToken ct = default)
    {
        var (post, error) = await LoadVisibleAsync(postId, userId, ct);
        if (post is null) return ServiceResult<bool>.Fail(error);

        if (saved)
        {
            db.PostSaves.Add(new PostSave { PostId = postId, UserId = userId });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();   // already saved — idempotent by contract
            }
        }
        else
        {
            await db.PostSaves.Where(s => s.PostId == postId && s.UserId == userId).ExecuteDeleteAsync(ct);
        }
        return ServiceResult<bool>.Success(true);
    }

    // ── Poll voting ────────────────────────────────────────────────────────────

    public async Task<ServiceResult<PostPollView>> VoteAsync(Guid postId, Guid userId, IReadOnlyList<Guid>? optionIds, CancellationToken ct = default)
    {
        var (post, error) = await LoadVisibleAsync(postId, userId, ct);
        if (post is null) return ServiceResult<PostPollView>.Fail(error);

        var poll = await db.PostPolls.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == postId, ct);
        if (poll is null) return ServiceResult<PostPollView>.Fail("post_not_found");
        if (poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow)
            return ServiceResult<PostPollView>.Fail("poll_closed");

        var ids = (optionIds ?? []).Distinct().ToList();
        if (ids.Count == 0) return ServiceResult<PostPollView>.Fail("invalid_option");
        if (!poll.AllowMultiple && ids.Count > 1) return ServiceResult<PostPollView>.Fail("invalid_option");

        var known = await db.PostPollOptions.AsNoTracking()
            .Where(o => o.PollId == poll.Id && ids.Contains(o.Id)).CountAsync(ct);
        if (known != ids.Count) return ServiceResult<PostPollView>.Fail("invalid_option");

        if (await db.PostPollBallots.AsNoTracking().AnyAsync(b => b.PollId == poll.Id && b.UserId == userId, ct))
            return ServiceResult<PostPollView>.Fail("already_voted");

        // The ballot goes in first and carries the unique (PollId, UserId) index. Two concurrent votes
        // from one account both pass the check above; only one survives this insert, which is what stops
        // a multi-select poll being stuffed by firing disjoint selections in parallel.
        db.PostPollBallots.Add(new PostPollBallot { PollId = poll.Id, UserId = userId });
        foreach (var id in ids)
            db.PostPollVotes.Add(new PostPollVote { PollId = poll.Id, OptionId = id, UserId = userId });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ServiceResult<PostPollView>.Fail("already_voted");
        }

        await db.PostPollOptions.Where(o => ids.Contains(o.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.VoteCount, o => o.VoteCount + 1), ct);
        // TotalVotes counts votes CAST, so it is exactly the sum of the option counts and needs no
        // second, distinct-voter claim to stay consistent with them (D-262).
        await db.PostPolls.Where(p => p.Id == poll.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalVotes, p => p.TotalVotes + ids.Count), ct);

        return ServiceResult<PostPollView>.Success(await PollViewAsync(poll.Id, userId, ct));
    }

    private async Task<PostPollView> PollViewAsync(Guid pollId, Guid userId, CancellationToken ct)
    {
        var poll = await db.PostPolls.AsNoTracking().FirstAsync(p => p.Id == pollId, ct);
        var options = await db.PostPollOptions.AsNoTracking()
            .Where(o => o.PollId == pollId).OrderBy(o => o.Sort).ToListAsync(ct);
        var mine = (await db.PostPollVotes.AsNoTracking()
            .Where(v => v.PollId == pollId && v.UserId == userId)
            .Select(v => v.OptionId).ToListAsync(ct)).ToHashSet();
        return ToPollView(poll, options, mine);
    }

    // ── Comments ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<PostCommentPage>> GetCommentsAsync(Guid postId, Guid viewerId, string? cursor, int limit, CancellationToken ct = default)
    {
        var (post, error) = await LoadVisibleAsync(postId, viewerId, ct);
        if (post is null) return ServiceResult<PostCommentPage>.Fail(error);

        limit = Math.Clamp(limit, 1, 50);

        // The page is a page of ROOTS, and every root brings its replies with it.
        //
        // Paginating the comments flat looks simpler and is subtly wrong: newest-first puts a reply
        // BEFORE the parent it belongs to, so a reply to an older comment arrives on page 1 while its
        // parent is still on page 2. The client groups by ParentCommentId — a reply whose parent is not
        // in the list is neither a root nor a child, so it renders nowhere at all until the reader
        // happens to page far enough.
        //
        // The response stays flat and the contract is unchanged; only WHICH rows make up a page moved.
        // Blocks apply to comments too (D-263). Hiding someone's posts but leaving their replies under
        // yours would make the block feel broken exactly where it matters most.
        var live = db.PostComments.AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted
                && !db.UserBlocks.Any(b => (b.BlockerId == viewerId && b.BlockedId == c.AuthorId)
                                        || (b.BlockerId == c.AuthorId && b.BlockedId == viewerId)));

        var roots = live.Where(c => c.ParentCommentId == null);

        // Same (CreatedAt, Id) keyset as posts. Newest-first: a comment arriving while the reader is
        // paging lands on the page they already hold, so no later page shifts or repeats.
        if (cursor is not null && Guid.TryParse(cursor, out var cursorId))
        {
            var pivot = await db.PostComments.AsNoTracking().Where(c => c.Id == cursorId)
                .Select(c => new { c.CreatedAt, c.Id }).FirstOrDefaultAsync(ct);
            if (pivot is not null)
                roots = roots.Where(c => EF.Functions.GreaterThan(
                    ValueTuple.Create(pivot.CreatedAt, pivot.Id),
                    ValueTuple.Create(c.CreatedAt, c.Id)));
        }

        var rootRows = await roots.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Take(limit).ToListAsync(ct);

        var rootIds = rootRows.Select(c => c.Id).ToList();
        // Replies read oldest-first: a thread is a conversation, and conversations read forwards.
        // Bounded per page so one comment with thousands of replies cannot return an unbounded page;
        // beyond this the tail is not served, which is a better failure than a timeout.
        var replies = await live
            .Where(c => c.ParentCommentId != null && rootIds.Contains(c.ParentCommentId!.Value))
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Take(limit * 10).ToListAsync(ct);

        var items = await BuildCommentViewsAsync([.. rootRows, .. replies], post, viewerId, ct);
        // The cursor names the last ROOT, so continuing never re-serves a thread already delivered.
        return ServiceResult<PostCommentPage>.Success(
            new PostCommentPage(items, rootRows.Count == limit ? rootRows[^1].Id.ToString() : null));
    }

    private async Task<List<PostCommentView>> BuildCommentViewsAsync(List<PostComment> comments, Post post, Guid viewerId, CancellationToken ct)
    {
        if (comments.Count == 0) return [];

        var ids = comments.Select(c => c.Id).ToList();
        var authors = await LoadAuthorsAsync(comments.Select(c => c.AuthorId).Distinct().ToList(), ct);
        var likedByMe = (await db.PostCommentLikes.AsNoTracking()
            .Where(l => ids.Contains(l.CommentId) && l.UserId == viewerId)
            .Select(l => l.CommentId).ToListAsync(ct)).ToHashSet();

        return comments.Select(c => new PostCommentView(
            c.Id, c.PostId, authors[c.AuthorId], c.Body, c.ParentCommentId,
            c.LikeCount, likedByMe.Contains(c.Id),
            // The post's author moderates their own thread; a commenter may always remove their own.
            CanDelete: c.AuthorId == viewerId || post.AuthorId == viewerId,
            c.CreatedAt)).ToList();
    }

    public async Task<ServiceResult<PostCommentView>> AddCommentAsync(Guid postId, Guid userId, string? body, Guid? parentCommentId, CancellationToken ct = default)
    {
        var (post, error) = await LoadVisibleAsync(postId, userId, ct);
        if (post is null) return ServiceResult<PostCommentView>.Fail(error);

        body = (body ?? "").Trim();
        if (body.Length == 0) return ServiceResult<PostCommentView>.Fail("body_required");
        if (body.Length > MaxCommentChars) return ServiceResult<PostCommentView>.Fail("body_too_long");

        Guid? parentId = null;
        Guid? parentAuthorId = null;
        if (parentCommentId.HasValue)
        {
            var parent = await db.PostComments.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentCommentId.Value && c.PostId == postId && !c.IsDeleted, ct);
            if (parent is null) return ServiceResult<PostCommentView>.Fail("comment_not_found");
            // Exactly one level: a reply to a reply attaches to the same parent, the rule chat already
            // uses. Two levels is an affordance; unbounded depth is a bug report.
            parentId = parent.ParentCommentId ?? parent.Id;
            parentAuthorId = parent.AuthorId;
        }

        var comment = new PostComment { PostId = postId, AuthorId = userId, Body = body, ParentCommentId = parentId };
        db.PostComments.Add(comment);
        await db.SaveChangesAsync(ct);

        await db.Posts.Where(p => p.Id == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CommentCount, p => p.CommentCount + 1), ct);

        await NotifyCommentAsync(post, comment, parentAuthorId, ct);

        var views = await BuildCommentViewsAsync([comment], post, userId, ct);
        return ServiceResult<PostCommentView>.Success(views[0]);
    }

    /// <summary>A reply notifies the comment's author; a top-level comment notifies the post's author.
    /// Nobody is ever notified about their own action, and a reply on your own post produces one notice,
    /// not two.</summary>
    private async Task NotifyCommentAsync(Post post, PostComment comment, Guid? parentAuthorId, CancellationToken ct)
    {
        var actorName = await db.Users.AsNoTracking().Where(u => u.Id == comment.AuthorId)
            .Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Someone";

        var notified = new HashSet<Guid> { comment.AuthorId };

        if (parentAuthorId.HasValue && notified.Add(parentAuthorId.Value))
            await NotifySafeAsync(parentAuthorId.Value, "post_reply", actorName,
                $"{actorName} replied to your comment", post.Id, comment.Id, comment.AuthorId, ct);

        if (notified.Add(post.AuthorId))
            await NotifySafeAsync(post.AuthorId, "post_comment", actorName,
                $"{actorName} commented on your post", post.Id, comment.Id, comment.AuthorId, ct);
    }

    public async Task<ServiceResult<bool>> DeleteCommentAsync(Guid commentId, Guid actorId, bool isModerator, CancellationToken ct = default)
    {
        var comment = await db.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, ct);
        if (comment is null) return ServiceResult<bool>.Fail("comment_not_found");

        var (post, error) = await LoadVisibleAsync(comment.PostId, actorId, ct);
        if (post is null && !isModerator) return ServiceResult<bool>.Fail(error);

        var postAuthorId = post?.AuthorId
            ?? await db.Posts.AsNoTracking().Where(p => p.Id == comment.PostId).Select(p => p.AuthorId).FirstAsync(ct);
        if (comment.AuthorId != actorId && postAuthorId != actorId && !isModerator)
            return ServiceResult<bool>.Fail("not_post_author");

        comment.IsDeleted = true;
        comment.DeletedBy = actorId;

        // Replies go with their parent. Threading is one level, so this recurses no further — and
        // leaving them would strand them: the listing filters deleted rows, so each orphan would come
        // back carrying a ParentCommentId naming a comment that is no longer in the page, and the
        // client has nothing to group it under.
        var replies = await db.PostComments
            .Where(c => c.ParentCommentId == commentId && !c.IsDeleted).ToListAsync(ct);
        foreach (var reply in replies)
        {
            reply.IsDeleted = true;
            reply.DeletedBy = actorId;
        }

        await db.SaveChangesAsync(ct);

        // One decrement per row actually removed, computed by the database and floored at zero.
        var removed = 1 + replies.Count;
        await db.Posts.Where(p => p.Id == comment.PostId && p.CommentCount >= removed)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CommentCount, p => p.CommentCount - removed), ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<PostLikeResult>> SetCommentLikeAsync(Guid commentId, Guid userId, bool liked, CancellationToken ct = default)
    {
        var comment = await db.PostComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, ct);
        if (comment is null) return ServiceResult<PostLikeResult>.Fail("comment_not_found");

        // The comment is only reachable if its post is — otherwise a comment id would be a way to probe
        // a post the caller cannot see.
        var (post, error) = await LoadVisibleAsync(comment.PostId, userId, ct);
        if (post is null) return ServiceResult<PostLikeResult>.Fail(error == "post_hidden" ? error : "comment_not_found");

        if (liked)
        {
            db.PostCommentLikes.Add(new PostCommentLike { CommentId = commentId, UserId = userId });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                return await CommentLikeResultAsync(commentId, userId, ct);
            }
            await db.PostComments.Where(c => c.Id == commentId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LikeCount, c => c.LikeCount + 1), ct);
        }
        else
        {
            var removed = await db.PostCommentLikes
                .Where(l => l.CommentId == commentId && l.UserId == userId).ExecuteDeleteAsync(ct);
            if (removed > 0)
                await db.PostComments.Where(c => c.Id == commentId && c.LikeCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.LikeCount, c => c.LikeCount - 1), ct);
        }

        return await CommentLikeResultAsync(commentId, userId, ct);
    }

    private async Task<ServiceResult<PostLikeResult>> CommentLikeResultAsync(Guid commentId, Guid userId, CancellationToken ct)
    {
        var count = await db.PostComments.AsNoTracking().Where(c => c.Id == commentId).Select(c => c.LikeCount).FirstAsync(ct);
        var mine = await db.PostCommentLikes.AsNoTracking().AnyAsync(l => l.CommentId == commentId && l.UserId == userId, ct);
        return ServiceResult<PostLikeResult>.Success(new PostLikeResult(mine, count));
    }

    // ── Moderation ─────────────────────────────────────────────────────────────

    public async Task<PostAdminPage> AdminListAsync(string? q, string? visibility, bool? reported, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Deliberately NOT filtered by VisibleTo: the moderation queue must be able to see a private post
        // that was reported, which is the entire reason the queue exists. Hidden posts stay listed so a
        // hide can be reviewed and undone.
        var query = db.Posts.AsNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Body, term));
        }
        if (!string.IsNullOrWhiteSpace(visibility))
        {
            var parsed = ParseVisibility(visibility);
            // An unparseable filter matches nothing rather than silently matching everything — a filter
            // that quietly does not apply is how a moderator ends up acting on the wrong row.
            if (parsed is null) return new PostAdminPage([], 0);
            query = query.Where(p => p.Visibility == parsed.Value);
        }
        if (reported == true)
            query = query.Where(p => db.Reports.Any(r => r.EntityType == "post" && r.EntityId == p.Id && r.Status == "open"));

        var total = await query.CountAsync(ct);
        var posts = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = await BuildViewsAsync(posts, viewerId: null, includeShared: true, ct);
        // The caller is a platform moderator, so the per-row capability is true regardless of authorship.
        return new PostAdminPage(items.Select(v => v with { CanDelete = true }).ToList(), total);
    }

    public async Task<ServiceResult<bool>> AdminSetHiddenAsync(Guid postId, Guid actorId, bool hidden, string? reason, CancellationToken ct = default)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);
        if (post is null) return ServiceResult<bool>.Fail("post_not_found");

        post.IsHidden = hidden;
        post.HiddenReason = hidden ? reason?.Trim() : null;

        audit.Write(new AuditEvent(hidden ? "post.hide" : "post.unhide", "posts", postId,
            ActorType: "admin", ActorId: actorId,
            After: new { hidden, reason = post.HiddenReason }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── Background ─────────────────────────────────────────────────────────────

    /// <summary>Removes storage objects for deleted posts' media and for uploads no post ever claimed.
    /// Orphans are unavoidable: confirm happens before create, so a closed composer, a failed create or a
    /// killed app all leave a stored object with no post. Without this sweep they accumulate forever.
    ///
    /// <para>Idempotent — rows are removed only after their object is gone, so a re-run finds nothing.</para></summary>
    public async Task CleanupMediaAsync(CancellationToken ct = default)
    {
        // Orphans get a grace window so an upload still in flight toward its post is never swept.
        var orphanCutoff = DateTime.UtcNow.AddHours(-24);

        var doomed = await db.PostMedia
            .Where(m => m.DeletedAt != null || (m.PostId == null && m.CreatedAt < orphanCutoff))
            .Take(500)   // bounded per run so a large backlog cannot monopolise a worker
            .ToListAsync(ct);
        if (doomed.Count == 0) return;

        foreach (var m in doomed) await TryDeleteObjectAsync(m.StorageKey, ct);

        db.PostMedia.RemoveRange(doomed);
        await db.SaveChangesAsync(ct);
    }
}
