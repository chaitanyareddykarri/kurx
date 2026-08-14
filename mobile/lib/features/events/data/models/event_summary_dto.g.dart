// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'event_summary_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$EventSummaryDtoImpl _$$EventSummaryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventSummaryDtoImpl(
  id: json['id'] as String,
  title: json['title'] as String,
  slug: json['slug'] as String,
  subtitle: json['subtitle'] as String?,
  venueName: json['venue_name'] as String?,
  city: json['city'] as String?,
  startsAt: json['starts_at'] == null
      ? null
      : DateTime.parse(json['starts_at'] as String),
  endsAt: json['ends_at'] == null
      ? null
      : DateTime.parse(json['ends_at'] as String),
  status: json['status'] as String? ?? '',
  bannerUrl: json['banner_url'] as String?,
  eventMode: json['event_mode'] as String?,
  categoryName: json['category_name'] as String?,
  priceFromPaise: (json['price_from_paise'] as num?)?.toInt(),
  currency: json['currency'] as String?,
  isFeatured: json['is_featured'] as bool?,
);

Map<String, dynamic> _$$EventSummaryDtoImplToJson(
  _$EventSummaryDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'slug': instance.slug,
  'subtitle': instance.subtitle,
  'venue_name': instance.venueName,
  'city': instance.city,
  'starts_at': instance.startsAt?.toIso8601String(),
  'ends_at': instance.endsAt?.toIso8601String(),
  'status': instance.status,
  'banner_url': instance.bannerUrl,
  'event_mode': instance.eventMode,
  'category_name': instance.categoryName,
  'price_from_paise': instance.priceFromPaise,
  'currency': instance.currency,
  'is_featured': instance.isFeatured,
};
