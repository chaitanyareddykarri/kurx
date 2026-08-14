import 'package:dio/dio.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/network/api_guard.dart';

part 'notifications_remote_data_source.freezed.dart';
part 'notifications_remote_data_source.g.dart';

/// `MeNotificationEndpoints.ToJson`.
@freezed
class NotificationDto with _$NotificationDto {
  const factory NotificationDto({
    required String id,
    @Default('') String kind,
    @Default('') String title,
    @Default('') String body,
    /// Payload for deep-linking, mirroring the push `data` map.
    @JsonKey(name: 'data_json') String? dataJson,
    @JsonKey(name: 'read_at') DateTime? readAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _NotificationDto;

  factory NotificationDto.fromJson(Map<String, dynamic> json) => _$NotificationDtoFromJson(json);
}

/// The `{ items, unread_count }` envelope.
@freezed
class NotificationPageDto with _$NotificationPageDto {
  const factory NotificationPageDto({
    @Default(<NotificationDto>[]) List<NotificationDto> items,
    @Default(0) int unreadCount,
  }) = _NotificationPageDto;

  factory NotificationPageDto.fromJson(Map<String, dynamic> json) => NotificationPageDto(
        items: (json['items'] as List? ?? const [])
            .cast<Map<String, dynamic>>()
            .map(NotificationDto.fromJson)
            .toList(),
        unreadCount: (json['unread_count'] as num?)?.toInt() ?? 0,
      );
}

/// In-app notifications — `/v1/me/notifications` (A6/D-064).
///
/// This closes a **stale client comment**, not a new backend feature: the provider previously
/// returned a hardcoded `[]` explaining that "no notifications endpoint exists yet — backend scope
/// excludes it (D-019)". That endpoint shipped as part of D-064 over the previously-dead
/// `NotificationService`, so the feed and unread badge were permanently empty for no reason.
class NotificationsRemoteDataSource {
  NotificationsRemoteDataSource(this._dio);
  final Dio _dio;

  Future<NotificationPageDto> list({int page = 1, int pageSize = 30}) => guard(
        () async {
          final res = await _dio.get(
            '/v1/me/notifications',
            queryParameters: {'page': page, 'pageSize': pageSize},
          );
          return NotificationPageDto.fromJson((res.data as Map).cast<String, dynamic>());
        },
        endpoint: 'GET /v1/me/notifications',
      );

  Future<void> markRead(String id) => guard(
        () => _dio.post('/v1/me/notifications/$id/read'),
        endpoint: 'POST /v1/me/notifications/{id}/read',
      );

  Future<void> markAllRead() => guard(
        () => _dio.post('/v1/me/notifications/read-all'),
        endpoint: 'POST /v1/me/notifications/read-all',
      );
}
