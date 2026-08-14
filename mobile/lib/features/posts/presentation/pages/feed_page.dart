import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_shell_app_bar.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/post.dart';
import '../providers/posts_providers.dart';
import '../widgets/post_card.dart';

/// The Posts tab. One list implementation serves the home feed, a profile's posts, an event's posts
/// and a hashtag — [FeedSource] picks which, so paging and optimistic like/save exist exactly once.
class FeedPage extends ConsumerStatefulWidget {
  const FeedPage({
    super.key,
    this.source = const HomeFeed(),
    this.title,
    this.showCompose = true,
    this.appBarBottom,
    this.emptyTitle = 'Your feed is quiet',
    this.emptyMessage = 'Connect with people and follow organizations to see their posts here.',
  });

  final FeedSource source;
  final String? title;
  final bool showCompose;

  /// Lets a caller hang its own control under the title — the search field, today. Passing it in beats
  /// a second copy of this whole list widget beside a `TextField`.
  final PreferredSizeWidget? appBarBottom;

  final String emptyTitle;
  final String emptyMessage;

  @override
  ConsumerState<FeedPage> createState() => _FeedPageState();
}

class _FeedPageState extends ConsumerState<FeedPage> {
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_maybeLoadMore);
  }

  @override
  void dispose() {
    _scroll.removeListener(_maybeLoadMore);
    _scroll.dispose();
    super.dispose();
  }

  void _maybeLoadMore() {
    if (!_scroll.hasClients) return;
    // Fetch a screen ahead of the bottom so the next page is usually there before it is needed.
    if (_scroll.position.pixels >= _scroll.position.maxScrollExtent - 600) {
      ref.read(postFeedProvider(widget.source).notifier).loadMore();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(postFeedProvider(widget.source));
    final controller = ref.read(postFeedProvider(widget.source).notifier);

    return Scaffold(
      // A nested feed (a hashtag, an event's posts, my/saved posts) is pushed on top of a tab, so it
      // gets a plain bar with a back button rather than the shell's profile/notification corners.
      appBar: widget.showCompose
          ? KurxShellAppBar(
              title: widget.title ?? 'Posts',
              bottom: widget.appBarBottom,
              actions: [
                IconButton(
                  icon: const Icon(Icons.search_rounded),
                  tooltip: 'Search posts',
                  onPressed: () => context.push('/posts/search'),
                ),
                IconButton(
                  icon: const Icon(Icons.tag_rounded),
                  tooltip: 'Trending',
                  onPressed: () => context.push('/posts/trending'),
                ),
                PopupMenuButton<String>(
                  tooltip: 'Your posts',
                  icon: const Icon(Icons.more_vert_rounded),
                  onSelected: (v) => context.push(v),
                  itemBuilder: (_) => const [
                    PopupMenuItem(value: '/posts/mine', child: Text('My posts')),
                    PopupMenuItem(value: '/posts/saved', child: Text('Saved posts')),
                  ],
                ),
              ],
            )
          : AppBar(title: Text(widget.title ?? 'Posts'), bottom: widget.appBarBottom),
      floatingActionButton: widget.showCompose
          ? FloatingActionButton(
              tooltip: 'Create post',
              onPressed: () async {
                final created = await context.push<Post>('/posts/compose');
                if (created != null) controller.prepend(created);
              },
              child: const Icon(Icons.edit_outlined),
            )
          : null,
      body: AsyncValueView(
        value: state,
        onRetry: controller.refresh,
        data: (feed) {
          if (feed.posts.isEmpty) {
            return EmptyState(
              icon: Icons.article_outlined,
              title: widget.emptyTitle,
              message: widget.emptyMessage,
            );
          }
          return RefreshIndicator(
            onRefresh: controller.refresh,
            child: ListView.separated(
              controller: _scroll,
              padding: const EdgeInsets.fromLTRB(KSpace.lg, KSpace.lg, KSpace.lg, KSpace.xxxl),
              itemCount: feed.posts.length + (feed.exhausted ? 0 : 1),
              separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
              itemBuilder: (context, i) {
                if (i >= feed.posts.length) {
                  return const Padding(
                    padding: EdgeInsets.all(KSpace.lg),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                final post = feed.posts[i];
                return PostCard(
                  post: post,
                  onLike: () => controller.toggleLike(post),
                  onSave: () => controller.toggleSaved(post),
                  onVote: (optionId) => controller.vote(post, optionId),
                  onDelete: () => controller.delete(post),
                  onEdit: () async {
                    final updated = await context.push<Post>('/posts/${post.id}/edit', extra: post);
                    if (updated != null) controller.refresh();
                  },
                  onShare: () async {
                    final created = await context.push<Post>('/posts/compose', extra: post);
                    if (created != null) controller.prepend(created);
                  },
                );
              },
            ),
          );
        },
      ),
    );
  }
}
