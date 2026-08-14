import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';
import '../../domain/entities/event_category.dart';
import '../../domain/entities/event_kind.dart';
import '../../domain/entities/event_query.dart';
import '../../domain/entities/event_summary.dart';
import '../../domain/usecases/get_categories.dart';
import '../../domain/usecases/get_event_types.dart';
import '../../domain/usecases/get_kinds.dart';
import '../../domain/usecases/search_events.dart';
import 'events_providers.dart';

final _searchEventsProvider = Provider((ref) => SearchEvents(ref.watch(eventsRepositoryProvider)));
final _getCategoriesProvider = Provider((ref) => GetCategories(ref.watch(eventsRepositoryProvider)));
final _getKindsProvider = Provider((ref) => GetKinds(ref.watch(eventsRepositoryProvider)));
final _getEventTypesProvider =
    Provider((ref) => GetEventTypes(ref.watch(eventsRepositoryProvider)));

/// Visible categories for the filter dropdown.
final categoriesProvider = FutureProvider<List<EventCategory>>(
  (ref) => ref.watch(_getCategoriesProvider).call(),
);

/// Taxonomy TYPES, for the create-event type step. Reference data like [categoriesProvider]; the same
/// `/v1/categories` payload, filtered to the other level rather than fetched twice.
final eventTypesProvider = FutureProvider<List<EventCategory>>(
  (ref) => ref.watch(_getEventTypesProvider).call(),
);

/// V3 Kind registry (Phase 16) for the Kind filter/quick-browse rail — reference data, cached for the
/// provider's lifetime same as [categoriesProvider] (not `.autoDispose`: it rarely changes).
final kindsProvider = FutureProvider<List<EventKind>>(
  (ref) => ref.watch(_getKindsProvider).call(),
);

enum SearchStatus { loading, success, error }

class EventSearchState {
  const EventSearchState({
    this.query = const EventQuery(),
    this.items = const [],
    this.total = 0,
    this.status = SearchStatus.loading,
    this.loadingMore = false,
    this.loadMoreFailed = false,
    this.error,
  });

  final EventQuery query;
  final List<EventSummary> items;
  final int total;
  final SearchStatus status;
  final bool loadingMore;
  final bool loadMoreFailed;
  final ApiError? error;

  bool get hasMore => items.length < total;

  EventSearchState copyWith({
    EventQuery? query,
    List<EventSummary>? items,
    int? total,
    SearchStatus? status,
    bool? loadingMore,
    bool? loadMoreFailed,
    ApiError? error,
  }) =>
      EventSearchState(
        query: query ?? this.query,
        items: items ?? this.items,
        total: total ?? this.total,
        status: status ?? this.status,
        loadingMore: loadingMore ?? this.loadingMore,
        loadMoreFailed: loadMoreFailed ?? this.loadMoreFailed,
        error: error,
      );
}

/// Owns the search query, result accumulation, and pagination. Filter changes reset
/// to page 1; `loadMore` appends the next page while there are more matches.
class EventSearchController extends Notifier<EventSearchState> {
  @override
  EventSearchState build() {
    Future.microtask(() => _run(reset: true));
    return const EventSearchState();
  }

  SearchEvents get _search => ref.read(_searchEventsProvider);

  Future<void> _run({required bool reset}) async {
    if (reset) {
      state = state.copyWith(
        status: SearchStatus.loading,
        loadMoreFailed: false,
        query: state.query.copyWith(page: 1),
      );
    } else {
      if (state.loadingMore || !state.hasMore) return;
      state = state.copyWith(
        loadingMore: true,
        loadMoreFailed: false,
        query: state.query.copyWith(page: state.query.page + 1),
      );
    }

    try {
      final result = await _search.call(state.query);
      final items = reset ? result.items : [...state.items, ...result.items];
      state = state.copyWith(
        items: items,
        total: result.total,
        status: SearchStatus.success,
        loadingMore: false,
      );
    } on ApiError catch (e) {
      if (reset) {
        state = state.copyWith(status: SearchStatus.error, error: e);
      } else {
        // Keep what we have; surface a footer retry and roll the page number back.
        state = state.copyWith(
          loadingMore: false,
          loadMoreFailed: true,
          query: state.query.copyWith(page: state.query.page - 1),
        );
      }
    }
  }

  void _applyAndReload(EventQuery next) {
    state = state.copyWith(query: next);
    _run(reset: true);
  }

  void setSearchText(String q) => _applyAndReload(state.query.copyWith(q: q));
  void setCity(String? city) =>
      _applyAndReload(state.query.copyWith(city: (city == null || city.trim().isEmpty) ? null : city.trim()));
  void setCategory(String? categoryId) => _applyAndReload(state.query.copyWith(categoryId: categoryId));
  void setSort(String? sort) => _applyAndReload(state.query.copyWith(sort: sort));
  /// The event-date filter: `GET /v1/events?dateFrom=&dateTo=`, which `SearchService` applies as
  /// `StartsAt >= dateFrom AND StartsAt <= dateTo` — an event matches when it *starts* inside the
  /// range, both bounds inclusive.
  ///
  /// [to] is widened to the last instant of its day because the picker hands back whole days at
  /// midnight. Sending 11 Aug 00:00 asked the backend for events starting at or before midnight,
  /// which silently excluded everything actually happening on the 11th — the end date the user
  /// picked was inclusive on screen and exclusive in the query.
  void setDateRange(DateTime? from, DateTime? to) => _applyAndReload(
        state.query.copyWith(dateFrom: from, dateTo: to == null ? null : endOfDay(to)),
      );

  @visibleForTesting
  static DateTime endOfDay(DateTime d) => DateTime(d.year, d.month, d.day, 23, 59, 59, 999);
  void setKind(String? kind) => _applyAndReload(state.query.copyWith(kind: kind));
  void setMode(String? mode) => _applyAndReload(state.query.copyWith(mode: mode));
  void setPrice(String? price) => _applyAndReload(state.query.copyWith(price: price));

  void clearFilters() => _applyAndReload(const EventQuery());

  Future<void> loadMore() => _run(reset: false);
  Future<void> retry() => _run(reset: true);
}

final eventSearchControllerProvider =
    NotifierProvider<EventSearchController, EventSearchState>(EventSearchController.new);
