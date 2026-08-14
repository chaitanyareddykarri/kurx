import 'package:freezed_annotation/freezed_annotation.dart';

part 'gamification_dto.freezed.dart';
part 'gamification_dto.g.dart';

/// Every `GamificationEndpoints.cs` route (`/me/points`, `/me/badges`, `/leaderboards*`) returns
/// `Results.Ok(record)` directly with no snake_case mapping layer, so the wire format is
/// **camelCase** — same quirk as `AttendeeRow`/`LeaderboardEntryDto` (D-208/D-210). These DTOs
/// previously described a level/tier system (`level`, `levelName`, `nextLevelPoints`) and a
/// nested `badges` list that never existed on the backend at all — `PointsSummary` is just
/// `{ totalPoints, history }`, and badges are their own endpoint (`/v1/me/badges`), not nested.
/// Fixed to match reality (D-212) rather than a contract that was never real.
@freezed
class BadgeDto with _$BadgeDto {
  const factory BadgeDto({
    required String id,
    required String name,
    required String type,
    required String description,
    String? iconKey,
    required DateTime earnedAt,
  }) = _BadgeDto;

  factory BadgeDto.fromJson(Map<String, dynamic> json) =>
      _$BadgeDtoFromJson(json);
}

@freezed
class PointsLogEntryDto with _$PointsLogEntryDto {
  const factory PointsLogEntryDto({
    required String id,
    required String source,
    required int points,
    String? reason,
    required DateTime createdAt,
  }) = _PointsLogEntryDto;

  factory PointsLogEntryDto.fromJson(Map<String, dynamic> json) =>
      _$PointsLogEntryDtoFromJson(json);
}

@freezed
class PointsSummaryDto with _$PointsSummaryDto {
  const factory PointsSummaryDto({
    required int totalPoints,
    @Default([]) List<PointsLogEntryDto> history,
  }) = _PointsSummaryDto;

  factory PointsSummaryDto.fromJson(Map<String, dynamic> json) =>
      _$PointsSummaryDtoFromJson(json);
}

@freezed
class LeaderboardEntryDto with _$LeaderboardEntryDto {
  /// Note: this endpoint returns `Results.Ok(record)` directly (no snake_case mapping layer),
  /// so the wire format is camelCase — unlike most of the API. Same quirk as `AttendeeRow`.
  const factory LeaderboardEntryDto({
    required int rank,
    required String userId,
    required String name,
    String? username,
    required int points,
  }) = _LeaderboardEntryDto;

  factory LeaderboardEntryDto.fromJson(Map<String, dynamic> json) =>
      _$LeaderboardEntryDtoFromJson(json);
}
