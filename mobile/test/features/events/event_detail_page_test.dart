import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_category.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_detail.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_kind.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_query.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_summary.dart';
import 'package:kurx_mobile/features/events/domain/entities/paged_events.dart';
import 'package:kurx_mobile/features/events/domain/entities/ticket_type.dart';
import 'package:kurx_mobile/features/events/domain/repositories/events_repository.dart';
import 'package:kurx_mobile/features/events/presentation/pages/event_detail_page.dart';
import 'package:kurx_mobile/features/events/presentation/providers/events_providers.dart';

class _FakeRepo implements EventsRepository {
  @override
  Future<List<EventSummary>> upcoming({int limit = 20}) async => const [];

  @override
  Future<EventDetail> detail(String slug) async => const EventDetail(
        id: 'e1',
        title: 'Sunburn Arena — Live in Mumbai',
        slug: 'sunburn',
        subtitle: 'An unforgettable night of music',
        description: 'Sunburn returns to Mumbai.',
        tags: ['edm'],
        venueName: 'NSCI Dome',
        city: 'Mumbai',
        status: 'published',
      );

  @override
  Future<List<EventSummary>> related(String slug, {int limit = 6}) async => const [
        EventSummary(id: 'r1', title: 'Indie Nights Bengaluru', slug: 'indie', status: 'published'),
      ];

  @override
  Future<List<TicketType>> ticketTypes(String eventId) async => const [
        TicketType(id: 't1', name: 'General Admission', pricePaise: 150000, available: 90, quantity: 100),
      ];

  @override
  Future<PagedEvents> search(EventQuery query) => throw UnimplementedError();

  @override
  Future<List<EventCategory>> categories() => throw UnimplementedError();
  @override
  Future<List<EventCategory>> eventTypes() => throw UnimplementedError();
  @override
  Future<List<EventKind>> kinds() async => const [];
  @override
  Future<List<EventSummary>> forYou({int limit = 10}) async => const [];

  @override
  Future<List<EventSummary>> featured({int limit = 20}) => throw UnimplementedError();

  @override
  Future<List<EventSummary>> trending({int limit = 20}) => throw UnimplementedError();

  @override
  Future<List<EventSummary>> latest({int limit = 20}) => throw UnimplementedError();
}

void main() {
  setUpAll(() async => initializeDateFormatting('en_IN'));

  testWidgets('renders detail, a ticket price, and related events', (tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [eventsRepositoryProvider.overrideWithValue(_FakeRepo())],
      child: const MaterialApp(home: EventDetailPage(slug: 'sunburn')),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Sunburn Arena — Live in Mumbai'), findsOneWidget);

    final scrollable = find.byType(Scrollable).first;

    await tester.scrollUntilVisible(find.text('Sunburn returns to Mumbai.'), 200, scrollable: scrollable);
    expect(find.text('Sunburn returns to Mumbai.'), findsOneWidget);

    // Tickets sit below the richer hero header — scroll them into view.
    await tester.scrollUntilVisible(find.text('General Admission'), 300, scrollable: scrollable);
    expect(find.text('General Admission'), findsOneWidget);
    // 150000 paise → ₹1,500, shown in the ticket tile and/or the sticky book bar.
    expect(find.text('₹1,500'), findsWidgets);

    await tester.scrollUntilVisible(find.text('Indie Nights Bengaluru'), 300, scrollable: scrollable);
    expect(find.text('Indie Nights Bengaluru'), findsOneWidget);
  });

  testWidgets('tapping a ticket routes to checkout with the event and ticket ids', (tester) async {
    String? capturedEventId;
    String? capturedTicketTypeId;

    // A real GoRouter mirroring app_router.dart's actual checkout route — a bare Navigator
    // wouldn't exercise the `context.push` the tile's onBook callback performs.
    final router = GoRouter(
      initialLocation: '/events/sunburn',
      routes: [
        GoRoute(
          path: '/events/:slug',
          builder: (_, _) => const EventDetailPage(slug: 'sunburn'),
        ),
        GoRoute(
          path: '/events/:slug/checkout/:eventId/:ticketTypeId',
          builder: (_, s) {
            capturedEventId = s.pathParameters['eventId'];
            capturedTicketTypeId = s.pathParameters['ticketTypeId'];
            return const SizedBox.shrink();
          },
        ),
      ],
    );

    await tester.pumpWidget(ProviderScope(
      overrides: [eventsRepositoryProvider.overrideWithValue(_FakeRepo())],
      child: MaterialApp.router(routerConfig: router),
    ));
    await tester.pumpAndSettle();

    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(find.text('General Admission'), 300, scrollable: scrollable);
    // scrollUntilVisible only guarantees the finder matches, not that the rect clears the
    // viewport edge — ensureVisible scrolls the remaining sliver so the tap actually hit-tests.
    await tester.ensureVisible(find.text('General Admission'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('General Admission'));
    await tester.pumpAndSettle();

    expect(capturedEventId, 'e1');
    expect(capturedTicketTypeId, 't1');
  });
}
