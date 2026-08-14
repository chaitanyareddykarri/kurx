import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/entities/event_section.dart';
import '../../domain/entities/event_summary.dart';
import '../../domain/entities/home_feed.dart';
import '../../domain/usecases/get_for_you_events.dart';
import '../../domain/usecases/get_home_feed.dart';
import 'events_providers.dart';

final _getHomeFeedProvider = Provider((ref) => GetHomeFeed(ref.watch(eventsRepositoryProvider)));
final _getForYouEventsProvider = Provider((ref) => GetForYouEvents(ref.watch(eventsRepositoryProvider)));

/// The discovery home feed (all sections + categories), refreshed via `ref.invalidate`.
final homeFeedProvider = FutureProvider.autoDispose<HomeFeed>(
  (ref) => ref.watch(_getHomeFeedProvider).call(),
);

/// V3 §15 (Phase 16) recommended feed (D-184). Only ever watched when signed in (see
/// `home_dashboard_page.dart`'s `isGuest` gate) — guests never trigger the underlying auth-required call.
final forYouProvider = FutureProvider.autoDispose<List<EventSummary>>(
  (ref) => ref.watch(_getForYouEventsProvider).call(),
);

/// A single section's full list, for the "View all" screen.
final sectionEventsProvider =
    FutureProvider.autoDispose.family<List<EventSummary>, EventSection>((ref, section) {
  final repo = ref.watch(eventsRepositoryProvider);
  return switch (section) {
    EventSection.featured => repo.featured(limit: 50),
    EventSection.trending => repo.trending(limit: 50),
    EventSection.upcoming => repo.upcoming(limit: 50),
    EventSection.latest => repo.latest(limit: 50),
  };
});
