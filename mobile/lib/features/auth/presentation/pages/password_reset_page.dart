import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/util/phone_utils.dart';
import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/session/session_controller.dart';
import '../../../../core/storage/token_store.dart';
import '../../../../core/theme/design_tokens.dart';
import '../password_policy.dart';
import '../providers/auth_providers.dart';
import '../widgets/password_field.dart';

/// Forgot-password → reset ceremony (Phase 2C, D-127, INV-B). Anonymous — the premise is that the user
/// can't sign in. OTP alone can never reset a password, but factor 2 is EITHER a recovery code OR a
/// satisfied step-up: `PasswordResetService` consumes a code when one is supplied and falls back to
/// `stepUp.StatusAsync(...).Satisfied` when it is not. See `_canComplete` for why this screen no longer
/// demands the code. On success the backend re-secures the account (every session + trusted browser
/// revoked) and returns a fresh session, so this ends signed in.
class PasswordResetPage extends ConsumerStatefulWidget {
  const PasswordResetPage({super.key});

  @override
  ConsumerState<PasswordResetPage> createState() => _PasswordResetPageState();
}

enum _Step { identify, reset }

class _PasswordResetPageState extends ConsumerState<PasswordResetPage> {
  _Step _step = _Step.identify;
  final _identifierController = TextEditingController();
  final _otpController = TextEditingController();
  final _recoveryController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _identifierController.dispose();
    _otpController.dispose();
    _recoveryController.dispose();
    _passwordController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _sendCode() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      // Always succeeds by design — never reveals whether the account exists.
      await ref
          .read(authRemoteDataSourceProvider)
          .startPasswordReset(toE164Identifier(_identifierController.text));
      if (mounted) setState(() => _step = _Step.reset);
    } on ApiError {
      if (mounted) setState(() => _error = "Couldn't start the reset. Please try again.");
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _complete() async {
    setState(() => _error = null);
    final issue = passwordIssue(_passwordController.text);
    if (issue != null) {
      setState(() => _error = issue);
      return;
    }
    if (_passwordController.text != _confirmController.text) {
      setState(() => _error = "The two passwords don't match.");
      return;
    }
    setState(() => _busy = true);
    try {
      final tokens = await ref.read(authRemoteDataSourceProvider).completePasswordReset(
            identifier: toE164Identifier(_identifierController.text),
            otpCode: _otpController.text.trim(),
            newPassword: _passwordController.text,
            recoveryCode: _recoveryController.text.trim(),
          );
      // The reset logged us in — same post-login transition as OTP/password login.
      await ref.read(tokenStoreProvider).save(access: tokens.accessToken, refresh: tokens.refreshToken);
      final user = await ref.read(authRepositoryProvider).me();
      ref.read(currentUserProvider.notifier).state = user;
      ref
          .read(sessionControllerProvider.notifier)
          .markAuthenticated(needsOnboarding: user.needsOnboarding);
      if (mounted) context.go(user.needsOnboarding ? Routes.onboarding : Routes.events);
    } on ApiError catch (e) {
      if (mounted) setState(() => _error = passwordErrorCopy(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Reset password')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(KSpace.lg),
          child: ContentWidth(
            maxWidth: 420,
            child: _step == _Step.identify ? _buildIdentify(context) : _buildReset(context),
          ),
        ),
      ),
    );
  }

  Widget _buildIdentify(BuildContext context) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Reset your password',
            style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
        const SizedBox(height: KSpace.xs),
        Text("We'll text a code to your registered number. You'll also need a second factor — a saved recovery code, or a device you've already verified.",
            style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35)),
        const SizedBox(height: KSpace.lg),
        TextField(
          controller: _identifierController,
          keyboardType: TextInputType.emailAddress,
          autofillHints: const [AutofillHints.username],
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(labelText: 'Phone, email, or username'),
        ),
        if (_error != null) ...[
          const SizedBox(height: KSpace.sm),
          Text(_error!, style: TextStyle(color: c.danger, fontSize: 13, fontWeight: FontWeight.w600)),
        ],
        const SizedBox(height: KSpace.lg),
        KurxButton(
          label: 'Send code',
          expand: true,
          loading: _busy,
          onPressed: (_busy || _identifierController.text.trim().length < 3) ? null : _sendCode,
        ),
      ],
    );
  }

  Widget _buildReset(BuildContext context) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Enter your codes and a new password',
            style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
        const SizedBox(height: KSpace.xs),
        // Kept to the original's line count on purpose — this column has no scroll headroom on a
        // small viewport, and a third wrapped line pushes the submit button out of reach.
        Text("If that account exists, we sent a code to it. Add a recovery code, or reset from a device you've already verified.",
            style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35)),
        const SizedBox(height: KSpace.lg),
        TextField(
          controller: _otpController,
          keyboardType: TextInputType.number,
          // Same one-time-code hint as the sign-in OTP: this screen is reached by someone locked
          // out, which is the worst moment to make them retype a code by hand.
          autofillHints: const [AutofillHints.oneTimeCode],
          inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(labelText: 'Code from your phone'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: _recoveryController,
          onChanged: (_) => setState(() {}),
          // "(optional)" in the label, not a helperText line: the body copy above already names both
          // factors, and a third line here pushes the submit button off a small viewport.
          decoration: const InputDecoration(
            labelText: 'Recovery code (optional)',
            hintText: 'a1b2c3d4-e5f6a7b8',
          ),
        ),
        const SizedBox(height: KSpace.md),
        PasswordField(
          controller: _passwordController,
          label: 'New password',
          showStrength: true,
          autofillHints: const [AutofillHints.newPassword],
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: KSpace.md),
        PasswordField(
          controller: _confirmController,
          label: 'Confirm new password',
          autofillHints: const [AutofillHints.newPassword],
          textInputAction: TextInputAction.done,
          onChanged: (_) => setState(() {}),
          onSubmitted: _busy ? null : _complete,
        ),
        if (_error != null) ...[
          const SizedBox(height: KSpace.sm),
          Text(_error!, style: TextStyle(color: c.danger, fontSize: 13, fontWeight: FontWeight.w600)),
        ],
        const SizedBox(height: KSpace.lg),
        KurxButton(
          label: 'Reset password',
          expand: true,
          loading: _busy,
          onPressed: _canComplete ? _complete : null,
        ),
        const SizedBox(height: KSpace.sm),
        Text('Resetting signs out every existing session.',
            style: TextStyle(color: c.muted, fontSize: 12)),
      ],
    );
  }

  /// The recovery code is deliberately NOT required here. `PasswordResetService` takes either a
  /// recovery code or a satisfied step-up as factor 2, and requiring the code locally refused every
  /// account that never minted one — which is most of them, since codes only ever come from an
  /// explicit, step-up-gated call. The server decides and answers `second_factor_required`.
  bool get _canComplete =>
      !_busy &&
      _otpController.text.trim().isNotEmpty &&
      _passwordController.text.isNotEmpty &&
      _confirmController.text.isNotEmpty;
}
