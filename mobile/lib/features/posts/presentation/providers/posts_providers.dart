import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/datasources/posts_remote_data_source.dart';
import '../../domain/entities/post.dart';

/// Posts state (D-262).
///
/// No repository layer: it would be a pure pass-through to the data source, which is the shape most
/// of this app's features already use (only auth, events and social carry one, and each of those
/// has real logic to hold — caching, an outbox, token rotation). Posts has none of that.

final postsSourceProvider = Provider<PostsRemoteDataSource>(
  (ref) => PostsRemoteDataSource(ref.watch(dioProvider)),
);

/// Which list a [PostFeedController] is showing. Selecting the loader here keeps every screen —
/// home feed, a profile's posts, an event's posts, a hashtag — on one paging implementation.
///
/// Value equality is load-bearing, not tidiness: `postFeedProvider` is a family, and a family keys
/// its cache on the argument. Without `==`/`hashCode`, `UserFeed('asha')` built during a rebuild
/// would not match the previous instance and every rebuild would spawn a fresh provider and refetch.
sealed class FeedSource {
  const FeedSource();

  /// The value that distinguishes one source of the same kind from another.
  String get key;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is FeedSource && other.runtimeType == runtimeType && other.key == key);

  @override
  int get hashCode => Object.hash(runtimeType, key);
}

class HomeFeed extends FeedSource {
  const HomeFeed();
  @override
  String get key => 'home';
}

class MyPostsFeed extends FeedSource {
  const MyPostsFeed();
  @override
  String get key => 'mine';
}

class SavedPostsFeed extends FeedSource {
  const SavedPostsFeed();
  @override
  String get key => 'saved';
}

class UserFeed extends FeedSource {
  const UserFeed(this.username);
  final String username;
  @override
  String get key => username;
}

class EventFeed extends FeedSource {
  const EventFeed(this.eventId);
  final String eventId;
  @override
  String get key => eventId;
}

class HashtagFeed extends FeedSource {
  const HashtagFeed(this.tag);
  final String tag;
  @override
  String get key => tag;
}

/// Free-text post search. The term IS the key, so `.family` gives each query its own cached feed and
/// typing a new one is a new provider rather than a mutation of the old one's paging state.
class SearchFeed extends FeedSource {
  const SearchFeed(this.q);
  final String q;
  @override
  String get key => q;
}

class FeedState {
  const FeedState({
    this.posts = const [],
    this.cursor,
    this.loadingMore = false,
    this.exhausted = false,
  });

  final List<Post> posts;
  final String? cursor;
  final bool loadingMore;
  final bool exhausted;

  FeedState copyWith({
    List<Post>? posts,
    String? cursor,
    bool? loadingMore,
    bool? exhausted,
  }) =>
      FeedState(
        posts: posts ?? this.posts,
        cursor: cursor ?? this.cursor,
        loadingMore: loadingMore ?? this.loadingMore,
        exhausted: exhausted ?? this.exhausted,
      );
}

class PostFeedController extends FamilyAsyncNotifier<FeedState, FeedSource> {
  PostsRemoteDataSource get _source => ref.read(postsSourceProvider);

  Future<PostPage> _load(String? cursor) => switch (arg) {
        HomeFeed() => _source.feed(cursor: cursor),
        MyPostsFeed() => _source.myPosts(cursor: cursor),
        SavedPostsFeed() => _source.savedPosts(cursor: cursor),
        UserFeed(:final username) => _source.userPosts(username, cursor: cursor),
        EventFeed(:final eventId) => _source.eventPosts(eventId, cursor: cursor),
        HashtagFeed(:final tag) => _source.hashtagPosts(tag, cursor: cursor),
        SearchFeed(:final q) => _source.searchPosts(q, cursor: cursor),
      };

  @override
  Future<FeedState> build(FeedSource arg) async {
    final page = await _load(null);
    return FeedState(posts: page.items, cursor: page.nextCursor, exhausted: !page.hasMore);
  }

  Future<void> loadMore() async {
    final current = state.valueOrNull;
    if (current == null || current.loadingMore || current.exhausted) return;

    state = AsyncData(current.copyWith(loadingMore: true));
    try {
      final page = await _load(current.cursor);
      // The feed is keyset-paged, so a post created between two fetches shifts the window and the
      // boundary row legitimately arrives twice. Without this the list renders duplicate keys.
      final seen = current.posts.map((p) => p.id).toSet();
      state = AsyncData(FeedState(
        posts: [...current.posts, ...page.items.where((p) => !seen.contains(p.id))],
        cursor: page.nextCursor,
        exhausted: !page.hasMore,
      ));
    } catch (_) {
      // A failed page must not discard what is already on screen.
      state = AsyncData(current.copyWith(loadingMore: false));
    }
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => build(arg));
  }

  void _replace(String postId, Post Function(Post) update) {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(current.copyWith(
      posts: [
        for (final p in current.posts) p.id == postId ? update(p) : p,
      ],
    ));
  }

  /// Optimistic flip, then the server's own count is written over it. The backend mutates the
  /// counter in SQL (D-240), so a locally incremented number is a guess — only the response is true.
  Future<void> toggleLike(Post post) async {
    _replace(
      post.id,
      (p) => p.copyWith(
        likedByMe: !post.likedByMe,
        likeCount: post.likeCount + (post.likedByMe ? -1 : 1),
      ),
    );
    try {
      final result = await _source.setLike(post.id, !post.likedByMe);
      _replace(post.id, (p) => p.copyWith(likedByMe: result.liked, likeCount: result.likeCount));
    } catch (_) {
      _replace(post.id, (p) => p.copyWith(likedByMe: post.likedByMe, likeCount: post.likeCount));
    }
  }

  Future<void> toggleSaved(Post post) async {
    _replace(post.id, (p) => p.copyWith(savedByMe: !post.savedByMe));
    try {
      await _source.setSaved(post.id, !post.savedByMe);
    } catch (_) {
      _replace(post.id, (p) => p.copyWith(savedByMe: post.savedByMe));
    }
  }

  Future<void> vote(Post post, String optionId) async {
    final poll = post.poll;
    if (poll == null || !poll.canVote) return;

    final selected = poll.allowMultiple
        ? [...poll.options.where((o) => o.votedByMe).map((o) => o.id), optionId]
        : [optionId];

    final updated = await _source.votePoll(post.id, selected);
    if (updated != null) _replace(post.id, (p) => p.copyWith(poll: updated));
  }

  Future<void> delete(Post post) async {
    final current = state.valueOrNull;
    if (current == null) return;
    final snapshot = current.posts;
    state = AsyncData(current.copyWith(posts: snapshot.where((p) => p.id != post.id).toList()));
    try {
      await _source.delete(post.id);
    } catch (_) {
      state = AsyncData(current.copyWith(posts: snapshot));
    }
  }

  /// Puts a freshly-composed post at the top without a round-trip.
  void prepend(Post post) {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(current.copyWith(
      posts: [post, ...current.posts.where((p) => p.id != post.id)],
    ));
  }
}

final postFeedProvider =
    AsyncNotifierProvider.family<PostFeedController, FeedState, FeedSource>(PostFeedController.new);

final postDetailProvider = FutureProvider.autoDispose.family<Post, String>(
  (ref, postId) => ref.watch(postsSourceProvider).post(postId),
);

final postCommentsProvider = FutureProvider.autoDispose.family<PostCommentPage, String>(
  (ref, postId) => ref.watch(postsSourceProvider).comments(postId),
);

final trendingHashtagsProvider = FutureProvider.autoDispose<List<TrendingHashtag>>(
  (ref) => ref.watch(postsSourceProvider).trendingHashtags(),
);
