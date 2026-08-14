import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../events/presentation/widgets/event_card.dart';
import '../providers/bookmarks_providers.dart';

/// Saved events (bookmarks). Renders instantly from the local store; tapping the
/// bookmark on any card removes it and the list updates live.
class SavedPage extends ConsumerWidget {
  const SavedPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final saved = ref.watch(bookmarksControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Saved'),
        bottom: saved.isEmpty
            ? null
            : PreferredSize(
                preferredSize: const Size.fromHeight(28),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, 0, KSpace.sm),
                    child: Text(
                      '${saved.length} saved event${saved.length == 1 ? '' : 's'}',
                      style: TextStyle(color: context.kurx.muted, fontSize: 13),
                    ),
                  ),
                ),
              ),
      ),
      body: saved.isEmpty
          ? Center(
              child: EmptyState(
                icon: Icons.bookmark_border_rounded,
                title: 'No saved events yet',
                message: 'Tap the bookmark on any event to keep it here for later.',
                actionLabel: 'Discover events',
                onAction: () => context.go(Routes.events),
              ),
            )
          : ContentWidth(
              child: ListView.builder(
                padding: const EdgeInsets.symmetric(vertical: KSpace.sm),
                itemCount: saved.length,
                itemBuilder: (context, i) => EventCard(
                  event: saved[i],
                  onTap: () => context.push('/events/${saved[i].slug}'),
                ),
              ),
            ),
    );
  }
}
