import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/security/device_key_service.dart';
import '../../data/models/trusted_device_dtos.dart';
import '../../data/repositories/trusted_device_repository.dart';

/// "Someone is trying to sign in" — the approval surface for push-approval login (AM4/D-080).
///
/// Three things this screen must get right, all of them security rather than polish:
///
/// 1. **The match number is shown, never auto-confirmed.** It is the anti-push-fatigue defence
///    (ADR-A9): the user compares it against the waiting browser before approving. A screen that
///    just says "Approve?" trains people to tap yes, which is exactly the attack.
/// 2. **Reject is as easy as approve, and needs no biometrics.** Declining is always safe; making
///    it harder than approving biases the user toward the dangerous action.
/// 3. **Request context is displayed.** IP and user agent are what let a user notice that the
///    request did not come from them.
class ApproveLoginPage extends ConsumerStatefulWidget {
  const ApproveLoginPage({super.key, required this.deviceId});

  /// This device's trusted-device id — the one whose key will sign.
  final String deviceId;

  @override
  ConsumerState<ApproveLoginPage> createState() => _ApproveLoginPageState();
}

class _ApproveLoginPageState extends ConsumerState<ApproveLoginPage> {
  String? _busyChallengeId;
  String? _error;

  Future<void> _approve(PendingLoginDto challenge, int matchNumber) async {
    setState(() {
      _busyChallengeId = challenge.challengeId;
      _error = null;
    });
    try {
      await ref
          .read(trustedDeviceRepositoryProvider)
          .approveLogin(challenge: challenge, deviceId: widget.deviceId, matchNumber: matchNumber);
      ref.invalidate(pendingLoginsProvider);
      if (mounted) _toast('Sign-in approved.');
    } on DeviceKeyException catch (e) {
      // A cancelled biometric prompt is a normal user choice, not an error to shout about.
      setState(() => _error = e.code == 'user_canceled'
          ? null
          : e.requiresReenrollment
              ? 'Your device security changed. Set this device up again to approve sign-ins.'
              : 'Could not use this device\'s key.');
    } catch (_) {
      setState(() => _error = 'Could not approve. Check your connection and try again.');
    } finally {
      if (mounted) setState(() => _busyChallengeId = null);
    }
  }

  Future<void> _reject(PendingLoginDto challenge) async {
    setState(() => _busyChallengeId = challenge.challengeId);
    try {
      await ref.read(trustedDeviceRepositoryProvider).rejectLogin(challenge.challengeId);
      ref.invalidate(pendingLoginsProvider);
      if (mounted) _toast('Sign-in declined.');
    } catch (_) {
      setState(() => _error = 'Could not decline. Check your connection and try again.');
    } finally {
      if (mounted) setState(() => _busyChallengeId = null);
    }
  }

  void _toast(String message) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));

  @override
  Widget build(BuildContext context) {
    final pending = ref.watch(pendingLoginsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Sign-in requests')),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(pendingLoginsProvider),
        child: pending.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          // Offline is the common case for a phone; say something actionable and keep the pull-to-
          // refresh affordance alive rather than replacing the list with a dead end.
          error: (_, _) => ListView(
            children: const [
              SizedBox(height: 120),
              Center(child: Text("Couldn't load requests. Pull down to retry.")),
            ],
          ),
          data: (challenges) {
            if (challenges.isEmpty) {
              return ListView(
                children: const [
                  SizedBox(height: 120),
                  Center(child: Text('No sign-in requests waiting.')),
                ],
              );
            }
            return ListView.builder(
              itemCount: challenges.length,
              itemBuilder: (context, i) => _ChallengeCard(
                challenge: challenges[i],
                busy: _busyChallengeId == challenges[i].challengeId,
                error: _error,
                onApprove: (matchNumber) => _approve(challenges[i], matchNumber),
                onReject: () => _reject(challenges[i]),
              ),
            );
          },
        ),
      ),
    );
  }
}

class _ChallengeCard extends StatefulWidget {
  const _ChallengeCard({
    required this.challenge,
    required this.busy,
    required this.error,
    required this.onApprove,
    required this.onReject,
  });

  final PendingLoginDto challenge;
  final bool busy;
  final String? error;

  /// Called with the two-digit number the user entered (D-181): the device signs "{nonce}.{NN}" over it.
  final void Function(int matchNumber) onApprove;
  final VoidCallback onReject;

  @override
  State<_ChallengeCard> createState() => _ChallengeCardState();

  /// Renders the server's context blob as a human sentence. Best-effort: an unparseable or absent
  /// context simply hides the line rather than blocking the approval.
  static String? _describeContext(String? contextJson) {
    if (contextJson == null || contextJson.isEmpty) return null;
    try {
      final map = jsonDecode(contextJson);
      if (map is! Map) return null;
      final ip = map['ip'] as String?;
      final ua = map['ua'] as String?;
      final parts = [
        if (ip != null && ip.isNotEmpty) 'from $ip',
        if (ua != null && ua.isNotEmpty) ua,
      ];
      return parts.isEmpty ? null : 'Request ${parts.join(' · ')}';
    } on FormatException {
      return null;
    }
  }
}

class _ChallengeCardState extends State<_ChallengeCard> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final describedContext = _ChallengeCard._describeContext(widget.challenge.contextJson);
    final entered = int.tryParse(_controller.text);
    final canApprove = !widget.busy && entered != null && entered >= 10 && entered <= 99;

    return Card(
      margin: const EdgeInsets.all(16),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Approve sign-in?', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            const Text(
              'Only approve if you started this. Enter the two-digit number shown on the screen you '
              'are signing in on — never a number this app shows you.',
            ),
            if (describedContext != null) ...[
              const SizedBox(height: 16),
              Text(describedContext, style: Theme.of(context).textTheme.bodySmall),
            ],
            const SizedBox(height: 20),
            // D-181: the user types the number from the *other* screen. A request they did not start (no
            // waiting browser to read from) therefore cannot be approved — which is the whole defence.
            TextField(
              controller: _controller,
              onChanged: (_) => setState(() {}),
              keyboardType: TextInputType.number,
              maxLength: 2,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.headlineMedium,
              decoration: const InputDecoration(
                labelText: 'Number on your screen',
                counterText: '',
                border: OutlineInputBorder(),
              ),
            ),
            if (widget.error != null) ...[
              const SizedBox(height: 12),
              Text(widget.error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            const SizedBox(height: 20),
            FilledButton(
              onPressed: canApprove ? () => widget.onApprove(entered) : null,
              child: Text(widget.busy ? 'Confirming…' : 'Approve'),
            ),
            const SizedBox(height: 8),
            // Same prominence as approve: declining must never be the harder path.
            OutlinedButton(
              onPressed: widget.busy ? null : widget.onReject,
              child: const Text("No, this wasn't me"),
            ),
          ],
        ),
      ),
    );
  }
}
