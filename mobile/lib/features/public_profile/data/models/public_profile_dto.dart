import 'package:freezed_annotation/freezed_annotation.dart';

part 'public_profile_dto.freezed.dart';
part 'public_profile_dto.g.dart';

@freezed
class CollegeDto with _$CollegeDto {
  const factory CollegeDto({
    String? institute,
    String? degree,
    String? branch,
  }) = _CollegeDto;

  factory CollegeDto.fromJson(Map<String, dynamic> json) =>
      _$CollegeDtoFromJson(json);
}

@freezed
class ProfileStatsDto with _$ProfileStatsDto {
  const factory ProfileStatsDto({
    // Null = hidden from this viewer, never zero (D-229/H2).
    @JsonKey(name: 'events_conducted') int? eventsConducted,
    @JsonKey(name: 'events_attended') int? eventsAttended,
    @JsonKey(name: 'certificates_count') int? certificatesCount,
    int? participations,
    int? achievements,
    @JsonKey(name: 'ally_count') int? allyCount,
  }) = _ProfileStatsDto;

  factory ProfileStatsDto.fromJson(Map<String, dynamic> json) =>
      _$ProfileStatsDtoFromJson(json);
}

@freezed
class VerificationBadgesDto with _$VerificationBadgesDto {
  const factory VerificationBadgesDto({
    @JsonKey(name: 'identity_verified') required bool identityVerified,
    @JsonKey(name: 'verified_member') required bool verifiedMember,
    required bool organizer,
    @JsonKey(name: 'verified_certificates') int? verifiedCertificates,
    @JsonKey(name: 'years_on_platform') required int yearsOnPlatform,
    // D-221 additions — defaulted so a client pinned to an older backend still parses. Positive
    // signals only: no risk, fraud, report or moderation field is ever exposed publicly.
    @JsonKey(name: 'phone_verified') @Default(false) bool phoneVerified,
    @JsonKey(name: 'email_verified') @Default(false) bool emailVerified,
    @JsonKey(name: 'speaker_verified') @Default(false) bool speakerVerified,
    @JsonKey(name: 'community_verified') @Default(false) bool communityVerified,
  }) = _VerificationBadgesDto;

  factory VerificationBadgesDto.fromJson(Map<String, dynamic> json) =>
      _$VerificationBadgesDtoFromJson(json);
}

@freezed
class EventDnaTagDto with _$EventDnaTagDto {
  const factory EventDnaTagDto({
    required String kind,
    required int count,
  }) = _EventDnaTagDto;

  factory EventDnaTagDto.fromJson(Map<String, dynamic> json) =>
      _$EventDnaTagDtoFromJson(json);
}

/// A recognition — NOT tied to certificate storage; `source` is "certificate" or "badge" (D-203).
@freezed
class AchievementCardDto with _$AchievementCardDto {
  const factory AchievementCardDto({
    required String name,
    String? description,
    @JsonKey(name: 'icon_key') String? iconKey,
    @JsonKey(name: 'earned_at') required DateTime earnedAt,
    required String source,
    @JsonKey(name: 'event_title') String? eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    @JsonKey(name: 'org_name') String? orgName,
  }) = _AchievementCardDto;

  factory AchievementCardDto.fromJson(Map<String, dynamic> json) =>
      _$AchievementCardDtoFromJson(json);
}

/// Verified institutional involvement — NOT a followed org (D-206).
@freezed
class ProfileOrgDto with _$ProfileOrgDto {
  const factory ProfileOrgDto({
    @JsonKey(name: 'org_id') required String orgId,
    @JsonKey(name: 'org_name') required String orgName,
    @JsonKey(name: 'org_slug') required String orgSlug,
    @JsonKey(name: 'logo_key') String? logoKey,
    @Default([]) List<String> roles,
    @JsonKey(name: 'is_verified') required bool isVerified,
    @JsonKey(name: 'joined_at') required DateTime joinedAt,
    @JsonKey(name: 'valid_until') DateTime? validUntil,
    @JsonKey(name: 'org_events_conducted') required int orgEventsConducted,
    @JsonKey(name: 'org_certificates_count') required int orgCertificatesCount,
    @JsonKey(name: 'org_achievements_count') required int orgAchievementsCount,
  }) = _ProfileOrgDto;

  factory ProfileOrgDto.fromJson(Map<String, dynamic> json) =>
      _$ProfileOrgDtoFromJson(json);
}

/// One accepted event assignment — Phase 2's "Experience". Verified, not a self-declared job
/// history: the row only exists because an organizer created the role and this person accepted it.
@freezed
class ProfileAssignmentDto with _$ProfileAssignmentDto {
  const factory ProfileAssignmentDto({
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'event_title') required String eventTitle,
    @JsonKey(name: 'event_slug') required String eventSlug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    required String role,
    required String status,
    @JsonKey(name: 'completed_at') DateTime? completedAt,
    @JsonKey(name: 'starts_at') required DateTime startsAt,
    // Null for a self-represented event (D-268) — there is no organization to name, and the
    // self-representation row is named after the person. `required` here threw on every such row.
    @JsonKey(name: 'org_name') String? orgName,
  }) = _ProfileAssignmentDto;

  factory ProfileAssignmentDto.fromJson(Map<String, dynamic> json) =>
      _$ProfileAssignmentDtoFromJson(json);
}

@freezed
class PublicProfileDto with _$PublicProfileDto {
  const factory PublicProfileDto({
    required String id,
    required String name,
    String? username,
    String? headline,
    String? bio,
    required String summary,
    /// When the account was created, **month precision only** — ISO year-month, `"2026-08"`.
    ///
    /// A `String`, not a `DateTime`: `DateTime.parse('2026-08')` throws, and there is no instant here
    /// to parse. The server truncates deliberately so the exact signup time is never on a public wire
    /// (D-312); the owner's full timestamp comes from `/v1/me`'s `created_at` instead.
    ///
    /// Not to be confused with [ProfileOrgDto.joinedAt], which is when this person joined an
    /// organisation — a different object and a different fact.
    @JsonKey(name: 'joined_at') String? joinedAt,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'cover_key') String? coverKey,
    CollegeDto? college,
    /// The wire key is `links_json` (from `PublicProfileView.LinksJson`). Without the annotation
    /// json_serializable derived `links` from the field name, read a key the server never sends, and
    /// `_parseLinks` was handed null on every profile — so the About panel's social links rendered for
    /// nobody, silently. Web declared the same wrong key.
    @JsonKey(name: 'links_json') String? links,
    @Default([]) List<String> skills,
    /// Phase 2 (About), self-declared. Empty means "not stated" — the section is omitted rather
    /// than rendering a heading over nothing.
    @Default([]) List<String> languages,
    /// Self-declared interests. Deliberately NOT [eventDna], which is what this person provably
    /// did: one is a claim, the other is proof, and the profile keeps them visibly apart (D-225).
    @Default([]) List<String> interests,
    required ProfileStatsDto stats,
    required VerificationBadgesDto verification,
    @JsonKey(name: 'event_dna') @Default([]) List<EventDnaTagDto> eventDna,
    @Default([]) List<AchievementCardDto> achievements,
    @JsonKey(name: 'identity_labels') @Default([]) List<String> identityLabels,
    /// The derived headline (D-225) — the short form of [identityLabels]. Distinct from [headline],
    /// which is the user's own self-declared line: one is proof, the other is a claim.
    @JsonKey(name: 'derived_headline') @Default('') String derivedHeadline,
    @Default([]) List<ProfileOrgDto> organizations,
  }) = _PublicProfileDto;

  factory PublicProfileDto.fromJson(Map<String, dynamic> json) =>
      _$PublicProfileDtoFromJson(json);
}

@freezed
class PublicCertificateCardDto with _$PublicCertificateCardDto {
  const factory PublicCertificateCardDto({
    required String id,
    @JsonKey(name: 'event_title') required String eventTitle,
    @JsonKey(name: 'issued_at') required DateTime issuedAt,
    @JsonKey(name: 'verify_code') required String verifyCode,
    @JsonKey(name: 'is_achievement') required bool isAchievement,
  }) = _PublicCertificateCardDto;

  factory PublicCertificateCardDto.fromJson(Map<String, dynamic> json) =>
      _$PublicCertificateCardDtoFromJson(json);
}

/// One event card: real involvement, every role held, and any certificate/achievement earned there.
@freezed
class PublicEventCardDto with _$PublicEventCardDto {
  const factory PublicEventCardDto({
    required String id,
    required String title,
    required String slug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    @JsonKey(name: 'starts_at') required DateTime startsAt,
    required String city,
    @JsonKey(name: 'org_name') required String orgName,
    @Default([]) List<String> roles,
    required String visibility,
    @JsonKey(name: 'certificate_verify_code') String? certificateVerifyCode,
    @JsonKey(name: 'is_achievement') required bool isAchievement,
  }) = _PublicEventCardDto;

  factory PublicEventCardDto.fromJson(Map<String, dynamic> json) =>
      _$PublicEventCardDtoFromJson(json);
}

/// One professional milestone (D-201) — an event with 2 roles is one entry, not two.
@freezed
class TimelineEntryDto with _$TimelineEntryDto {
  const factory TimelineEntryDto({
    required String kind,
    required String title,
    String? slug,
    @JsonKey(name: 'banner_key') String? bannerKey,
    @Default([]) List<String> roles,
    @JsonKey(name: 'org_name') String? orgName,
    String? city,
    @JsonKey(name: 'occurred_at') required DateTime occurredAt,
    @JsonKey(name: 'verify_code') String? verifyCode,
    @JsonKey(name: 'is_first_event') required bool isFirstEvent,
  }) = _TimelineEntryDto;

  factory TimelineEntryDto.fromJson(Map<String, dynamic> json) =>
      _$TimelineEntryDtoFromJson(json);
}

@freezed
class PublicUserSearchResultDto with _$PublicUserSearchResultDto {
  const factory PublicUserSearchResultDto({
    required String id,
    required String name,
    required String username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    String? headline,
  }) = _PublicUserSearchResultDto;

  factory PublicUserSearchResultDto.fromJson(Map<String, dynamic> json) =>
      _$PublicUserSearchResultDtoFromJson(json);
}

@freezed
class AllyProfileCardDto with _$AllyProfileCardDto {
  const factory AllyProfileCardDto({
    @JsonKey(name: 'user_id') required String userId,
    required String name,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'mutual_event_count') required int mutualEventCount,
  }) = _AllyProfileCardDto;

  factory AllyProfileCardDto.fromJson(Map<String, dynamic> json) =>
      _$AllyProfileCardDtoFromJson(json);
}

/// One ally connection from the caller's perspective (D-201) — `otherUserId` is always the
/// counterpart, never the caller. `status` is Pending/Accepted/Declined/Revoked.
@freezed
class AllyConnectionDto with _$AllyConnectionDto {
  const factory AllyConnectionDto({
    required String id,
    @JsonKey(name: 'other_user_id') required String otherUserId,
    @JsonKey(name: 'other_name') required String otherName,
    @JsonKey(name: 'other_username') String? otherUsername,
    @JsonKey(name: 'other_avatar_key') String? otherAvatarKey,
    required String status,
    required String visibility,
    @JsonKey(name: 'requested_at') required DateTime requestedAt,
    @JsonKey(name: 'responded_at') DateTime? respondedAt,
    @JsonKey(name: 'first_shared_event_id') String? firstSharedEventId,
    @JsonKey(name: 'first_shared_event_title') String? firstSharedEventTitle,
    @JsonKey(name: 'first_shared_event_slug') String? firstSharedEventSlug,
  }) = _AllyConnectionDto;

  factory AllyConnectionDto.fromJson(Map<String, dynamic> json) =>
      _$AllyConnectionDtoFromJson(json);
}

@freezed
class SharedEventSummaryDto with _$SharedEventSummaryDto {
  const factory SharedEventSummaryDto({
    required String id,
    required String title,
    required String slug,
    @JsonKey(name: 'starts_at') required DateTime startsAt,
  }) = _SharedEventSummaryDto;

  factory SharedEventSummaryDto.fromJson(Map<String, dynamic> json) =>
      _$SharedEventSummaryDtoFromJson(json);
}

@freezed
class SharedOrgSummaryDto with _$SharedOrgSummaryDto {
  const factory SharedOrgSummaryDto({
    required String id,
    required String name,
    required String slug,
  }) = _SharedOrgSummaryDto;

  factory SharedOrgSummaryDto.fromJson(Map<String, dynamic> json) =>
      _$SharedOrgSummaryDtoFromJson(json);
}

@freezed
class MutualDetailDto with _$MutualDetailDto {
  const factory MutualDetailDto({
    @JsonKey(name: 'shared_events') @Default([]) List<SharedEventSummaryDto> sharedEvents,
    @JsonKey(name: 'shared_orgs') @Default([]) List<SharedOrgSummaryDto> sharedOrgs,
    /// Why the two know each other (D-226). Defaults to empty so a pre-D-226 backend still parses.
    @Default([]) List<ProfileRelationshipDto> relationships,
  }) = _MutualDetailDto;

  factory MutualDetailDto.fromJson(Map<String, dynamic> json) =>
      _$MutualDetailDtoFromJson(json);
}

/// A ranked candidate for a new ally connection (D-20x), v1 signals only.
@freezed
class AllySuggestionDto with _$AllySuggestionDto {
  const factory AllySuggestionDto({
    @JsonKey(name: 'user_id') required String userId,
    required String name,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'shared_event_count') required int sharedEventCount,
    @JsonKey(name: 'shared_org_count') required int sharedOrgCount,
    required String reason,
  }) = _AllySuggestionDto;

  factory AllySuggestionDto.fromJson(Map<String, dynamic> json) =>
      _$AllySuggestionDtoFromJson(json);
}

/// Evidence backing one Professional Journey node (D-223). Every node names the row that proved it —
/// a node with no resolvable evidence is never emitted by the server.
@freezed
class JourneyEvidenceDto with _$JourneyEvidenceDto {
  const factory JourneyEvidenceDto({
    required String kind,
    @JsonKey(name: 'event_title') String? eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    String? detail,
    @JsonKey(name: 'org_name') String? orgName,
  }) = _JourneyEvidenceDto;

  factory JourneyEvidenceDto.fromJson(Map<String, dynamic> json) =>
      _$JourneyEvidenceDtoFromJson(json);
}

/// One rung of the Professional Journey (D-223) — the first time a tier was provably reached.
/// The list arrives ordered chronologically by [firstAttainedAt], never by a canonical career
/// ladder, so the client renders it in the order given.
@freezed
class JourneyNodeDto with _$JourneyNodeDto {
  const factory JourneyNodeDto({
    required String tier,
    @JsonKey(name: 'first_attained_at') required DateTime firstAttainedAt,
    @Default(1) int occurrences,
    @Default('verified') String source,
    required JourneyEvidenceDto evidence,
  }) = _JourneyNodeDto;

  factory JourneyNodeDto.fromJson(Map<String, dynamic> json) =>
      _$JourneyNodeDtoFromJson(json);
}

/// One derived professional relationship (D-226) — *why* two people know each other.
/// [label] is already written from the viewer's perspective by the server, so the client renders it
/// verbatim rather than re-wording it. There is deliberately no strength score.
@freezed
class ProfileRelationshipDto with _$ProfileRelationshipDto {
  const factory ProfileRelationshipDto({
    required String type,
    required String label,
    @Default(1) int count,
    String? context,
  }) = _ProfileRelationshipDto;

  factory ProfileRelationshipDto.fromJson(Map<String, dynamic> json) =>
      _$ProfileRelationshipDtoFromJson(json);
}

/// Profile metrics (D-225). **A null member means hidden from this viewer, never zero** — render an
/// em dash for null. Showing 0 would turn the owner's privacy choice into a claim about them.
@freezed
class ProfileMetricsDto with _$ProfileMetricsDto {
  const factory ProfileMetricsDto({
    // Every count nullable: null means hidden from this viewer, never zero (D-229/H2). A
    // `@Default(0)` here would silently convert a privacy choice into a claim about the person.
    @JsonKey(name: 'events_organized') int? eventsOrganized,
    @JsonKey(name: 'events_participated') int? eventsParticipated,
    @JsonKey(name: 'events_attended') int? eventsAttended,
    @JsonKey(name: 'completion_rate') double? completionRate,
    @JsonKey(name: 'assignments_accepted') int? assignmentsAccepted,
    @JsonKey(name: 'assignments_completed') int? assignmentsCompleted,
    @JsonKey(name: 'competitions_entered') int? competitionsEntered,
    @JsonKey(name: 'competitions_won') int? competitionsWon,
    @JsonKey(name: 'speaker_sessions') int? speakerSessions,
    int? certificates,
    @JsonKey(name: 'achievement_certificates') int? achievementCertificates,
    int? organizations,
    @JsonKey(name: 'verified_organizations') int? verifiedOrganizations,
    @JsonKey(name: 'ally_count') int? allyCount,
    @Default(<String>[]) List<String> cities,
    @JsonKey(name: 'event_dna') @Default(<EventDnaTagDto>[]) List<EventDnaTagDto> eventDna,
  }) = _ProfileMetricsDto;

  factory ProfileMetricsDto.fromJson(Map<String, dynamic> json) =>
      _$ProfileMetricsDtoFromJson(json);
}

/// Experience (D-225) — a band plus the counts that produced it. The two always render together:
/// a band alone is an unfalsifiable judgement, and the counts make it checkable.
@freezed
class ExperienceSummaryDto with _$ExperienceSummaryDto {
  const factory ExperienceSummaryDto({
    @Default('Building') String band,
    @JsonKey(name: 'distinct_events') @Default(0) int distinctEvents,
    @JsonKey(name: 'events_organized') @Default(0) int eventsOrganized,
    @JsonKey(name: 'events_participated') @Default(0) int eventsParticipated,
    @JsonKey(name: 'events_attended') @Default(0) int eventsAttended,
    @JsonKey(name: 'leadership_events') @Default(0) int leadershipEvents,
    @Default(0) int organizations,
    @JsonKey(name: 'verified_organizations') @Default(0) int verifiedOrganizations,
    @JsonKey(name: 'assignments_completed') @Default(0) int assignmentsCompleted,
    @JsonKey(name: 'speaker_sessions') @Default(0) int speakerSessions,
    @JsonKey(name: 'competitions_won') @Default(0) int competitionsWon,
    @JsonKey(name: 'years_active') @Default(0) int yearsActive,
    @JsonKey(name: 'first_activity_at') DateTime? firstActivityAt,
  }) = _ExperienceSummaryDto;

  factory ExperienceSummaryDto.fromJson(Map<String, dynamic> json) =>
      _$ExperienceSummaryDtoFromJson(json);
}

/// One day of the contributions heatmap (D-228). [level] is a 0–4 intensity band computed by the
/// server, so the client never re-decides the scale.
@freezed
class ContributionDayDto with _$ContributionDayDto {
  const factory ContributionDayDto({
    required String date,
    @Default(0) int count,
    @Default(0) int level,
  }) = _ContributionDayDto;

  factory ContributionDayDto.fromJson(Map<String, dynamic> json) =>
      _$ContributionDayDtoFromJson(json);
}

/// The contributions heatmap (D-228). Only days *with* activity are returned — the client fills the
/// gaps when it lays out the grid, so a year of zeroes never crosses the wire.
@freezed
class ContributionsDto with _$ContributionsDto {
  const factory ContributionsDto({
    required String from,
    required String to,
    @Default(0) int total,
    @Default(<ContributionDayDto>[]) List<ContributionDayDto> days,
  }) = _ContributionsDto;

  factory ContributionsDto.fromJson(Map<String, dynamic> json) =>
      _$ContributionsDtoFromJson(json);
}
