namespace Kurx.Application.Abstractions;

// Every response record below MUST stay in this namespace: SnakeCaseResponseConverter keys off the
// namespace and nothing else (D-259 addendum), so a view declared elsewhere silently serializes
// camelCase and breaks every client. Request shapes stay camelCase — Read is delegated untouched.

public record PostAuthorView(Guid Id, string Name, string? Username, string? AvatarKey, bool IsVerified,
    /// <summary>Presigned companion to <c>AvatarKey</c> (D-302). Null when there is no key.</summary>
    string? AvatarUrl = null);

/// <summary><paramref name="Url"/> is a short-lived signed download URL minted per read, never a stored
/// public link — the same rule chat attachments follow, so revoking access is a matter of not minting
/// another one.</summary>
public record PostMediaView(Guid Id, string Kind, string Url, string ContentType,
    long SizeBytes, int? Width, int? Height, int? DurationSeconds, int Sort);

public record PostPollOptionView(Guid Id, string Text, int Sort, int VoteCount, bool VotedByMe);

/// <summary><paramref name="TotalVotes"/> is votes CAST, not voters — the sum of every option's count
/// (D-262). Identical to the voter count for a single-choice poll.</summary>
public record PostPollView(Guid Id, string Question, bool AllowMultiple, DateTime? ClosesAt,
    bool IsClosed, int TotalVotes, IReadOnlyList<PostPollOptionView> Options);

public record PostEventRefView(Guid Id, string Title, string Slug, string? BannerKey,
    DateTime StartsAt, string? City,
    /// <summary>Presigned. The key beside it is not fetchable, so the event card attached to a post
    /// rendered a broken image for every post that named an event (D-302).</summary>
    string? BannerUrl = null);

public record PostView(
    Guid Id,
    PostAuthorView Author,
    string Kind,                       // text | images | video | document | poll | event | share
    string Body,
    string Visibility,                 // public | connections | event_participants | only_me
    IReadOnlyList<PostMediaView> Media,
    PostPollView? Poll,
    PostEventRefView? Event,           // set when the post is attached to / shares an event
    PostView? SharedPost,              // set when Kind = share; never nested more than one level
    IReadOnlyList<string> Hashtags,
    IReadOnlyList<PostAuthorView> Mentions,
    int LikeCount, int CommentCount, int ShareCount,
    bool LikedByMe, bool SavedByMe,
    bool CanEdit, bool CanDelete,
    DateTime CreatedAt, DateTime? EditedAt);

/// <summary>One page plus the cursor to continue. Cursors are opaque strings (the rule chat set in
/// D-104) — clients must not parse, order or construct them. Null means the end.</summary>
public record PostPage(IReadOnlyList<PostView> Items, string? NextCursor);

public record PostCommentView(
    Guid Id, Guid PostId, PostAuthorView Author, string Body,
    Guid? ParentCommentId, int LikeCount, bool LikedByMe,
    bool CanDelete, DateTime CreatedAt);

public record PostCommentPage(IReadOnlyList<PostCommentView> Items, string? NextCursor);

public record PostLikeResult(bool Liked, int LikeCount);

/// <summary>What the client needs to upload one file: where to PUT the bytes, and the ids to quote back
/// on confirm.</summary>
public record PostMediaPresignView(Guid MediaId, string UploadUrl, string StorageKey);

public record TrendingHashtagView(string Tag, int PostCount);

/// <summary>Offset-paginated rather than keyset: the moderation queue is filtered and jumped around in,
/// which a cursor cannot serve, and its result sets are small by construction.</summary>
public record PostAdminPage(IReadOnlyList<PostView> Items, int Total);

/// <summary>One poll option as submitted at creation.</summary>
public record PostPollOptionInput(string Text);

/// <summary>The poll payload on a create request. Two options minimum — a one-option poll is a statement.</summary>
public record PostPollInput(string Question, IReadOnlyList<PostPollOptionInput> Options,
    bool AllowMultiple, DateTime? ClosesAt);

/// <summary>
/// The Posts module (D-262) — a social feed alongside Events.
///
/// <para><b>Visibility is enforced here, on every read path, and nowhere else.</b> Feed, single-post,
/// profile, hashtag and event listings all funnel through the same predicate, because a second
/// implementation is a second chance to get it wrong. A post the caller may not see returns
/// <c>post_not_found</c> — 404, never 403 (D-018).</para>
///
/// <para>Authorization is passed in, never a <c>ClaimsPrincipal</c>: <paramref name="viewerId"/> is null
/// for the public profile route and a real user everywhere else.</para>
/// </summary>
public interface IPostService
{
    // ── Reads ──────────────────────────────────────────────────────────────────

    /// <summary>The home feed: the caller's own posts, posts by accepted allies, posts attached to events
    /// of orgs the caller follows, and posts attached to events the caller holds a ticket for. Public posts
    /// from strangers are deliberately NOT here — they surface via profile, hashtag and event pages.</summary>
    Task<ServiceResult<PostPage>> GetFeedAsync(Guid viewerId, string? cursor, int limit, CancellationToken ct = default);

    Task<ServiceResult<PostView>> GetAsync(Guid postId, Guid? viewerId, CancellationToken ct = default);

    Task<ServiceResult<PostPage>> GetMyPostsAsync(Guid viewerId, string? cursor, int limit, CancellationToken ct = default);
    Task<ServiceResult<PostPage>> GetSavedAsync(Guid viewerId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>A user's public profile feed. <paramref name="viewerId"/> may be null (anonymous), in which
    /// case only Public posts are returned.</summary>
    Task<ServiceResult<PostPage>> GetByUsernameAsync(string username, Guid? viewerId, string? cursor, int limit, CancellationToken ct = default);

    Task<ServiceResult<PostPage>> GetByEventAsync(Guid eventId, Guid viewerId, string? cursor, int limit, CancellationToken ct = default);
    Task<ServiceResult<PostPage>> GetByHashtagAsync(string tag, Guid viewerId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>Full-text search over post bodies, scoped to what the caller may already see. It composes
    /// the SAME visibility predicate every other read path uses, so search can never become the one route
    /// that surfaces a post the feed would have hidden — the failure mode that makes search worth writing
    /// down at all. Accepts what a person types (phrases, OR, leading minus) via
    /// <c>websearch_to_tsquery</c>, matching chat message search (D-295).</summary>
    Task<ServiceResult<PostPage>> SearchAsync(string q, Guid viewerId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>Trending tags over the recent window, counted only over posts the caller can actually
    /// see — otherwise the trending list itself leaks the existence of private content.</summary>
    Task<IReadOnlyList<TrendingHashtagView>> GetTrendingHashtagsAsync(Guid viewerId, int limit, CancellationToken ct = default);

    // ── Writes ─────────────────────────────────────────────────────────────────

    Task<ServiceResult<PostView>> CreateAsync(Guid authorId, string? body, string? kind, string? visibility,
        IReadOnlyList<Guid>? mediaIds, Guid? eventId, Guid? sharedPostId, PostPollInput? poll, CancellationToken ct = default);

    /// <summary>Body and visibility only, author only. Media, poll, event attachment and kind are fixed at
    /// creation — see <see cref="Kurx.Domain.Entities.Post"/>.</summary>
    Task<ServiceResult<PostView>> UpdateAsync(Guid postId, Guid actorId, string? body, string? visibility, CancellationToken ct = default);

    /// <param name="isModerator">Platform moderator, resolved live at the endpoint. A moderator may delete
    /// any post; everyone else may delete only their own.</param>
    Task<ServiceResult<bool>> DeleteAsync(Guid postId, Guid actorId, bool isModerator, CancellationToken ct = default);

    // ── Media (two-step, mirroring chat attachments D-110) ─────────────────────

    Task<ServiceResult<PostMediaPresignView>> PresignMediaAsync(Guid userId, string fileName, string contentType,
        long sizeBytes, CancellationToken ct = default);

    /// <summary>The upload finished. This is where every authoritative check runs — size, MIME, extension,
    /// magic bytes, dimensions and the malware scan — because it is the first moment the server can look at
    /// the actual object. Idempotent per media id.</summary>
    Task<ServiceResult<PostMediaView>> ConfirmMediaAsync(Guid userId, Guid mediaId, string storageKey, CancellationToken ct = default);

    // ── Engagement ─────────────────────────────────────────────────────────────

    /// <summary>Idempotent: liking twice leaves one like and reports the true count.</summary>
    Task<ServiceResult<PostLikeResult>> SetLikeAsync(Guid postId, Guid userId, bool liked, CancellationToken ct = default);
    Task<ServiceResult<bool>> SetSaveAsync(Guid postId, Guid userId, bool saved, CancellationToken ct = default);

    /// <summary>One ballot per voter, cast once. Rejected after <c>ClosesAt</c>.</summary>
    Task<ServiceResult<PostPollView>> VoteAsync(Guid postId, Guid userId, IReadOnlyList<Guid>? optionIds, CancellationToken ct = default);

    Task<ServiceResult<PostCommentPage>> GetCommentsAsync(Guid postId, Guid viewerId, string? cursor, int limit, CancellationToken ct = default);
    Task<ServiceResult<PostCommentView>> AddCommentAsync(Guid postId, Guid userId, string? body, Guid? parentCommentId, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeleteCommentAsync(Guid commentId, Guid actorId, bool isModerator, CancellationToken ct = default);
    Task<ServiceResult<PostLikeResult>> SetCommentLikeAsync(Guid commentId, Guid userId, bool liked, CancellationToken ct = default);

    // ── Moderation ─────────────────────────────────────────────────────────────

    Task<PostAdminPage> AdminListAsync(string? q, string? visibility, bool? reported, int page, int pageSize, CancellationToken ct = default);
    Task<ServiceResult<bool>> AdminSetHiddenAsync(Guid postId, Guid actorId, bool hidden, string? reason, CancellationToken ct = default);

    // ── Background ─────────────────────────────────────────────────────────────

    /// <summary>Removes storage objects for deleted posts' media and for uploads no post ever claimed.
    /// Both are the direct consequence of confirm happening before create.</summary>
    Task CleanupMediaAsync(CancellationToken ct = default);
}
