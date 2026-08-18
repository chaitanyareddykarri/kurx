// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'event_content_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$SpeakerDtoImpl _$$SpeakerDtoImplFromJson(Map<String, dynamic> json) =>
    _$SpeakerDtoImpl(
      id: json['id'] as String,
      orgId: json['org_id'] as String?,
      name: json['name'] as String,
      bio: json['bio'] as String?,
      photoKey: json['photo_key'] as String?,
      company: json['company'] as String?,
      role: json['role'] as String?,
      socialLinksJson: json['social_links_json'] as String?,
      userId: json['user_id'] as String?,
      username: json['username'] as String?,
      avatarKey: json['avatar_key'] as String?,
    );

Map<String, dynamic> _$$SpeakerDtoImplToJson(_$SpeakerDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'org_id': instance.orgId,
      'name': instance.name,
      'bio': instance.bio,
      'photo_key': instance.photoKey,
      'company': instance.company,
      'role': instance.role,
      'social_links_json': instance.socialLinksJson,
      'user_id': instance.userId,
      'username': instance.username,
      'avatar_key': instance.avatarKey,
    };

_$SponsorDtoImpl _$$SponsorDtoImplFromJson(Map<String, dynamic> json) =>
    _$SponsorDtoImpl(
      id: json['id'] as String,
      orgId: json['org_id'] as String?,
      name: json['name'] as String,
      logoKey: json['logo_key'] as String?,
      website: json['website'] as String?,
      tier: json['tier'] as String?,
      priority: (json['priority'] as num?)?.toInt(),
    );

Map<String, dynamic> _$$SponsorDtoImplToJson(_$SponsorDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'org_id': instance.orgId,
      'name': instance.name,
      'logo_key': instance.logoKey,
      'website': instance.website,
      'tier': instance.tier,
      'priority': instance.priority,
    };

_$VenueDtoImpl _$$VenueDtoImplFromJson(Map<String, dynamic> json) =>
    _$VenueDtoImpl(
      id: json['id'] as String,
      orgId: json['org_id'] as String?,
      name: json['name'] as String,
      address: json['address'] as String?,
      city: json['city'] as String?,
      lat: (json['lat'] as num?)?.toDouble(),
      lng: (json['lng'] as num?)?.toDouble(),
      googleMapsUrl: json['google_maps_url'] as String?,
      capacity: (json['capacity'] as num?)?.toInt(),
      hasParking: json['has_parking'] as bool? ?? false,
      isAccessible: json['is_accessible'] as bool? ?? false,
      notes: json['notes'] as String?,
      imageKeys:
          (json['image_keys'] as List<dynamic>?)
              ?.map((e) => e as String)
              .toList() ??
          const <String>[],
    );

Map<String, dynamic> _$$VenueDtoImplToJson(_$VenueDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'org_id': instance.orgId,
      'name': instance.name,
      'address': instance.address,
      'city': instance.city,
      'lat': instance.lat,
      'lng': instance.lng,
      'google_maps_url': instance.googleMapsUrl,
      'capacity': instance.capacity,
      'has_parking': instance.hasParking,
      'is_accessible': instance.isAccessible,
      'notes': instance.notes,
      'image_keys': instance.imageKeys,
    };

_$SessionDtoImpl _$$SessionDtoImplFromJson(Map<String, dynamic> json) =>
    _$SessionDtoImpl(
      id: json['id'] as String,
      eventId: json['event_id'] as String?,
      title: json['title'] as String,
      description: json['description'] as String?,
      kind: json['kind'] as String? ?? 'session',
      startsAt: DateTime.parse(json['starts_at'] as String),
      endsAt: DateTime.parse(json['ends_at'] as String),
      sort: (json['sort'] as num?)?.toInt() ?? 0,
      speakerIds:
          (json['speaker_ids'] as List<dynamic>?)
              ?.map((e) => e as String)
              .toList() ??
          const <String>[],
    );

Map<String, dynamic> _$$SessionDtoImplToJson(_$SessionDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'event_id': instance.eventId,
      'title': instance.title,
      'description': instance.description,
      'kind': instance.kind,
      'starts_at': instance.startsAt.toIso8601String(),
      'ends_at': instance.endsAt.toIso8601String(),
      'sort': instance.sort,
      'speaker_ids': instance.speakerIds,
    };

_$PresignDtoImpl _$$PresignDtoImplFromJson(Map<String, dynamic> json) =>
    _$PresignDtoImpl(
      key: json['key'] as String,
      url: json['url'] as String,
      headers:
          (json['headers'] as Map<String, dynamic>?)?.map(
            (k, e) => MapEntry(k, e as String),
          ) ??
          const <String, String>{},
    );

Map<String, dynamic> _$$PresignDtoImplToJson(_$PresignDtoImpl instance) =>
    <String, dynamic>{
      'key': instance.key,
      'url': instance.url,
      'headers': instance.headers,
    };

_$EventMediaDtoImpl _$$EventMediaDtoImplFromJson(Map<String, dynamic> json) =>
    _$EventMediaDtoImpl(
      id: json['id'] as String,
      kind: json['kind'] as String? ?? 'gallery',
      key: json['key'] as String,
      caption: json['caption'] as String?,
      sort: (json['sort'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$$EventMediaDtoImplToJson(_$EventMediaDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'kind': instance.kind,
      'key': instance.key,
      'caption': instance.caption,
      'sort': instance.sort,
    };

_$CertificateRosterDtoImpl _$$CertificateRosterDtoImplFromJson(
  Map<String, dynamic> json,
) => _$CertificateRosterDtoImpl(
  id: json['id'] as String,
  eventId: json['event_id'] as String?,
  verifyCode: json['verify_code'] as String?,
  userId: json['user_id'] as String?,
  holderName: json['holder_name'] as String?,
  kind: json['kind'] as String?,
  status: json['status'] as String?,
  isRevoked: json['is_revoked'] as bool? ?? false,
  revokedReason: json['revoked_reason'] as String?,
  issuedAt: json['issued_at'] == null
      ? null
      : DateTime.parse(json['issued_at'] as String),
);

Map<String, dynamic> _$$CertificateRosterDtoImplToJson(
  _$CertificateRosterDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'event_id': instance.eventId,
  'verify_code': instance.verifyCode,
  'user_id': instance.userId,
  'holder_name': instance.holderName,
  'kind': instance.kind,
  'status': instance.status,
  'is_revoked': instance.isRevoked,
  'revoked_reason': instance.revokedReason,
  'issued_at': instance.issuedAt?.toIso8601String(),
};

_$MembershipClaimDtoImpl _$$MembershipClaimDtoImplFromJson(
  Map<String, dynamic> json,
) => _$MembershipClaimDtoImpl(
  id: json['id'] as String,
  orgId: json['org_id'] as String,
  orgName: json['org_name'] as String?,
  orgSlug: json['org_slug'] as String?,
  claimedRole: json['claimed_role'] as String? ?? 'staff',
  status: json['status'] as String? ?? 'submitted',
  fastTrack: json['fast_track'] as bool? ?? false,
  validUntil: json['valid_until'] == null
      ? null
      : DateTime.parse(json['valid_until'] as String),
  reviewedAt: json['reviewed_at'] == null
      ? null
      : DateTime.parse(json['reviewed_at'] as String),
  notes: json['notes'] as String?,
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$MembershipClaimDtoImplToJson(
  _$MembershipClaimDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'org_id': instance.orgId,
  'org_name': instance.orgName,
  'org_slug': instance.orgSlug,
  'claimed_role': instance.claimedRole,
  'status': instance.status,
  'fast_track': instance.fastTrack,
  'valid_until': instance.validUntil?.toIso8601String(),
  'reviewed_at': instance.reviewedAt?.toIso8601String(),
  'notes': instance.notes,
  'created_at': instance.createdAt?.toIso8601String(),
};

_$EventAuthorizationDtoImpl _$$EventAuthorizationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventAuthorizationDtoImpl(
  eventId: json['event_id'] as String,
  headName: json['head_name'] as String? ?? '',
  headDesignation: json['head_designation'] as String? ?? '',
  officialEmail: json['official_email'] as String? ?? '',
  officialPhone: json['official_phone'] as String?,
  representativeRole: json['representative_role'] as String? ?? '',
  representativeRoleOther: json['representative_role_other'] as String?,
  representativeUsername: json['representative_username'] as String?,
  reviewerName: json['reviewer_name'] as String?,
  letterheadUrl: json['letterhead_url'] as String?,
  status: json['status'] as String? ?? 'Submitted',
  reasonCode: json['reason_code'] as String?,
  notes: json['notes'] as String?,
  updatedAt: json['updated_at'] == null
      ? null
      : DateTime.parse(json['updated_at'] as String),
);

Map<String, dynamic> _$$EventAuthorizationDtoImplToJson(
  _$EventAuthorizationDtoImpl instance,
) => <String, dynamic>{
  'event_id': instance.eventId,
  'head_name': instance.headName,
  'head_designation': instance.headDesignation,
  'official_email': instance.officialEmail,
  'official_phone': instance.officialPhone,
  'representative_role': instance.representativeRole,
  'representative_role_other': instance.representativeRoleOther,
  'representative_username': instance.representativeUsername,
  'reviewer_name': instance.reviewerName,
  'letterhead_url': instance.letterheadUrl,
  'status': instance.status,
  'reason_code': instance.reasonCode,
  'notes': instance.notes,
  'updated_at': instance.updatedAt?.toIso8601String(),
};
