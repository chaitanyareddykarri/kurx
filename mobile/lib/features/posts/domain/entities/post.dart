/// Domain entities for the Posts module (D-262).
///
/// Plain classes rather than freezed, matching `chat_*` and for the same reason: these are
/// mapping-only wire shapes. `copyWith` exists only where the UI actually reconciles a server
/// response onto a held object (like/save counts), not as a blanket generated API.
library;

class PostAuthor {
  const PostAuthor({
    required this.id,
    required this.name,
    this.username,
    this.avatarKey,
    this.isVerified = false,
  });

  final String id;
  final String name;
  final String? username;
  final String? avatarKey;
  final bool isVerified;
}

class PostMedia {
  const PostMedia({
    required this.id,
    required this.kind,
    required this.url,
    required this.contentType,
    required this.sizeBytes,
    this.width,
    this.height,
    this.durationSeconds,
    this.sort = 0,
  });

  final String id;
  final String kind;
  final String url;
  final String contentType;
  final int sizeBytes;
  final int? width;
  final int? height;
  final int? durationSeconds;
  final int sort;

  bool get isImage => contentType.startsWith('image/');
  bool get isVideo => contentType.startsWith('video/');
  bool get isDocument => !isImage && !isVideo;
}

class PostPollOption {
  const PostPollOption({
    required this.id,
    required this.text,
    this.sort = 0,
    this.voteCount = 0,
    this.votedByMe = false,
  });

  final String id;
  final String text;
  final int sort;
  final int voteCount;
  final bool votedByMe;
}

class PostPoll {
  const PostPoll({
    required this.id,
    required this.question,
    this.allowMultiple = false,
    this.closesAt,
    this.isClosed = false,
    this.totalVotes = 0,
    this.options = const [],
  });

  final String id;
  final String question;
  final bool allowMultiple;
  final DateTime? closesAt;
  final bool isClosed;
  final int totalVotes;
  final List<PostPollOption> options;

  bool get hasVoted => options.any((o) => o.votedByMe);

  /// Results stay hidden until the viewer has voted or the poll has closed — showing them earlier
  /// biases the vote, which is why the server sends `voted_by_me` per option rather than letting
  /// the client decide when to reveal.
  bool get showResults => hasVoted || isClosed;

  bool get canVote => !isClosed && (!hasVoted || allowMultiple);
}

class PostEventRef {
  const PostEventRef({
    required this.id,
    required this.title,
    required this.slug,
    required this.startsAt,
    this.bannerKey,
    this.bannerUrl,
    this.city,
  });

  final String id;
  final String title;
  final String slug;
  final DateTime startsAt;

  /// The raw storage key. **Not fetchable** — kept only because the wire carries it; render
  /// [bannerUrl] instead. `post_card.dart` used to pass this straight to `Image.network`, which is
  /// why an event referenced by a post always showed an empty box (D-302).
  final String? bannerKey;

  /// Presigned GET — the only renderable form.
  final String? bannerUrl;
  final String? city;
}

class Post {
  const Post({
    required this.id,
    required this.author,
    required this.kind,
    required this.body,
    required this.visibility,
    required this.createdAt,
    this.media = const [],
    this.poll,
    this.event,
    this.sharedPost,
    this.hashtags = const [],
    this.mentions = const [],
    this.likeCount = 0,
    this.commentCount = 0,
    this.shareCount = 0,
    this.likedByMe = false,
    this.savedByMe = false,
    this.canEdit = false,
    this.canDelete = false,
    this.editedAt,
  });

  final String id;
  final PostAuthor author;
  final String kind;
  final String body;
  final String visibility;
  final List<PostMedia> media;
  final PostPoll? poll;
  final PostEventRef? event;

  /// Exactly one level deep. The server reattaches a share-of-a-share to the original
  /// (`cannot_share_a_share`), so this is never itself carrying a `sharedPost`.
  final Post? sharedPost;

  final List<String> hashtags;
  final List<PostAuthor> mentions;
  final int likeCount;
  final int commentCount;
  final int shareCount;
  final bool likedByMe;
  final bool savedByMe;
  final bool canEdit;
  final bool canDelete;
  final DateTime createdAt;
  final DateTime? editedAt;

  Post copyWith({
    int? likeCount,
    int? commentCount,
    int? shareCount,
    bool? likedByMe,
    bool? savedByMe,
    PostPoll? poll,
  }) =>
      Post(
        id: id,
        author: author,
        kind: kind,
        body: body,
        visibility: visibility,
        media: media,
        poll: poll ?? this.poll,
        event: event,
        sharedPost: sharedPost,
        hashtags: hashtags,
        mentions: mentions,
        likeCount: likeCount ?? this.likeCount,
        commentCount: commentCount ?? this.commentCount,
        shareCount: shareCount ?? this.shareCount,
        likedByMe: likedByMe ?? this.likedByMe,
        savedByMe: savedByMe ?? this.savedByMe,
        canEdit: canEdit,
        canDelete: canDelete,
        createdAt: createdAt,
        editedAt: editedAt,
      );
}

class PostPage {
  const PostPage({this.items = const [], this.nextCursor});

  final List<Post> items;

  /// Opaque token (same rule as chat, D-104) — passed straight back, never parsed.
  final String? nextCursor;

  bool get hasMore => nextCursor != null;
}

class PostComment {
  const PostComment({
    required this.id,
    required this.postId,
    required this.author,
    required this.body,
    required this.createdAt,
    this.parentCommentId,
    this.likeCount = 0,
    this.likedByMe = false,
    this.canDelete = false,
  });

  final String id;
  final String postId;
  final PostAuthor author;
  final String body;
  final String? parentCommentId;
  final int likeCount;
  final bool likedByMe;
  final bool canDelete;
  final DateTime createdAt;

  bool get isReply => parentCommentId != null;

  PostComment copyWith({int? likeCount, bool? likedByMe}) => PostComment(
        id: id,
        postId: postId,
        author: author,
        body: body,
        parentCommentId: parentCommentId,
        likeCount: likeCount ?? this.likeCount,
        likedByMe: likedByMe ?? this.likedByMe,
        canDelete: canDelete,
        createdAt: createdAt,
      );
}

class PostCommentPage {
  const PostCommentPage({this.items = const [], this.nextCursor});

  final List<PostComment> items;
  final String? nextCursor;

  bool get hasMore => nextCursor != null;
}

class PostLikeResult {
  const PostLikeResult({required this.liked, required this.likeCount});

  final bool liked;
  final int likeCount;
}

class PostMediaTicket {
  const PostMediaTicket({required this.mediaId, required this.uploadUrl, required this.storageKey});

  final String mediaId;
  final String uploadUrl;
  final String storageKey;
}

class TrendingHashtag {
  const TrendingHashtag({required this.tag, required this.postCount});

  final String tag;
  final int postCount;
}

/// Mirrors the backend `PostVisibility` enum, widest audience first.
enum PostVisibility {
  public('public', 'Anyone'),
  connections('connections', 'Allies only'),
  eventParticipants('event_participants', 'Event participants'),
  onlyMe('only_me', 'Only me');

  const PostVisibility(this.wire, this.label);

  final String wire;
  final String label;

  static PostVisibility fromWire(String value) =>
      PostVisibility.values.where((v) => v.wire == value).firstOrNull ?? PostVisibility.public;
}

/// Caps mirror the server's (`too_many_media`) so the composer can disable its own picker rather
/// than letting someone upload into a rejection.
abstract final class PostLimits {
  static const int bodyMax = 3000;
  static const int commentMax = 1000;
  static const int images = 10;
  static const int videos = 1;
  static const int documents = 5;
}
