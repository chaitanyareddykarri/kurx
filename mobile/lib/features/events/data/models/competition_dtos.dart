import 'package:freezed_annotation/freezed_annotation.dart';

part 'competition_dtos.freezed.dart';
part 'competition_dtos.g.dart';

/// Series, team and competition DTOs.
///
/// ## Why these are camelCase and the others are snake_case
///
/// `SeriesEndpoints` and `CompetitionEndpoints` return their view records **directly**
/// (`Results.Ok(r.Value)`), so System.Text.Json serialises them with the minimal-API default
/// (`JsonSerializerDefaults.Web` → `JsonNamingPolicy.CamelCase`): `eventId`, `subjectName`,
/// `participantCount`. Endpoints that hand-roll a `ToJson` anonymous object — including
/// `TeamEndpoints` — emit literal snake_case instead, and the camelCase policy leaves those alone
/// because they already start lowercase.
///
/// So the naming here is per-endpoint, not per-feature. It was read off each endpoint rather than
/// assumed; the existing `WalletDto` (camelCase, over a raw `WalletView`) is the precedent.

// ── Series (camelCase — raw `SeriesView`) ────────────────────────────────────

@freezed
class SeriesDto with _$SeriesDto {
  const factory SeriesDto({
    required String id,
    required String orgId,
    required String name,
    @Default('') String slug,
    /// `recurring` or `editions`.
    @Default('recurring') String mode,
    @Default('') String description,
    String? bannerKey,
    String? rrule,
    @Default(0) int followerCount,
    @Default(0) int memberCount,
    DateTime? createdAt,
  }) = _SeriesDto;

  factory SeriesDto.fromJson(Map<String, dynamic> json) => _$SeriesDtoFromJson(json);
}

/// One edition/occurrence inside a series — raw `SeriesMemberView`.
@freezed
class SeriesMemberDto with _$SeriesMemberDto {
  const factory SeriesMemberDto({
    required String eventId,
    required String title,
    @Default('') String slug,
    int? editionOrdinal,
    String? editionLabel,
    DateTime? startsAt,
    @Default('') String timezone,
    @Default('') String status,
  }) = _SeriesMemberDto;

  factory SeriesMemberDto.fromJson(Map<String, dynamic> json) => _$SeriesMemberDtoFromJson(json);
}

// ── Competition (camelCase — raw `StageView` / `StageParticipantView` / `ResultView`) ──

@freezed
class StageDto with _$StageDto {
  const factory StageDto({
    required String id,
    required String eventId,
    @Default(0) int sequence,
    required String name,
    /// `knockout` / `league` / `judged` / `freeform`…
    @Default('') String format,
    @Default('') String participantSource,
    String? advancedFromStageId,
    @Default('') String advancementRule,
    int? advancementThreshold,
    String? scoringPolicyId,
    DateTime? startsAt,
    DateTime? endsAt,
    String? venueId,
    @Default('') String mode,
    /// `public` / `participants` / `private` — drives whether results are shown at all.
    @Default('private') String resultsVisibility,
    /// `draft` / `open` / `running` / `completed` / `published`.
    @Default('draft') String state,
    @Default(0) int participantCount,
  }) = _StageDto;

  factory StageDto.fromJson(Map<String, dynamic> json) => _$StageDtoFromJson(json);
}

/// A roster row. [subjectName] is resolved server-side precisely because the client cannot
/// resolve a `subjectId` — there is no by-id person lookup on this surface.
@freezed
class StageParticipantDto with _$StageParticipantDto {
  const factory StageParticipantDto({
    required String id,
    required String stageId,
    @Default('') String subjectType,
    required String subjectId,
    @Default('') String subjectName,
    int? seed,
    @Default(false) bool advanced,
  }) = _StageParticipantDto;

  factory StageParticipantDto.fromJson(Map<String, dynamic> json) =>
      _$StageParticipantDtoFromJson(json);
}

@freezed
class StageResultDto with _$StageResultDto {
  const factory StageResultDto({
    required String id,
    required String stageId,
    @Default('') String subjectType,
    required String subjectId,
    @Default('') String subjectName,
    @Default(0) int rank,
    /// `decimal?` over the wire — read as `num` then widened, never as `int`.
    double? finalScore,
    String? scoreBreakdownJson,
    @Default('') String state,
    DateTime? publishedAt,
    @Default(0) int correctionCount,
  }) = _StageResultDto;

  factory StageResultDto.fromJson(Map<String, dynamic> json) => _$StageResultDtoFromJson(json);
}

// ── Teams (snake_case — explicit `ToTeamJson`) ───────────────────────────────

@freezed
class TeamMemberDto with _$TeamMemberDto {
  const factory TeamMemberDto({
    @JsonKey(name: 'membership_id') required String membershipId,
    @JsonKey(name: 'person_id') String? personId,
    @Default('') String name,
    @Default('member') String role,
    @Default('active') String state,
    @JsonKey(name: 'joined_at') DateTime? joinedAt,
  }) = _TeamMemberDto;

  factory TeamMemberDto.fromJson(Map<String, dynamic> json) => _$TeamMemberDtoFromJson(json);
}

@freezed
class TeamDto with _$TeamDto {
  const factory TeamDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    required String name,
    String? slug,
    @JsonKey(name: 'logo_url') String? logoUrl,
    String? tagline,
    /// `forming` / `locked` / `withdrawn` / `merged`.
    @Default('forming') String state,
    @JsonKey(name: 'active_member_count') @Default(0) int activeMemberCount,
    @JsonKey(name: 'created_at') DateTime? createdAt,
    @Default(<TeamMemberDto>[]) List<TeamMemberDto> members,
  }) = _TeamDto;

  factory TeamDto.fromJson(Map<String, dynamic> json) => _$TeamDtoFromJson(json);
}

/// `ToInviteJson` — note this one **does** carry `token`, unlike org invitations, so a team invite
/// can be accepted straight from a list.
@freezed
class TeamInviteDto with _$TeamInviteDto {
  const factory TeamInviteDto({
    required String id,
    @JsonKey(name: 'team_id') required String teamId,
    @JsonKey(name: 'invitee_email') String? inviteeEmail,
    @JsonKey(name: 'invitee_phone') String? inviteePhone,
    @Default('member') String role,
    @Default('pending') String state,
    String? token,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
  }) = _TeamInviteDto;

  factory TeamInviteDto.fromJson(Map<String, dynamic> json) => _$TeamInviteDtoFromJson(json);
}
