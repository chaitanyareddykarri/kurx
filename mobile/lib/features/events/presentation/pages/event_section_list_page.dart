import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/shimmer.dart';
import '../../domain/entities/event_section.dart';
import '../../domain/entities/event_summary.dart';
import '../providers/home_providers.dart';
import '../widgets/event_card.dart';

/// The full list for one discovery section (the "View all" target).
class EventSectionListPage extends ConsumerWidget {
  const EventSectionListPage({super.key, required this.section});

  final EventSection section;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final eventsAsync = ref.watch(sectionEventsProvider(section));

    return Scaffold(
      appBar: AppBar(title: Text('${section.title} events')),
      body: RefreshIndicator(
        onRefresh: () async => ref.refresh(sectionEventsProvider(section).future),
        child: AsyncValueView<List<EventSummary>>(
          value: eventsAsync,
          loading: const ListSkeleton(),
          onRetry: () => ref.invalidate(sectionEventsProvider(section)),
          isEmpty: (list) => list.isEmpty,
          data: (list) => ListView.builder(
            padding: const EdgeInsets.symmetric(vertical: 8),
            itemCount: list.length,
            itemBuilder: (_, i) => EventCard(
              event: list[i],
              onTap: () => context.push('/events/${list[i].slug}'),
            ),
          ),
        ),
      ),
    );
  }
}
