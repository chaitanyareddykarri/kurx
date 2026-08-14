import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/datasources/competition_remote_data_source.dart';
import '../../data/models/competition_dtos.dart';

final competitionSourceProvider =
    Provider((ref) => CompetitionRemoteDataSource(ref.watch(dioProvider)));

// ── Series ───────────────────────────────────────────────────────────────────

final seriesProvider = FutureProvider.autoDispose.family<SeriesDto, String>(
    (ref, id) => ref.watch(competitionSourceProvider).series(id));

final seriesEditionsProvider = FutureProvider.autoDispose.family<List<SeriesMemberDto>, String>(
    (ref, id) => ref.watch(competitionSourceProvider).seriesEditions(id));

// ── Competition ──────────────────────────────────────────────────────────────

final eventStagesProvider = FutureProvider.autoDispose.family<List<StageDto>, String>(
    (ref, eventId) => ref.watch(competitionSourceProvider).stages(eventId));

final stageParticipantsProvider =
    FutureProvider.autoDispose.family<List<StageParticipantDto>, String>(
        (ref, stageId) => ref.watch(competitionSourceProvider).stageParticipants(stageId));

final stageResultsProvider = FutureProvider.autoDispose.family<List<StageResultDto>, String>(
    (ref, stageId) => ref.watch(competitionSourceProvider).stageResults(stageId));

// ── Teams ────────────────────────────────────────────────────────────────────

final eventTeamsProvider = FutureProvider.autoDispose.family<List<TeamDto>, String>(
    (ref, eventId) => ref.watch(competitionSourceProvider).eventTeams(eventId));

final myTeamsProvider = FutureProvider.autoDispose<List<TeamDto>>(
    (ref) => ref.watch(competitionSourceProvider).myTeams());

/// Mutations. Errors propagate so the screen can show the server's own refusal — team formation is
/// heavily policy-gated (`team_locked`, `roster_locked`, `max_teams_reached`, `team_full`) and a
/// generic "something went wrong" would hide which rule was hit.
class CompetitionActions {
  const CompetitionActions(this._ref);
  final Ref _ref;

  CompetitionRemoteDataSource get _api => _ref.read(competitionSourceProvider);

  Future<void> followSeries(String seriesId, {required bool follow}) async {
    if (follow) {
      await _api.followSeries(seriesId);
    } else {
      await _api.unfollowSeries(seriesId);
    }
    _ref.invalidate(seriesProvider(seriesId));
  }

  Future<TeamDto> createTeam(
    String eventId, {
    required String ticketTypeId,
    required String name,
    String? tagline,
  }) async {
    final team = await _api.createTeam(
      eventId,
      ticketTypeId: ticketTypeId,
      name: name,
      tagline: tagline,
    );
    _ref.invalidate(eventTeamsProvider(eventId));
    _ref.invalidate(myTeamsProvider);
    return team;
  }

  Future<void> requestToJoin(String eventId, String teamId, {String? message}) async {
    await _api.requestToJoin(teamId, message: message);
    _ref.invalidate(eventTeamsProvider(eventId));
  }

  Future<void> leaveTeam(String eventId, String teamId) async {
    await _api.leaveTeam(teamId);
    _ref.invalidate(eventTeamsProvider(eventId));
    _ref.invalidate(myTeamsProvider);
  }

  Future<void> invite(String teamId, {String? phone, String? email}) =>
      _api.inviteToTeam(teamId, phone: phone, email: email);
}

final competitionActionsProvider = Provider((ref) => CompetitionActions(ref));
