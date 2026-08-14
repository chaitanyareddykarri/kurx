import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/network_providers.dart';
import '../../data/account_remote_data_source.dart';

/// Account settings (D-263) and direct messages (D-264).
final accountDataSourceProvider = Provider<AccountRemoteDataSource>(
  (ref) => AccountRemoteDataSource(ref.watch(dioProvider)),
);

final notificationPreferencesProvider = FutureProvider<List<NotificationPreference>>(
  (ref) => ref.watch(accountDataSourceProvider).notificationPreferences(),
);

final blockedUsersProvider = FutureProvider<List<BlockedUser>>(
  (ref) => ref.watch(accountDataSourceProvider).blocks(),
);

final accountDeletionProvider = FutureProvider<DateTime?>(
  (ref) => ref.watch(accountDataSourceProvider).deletionScheduledFor(),
);

final usernameHistoryProvider = FutureProvider<List<UsernameHistoryEntry>>(
  (ref) => ref.watch(accountDataSourceProvider).usernameHistory(),
);

/// Accepted conversations. Archived ones are a separate read rather than a filter, because the
/// server treats the archive as a folder and the two lists are genuinely different queries.
final dmRoomsProvider = FutureProvider.family<List<DmRoom>, bool>(
  (ref, archived) => ref.watch(accountDataSourceProvider).dms(archived: archived),
);

final dmRequestsProvider = FutureProvider<List<DmRoom>>(
  (ref) => ref.watch(accountDataSourceProvider).dmRequests(),
);
