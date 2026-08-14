import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_category.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_detail.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_kind.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_query.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_summary.dart';
import 'package:kurx_mobile/features/events/domain/entities/paged_events.dart';
import 'package:kurx_mobile/features/events/domain/entities/ticket_type.dart';
import 'package:kurx_mobile/features/events/domain/repositories/events_repository.dart';
import 'package:kurx_mobile/features/events/presentation/providers/events_providers.dart';
import 'package:kurx_mobile/features/events/presentation/providers/search_providers.dart';

/// Fake that pages a fixed corpus of [total] events, and can be told to fail.
class _PagingRepo implements EventsRepository {
  _PagingRepo({this.total = 25});
  final int total;
  bool fail = false;
  int searchCalls = 0;
  EventQuery? lastQuery;

  @override
  Future<PagedEvents> search(EventQuery query) async {
    searchCalls++;
    lastQuery = query;
    if (fail) throw const ApiError(status: 0, code: 'network_error');
    final start = (query.page - 1) * query.pageSize;
    final count = (total - start).clamp(0, query.pageSize);
    final items = List.generate(
      count,
      (i) => EventSummary(id: '${start + i}', title: 'Event ${start + i}', slug: 's${start + i}', status: 'published'),
    );
    return PagedEvents(items: items, total: total);
  }

  @override
  Future<List<EventCategory>> categories() async => const [];
  @override
  Future<List<EventCategory>> eventTypes() => throw UnimplementedError();
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

Future<void> _settle() => Future<void>.delayed(const Duration(milliseconds: 20));

void main() {
  test('initial load fills the first page; loadMore appends until total is reached', () async {
    final repo = _PagingRepo(total: 25);
    final container = ProviderContainer(overrides: [eventsRepositoryProvider.overrideWithValue(repo)]);
    addTearDown(container.dispose);

    container.read(eventSearchControllerProvider); // triggers build + initial load
    await _settle();

    var state = container.read(eventSearchControllerProvider);
    expect(state.status, SearchStatus.success);
    expect(state.items.length, 20);
    expect(state.total, 25);
    expect(state.hasMore, isTrue);

    container.read(eventSearchControllerProvider.notifier).loadMore();
    await _settle();

    state = container.read(eventSearchControllerProvider);
    expect(state.items.length, 25);
    expect(state.hasMore, isFalse);
    expect(repo.searchCalls, 2);
  });

  test('changing a filter resets to page 1 and re-queries', () async {
    final repo = _PagingRepo(total: 25);
    final container = ProviderContainer(overrides: [eventsRepositoryProvider.overrideWithValue(repo)]);
    addTearDown(container.dispose);

    container.read(eventSearchControllerProvider);
    await _settle();
    container.read(eventSearchControllerProvider.notifier).loadMore();
    await _settle();

    container.read(eventSearchControllerProvider.notifier).setSort('popular');
    await _settle();

    final state = container.read(eventSearchControllerProvider);
    expect(state.query.page, 1);
    expect(state.query.sort, 'popular');
    expect(state.items.length, 20); // reset back to a single page
    expect(repo.lastQuery!.sort, 'popular');
  });

  test('a load-more failure keeps existing items and flags a retry', () async {
    final repo = _PagingRepo(total: 25);
    final container = ProviderContainer(overrides: [eventsRepositoryProvider.overrideWithValue(repo)]);
    addTearDown(container.dispose);

    container.read(eventSearchControllerProvider);
    await _settle();
    repo.fail = true;
    container.read(eventSearchControllerProvider.notifier).loadMore();
    await _settle();

    final state = container.read(eventSearchControllerProvider);
    expect(state.status, SearchStatus.success); // not wiped
    expect(state.items.length, 20); // kept
    expect(state.loadMoreFailed, isTrue);
    expect(state.query.page, 1); // rolled back
  });
}
