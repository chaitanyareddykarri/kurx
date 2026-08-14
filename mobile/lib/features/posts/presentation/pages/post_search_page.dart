import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../providers/posts_providers.dart';
import 'feed_page.dart';

/// Post search. It owns only the query — the list, paging, and every optimistic interaction come from
/// [FeedPage] with a [SearchFeed] source, so search behaves exactly like the feed rather than like a
/// second, subtly different list.
///
/// An empty query is submitted as-is: the API answers an empty page without touching the database, so
/// the resting state costs nothing and needs no branch here.
class PostSearchPage extends StatefulWidget {
  const PostSearchPage({super.key});

  @override
  State<PostSearchPage> createState() => _PostSearchPageState();
}

class _PostSearchPageState extends State<PostSearchPage> {
  final _controller = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FeedPage(
      // The term is the family key, so submitting a new one swaps to a different provider and its own
      // paging state — no manual reset needed.
      source: SearchFeed(_query),
      title: 'Search posts',
      showCompose: false,
      emptyTitle: _query.isEmpty ? 'Search posts' : 'No posts match that search',
      emptyMessage: _query.isEmpty
          ? 'Find posts by any word in them. You only ever see posts you already have access to.'
          : 'Try a different word, or browse a hashtag from Trending.',
      appBarBottom: PreferredSize(
        preferredSize: const Size.fromHeight(64),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.md),
          child: TextField(
            controller: _controller,
            autofocus: true,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'Search posts',
              prefixIcon: const Icon(Icons.search_rounded),
              suffixIcon: _query.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'Clear',
                      icon: const Icon(Icons.close_rounded),
                      onPressed: () {
                        _controller.clear();
                        setState(() => _query = '');
                      },
                    ),
            ),
            onSubmitted: (v) => setState(() => _query = v.trim()),
          ),
        ),
      ),
    );
  }
}
