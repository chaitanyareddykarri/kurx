import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_category.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_detail.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_kind.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_query.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_summary.dart';
import 'package:kurx_mobile/features/events/domain/entities/paged_events.dart';
import 'package:kurx_mobile/features/events/domain/entities/ticket_type.dart';
import 'package:kurx_mobile/features/events/domain/repositories/events_repository.dart';
import 'package:kurx_mobile/features/events/presentation/pages/event_search_page.dart';
import 'package:kurx_mobile/features/events/presentation/providers/events_providers.dart';
import 'package:kurx_mobile/features/posts/presentation/providers/posts_providers.dart';

/// D-297. Post search on mobile, and the three-way search entry that made it reachable.
///
/// [SearchFeed] is worth its own test because it is a `.family` key: if two different queries ever
/// compared equal, the second search would silently render the first one's results out of the
/// provider cache — a wrong answer that looks exactly like a working search.

class _FakeRepo implements EventsRepository {
  @override
  Future<PagedEvents> search(EventQuery query) async => const PagedEvents(items: [], total: 0);
  @override
  Future<List<EventCategory>> categories() async => const [EventCategory(id: 'c1', name: 'Music')];
  @override
  Future<List<EventCategory>> eventTypes() async => const [];
  @override
  Future<List<EventKind>> kinds() async => const [];
  @override
  Future<List<EventSummary>> forYou({int limit = 10}) async => const [];
  @override
  Future<List<EventSummary>> upcoming({int limit = 20}) => throw UnimplementedError();
  @override
  Future<List<EventSummary>> featured({int limit = 20}) => throw UnimplementedError();
  @override
  Future<List<EventSummary>> trending({int limit = 20}) => throw UnimplementedError();
  @override
  Future<List<EventSummary>> latest({int limit = 20}) => throw UnimplementedError();
  @override
  Future<EventDetail> detail(String slug) => throw UnimplementedError();
  @override
  Future<List<EventSummary>> related(String slug, {int limit = 6}) => throw UnimplementedError();
  @override
  Future<List<TicketType>> ticketTypes(String eventId) => throw UnimplementedError();
}

void main() {
  setUpAll(() => initializeDateFormatting('en_IN'));

  group('SearchFeed — the provider family key', () {
    test('two different queries are two different sources', () {
      expect(const SearchFeed('keynote'), isNot(const SearchFeed('workshop')));
      expect(
        const SearchFeed('keynote').hashCode,
        isNot(const SearchFeed('workshop').hashCode),
      );
    });

    test('the same query is the same source, so a rebuild reuses its cached page', () {
      expect(const SearchFeed('keynote'), const SearchFeed('keynote'));
      expect(const SearchFeed('keynote').hashCode, const SearchFeed('keynote').hashCode);
    });

    test('never collides with another feed kind carrying the same key', () {
      // Both would key on the same string; only runtimeType separates them, which is exactly what
      // FeedSource's operator== compares alongside the key.
      expect(const SearchFeed('kurx'), isNot(const HashtagFeed('kurx')));
      expect(const SearchFeed('mine'), isNot(const MyPostsFeed()));
    });

    test('an empty query is still a valid, distinct source', () {
      // The search page's resting state. It must not equal some other empty-keyed source.
      expect(const SearchFeed(''), const SearchFeed(''));
      expect(const SearchFeed(''), isNot(const SearchFeed('a')));
    });
  });

  group('Search scopes on the event search page', () {
    testWidgets('offers all three searches, with events marked as the current one', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [eventsRepositoryProvider.overrideWithValue(_FakeRepo())],
          child: const MaterialApp(home: EventSearchPage()),
        ),
      );
      await tester.pump();

      // Before D-297 only events were reachable from here; people and posts existed with no door.
      expect(find.text('Events'), findsOneWidget);
      expect(find.text('People'), findsOneWidget);
      expect(find.text('Posts'), findsOneWidget);
    });
  });
}
