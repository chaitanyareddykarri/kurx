import '../../domain/entities/post.dart';

/// JSON → entity mapping for the frozen Posts contract (D-262).
///
/// Keys are read as **snake_case only**, unlike `ChatMappers`. That is deliberate rather than
/// inconsistent: chat's tolerant snake-or-camel lookup exists because its parsers also read a
/// camelCase on-device outbox written by older builds (D-259). Posts ships after the wire settled
/// and has no cache to migrate, so accepting a second spelling would encode a legacy that never
/// existed.
///
/// Every parser is tolerant of missing or malformed fields: one bad post must not be able to take
/// down the whole feed.
class PostMappers {
  static DateTime _date(Object? v) =>
      v is String ? (DateTime.tryParse(v)?.toUtc() ?? DateTime.now().toUtc()) : DateTime.now().toUtc();

  static DateTime? _dateOrNull(Object? v) => v is String ? DateTime.tryParse(v)?.toUtc() : null;

  static bool _bool(Object? v) => v == true;
  static int _int(Object? v) => v is int ? v : int.tryParse('${v ?? ''}') ?? 0;
  static String _str(Object? v) => v == null ? '' : '$v';
  static String? _strOrNull(Object? v) {
    if (v == null) return null;
    final s = '$v';
    return s.isEmpty ? null : s;
  }

  static List<Map<String, dynamic>> _list(Object? v) => (v as List? ?? const [])
      .whereType<Map>()
      .map((e) => Map<String, dynamic>.from(e))
      .toList();

  static PostAuthor author(Map<String, dynamic>? json) {
    if (json == null) return const PostAuthor(id: '', name: 'Unknown');
    return PostAuthor(
      id: _str(json['id']),
      name: _str(json['name']).isEmpty ? 'Unknown' : _str(json['name']),
      username: _strOrNull(json['username']),
      avatarKey: _strOrNull(json['avatar_key']),
      isVerified: _bool(json['is_verified']),
    );
  }

  static PostMedia media(Map<String, dynamic> json) => PostMedia(
        id: _str(json['id']),
        kind: _str(json['kind']),
        url: _str(json['url']),
        contentType: _str(json['content_type']),
        sizeBytes: _int(json['size_bytes']),
        width: json['width'] is int ? json['width'] as int : null,
        height: json['height'] is int ? json['height'] as int : null,
        durationSeconds: json['duration_seconds'] is int ? json['duration_seconds'] as int : null,
        sort: _int(json['sort']),
      );

  static PostPollOption pollOption(Map<String, dynamic> json) => PostPollOption(
        id: _str(json['id']),
        text: _str(json['text']),
        sort: _int(json['sort']),
        voteCount: _int(json['vote_count']),
        votedByMe: _bool(json['voted_by_me']),
      );

  static PostPoll? poll(Object? value) {
    if (value is! Map) return null;
    final json = Map<String, dynamic>.from(value);
    return PostPoll(
      id: _str(json['id']),
      question: _str(json['question']),
      allowMultiple: _bool(json['allow_multiple']),
      closesAt: _dateOrNull(json['closes_at']),
      isClosed: _bool(json['is_closed']),
      totalVotes: _int(json['total_votes']),
      options: _list(json['options']).map(pollOption).toList()..sort((a, b) => a.sort.compareTo(b.sort)),
    );
  }

  static PostEventRef? eventRef(Object? value) {
    if (value is! Map) return null;
    final json = Map<String, dynamic>.from(value);
    return PostEventRef(
      id: _str(json['id']),
      title: _str(json['title']),
      slug: _str(json['slug']),
      startsAt: _date(json['starts_at']),
      bannerKey: _strOrNull(json['banner_key']),
      bannerUrl: _strOrNull(json['banner_url']),
      city: _strOrNull(json['city']),
    );
  }

  /// [allowShared] stops a malformed payload from recursing: the contract guarantees at most one
  /// level of sharing, so the nested parse never looks for another.
  static Post post(Map<String, dynamic> json, {bool allowShared = true}) => Post(
        id: _str(json['id']),
        author: author(json['author'] is Map ? Map<String, dynamic>.from(json['author'] as Map) : null),
        kind: _str(json['kind']),
        body: _str(json['body']),
        visibility: _str(json['visibility']).isEmpty ? 'public' : _str(json['visibility']),
        media: _list(json['media']).map(media).toList()..sort((a, b) => a.sort.compareTo(b.sort)),
        poll: poll(json['poll']),
        event: eventRef(json['event']),
        sharedPost: allowShared && json['shared_post'] is Map
            ? post(Map<String, dynamic>.from(json['shared_post'] as Map), allowShared: false)
            : null,
        hashtags: (json['hashtags'] as List? ?? const []).map((e) => '$e').toList(),
        mentions: _list(json['mentions']).map((m) => author(m)).toList(),
        likeCount: _int(json['like_count']),
        commentCount: _int(json['comment_count']),
        shareCount: _int(json['share_count']),
        likedByMe: _bool(json['liked_by_me']),
        savedByMe: _bool(json['saved_by_me']),
        canEdit: _bool(json['can_edit']),
        canDelete: _bool(json['can_delete']),
        createdAt: _date(json['created_at']),
        editedAt: _dateOrNull(json['edited_at']),
      );

  static PostPage page(Map<String, dynamic> json) => PostPage(
        items: _list(json['items']).map((e) => post(e)).toList(),
        nextCursor: _strOrNull(json['next_cursor']),
      );

  static PostComment comment(Map<String, dynamic> json) => PostComment(
        id: _str(json['id']),
        postId: _str(json['post_id']),
        author: author(json['author'] is Map ? Map<String, dynamic>.from(json['author'] as Map) : null),
        body: _str(json['body']),
        parentCommentId: _strOrNull(json['parent_comment_id']),
        likeCount: _int(json['like_count']),
        likedByMe: _bool(json['liked_by_me']),
        canDelete: _bool(json['can_delete']),
        createdAt: _date(json['created_at']),
      );

  static PostCommentPage commentPage(Map<String, dynamic> json) => PostCommentPage(
        items: _list(json['items']).map(comment).toList(),
        nextCursor: _strOrNull(json['next_cursor']),
      );

  static PostLikeResult likeResult(Map<String, dynamic> json) => PostLikeResult(
        liked: _bool(json['liked']),
        likeCount: _int(json['like_count']),
      );

  static PostMediaTicket mediaTicket(Map<String, dynamic> json) => PostMediaTicket(
        mediaId: _str(json['media_id']),
        uploadUrl: _str(json['upload_url']),
        storageKey: _str(json['storage_key']),
      );

  static TrendingHashtag trending(Map<String, dynamic> json) => TrendingHashtag(
        tag: _str(json['tag']),
        postCount: _int(json['post_count']),
      );
}
