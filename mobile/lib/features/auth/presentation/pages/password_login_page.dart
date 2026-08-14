import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/util/phone_utils.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/session/session_controller.dart';
import '../../../../core/storage/token_store.dart';
import '../../data/models/trusted_device_dtos.dart';
import '../../data/repositories/trusted_device_repository.dart';
import '../providers/auth_providers.dart';

/// Password-first sign-in (Feature 1 / Phase 2B, D-182), the primary login screen across every client.
///
/// Factor 1 is the password. Factor 2 on mobile is always a **trusted device**: the backend either
/// returns a session immediately (a device is already trusted for this account) or pushes a
/// device-approval challenge, which this screen then waits on — showing the match number the user
/// confirms on their other device (the anti-push-fatigue defence, D-181). A one-time code remains the
/// bootstrap path for accounts with no password/device yet, reached via the link below.
class PasswordLoginPage extends ConsumerStatefulWidget {
  const PasswordLoginPage({super.key});

  @override
  ConsumerState<PasswordLoginPage> createState() => _PasswordLoginPageState();
}

class _PasswordLoginPageState extends ConsumerState<PasswordLoginPage> {
  final _formKey = GlobalKey<FormState>();
  final _idController = TextEditingController();
  final _pwController = TextEditingController();

  bool _busy = false;
  String? _error;

  /// Whether this device has a usable platform authenticator. Resolved once on init; false keeps the
  /// passkey button hidden rather than showing one that cannot work.
  bool _passkeysAvailable = false;

  // Non-null while a device-approval challenge is in flight; the waiting view then owns the screen.
  PasswordLoginDto? _approval;

  // Non-null once the password succeeded and the backend offered a choice of second factors (D-280).
  PasswordLoginDto? _secondFactor;
  SecondFactorMethodDto? _chosenMethod;
  String _code = '';

  String _approvalStatus = 'pending';
  int _secondsLeft = 0;
  bool _settled = false;
  Timer? _poll;
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _checkPasskeySupport();
  }

  /// Best-effort: a device without a platform authenticator simply never sees the button. A failure here
  /// must not block the password form, which is the path every account can always use.
  Future<void> _checkPasskeySupport() async {
    try {
      final available = await ref.read(trustedDeviceRepositoryProvider).passkeysAvailable();
      if (mounted) setState(() => _passkeysAvailable = available);
    } catch (_) {
      // Leave the button hidden.
    }
  }

  /// Signs in with a passkey. Needs the identifier because the server resolves this account's credential
  /// set from it; the assertion itself is produced by the platform authenticator, not by us.
  Future<void> _signInWithPasskey() async {
    final identifier = _idController.text.trim();
    if (identifier.isEmpty) {
      setState(() => _error = 'Enter your phone, email, or username first.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(trustedDeviceRepositoryProvider).loginWithPasskey(toE164Identifier(identifier));
      final user = await ref.read(authRepositoryProvider).me();
      ref.read(currentUserProvider.notifier).state = user;
      ref
          .read(sessionControllerProvider.notifier)
          .markAuthenticated(needsOnboarding: user.needsOnboarding);
      if (mounted) context.go(user.needsOnboarding ? Routes.onboarding : Routes.events);
    } on ApiError catch (e) {
      if (mounted) setState(() => _error = e.userMessage);
    } catch (_) {
      // A cancelled biometric prompt lands here and is a user choice, not an error worth shouting about.
      if (mounted) setState(() => _error = 'Passkey sign-in was not completed.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  void dispose() {
    _stopTimers();
    _idController.dispose();
    _pwController.dispose();
    super.dispose();
  }

  void _stopTimers() {
    _poll?.cancel();
    _tick?.cancel();
    _poll = null;
    _tick = null;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final res = await ref.read(trustedDeviceRemoteDataSourceProvider).loginPassword(
            identifier: toE164Identifier(_idController.text),
            password: _pwController.text,
          );
      if (res.needsDeviceApproval) {
        _startApprovalWait(res);
      } else if (res.needsSecondFactor) {
        // The backend told us which factors this account actually has, already ranked (D-283). This page
        // renders that list; it never decides what is available.
        setState(() {
          _secondFactor = res;
          _chosenMethod = null;
          _code = '';
        });
      } else {
        await _completeSession(res.accessToken!, res.refreshToken!);
      }
    } on ApiError catch (e) {
      if (mounted) setState(() => _error = _loginErrorCopy(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Same post-login transition as OTP verify — save tokens, load /v1/me for the real onboarding
  /// flag (D-037), then flip session state so the router redirects. No parallel logic.
  Future<void> _completeSession(String access, String refresh) async {
    await ref.read(tokenStoreProvider).save(access: access, refresh: refresh);
    final user = await ref.read(authRepositoryProvider).me();
    ref.read(currentUserProvider.notifier).state = user;
    ref
        .read(sessionControllerProvider.notifier)
        .markAuthenticated(needsOnboarding: user.needsOnboarding);
    if (mounted) context.go(user.needsOnboarding ? Routes.onboarding : Routes.events);
  }

  void _startApprovalWait(PasswordLoginDto challenge) {
    _settled = false;
    setState(() {
      _approval = challenge;
      _approvalStatus = 'pending';
      _secondsLeft = _remaining(challenge.expiresAt);
    });
    _poll = Timer.periodic(const Duration(seconds: 2), (_) => _pollApproval());
    _tick = Timer.periodic(const Duration(seconds: 1), (_) {
      final left = _remaining(challenge.expiresAt);
      if (mounted && left != _secondsLeft) setState(() => _secondsLeft = left);
    });
  }

  Future<void> _pollApproval() async {
    final a = _approval;
    if (a == null || _settled) return;
    try {
      final status = await ref
          .read(trustedDeviceRemoteDataSourceProvider)
          .loginStatus(challengeId: a.challengeId!, pollToken: a.pollToken!);
      if (status.isApproved) {
        _settled = true;
        _stopTimers();
        await _completeSession(status.accessToken!, status.refreshToken!);
        return;
      }
      if (status.isTerminal) {
        _settled = true;
        _stopTimers();
        if (mounted) setState(() => _approvalStatus = status.status);
      }
    } on ApiError {
      // Transient poll failure (offline/blip): keep polling. The countdown flips to expired if the
      // challenge dies, and an approval that already landed is still there on the next tick.
    }
  }

  void _cancelApproval() {
    _stopTimers();
    _settled = false;
    setState(() {
      _approval = null;
      _approvalStatus = 'pending';
    });
  }

  String _loginErrorCopy(ApiError e) {
    switch (e.code) {
      case 'invalid_credentials':
        return 'Incorrect phone, email, or username, or password.';
      case 'account_locked':
        return 'Too many attempts — this account is locked for a few minutes. Try again shortly.';
      case 'no_second_factor':
        // Reachable only when the account has nothing at all — no phone, no verified email, no device,
        // no passkey, no recovery code. With any one of those the backend offers it instead (D-280).
        return "Your password is correct, but there's no way to confirm it's you on this account yet. "
            "Add a phone number or verify your email to finish signing in.";
      default:
        return e.userMessage;
    }
  }

  static int _remaining(DateTime? expiresAt) => expiresAt == null
      ? 0
      : expiresAt.difference(DateTime.now()).inSeconds.clamp(0, 1 << 30);

  /// Leaves sign-in without trapping the user or dumping them out of the app.
  ///
  /// This screen is reached two ways: *pushed* from a guest-aware screen (Home/Profile/Settings),
  /// which leaves something to pop; and by the router's sign-in gate, which is a **redirect** and
  /// therefore replaces the stack, leaving nothing. In the second case there is no back entry at
  /// all, so the system gesture would pop the last route and close the app. Home is the honest
  /// destination — it is where a signed-out visitor is allowed to be.
  /// `Navigator.canPop`, not go_router's `context.canPop()` — the latter asserts outright when no
  /// GoRouter sits above it, which is any widget test pumping this page on its own.
  void _leave() =>
      Navigator.canPop(context) ? Navigator.pop(context) : context.go(Routes.events);

  @override
  Widget build(BuildContext context) {
    final canPop = Navigator.canPop(context);
    return PopScope(
      // Only intercept when there is genuinely nothing behind this screen; when there is, the
      // ordinary pop keeps Android's predictive-back animation intact.
      canPop: canPop,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) context.go(Routes.events);
      },
      child: Scaffold(
        // Explicit, because AppBar's automatic one appears only when the route can pop — which,
        // arriving via the gate's redirect, it cannot. Sign-in is never a dead end.
        appBar: AppBar(
          title: const Text('Sign in'),
          leading: IconButton(
            icon: Icon(canPop ? Icons.arrow_back : Icons.close),
            tooltip: canPop ? 'Back' : 'Continue browsing',
            onPressed: _leave,
          ),
        ),
        body: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: _secondFactor != null
                    ? _buildSecondFactor(context)
                    : _approval == null
                        ? _buildForm(context)
                        : _buildWaiting(context),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildForm(BuildContext context) {
    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Welcome to Kurx',
              style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
          const SizedBox(height: 8),
          Text('Sign in with your password.',
              style: Theme.of(context).textTheme.bodyMedium, textAlign: TextAlign.center),
          const SizedBox(height: 28),
          TextFormField(
            controller: _idController,
            keyboardType: TextInputType.emailAddress,
            autofillHints: const [AutofillHints.username],
            textInputAction: TextInputAction.next,
            decoration: const InputDecoration(
              labelText: 'Phone, email, or username',
            ),
            validator: (v) => (v ?? '').trim().isEmpty ? 'Enter your phone, email, or username' : null,
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _pwController,
            obscureText: true,
            autofillHints: const [AutofillHints.password],
            textInputAction: TextInputAction.done,
            decoration: const InputDecoration(labelText: 'Password'),
            validator: (v) => (v ?? '').isEmpty ? 'Enter your password' : null,
            onFieldSubmitted: (_) => _busy ? null : _submit(),
          ),
          const SizedBox(height: 20),
          FilledButton(
            onPressed: _busy ? null : _submit,
            child: _busy
                ? const SizedBox(
                    height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Sign in'),
          ),
          // Passkey sign-in (M1). The repository has implemented this since AM3 and no screen called it, so
          // a user could enrol a passkey from Security and then never sign in with one. Shown only when the
          // platform actually has an authenticator — offering a button that always fails is worse than not
          // offering it — and it needs the identifier, because the server resolves the credential set from it.
          if (_passkeysAvailable) ...[
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _busy ? null : _signInWithPasskey,
              icon: const Icon(Icons.fingerprint),
              label: const Text('Sign in with a passkey'),
            ),
          ],
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
                textAlign: TextAlign.center),
          ],
          const SizedBox(height: 8),
          TextButton(
            onPressed: _busy ? null : () => context.push(Routes.resetPassword),
            child: const Text('Forgot password?'),
          ),
          // The ONLY one-time-code entry point on this screen, and it is framed as sign-up on
          // purpose (D-320). The OTP ceremony both signs in a known number and creates an account
          // for an unknown one (D-011/D-037), so the "use a one-time code instead" shortcut that
          // used to sit beside the password field was a one-tap way to skip the password entirely —
          // which every returning user took, billing an SMS on each login. A returning user who
          // cannot use their password goes through "Forgot password?"; an account that holds any
          // usable factor is offered one by the backend after a correct password (D-280/D-283).
          TextButton(
            onPressed: _busy ? null : () => context.push(Routes.loginOtp),
            child: const Text('New to Kurx? Create an account'),
          ),
        ],
      ),
    );
  }

  /// Asks the server to deliver a code for the chosen method, then switches to code entry.
  Future<void> _sendCode(SecondFactorMethodDto method) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(trustedDeviceRemoteDataSourceProvider).sendSecondFactorCode(
            challengeId: _secondFactor!.challengeId!,
            pollToken: _secondFactor!.pollToken!,
            method: method.method,
          );
      if (mounted) setState(() => _chosenMethod = method);
    } on ApiError catch (e) {
      if (mounted) setState(() => _error = e.userMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _verifyCode() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final tokens = await ref.read(trustedDeviceRemoteDataSourceProvider).verifySecondFactorCode(
            challengeId: _secondFactor!.challengeId!,
            pollToken: _secondFactor!.pollToken!,
            code: _code,
          );
      await _completeSession(tokens.accessToken, tokens.refreshToken);
    } on ApiError {
      // Never distinguishes a wrong code from an expired challenge — the server deliberately does not
      // either, and the attempt cap lives in the OTP platform.
      if (mounted) {
        setState(() => _error = "That code didn't work. Check the digits, or request a new one.");
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Renders exactly the methods the backend returned, in the order it returned them (D-283). There is
  /// deliberately no local list and no local ranking here — that is what keeps mobile, web and admin
  /// showing the same thing.
  Widget _buildSecondFactor(BuildContext context) {
    final methods = _secondFactor!.methods;
    final chosen = _chosenMethod;

    if (chosen != null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Enter your code',
              style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
          const SizedBox(height: 8),
          Text('We sent a 6-digit code to ${chosen.hint ?? 'you'}.',
              style: Theme.of(context).textTheme.bodyMedium, textAlign: TextAlign.center),
          const SizedBox(height: 24),
          TextField(
            autofocus: true,
            keyboardType: TextInputType.number,
            maxLength: 6,
            autofillHints: const [AutofillHints.oneTimeCode],
            decoration: const InputDecoration(labelText: '6-digit code', counterText: ''),
            onChanged: (v) => setState(() => _code = v.replaceAll(RegExp(r'\D'), '')),
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: _busy || _code.length != 6 ? null : _verifyCode,
            child: _busy
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Verify and sign in'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
                textAlign: TextAlign.center),
          ],
          TextButton(
            onPressed: _busy
                ? null
                : () => setState(() {
                      _chosenMethod = null;
                      _code = '';
                      _error = null;
                    }),
            child: const Text('Use a different method'),
          ),
        ],
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('One more step',
            style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
        const SizedBox(height: 8),
        Text("Confirm it's you to finish signing in.",
            style: Theme.of(context).textTheme.bodyMedium, textAlign: TextAlign.center),
        const SizedBox(height: 24),
        for (final method in methods)
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: method.isCodeBased
                ? FilledButton(
                    onPressed: _busy ? null : () => _sendCode(method),
                    child: Text(method.hint == null
                        ? method.label
                        : '${method.label} (${method.hint})'),
                  )
                // Not code-delivered: the trusted-device and passkey ceremonies are reached from their own
                // surfaces, so naming them here is information, not an action.
                : OutlinedButton(
                    onPressed: null,
                    child: Text(method.label),
                  ),
          ),
        if (_error != null) ...[
          const SizedBox(height: 4),
          Text(_error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
              textAlign: TextAlign.center),
        ],
        TextButton(
          onPressed: _busy
              ? null
              : () => setState(() {
                    _secondFactor = null;
                    _chosenMethod = null;
                    _error = null;
                  }),
          child: const Text('Back to sign in'),
        ),
      ],
    );
  }

  Widget _buildWaiting(BuildContext context) {
    final a = _approval!;
    final terminal = _approvalStatus != 'pending';
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Approve on your other device',
            style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
        const SizedBox(height: 8),
        Text('Open Kurx on your trusted device and enter this number to confirm it\'s you:',
            style: Theme.of(context).textTheme.bodyMedium, textAlign: TextAlign.center),
        const SizedBox(height: 24),
        Semantics(
          label: 'Match number ${a.matchNumber}',
          container: true,
          excludeSemantics: true,
          child: Container(
            width: 96,
            height: 96,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              border: Border.all(width: 2, color: Theme.of(context).colorScheme.primary),
              borderRadius: BorderRadius.circular(24),
            ),
            child: Text('${a.matchNumber}', style: Theme.of(context).textTheme.displaySmall),
          ),
        ),
        const SizedBox(height: 24),
        if (!terminal) ...[
          const CircularProgressIndicator(),
          const SizedBox(height: 16),
          Text('Waiting for approval…',
              style: Theme.of(context).textTheme.bodyLarge, textAlign: TextAlign.center),
          if (_secondsLeft > 0) ...[
            const SizedBox(height: 8),
            Text('${_secondsLeft}s left',
                semanticsLabel: '$_secondsLeft seconds left',
                style: Theme.of(context).textTheme.bodySmall),
          ],
        ] else
          Text(
            _approvalStatus == 'rejected'
                ? 'That sign-in was declined on your device.'
                : _approvalStatus == 'expired'
                    ? 'This sign-in request expired.'
                    : 'This sign-in request was already used.',
            style: TextStyle(color: Theme.of(context).colorScheme.error),
            textAlign: TextAlign.center,
          ),
        const SizedBox(height: 24),
        TextButton(
          onPressed: _cancelApproval,
          child: Text(terminal ? 'Try again' : 'Cancel'),
        ),
      ],
    );
  }
}
