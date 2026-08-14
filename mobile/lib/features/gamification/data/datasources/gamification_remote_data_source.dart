import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/gamification_dto.dart';

class GamificationRemoteDataSource {
  GamificationRemoteDataSource(this._dio);
  final Dio _dio;

  Future<PointsSummaryDto> myPoints() => guard(() async {
        final res = await _dio.get('/v1/me/points');
        return PointsSummaryDto.fromJson(
            (res.data as Map).cast<String, dynamic>());
      });

  Future<List<BadgeDto>> myBadges() => guard(() async {
        final res = await _dio.get('/v1/me/badges');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(BadgeDto.fromJson)
            .toList();
      }, endpoint: 'GET /v1/me/badges');

  /// The real backend (`GamificationEndpoints.cs`) has no time-period dimension — `Leaderboard`
  /// rows are scoped `global`/`organization` only, refreshed by a background job, with no
  /// week/month/all-time filter to request. There is exactly one real global leaderboard.
  Future<List<LeaderboardEntryDto>> leaderboard({int limit = 50}) => guard(() async {
        final res = await _dio.get('/v1/leaderboards', queryParameters: {'limit': limit});
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(LeaderboardEntryDto.fromJson)
            .toList();
      }, endpoint: 'GET /v1/leaderboards');
}
