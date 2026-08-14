using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A social-feed post (D-262). Distinct from <see cref="ChatMessage"/> in every dimension that
/// matters: it is authored to an audience rather than to a room, it carries structured payloads (media,
/// poll, event, reshare), and its reach is decided by <see cref="Visibility"/> rather than by membership.
///
/// <para><see cref="Kind"/>, <see cref="EventId"/>, <see cref="SharedPostId"/> and the media/poll payloads
/// are immutable after creation — an edit may change <see cref="Body"/> and <see cref="Visibility"/> only.
/// Letting a text post become a poll after people had already engaged with it would make every like and
/// comment a response to something that no longer exists.</para></summary>
public class Post
{
    /// <summary>UUIDv7: k-sortable, so it doubles as the keyset pagination cursor exactly as
    /// <see cref="ChatMessage.Id"/> does. Ordering is still the (CreatedAt, Id) pair — the id alone is
    /// only monotonic per generator, and the pair is what the index serves.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AuthorId { get; set; }
    public PostKind Kind { get; set; } = PostKind.Text;

    /// <summary>Max 3000 chars. Hashtags and mentions are extracted from this server-side on create and
    /// on edit — never accepted from the client, which would let a caller tag a trend it never wrote.</summary>
    public string Body { get; set; } = "";

    public PostVisibility Visibility { get; set; } = PostVisibility.Public;

    /// <summary>The event this post is attached to. Required for <see cref="PostVisibility.EventParticipants"/>,
    /// which has nothing to scope participation against without it.</summary>
    public Guid? EventId { get; set; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="PostKind.Share"/>. Always points at an ORIGINAL
    /// post: sharing a share re-targets the original rather than nesting, so the render depth is bounded
    /// at one by the data and not by client discipline.</summary>
    public Guid? SharedPostId { get; set; }

    // Contested columns — every one of these is mutated with a single UPDATE whose new value the database
    // computes (D-240). Loading the row and writing `LikeCount++` is a lost update under concurrency.
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }

    /// <summary>Platform moderation, mirroring <see cref="Event.IsHidden"/>. A hidden post is unreachable
    /// on every read path; its author is told <c>post_hidden</c> so moderation is not silent to them, while
    /// everyone else gets the same 404 a nonexistent post gets.</summary>
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }

    /// <summary>Soft delete. Comments on a deleted post are not resurrectable and its counters stop being
    /// served, so the row survives only to keep foreign keys (reshares, reports) intact.</summary>
    public bool IsDeleted { get; set; }
    public Guid? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
}

/// <summary>One file on a post (D-262), following <see cref="ChatAttachment"/>'s two-step upload.
///
/// <para><see cref="PostId"/> is nullable because the upload completes BEFORE the post exists: the client
/// presigns, PUTs, confirms, and only then creates the post referencing the media. Anything never claimed
/// is an orphan, which is the entire reason <c>PostMediaCleanupJob</c> exists.</para>
///
/// <para>Unlike chat, the row is created at PRESIGN rather than at confirm, because the frozen client
/// contract hands back a <c>mediaId</c> from presign. Until <see cref="IsConfirmed"/> the row holds only
/// the client's unverified claims and can never be attached to a post.</para></summary>
public class PostMedia
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Null until a post claims it. Exactly one post, once set.</summary>
    public Guid? PostId { get; set; }
    public Guid UploadedBy { get; set; }

    public PostMediaKind Kind { get; set; } = PostMediaKind.Image;
    public string StorageKey { get; set; } = null!;

    /// <summary>Display only — sanitized at presign and never used to build a path.</summary>
    public string FileName { get; set; } = null!;

    /// <summary>The client's claim until <see cref="IsConfirmed"/>, then re-derived from the magic bytes.</summary>
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Always null today: no media probe is wired, and a fabricated duration is worse than an
    /// absent one. Nullable in the contract precisely so a real probe can fill it without a client change.</summary>
    public int? DurationSeconds { get; set; }

    /// <summary>Render order within the post, assigned from the order the author submitted the ids in.</summary>
    public int Sort { get; set; }

    /// <summary>False until the server has looked at the bytes. An unconfirmed row is not attachable.</summary>
    public bool IsConfirmed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the owning post is deleted; the storage object goes in the sweep, so deletion
    /// stays fast and the sweep stays idempotent.</summary>
    public DateTime? DeletedAt { get; set; }
}

public class PostPoll
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }                       // unique — one poll per post
    public string Question { get; set; } = null!;
    public bool AllowMultiple { get; set; }
    public DateTime? ClosesAt { get; set; }

    /// <summary>Total votes CAST, i.e. the sum of every option's count — not the number of voters
    /// (D-262). For a single-choice poll the two are identical; for a multi-select they are not, and
    /// counting rows is the definition that stays exact under concurrency without a second claim.</summary>
    public int TotalVotes { get; set; }
}

public class PostPollOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public string Text { get; set; } = null!;
    public int Sort { get; set; }
    public int VoteCount { get; set; }
}

/// <summary>One voter's claim on a poll, unique per (poll, user) (D-262).
///
/// <para>It exists because a ballot is N rows in <see cref="PostPollVote"/> and "one ballot per voter" is
/// not expressible as a constraint over N rows. Without it two concurrent votes from one user can each see
/// no prior vote and each insert a DIFFERENT option, and the unique index on the vote rows — which only
/// forbids the same option twice — lets both through. This row is inserted first, so the loser fails on
/// the index rather than stuffing the ballot box.</para></summary>
public class PostPollBallot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostPollVote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public Guid OptionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostLike
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A comment, or a reply to one. Threading is exactly one level deep: a reply to a reply attaches
/// to the same parent, the rule chat already uses. Two levels is a UI affordance; ten is a bug report.</summary>
public class PostComment
{
    /// <summary>UUIDv7 — same keyset-cursor role as <see cref="Post.Id"/>.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PostId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = null!;              // max 1000 chars
    public Guid? ParentCommentId { get; set; }
    public int LikeCount { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostCommentLike
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommentId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostSave
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Extracted from the body server-side, normalized lowercase. Stored rather than searched for
/// because the trending query and the per-tag feed both need an index, and LIKE '%#tag%' is neither.</summary>
public class PostHashtag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public string Tag { get; set; } = null!;               // lowercase, no leading '#'
}

/// <summary>An @mention that resolved to a real account. Unresolvable handles are dropped silently — a
/// mention is a link and a notification, and neither means anything without a user behind it.</summary>
public class PostMention
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid MentionedUserId { get; set; }
}
