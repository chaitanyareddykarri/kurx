import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/content_width.dart';
import '../../data/account_remote_data_source.dart';
import '../providers/account_providers.dart';

const _labels = <String, String>{
  'messages': 'Messages',
  'connection_requests': 'Connection requests',
  'posts': 'Posts',
  'event_updates': 'Event updates',
  'announcements': 'Announcements',
  'invitations': 'Invitations',
  'staff_invitations': 'Staff invitations',
  'team_invitations': 'Team invitations',
  'payments': 'Payments & refunds',
  'certificates': 'Certificates',
  'security': 'Security',
  'system': 'System',
};

/// Notification preferences (D-263).
///
/// Optimistic per switch and reverted on failure — waiting for a round trip before the toggle moves
/// makes a screen of switches feel broken on a slow connection.
///
/// A locked category renders disabled. That is presentation: the server refuses the write regardless,
/// because whoever sends the request is exactly who the rule protects against.
class NotificationPreferencesPage extends ConsumerStatefulWidget {
  const NotificationPreferencesPage({super.key});

  @override
  ConsumerState<NotificationPreferencesPage> createState() => _NotificationPreferencesPageState();
}

class _NotificationPreferencesPageState extends ConsumerState<NotificationPreferencesPage> {
  List<NotificationPreference>? _rows;

  Future<void> _toggle(NotificationPreference row, String channel, bool value) async {
    final before = List<NotificationPreference>.from(_rows!);
    setState(() {
      _rows = _rows!
          .map((r) => r.category != row.category
              ? r
              : r.copyWith(
                  inApp: channel == 'inApp' ? value : null,
                  push: channel == 'push' ? value : null,
                  email: channel == 'email' ? value : null,
                  whatsApp: channel == 'whatsApp' ? value : null,
                ))
          .toList();
    });

    try {
      await ref.read(accountDataSourceProvider).updateNotificationPreference(
            row.category,
            inApp: channel == 'inApp' ? value : null,
            push: channel == 'push' ? value : null,
            email: channel == 'email' ? value : null,
            whatsApp: channel == 'whatsApp' ? value : null,
          );
    } catch (e) {
      if (!mounted) return;
      setState(() => _rows = before);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(notificationPreferencesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Notifications')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Padding(padding: const EdgeInsets.all(24), child: Text('$e'))),
        data: (loaded) {
          _rows ??= loaded;
          return ContentWidth(
            child: ListView(
              children: [
                const Padding(
                  padding: EdgeInsets.fromLTRB(16, 16, 16, 8),
                  child: Text(
                    'Choose how each kind of update reaches you. Security alerts stay on — they are how '
                    'you find out if someone else is trying to get into your account.',
                  ),
                ),
                for (final row in _rows!) ...[
                  const Divider(height: 1),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                    child: Row(
                      children: [
                        Text(
                          _labels[row.category] ?? row.category,
                          style: Theme.of(context).textTheme.titleSmall,
                        ),
                        if (row.locked) ...[
                          const SizedBox(width: 8),
                          const Chip(
                            label: Text('Always on'),
                            visualDensity: VisualDensity.compact,
                            padding: EdgeInsets.zero,
                          ),
                        ],
                      ],
                    ),
                  ),
                  _ChannelSwitch(
                    label: 'In-app',
                    value: row.inApp,
                    enabled: !row.locked,
                    onChanged: (v) => _toggle(row, 'inApp', v),
                  ),
                  _ChannelSwitch(
                    label: 'Push',
                    value: row.push,
                    enabled: !row.locked,
                    onChanged: (v) => _toggle(row, 'push', v),
                  ),
                  _ChannelSwitch(
                    label: 'Email',
                    value: row.email,
                    enabled: !row.locked,
                    onChanged: (v) => _toggle(row, 'email', v),
                  ),
                  _ChannelSwitch(
                    label: 'WhatsApp',
                    value: row.whatsApp,
                    enabled: !row.locked,
                    onChanged: (v) => _toggle(row, 'whatsApp', v),
                  ),
                ],
                const Padding(
                  padding: EdgeInsets.all(16),
                  child: Text(
                    'Email and WhatsApp switches are saved, but only in-app and push are enforced today.',
                    style: TextStyle(fontSize: 12),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _ChannelSwitch extends StatelessWidget {
  const _ChannelSwitch({
    required this.label,
    required this.value,
    required this.enabled,
    required this.onChanged,
  });

  final String label;
  final bool value;
  final bool enabled;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return SwitchListTile(
      dense: true,
      title: Text(label),
      value: value,
      onChanged: enabled ? onChanged : null,
    );
  }
}
