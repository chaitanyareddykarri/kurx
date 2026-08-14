import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../../../core/storage/event_cache.dart';
import '../../data/datasources/events_remote_data_source.dart';
import '../../data/repositories/events_repository_impl.dart';
import '../../domain/entities/event_page.dart';
import '../../domain/entities/event_summary.dart';
import '../../domain/repositories/events_repository.dart';
import '../../domain/usecases/get_event_page.dart';
import '../../domain/usecases/get_upcoming_events.dart';

/// Read access to the public event-discovery API, with a last-good cache for
/// the upcoming list (see [EventsRepositoryImpl]).
final eventsRepositoryProvider = Provider<EventsRepository>((ref) {
  return EventsRepositoryImpl(
    EventsRemoteDataSource(ref.watch(dioProvider)),
    ref.watch(eventCacheProvider),
  );
});

final _getUpcomingEventsProvider =
    Provider((ref) => GetUpcomingEvents(ref.watch(eventsRepositoryProvider)));

/// The upcoming-events list for the home screen. Auto-disposes; refreshed via
/// `ref.invalidate` on pull-to-refresh / retry.
final upcomingEventsProvider = FutureProvider.autoDispose<List<EventSummary>>((ref) {
  return ref.watch(_getUpcomingEventsProvider).call();
});

final _getEventPageProvider =
    Provider((ref) => GetEventPage(ref.watch(eventsRepositoryProvider)));

/// The full detail page (event + ticket types + related) keyed by slug.
final eventPageProvider = FutureProvider.autoDispose.family<EventPage, String>((ref, slug) {
  return ref.watch(_getEventPageProvider).call(slug);
});
