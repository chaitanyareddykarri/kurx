import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../../domain/entities/event_query.dart';
import '../models/event_category_dto.dart';
import '../models/event_detail_dto.dart';
import '../models/event_summary_dto.dart';
import '../models/kind_dto.dart';
import '../models/ticket_type_dto.dart';

/// A page of raw summary DTOs plus the total match count.
class PagedSummaryDto {
  const PagedSummaryDto(this.items, this.total);
  final List<EventSummaryDto> items;
  final int total;
}

/// HTTP layer over the public event/category/kind endpoints, plus [forYou] (the one endpoint here that
/// requires auth — the shared [Dio] instance's interceptor attaches the bearer token automatically).
class EventsRemoteDataSource {
  EventsRemoteDataSource(this._dio);

  final Dio _dio;

  Future<List<EventSummaryDto>> upcoming({int limit = 20}) => _sectionList('upcoming', limit);
  Future<List<EventSummaryDto>> featured({int limit = 20}) => _sectionList('featured', limit);
  Future<List<EventSummaryDto>> trending({int limit = 20}) => _sectionList('trending', limit);
  Future<List<EventSummaryDto>> latest({int limit = 20}) => _sectionList('latest', limit);

  Future<List<EventSummaryDto>> _sectionList(String section, int limit) => guard(() async {
        final res = await _dio.get('/v1/events/$section', queryParameters: {'limit': limit});
        return _summaryList(res.data);
      });

  Future<PagedSummaryDto> search(EventQuery query) => guard(() async {
        final res = await _dio.get('/v1/events', queryParameters: query.toQueryParameters());
        final map = (res.data as Map).cast<String, dynamic>();
        final items = (map['items'] as List).cast<Map<String, dynamic>>().map(EventSummaryDto.fromJson).toList();
        return PagedSummaryDto(items, (map['total'] as num).toInt());
      });

  Future<List<EventCategoryDto>> categories() => guard(() async {
        final res = await _dio.get('/v1/categories');
        return (res.data as List).cast<Map<String, dynamic>>().map(EventCategoryDto.fromJson).toList();
      });

  /// V3 §15 (Phase 16) Kind registry — public, powers the Kind filter/quick-browse rail.
  Future<List<KindDto>> kinds() => guard(() async {
        final res = await _dio.get('/v1/kinds');
        return (res.data as List).cast<Map<String, dynamic>>().map(KindDto.fromJson).toList();
      });

  /// V3 §15 (Phase 16) eligibility-aware "events you can attend" feed (D-184). Auth required.
  Future<List<EventSummaryDto>> forYou({int limit = 10}) => guard(() async {
        final res = await _dio.get('/v1/events/for-you', queryParameters: {'limit': limit});
        return _summaryList(res.data);
      });

  Future<EventDetailDto> detail(String slug) => guard(() async {
        final res = await _dio.get('/v1/events/$slug');
        return EventDetailDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<List<EventSummaryDto>> related(String slug, {int limit = 6}) => guard(() async {
        final res = await _dio.get('/v1/events/$slug/related', queryParameters: {'limit': limit});
        return _summaryList(res.data);
      });

  Future<List<TicketTypeDto>> ticketTypes(String eventId) => guard(() async {
        final res = await _dio.get('/v1/events/$eventId/ticket-types');
        final list = (res.data as List).cast<Map<String, dynamic>>();
        return list.map(TicketTypeDto.fromJson).toList();
      });

  List<EventSummaryDto> _summaryList(Object? data) =>
      (data as List).cast<Map<String, dynamic>>().map(EventSummaryDto.fromJson).toList();
}
