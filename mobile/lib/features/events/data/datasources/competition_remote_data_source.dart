import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/competition_dtos.dart';

/// Series, teams and competition reads for the attendee-facing screens.
///
/// Deliberately read-plus-participation only: stage creation, scoring policies, fixtures and
/// result publication are organiser/admin actions that already live in the admin console, and the
/// attendee app has no business carrying them.
class CompetitionRemoteDataSource {
  CompetitionRemoteDataSource(this._dio);
  final Dio _dio;

  // ── Series ─────────────────────────────────────────────────────────────────

  Future<SeriesDto> series(String seriesId) => guard(
        () async {
          final res = await _dio.get('/v1/series/$seriesId');
          return SeriesDto.fromJson(_map(res.data));
        },
        endpoint: 'GET /v1/series/{seriesId}',
      );

  Future<List<SeriesMemberDto>> seriesEditions(String seriesId) => guard(
        () async {
          final res = await _dio.get('/v1/series/$seriesId/events');
          return _list(res.data, SeriesMemberDto.fromJson);
        },
        endpoint: 'GET /v1/series/{seriesId}/events',
      );

  Future<void> followSeries(String seriesId) => guard(
        () => _dio.post('/v1/series/$seriesId/follow'),
        endpoint: 'POST /v1/series/{seriesId}/follow',
      );

  Future<void> unfollowSeries(String seriesId) => guard(
        () => _dio.delete('/v1/series/$seriesId/follow'),
        endpoint: 'DELETE /v1/series/{seriesId}/follow',
      );

  // ── Competition ────────────────────────────────────────────────────────────

  Future<List<StageDto>> stages(String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/events/$eventId/stages');
          return _list(res.data, StageDto.fromJson);
        },
        endpoint: 'GET /v1/events/{eventId}/stages',
      );

  Future<List<StageParticipantDto>> stageParticipants(String stageId) => guard(
        () async {
          final res = await _dio.get('/v1/stages/$stageId/participants');
          return _list(res.data, StageParticipantDto.fromJson);
        },
        endpoint: 'GET /v1/stages/{stageId}/participants',
      );

  Future<List<StageResultDto>> stageResults(String stageId) => guard(
        () async {
          final res = await _dio.get('/v1/stages/$stageId/results');
          return _list(res.data, StageResultDto.fromJson);
        },
        endpoint: 'GET /v1/stages/{stageId}/results',
      );

  // ── Teams ──────────────────────────────────────────────────────────────────

  Future<List<TeamDto>> eventTeams(String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/events/$eventId/teams');
          return _list(res.data, TeamDto.fromJson);
        },
        endpoint: 'GET /v1/events/{eventId}/teams',
      );

  Future<List<TeamDto>> myTeams() => guard(
        () async {
          final res = await _dio.get('/v1/me/teams');
          return _list(res.data, TeamDto.fromJson);
        },
        endpoint: 'GET /v1/me/teams',
      );

  Future<TeamDto> team(String teamId) => guard(
        () async {
          final res = await _dio.get('/v1/teams/$teamId');
          return TeamDto.fromJson(_map(res.data));
        },
        endpoint: 'GET /v1/teams/{teamId}',
      );

  /// `ticketTypeId` is a **query** parameter on this route, not part of the body.
  Future<TeamDto> createTeam(
    String eventId, {
    required String ticketTypeId,
    required String name,
    String? tagline,
  }) =>
      guard(
        () async {
          final res = await _dio.post(
            '/v1/events/$eventId/teams',
            queryParameters: {'ticketTypeId': ticketTypeId},
            data: {'name': name, 'tagline': ?tagline},
          );
          return TeamDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/events/{eventId}/teams',
      );

  Future<void> requestToJoin(String teamId, {String? message}) => guard(
        () => _dio.post('/v1/teams/$teamId/join-requests', data: {'message': ?message}),
        endpoint: 'POST /v1/teams/{teamId}/join-requests',
      );

  Future<void> leaveTeam(String teamId) => guard(
        () => _dio.post('/v1/teams/$teamId/leave'),
        endpoint: 'POST /v1/teams/{teamId}/leave',
      );

  Future<void> inviteToTeam(String teamId, {String? phone, String? email, String? role}) => guard(
        () => _dio.post('/v1/teams/$teamId/invites',
            data: {'phone': ?phone, 'email': ?email, 'role': ?role}),
        endpoint: 'POST /v1/teams/{teamId}/invites',
      );

  Future<void> acceptTeamInvite(String token) => guard(
        () => _dio.post('/v1/teams/invites/$token/accept'),
        endpoint: 'POST /v1/teams/invites/{token}/accept',
      );

  static Map<String, dynamic> _map(Object? data) => (data as Map).cast<String, dynamic>();

  static List<T> _list<T>(Object? data, T Function(Map<String, dynamic>) fromJson) =>
      (data as List? ?? const []).cast<Map<String, dynamic>>().map(fromJson).toList();
}
