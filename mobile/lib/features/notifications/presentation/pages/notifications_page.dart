import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../public_profile/presentation/providers/public_profile_providers.dart';
import '../../domain/entities/app_notification.dart';
import '../providers/notifications_providers.dart';

/// The notifications feed: reminders, price drops, new events, and system
/// messages, with unread styling and deep-links.
class NotificationsPage extends ConsumerWidget {
  const NotificationsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadNotificationsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          if (unread > 0)
            TextButton(
              onPressed: () => ref.read(notificationActionsProvider).markAllRead(),
              child: const Text('Mark all read'),
            ),
          // Preferences live with the notifications they govern, not three sections away in
          // Settings. Splitting them meant someone wanting to silence a notification had to leave
          // the screen showing it and go hunting.
          IconButton(
            icon: const Icon(Icons.tune_rounded),
            tooltip: 'Notification preferences',
            onPressed: () => context.push('/settings/notifications'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(notificationsFeedProvider);
          await ref.read(notificationsFeedProvider.future);
        },
        child: AsyncValueView(
          value: ref.watch(notificationsFeedProvider),
          onRetry: () => ref.invalidate(notificationsFeedProvider),
          isEmpty: (items) => items.isEmpty,
          empty: const EmptyState(
            icon: Icons.notifications_none_rounded,
            title: 'You’re all caught up',
            message: 'Reminders and updates about your events will show up here.',
          ),
          data: (items) => ContentWidth(
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(vertical: KSpace.sm),
              itemCount: items.length,
              separatorBuilder: (_, _) =>
                  Divider(height: 1, color: context.kurx.border.withValues(alpha: 0.5)),
              itemBuilder: (context, i) => _NotificationTile(notification: items[i]),
            ),
          ),
        ),
      ),
    );
  }
}

class _NotificationTile extends ConsumerWidget {
  const _NotificationTile({required this.notification});

  final AppNotification notification;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final unread = !notification.read;
    return InkWell(
      onTap: () {
        ref.read(notificationActionsProvider).markRead(notification.id);
        if (notification.route != null) context.push(notification.route!);
      },
      child: Container(
        color: unread ? c.accent.withValues(alpha: 0.05) : Colors.transparent,
        padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.md),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              height: 38,
              width: 38,
              decoration: BoxDecoration(
                color: c.accent.withValues(alpha: 0.14),
                borderRadius: BorderRadius.circular(KRadius.md),
              ),
              child: Icon(notification.kind.icon, size: 19, color: c.accent),
            ),
            const SizedBox(width: KSpace.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(notification.title,
                            style: TextStyle(
                                color: c.text,
                                fontSize: 14.5,
                                fontWeight: unread ? FontWeight.w800 : FontWeight.w600)),
                      ),
                      const SizedBox(width: KSpace.sm),
                      Text(_ago(notification.at), style: TextStyle(color: c.muted, fontSize: 11.5)),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(notification.body,
                      style: TextStyle(color: c.muted, fontSize: 13, height: 1.3)),
                  if (notification.connectionId != null)
                    _AllyRequestActions(connectionId: notification.connectionId!),
                ],
              ),
            ),
            if (unread)
              Container(
                margin: const EdgeInsets.only(left: KSpace.sm, top: 4),
                height: 8,
                width: 8,
                decoration: BoxDecoration(color: c.accent, shape: BoxShape.circle),
              ),
          ],
        ),
      ),
    );
  }
}

/// Inline Accept/Decline on a "New Ally Request" notification — reuses the same
/// `AllyRemoteDataSource` calls the profile page's Connect button already uses.
class _AllyRequestActions extends ConsumerStatefulWidget {
  const _AllyRequestActions({required this.connectionId});
  final String connectionId;

  @override
  ConsumerState<_AllyRequestActions> createState() => _AllyRequestActionsState();
}

class _AllyRequestActionsState extends ConsumerState<_AllyRequestActions> {
  bool _pending = false;
  String? _resolved;
  String? _error;

  Future<void> _respond(bool accept) async {
    setState(() {
      _pending = true;
      _error = null;
    });
    try {
      if (accept) {
        await ref.read(allySourceProvider).accept(widget.connectionId);
      } else {
        await ref.read(allySourceProvider).decline(widget.connectionId);
      }
      if (mounted) setState(() => _resolved = accept ? 'Accepted' : 'Declined');
    } on ApiError catch (e) {
      if (mounted) setState(() => _error = e.userMessage);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    if (_resolved != null) {
      return Padding(
        padding: const EdgeInsets.only(top: 4),
        child: Text(_resolved!, style: TextStyle(color: c.muted, fontSize: 12)),
      );
    }
    return Padding(
      padding: const EdgeInsets.only(top: KSpace.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              TextButton(
                onPressed: _pending ? null : () => _respond(true),
                child: const Text('Accept'),
              ),
              const SizedBox(width: KSpace.sm),
              TextButton(
                onPressed: _pending ? null : () => _respond(false),
                child: const Text('Decline'),
              ),
            ],
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(left: KSpace.sm, bottom: KSpace.xs),
              child: Text(_error!, style: TextStyle(color: c.danger, fontSize: 12)),
            ),
        ],
      ),
    );
  }
}

String _ago(DateTime at) {
  final d = DateTime.now().difference(at);
  if (d.inMinutes < 1) return 'now';
  if (d.inMinutes < 60) return '${d.inMinutes}m';
  if (d.inHours < 24) return '${d.inHours}h';
  if (d.inDays < 7) return '${d.inDays}d';
  return '${(d.inDays / 7).floor()}w';
}
