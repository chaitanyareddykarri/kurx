import '../../../../core/network/api_error.dart';
import '../../../../core/storage/event_cache.dart';
import '../../domain/entities/event_category.dart';
import '../../domain/entities/event_detail.dart';
import '../../domain/entities/event_kind.dart';
import '../../domain/entities/event_query.dart';
import '../../domain/entities/event_summary.dart';
import '../../domain/entities/paged_events.dart';
import '../../domain/entities/ticket_type.dart';
import '../../domain/repositories/events_repository.dart';
import '../datasources/events_remote_data_source.dart';
import '../models/event_summary_dto.dart';

class EventsRepositoryImpl implements EventsRepository {
  EventsRepositoryImpl(this._remote, this._cache);

  final EventsRemoteDataSource _remote;
  final EventCache _cache;

  @override
  Future<List<EventSummary>> upcoming({int limit = 20}) async {
    try {
      final dtos = await _remote.upcoming(limit: limit);
      await _cache.saveUpcoming(dtos.map((d) => d.toJson()).toList());
      return dtos.map((d) => d.toEntity()).toList();
    } on ApiError catch (e) {
      // Offline/timeout: fall back to the last cached list if we genuinely have one. A cache
      // miss (empty list, not null — see EventCache.readUpcoming) must still surface as an
      // error so the UI can show "you're offline" rather than a false "no upcoming events".
      if (e.status == 0) {
        final cached = _cache.readUpcoming();
        if (cached.isNotEmpty) {
          return cached
              .cast<Map<String, dynamic>>()
              .map(EventSummaryDto.fromJson)
              .map((d) => d.toEntity())
              .toList();
        }
      }
      rethrow;
    }
  }

  @override
  Future<List<EventSummary>> featured({int limit = 20}) async =>
      (await _remote.featured(limit: limit)).map((d) => d.toEntity()).toList();

  @override
  Future<List<EventSummary>> trending({int limit = 20}) async =>
      (await _remote.trending(limit: limit)).map((d) => d.toEntity()).toList();

  @override
  Future<List<EventSummary>> latest({int limit = 20}) async =>
      (await _remote.latest(limit: limit)).map((d) => d.toEntity()).toList();

  @override
  Future<PagedEvents> search(EventQuery query) async {
    final page = await _remote.search(query);
    return PagedEvents(items: page.items.map((d) => d.toEntity()).toList(), total: page.total);
  }

  @override
  Future<List<EventCategory>> categories() async {
    final dtos = await _remote.categories();
    return dtos
        .where((d) => d.isVisible && d.level == 'category')
        .map((d) => d.toEntity())
        .toList();
  }

  @override
  Future<List<EventCategory>> eventTypes() async {
    final dtos = await _remote.categories();
    return dtos
        .where((d) => d.isVisible && d.level == 'type')
        .map((d) => d.toEntity())
        .toList();
  }

  @override
  Future<List<EventKind>> kinds() async =>
      (await _remote.kinds()).map((d) => d.toEntity()).toList();

  @override
  Future<List<EventSummary>> forYou({int limit = 10}) async =>
      (await _remote.forYou(limit: limit)).map((d) => d.toEntity()).toList();

  @override
  Future<EventDetail> detail(String slug) async => (await _remote.detail(slug)).toEntity();

  @override
  Future<List<EventSummary>> related(String slug, {int limit = 6}) async =>
      (await _remote.related(slug, limit: limit)).map((d) => d.toEntity()).toList();

  @override
  Future<List<TicketType>> ticketTypes(String eventId) async =>
      (await _remote.ticketTypes(eventId)).map((d) => d.toEntity()).toList();
}
