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

_$EventContentDtoImpl _$$EventContentDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventContentDtoImpl(
  tagline: json['tagline'] as String?,
  shortDescription: json['short_description'] as String?,
  rules: json['rules'] as String?,
);

Map<String, dynamic> _$$EventContentDtoImplToJson(
  _$EventContentDtoImpl instance,
) => <String, dynamic>{
  'tagline': instance.tagline,
  'short_description': instance.shortDescription,
  'rules': instance.rules,
};

_$EventLegalDtoImpl _$$EventLegalDtoImplFromJson(Map<String, dynamic> json) =>
    _$EventLegalDtoImpl(
      termsUrl: json['terms_url'] as String?,
      termsText: json['terms_text'] as String?,
      codeOfConduct: json['code_of_conduct'] as String?,
      refundPolicy: json['refund_policy'] as String?,
      cancellationPolicy: json['cancellation_policy'] as String?,
      requiresConsent: json['requires_consent'] as bool? ?? false,
      consentText: json['consent_text'] as String?,
    );

Map<String, dynamic> _$$EventLegalDtoImplToJson(_$EventLegalDtoImpl instance) =>
    <String, dynamic>{
      'terms_url': instance.termsUrl,
      'terms_text': instance.termsText,
      'code_of_conduct': instance.codeOfConduct,
      'refund_policy': instance.refundPolicy,
      'cancellation_policy': instance.cancellationPolicy,
      'requires_consent': instance.requiresConsent,
      'consent_text': instance.consentText,
    };

_$EventScheduleDtoImpl _$$EventScheduleDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventScheduleDtoImpl(
  registrationOpensAt: json['registration_opens_at'] == null
      ? null
      : DateTime.parse(json['registration_opens_at'] as String),
  registrationClosesAt: json['registration_closes_at'] == null
      ? null
      : DateTime.parse(json['registration_closes_at'] as String),
  checkinOpensAt: json['checkin_opens_at'] == null
      ? null
      : DateTime.parse(json['checkin_opens_at'] as String),
  checkinClosesAt: json['checkin_closes_at'] == null
      ? null
      : DateTime.parse(json['checkin_closes_at'] as String),
);

Map<String, dynamic> _$$EventScheduleDtoImplToJson(
  _$EventScheduleDtoImpl instance,
) => <String, dynamic>{
  'registration_opens_at': instance.registrationOpensAt?.toIso8601String(),
  'registration_closes_at': instance.registrationClosesAt?.toIso8601String(),
  'checkin_opens_at': instance.checkinOpensAt?.toIso8601String(),
  'checkin_closes_at': instance.checkinClosesAt?.toIso8601String(),
};

_$EventEligibilityDtoImpl _$$EventEligibilityDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventEligibilityDtoImpl(
  minAge: (json['min_age'] as num?)?.toInt(),
  maxAge: (json['max_age'] as num?)?.toInt(),
  genderRestriction: json['gender_restriction'] as String?,
  maxTeams: (json['max_teams'] as num?)?.toInt(),
);

Map<String, dynamic> _$$EventEligibilityDtoImplToJson(
  _$EventEligibilityDtoImpl instance,
) => <String, dynamic>{
  'min_age': instance.minAge,
  'max_age': instance.maxAge,
  'gender_restriction': instance.genderRestriction,
  'max_teams': instance.maxTeams,
};

_$EventLocationDetailDtoImpl _$$EventLocationDetailDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventLocationDetailDtoImpl(
  building: json['building'] as String?,
  floor: json['floor'] as String?,
  room: json['room'] as String?,
  googleMapsUrl: json['google_maps_url'] as String?,
  meetingPlatform: json['meeting_platform'] as String?,
);

Map<String, dynamic> _$$EventLocationDetailDtoImplToJson(
  _$EventLocationDetailDtoImpl instance,
) => <String, dynamic>{
  'building': instance.building,
  'floor': instance.floor,
  'room': instance.room,
  'google_maps_url': instance.googleMapsUrl,
  'meeting_platform': instance.meetingPlatform,
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
      content: json['content'] == null
          ? null
          : EventContentDto.fromJson(json['content'] as Map<String, dynamic>),
      legal: json['legal'] == null
          ? null
          : EventLegalDto.fromJson(json['legal'] as Map<String, dynamic>),
      schedule: json['schedule'] == null
          ? null
          : EventScheduleDto.fromJson(json['schedule'] as Map<String, dynamic>),
      eligibility: json['eligibility'] == null
          ? null
          : EventEligibilityDto.fromJson(
              json['eligibility'] as Map<String, dynamic>,
            ),
      locationDetail: json['location_detail'] == null
          ? null
          : EventLocationDetailDto.fromJson(
              json['location_detail'] as Map<String, dynamic>,
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
  'content': instance.content,
  'legal': instance.legal,
  'schedule': instance.schedule,
  'eligibility': instance.eligibility,
  'location_detail': instance.locationDetail,
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
