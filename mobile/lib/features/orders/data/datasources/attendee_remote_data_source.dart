import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../../../events/data/models/event_summary_dto.dart';
import '../../../organizer/data/models/event_manage_dto.dart' show AssignmentDto;
import '../models/attendee_dtos.dart';

/// The attendee's own API surface: ordering, reviews, waitlist, participations, staff assignments,
/// org invitations, eligibility and identity verification.
///
/// One datasource because these are all `/v1/me/*` or event-scoped attendee reads behind the same
/// bearer session — splitting them would mean seven near-identical classes over one auth model.
class AttendeeRemoteDataSource {
  AttendeeRemoteDataSource(this._dio);
  final Dio _dio;

  // ── Orders / checkout ──────────────────────────────────────────────────────

  /// Creates an order for a ticket type.
  ///
  /// [idempotencyKey] rides the standard `Idempotency-Key` header (V3 §17.1). It matters: a
  /// double-tap or a retry after a dropped response must not buy two tickets, and the server
  /// claims the key atomically.
  Future<OrderDto> createOrder(
    String eventId, {
    required String ticketTypeId,
    required String idempotencyKey,
    int? groupSize,
    String? displayName,
    Map<String, String>? answers,
  }) =>
      guard(
        () async {
          final res = await _dio.post(
            '/v1/events/$eventId/orders',
            data: {
              'ticketTypeId': ticketTypeId,
              'groupSize': ?groupSize,
              'displayName': ?displayName,
              'answers': ?answers,
            },
            options: Options(headers: {'Idempotency-Key': idempotencyKey}),
          );
          return OrderDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/events/{eventId}/orders',
      );

  Future<List<OrderDto>> myOrders() => guard(
        () async {
          final res = await _dio.get('/v1/orders');
          return _list(res.data, OrderDto.fromJson);
        },
        endpoint: 'GET /v1/orders',
      );

  // ── Saved events (A1/D-064) ────────────────────────────────────────────────

  /// Returns `EventEndpoints.ToSummaryJson` rows — the **same** shape as every other event list,
  /// so this reuses `EventSummaryDto` rather than introducing a saved-event model.
  Future<List<EventSummaryDto>> savedEvents({int limit = 50}) => guard(
        () async {
          final res = await _dio.get('/v1/me/saved', queryParameters: {'limit': limit});
          return _list(res.data, EventSummaryDto.fromJson);
        },
        endpoint: 'GET /v1/me/saved',
      );

  /// Idempotent server-side; saving requires a visible Published + Public event, else `not_found`.
  Future<void> saveEvent(String eventId) => guard(
        () => _dio.post('/v1/events/$eventId/save'),
        endpoint: 'POST /v1/events/{eventId}/save',
      );

  Future<void> unsaveEvent(String eventId) => guard(
        () => _dio.delete('/v1/events/$eventId/save'),
        endpoint: 'DELETE /v1/events/{eventId}/save',
      );

  // ── Reviews ────────────────────────────────────────────────────────────────

  Future<ReviewPageDto> reviews(String eventId, {int page = 1, int pageSize = 20}) => guard(
        () async {
          final res = await _dio.get(
            '/v1/events/$eventId/reviews',
            queryParameters: {'page': page, 'pageSize': pageSize},
          );
          return ReviewPageDto.fromJson(_map(res.data));
        },
        endpoint: 'GET /v1/events/{eventId}/reviews',
      );

  /// Upsert — the backend treats a second POST from the same user as an edit.
  /// Requires an issued ticket for the event, else `review_requires_ticket` (403).
  Future<ReviewDto> upsertReview(
    String eventId, {
    required int rating,
    String? title,
    String? body,
    bool isAnonymous = false,
  }) =>
      guard(
        () async {
          final res = await _dio.post('/v1/events/$eventId/reviews', data: {
            'rating': rating,
            'title': ?title,
            'body': ?body,
            'isAnonymous': isAnonymous,
          });
          return ReviewDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/events/{eventId}/reviews',
      );

  Future<void> deleteMyReview(String eventId) => guard(
        () => _dio.delete('/v1/events/$eventId/reviews/mine'),
        endpoint: 'DELETE /v1/events/{eventId}/reviews/mine',
      );

  // ── Waitlist ───────────────────────────────────────────────────────────────

  /// Join only works once a ticket type is sold out — otherwise `tickets_available` (409).
  Future<WaitlistEntryDto> joinWaitlist(String eventId, String ticketTypeId) => guard(
        () async {
          final res =
              await _dio.post('/v1/events/$eventId/ticket-types/$ticketTypeId/waitlist');
          return WaitlistEntryDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/events/{eventId}/ticket-types/{id}/waitlist',
      );

  Future<void> leaveWaitlist(String eventId, String ticketTypeId) => guard(
        () => _dio.delete('/v1/events/$eventId/ticket-types/$ticketTypeId/waitlist'),
        endpoint: 'DELETE /v1/events/{eventId}/ticket-types/{id}/waitlist',
      );

  Future<List<WaitlistEntryDto>> myWaitlist() => guard(
        () async {
          final res = await _dio.get('/v1/me/waitlist');
          return _list(res.data, WaitlistEntryDto.fromJson);
        },
        endpoint: 'GET /v1/me/waitlist',
      );

  // ── Refunds ────────────────────────────────────────────────────────────────

  /// The caller's own refunds across every event (D-199).
  Future<List<RefundDto>> myRefunds() => guard(
        () async {
          final res = await _dio.get('/v1/refunds');
          return _list(res.data, RefundDto.fromJson);
        },
        endpoint: 'GET /v1/refunds',
      );

  // ── Participations ─────────────────────────────────────────────────────────

  Future<List<ParticipationDto>> myParticipations() => guard(
        () async {
          final res = await _dio.get('/v1/me/participations');
          return _list(res.data, ParticipationDto.fromJson);
        },
        endpoint: 'GET /v1/me/participations',
      );

  Future<void> respondToParticipation(String participantId, {required bool accept}) => guard(
        () => _dio.post('/v1/participants/$participantId/respond', data: {'accept': accept}),
        endpoint: 'POST /v1/participants/{participantId}/respond',
      );

  // ── Event staff assignments (D-319) ────────────────────────────────────────

  /// Every assignment naming the signed-in user — invited and already accepted. Unlike an org
  /// invitation this is keyed by **id**, not a token, so this list is directly actionable.
  Future<List<AssignmentDto>> myAssignments() => guard(
        () async {
          final res = await _dio.get('/v1/me/assignments');
          return _list(res.data, AssignmentDto.fromJson);
        },
        endpoint: 'GET /v1/me/assignments',
      );

  /// The invitee is the only actor the API permits here — the organiser who sent the invite is
  /// refused, and so is a platform admin.
  Future<AssignmentDto> respondToAssignment(String id, {required bool accept}) => guard(
        () async {
          final res = await _dio.post('/v1/assignments/$id/${accept ? 'accept' : 'decline'}');
          return AssignmentDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/assignments/{id}/{accept|decline}',
      );

  // ── Org invitations ────────────────────────────────────────────────────────

  Future<List<OrgInvitationDto>> myOrgInvitations() => guard(
        () async {
          final res = await _dio.get('/v1/me/org-invitations');
          return _list(res.data, OrgInvitationDto.fromJson);
        },
        endpoint: 'GET /v1/me/org-invitations',
      );

  /// Keyed by **token**, not id — the token arrives in the invite link, never in the list response.
  Future<OrgInvitationDto> acceptOrgInvitation(String token) => guard(
        () async {
          final res = await _dio.post('/v1/org-invitations/$token/accept');
          return OrgInvitationDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/org-invitations/{token}/accept',
      );

  Future<void> declineOrgInvitation(String token) => guard(
        () => _dio.post('/v1/org-invitations/$token/decline'),
        endpoint: 'POST /v1/org-invitations/{token}/decline',
      );

  // ── Eligibility (V3 §4.4 audience rules) ───────────────────────────────────

  Future<EligibilityDto> eligibility(String eventId) => guard(
        () async {
          final res = await _dio.get('/v1/events/$eventId/eligibility');
          return EligibilityDto.fromJson(_map(res.data));
        },
        endpoint: 'GET /v1/events/{eventId}/eligibility',
      );

  // ── Identity verification (M3/D-042) ───────────────────────────────────────

  Future<IdentityStatusDto> identityStatus() => guard(
        () async {
          final res = await _dio.get('/v1/me/identity');
          return IdentityStatusDto.fromJson(_map(res.data));
        },
        endpoint: 'GET /v1/me/identity',
      );

  /// The caller's own verification history, newest first. Caller-scoped server-side — the subject is
  /// resolved from the token, never from a parameter, because a rejection is a private fact about a
  /// person and an id-addressed history would be a disclosure surface.
  Future<List<IdentityHistoryEntryDto>> identityHistory({int limit = 50}) => guard(
        () async {
          final res = await _dio.get(
            '/v1/me/identity/history',
            queryParameters: {'limit': limit},
          );
          return (res.data as List)
              .cast<Map<String, dynamic>>()
              .map(IdentityHistoryEntryDto.fromJson)
              .toList();
        },
        endpoint: 'GET /v1/me/identity/history',
      );

  Future<IdentityStatusDto> submitGovernmentId({
    required String kind,
    required String idNumber,
    required String name,
  }) =>
      guard(
        () async {
          final res = await _dio.post('/v1/me/identity/government-id',
              data: {'kind': kind, 'idNumber': idNumber, 'name': name});
          return IdentityStatusDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/me/identity/government-id',
      );

  Future<IdentityStatusDto> submitPan({required String pan, required String name}) => guard(
        () async {
          final res = await _dio.post('/v1/me/identity/pan', data: {'pan': pan, 'name': name});
          return IdentityStatusDto.fromJson(_map(res.data));
        },
        endpoint: 'POST /v1/me/identity/pan',
      );

  static Map<String, dynamic> _map(Object? data) => (data as Map).cast<String, dynamic>();

  static List<T> _list<T>(Object? data, T Function(Map<String, dynamic>) fromJson) =>
      (data as List? ?? const []).cast<Map<String, dynamic>>().map(fromJson).toList();
}
