import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/router/app_router.dart';
import '../../core/theme/design_tokens.dart';
import '../../features/notifications/presentation/providers/notifications_providers.dart';
import '../../features/profile/presentation/providers/profile_providers.dart';
import 'kurx_avatar.dart';

/// The app bar every Main Application tab shares: **profile top-left, notifications top-right**.
///
/// Those two are fixed positions rather than tabs because the product flow names seven Main
/// Application areas and the pill nav holds five — Profile and Notifications are the two you visit
/// and come back from, not places you dwell, so they live here where they are reachable from every
/// tab at the same coordinates.
///
/// Screen-specific controls still go in [actions]; they render to the left of the bell, so the
/// notification icon keeps its corner no matter which tab is showing.
class KurxShellAppBar extends ConsumerWidget implements PreferredSizeWidget {
  const KurxShellAppBar({
    super.key,
    required this.title,
    this.actions = const [],
    this.bottom,
  });

  final String title;
  final List<Widget> actions;
  final PreferredSizeWidget? bottom;

  @override
  Size get preferredSize =>
      Size.fromHeight(kToolbarHeight + (bottom?.preferredSize.height ?? 0));

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final profile = ref.watch(profileControllerProvider);
    final unread = ref.watch(unreadNotificationsProvider);

    return AppBar(
      title: Text(title),
      titleSpacing: 0,
      leading: IconButton(
        tooltip: 'Profile',
        padding: EdgeInsets.zero,
        icon: KurxAvatar(name: profile.name, imageUrl: profile.avatarKey, size: 30),
        onPressed: () => context.push(Routes.profile),
      ),
      actions: [
        ...actions,
        IconButton(
          tooltip: 'Notifications',
          icon: unread > 0
              ? Badge(
                  backgroundColor: c.accent,
                  label: Text('$unread'),
                  child: const Icon(Icons.notifications_none_rounded),
                )
              : const Icon(Icons.notifications_none_rounded),
          onPressed: () => context.push(Routes.notifications),
        ),
      ],
      bottom: bottom,
    );
  }
}
