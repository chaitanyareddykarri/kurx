// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'notifications_remote_data_source.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$NotificationDtoImpl _$$NotificationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$NotificationDtoImpl(
  id: json['id'] as String,
  kind: json['kind'] as String? ?? '',
  title: json['title'] as String? ?? '',
  body: json['body'] as String? ?? '',
  dataJson: json['data_json'] as String?,
  readAt: json['read_at'] == null
      ? null
      : DateTime.parse(json['read_at'] as String),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$NotificationDtoImplToJson(
  _$NotificationDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'kind': instance.kind,
  'title': instance.title,
  'body': instance.body,
  'data_json': instance.dataJson,
  'read_at': instance.readAt?.toIso8601String(),
  'created_at': instance.createdAt?.toIso8601String(),
};

_$NotificationPageDtoImpl _$$NotificationPageDtoImplFromJson(
  Map<String, dynamic> json,
) => _$NotificationPageDtoImpl(
  items:
      (json['items'] as List<dynamic>?)
          ?.map((e) => NotificationDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <NotificationDto>[],
  unreadCount: (json['unreadCount'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$$NotificationPageDtoImplToJson(
  _$NotificationPageDtoImpl instance,
) => <String, dynamic>{
  'items': instance.items,
  'unreadCount': instance.unreadCount,
};
