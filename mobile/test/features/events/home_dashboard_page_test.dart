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
import 'package:kurx_mobile/features/events/presentation/pages/home_dashboard_page.dart';
import 'package:kurx_mobile/features/events/presentation/providers/events_providers.dart';
import 'package:kurx_mobile/features/events/presentation/widgets/section_header.dart';

class _FakeRepo implements EventsRepository {
  _FakeRepo({this.empty = false, this.fail = false});
  final bool empty;
  final bool fail;

  List<EventSummary> _list(String tag) => empty
      ? const []
      : [EventSummary(id: tag, title: 'Event $tag', slug: 's-$tag', city: 'Mumbai', status: 'published')];

  @override
  Future<List<EventSummary>> featured({int limit = 20}) async => _guard(() => _list('feat'));
  @override
  Future<List<EventSummary>> trending({int limit = 20}) async => _guard(() => _list('trend'));
  @override
  Future<List<EventSummary>> upcoming({int limit = 20}) async => _guard(() => _list('up'));
  @override
  Future<List<EventSummary>> latest({int limit = 20}) async => _guard(() => _list('late'));
  @override
  Future<List<EventCategory>> categories() async =>
      _guard(() => empty ? const <EventCategory>[] : const [EventCategory(id: 'c1', name: 'Music')]);
  @override
  Future<List<EventCategory>> eventTypes() async => _guard(() => const <EventCategory>[]);
  @override
  Future<List<EventKind>> kinds() async => _guard(() => const <EventKind>[]);
  @override
  Future<List<EventSummary>> forYou({int limit = 10}) async => _guard(() => const <EventSummary>[]);

  T _guard<T>(T Function() f) {
    if (fail) throw const ApiError(status: 0, code: 'network_error');
    return f();
  }

  @override
  Future<PagedEvents> search(EventQuery query) => throw UnimplementedError();
  @override
  Future<EventDetail> detail(String slug) => throw UnimplementedError();
  @override
  Future<List<EventSummary>> related(String slug, {int limit = 6}) => throw UnimplementedError();
  @override
  Future<List<TicketType>> ticketTypes(String eventId) => throw UnimplementedError();
}

Widget _wrap(EventsRepository repo) => ProviderScope(
      overrides: [eventsRepositoryProvider.overrideWithValue(repo)],
      child: const MaterialApp(home: HomeDashboardPage()),
    );

void main() {
  setUpAll(() async => initializeDateFormatting('en_IN'));

  testWidgets('renders discovery sections and the search bar', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo()));
    await tester.pumpAndSettle();

    expect(find.text('Search events'), findsOneWidget);
    expect(find.text('Featured'), findsOneWidget);
    // The trending rail also stamps a "Trending" badge onto its card (CompactEventCard's `badge`
    // tag), so a bare find.text('Trending') matches both the section heading and the card badge.
    // Scope to the SectionHeader specifically — that's what "renders discovery sections" means here.
    expect(
      find.descendant(of: find.byType(SectionHeader), matching: find.text('Trending')),
      findsOneWidget,
    );
    expect(find.text('Event trend'), findsOneWidget); // a trending card rendered
    expect(find.text('View all'), findsWidgets); // section view-all actions
  });

  testWidgets('shows the empty discovery state', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo(empty: true)));
    await tester.pumpAndSettle();
    expect(find.text('No events to discover yet'), findsOneWidget);
  });

  testWidgets('shows error + retry when the feed fails', (tester) async {
    await tester.pumpWidget(_wrap(_FakeRepo(fail: true)));
    await tester.pumpAndSettle();
    expect(find.text('Retry'), findsOneWidget);
    expect(find.textContaining('reach Kurx'), findsOneWidget);
  });
}
