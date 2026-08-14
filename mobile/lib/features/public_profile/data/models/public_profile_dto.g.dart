// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'public_profile_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$CollegeDtoImpl _$$CollegeDtoImplFromJson(Map<String, dynamic> json) =>
    _$CollegeDtoImpl(
      institute: json['institute'] as String?,
      degree: json['degree'] as String?,
      branch: json['branch'] as String?,
    );

Map<String, dynamic> _$$CollegeDtoImplToJson(_$CollegeDtoImpl instance) =>
    <String, dynamic>{
      'institute': instance.institute,
      'degree': instance.degree,
      'branch': instance.branch,
    };

_$ProfileStatsDtoImpl _$$ProfileStatsDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ProfileStatsDtoImpl(
  eventsConducted: (json['events_conducted'] as num?)?.toInt(),
  eventsAttended: (json['events_attended'] as num?)?.toInt(),
  certificatesCount: (json['certificates_count'] as num?)?.toInt(),
  participations: (json['participations'] as num?)?.toInt(),
  achievements: (json['achievements'] as num?)?.toInt(),
  allyCount: (json['ally_count'] as num?)?.toInt(),
);

Map<String, dynamic> _$$ProfileStatsDtoImplToJson(
  _$ProfileStatsDtoImpl instance,
) => <String, dynamic>{
  'events_conducted': instance.eventsConducted,
  'events_attended': instance.eventsAttended,
  'certificates_count': instance.certificatesCount,
  'participations': instance.participations,
  'achievements': instance.achievements,
  'ally_count': instance.allyCount,
};

_$VerificationBadgesDtoImpl _$$VerificationBadgesDtoImplFromJson(
  Map<String, dynamic> json,
) => _$VerificationBadgesDtoImpl(
  identityVerified: json['identity_verified'] as bool,
  verifiedMember: json['verified_member'] as bool,
  organizer: json['organizer'] as bool,
  verifiedCertificates: (json['verified_certificates'] as num?)?.toInt(),
  yearsOnPlatform: (json['years_on_platform'] as num).toInt(),
  phoneVerified: json['phone_verified'] as bool? ?? false,
  emailVerified: json['email_verified'] as bool? ?? false,
  speakerVerified: json['speaker_verified'] as bool? ?? false,
  communityVerified: json['community_verified'] as bool? ?? false,
);

Map<String, dynamic> _$$VerificationBadgesDtoImplToJson(
  _$VerificationBadgesDtoImpl instance,
) => <String, dynamic>{
  'identity_verified': instance.identityVerified,
  'verified_member': instance.verifiedMember,
  'organizer': instance.organizer,
  'verified_certificates': instance.verifiedCertificates,
  'years_on_platform': instance.yearsOnPlatform,
  'phone_verified': instance.phoneVerified,
  'email_verified': instance.emailVerified,
  'speaker_verified': instance.speakerVerified,
  'community_verified': instance.communityVerified,
};

_$EventDnaTagDtoImpl _$$EventDnaTagDtoImplFromJson(Map<String, dynamic> json) =>
    _$EventDnaTagDtoImpl(
      kind: json['kind'] as String,
      count: (json['count'] as num).toInt(),
    );

Map<String, dynamic> _$$EventDnaTagDtoImplToJson(
  _$EventDnaTagDtoImpl instance,
) => <String, dynamic>{'kind': instance.kind, 'count': instance.count};

_$AchievementCardDtoImpl _$$AchievementCardDtoImplFromJson(
  Map<String, dynamic> json,
) => _$AchievementCardDtoImpl(
  name: json['name'] as String,
  description: json['description'] as String?,
  iconKey: json['icon_key'] as String?,
  earnedAt: DateTime.parse(json['earned_at'] as String),
  source: json['source'] as String,
  eventTitle: json['event_title'] as String?,
  eventSlug: json['event_slug'] as String?,
  orgName: json['org_name'] as String?,
);

Map<String, dynamic> _$$AchievementCardDtoImplToJson(
  _$AchievementCardDtoImpl instance,
) => <String, dynamic>{
  'name': instance.name,
  'description': instance.description,
  'icon_key': instance.iconKey,
  'earned_at': instance.earnedAt.toIso8601String(),
  'source': instance.source,
  'event_title': instance.eventTitle,
  'event_slug': instance.eventSlug,
  'org_name': instance.orgName,
};

_$ProfileOrgDtoImpl _$$ProfileOrgDtoImplFromJson(Map<String, dynamic> json) =>
    _$ProfileOrgDtoImpl(
      orgId: json['org_id'] as String,
      orgName: json['org_name'] as String,
      orgSlug: json['org_slug'] as String,
      logoKey: json['logo_key'] as String?,
      roles:
          (json['roles'] as List<dynamic>?)?.map((e) => e as String).toList() ??
          const [],
      isVerified: json['is_verified'] as bool,
      joinedAt: DateTime.parse(json['joined_at'] as String),
      validUntil: json['valid_until'] == null
          ? null
          : DateTime.parse(json['valid_until'] as String),
      orgEventsConducted: (json['org_events_conducted'] as num).toInt(),
      orgCertificatesCount: (json['org_certificates_count'] as num).toInt(),
      orgAchievementsCount: (json['org_achievements_count'] as num).toInt(),
    );

Map<String, dynamic> _$$ProfileOrgDtoImplToJson(_$ProfileOrgDtoImpl instance) =>
    <String, dynamic>{
      'org_id': instance.orgId,
      'org_name': instance.orgName,
      'org_slug': instance.orgSlug,
      'logo_key': instance.logoKey,
      'roles': instance.roles,
      'is_verified': instance.isVerified,
      'joined_at': instance.joinedAt.toIso8601String(),
      'valid_until': instance.validUntil?.toIso8601String(),
      'org_events_conducted': instance.orgEventsConducted,
      'org_certificates_count': instance.orgCertificatesCount,
      'org_achievements_count': instance.orgAchievementsCount,
    };

_$ProfileAssignmentDtoImpl _$$ProfileAssignmentDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ProfileAssignmentDtoImpl(
  eventId: json['event_id'] as String,
  eventTitle: json['event_title'] as String,
  eventSlug: json['event_slug'] as String,
  bannerKey: json['banner_key'] as String?,
  role: json['role'] as String,
  status: json['status'] as String,
  completedAt: json['completed_at'] == null
      ? null
      : DateTime.parse(json['completed_at'] as String),
  startsAt: DateTime.parse(json['starts_at'] as String),
  orgName: json['org_name'] as String?,
);

Map<String, dynamic> _$$ProfileAssignmentDtoImplToJson(
  _$ProfileAssignmentDtoImpl instance,
) => <String, dynamic>{
  'event_id': instance.eventId,
  'event_title': instance.eventTitle,
  'event_slug': instance.eventSlug,
  'banner_key': instance.bannerKey,
  'role': instance.role,
  'status': instance.status,
  'completed_at': instance.completedAt?.toIso8601String(),
  'starts_at': instance.startsAt.toIso8601String(),
  'org_name': instance.orgName,
};

_$PublicProfileDtoImpl _$$PublicProfileDtoImplFromJson(
  Map<String, dynamic> json,
) => _$PublicProfileDtoImpl(
  id: json['id'] as String,
  name: json['name'] as String,
  username: json['username'] as String?,
  headline: json['headline'] as String?,
  bio: json['bio'] as String?,
  summary: json['summary'] as String,
  joinedAt: json['joined_at'] as String?,
  avatarKey: json['avatar_key'] as String?,
  coverKey: json['cover_key'] as String?,
  college: json['college'] == null
      ? null
      : CollegeDto.fromJson(json['college'] as Map<String, dynamic>),
  links: json['links_json'] as String?,
  skills:
      (json['skills'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const [],
  languages:
      (json['languages'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const [],
  interests:
      (json['interests'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const [],
  stats: ProfileStatsDto.fromJson(json['stats'] as Map<String, dynamic>),
  verification: VerificationBadgesDto.fromJson(
    json['verification'] as Map<String, dynamic>,
  ),
  eventDna:
      (json['event_dna'] as List<dynamic>?)
          ?.map((e) => EventDnaTagDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const [],
  achievements:
      (json['achievements'] as List<dynamic>?)
          ?.map((e) => AchievementCardDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const [],
  identityLabels:
      (json['identity_labels'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList() ??
      const [],
  derivedHeadline: json['derived_headline'] as String? ?? '',
  organizations:
      (json['organizations'] as List<dynamic>?)
          ?.map((e) => ProfileOrgDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const [],
);

Map<String, dynamic> _$$PublicProfileDtoImplToJson(
  _$PublicProfileDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'username': instance.username,
  'headline': instance.headline,
  'bio': instance.bio,
  'summary': instance.summary,
  'joined_at': instance.joinedAt,
  'avatar_key': instance.avatarKey,
  'cover_key': instance.coverKey,
  'college': instance.college,
  'links_json': instance.links,
  'skills': instance.skills,
  'languages': instance.languages,
  'interests': instance.interests,
  'stats': instance.stats,
  'verification': instance.verification,
  'event_dna': instance.eventDna,
  'achievements': instance.achievements,
  'identity_labels': instance.identityLabels,
  'derived_headline': instance.derivedHeadline,
  'organizations': instance.organizations,
};

_$PublicCertificateCardDtoImpl _$$PublicCertificateCardDtoImplFromJson(
  Map<String, dynamic> json,
) => _$PublicCertificateCardDtoImpl(
  id: json['id'] as String,
  eventTitle: json['event_title'] as String,
  issuedAt: DateTime.parse(json['issued_at'] as String),
  verifyCode: json['verify_code'] as String,
  isAchievement: json['is_achievement'] as bool,
);

Map<String, dynamic> _$$PublicCertificateCardDtoImplToJson(
  _$PublicCertificateCardDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'event_title': instance.eventTitle,
  'issued_at': instance.issuedAt.toIso8601String(),
  'verify_code': instance.verifyCode,
  'is_achievement': instance.isAchievement,
};

_$PublicEventCardDtoImpl _$$PublicEventCardDtoImplFromJson(
  Map<String, dynamic> json,
) => _$PublicEventCardDtoImpl(
  id: json['id'] as String,
  title: json['title'] as String,
  slug: json['slug'] as String,
  bannerKey: json['banner_key'] as String?,
  startsAt: DateTime.parse(json['starts_at'] as String),
  city: json['city'] as String,
  orgName: json['org_name'] as String,
  roles:
      (json['roles'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const [],
  visibility: json['visibility'] as String,
  certificateVerifyCode: json['certificate_verify_code'] as String?,
  isAchievement: json['is_achievement'] as bool,
);

Map<String, dynamic> _$$PublicEventCardDtoImplToJson(
  _$PublicEventCardDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'slug': instance.slug,
  'banner_key': instance.bannerKey,
  'starts_at': instance.startsAt.toIso8601String(),
  'city': instance.city,
  'org_name': instance.orgName,
  'roles': instance.roles,
  'visibility': instance.visibility,
  'certificate_verify_code': instance.certificateVerifyCode,
  'is_achievement': instance.isAchievement,
};

_$TimelineEntryDtoImpl _$$TimelineEntryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$TimelineEntryDtoImpl(
  kind: json['kind'] as String,
  title: json['title'] as String,
  slug: json['slug'] as String?,
  bannerKey: json['banner_key'] as String?,
  roles:
      (json['roles'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const [],
  orgName: json['org_name'] as String?,
  city: json['city'] as String?,
  occurredAt: DateTime.parse(json['occurred_at'] as String),
  verifyCode: json['verify_code'] as String?,
  isFirstEvent: json['is_first_event'] as bool,
);

Map<String, dynamic> _$$TimelineEntryDtoImplToJson(
  _$TimelineEntryDtoImpl instance,
) => <String, dynamic>{
  'kind': instance.kind,
  'title': instance.title,
  'slug': instance.slug,
  'banner_key': instance.bannerKey,
  'roles': instance.roles,
  'org_name': instance.orgName,
  'city': instance.city,
  'occurred_at': instance.occurredAt.toIso8601String(),
  'verify_code': instance.verifyCode,
  'is_first_event': instance.isFirstEvent,
};

_$PublicUserSearchResultDtoImpl _$$PublicUserSearchResultDtoImplFromJson(
  Map<String, dynamic> json,
) => _$PublicUserSearchResultDtoImpl(
  id: json['id'] as String,
  name: json['name'] as String,
  username: json['username'] as String,
  avatarKey: json['avatar_key'] as String?,
  headline: json['headline'] as String?,
);

Map<String, dynamic> _$$PublicUserSearchResultDtoImplToJson(
  _$PublicUserSearchResultDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'username': instance.username,
  'avatar_key': instance.avatarKey,
  'headline': instance.headline,
};

_$AllyProfileCardDtoImpl _$$AllyProfileCardDtoImplFromJson(
  Map<String, dynamic> json,
) => _$AllyProfileCardDtoImpl(
  userId: json['user_id'] as String,
  name: json['name'] as String,
  username: json['username'] as String?,
  avatarKey: json['avatar_key'] as String?,
  mutualEventCount: (json['mutual_event_count'] as num).toInt(),
);

Map<String, dynamic> _$$AllyProfileCardDtoImplToJson(
  _$AllyProfileCardDtoImpl instance,
) => <String, dynamic>{
  'user_id': instance.userId,
  'name': instance.name,
  'username': instance.username,
  'avatar_key': instance.avatarKey,
  'mutual_event_count': instance.mutualEventCount,
};

_$AllyConnectionDtoImpl _$$AllyConnectionDtoImplFromJson(
  Map<String, dynamic> json,
) => _$AllyConnectionDtoImpl(
  id: json['id'] as String,
  otherUserId: json['other_user_id'] as String,
  otherName: json['other_name'] as String,
  otherUsername: json['other_username'] as String?,
  otherAvatarKey: json['other_avatar_key'] as String?,
  status: json['status'] as String,
  visibility: json['visibility'] as String,
  requestedAt: DateTime.parse(json['requested_at'] as String),
  respondedAt: json['responded_at'] == null
      ? null
      : DateTime.parse(json['responded_at'] as String),
  firstSharedEventId: json['first_shared_event_id'] as String?,
  firstSharedEventTitle: json['first_shared_event_title'] as String?,
  firstSharedEventSlug: json['first_shared_event_slug'] as String?,
);

Map<String, dynamic> _$$AllyConnectionDtoImplToJson(
  _$AllyConnectionDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'other_user_id': instance.otherUserId,
  'other_name': instance.otherName,
  'other_username': instance.otherUsername,
  'other_avatar_key': instance.otherAvatarKey,
  'status': instance.status,
  'visibility': instance.visibility,
  'requested_at': instance.requestedAt.toIso8601String(),
  'responded_at': instance.respondedAt?.toIso8601String(),
  'first_shared_event_id': instance.firstSharedEventId,
  'first_shared_event_title': instance.firstSharedEventTitle,
  'first_shared_event_slug': instance.firstSharedEventSlug,
};

_$SharedEventSummaryDtoImpl _$$SharedEventSummaryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$SharedEventSummaryDtoImpl(
  id: json['id'] as String,
  title: json['title'] as String,
  slug: json['slug'] as String,
  startsAt: DateTime.parse(json['starts_at'] as String),
);

Map<String, dynamic> _$$SharedEventSummaryDtoImplToJson(
  _$SharedEventSummaryDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'slug': instance.slug,
  'starts_at': instance.startsAt.toIso8601String(),
};

_$SharedOrgSummaryDtoImpl _$$SharedOrgSummaryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$SharedOrgSummaryDtoImpl(
  id: json['id'] as String,
  name: json['name'] as String,
  slug: json['slug'] as String,
);

Map<String, dynamic> _$$SharedOrgSummaryDtoImplToJson(
  _$SharedOrgSummaryDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'slug': instance.slug,
};

_$MutualDetailDtoImpl _$$MutualDetailDtoImplFromJson(
  Map<String, dynamic> json,
) => _$MutualDetailDtoImpl(
  sharedEvents:
      (json['shared_events'] as List<dynamic>?)
          ?.map(
            (e) => SharedEventSummaryDto.fromJson(e as Map<String, dynamic>),
          )
          .toList() ??
      const [],
  sharedOrgs:
      (json['shared_orgs'] as List<dynamic>?)
          ?.map((e) => SharedOrgSummaryDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const [],
  relationships:
      (json['relationships'] as List<dynamic>?)
          ?.map(
            (e) => ProfileRelationshipDto.fromJson(e as Map<String, dynamic>),
          )
          .toList() ??
      const [],
);

Map<String, dynamic> _$$MutualDetailDtoImplToJson(
  _$MutualDetailDtoImpl instance,
) => <String, dynamic>{
  'shared_events': instance.sharedEvents,
  'shared_orgs': instance.sharedOrgs,
  'relationships': instance.relationships,
};

_$AllySuggestionDtoImpl _$$AllySuggestionDtoImplFromJson(
  Map<String, dynamic> json,
) => _$AllySuggestionDtoImpl(
  userId: json['user_id'] as String,
  name: json['name'] as String,
  username: json['username'] as String?,
  avatarKey: json['avatar_key'] as String?,
  sharedEventCount: (json['shared_event_count'] as num).toInt(),
  sharedOrgCount: (json['shared_org_count'] as num).toInt(),
  reason: json['reason'] as String,
);

Map<String, dynamic> _$$AllySuggestionDtoImplToJson(
  _$AllySuggestionDtoImpl instance,
) => <String, dynamic>{
  'user_id': instance.userId,
  'name': instance.name,
  'username': instance.username,
  'avatar_key': instance.avatarKey,
  'shared_event_count': instance.sharedEventCount,
  'shared_org_count': instance.sharedOrgCount,
  'reason': instance.reason,
};

_$JourneyEvidenceDtoImpl _$$JourneyEvidenceDtoImplFromJson(
  Map<String, dynamic> json,
) => _$JourneyEvidenceDtoImpl(
  kind: json['kind'] as String,
  eventTitle: json['event_title'] as String?,
  eventSlug: json['event_slug'] as String?,
  detail: json['detail'] as String?,
  orgName: json['org_name'] as String?,
);

Map<String, dynamic> _$$JourneyEvidenceDtoImplToJson(
  _$JourneyEvidenceDtoImpl instance,
) => <String, dynamic>{
  'kind': instance.kind,
  'event_title': instance.eventTitle,
  'event_slug': instance.eventSlug,
  'detail': instance.detail,
  'org_name': instance.orgName,
};

_$JourneyNodeDtoImpl _$$JourneyNodeDtoImplFromJson(Map<String, dynamic> json) =>
    _$JourneyNodeDtoImpl(
      tier: json['tier'] as String,
      firstAttainedAt: DateTime.parse(json['first_attained_at'] as String),
      occurrences: (json['occurrences'] as num?)?.toInt() ?? 1,
      source: json['source'] as String? ?? 'verified',
      evidence: JourneyEvidenceDto.fromJson(
        json['evidence'] as Map<String, dynamic>,
      ),
    );

Map<String, dynamic> _$$JourneyNodeDtoImplToJson(
  _$JourneyNodeDtoImpl instance,
) => <String, dynamic>{
  'tier': instance.tier,
  'first_attained_at': instance.firstAttainedAt.toIso8601String(),
  'occurrences': instance.occurrences,
  'source': instance.source,
  'evidence': instance.evidence,
};

_$ProfileRelationshipDtoImpl _$$ProfileRelationshipDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ProfileRelationshipDtoImpl(
  type: json['type'] as String,
  label: json['label'] as String,
  count: (json['count'] as num?)?.toInt() ?? 1,
  context: json['context'] as String?,
);

Map<String, dynamic> _$$ProfileRelationshipDtoImplToJson(
  _$ProfileRelationshipDtoImpl instance,
) => <String, dynamic>{
  'type': instance.type,
  'label': instance.label,
  'count': instance.count,
  'context': instance.context,
};

_$ProfileMetricsDtoImpl _$$ProfileMetricsDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ProfileMetricsDtoImpl(
  eventsOrganized: (json['events_organized'] as num?)?.toInt(),
  eventsParticipated: (json['events_participated'] as num?)?.toInt(),
  eventsAttended: (json['events_attended'] as num?)?.toInt(),
  completionRate: (json['completion_rate'] as num?)?.toDouble(),
  assignmentsAccepted: (json['assignments_accepted'] as num?)?.toInt(),
  assignmentsCompleted: (json['assignments_completed'] as num?)?.toInt(),
  competitionsEntered: (json['competitions_entered'] as num?)?.toInt(),
  competitionsWon: (json['competitions_won'] as num?)?.toInt(),
  speakerSessions: (json['speaker_sessions'] as num?)?.toInt(),
  certificates: (json['certificates'] as num?)?.toInt(),
  achievementCertificates: (json['achievement_certificates'] as num?)?.toInt(),
  organizations: (json['organizations'] as num?)?.toInt(),
  verifiedOrganizations: (json['verified_organizations'] as num?)?.toInt(),
  allyCount: (json['ally_count'] as num?)?.toInt(),
  cities:
      (json['cities'] as List<dynamic>?)?.map((e) => e as String).toList() ??
      const <String>[],
  eventDna:
      (json['event_dna'] as List<dynamic>?)
          ?.map((e) => EventDnaTagDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <EventDnaTagDto>[],
);

Map<String, dynamic> _$$ProfileMetricsDtoImplToJson(
  _$ProfileMetricsDtoImpl instance,
) => <String, dynamic>{
  'events_organized': instance.eventsOrganized,
  'events_participated': instance.eventsParticipated,
  'events_attended': instance.eventsAttended,
  'completion_rate': instance.completionRate,
  'assignments_accepted': instance.assignmentsAccepted,
  'assignments_completed': instance.assignmentsCompleted,
  'competitions_entered': instance.competitionsEntered,
  'competitions_won': instance.competitionsWon,
  'speaker_sessions': instance.speakerSessions,
  'certificates': instance.certificates,
  'achievement_certificates': instance.achievementCertificates,
  'organizations': instance.organizations,
  'verified_organizations': instance.verifiedOrganizations,
  'ally_count': instance.allyCount,
  'cities': instance.cities,
  'event_dna': instance.eventDna,
};

_$ExperienceSummaryDtoImpl _$$ExperienceSummaryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ExperienceSummaryDtoImpl(
  band: json['band'] as String? ?? 'Building',
  distinctEvents: (json['distinct_events'] as num?)?.toInt() ?? 0,
  eventsOrganized: (json['events_organized'] as num?)?.toInt() ?? 0,
  eventsParticipated: (json['events_participated'] as num?)?.toInt() ?? 0,
  eventsAttended: (json['events_attended'] as num?)?.toInt() ?? 0,
  leadershipEvents: (json['leadership_events'] as num?)?.toInt() ?? 0,
  organizations: (json['organizations'] as num?)?.toInt() ?? 0,
  verifiedOrganizations: (json['verified_organizations'] as num?)?.toInt() ?? 0,
  assignmentsCompleted: (json['assignments_completed'] as num?)?.toInt() ?? 0,
  speakerSessions: (json['speaker_sessions'] as num?)?.toInt() ?? 0,
  competitionsWon: (json['competitions_won'] as num?)?.toInt() ?? 0,
  yearsActive: (json['years_active'] as num?)?.toInt() ?? 0,
  firstActivityAt: json['first_activity_at'] == null
      ? null
      : DateTime.parse(json['first_activity_at'] as String),
);

Map<String, dynamic> _$$ExperienceSummaryDtoImplToJson(
  _$ExperienceSummaryDtoImpl instance,
) => <String, dynamic>{
  'band': instance.band,
  'distinct_events': instance.distinctEvents,
  'events_organized': instance.eventsOrganized,
  'events_participated': instance.eventsParticipated,
  'events_attended': instance.eventsAttended,
  'leadership_events': instance.leadershipEvents,
  'organizations': instance.organizations,
  'verified_organizations': instance.verifiedOrganizations,
  'assignments_completed': instance.assignmentsCompleted,
  'speaker_sessions': instance.speakerSessions,
  'competitions_won': instance.competitionsWon,
  'years_active': instance.yearsActive,
  'first_activity_at': instance.firstActivityAt?.toIso8601String(),
};

_$ContributionDayDtoImpl _$$ContributionDayDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ContributionDayDtoImpl(
  date: json['date'] as String,
  count: (json['count'] as num?)?.toInt() ?? 0,
  level: (json['level'] as num?)?.toInt() ?? 0,
);

Map<String, dynamic> _$$ContributionDayDtoImplToJson(
  _$ContributionDayDtoImpl instance,
) => <String, dynamic>{
  'date': instance.date,
  'count': instance.count,
  'level': instance.level,
};

_$ContributionsDtoImpl _$$ContributionsDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ContributionsDtoImpl(
  from: json['from'] as String,
  to: json['to'] as String,
  total: (json['total'] as num?)?.toInt() ?? 0,
  days:
      (json['days'] as List<dynamic>?)
          ?.map((e) => ContributionDayDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const <ContributionDayDto>[],
);

Map<String, dynamic> _$$ContributionsDtoImplToJson(
  _$ContributionsDtoImpl instance,
) => <String, dynamic>{
  'from': instance.from,
  'to': instance.to,
  'total': instance.total,
  'days': instance.days,
};
