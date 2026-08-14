import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
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
import 'package:kurx_mobile/features/events/presentation/providers/search_providers.dart';
import 'package:kurx_mobile/features/events/presentation/widgets/event_filter_bar.dart';

class _FakeRepo implements EventsRepository {
  _FakeRepo({this.results = const [], this.fail = false});
  final List<EventSummary> results;
  final bool fail;

  @override
  Future<PagedEvents> search(EventQuery query) async {
    if (fail) throw const ApiError(status: 0, code: 'network_error');
    return PagedEvents(items: results, total: results.length);
  }

  @override
  Future<List<EventCategory>> categories() async =>
      const [EventCategory(id: 'c1', name: 'Music')];
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

Widget _wrap(EventsRepository repo) => ProviderScope(
      overrides: [eventsRepositoryProvider.overrideWithValue(repo)],
      child: const MaterialApp(home: EventSearchPage()),
    );

void main() {
  setUpAll(() async => initializeDateFormatting('en_IN'));

  testWidgets('renders results with a count', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo(results: const [
      EventSummary(id: '1', title: 'Sunburn Arena', slug: 'sunburn', status: 'published'),
      EventSummary(id: '2', title: 'Indie Nights', slug: 'indie', status: 'published'),
    ])));
    await tester.pumpAndSettle();

    expect(find.text('2 results'), findsOneWidget);
    expect(find.text('Sunburn Arena'), findsOneWidget);
    // Filters and results now share one CustomScrollView, so there is a single scrollable rather
    // than a fixed header above an inner list.
    // `.first` because a TextField carries its own internal Scrollable for the text it holds, so
    // the filter bar puts more than one inside the page's scroll view.
    await tester.scrollUntilVisible(find.text('Indie Nights'), 200,
        scrollable: find
            .descendant(of: find.byType(CustomScrollView), matching: find.byType(Scrollable))
            .first);
    expect(find.text('Indie Nights'), findsOneWidget);
  });

  test('the date filter cannot reach into the past', () {
    final now = DateTime(2026, 8, 9, 14, 30);
    final floor = EventFilterBar.dateFloor(now);

    // Midnight today, so an event happening later today still matches.
    expect(floor, DateTime(2026, 8, 9));

    // A range left over from before the floor moved is dropped rather than handed to the picker,
    // which asserts on an initial range starting before its firstDate.
    expect(
      EventFilterBar.initialRangeWithin(
        DateTimeRange(start: DateTime(2025, 1, 1), end: DateTime(2026, 12, 1)),
        floor,
      ),
      isNull,
    );
    final future = DateTimeRange(start: DateTime(2026, 9, 1), end: DateTime(2026, 9, 5));
    expect(EventFilterBar.initialRangeWithin(future, floor), future);
    expect(EventFilterBar.initialRangeWithin(null, floor), isNull);
  });

  testWidgets('secondary filters stay disclosed, not stacked above the results', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo(results: const [
      EventSummary(id: '1', title: 'Sunburn Arena', slug: 'sunburn', status: 'published'),
    ])));
    await tester.pumpAndSettle();

    // Nine always-expanded controls used to push the results off the bottom of the screen. Only the
    // query and a way in to the rest belong on the page by default.
    expect(find.widgetWithText(OutlinedButton, 'Filters'), findsOneWidget);
    expect(find.text('All categories'), findsNothing);
    expect(find.widgetWithText(ChoiceChip, 'Free'), findsNothing);
    expect(find.text('Sunburn Arena'), findsOneWidget); // results visible without scrolling

    await tester.tap(find.widgetWithText(OutlinedButton, 'Filters'));
    await tester.pumpAndSettle();

    // Grouped, primary filters first.
    expect(find.text('Where'), findsOneWidget);
    expect(find.text('Filters'), findsWidgets);
    expect(find.text('Additional filters'), findsOneWidget);
    expect(find.text('Sort'), findsOneWidget);
    expect(find.text('All categories'), findsOneWidget);
    expect(find.text('Event date'), findsOneWidget); // says what the calendar filters
    expect(find.text('Any dates'), findsOneWidget);
    expect(find.widgetWithText(ChoiceChip, 'Free'), findsOneWidget);
  });

  testWidgets('no overflow when the keyboard takes half the screen', (tester) async {
    // A small phone, then the Android keyboard on top of it. The old fixed Column header above an
    // Expanded list could not shrink, so this configuration produced "Bottom overflowed by N
    // pixels"; one scroll view simply scrolls instead.
    tester.view.physicalSize = const Size(1080, 1920);
    tester.view.devicePixelRatio = 3.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(_wrap(_FakeRepo(results: const [
      EventSummary(id: '1', title: 'Sunburn Arena', slug: 'sunburn', status: 'published'),
    ])));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(OutlinedButton, 'Filters'));
    await tester.pumpAndSettle();

    // Every filter open AND the keyboard up — the worst case for vertical space.
    tester.view.viewInsets = const FakeViewPadding(bottom: 1000);
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);

    await tester.tap(find.byType(TextField).first);
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);

    // Still scrollable with the keyboard up, so the fields below the fold remain reachable.
    await tester.drag(find.byType(CustomScrollView), const Offset(0, -200));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });

  testWidgets('zero results offers a way out of the filters', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo(results: const [])));
    await tester.pumpAndSettle();

    // Nothing applied yet, so this is "nothing here", not "your filters are too narrow" — and
    // there is nothing to clear.
    expect(find.text('No events yet'), findsOneWidget);
    expect(find.text('Clear filters'), findsNothing);
  });

  testWidgets('shows the empty state when nothing matches', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo(results: const [])));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'nothing like this exists');
    await tester.pump(const Duration(milliseconds: 400)); // past the search debounce
    await tester.pumpAndSettle();

    // Filters ARE responsible for the empty result here, so say so and offer the undo.
    expect(find.text('No events match your filters'), findsOneWidget);
    expect(find.text('Clear filters'), findsOneWidget);

    await tester.tap(find.text('Clear filters'));
    await tester.pumpAndSettle();
    expect(find.text('No events yet'), findsOneWidget); // back to unfiltered
  });

  test('the picked date range reaches the API as an inclusive event-date filter', () {
    // SearchService applies `StartsAt >= dateFrom AND StartsAt <= dateTo`, so the end day has to be
    // widened past midnight or an event at 18:00 on the last day of the range is dropped.
    final end = EventSearchController.endOfDay(DateTime(2026, 8, 11));
    expect(end, DateTime(2026, 8, 11, 23, 59, 59, 999));

    final params = EventQuery(dateFrom: DateTime(2026, 8, 7), dateTo: end).toQueryParameters();
    expect(params['dateFrom'], DateTime(2026, 8, 7).toUtc().toIso8601String());
    expect(params['dateTo'], end.toUtc().toIso8601String());

    // "Any dates" must send no date keys at all, not a wide-open default range.
    expect(const EventQuery().toQueryParameters().containsKey('dateFrom'), isFalse);
    expect(const EventQuery().toQueryParameters().containsKey('dateTo'), isFalse);
    // Same for the other "Any …" options.
    expect(const EventQuery().toQueryParameters().containsKey('mode'), isFalse);
    expect(const EventQuery().toQueryParameters().containsKey('price'), isFalse);
    expect(const EventQuery().toQueryParameters().containsKey('sort'), isFalse);
  });

  testWidgets('shows error + retry on failure', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo(fail: true)));
    await tester.pumpAndSettle();
    expect(find.text('Retry'), findsOneWidget);
    expect(find.textContaining('reach Kurx'), findsOneWidget);
  });
}
