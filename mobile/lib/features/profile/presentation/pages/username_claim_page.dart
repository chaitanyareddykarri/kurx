import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/profile_providers.dart';

/// Claim/change a public @username — the LinkedIn/GitHub-style claimable-handle
/// flow, for a user who has already completed onboarding (D-037). Availability
/// and claim both hit the real backend.
class UsernameClaimPage extends ConsumerStatefulWidget {
  const UsernameClaimPage({super.key});

  @override
  ConsumerState<UsernameClaimPage> createState() => _UsernameClaimPageState();
}

enum _Check { idle, checking, available, taken, tooShort }

class _UsernameClaimPageState extends ConsumerState<UsernameClaimPage> {
  final _controller = TextEditingController();
  _Check _status = _Check.idle;
  int _token = 0;

  @override
  void initState() {
    super.initState();
    final current = ref.read(profileControllerProvider).username;
    if (current != null) _controller.text = current;
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _check(String value) async {
    final v = value.trim().toLowerCase();
    final token = ++_token;
    if (v.length < 3) {
      setState(() => _status = v.isEmpty ? _Check.idle : _Check.tooShort);
      return;
    }
    setState(() => _status = _Check.checking);
    try {
      final available = await ref.read(profileControllerProvider.notifier).isUsernameAvailable(v);
      if (!mounted || token != _token) return; // a newer keystroke superseded this check
      setState(() => _status = available ? _Check.available : _Check.taken);
    } on ApiError {
      if (!mounted || token != _token) return;
      setState(() => _status = _Check.idle);
    }
  }

  Future<void> _claim() async {
    final v = _controller.text.trim().toLowerCase();
    try {
      await ref.read(profileControllerProvider.notifier).claimUsername(v);
      if (!mounted) return;
      KurxFeedback.success(context, 'Username @$v is yours!');
      context.pop();
    } on ApiError catch (e) {
      if (!mounted) return;
      KurxFeedback.error(context, e.userMessage);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final canClaim = _status == _Check.available;

    return Scaffold(
      appBar: AppBar(title: const Text('Claim username')),
      body: ContentWidth(
        maxWidth: 460,
        child: ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            Text('Pick your public handle',
                style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
            const SizedBox(height: KSpace.xs),
            Text('This is how friends find your Kurx profile — like kurx.app/@yourname.',
                style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35)),
            const SizedBox(height: KSpace.xl),
            TextField(
              controller: _controller,
              autofocus: true,
              inputFormatters: [
                LengthLimitingTextInputFormatter(20),
                FilteringTextInputFormatter.allow(RegExp(r'[a-zA-Z0-9_]')),
              ],
              onChanged: _check,
              decoration: InputDecoration(
                prefixText: '@',
                hintText: 'yourname',
                suffixIcon: _suffix(),
              ),
            ),
            const SizedBox(height: KSpace.sm),
            _statusLine(),
            const SizedBox(height: KSpace.xl),
            KurxButton(
              label: 'Claim username',
              expand: true,
              onPressed: canClaim ? _claim : null,
            ),
          ],
        ),
      ),
    );
  }

  Widget? _suffix() {
    return switch (_status) {
      _Check.checking => const Padding(
          padding: EdgeInsets.all(14),
          child: SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2)),
        ),
      _Check.available => Icon(Icons.check_circle_rounded, color: context.kurx.success),
      _Check.taken || _Check.tooShort => Icon(Icons.error_outline_rounded, color: context.kurx.danger),
      _Check.idle => null,
    };
  }

  Widget _statusLine() {
    final (text, color) = switch (_status) {
      _Check.checking => ('Checking availability…', context.kurx.muted),
      _Check.available => ('@${_controller.text.trim().toLowerCase()} is available', context.kurx.success),
      _Check.taken => ('That username is taken. Try another.', context.kurx.danger),
      _Check.tooShort => ('Usernames must be at least 3 characters.', context.kurx.danger),
      _Check.idle => ('Letters, numbers, and underscores only.', context.kurx.muted),
    };
    return Text(text, style: TextStyle(color: color, fontSize: 13, fontWeight: FontWeight.w600));
  }
}
