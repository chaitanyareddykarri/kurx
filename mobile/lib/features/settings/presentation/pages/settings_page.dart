import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../core/app_info.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/session/session_controller.dart';
import '../../../../core/theme/theme_mode_controller.dart';
import '../../../auth/presentation/providers/auth_providers.dart';

class SettingsPage extends ConsumerWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final themeMode = ref.watch(themeModeControllerProvider);
    final isGuest = ref.watch(sessionControllerProvider).status != AuthStatus.authenticated;

    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ContentWidth(
        child: ListView(
          children: [
            const _SectionLabel('Appearance'),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: SegmentedButton<ThemeMode>(
                segments: const [
                  ButtonSegment(value: ThemeMode.system, icon: Icon(Icons.brightness_auto), label: Text('System')),
                  ButtonSegment(value: ThemeMode.light, icon: Icon(Icons.light_mode), label: Text('Light')),
                  ButtonSegment(value: ThemeMode.dark, icon: Icon(Icons.dark_mode), label: Text('Dark')),
                ],
                selected: {themeMode},
                onSelectionChanged: (s) => ref.read(themeModeControllerProvider.notifier).setMode(s.first),
              ),
            ),
            const Divider(height: 24),
            const _SectionLabel('Account'),
            if (isGuest)
              ListTile(
                leading: const Icon(Icons.login),
                title: const Text('Sign in'),
                subtitle: const Text('Log in with your phone to register for events'),
                onTap: () {
                  ref.read(loginReturnToProvider.notifier).state = GoRouterState.of(context).matchedLocation;
                  context.push(Routes.login);
                },
              )
            else ...[
              // D-263 — email, phone, language and username history, matching web /settings/account.
              ListTile(
                leading: const Icon(Icons.manage_accounts_outlined),
                title: const Text('Account'),
                subtitle: const Text('Email, phone, language and previous usernames'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => context.push(Routes.accountSettings),
              ),
              ListTile(
                leading: const Icon(Icons.shield_outlined),
                title: const Text('Security'),
                subtitle: const Text('Passkeys, trusted devices, sessions and recovery codes'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => context.push(Routes.security),
              ),
              // D-263. Notifications and blocking sit next to Security because all three are about
              // what other people can do to your attention and your account.
              // Theme is an account-level display choice and belongs beside language, which is the
              // other thing on this screen that changes how the app presents itself. Web has had
              // this under Account since D-263; mobile had the controller and no surface for it.
              ListTile(
                leading: const Icon(Icons.brightness_6_outlined),
                title: const Text('Theme'),
                subtitle: Text(switch (ref.watch(themeModeControllerProvider)) {
                  ThemeMode.light => 'Light',
                  ThemeMode.dark => 'Dark',
                  ThemeMode.system => 'Match system',
                }),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => _pickTheme(context, ref),
              ),
              ListTile(
                leading: const Icon(Icons.block_outlined),
                title: const Text('Blocked accounts'),
                subtitle: const Text('People who cannot see or message you'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => context.push(Routes.blockedUsers),
              ),
              ListTile(
                leading: const Icon(Icons.logout),
                title: const Text('Sign out'),
                onTap: () => ref.read(authControllerProvider.notifier).logout(),
              ),
              ListTile(
                leading: Icon(Icons.delete_forever_outlined, color: Theme.of(context).colorScheme.error),
                title: Text('Delete account', style: TextStyle(color: Theme.of(context).colorScheme.error)),
                subtitle: const Text('Scheduled, with 30 days to change your mind'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => context.push(Routes.deleteAccount),
              ),
            ],
            const Divider(height: 24),
            const _SectionLabel('About'),
            const ListTile(
              leading: Icon(Icons.info_outline),
              title: Text(AppInfo.name),
              subtitle: Text(AppInfo.tagline),
            ),
            const ListTile(
              leading: Icon(Icons.tag),
              title: Text('Version'),
              trailing: Text(AppInfo.version),
            ),
            ListTile(
              leading: const Icon(Icons.privacy_tip_outlined),
              title: const Text('Privacy Policy'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/legal/privacy'),
            ),
            ListTile(
              leading: const Icon(Icons.description_outlined),
              title: const Text('Terms of Service'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/legal/terms'),
            ),
            ListTile(
              leading: const Icon(Icons.receipt_long_outlined),
              title: const Text('Refund Policy'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/legal/refund'),
            ),
            ListTile(
              leading: const Icon(Icons.workspaces_outline),
              title: const Text('Open source licenses'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => showLicensePage(
                context: context,
                applicationName: AppInfo.name,
                applicationVersion: AppInfo.version,
              ),
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Text(
        text,
        style: Theme.of(context).textTheme.titleSmall?.copyWith(color: Theme.of(context).colorScheme.primary),
      ),
    );
  }
}

/// Theme picker. A sheet rather than a settings sub-page: three mutually exclusive options with no
/// further depth is exactly what a sheet is for, and a whole route for it would be a page that
/// exists to hold one radio group.
Future<void> _pickTheme(BuildContext context, WidgetRef ref) async {
  final current = ref.read(themeModeControllerProvider);
  final picked = await showModalBottomSheet<ThemeMode>(
    context: context,
    builder: (sheetContext) => SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          // Plain ListTiles with a check: RadioListTile's groupValue/onChanged are deprecated in
          // favour of a RadioGroup ancestor, and a three-item exclusive list does not need either.
          for (final mode in ThemeMode.values)
            ListTile(
              title: Text(switch (mode) {
                ThemeMode.light => 'Light',
                ThemeMode.dark => 'Dark',
                ThemeMode.system => 'Match system',
              }),
              trailing: mode == current ? const Icon(Icons.check_rounded) : null,
              selected: mode == current,
              onTap: () => Navigator.of(sheetContext).pop(mode),
            ),
        ],
      ),
    ),
  );
  if (picked != null) ref.read(themeModeControllerProvider.notifier).setMode(picked);
}
