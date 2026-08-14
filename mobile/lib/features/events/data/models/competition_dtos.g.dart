// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'competition_dtos.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$SeriesDtoImpl _$$SeriesDtoImplFromJson(Map<String, dynamic> json) =>
    _$SeriesDtoImpl(
      id: json['id'] as String,
      orgId: json['orgId'] as String,
      name: json['name'] as String,
      slug: json['slug'] as String? ?? '',
      mode: json['mode'] as String? ?? 'recurring',
      description: json['description'] as String? ?? '',
      bannerKey: json['bannerKey'] as String?,
      rrule: json['rrule'] as String?,
      followerCount: (json['followerCount'] as num?)?.toInt() ?? 0,
      memberCount: (json['memberCount'] as num?)?.toInt() ?? 0,
      createdAt: json['createdAt'] == null
          ? null
          : DateTime.parse(json['createdAt'] as String),
    );

Map<String, dynamic> _$$SeriesDtoImplToJson(_$SeriesDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'orgId': instance.orgId,
      'name': instance.name,
      'slug': instance.slug,
      'mode': instance.mode,
      'description': instance.description,
      'bannerKey': instance.bannerKey,
      'rrule': instance.rrule,
      'followerCount': instance.followerCount,
      'memberCount': instance.memberCount,
      'createdAt': instance.createdAt?.toIso8601String(),
    };

_$SeriesMemberDtoImpl _$$SeriesMemberDtoImplFromJson(
  Map<String, dynamic> json,
) => _$SeriesMemberDtoImpl(
  eventId: json['eventId'] as String,
  title: json['title'] as String,
  slug: json['slug'] as String? ?? '',
  editionOrdinal: (json['editionOrdinal'] as num?)?.toInt(),
  editionLabel: json['editionLabel'] as String?,
  startsAt: json['startsAt'] == null
      ? null
      : DateTime.parse(json['startsAt'] as String),
  timezone: json['timezone'] as String? ?? '',
  status: json['status'] as String? ?? '',
);

Map<String, dynamic> _$$SeriesMemberDtoImplToJson(
  _$SeriesMemberDtoImpl instance,
) => <String, dynamic>{
  'eventId': instance.eventId,
  'title': instance.title,
  'slug': instance.slug,
  'editionOrdinal': instance.editionOrdinal,
  'editionLabel': instance.editionLabel,
  'startsAt': instance.startsAt?.toIso8601String(),
  'timezone': instance.timezone,
  'status': instance.status,
};

_$StageDtoImpl _$$StageDtoImplFromJson(Map<String, dynamic> json) =>
    _$StageDtoImpl(
      id: json['id'] as String,
      eventId: json['eventId'] as String,
      sequence: (json['sequence'] as num?)?.toInt() ?? 0,
      name: json['name'] as String,
      format: json['format'] as String? ?? '',
      participantSource: json['participantSource'] as String? ?? '',
      advancedFromStageId: json['advancedFromStageId'] as String?,
      advancementRule: json['advancementRule'] as String? ?? '',
      advancementThreshold: (json['advancementThreshold'] as num?)?.toInt(),
      scoringPolicyId: json['scoringPolicyId'] as String?,
      startsAt: json['startsAt'] == null
          ? null
          : DateTime.parse(json['startsAt'] as String),
      endsAt: json['endsAt'] == null
          ? null
          : DateTime.parse(json['endsAt'] as String),
      venueId: json['venueId'] as String?,
      mode: json['mode'] as String? ?? '',
      resultsVisibility: json['resultsVisibility'] as String? ?? 'private',
      state: json['state'] as String? ?? 'draft',
      participantCount: (json['participantCount'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$$StageDtoImplToJson(_$StageDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'eventId': instance.eventId,
      'sequence': instance.sequence,
      'name': instance.name,
      'format': instance.format,
      'participantSource': instance.participantSource,
      'advancedFromStageId': instance.advancedFromStageId,
      'advancementRule': instance.advancementRule,
      'advancementThreshold': instance.advancementThreshold,
      'scoringPolicyId': instance.scoringPolicyId,
      'startsAt': instance.startsAt?.toIso8601String(),
      'endsAt': instance.endsAt?.toIso8601String(),
      'venueId': instance.venueId,
      'mode': instance.mode,
      'resultsVisibility': instance.resultsVisibility,
      'state': instance.state,
      'participantCount': instance.participantCount,
    };

_$StageParticipantDtoImpl _$$StageParticipantDtoImplFromJson(
  Map<String, dynamic> json,
) => _$StageParticipantDtoImpl(
  id: json['id'] as String,
  stageId: json['stageId'] as String,
  subjectType: json['subjectType'] as String? ?? '',
  subjectId: json['subjectId'] as String,
  subjectName: json['subjectName'] as String? ?? '',
  seed: (json['seed'] as num?)?.toInt(),
  advanced: json['advanced'] as bool? ?? false,
);

Map<String, dynamic> _$$StageParticipantDtoImplToJson(
  _$StageParticipantDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'stageId': instance.stageId,
  'subjectType': instance.subjectType,
  'subjectId': instance.subjectId,
  'subjectName': instance.subjectName,
  'seed': instance.seed,
  'advanced': instance.advanced,
};

_$StageResultDtoImpl _$$StageResultDtoImplFromJson(Map<String, dynamic> json) =>
    _$StageResultDtoImpl(
      id: json['id'] as String,
      stageId: json['stageId'] as String,
      subjectType: json['subjectType'] as String? ?? '',
      subjectId: json['subjectId'] as String,
      subjectName: json['subjectName'] as String? ?? '',
      rank: (json['rank'] as num?)?.toInt() ?? 0,
      finalScore: (json['finalScore'] as num?)?.toDouble(),
      scoreBreakdownJson: json['scoreBreakdownJson'] as String?,
      state: json['state'] as String? ?? '',
      publishedAt: json['publishedAt'] == null
          ? null
          : DateTime.parse(json['publishedAt'] as String),
      correctionCount: (json['correctionCount'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$$StageResultDtoImplToJson(
  _$StageResultDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'stageId': instance.stageId,
  'subjectType': instance.subjectType,
  'subjectId': instance.subjectId,
  'subjectName': instance.subjectName,
  'rank': instance.rank,
  'finalScore': instance.finalScore,
  'scoreBreakdownJson': instance.scoreBreakdownJson,
  'state': instance.state,
  'publishedAt': instance.publishedAt?.toIso8601String(),
  'correctionCount': instance.correctionCount,
};

_$TeamMemberDtoImpl _$$TeamMemberDtoImplFromJson(Map<String, dynamic> json) =>
    _$TeamMemberDtoImpl(
      membershipId: json['membership_id'] as String,
      personId: json['person_id'] as String?,
      name: json['name'] as String? ?? '',
      role: json['role'] as String? ?? 'member',
      state: json['state'] as String? ?? 'active',
      joinedAt: json['joined_at'] == null
          ? null
          : DateTime.parse(json['joined_at'] as String),
    );

Map<String, dynamic> _$$TeamMemberDtoImplToJson(_$TeamMemberDtoImpl instance) =>
    <String, dynamic>{
      'membership_id': instance.membershipId,
      'person_id': instance.personId,
      'name': instance.name,
      'role': instance.role,
      'state': instance.state,
      'joined_at': instance.joinedAt?.toIso8601String(),
    };

_$TeamDtoImpl _$$TeamDtoImplFromJson(Map<String, dynamic> json) =>
    _$TeamDtoImpl(
      id: json['id'] as String,
      eventId: json['event_id'] as String,
      ticketTypeId: json['ticket_type_id'] as String?,
      name: json['name'] as String,
      slug: json['slug'] as String?,
      logoUrl: json['logo_url'] as String?,
      tagline: json['tagline'] as String?,
      state: json['state'] as String? ?? 'forming',
      activeMemberCount: (json['active_member_count'] as num?)?.toInt() ?? 0,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
      members:
          (json['members'] as List<dynamic>?)
              ?.map((e) => TeamMemberDto.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <TeamMemberDto>[],
    );

Map<String, dynamic> _$$TeamDtoImplToJson(_$TeamDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'event_id': instance.eventId,
      'ticket_type_id': instance.ticketTypeId,
      'name': instance.name,
      'slug': instance.slug,
      'logo_url': instance.logoUrl,
      'tagline': instance.tagline,
      'state': instance.state,
      'active_member_count': instance.activeMemberCount,
      'created_at': instance.createdAt?.toIso8601String(),
      'members': instance.members,
    };

_$TeamInviteDtoImpl _$$TeamInviteDtoImplFromJson(Map<String, dynamic> json) =>
    _$TeamInviteDtoImpl(
      id: json['id'] as String,
      teamId: json['team_id'] as String,
      inviteeEmail: json['invitee_email'] as String?,
      inviteePhone: json['invitee_phone'] as String?,
      role: json['role'] as String? ?? 'member',
      state: json['state'] as String? ?? 'pending',
      token: json['token'] as String?,
      expiresAt: json['expires_at'] == null
          ? null
          : DateTime.parse(json['expires_at'] as String),
    );

Map<String, dynamic> _$$TeamInviteDtoImplToJson(_$TeamInviteDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'team_id': instance.teamId,
      'invitee_email': instance.inviteeEmail,
      'invitee_phone': instance.inviteePhone,
      'role': instance.role,
      'state': instance.state,
      'token': instance.token,
      'expires_at': instance.expiresAt?.toIso8601String(),
    };
