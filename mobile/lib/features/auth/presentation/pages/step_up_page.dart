import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/auth_retry.dart';
import '../../../../core/security/device_key_service.dart';
import '../../data/repositories/trusted_device_repository.dart';

/// Step-up authentication (AM6/D-084): a fresh device signature before a sensitive action, so a
/// stolen access token alone cannot perform it.
///
/// Pushed as a gate in front of the action, and pops `true` only once the server has recorded the
/// step-up. Callers must treat any other outcome as "not authorised" — never as a soft failure to
/// be ignored, or the gate is decorative.
class StepUpPage extends ConsumerStatefulWidget {
  const StepUpPage({super.key, required this.action, required this.deviceId, this.reason});

  /// Audit label recorded with the challenge, e.g. `recovery_codes`.
  final String action;

  /// This device's trusted-device id — the key that will sign.
  final String deviceId;

  /// Human explanation of *why* re-confirmation is being asked for.
  final String? reason;

  @override
  ConsumerState<StepUpPage> createState() => _StepUpPageState();
}

class _StepUpPageState extends ConsumerState<StepUpPage> {
  bool _busy = false;
  String? _error;

  Future<void> _confirm() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref
          .read(trustedDeviceRepositoryProvider)
          .stepUp(action: widget.action, deviceId: widget.deviceId);
      if (mounted) Navigator.of(context).pop(true);
    } on DeviceKeyException catch (e) {
      setState(() {
        // Cancelling is a deliberate choice, not an error to shout about.
        if (e.code == 'user_canceled') return;
        _error = e.requiresReenrollment
            ? 'Your device security changed. Set this device up again to continue.'
            : "Couldn't use this device's key.";
      });
    } catch (e) {
      setState(() => _error = AuthRetry.isOffline(e)
          ? "You're offline. Reconnect and try again."
          : "Couldn't confirm it was you. Please try again.");
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Confirm it\'s you')),
        body: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                widget.reason ??
                    'This action changes how you sign in, so we need to confirm it\'s really you.',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 24),
              if (_error != null) ...[
                Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                const SizedBox(height: 16),
              ],
              FilledButton(
                onPressed: _busy ? null : _confirm,
                child: Text(_busy ? 'Confirming…' : 'Confirm with biometrics'),
              ),
              const SizedBox(height: 8),
              TextButton(
                // Cancelling must return a definite "not authorised" rather than an ambiguous null.
                onPressed: _busy ? null : () => Navigator.of(context).pop(false),
                child: const Text('Cancel'),
              ),
            ],
          ),
        ),
      );
}
