import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/event_content_dto.dart';
import '../models/event_manage_dto.dart';
import '../models/org_dto.dart';

class OrgRemoteDataSource {
  OrgRemoteDataSource(this._dio);
  final Dio _dio;

  /// The organizations the caller may represent — `GET /v1/me/representations` (D-268). Named for
  /// what it answers. Self-representation is excluded server-side: it is not an organization.
  Future<List<RepresentationDto>> myRepresentations() => guard(() async {
        final res = await _dio.get('/v1/me/representations');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(RepresentationDto.fromJson)
            .toList();
      }, endpoint: 'GET /v1/me/representations');

  Future<OrgDto> org(String orgId) => guard(() async {
        final res = await _dio.get('/v1/orgs/$orgId');
        return OrgDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  // `POST /v1/orgs` had exactly one caller: the create-organization page, deleted with the org-first
  // workflow (D-267). Users do not mint organizations — they submit a representation request an admin
  // verifies (D-074/D-075) — and self-representation is resolved server-side, never posted from here.

  // ── D-074/D-075 · representing an institution that is not on Kurx yet ──────
  // D-382 — both of these existed on web and on neither Flutter screen, which closed a loop: the
  // wizard told an organiser with no representation to request one "from your profile", and the
  // profile's Representing page told them they are asked during event creation. Since D-379 an event
  // cannot be created without a verified organization, so that loop was the end of the road.

  /// Presigns the proof of affiliation. **User-scoped**: the institution does not exist yet, so there
  /// is no orgId to scope it to — unlike every other org document presign.
  Future<PresignDto> presignRepresentationDoc(String contentType, int maxBytes) => guard(
        () async {
          final res = await _dio.post(
            '/v1/orgs/representation-requests/media/presign',
            data: {'contentType': contentType, 'maxBytes': maxBytes},
          );
          return PresignDto.fromJson((res.data as Map).cast<String, dynamic>());
        },
        endpoint: 'POST /v1/orgs/representation-requests/media/presign',
      );

  /// Stages a HIDDEN `PendingReview` organization plus its evidence. The caller becomes a *pending*
  /// representative — never an owner, because organizations have no account — and an admin verifies
  /// the institution before it joins the registry or can back an event.
  Future<OrgDto> submitRepresentationRequest(Map<String, dynamic> body) => guard(
        () async {
          final res = await _dio.post('/v1/orgs/representation-requests', data: body);
          return OrgDto.fromJson((res.data as Map).cast<String, dynamic>());
        },
        endpoint: 'POST /v1/orgs/representation-requests',
      );

  Future<WalletDto> wallet(String orgId) => guard(() async {
        final res = await _dio.get('/v1/orgs/$orgId/wallet');
        return WalletDto.fromJson((res.data as Map).cast<String, dynamic>());
      }, endpoint: 'GET /v1/orgs/{orgId}/wallet');

  Future<List<OrgMemberDto>> members(String orgId) => guard(() async {
        final res = await _dio.get('/v1/orgs/$orgId/members');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(OrgMemberDto.fromJson)
            .toList();
      });

  /// Records a payout request — `POST /v1/orgs/{orgId}/wallet/withdraw`.
  ///
  /// Creates a `Withdrawal` row at status `requested`. Nothing in the backend advances it beyond
  /// that today (no payout executor, mock gateway), so the caller must not present this as money
  /// having moved.
  Future<String> requestWithdrawal(String orgId, int amountPaise) => guard(() async {
        final res = await _dio.post('/v1/orgs/$orgId/wallet/withdraw',
            data: {'amountPaise': amountPaise});
        return (res.data as Map).cast<String, dynamic>()['withdrawalId'].toString();
      }, endpoint: 'POST /v1/orgs/{orgId}/wallet/withdraw');

  /// Invites a teammate to the organisation — `POST /v1/orgs/{orgId}/invitations`.
  /// Owner may invite any role; Manager may invite Staff only (D-015), enforced server-side.
  Future<void> inviteMember(String orgId, {required String phone, required String role}) => guard(
        () => _dio.post('/v1/orgs/$orgId/invitations', data: {'phone': phone, 'role': role}),
        endpoint: 'POST /v1/orgs/{orgId}/invitations',
      );

  Future<List<LedgerEntryDto>> ledger(String orgId) => guard(() async {
        final res = await _dio.get('/v1/orgs/$orgId/wallet/ledger');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(LedgerEntryDto.fromJson)
            .toList();
      });
}

class EventManageRemoteDataSource {
  EventManageRemoteDataSource(this._dio);
  final Dio _dio;

  /// Every event the caller hosts, across every organisation they represent — `GET /v1/me/events`
  /// (D-267). One call, no organisation to pick first, which is what lets Workspace be a list of the
  /// person's events instead of a browser over their organisations.
  ///
  /// The payload is `{ items, total }`, not a bare array. The previous version read `res.data as List`
  /// against the org-scoped route, which has always answered with the same envelope — so this threw on
  /// every call.
  Future<List<EventManageDto>> myEvents() => guard(() async {
        final res = await _dio.get('/v1/me/events');
        final items = (res.data as Map).cast<String, dynamic>()['items'] as List;
        return items
            .cast<Map<String, dynamic>>()
            .map(EventManageDto.fromJson)
            .toList();
      }, endpoint: 'GET /v1/me/events');

  /// One event by its own id — `GET /v1/events/{eventId}` (D-267). No organisation in the path: the
  /// caller's role on the event's own organisation authorizes the read.
  Future<EventManageDto> event(String eventId) => guard(() async {
        final res = await _dio.get('/v1/events/$eventId');
        return EventManageDto.fromJson(
            (res.data as Map).cast<String, dynamic>());
      }, endpoint: 'GET /v1/events/{eventId}');

  /// Creates a draft event — `POST /v1/events` (D-267). [representingOrgId] null means **Personal**:
  /// the server files it under the caller's own "just me" organisation. No organisation is required to
  /// reach this call.
  ///
  /// Returns the created event's **id** rather than a parsed row: the create reply is the full
  /// `ToEventJson` detail shape, which is far wider than [EventManageDto] (the list shape), and
  /// parsing it into the narrower DTO would silently drop most of it. The caller navigates to the
  /// manage screen, which re-reads the event properly.
  /// Returns the new event's id **and** the organization it represents.
  ///
  /// The org id is needed immediately: ticket types are created at
  /// `/v1/orgs/{orgId}/events/{eventId}/ticket-types`, and the wizard creates the event's first ticket
  /// straight after the event (D-305). Returning only the id — what this did before — meant the caller
  /// had to re-fetch the event just to learn something the create response already carried.
  Future<({String id, String orgId})> createEvent(
          String? representingOrgId, Map<String, dynamic> body) =>
      guard(() async {
        // Sent even when null — JSON null binds to the nullable field, which is exactly "Personal".
        final res = await _dio.post('/v1/events', data: {
          ...body,
          'representingOrgId': representingOrgId,
        });
        final map = (res.data as Map).cast<String, dynamic>();
        return (id: map['id'] as String, orgId: map['representing_org_id'] as String);
      }, endpoint: 'POST /v1/events');

  /// Drives the `Draft → PendingReview → UnderReview → Approved → Published → Closed → Archived`
  /// workflow (D-266 M4).
  Future<void> transitionEvent(String orgId, String eventId, String action) => guard(
        () => _dio.post('/v1/orgs/$orgId/events/$eventId/transition', data: {'action': action}),
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/transition',
      );

  /// Discards a draft — `DELETE /v1/orgs/{orgId}/events/{eventId}`.
  ///
  /// The app could create an event and then never get rid of it: web and the admin console both had
  /// this call, mobile had delete for speakers, sponsors, sessions, ticket types and media but none for
  /// the event itself. So a draft started on a phone stayed in that person's list forever.
  ///
  /// **Soft (D-364).** The row is retained with `DeletedAt` set and a global query filter takes it out
  /// of every read; nothing cascades. The server refuses `not_draft` past Draft and `event_has_history`
  /// if the event ever carried an order, ticket or registration (D-363 §3) — both are refusals this
  /// screen has copy for, so the app offers the button and lets the server decide.
  Future<void> deleteEvent(String orgId, String eventId) => guard(
        () => _dio.delete('/v1/orgs/$orgId/events/$eventId'),
        endpoint: 'DELETE /v1/orgs/{orgId}/events/{eventId}',
      );

  /// Registrations for an event — the shadow of Order/Ticket, which is what an organiser triages
  /// when approval is manual. Distinct from `attendees`, which lists issued admissions.
  Future<List<RegistrationDto>> registrations(String orgId, String eventId) =>
      guard(() async {
        final res =
            await _dio.get('/v1/orgs/$orgId/events/$eventId/registrations');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(RegistrationDto.fromJson)
            .toList();
      });

  Future<List<AttendeeDto>> attendees(String orgId, String eventId) =>
      guard(() async {
        final res =
            await _dio.get('/v1/orgs/$orgId/events/$eventId/attendees');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AttendeeDto.fromJson)
            .toList();
      });

  Future<List<InvitationDto>> invitations(String orgId, String eventId) =>
      guard(() async {
        // Invitations are **event-scoped**, not org-scoped: InvitationEndpoints maps
        // /v1/events/{eventId}/invitations. The org-scoped path this used to call does not exist,
        // so this screen had only ever rendered a 404 error state.
        final res = await _dio.get('/v1/events/$eventId/invitations');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(InvitationDto.fromJson)
            .toList();
      });

  Future<List<AnnouncementDto>> announcements(String orgId, String eventId) =>
      guard(() async {
        // Event-scoped for the same reason as invitations — AnnouncementEndpoints maps
        // /v1/events/{eventId}/announcements and has no org-scoped route.
        final res = await _dio.get('/v1/events/$eventId/announcements');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AnnouncementDto.fromJson)
            .toList();
      });

  /// Adds one guest to the invite list. `channel` is `email` / `whatsapp` / `sms`; the backend
  /// validates that the matching contact field is present.
  Future<void> addInvitation(
    String eventId, {
    required String channel,
    String? name,
    String? email,
    String? phone,
  }) =>
      guard(
        () => _dio.post('/v1/events/$eventId/invitations', data: {
          'channel': channel,
          'name': ?name,
          'email': ?email,
          'phone': ?phone,
        }),
        endpoint: 'POST /v1/events/{eventId}/invitations',
      );

  /// Sends pending invitations. A null [invitationIds] sends every unsent one — the backend rate-
  /// limits this under the `"heavy"` policy, so it is confirmation-gated in the UI.
  Future<void> sendInvitations(String eventId, {List<String>? invitationIds}) => guard(
        () => _dio.post('/v1/events/$eventId/invitations/send',
            data: {'invitationIds': ?invitationIds}),
        endpoint: 'POST /v1/events/{eventId}/invitations/send',
      );

  Future<void> resendInvitation(String invitationId) => guard(
        () => _dio.post('/v1/invitations/$invitationId/resend'),
        endpoint: 'POST /v1/invitations/{invitationId}/resend',
      );

  Future<void> revokeInvitation(String invitationId) => guard(
        () => _dio.delete('/v1/invitations/$invitationId'),
        endpoint: 'DELETE /v1/invitations/{invitationId}',
      );

  /// Creates an announcement. `audience` is one of `AllRegistrants` / `CheckedIn` /
  /// `NotCheckedIn` / `TicketType`; omitting [scheduledAt] sends immediately.
  Future<void> createAnnouncement(
    String eventId, {
    required String title,
    required String body,
    required String audience,
    required List<String> channels,
    DateTime? scheduledAt,
  }) =>
      guard(
        () => _dio.post('/v1/events/$eventId/announcements', data: {
          'title': title,
          'body': body,
          'audience': audience,
          'channels': channels,
          'scheduledAt': ?scheduledAt?.toUtc().toIso8601String(),
          'includeChildEvents': false,
        }),
        endpoint: 'POST /v1/events/{eventId}/announcements',
      );

  Future<AnalyticsDto> analytics(String orgId, String eventId) =>
      guard(() async {
        final res =
            await _dio.get('/v1/orgs/$orgId/events/$eventId/analytics');
        return AnalyticsDto.fromJson(
            (res.data as Map).cast<String, dynamic>());
      });

  Future<List<AssignmentDto>> assignments(String orgId, String eventId) =>
      guard(() async {
        final res =
            await _dio.get('/v1/orgs/$orgId/events/$eventId/assignments');
        return (res.data as List)
            .cast<Map<String, dynamic>>()
            .map(AssignmentDto.fromJson)
            .toList();
      });

  /// Assigns a staff/volunteer role by phone — `POST /v1/orgs/{orgId}/events/{eventId}/assignments`.
  /// Unknown phones get a provisional user row, same as an org invite (D-012/D-015).
  Future<void> assign(
    String orgId,
    String eventId, {
    required String phone,
    required String role,
    String? customRole,
    String? notes,
  }) =>
      guard(
        () => _dio.post('/v1/orgs/$orgId/events/$eventId/assignments', data: {
          'phone': phone,
          'role': role,
          'customRole': ?customRole,
          'notes': ?notes,
        }),
        endpoint: 'POST /v1/orgs/{orgId}/events/{eventId}/assignments',
      );

  Future<void> removeAssignment(String orgId, String eventId, String id) => guard(
        () => _dio.delete('/v1/orgs/$orgId/events/$eventId/assignments/$id'),
        endpoint: 'DELETE /v1/orgs/{orgId}/events/{eventId}/assignments/{id}',
      );
}
