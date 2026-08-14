import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/datasources/gamification_remote_data_source.dart';
import '../../data/models/gamification_dto.dart';

final _gamificationSourceProvider = Provider(
  (ref) => GamificationRemoteDataSource(ref.watch(dioProvider)),
);

final myPointsProvider =
    FutureProvider.autoDispose<PointsSummaryDto>(
        (ref) => ref.watch(_gamificationSourceProvider).myPoints());

final myBadgesProvider = FutureProvider.autoDispose<List<BadgeDto>>(
    (ref) => ref.watch(_gamificationSourceProvider).myBadges());

final leaderboardProvider = FutureProvider.autoDispose<List<LeaderboardEntryDto>>(
    (ref) => ref.watch(_gamificationSourceProvider).leaderboard());
