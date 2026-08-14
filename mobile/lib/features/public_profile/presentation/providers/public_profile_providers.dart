import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/datasources/public_profile_remote_data_source.dart';
import '../../data/models/public_profile_dto.dart';

final publicProfileSourceProvider = Provider(
  (ref) => PublicProfileRemoteDataSource(ref.watch(dioProvider)),
);
final allySourceProvider = Provider(
  (ref) => AllyRemoteDataSource(ref.watch(dioProvider)),
);

final publicProfileProvider =
    FutureProvider.autoDispose.family<PublicProfileDto, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getProfile(username));

final publicProfileCertificatesProvider =
    FutureProvider.autoDispose.family<List<PublicCertificateCardDto>, String>((ref, username) =>
        ref.watch(publicProfileSourceProvider).getCertificates(username));

/// Organized + attended, merged (D-201 §1 lanes stay independent server-side; the client just
/// shows one combined "Events" list, newest first — the participation-role lane lives in Timeline).
final publicProfileEventsProvider =
    FutureProvider.autoDispose.family<List<PublicEventCardDto>, String>((ref, username) async {
  final src = ref.watch(publicProfileSourceProvider);
  final results = await Future.wait([
    src.getEvents(username, type: 'conducted'),
    src.getEvents(username, type: 'attended'),
  ]);
  final merged = [...results[0], ...results[1]]
    ..sort((a, b) => b.startsAt.compareTo(a.startsAt));
  return merged;
});

final publicProfileTimelineProvider =
    FutureProvider.autoDispose.family<List<TimelineEntryDto>, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getTimeline(username));

final publicProfileAlliesProvider =
    FutureProvider.autoDispose.family<List<AllyProfileCardDto>, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getAllies(username));

/// The Professional Journey (D-223). Its own provider so a slow or forbidden section never blocks
/// the profile header — the section simply doesn't render.
final publicProfileJourneyProvider =
    FutureProvider.autoDispose.family<List<JourneyNodeDto>, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getJourney(username));

/// Phase 2 Experience — accepted assignments. Its own provider so a viewer who may not see it
/// simply gets no Experience block rather than losing the whole About panel.
final publicProfileAssignmentsProvider =
    FutureProvider.autoDispose.family<List<ProfileAssignmentDto>, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getAssignments(username));

/// Metrics and Experience (D-225), each its own provider so a section the viewer may not see fails
/// alone and simply doesn't render — it must never take the rest of the profile down with it.
final publicProfileMetricsProvider =
    FutureProvider.autoDispose.family<ProfileMetricsDto, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getMetrics(username));

final publicProfileExperienceProvider =
    FutureProvider.autoDispose.family<ExperienceSummaryDto, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getExperience(username));

final publicProfileContributionsProvider =
    FutureProvider.autoDispose.family<ContributionsDto, String>(
        (ref, username) => ref.watch(publicProfileSourceProvider).getContributions(username));

final incomingAllyRequestsProvider = FutureProvider.autoDispose<List<AllyConnectionDto>>(
    (ref) => ref.watch(allySourceProvider).incoming());
final outgoingAllyRequestsProvider = FutureProvider.autoDispose<List<AllyConnectionDto>>(
    (ref) => ref.watch(allySourceProvider).outgoing());
final myAlliesProvider = FutureProvider.autoDispose<List<AllyConnectionDto>>(
    (ref) => ref.watch(allySourceProvider).mine());

/// Batch ally status for a page of user ids — joined into one cache key so the same list of ids
/// (e.g. one screen's attendee roster) is fetched once, not per card.
final allyStatusBatchProvider =
    FutureProvider.autoDispose.family<Map<String, String>, List<String>>(
        (ref, userIds) => ref.watch(allySourceProvider).statusBatch(userIds));

final allySuggestionsProvider = FutureProvider.autoDispose<List<AllySuggestionDto>>(
    (ref) => ref.watch(allySourceProvider).suggestions());

final allyMutualDetailProvider =
    FutureProvider.autoDispose.family<MutualDetailDto, String>(
        (ref, otherUserId) => ref.watch(allySourceProvider).mutualDetail(otherUserId));

final peopleSearchProvider =
    FutureProvider.autoDispose.family<List<PublicUserSearchResultDto>, String>(
        (ref, query) => ref.watch(publicProfileSourceProvider).search(query));
