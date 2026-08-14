import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../../organizer/data/models/event_manage_dto.dart' show AssignmentDto;
import '../../data/datasources/attendee_remote_data_source.dart';
import '../../data/models/attendee_dtos.dart';

/// Providers for the attendee's own surfaces.
///
/// Reads are `autoDispose` so a screen refetches on entry — a waitlist position, a review count and
/// an eligibility verdict are all server-owned and change without the client acting.

final attendeeSourceProvider =
    Provider((ref) => AttendeeRemoteDataSource(ref.watch(dioProvider)));

final myOrdersProvider = FutureProvider.autoDispose<List<OrderDto>>(
    (ref) => ref.watch(attendeeSourceProvider).myOrders());

final eventReviewsProvider = FutureProvider.autoDispose.family<ReviewPageDto, String>(
    (ref, eventId) => ref.watch(attendeeSourceProvider).reviews(eventId));

final myWaitlistProvider = FutureProvider.autoDispose<List<WaitlistEntryDto>>(
    (ref) => ref.watch(attendeeSourceProvider).myWaitlist());

final myRefundsProvider = FutureProvider.autoDispose<List<RefundDto>>(
    (ref) => ref.watch(attendeeSourceProvider).myRefunds());

final myParticipationsProvider = FutureProvider.autoDispose<List<ParticipationDto>>(
    (ref) => ref.watch(attendeeSourceProvider).myParticipations());

final myOrgInvitationsProvider = FutureProvider.autoDispose<List<OrgInvitationDto>>(
    (ref) => ref.watch(attendeeSourceProvider).myOrgInvitations());

/// Staff assignments naming the signed-in user (D-319). Actionable here, unlike org invitations —
/// those are keyed by a token that only reaches the invitee in the invite message.
final myAssignmentsProvider = FutureProvider.autoDispose<List<AssignmentDto>>(
    (ref) => ref.watch(attendeeSourceProvider).myAssignments());

/// Audience-rule verdict for one event (V3 §4.4).
///
/// Errors are **not** swallowed into "allowed" — an eligibility check that failed is not the same
/// as an eligibility check that passed, and treating it as permissive would show a Book button the
/// server will then refuse.
final eventEligibilityProvider = FutureProvider.autoDispose.family<EligibilityDto, String>(
    (ref, eventId) => ref.watch(attendeeSourceProvider).eligibility(eventId));

final identityStatusProvider = FutureProvider.autoDispose<IdentityStatusDto>(
    (ref) => ref.watch(attendeeSourceProvider).identityStatus());

/// Verification history. Its own provider so a failure here leaves the status rows on screen — the
/// trail is context, and losing it must never take down the states it explains.
final identityHistoryProvider =
    FutureProvider.autoDispose<List<IdentityHistoryEntryDto>>(
        (ref) => ref.watch(attendeeSourceProvider).identityHistory());


/// Mutations. Each invalidates only what it can have changed, and lets [ApiError] propagate so the
/// screen shows the server's own reason (`review_requires_ticket`, `tickets_available`,
/// `already_waitlisted`, `payments_not_enabled`).
class AttendeeActions {
  const AttendeeActions(this._ref);
  final Ref _ref;

  AttendeeRemoteDataSource get _api => _ref.read(attendeeSourceProvider);

  Future<OrderDto> createOrder(
    String eventId, {
    required String ticketTypeId,
    required String idempotencyKey,
    Map<String, String>? answers,
  }) async {
    final order = await _api.createOrder(
      eventId,
      ticketTypeId: ticketTypeId,
      idempotencyKey: idempotencyKey,
      answers: answers,
    );
    _ref.invalidate(myOrdersProvider);
    return order;
  }

  Future<void> upsertReview(
    String eventId, {
    required int rating,
    String? title,
    String? body,
    bool isAnonymous = false,
  }) async {
    await _api.upsertReview(eventId,
        rating: rating, title: title, body: body, isAnonymous: isAnonymous);
    _ref.invalidate(eventReviewsProvider(eventId));
  }

  Future<void> deleteMyReview(String eventId) async {
    await _api.deleteMyReview(eventId);
    _ref.invalidate(eventReviewsProvider(eventId));
  }

  Future<void> joinWaitlist(String eventId, String ticketTypeId) async {
    await _api.joinWaitlist(eventId, ticketTypeId);
    _ref.invalidate(myWaitlistProvider);
  }

  Future<void> leaveWaitlist(String eventId, String ticketTypeId) async {
    await _api.leaveWaitlist(eventId, ticketTypeId);
    _ref.invalidate(myWaitlistProvider);
  }

  Future<void> respondToParticipation(String participantId, {required bool accept}) async {
    await _api.respondToParticipation(participantId, accept: accept);
    _ref.invalidate(myParticipationsProvider);
  }

  /// Accept or decline a staff assignment (D-319). Accepting is what satisfies §14.2's go-live gate,
  /// so this is the one call standing between a published event and Live.
  Future<void> respondToAssignment(String id, {required bool accept}) async {
    await _api.respondToAssignment(id, accept: accept);
    _ref.invalidate(myAssignmentsProvider);
  }

  Future<void> acceptOrgInvitation(String token) async {
    await _api.acceptOrgInvitation(token);
    _ref.invalidate(myOrgInvitationsProvider);
  }

  Future<void> declineOrgInvitation(String token) async {
    await _api.declineOrgInvitation(token);
    _ref.invalidate(myOrgInvitationsProvider);
  }

  Future<void> submitGovernmentId({
    required String kind,
    required String idNumber,
    required String name,
  }) async {
    await _api.submitGovernmentId(kind: kind, idNumber: idNumber, name: name);
    _ref.invalidate(identityStatusProvider);
  }

  Future<void> submitPan({required String pan, required String name}) async {
    await _api.submitPan(pan: pan, name: name);
    _ref.invalidate(identityStatusProvider);
  }
}

final attendeeActionsProvider = Provider((ref) => AttendeeActions(ref));
