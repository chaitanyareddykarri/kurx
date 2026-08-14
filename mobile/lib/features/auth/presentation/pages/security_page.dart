import 'dart:io' show Platform;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/network/auth_retry.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/security/device_key_service.dart';
import '../../../../core/security/passkey_service.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/trusted_device_dtos.dart';
import '../../data/repositories/trusted_device_repository.dart';
import '../providers/auth_providers.dart';
import '../security_activity_labels.dart';
import 'step_up_page.dart';

/// Account security: trusted devices, active sessions, and recovery codes (AM5/AM6/AM7).
///
/// The mobile twin of the web `/settings/security` screen. Both are views over the same
/// `trusted_devices` aggregate, so the wording is kept deliberately consistent — a user who removes
/// a device on one surface should recognise what happened on the other.
class SecurityPage extends ConsumerStatefulWidget {
  const SecurityPage({super.key, this.thisDeviceId});

  /// This device's trusted-device id, when it is enrolled — used to label "this device" and to
  /// know when a revocation must also destroy the local key.
  final String? thisDeviceId;

  @override
  ConsumerState<SecurityPage> createState() => _SecurityPageState();
}

class _SecurityPageState extends ConsumerState<SecurityPage> {
  String? _busyId;

  Future<void> _revokeDevice(TrustedDeviceDto device) async {
    final isThisDevice = device.id == widget.thisDeviceId;
    final confirmed = await _confirm(
      title: isThisDevice ? 'Sign this device out?' : 'Remove ${device.name ?? 'this device'}?',
      body: isThisDevice
          ? "You'll be signed out and will need to set this device up again."
          : "It will be signed out immediately and can't approve sign-ins until it's set up again.",
    );
    if (!confirmed) return;

    setState(() => _busyId = device.id);
    try {
      await ref
          .read(trustedDeviceRepositoryProvider)
          .revokeDevice(device.id, isThisDevice: isThisDevice);
      ref
        ..invalidate(trustedDevicesProvider)
        ..invalidate(authSessionsProvider);
    } catch (_) {
      _snack("Couldn't remove that device. Check your connection.");
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  Future<void> _revokeSession(AuthSessionDto session) async {
    setState(() => _busyId = session.id);
    try {
      await ref.read(trustedDeviceRepositoryProvider).revokeSession(session.id);
      ref
        ..invalidate(authSessionsProvider)
        ..invalidate(securityOverviewProvider);
    } catch (_) {
      _snack("Couldn't sign that session out. Check your connection.");
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  Future<void> _revokeBrowser(TrustedBrowserDto browser) async {
    final confirmed = await _confirm(
      title: 'Forget this browser?',
      body: 'It will have to sign in with a second factor again.',
      confirmLabel: 'Forget',
    );
    if (!confirmed) return;
    setState(() => _busyId = browser.id);
    try {
      await ref.read(trustedDeviceRepositoryProvider).revokeTrustedBrowser(browser.id);
      ref
        ..invalidate(trustedBrowsersProvider)
        ..invalidate(securityOverviewProvider);
    } catch (_) {
      _snack("Couldn't forget that browser. Check your connection.");
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  Future<void> _signOutEverywhere() async {
    final confirmed = await _confirm(
      title: 'Sign out everywhere?',
      body: 'This signs out every session and forgets every trusted browser, including this one.',
      confirmLabel: 'Sign out',
    );
    if (!confirmed) return;
    setState(() => _busyId = 'signout');
    try {
      await ref.read(trustedDeviceRepositoryProvider).signOutEverywhere();
      // Revoked this session too — end it locally so the router sends us back to sign-in.
      await ref.read(authControllerProvider.notifier).logout();
    } catch (_) {
      _snack("Couldn't sign out everywhere. Check your connection.");
      if (mounted) setState(() => _busyId = null);
    }
  }

  Future<void> _addPasskey() async {
    setState(() => _busyId = 'passkey');
    try {
      await ref.read(trustedDeviceRepositoryProvider).registerPasskey(deviceName: 'Passkey');
      ref
        ..invalidate(passkeysProvider)
        ..invalidate(trustedDevicesProvider);
      _snack('Passkey added.');
    } on PasskeyException catch (e) {
      // Cancelling, or already having one, are ordinary states of the world — not failures.
      if (e.code == 'user_canceled') return;
      _snack(switch (e.code) {
        'already_registered' => 'This device already has a passkey for your account.',
        'no_provider' =>
          'No passkey provider on this device. Set a screen lock, or use trusted-device sign-in.',
        'no_credential' => 'No passkey found on this device.',
        // A DOM error here is almost always a Digital Asset Links mismatch (D-097) — the user
        // cannot fix it, so say something that gets it reported rather than blaming them.
        'dom_error' => 'Passkeys are not set up for this app build. Please report this.',
        _ => "Couldn't add a passkey. Please try again.",
      });
    } catch (_) {
      _snack("Couldn't add a passkey. Please try again.");
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  /// Enrolls this handset as a trusted device (AM2). Prompts for biometrics via the keystore, so it
  /// must only ever run from an explicit tap — never automatically on screen open.
  Future<void> _enrollThisDevice() async {
    setState(() => _busyId = 'enroll');
    try {
      await ref.read(trustedDeviceRepositoryProvider).enrollThisDevice(
            name: _defaultDeviceName(),
            platform: Platform.isIOS ? 'ios' : 'android',
          );
      ref
        ..invalidate(trustedDevicesProvider)
        ..invalidate(thisDeviceIdProvider);
      if (mounted) _snack('This device can now approve your sign-ins.');
    } on DeviceKeyException catch (e) {
      if (e.code != 'user_canceled') {
        _snack(e.requiresReenrollment
            ? 'Your device security changed. Try setting it up again.'
            : "Couldn't use this device's secure hardware.");
      }
    } catch (e) {
      _snack(AuthRetry.isOffline(e)
          ? "You're offline. Reconnect and try again."
          : "Couldn't set up this device. Please try again.");
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  String _defaultDeviceName() => Platform.isIOS ? 'iPhone' : 'Android phone';

  Future<void> _generateRecoveryCodes({bool steppedUp = false}) async {
    setState(() => _busyId = 'codes');
    try {
      final codes = await ref.read(trustedDeviceRepositoryProvider).generateRecoveryCodes();
      if (mounted) await _showCodes(codes);
    } on DeviceKeyException catch (e) {
      if (e.code != 'user_canceled') _snack('Could not confirm it was you.');
    } on ApiError catch (e) {
      // Only a 403 is the step-up gate (D-084). Reporting an offline failure as "confirm it's you"
      // would send the user to do something that cannot possibly help.
      if (e.status != 403) {
        _snack(e.userMessage);
      } else if (steppedUp) {
        // Already stepped up and still refused: retrying would loop. Something else is wrong.
        _snack("Couldn't confirm it was you. Please try again.");
      } else {
        await _stepUpThenRetry();
      }
    } catch (_) {
      _snack('Could not generate recovery codes. Please try again.');
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  /// Standalone step-up (AM6). Satisfying it grants a short server-side window during which a sensitive
  /// action can proceed — including on *another* device (the web/admin console can't sign a step-up
  /// itself, so it waits for one done here). Reuses the same D-181 [StepUpPage] as the inline gate.
  Future<void> _stepUpNow() async {
    final deviceId = widget.thisDeviceId;
    if (deviceId == null) return;
    setState(() => _busyId = 'stepup');
    final ok = await Navigator.of(context).push<bool>(MaterialPageRoute(
      builder: (_) => StepUpPage(
        action: 'security',
        deviceId: deviceId,
        reason: 'Confirming now lets you complete a sensitive action — like generating recovery '
            'codes on the Kurx website — for the next few minutes.',
      ),
    ));
    if (!mounted) return;
    setState(() => _busyId = null);
    if (ok == true) {
      ref.invalidate(securityOverviewProvider);
      _snack('Confirmed. Sensitive actions are unlocked for a few minutes.');
    }
  }

  /// The 403 gate is only useful if the user can actually satisfy it here (D-117). Telling them to
  /// "confirm it's you" without offering the screen that does it was a dead end — and it hit exactly
  /// the users who *have* a trusted device, i.e. the ones the gate exists to protect.
  Future<void> _stepUpThenRetry() async {
    final deviceId = widget.thisDeviceId;
    if (deviceId == null) {
      // No enrolled device on this handset, so there is nothing to sign with. The server exempts
      // these users from the gate, so a 403 here means the session is bound to a different device.
      _snack('Confirm it\'s you on the device you set up, then try again.');
      return;
    }

    final ok = await Navigator.of(context).push<bool>(MaterialPageRoute(
      builder: (_) => StepUpPage(
        action: 'recovery_codes',
        deviceId: deviceId,
        reason: 'Recovery codes are a way back into your account, so we need to confirm '
            "it's really you before creating new ones.",
      ),
    ));
    if (ok == true && mounted) await _generateRecoveryCodes(steppedUp: true);
  }

  /// Shown exactly once — the plaintext codes never exist on the server or on disk after this.
  Future<void> _showCodes(List<String> codes) => showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (context) => AlertDialog(
          title: const Text('Save your recovery codes'),
          content: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text("These won't be shown again. Store them somewhere safe and offline."),
                const SizedBox(height: 16),
                for (final code in codes)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: SelectableText(code, style: const TextStyle(fontFamily: 'monospace')),
                  ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () async {
                await Clipboard.setData(ClipboardData(text: codes.join('\n')));
                if (context.mounted) _snack('Recovery codes copied.');
              },
              child: const Text('Copy'),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text("I've saved them"),
            ),
          ],
        ),
      );

  Future<bool> _confirm({required String title, required String body, String confirmLabel = 'Remove'}) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(body),
          actions: [
            TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(context).pop(true), child: Text(confirmLabel)),
          ],
        ),
      ) ??
      false;

  void _snack(String message) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));

  Widget _overview(SecurityOverviewDto o) {
    final items = <(String, String, bool)>[
      ('Password', o.hasPassword ? 'Set' : 'Not set', o.hasPassword),
      ('Email', o.emailVerified ? 'Verified' : (o.email != null ? 'Unverified' : 'Not added'), o.emailVerified),
      ('Phone', o.phoneVerified ? 'Verified' : 'Unverified', o.phoneVerified),
      ('Passkeys', '${o.passkeys}', o.passkeys > 0),
      ('Trusted devices', '${o.trustedDevices}', o.trustedDevices > 0),
      ('Trusted browsers', '${o.trustedBrowsers}', true),
      ('Sessions', '${o.activeSessions}', true),
      ('Recovery codes', '${o.recoveryCodesRemaining}', o.recoveryCodesRemaining > 0),
    ];
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          for (final (label, value, ok) in items)
            Container(
              width: 150,
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                border: Border.all(color: c.border),
                borderRadius: BorderRadius.circular(KRadius.md),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(label, style: TextStyle(color: c.muted, fontSize: 12)),
                  const SizedBox(height: 2),
                  Text(value,
                      style: TextStyle(
                          color: ok ? c.text : c.danger, fontSize: 14, fontWeight: FontWeight.w700)),
                ],
              ),
            ),
        ],
      ),
    );
  }

  String _browserTitle(TrustedBrowserDto b) {
    final parts = [b.browser, b.operatingSystem].whereType<String>().where((s) => s.isNotEmpty).toList();
    return parts.isEmpty ? (b.label ?? 'Browser') : parts.join(' · ');
  }

  String _browserSubtitle(TrustedBrowserDto b) {
    final when = b.lastUsedAt != null ? 'last used ${relativeTime(b.lastUsedAt!)}' : 'added ${relativeTime(b.createdAt)}';
    return [
      if (b.isCurrent) 'this browser',
      b.approxLocation ?? b.ip,
      when,
    ].whereType<String>().where((s) => s.isNotEmpty).join(' · ');
  }

  Color _severityColor(String severity) => switch (severity) {
        'critical' => context.kurx.danger,
        // `Colors.amber` between two theme reads: it ignores dark mode and was never contrast-solved,
        // and this is the severity of a security alert. `warning` is the token that was.
        'warning' => context.kurx.warning,
        _ => context.kurx.accent,
      };

  @override
  Widget build(BuildContext context) {
    final devices = ref.watch(trustedDevicesProvider);
    final sessions = ref.watch(authSessionsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Security')),
      body: RefreshIndicator(
        onRefresh: () async => ref
          ..invalidate(securityOverviewProvider)
          ..invalidate(trustedDevicesProvider)
          ..invalidate(trustedBrowsersProvider)
          ..invalidate(authSessionsProvider)
          ..invalidate(securityActivityProvider),
        child: ListView(
          padding: const EdgeInsets.symmetric(vertical: 8),
          children: [
            const _SectionHeader(
              title: 'Overview',
              subtitle: 'Your account security at a glance.',
            ),
            ref.watch(securityOverviewProvider).when(
                  loading: () => const _Loading(),
                  error: (_, _) => const _Message("Couldn't load your security overview. Pull down to retry."),
                  data: _overview,
                ),
            const Divider(height: 32),
            if (widget.thisDeviceId != null) ...[
              const _SectionHeader(
                title: "Confirm it's you",
                subtitle: 'Unlock sensitive actions (like recovery codes on the web) for a few minutes.',
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                child: OutlinedButton(
                  onPressed: _busyId == 'stepup' ? null : _stepUpNow,
                  child: Text(_busyId == 'stepup' ? 'Confirming…' : "Confirm it's you"),
                ),
              ),
              const Divider(height: 32),
            ],
            const _SectionHeader(
              title: 'Password',
              subtitle: 'Create or change the password you sign in with.',
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: OutlinedButton(
                onPressed: () => context.push(Routes.password),
                child: const Text('Manage password'),
              ),
            ),
            const Divider(height: 32),
            const _SectionHeader(
              title: 'Passkeys',
              subtitle: "Sign in with your fingerprint or face. Passkeys can't be phished.",
            ),
            ref.watch(passkeysProvider).when(
                  loading: () => const _Loading(),
                  error: (_, _) => const _Message("Couldn't load passkeys. Pull down to retry."),
                  data: (list) => list.isEmpty
                      ? const _Message('No passkeys yet.')
                      : Column(
                          children: [
                            for (final passkey in list)
                              ListTile(
                                title: Text(passkey.name ?? 'Passkey'),
                                subtitle: Text('${passkey.platform} · ${passkey.state}'),
                                trailing: _busyId == passkey.id
                                    ? const _SmallSpinner()
                                    : TextButton(
                                        // A passkey is a TrustedDevice server-side, so removal goes
                                        // through the same revocation path — no parallel logic.
                                        onPressed: () => _revokeDevice(passkey),
                                        child: const Text('Remove'),
                                      ),
                              ),
                          ],
                        ),
                ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: OutlinedButton(
                onPressed: _busyId == 'passkey' ? null : _addPasskey,
                child: Text(_busyId == 'passkey' ? 'Waiting…' : 'Add a passkey'),
              ),
            ),
            const Divider(height: 32),
            const _SectionHeader(
              title: 'Trusted devices',
              subtitle: 'Devices that can approve your sign-ins.',
            ),
            devices.when(
              loading: () => const _Loading(),
              // Offline is normal on mobile: keep pull-to-refresh meaningful instead of a dead end.
              error: (_, _) => const _Message('Couldn\'t load devices. Pull down to retry.'),
              data: (list) => list.isEmpty
                  ? const _Message('No trusted devices yet.')
                  : Column(
                      children: [
                        for (final device in list)
                          ListTile(
                            title: Text(device.name ?? 'Unnamed device'),
                            subtitle: Text(
                              [
                                device.platform,
                                device.state,
                                if (device.id == widget.thisDeviceId) 'this device',
                              ].join(' · '),
                            ),
                            trailing: _busyId == device.id
                                ? const _SmallSpinner()
                                : TextButton(
                                    onPressed: () => _revokeDevice(device),
                                    child: const Text('Remove'),
                                  ),
                          ),
                      ],
                    ),
            ),
            // Without this, the whole device-key rail was unreachable: nothing else in the app calls
            // enrollThisDevice, so no user could ever approve a sign-in or step up (D-117).
            if (widget.thisDeviceId == null)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                child: OutlinedButton(
                  onPressed: _busyId == 'enroll' ? null : _enrollThisDevice,
                  child: Text(_busyId == 'enroll' ? 'Setting up…' : 'Set up this device'),
                ),
              ),
            const Divider(height: 32),
            const _SectionHeader(
              title: 'Trusted browsers',
              subtitle: 'Browsers that skip the device approval step. They never skip your password.',
            ),
            ref.watch(trustedBrowsersProvider).when(
                  loading: () => const _Loading(),
                  error: (_, _) => const _Message("Couldn't load browsers. Pull down to retry."),
                  data: (list) => list.isEmpty
                      ? const _Message('No trusted browsers.')
                      : Column(
                          children: [
                            for (final browser in list)
                              ListTile(
                                title: Text(_browserTitle(browser)),
                                subtitle: Text(_browserSubtitle(browser)),
                                trailing: _busyId == browser.id
                                    ? const _SmallSpinner()
                                    : TextButton(
                                        onPressed: () => _revokeBrowser(browser),
                                        child: const Text('Remove'),
                                      ),
                              ),
                          ],
                        ),
                ),
            const Divider(height: 32),
            const _SectionHeader(
              title: "Where you're signed in",
              subtitle: 'Sign out a session you don\'t recognise.',
            ),
            sessions.when(
              loading: () => const _Loading(),
              error: (_, _) => const _Message('Couldn\'t load sessions. Pull down to retry.'),
              data: (list) => list.isEmpty
                  ? const _Message('No active sessions.')
                  : Column(
                      children: [
                        for (final session in list)
                          ListTile(
                            title: Text(session.deviceName ?? 'Unknown device'),
                            subtitle: Text(session.platform ?? 'unknown'),
                            trailing: _busyId == session.id
                                ? const _SmallSpinner()
                                : TextButton(
                                    onPressed: () => _revokeSession(session),
                                    child: const Text('Sign out'),
                                  ),
                          ),
                      ],
                    ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: OutlinedButton(
                onPressed: _busyId == 'signout' ? null : _signOutEverywhere,
                child: Text(_busyId == 'signout' ? 'Signing out…' : 'Sign out everywhere'),
              ),
            ),
            const Divider(height: 32),
            const _SectionHeader(
              title: 'Recovery codes',
              subtitle: 'One-time codes to get back in if you lose every device.',
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: OutlinedButton(
                onPressed: _busyId == 'codes' ? null : _generateRecoveryCodes,
                child: Text(_busyId == 'codes' ? 'Generating…' : 'Generate recovery codes'),
              ),
            ),
            const Divider(height: 32),
            const _SectionHeader(
              title: 'Recent security activity',
              subtitle: 'Sign-ins, password changes, and other events on your account.',
            ),
            ref.watch(securityActivityProvider).when(
                  loading: () => const _Loading(),
                  error: (_, _) => const _Message("Couldn't load activity. Pull down to retry."),
                  data: (list) => list.isEmpty
                      ? const _Message('Nothing recent.')
                      : Column(
                          children: [
                            for (final item in list)
                              ListTile(
                                dense: true,
                                leading: Icon(Icons.circle, size: 10, color: _severityColor(item.severity)),
                                title: Text(securityActivityLabel(item.type)),
                                trailing: Text(relativeTime(item.createdAt),
                                    style: Theme.of(context).textTheme.bodySmall),
                              ),
                          ],
                        ),
                ),
            const Divider(height: 32),
            const _SectionHeader(
              title: 'Lost access?',
              subtitle: 'If you lost your devices, use your recovery codes to get back in.',
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: OutlinedButton(
                onPressed: () => context.push(Routes.recover),
                child: const Text('Recover your account'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 2),
            Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      );
}

class _Message extends StatelessWidget {
  const _Message(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Text(text, style: Theme.of(context).textTheme.bodySmall),
      );
}

class _Loading extends StatelessWidget {
  const _Loading();

  @override
  Widget build(BuildContext context) => const Padding(
        padding: EdgeInsets.all(16),
        child: Center(child: CircularProgressIndicator()),
      );
}

class _SmallSpinner extends StatelessWidget {
  const _SmallSpinner();

  @override
  Widget build(BuildContext context) =>
      const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2));
}
