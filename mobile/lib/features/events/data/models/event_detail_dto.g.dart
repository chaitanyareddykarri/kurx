// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'event_detail_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$VenueDtoImpl _$$VenueDtoImplFromJson(Map<String, dynamic> json) =>
    _$VenueDtoImpl(
      name: json['name'] as String?,
      address: json['address'] as String?,
      city: json['city'] as String?,
      googleMapsUrl: json['google_maps_url'] as String?,
    );

Map<String, dynamic> _$$VenueDtoImplToJson(_$VenueDtoImpl instance) =>
    <String, dynamic>{
      'name': instance.name,
      'address': instance.address,
      'city': instance.city,
      'google_maps_url': instance.googleMapsUrl,
    };

_$EventDetailDtoImpl _$$EventDetailDtoImplFromJson(Map<String, dynamic> json) =>
    _$EventDetailDtoImpl(
      id: json['id'] as String,
      title: json['title'] as String,
      slug: json['slug'] as String,
      subtitle: json['subtitle'] as String?,
      description: json['description'] as String?,
      tags:
          (json['tags'] as List<dynamic>?)?.map((e) => e as String).toList() ??
          const <String>[],
      venue: json['venue'] == null
          ? null
          : VenueDto.fromJson(json['venue'] as Map<String, dynamic>),
      startsAt: json['starts_at'] == null
          ? null
          : DateTime.parse(json['starts_at'] as String),
      endsAt: json['ends_at'] == null
          ? null
          : DateTime.parse(json['ends_at'] as String),
      status: json['status'] as String? ?? '',
      viewCount: (json['view_count'] as num?)?.toInt() ?? 0,
      bannerUrl: json['banner_url'] as String?,
      eventMode: json['event_mode'] as String?,
      onlineUrl: json['online_url'] as String?,
      media:
          (json['media'] as List<dynamic>?)
              ?.map((e) => EventMediaDto.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <EventMediaDto>[],
      representing: json['representing'] == null
          ? null
          : EventRepresentationDto.fromJson(
              json['representing'] as Map<String, dynamic>,
            ),
    );

Map<String, dynamic> _$$EventDetailDtoImplToJson(
  _$EventDetailDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'slug': instance.slug,
  'subtitle': instance.subtitle,
  'description': instance.description,
  'tags': instance.tags,
  'venue': instance.venue,
  'starts_at': instance.startsAt?.toIso8601String(),
  'ends_at': instance.endsAt?.toIso8601String(),
  'status': instance.status,
  'view_count': instance.viewCount,
  'banner_url': instance.bannerUrl,
  'event_mode': instance.eventMode,
  'online_url': instance.onlineUrl,
  'media': instance.media,
  'representing': instance.representing,
};

_$EventMediaDtoImpl _$$EventMediaDtoImplFromJson(Map<String, dynamic> json) =>
    _$EventMediaDtoImpl(
      id: json['id'] as String? ?? '',
      kind: json['kind'] as String? ?? '',
      caption: json['caption'] as String?,
      url: json['url'] as String?,
    );

Map<String, dynamic> _$$EventMediaDtoImplToJson(_$EventMediaDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'kind': instance.kind,
      'caption': instance.caption,
      'url': instance.url,
    };

_$EventRepresentationDtoImpl _$$EventRepresentationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventRepresentationDtoImpl(
  orgId: json['org_id'] as String? ?? '',
  name: json['name'] as String? ?? '',
  slug: json['slug'] as String? ?? '',
  isVerified: json['is_verified'] as bool? ?? false,
);

Map<String, dynamic> _$$EventRepresentationDtoImplToJson(
  _$EventRepresentationDtoImpl instance,
) => <String, dynamic>{
  'org_id': instance.orgId,
  'name': instance.name,
  'slug': instance.slug,
  'is_verified': instance.isVerified,
};
