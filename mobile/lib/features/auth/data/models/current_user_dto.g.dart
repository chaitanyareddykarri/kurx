// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'current_user_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$CurrentUserDtoImpl _$$CurrentUserDtoImplFromJson(
  Map<String, dynamic> json,
) => _$CurrentUserDtoImpl(
  id: json['id'] as String,
  phone: json['phone'] as String,
  name: json['name'] as String? ?? '',
  username: json['username'] as String?,
  email: json['email'] as String?,
  emailVerified: json['email_verified'] as bool? ?? false,
  needsOnboarding: json['needs_onboarding'] as bool? ?? false,
  dateOfBirth: json['date_of_birth'] == null
      ? null
      : DateTime.parse(json['date_of_birth'] as String),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
  headline: json['headline'] as String?,
  bio: json['bio'] as String?,
  skills:
      (json['skills'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const <String>[],
  languages:
      (json['languages'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const <String>[],
  interests:
      (json['interests'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const <String>[],
  educationJson: json['education_json'] as String?,
  linksJson: json['links_json'] as String?,
  avatarKey: json['avatar_key'] as String?,
  coverKey: json['cover_key'] as String?,
  privacy: json['privacy'] == null
      ? null
      : ProfilePrivacyDto.fromJson(json['privacy'] as Map<String, dynamic>),
  trust: json['trust'] == null
      ? null
      : TrustCapabilitiesDto.fromJson(json['trust'] as Map<String, dynamic>),
);

Map<String, dynamic> _$$CurrentUserDtoImplToJson(
  _$CurrentUserDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'phone': instance.phone,
  'name': instance.name,
  'username': instance.username,
  'email': instance.email,
  'email_verified': instance.emailVerified,
  'needs_onboarding': instance.needsOnboarding,
  'date_of_birth': instance.dateOfBirth?.toIso8601String(),
  'created_at': instance.createdAt?.toIso8601String(),
  'headline': instance.headline,
  'bio': instance.bio,
  'skills': instance.skills,
  'languages': instance.languages,
  'interests': instance.interests,
  'education_json': instance.educationJson,
  'links_json': instance.linksJson,
  'avatar_key': instance.avatarKey,
  'cover_key': instance.coverKey,
  'privacy': instance.privacy,
  'trust': instance.trust,
};

_$TrustCapabilitiesDtoImpl _$$TrustCapabilitiesDtoImplFromJson(
  Map<String, dynamic> json,
) => _$TrustCapabilitiesDtoImpl(
  level: json['level'] as String? ?? 'L1',
  canOrganizeFree: json['can_organize_free'] as bool? ?? true,
  canOrganizePaid: json['can_organize_paid'] as bool? ?? false,
  canReceivePayout: json['can_receive_payout'] as bool? ?? false,
  identityVerified: json['identity_verified'] as bool? ?? false,
  bankVerified: json['bank_verified'] as bool? ?? false,
  canCreatePublicEvent: json['can_create_public_event'] as bool? ?? false,
  canCreatePrivateEvent: json['can_create_private_event'] as bool? ?? true,
);

Map<String, dynamic> _$$TrustCapabilitiesDtoImplToJson(
  _$TrustCapabilitiesDtoImpl instance,
) => <String, dynamic>{
  'level': instance.level,
  'can_organize_free': instance.canOrganizeFree,
  'can_organize_paid': instance.canOrganizePaid,
  'can_receive_payout': instance.canReceivePayout,
  'identity_verified': instance.identityVerified,
  'bank_verified': instance.bankVerified,
  'can_create_public_event': instance.canCreatePublicEvent,
  'can_create_private_event': instance.canCreatePrivateEvent,
};

_$ProfilePrivacyDtoImpl _$$ProfilePrivacyDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ProfilePrivacyDtoImpl(
  profilePublic: json['profile_public'] as bool? ?? true,
  showAttended: json['show_attended'] as bool? ?? false,
  showCertificates: json['show_certificates'] as bool? ?? true,
  showAllies: json['show_allies'] as bool? ?? true,
  sections:
      (json['sections'] as Map<String, dynamic>?)?.map(
        (k, e) => MapEntry(k, e as String),
      ) ??
      const <String, String>{},
);

Map<String, dynamic> _$$ProfilePrivacyDtoImplToJson(
  _$ProfilePrivacyDtoImpl instance,
) => <String, dynamic>{
  'profile_public': instance.profilePublic,
  'show_attended': instance.showAttended,
  'show_certificates': instance.showCertificates,
  'show_allies': instance.showAllies,
  'sections': instance.sections,
};
