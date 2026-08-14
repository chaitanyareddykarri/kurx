import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/util/phone_utils.dart';
import '../../data/repositories/trusted_device_repository.dart';

/// Account recovery (AM7/D-083) — the way back in when every trusted device is gone.
///
/// The server returns one opaque `invalid_recovery` for every failure (unknown account, wrong OTP,
/// wrong or already-used code), so this screen shows a single generic message rather than inventing
/// more specific ones. Guessing at which half failed would hand an attacker the account enumeration
/// the backend is careful not to leak — the UI must not undo that at the last mile.
class RecoveryPage extends ConsumerStatefulWidget {
  const RecoveryPage({super.key});

  @override
  ConsumerState<RecoveryPage> createState() => _RecoveryPageState();
}

enum _Step { identify, redeem }

class _RecoveryPageState extends ConsumerState<RecoveryPage> {
  final _identifier = TextEditingController();
  final _otp = TextEditingController();
  final _code = TextEditingController();

  _Step _step = _Step.identify;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _identifier.dispose();
    _otp.dispose();
    _code.dispose();
    super.dispose();
  }

  Future<void> _sendCode() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      // Always succeeds by design — never reveals whether the account exists.
      await ref.read(trustedDeviceRepositoryProvider).startRecovery(toE164Identifier(_identifier.text));
      if (mounted) setState(() => _step = _Step.redeem);
    } catch (_) {
      setState(() => _error = "Couldn't start recovery. Check your connection and try again.");
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _redeem() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(trustedDeviceRepositoryProvider).redeemRecovery(
            identifier: toE164Identifier(_identifier.text),
            otpCode: _otp.text.trim(),
            recoveryCode: _code.text.trim(),
          );
      if (!mounted) return;
      // Recovery revoked every session and suspended every device, so setting this device up again
      // is the immediate next task rather than something to discover later.
      Navigator.of(context).pop(true);
    } catch (_) {
      setState(() => _error =
          "That combination didn't work. Check the code from your phone and your recovery code.");
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Recover your account')),
        body: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: _step == _Step.identify ? _identifyStep(context) : _redeemStep(context),
        ),
      );

  Widget _identifyStep(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            "We'll send a code to your registered number. You'll also need one of your saved "
            'recovery codes.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          TextField(
            controller: _identifier,
            keyboardType: TextInputType.phone,
            autofillHints: const [AutofillHints.username],
            decoration: const InputDecoration(
              labelText: 'Phone number or email',
              hintText: 'Email, or phone with country code',
              border: OutlineInputBorder(),
            ),
            onChanged: (_) => setState(() {}),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          const SizedBox(height: 20),
          FilledButton(
            onPressed: _busy || _identifier.text.trim().length < 3 ? null : _sendCode,
            child: Text(_busy ? 'Sending…' : 'Send code'),
          ),
        ],
      );

  Widget _redeemStep(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'If that account exists, we sent a code to it. Enter it along with one of your '
            'recovery codes.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          TextField(
            controller: _otp,
            keyboardType: TextInputType.number,
            autofillHints: const [AutofillHints.oneTimeCode],
            decoration: const InputDecoration(
              labelText: 'Code from your phone',
              border: OutlineInputBorder(),
            ),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _code,
            decoration: const InputDecoration(
              labelText: 'Recovery code',
              hintText: 'a1b2c3d4-e5f6a7b8',
              border: OutlineInputBorder(),
            ),
            style: const TextStyle(fontFamily: 'monospace'),
            onChanged: (_) => setState(() {}),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          const SizedBox(height: 20),
          FilledButton(
            onPressed:
                _busy || _otp.text.trim().isEmpty || _code.text.trim().isEmpty ? null : _redeem,
            child: Text(_busy ? 'Verifying…' : 'Recover account'),
          ),
          const SizedBox(height: 12),
          Text(
            'Recovering signs out every existing session and requires your devices to be set up again.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      );
}
