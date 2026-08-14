import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/posts_providers.dart';

/// Trending hashtags. The web feed shows these in a sidebar rail; a phone has no room beside the
/// feed, so on mobile they get their own screen reached from the Posts app bar.
class HashtagBrowsePage extends ConsumerWidget {
  const HashtagBrowsePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final trending = ref.watch(trendingHashtagsProvider);
    final c = context.kurx;

    return Scaffold(
      appBar: AppBar(title: const Text('Trending')),
      body: AsyncValueView(
        value: trending,
        onRetry: () => ref.invalidate(trendingHashtagsProvider),
        data: (tags) => tags.isEmpty
            ? const EmptyState(
                icon: Icons.tag_rounded,
                title: 'Nothing trending yet',
                message: 'Tags people use in posts show up here.',
              )
            : ListView.separated(
                padding: const EdgeInsets.all(KSpace.lg),
                itemCount: tags.length,
                separatorBuilder: (_, _) => Divider(height: 1, color: c.border),
                itemBuilder: (context, i) {
                  final t = tags[i];
                  return ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: Icon(Icons.tag_rounded, color: c.accent),
                    title: Text('#${t.tag}'),
                    subtitle: Text('${t.postCount} ${t.postCount == 1 ? 'post' : 'posts'}'),
                    trailing: Icon(Icons.chevron_right_rounded, color: c.muted),
                    onTap: () => context.push('/posts/tag/${t.tag}'),
                  );
                },
              ),
      ),
    );
  }
}
