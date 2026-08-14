import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/network/network_providers.dart';
import '../../../../core/network/api_guard.dart';
import '../../../../core/theme/design_tokens.dart';

final _devicesProvider = FutureProvider.autoDispose<List<Map<String, dynamic>>>(
  (ref) => guard(() async {
    final res = await ref.watch(dioProvider).get('/v1/me/devices');
    return (res.data as List).cast<Map<String, dynamic>>();
  }),
);

class MyDevicesPage extends ConsumerWidget {
  const MyDevicesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('My Devices')),
      body: AsyncValueView(
        value: ref.watch(_devicesProvider),
        onRetry: () => ref.invalidate(_devicesProvider),
        data: (devices) => ListView.separated(
          padding: const EdgeInsets.all(KSpace.lg),
          itemCount: devices.length,
          separatorBuilder: (_, _) =>
              Divider(color: c.border),
          itemBuilder: (_, i) {
            final d = devices[i];
            final isCurrent = d['is_current'] == true;
            return ListTile(
              leading: Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: c.elevated,
                  borderRadius: BorderRadius.circular(KRadius.md),
                ),
                child: Icon(
                  Icons.smartphone_rounded,
                  color: isCurrent ? c.accent : c.muted,
                ),
              ),
              title: Row(
                children: [
                  Expanded(
                    child: Text(
                      d['device_name']?.toString() ?? 'Unknown device',
                      style: TextStyle(
                        color: c.text,
                        fontWeight: FontWeight.w600,
                        fontSize: 14,
                      ),
                    ),
                  ),
                  if (isCurrent)
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: KSpace.sm, vertical: 2),
                      decoration: BoxDecoration(
                        color: c.accent.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(KRadius.pill),
                      ),
                      child: Text(
                        'This device',
                        style: TextStyle(
                          color: c.accent,
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                ],
              ),
              subtitle: Text(
                'Last active ${d['last_active_at'] ?? ''}',
                style: TextStyle(color: c.muted, fontSize: 12),
              ),
              // A "Remove" button with an empty `onPressed` used to sit here — on the screen
              // somebody opens when they think their account is compromised. A dead control is bad
              // anywhere; one that implies a *security* action succeeded is worse, because the user
              // walks away believing the device was revoked.
              //
              // Not wired, and deliberately: this page is an ORPHAN. It is registered in the router
              // and nothing in the app navigates to it, while `security_page.dart` carries real,
              // reachable device revocation (`_revokeDevice`, over `revokeSession`). Wiring a
              // second, unreachable copy of a security action is worse than removing the control;
              // deleting the page itself belongs to Phase 49, with the orphan evidence in the
              // Regression Ledger.
              trailing: null,
            );
          },
        ),
      ),
    );
  }
}
