import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../../domain/entities/post.dart';
import '../models/post_mappers.dart';

/// REST access to the frozen Posts contract (D-262). One method per endpoint, no extra shaping —
/// business decisions live in the repository, not here.
class PostsRemoteDataSource {
  /// [uploadClient] is a SEPARATE Dio for the same reason as chat's: a presigned URL is absolute and
  /// carries its own signature, so it must not inherit the app client's base URL or Authorization
  /// header — with real object storage a stray header invalidates the signature.
  PostsRemoteDataSource(this._dio, {Dio? uploadClient}) : _upload = uploadClient ?? Dio();

  final Dio _dio;
  final Dio _upload;

  Future<PostPage> _page(String path, {String? cursor, int limit = 20}) => guard(() async {
        final res = await _dio.get(path, queryParameters: {'cursor': ?cursor, 'limit': limit});
        return PostMappers.page(Map<String, dynamic>.from(res.data as Map));
      });

  Future<PostPage> feed({String? cursor, int limit = 20}) => _page('/v1/feed', cursor: cursor, limit: limit);

  Future<PostPage> myPosts({String? cursor}) => _page('/v1/me/posts', cursor: cursor);

  Future<PostPage> savedPosts({String? cursor}) => _page('/v1/me/posts/saved', cursor: cursor);

  Future<PostPage> userPosts(String username, {String? cursor}) =>
      _page('/v1/public/users/$username/posts', cursor: cursor);

  Future<PostPage> eventPosts(String eventId, {String? cursor}) =>
      _page('/v1/events/$eventId/posts', cursor: cursor);

  Future<PostPage> hashtagPosts(String tag, {String? cursor}) =>
      _page('/v1/hashtags/$tag/posts', cursor: cursor);

  /// Free-text search over post bodies, scoped server-side to what the caller may already see. Its own
  /// call rather than `_page` because it is the one listing here that carries a query parameter.
  Future<PostPage> searchPosts(String q, {String? cursor, int limit = 20}) => guard(() async {
        final res = await _dio.get(
          '/v1/posts/search',
          queryParameters: {'q': q, 'cursor': ?cursor, 'limit': limit},
        );
        return PostMappers.page(Map<String, dynamic>.from(res.data as Map));
      });

  Future<Post> post(String postId) => guard(() async {
        final res = await _dio.get('/v1/posts/$postId');
        return PostMappers.post(Map<String, dynamic>.from(res.data as Map));
      });

  Future<List<TrendingHashtag>> trendingHashtags({int limit = 10}) => guard(() async {
        final res = await _dio.get('/v1/hashtags/trending', queryParameters: {'limit': limit});
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((e) => PostMappers.trending(Map<String, dynamic>.from(e)))
            .toList();
      });

  /// Request bodies are camelCase — the platform contract is camelCase in, snake_case out (D-259).
  Future<Post> create({
    required String body,
    required String kind,
    required String visibility,
    List<String> mediaIds = const [],
    String? eventId,
    String? sharedPostId,
    Map<String, dynamic>? poll,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/posts', data: {
          'body': body,
          'kind': kind,
          'visibility': visibility,
          if (mediaIds.isNotEmpty) 'mediaIds': mediaIds,
          'eventId': ?eventId,
          'sharedPostId': ?sharedPostId,
          'poll': ?poll,
        });
        return PostMappers.post(Map<String, dynamic>.from(res.data as Map));
      });

  Future<Post> update(String postId, {String? body, String? visibility}) => guard(() async {
        final res = await _dio.patch('/v1/posts/$postId', data: {
          'body': ?body,
          'visibility': ?visibility,
        });
        return PostMappers.post(Map<String, dynamic>.from(res.data as Map));
      });

  Future<void> delete(String postId) => guard(() async {
        await _dio.delete('/v1/posts/$postId');
      });

  Future<PostLikeResult> setLike(String postId, bool liked) => guard(() async {
        final path = '/v1/posts/$postId/like';
        final res = liked ? await _dio.post(path) : await _dio.delete(path);
        return PostMappers.likeResult(Map<String, dynamic>.from(res.data as Map));
      });

  Future<void> setSaved(String postId, bool saved) => guard(() async {
        final path = '/v1/posts/$postId/save';
        if (saved) {
          await _dio.post(path);
        } else {
          await _dio.delete(path);
        }
      });

  Future<PostPoll?> votePoll(String postId, List<String> optionIds) => guard(() async {
        final res = await _dio.post('/v1/posts/$postId/poll/vote', data: {'optionIds': optionIds});
        return PostMappers.poll(res.data);
      });

  Future<PostCommentPage> comments(String postId, {String? cursor, int limit = 20}) => guard(() async {
        final res = await _dio.get(
          '/v1/posts/$postId/comments',
          queryParameters: {'cursor': ?cursor, 'limit': limit},
        );
        return PostMappers.commentPage(Map<String, dynamic>.from(res.data as Map));
      });

  Future<PostComment> addComment(String postId, String body, {String? parentCommentId}) =>
      guard(() async {
        final res = await _dio.post('/v1/posts/$postId/comments', data: {
          'body': body,
          'parentCommentId': ?parentCommentId,
        });
        return PostMappers.comment(Map<String, dynamic>.from(res.data as Map));
      });

  Future<void> deleteComment(String commentId) => guard(() async {
        await _dio.delete('/v1/comments/$commentId');
      });

  Future<PostLikeResult> setCommentLike(String commentId, bool liked) => guard(() async {
        final path = '/v1/comments/$commentId/like';
        final res = liked ? await _dio.post(path) : await _dio.delete(path);
        return PostMappers.likeResult(Map<String, dynamic>.from(res.data as Map));
      });

  /// Content reports reuse the platform endpoint (D-059) rather than a posts-specific one —
  /// [entityType] is `post` or `post_comment`. One moderation queue, not two.
  Future<void> report({
    required String entityType,
    required String entityId,
    required String reason,
    String? details,
  }) =>
      guard(() async {
        await _dio.post('/v1/reports', data: {
          'entityType': entityType,
          'entityId': entityId,
          'reason': reason,
          'details': ?details,
        });
      });

  // ── Media ──────────────────────────────────────────────────────────────────

  Future<PostMediaTicket> presignMedia({
    required String fileName,
    required String contentType,
    required int sizeBytes,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/posts/media/presign', data: {
          'fileName': fileName,
          'contentType': contentType,
          'sizeBytes': sizeBytes,
        });
        return PostMappers.mediaTicket(Map<String, dynamic>.from(res.data as Map));
      });

  Future<void> uploadBytes(PostMediaTicket ticket, Uint8List bytes, String contentType) =>
      guard(() async {
        await _upload.put(
          ticket.uploadUrl,
          data: Stream.fromIterable([bytes]),
          options: Options(
            headers: {'Content-Type': contentType, Headers.contentLengthHeader: bytes.length},
          ),
        );
      });

  Future<PostMedia> confirmMedia(String mediaId, String storageKey) => guard(() async {
        final res = await _dio.post('/v1/posts/media/confirm', data: {
          'mediaId': mediaId,
          'storageKey': storageKey,
        });
        return PostMappers.media(Map<String, dynamic>.from(res.data as Map));
      });
}
