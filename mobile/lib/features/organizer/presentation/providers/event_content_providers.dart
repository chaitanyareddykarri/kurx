import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../../events/data/models/ticket_type_dto.dart';
import '../../data/datasources/event_content_remote_data_source.dart';
import '../../data/models/event_content_dto.dart';

/// Providers for the organiser's event-content screens.
///
/// Every list is an `autoDispose` `FutureProvider` so a screen refetches on entry and drops its
/// cache on exit — the organiser surfaces are edited by several people at once, and a stale roster
/// is worse than a spinner. Writes go through [EventContentActions], which invalidates the exact
/// providers a mutation can affect rather than blanket-refreshing the tree.

final eventContentSourceProvider =
    Provider((ref) => EventContentRemoteDataSource(ref.watch(dioProvider)));

/// Identifies an org-scoped event. Records give value equality for free, which is what makes
/// `family` caching work — two identical `(orgId, eventId)` pairs share one cache entry.
typedef OrgEventRef = ({String orgId, String eventId});

// ── Reads ────────────────────────────────────────────────────────────────────

final orgSpeakersProvider = FutureProvider.autoDispose.family<List<SpeakerDto>, String>(
    (ref, orgId) => ref.watch(eventContentSourceProvider).orgSpeakers(orgId));

final eventSpeakersProvider = FutureProvider.autoDispose.family<List<SpeakerDto>, OrgEventRef>(
    (ref, e) => ref.watch(eventContentSourceProvider).eventSpeakers(e.orgId, e.eventId));

final orgSponsorsProvider = FutureProvider.autoDispose.family<List<SponsorDto>, String>(
    (ref, orgId) => ref.watch(eventContentSourceProvider).orgSponsors(orgId));

final eventSponsorsProvider = FutureProvider.autoDispose.family<List<SponsorDto>, OrgEventRef>(
    (ref, e) => ref.watch(eventContentSourceProvider).eventSponsors(e.orgId, e.eventId));

final orgVenuesProvider = FutureProvider.autoDispose.family<List<VenueDto>, String>(
    (ref, orgId) => ref.watch(eventContentSourceProvider).venues(orgId));

final eventSessionsProvider = FutureProvider.autoDispose.family<List<SessionDto>, OrgEventRef>(
    (ref, e) => ref.watch(eventContentSourceProvider).sessions(e.orgId, e.eventId));

final orgTicketTypesProvider = FutureProvider.autoDispose.family<List<TicketTypeDto>, OrgEventRef>(
    (ref, e) => ref.watch(eventContentSourceProvider).ticketTypes(e.orgId, e.eventId));

final eventMediaProvider = FutureProvider.autoDispose.family<List<EventMediaDto>, OrgEventRef>(
    (ref, e) => ref.watch(eventContentSourceProvider).media(e.orgId, e.eventId));

/// The event's presigned banner URL, or null (D-302).
final eventBannerProvider = FutureProvider.autoDispose.family<String?, OrgEventRef>(
    (ref, e) => ref.watch(eventContentSourceProvider).bannerUrl(e.orgId, e.eventId));

final myMembershipClaimsProvider = FutureProvider.autoDispose<List<MembershipClaimDto>>(
    (ref) => ref.watch(eventContentSourceProvider).myMembershipClaims());

// ── Writes ───────────────────────────────────────────────────────────────────

/// Mutations, each invalidating only what it can have changed.
///
/// Nothing here swallows an error: every method lets the [ApiError] from `guard` propagate so the
/// calling screen shows the real reason (`forbidden`, `ticket_type_has_sales`, `invalid_evidence`)
/// instead of a generic failure — or worse, a silent success.
class EventContentActions {
  const EventContentActions(this._ref);
  final Ref _ref;

  EventContentRemoteDataSource get _api => _ref.read(eventContentSourceProvider);

  // Speakers
  Future<void> createSpeaker(String orgId, Map<String, dynamic> body) async {
    await _api.createSpeaker(orgId, body);
    _ref.invalidate(orgSpeakersProvider(orgId));
  }

  Future<void> updateSpeaker(String orgId, String speakerId, Map<String, dynamic> body) async {
    await _api.updateSpeaker(orgId, speakerId, body);
    _ref.invalidate(orgSpeakersProvider(orgId));
  }

  Future<void> deleteSpeaker(String orgId, String speakerId) async {
    await _api.deleteSpeaker(orgId, speakerId);
    _ref.invalidate(orgSpeakersProvider(orgId));
  }

  Future<void> assignSpeaker(OrgEventRef e, String speakerId, {String? sessionId}) async {
    await _api.assignSpeaker(e.orgId, e.eventId, speakerId, sessionId: sessionId);
    _ref.invalidate(eventSpeakersProvider(e));
  }

  Future<void> unassignSpeaker(OrgEventRef e, String speakerId) async {
    await _api.unassignSpeaker(e.orgId, e.eventId, speakerId);
    _ref.invalidate(eventSpeakersProvider(e));
  }

  // Sponsors
  Future<void> createSponsor(String orgId, Map<String, dynamic> body) async {
    await _api.createSponsor(orgId, body);
    _ref.invalidate(orgSponsorsProvider(orgId));
  }

  Future<void> updateSponsor(String orgId, String sponsorId, Map<String, dynamic> body) async {
    await _api.updateSponsor(orgId, sponsorId, body);
    _ref.invalidate(orgSponsorsProvider(orgId));
  }

  Future<void> deleteSponsor(String orgId, String sponsorId) async {
    await _api.deleteSponsor(orgId, sponsorId);
    _ref.invalidate(orgSponsorsProvider(orgId));
  }

  Future<void> assignSponsor(OrgEventRef e, String sponsorId, {int? sort}) async {
    await _api.assignSponsor(e.orgId, e.eventId, sponsorId, sort: sort);
    _ref.invalidate(eventSponsorsProvider(e));
  }

  Future<void> unassignSponsor(OrgEventRef e, String sponsorId) async {
    await _api.unassignSponsor(e.orgId, e.eventId, sponsorId);
    _ref.invalidate(eventSponsorsProvider(e));
  }

  // Venues
  Future<void> createVenue(String orgId, Map<String, dynamic> body) async {
    await _api.createVenue(orgId, body);
    _ref.invalidate(orgVenuesProvider(orgId));
  }

  Future<void> updateVenue(String orgId, String venueId, Map<String, dynamic> body) async {
    await _api.updateVenue(orgId, venueId, body);
    _ref.invalidate(orgVenuesProvider(orgId));
  }

  Future<void> deleteVenue(String orgId, String venueId) async {
    await _api.deleteVenue(orgId, venueId);
    _ref.invalidate(orgVenuesProvider(orgId));
  }

  // Sessions
  Future<void> createSession(OrgEventRef e, Map<String, dynamic> body) async {
    await _api.createSession(e.orgId, e.eventId, body);
    _ref.invalidate(eventSessionsProvider(e));
  }

  Future<void> updateSession(OrgEventRef e, String sessionId, Map<String, dynamic> body) async {
    await _api.updateSession(e.orgId, e.eventId, sessionId, body);
    _ref.invalidate(eventSessionsProvider(e));
  }

  Future<void> deleteSession(OrgEventRef e, String sessionId) async {
    await _api.deleteSession(e.orgId, e.eventId, sessionId);
    _ref.invalidate(eventSessionsProvider(e));
  }

  // Ticket types
  Future<void> createTicketType(OrgEventRef e, Map<String, dynamic> body) async {
    await _api.createTicketType(e.orgId, e.eventId, body);
    _ref.invalidate(orgTicketTypesProvider(e));
  }

  Future<void> updateTicketType(OrgEventRef e, String id, Map<String, dynamic> body) async {
    await _api.updateTicketType(e.orgId, e.eventId, id, body);
    _ref.invalidate(orgTicketTypesProvider(e));
  }

  Future<void> deleteTicketType(OrgEventRef e, String id) async {
    await _api.deleteTicketType(e.orgId, e.eventId, id);
    _ref.invalidate(orgTicketTypesProvider(e));
  }

  // Media — presign, PUT the bytes, then attach the key. Three calls, one user action; a failure
  // at any step surfaces as itself rather than as a half-uploaded gallery.
  Future<void> uploadMedia(
    OrgEventRef e, {
    required List<int> bytes,
    required String contentType,
    required String kind,
    String? caption,
  }) async {
    final presign = await _api.presignMedia(e.orgId, e.eventId, contentType, bytes.length);
    await _api.uploadToPresigned(presign, bytes, contentType);
    await _api.attachMedia(e.orgId, e.eventId, kind, presign.key, caption);
    _ref.invalidate(eventMediaProvider(e));
  }

  /// Banner: presign, PUT, then PATCH the key onto the event (D-302). Deliberately **not** an
  /// `attachMedia` call — `MediaKind.Banner` exists but nothing reads gallery rows for a card, so
  /// attaching one would report success and leave every surface unchanged.
  Future<void> uploadBanner(
    OrgEventRef e, {
    required List<int> bytes,
    required String contentType,
  }) async {
    final presign = await _api.presignMedia(e.orgId, e.eventId, contentType, bytes.length);
    await _api.uploadToPresigned(presign, bytes, contentType);
    await _api.updateBanner(e.orgId, e.eventId, presign.key);
    _ref.invalidate(eventBannerProvider(e));
  }

  /// Empty string is the clear signal — null would mean "this PATCH did not mention the banner".
  Future<void> removeBanner(OrgEventRef e) async {
    await _api.updateBanner(e.orgId, e.eventId, '');
    _ref.invalidate(eventBannerProvider(e));
  }

  Future<void> removeMedia(OrgEventRef e, String mediaId) async {
    await _api.removeMedia(e.orgId, e.eventId, mediaId);
    _ref.invalidate(eventMediaProvider(e));
  }

  // Membership claims
  Future<void> submitMembershipClaim(
    String orgId, {
    required String claimedRole,
    required List<Map<String, dynamic>> documents,
    DateTime? validUntil,
  }) async {
    await _api.submitMembershipClaim(
      orgId,
      claimedRole: claimedRole,
      documents: documents,
      validUntil: validUntil,
    );
    _ref.invalidate(myMembershipClaimsProvider);
  }

  /// Presign → PUT for a claim evidence document, returning the storage key to submit with.
  Future<String> uploadClaimDocument(
    String orgId, {
    required List<int> bytes,
    required String contentType,
  }) async {
    final presign = await _api.presignClaimDoc(orgId, contentType, bytes.length);
    await _api.uploadToPresigned(presign, bytes, contentType);
    return presign.key;
  }
}

final eventContentActionsProvider = Provider((ref) => EventContentActions(ref));
