import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/public_profile_dto.dart';

class PublicProfileRemoteDataSource {
  PublicProfileRemoteDataSource(this._dio);
  final Dio _dio;

  Future<PublicProfileDto> getProfile(String username) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username');
        return PublicProfileDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<List<PublicCertificateCardDto>> getCertificates(String username) =>
      guard(() async {
        final res = await _dio.get('/v1/public/users/$username/certificates');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(PublicCertificateCardDto.fromJson)
            .toList();
      });

  Future<List<PublicEventCardDto>> getEvents(String username, {required String type}) =>
      guard(() async {
        final res = await _dio.get('/v1/public/users/$username/events',
            queryParameters: {'type': type});
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(PublicEventCardDto.fromJson)
            .toList();
      });

  Future<List<TimelineEntryDto>> getTimeline(String username) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username/timeline');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(TimelineEntryDto.fromJson)
            .toList();
      });

  Future<List<AllyProfileCardDto>> getAllies(String username) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username/allies');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AllyProfileCardDto.fromJson)
            .toList();
      });

  /// The Professional Journey (D-223). Unpaginated — at most one node per tier.
  Future<List<JourneyNodeDto>> getJourney(String username) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username/journey');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(JourneyNodeDto.fromJson)
            .toList();
      });

  /// Accepted assignments — Phase 2's Experience block. Same 403-means-omit contract as every other
  /// section here.
  Future<List<ProfileAssignmentDto>> getAssignments(String username) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username/assignments');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(ProfileAssignmentDto.fromJson)
            .toList();
      });

  /// Counts, rates and the Event DNA distribution (D-225). A 403 means the viewer may not see this
  /// section — callers treat that as "omit", not as an error.
  Future<ProfileMetricsDto> getMetrics(String username) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username/metrics');
        return ProfileMetricsDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  /// Experience band + the counts behind it (D-225).
  Future<ExperienceSummaryDto> getExperience(String username) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username/experience');
        return ExperienceSummaryDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  /// Daily contribution density (D-228). Only days with activity come back.
  Future<ContributionsDto> getContributions(String username, {int months = 12}) => guard(() async {
        final res = await _dio.get('/v1/public/users/$username/contributions',
            queryParameters: {'months': months});
        return ContributionsDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  /// The resume is a PDF download, not JSON — the URL is handed to the OS browser so the file lands
  /// through the normal download path rather than being buffered in the app.
  String resumeUrl(String username) =>
      '${_dio.options.baseUrl}/v1/public/users/$username/resume';

  /// Foundation for people-search (D-20x) — public profiles only, name/username substring match.
  Future<List<PublicUserSearchResultDto>> search(String query) => guard(() async {
        if (query.trim().length < 2) return [];
        final res = await _dio.get('/v1/public/users', queryParameters: {'q': query});
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(PublicUserSearchResultDto.fromJson)
            .toList();
      });
}

/// Ally connections (D-201) — request/accept/decline/revoke + the three list views.
class AllyRemoteDataSource {
  AllyRemoteDataSource(this._dio);
  final Dio _dio;

  Future<AllyConnectionDto> request(String targetUserId) => guard(() async {
        final res = await _dio.post('/v1/allies/requests',
            data: {'targetUserId': targetUserId});
        return AllyConnectionDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<AllyConnectionDto> accept(String connectionId) => guard(() async {
        final res = await _dio.post('/v1/allies/requests/$connectionId/accept');
        return AllyConnectionDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<AllyConnectionDto> decline(String connectionId) => guard(() async {
        final res = await _dio.post('/v1/allies/requests/$connectionId/decline');
        return AllyConnectionDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<void> revoke(String connectionId) => guard(() async {
        await _dio.delete('/v1/allies/$connectionId');
      });

  /// Per-connection visibility (D-219) — hide one ally from the public list without turning off
  /// `showAllies` entirely. Either party may set it and the flag is shared; the more private choice
  /// wins. `visibility` is the case-insensitive enum name: `public` or `hidden`.
  Future<AllyConnectionDto> setVisibility(String connectionId, String visibility) =>
      guard(() async {
        final res = await _dio.patch(
          '/v1/allies/$connectionId/visibility',
          data: {'visibility': visibility},
        );
        return AllyConnectionDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<List<AllyConnectionDto>> incoming() => guard(() async {
        final res = await _dio.get('/v1/allies/requests/incoming');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AllyConnectionDto.fromJson)
            .toList();
      });

  Future<List<AllyConnectionDto>> outgoing() => guard(() async {
        final res = await _dio.get('/v1/allies/requests/outgoing');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AllyConnectionDto.fromJson)
            .toList();
      });

  Future<List<AllyConnectionDto>> mine() => guard(() async {
        final res = await _dio.get('/v1/me/allies');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AllyConnectionDto.fromJson)
            .toList();
      });

  /// One call for a whole page of user ids — the batch primitive every person-list surface uses
  /// instead of a request per card (no N+1). Values: none|pending_outgoing|pending_incoming|accepted.
  Future<Map<String, String>> statusBatch(List<String> userIds) => guard(() async {
        if (userIds.isEmpty) return {};
        final res = await _dio.post('/v1/me/allies/status-batch', data: {'userIds': userIds});
        return (res.data as Map).cast<String, String>();
      });

  Future<MutualDetailDto> mutualDetail(String otherUserId) => guard(() async {
        final res = await _dio.get('/v1/me/allies/mutual/$otherUserId');
        return MutualDetailDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<List<AllySuggestionDto>> suggestions({int limit = 20}) => guard(() async {
        final res = await _dio.get('/v1/me/allies/suggestions', queryParameters: {'limit': limit});
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AllySuggestionDto.fromJson)
            .toList();
      });
}
