import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../../../events/data/models/ticket_type_dto.dart';
import '../models/event_content_dto.dart';

/// The organiser's event-content API: speakers, sponsors, venues, sessions, ticket types and media.
///
/// One datasource rather than six, because it is one API surface — every route is org-scoped under
/// the same D-015 role gate (`Owner`/`Manager`, with `kurx_admin` short-circuiting `CanManage`) and
/// every write returns the same `ServiceResult` error codes that [ApiError] already maps.
///
/// Reads return the server's row; writes that reply `{ok:true}` (media attach/remove, session
/// reorder) are followed by a re-read in the provider rather than a locally-guessed row.
class EventContentRemoteDataSource {
  EventContentRemoteDataSource(this._dio);
  final Dio _dio;

  // ── Speakers (org catalogue + per-event assignment) ────────────────────────

  Future<List<SpeakerDto>> orgSpeakers(String orgId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/speakers');
          return _list(res.data, SpeakerDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/speakers',
      );

  Future<List<SpeakerDto>> eventSpeakers(String orgId, String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/events/$eventId/speakers');
          return _list(res.data, SpeakerDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/events/{eventId}/speakers',
      );

  Future<SpeakerDto> createSpeaker(String orgId, Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.post('/v1/orgs/$orgId/speakers', data: body);
          return SpeakerDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/orgs/{orgId}/speakers',
      );

  Future<SpeakerDto> updateSpeaker(String orgId, String speakerId, Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.patch('/v1/orgs/$orgId/speakers/$speakerId', data: body);
          return SpeakerDto.fromJson(_map(res.data));
        },
        endpoint: 'PATCH /v1/orgs/{orgId}/speakers/{speakerId}',
      );

  Future<void> deleteSpeaker(String orgId, String speakerId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/speakers/$speakerId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/speakers/{speakerId}',
      );

  /// Attach an existing catalogue speaker to this event (optionally to one session).
  Future<void> assignSpeaker(String orgId, String eventId, String speakerId, {String? sessionId}) => guard(
        () => _dio.post(
          '/v1/orgs/$orgId/events/$eventId/speakers',
          data: {'speakerId': speakerId, 'sessionId': ?sessionId},
        ),
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/speakers',
      );

  Future<void> unassignSpeaker(String orgId, String eventId, String speakerId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/events/$eventId/speakers/$speakerId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/events/{eventId}/speakers/{speakerId}',
      );

  // ── Sponsors (same catalogue + assignment shape) ───────────────────────────

  Future<List<SponsorDto>> orgSponsors(String orgId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/sponsors');
          return _list(res.data, SponsorDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/sponsors',
      );

  Future<List<SponsorDto>> eventSponsors(String orgId, String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/events/$eventId/sponsors');
          return _list(res.data, SponsorDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/events/{eventId}/sponsors',
      );

  Future<SponsorDto> createSponsor(String orgId, Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.post('/v1/orgs/$orgId/sponsors', data: body);
          return SponsorDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/orgs/{orgId}/sponsors',
      );

  Future<SponsorDto> updateSponsor(String orgId, String sponsorId, Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.patch('/v1/orgs/$orgId/sponsors/$sponsorId', data: body);
          return SponsorDto.fromJson(_map(res.data));
        },
        endpoint: 'PATCH /v1/orgs/{orgId}/sponsors/{sponsorId}',
      );

  Future<void> deleteSponsor(String orgId, String sponsorId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/sponsors/$sponsorId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/sponsors/{sponsorId}',
      );

  Future<void> assignSponsor(String orgId, String eventId, String sponsorId, {int? sort}) => guard(
        () => _dio.post(
          '/v1/orgs/$orgId/events/$eventId/sponsors',
          data: {'sponsorId': sponsorId, 'sort': ?sort},
        ),
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/sponsors',
      );

  Future<void> unassignSponsor(String orgId, String eventId, String sponsorId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/events/$eventId/sponsors/$sponsorId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/events/{eventId}/sponsors/{sponsorId}',
      );

  // ── Venues (org-scoped catalogue; an event references one) ──────────────────

  Future<List<VenueDto>> venues(String orgId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/venues');
          return _list(res.data, VenueDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/venues',
      );

  Future<VenueDto> createVenue(String orgId, Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.post('/v1/orgs/$orgId/venues', data: body);
          return VenueDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/orgs/{orgId}/venues',
      );

  Future<VenueDto> updateVenue(String orgId, String venueId, Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.patch('/v1/orgs/$orgId/venues/$venueId', data: body);
          return VenueDto.fromJson(_map(res.data));
        },
        endpoint: 'PATCH /v1/orgs/{orgId}/venues/{venueId}',
      );

  Future<void> deleteVenue(String orgId, String venueId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/venues/$venueId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/venues/{venueId}',
      );

  // ── Schedule / sessions ────────────────────────────────────────────────────

  Future<List<SessionDto>> sessions(String orgId, String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/events/$eventId/sessions');
          return _list(res.data, SessionDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/events/{eventId}/sessions',
      );

  Future<SessionDto> createSession(String orgId, String eventId, Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.post('/v1/orgs/$orgId/events/$eventId/sessions', data: body);
          return SessionDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/sessions',
      );

  Future<SessionDto> updateSession(
          String orgId, String eventId, String sessionId, Map<String, dynamic> body) =>
      guard(
        () async {
          final res =
              await _dio.patch('/v1/orgs/$orgId/events/$eventId/sessions/$sessionId', data: body);
          return SessionDto.fromJson(_map(res.data));
        },
        endpoint: 'PATCH /v1/orgs/{orgId}/events/{eventId}/sessions/{sessionId}',
      );

  Future<void> deleteSession(String orgId, String eventId, String sessionId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/events/$eventId/sessions/$sessionId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/events/{eventId}/sessions/{sessionId}',
      );

  // ── Ticket types (reuses the shared TicketTypeDto — no second model) ────────

  Future<List<TicketTypeDto>> ticketTypes(String orgId, String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/events/$eventId/ticket-types');
          return _list(res.data, TicketTypeDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/events/{eventId}/ticket-types',
      );

  Future<TicketTypeDto> createTicketType(
          String orgId, String eventId, Map<String, dynamic> body) =>
      guard(
        () async {
          final res = await _dio.post('/v1/orgs/$orgId/events/$eventId/ticket-types', data: body);
          return TicketTypeDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/ticket-types',
      );

  Future<TicketTypeDto> updateTicketType(
          String orgId, String eventId, String ticketTypeId, Map<String, dynamic> body) =>
      guard(
        () async {
          final res = await _dio
              .patch('/v1/orgs/$orgId/events/$eventId/ticket-types/$ticketTypeId', data: body);
          return TicketTypeDto.fromJson(_map(res.data));
        },
        endpoint: 'PATCH /v1/orgs/{orgId}/events/{eventId}/ticket-types/{id}',
      );

  Future<void> deleteTicketType(String orgId, String eventId, String ticketTypeId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/events/$eventId/ticket-types/$ticketTypeId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/events/{eventId}/ticket-types/{id}',
      );

  // ── Media ──────────────────────────────────────────────────────────────────

  /// There is no `GET …/media` route; the gallery is read off the org-scoped event detail.
  Future<List<EventMediaDto>> media(String orgId, String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/events/$eventId');
          final detail = _map(res.data);
          return _list(detail['media'], EventMediaDto.fromJson);
        },
        endpoint: 'GET /v1/orgs/{orgId}/events/{eventId} (media)',
      );

  /// The event's current banner as a **presigned** URL, or null when none is set (D-302). Read off the
  /// same org-scoped detail the gallery is, for the same reason: there is no dedicated route, and one
  /// round trip that already returns the whole event beats inventing a second.
  Future<String?> bannerUrl(String orgId, String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/orgs/$orgId/events/$eventId');
          final url = _map(res.data)['banner_url'];
          return url is String && url.isNotEmpty ? url : null;
        },
        endpoint: 'GET /v1/orgs/{orgId}/events/{eventId} (banner)',
      );

  /// Persists the banner key onto the event (D-302). The banner is a field on the event, not an
  /// `EventMedia` row — see `IMediaService`'s own note — so it is saved with the ordinary event PATCH.
  /// An empty [bannerKey] clears it.
  Future<void> updateBanner(String orgId, String eventId, String bannerKey) => guard(
        () async => _dio.patch('/v1/orgs/$orgId/events/$eventId', data: {'bannerKey': bannerKey}),
        endpoint: 'PATCH /v1/orgs/{orgId}/events/{eventId} (banner)',
      );

  Future<PresignDto> presignMedia(
          String orgId, String eventId, String contentType, int maxBytes) =>
      guard(
        () async {
          final res = await _dio.post(
            '/v1/orgs/$orgId/events/$eventId/media/presign',
            data: {'contentType': contentType, 'maxBytes': maxBytes},
          );
          return PresignDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/media/presign',
      );

  // ── D-266 M5 / D-351 · institutional authorization ────────────────────────
  // Collected inside event creation rather than on a separate screen. All three are keyed on an
  // eventId, which is why the wizard files them AFTER the create call rather than before it.

  /// The closed representative-role vocabulary, from the server that validates it. Never a copy held
  /// in the client — a list that drifts offers a role the API then refuses.
  Future<List<String>> representativeRoles() => guard(
        () async {
          final res = await _dio.get('/v1/events/authorization/roles');
          return (res.data as List).map((e) => e.toString()).toList();
        },
        endpoint: 'GET /v1/events/authorization/roles',
      );

  Future<PresignDto> presignAuthorizationDoc(String eventId, String contentType, int maxBytes) =>
      guard(
        () async {
          final res = await _dio.post(
            '/v1/events/$eventId/authorization/presign',
            data: {'contentType': contentType, 'maxBytes': maxBytes},
          );
          return PresignDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/events/{eventId}/authorization/presign',
      );

  /// Files the institution's written consent. `letterheadDocumentKey` is the key a presigned PUT
  /// returned — document bytes never travel through the API.
  Future<void> submitAuthorization(String eventId, Map<String, dynamic> body) => guard(
        () => _dio.post('/v1/events/$eventId/authorization', data: body),
        endpoint: 'POST /v1/events/{eventId}/authorization',
      );

  /// Uploads bytes to a presigned URL. Deliberately uses a **bare** Dio: the presigned URL is
  /// absolute and its signature is the credential, so attaching the session bearer token would
  /// leak it to the storage host for no benefit (`StorageEndpoints` ignores it entirely).
  Future<void> uploadToPresigned(PresignDto presign, List<int> bytes, String contentType) => guard(
        () => Dio().put(
          presign.url,
          data: Stream.fromIterable([bytes]),
          options: Options(
            headers: {
              ...presign.headers,
              Headers.contentTypeHeader: contentType,
              Headers.contentLengthHeader: bytes.length,
            },
          ),
        ),
        endpoint: 'PUT {presigned}',
      );

  Future<void> attachMedia(String orgId, String eventId, String kind, String key, String? caption) =>
      guard(
        () => _dio.post(
          '/v1/orgs/$orgId/events/$eventId/media',
          data: {'kind': kind, 'key': key, 'caption': ?caption},
        ),
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/media',
      );

  Future<void> removeMedia(String orgId, String eventId, String mediaId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/events/$eventId/media/$mediaId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/events/{eventId}/media/{mediaId}',
      );

  // ── Certificates (event-scoped roster; not org-scoped) ─────────────────────

  Future<List<CertificateRosterDto>> certificateRoster(String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/events/$eventId/certificates');
          return _list(res.data, CertificateRosterDto.fromJson);
        },
        endpoint: 'GET /v1/events/{eventId}/certificates',
      );

  Future<int> generateCertificates(String eventId, {String? templateId}) => guard(
        () async {
          final res = await _dio.post(
            '/v1/events/$eventId/certificates/generate',
            data: {'templateId': ?templateId},
          );
          final body = _map(res.data);
          return (body['generated'] as num?)?.toInt() ?? 0;
        },
        endpoint: 'POST /v1/events/{eventId}/certificates/generate',
      );

  Future<void> revokeCertificate(String certificateId, String reason) => guard(
        () => _dio.post('/v1/certificates/$certificateId/revoke', data: {'reason': reason}),
        endpoint: 'POST /v1/certificates/{id}/revoke',
      );

  // ── Membership claims (the caller's own) ───────────────────────────────────

  Future<List<MembershipClaimDto>> myMembershipClaims() => guard(
        () async {
          final res = await _dio.get('/v1/me/membership-claims');
          return _list(res.data, MembershipClaimDto.fromJson);
        },
        endpoint: 'GET /v1/me/membership-claims',
      );

  /// Evidence is required — the backend rejects an evidence-less claim with `invalid_evidence`
  /// (the same trap that made the web claim form always fail, D-055 G4).
  Future<void> submitMembershipClaim(
    String orgId, {
    required String claimedRole,
    required List<Map<String, dynamic>> documents,
    DateTime? validUntil,
  }) =>
      guard(
        () => _dio.post(
          '/v1/orgs/$orgId/membership-claims',
          data: {
            'claimedRole': claimedRole,
            'documents': documents,
            if (validUntil != null) 'validUntil': validUntil.toIso8601String(),
          },
        ),
        endpoint: 'POST /v1/orgs/{orgId}/membership-claims',
      );

  Future<PresignDto> presignClaimDoc(String orgId, String contentType, int maxBytes) => guard(
        () async {
          final res = await _dio.post(
            '/v1/orgs/$orgId/membership-claims/media/presign',
            data: {'contentType': contentType, 'maxBytes': maxBytes},
          );
          return PresignDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/orgs/{orgId}/membership-claims/media/presign',
      );

  // ── Shared decoding ────────────────────────────────────────────────────────

  static Map<String, dynamic> _map(Object? data) => (data as Map).cast<String, dynamic>();

  static List<T> _list<T>(Object? data, T Function(Map<String, dynamic>) fromJson) =>
      (data as List? ?? const []).cast<Map<String, dynamic>>().map(fromJson).toList();
}
