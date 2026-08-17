import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/datasources/org_remote_data_source.dart';
import '../../data/models/event_manage_dto.dart';
import '../../data/models/org_dto.dart';

// ── Org ──────────────────────────────────────────────────────────────────────

final orgSourceProvider =
    Provider((ref) => OrgRemoteDataSource(ref.watch(dioProvider)));

/// The institutions the caller may represent (D-268). Self-representation is not in this list and is
/// not filtered out here — the server never sends it, because representing yourself is not an
/// organization. This is what the Representing surface and the create-event Representing step list.
final myRepresentationsProvider = FutureProvider.autoDispose<List<RepresentationDto>>(
    (ref) => ref.watch(orgSourceProvider).myRepresentations());

final orgDetailProvider =
    FutureProvider.autoDispose.family<OrgDto, String>(
        (ref, id) => ref.watch(orgSourceProvider).org(id));

final orgMembersProvider =
    FutureProvider.autoDispose.family<List<OrgMemberDto>, String>(
        (ref, orgId) => ref.watch(orgSourceProvider).members(orgId));

final orgWalletProvider =
    FutureProvider.autoDispose.family<WalletDto, String>(
        (ref, orgId) => ref.watch(orgSourceProvider).wallet(orgId));

final orgLedgerProvider =
    FutureProvider.autoDispose.family<List<LedgerEntryDto>, String>(
        (ref, orgId) => ref.watch(orgSourceProvider).ledger(orgId));

// ── Event management ──────────────────────────────────────────────────────────

final eventManageSourceProvider =
    Provider((ref) => EventManageRemoteDataSource(ref.watch(dioProvider)));

/// Every event the caller hosts, in one list (D-267). Not keyed by organisation — a person's events
/// are theirs, and the organisation each one represents rides along on the row.
final myEventsProvider = FutureProvider.autoDispose<List<EventManageDto>>(
    (ref) => ref.watch(eventManageSourceProvider).myEvents());

typedef _OrgEvent = ({String orgId, String eventId});

/// Keyed by event id alone — the event resolves its own organisation, which is what lets the manage
/// routes address an event directly instead of nesting it under one (D-267).
final eventManageDetailProvider =
    FutureProvider.autoDispose.family<EventManageDto, String>(
        (ref, eventId) => ref.watch(eventManageSourceProvider).event(eventId));

final registrationsProvider =
    FutureProvider.autoDispose.family<List<RegistrationDto>, _OrgEvent>(
        (ref, p) =>
            ref.watch(eventManageSourceProvider).registrations(p.orgId, p.eventId));

final attendeesProvider =
    FutureProvider.autoDispose.family<List<AttendeeDto>, _OrgEvent>(
        (ref, p) =>
            ref.watch(eventManageSourceProvider).attendees(p.orgId, p.eventId));

final invitationsProvider =
    FutureProvider.autoDispose.family<List<InvitationDto>, _OrgEvent>(
        (ref, p) =>
            ref.watch(eventManageSourceProvider).invitations(p.orgId, p.eventId));

final announcementsProvider =
    FutureProvider.autoDispose.family<List<AnnouncementDto>, _OrgEvent>(
        (ref, p) => ref
            .watch(eventManageSourceProvider)
            .announcements(p.orgId, p.eventId));

final eventAnalyticsProvider =
    FutureProvider.autoDispose.family<AnalyticsDto, _OrgEvent>(
        (ref, p) => ref
            .watch(eventManageSourceProvider)
            .analytics(p.orgId, p.eventId));

final assignmentsProvider =
    FutureProvider.autoDispose.family<List<AssignmentDto>, _OrgEvent>(
        (ref, p) =>
            ref.watch(eventManageSourceProvider).assignments(p.orgId, p.eventId));

/*
 * D-372/D-366 — does this archetype permit TEAM entry?
 *
 * Asked of the capability engine (`GET /v1/archetypes/{slug}/capabilities`), never inferred from a
 * type's name. The matrix is data an admin can change, so a second copy of it in this app would
 * silently disagree with the server the day someone edits it — which is precisely what D-372 forbids.
 *
 * Fails CLOSED: an unreadable capability set, or a Type with no archetype at all, yields false, so the
 * Registration step offers individual entry only rather than a team option the server may refuse.
 * `family` on the slug because a wizard visits one archetype at a time and the answer is cacheable.
 */
final archetypeSupportsTeamsProvider =
    FutureProvider.autoDispose.family<bool, String?>((ref, slug) async {
  if (slug == null || slug.isEmpty) return false;
  try {
    final res = await ref.watch(dioProvider).get('/v1/archetypes/$slug/capabilities');
    final caps = (res.data as List).cast<Map<String, dynamic>>();
    final teams = caps.where((c) => c['slug'] == 'teams').firstOrNull;
    if (teams == null) return false;
    final state = (teams['state'] as String? ?? '').toLowerCase();
    // The engine's own words: only these two mean "this event cannot have teams" (D-266 M2).
    return state != 'unsupported' && state != 'locked';
  } catch (_) {
    return false;
  }
});
