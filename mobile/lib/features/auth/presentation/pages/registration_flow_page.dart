import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/session/session_controller.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/registration_status_dto.dart';
import '../providers/auth_providers.dart';

/// Registration ceremony (Phase 2D) — the destination for a new user after the phone-OTP signup that
/// created the account. One route, distinct steps, driven by `/registration/status`:
/// complete profile (name, username, date of birth, optional bio) → set a password → verify email
/// (skippable — email is a recovery channel, never an auth factor) → success.
///
/// The first two are blocking and mirror the server's own `needs_onboarding`, so neither can be
/// skipped past: doing so would land the user on Home only for the router to send them straight back.
/// Phone is verified implicitly by the OTP signup, so it renders as a completed checklist item.
///
/// Session state is only flipped to onboarded on the success screen's Continue, so the router never
/// yanks the user out of the flow mid-way (profile is saved via the raw PATCH, not the onboarding
/// path that marks the session).
class RegistrationFlowPage extends ConsumerStatefulWidget {
  const RegistrationFlowPage({super.key});

  @override
  ConsumerState<RegistrationFlowPage> createState() => _RegistrationFlowPageState();
}

enum _Step { loading, email, profile, password, success }

enum _UsernameCheck { idle, checking, available, taken, tooShort }

class _RegistrationFlowPageState extends ConsumerState<RegistrationFlowPage> {
  _Step _step = _Step.loading;
  RegistrationStatusDto? _status;

  final _emailController = TextEditingController();
  final _emailCodeController = TextEditingController();
  bool _emailCodeStage = false;
  bool _emailBusy = false;
  String? _emailError;

  final _nameController = TextEditingController();
  final _usernameController = TextEditingController();
  final _bioController = TextEditingController();
  DateTime? _dateOfBirth;
  _UsernameCheck _uCheck = _UsernameCheck.idle;
  int _uToken = 0;
  bool _profileBusy = false;

  final _passwordController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _passwordBusy = false;
  String? _passwordError;

  /// The server's policy, read from `GET /v1/auth/password/status` rather than assumed. The form
  /// previously enforced 8 characters while the backend required 12, so a password the user was
  /// allowed to type and confirm was then rejected on submit.
  int _passwordMinLength = _passwordMinLengthFallback;
  static const _passwordMinLengthFallback = 12;

  /// Youngest permitted account holder, mirrored from `Kurx.Domain.Onboarding.MinimumAgeYears`. The
  /// picker's bounds are a convenience; the backend re-validates and is the authority.
  static const _minimumAgeYears = 13;

  bool _finishBusy = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _emailController.dispose();
    _emailCodeController.dispose();
    _nameController.dispose();
    _usernameController.dispose();
    _bioController.dispose();
    _passwordController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final user = ref.read(currentUserProvider);
    if (user != null && user.name.isNotEmpty) _nameController.text = user.name;
    // Best-effort: a failure here leaves the documented default, which matches the server's own
    // minimum, so the form is never more permissive than the policy it is validating against.
    unawaited(() async {
      try {
        final policy = await ref.read(authRemoteDataSourceProvider).passwordStatus();
        if (mounted) setState(() => _passwordMinLength = policy.minLength);
      } on ApiError {
        // Keep the fallback.
      }
    }());
    try {
      final status = await ref.read(authRemoteDataSourceProvider).registrationStatus();
      if (!mounted) return;
      if (status.email != null && status.email!.isNotEmpty) _emailController.text = status.email!;
      setState(() {
        _status = status;
        _step = _firstStep(status);
      });
    } on ApiError {
      // Can't load the checklist (offline/transient): fall back to the gating profile step so
      // registration can still complete.
      if (!mounted) return;
      setState(() => _step = _Step.profile);
    }
  }

  /// Blocking steps first, then the optional one. Email led this list before, which put a skippable
  /// step ahead of the two the account cannot be finished without.
  ///
  /// A password is now REQUIRED, not offered: an account reachable only by SMS code is locked out the
  /// moment the number is lost, and every surface's sign-in screen is password-first. The server agrees
  /// — `needs_onboarding` is false only once one is set — so skipping here would have bounced the user
  /// straight back into this flow on the next launch.
  _Step _firstStep(RegistrationStatusDto s) {
    if (s.needsProfile) return _Step.profile;
    if (!s.hasPassword) return _Step.password;
    if (s.needsEmail) return _Step.email;
    return _Step.success;
  }

  Future<void> _refreshAndAdvance() async {
    try {
      final status = await ref.read(authRemoteDataSourceProvider).registrationStatus();
      if (!mounted) return;
      setState(() {
        _status = status;
        _step = _firstStep(status);
      });
    } on ApiError {
      // Unknown status falls back to the blocking steps, never past them: `?? true` on hasPassword
      // used to mean "assume they have one" and jumped an offline user straight to success, which is
      // how accounts ended up created without ever being asked for a password.
      //
      // The last known status goes through the SAME ladder rather than a second inline copy of it —
      // the copy here ended at `_Step.success` and so skipped email even when the checklist said it
      // was outstanding. No status at all still means the first blocking step.
      if (!mounted) return;
      final last = _status;
      setState(() => _step = last == null ? _Step.profile : _firstStep(last));
    }
  }

  // ── Email step ─────────────────────────────────────────────────────────────

  Future<void> _sendEmail() async {
    setState(() {
      _emailBusy = true;
      _emailError = null;
    });
    try {
      await ref.read(authRemoteDataSourceProvider).startEmailVerification(_emailController.text.trim());
      if (mounted) setState(() => _emailCodeStage = true);
    } on ApiError catch (e) {
      if (mounted) setState(() => _emailError = _emailErrorCopy(e));
    } finally {
      if (mounted) setState(() => _emailBusy = false);
    }
  }

  Future<void> _verifyEmail() async {
    setState(() {
      _emailBusy = true;
      _emailError = null;
    });
    try {
      await ref
          .read(authRemoteDataSourceProvider)
          .completeEmailVerification(_emailController.text.trim(), _emailCodeController.text.trim());
      await _refreshAndAdvance();
      if (mounted) setState(() => _emailBusy = false);
    } on ApiError catch (e) {
      if (mounted) {
        setState(() {
          _emailError = _emailErrorCopy(e);
          _emailBusy = false;
        });
      }
    }
  }

  /// Email is the last step and the only optional one, so skipping it lands on success — the two
  /// blocking steps are already behind the user by the time this button exists.
  void _skipEmail() => setState(() => _step = _Step.success);

  // ── Profile step ─────────────────────────────────────────────────────────────

  Future<void> _checkUsername(String value) async {
    final v = value.trim().toLowerCase();
    final token = ++_uToken;
    if (v.length < 3) {
      setState(() => _uCheck = v.isEmpty ? _UsernameCheck.idle : _UsernameCheck.tooShort);
      return;
    }
    setState(() => _uCheck = _UsernameCheck.checking);
    try {
      final ok = await ref.read(authControllerProvider.notifier).checkUsernameAvailable(v);
      if (!mounted || token != _uToken) return;
      setState(() => _uCheck = ok ? _UsernameCheck.available : _UsernameCheck.taken);
    } on ApiError {
      if (!mounted || token != _uToken) return;
      setState(() => _uCheck = _UsernameCheck.idle);
    }
  }

  bool get _canSaveProfile =>
      !_profileBusy &&
      _nameController.text.trim().isNotEmpty &&
      _uCheck == _UsernameCheck.available &&
      // Required, because the server requires it: `needs_onboarding` stays true without it and the
      // router would send the user straight back here.
      _dateOfBirth != null;

  Future<void> _pickDateOfBirth() async {
    final today = DateTime.now();
    final latest = DateTime(today.year - _minimumAgeYears, today.month, today.day);
    final picked = await showDatePicker(
      context: context,
      // Opens on a plausible adult birth year rather than today — a date-of-birth picker that starts
      // on the current month makes every user scroll through decades.
      initialDate: _dateOfBirth ?? DateTime(today.year - 20, today.month, today.day),
      firstDate: DateTime(today.year - 120),
      lastDate: latest,
      helpText: 'Your date of birth',
      initialDatePickerMode: DatePickerMode.year,
    );
    if (picked != null && mounted) setState(() => _dateOfBirth = picked);
  }

  Future<void> _saveProfile() async {
    setState(() => _profileBusy = true);
    try {
      // Raw PATCH — deliberately NOT the onboarding path that flips session state, so the router
      // doesn't redirect before the success screen.
      await ref.read(authRemoteDataSourceProvider).updateProfile(
            name: _nameController.text.trim(),
            username: _usernameController.text.trim().toLowerCase(),
            dateOfBirth: _dateOfBirth,
            // Optional, and only sent when written — an empty string would overwrite a bio the user
            // had already set from another surface.
            bio: _bioController.text.trim().isEmpty ? null : _bioController.text.trim(),
          );
      RegistrationStatusDto? status;
      try {
        status = await ref.read(authRemoteDataSourceProvider).registrationStatus();
      } on ApiError {
        // Non-fatal — fall back to the local flag below.
      }
      if (!mounted) return;
      setState(() {
        if (status != null) _status = status;
        // One ladder, as everywhere else. When the status call failed we do not know whether a
        // password exists, and the safe unknown is "not yet" — showing the step again is
        // recoverable, skipping it is not.
        final s = _status;
        _step = (s != null && s.hasPassword) ? _firstStep(s) : _Step.password;
      });
    } on ApiError catch (e) {
      if (mounted) KurxFeedback.error(context, e.userMessage);
    } finally {
      if (mounted) setState(() => _profileBusy = false);
    }
  }

  // ── Success ─────────────────────────────────────────────────────────────

  Future<void> _finish() async {
    setState(() => _finishBusy = true);
    try {
      final user = await ref.read(authRepositoryProvider).me();
      ref.read(currentUserProvider.notifier).state = user;
      ref
          .read(sessionControllerProvider.notifier)
          .markAuthenticated(needsOnboarding: user.needsOnboarding);
      if (mounted) context.go(Routes.events);
    } on ApiError catch (e) {
      if (mounted) {
        KurxFeedback.error(context, e.userMessage);
        setState(() => _finishBusy = false);
      }
    }
  }

  String _emailErrorCopy(ApiError e) {
    switch (e.code) {
      case 'invalid_email':
        return 'Enter a valid email address.';
      case 'email_taken':
        return 'That email is already linked to another account.';
      case 'resend_cooldown':
      case 'rate_limited':
        return 'Please wait a moment before requesting another code.';
      case 'invalid_code':
        return "That code isn't right. Check it and try again.";
      default:
        return e.userMessage;
    }
  }

  /// True once the person has put something into this flow that leaving would throw away.
  ///
  /// Registration is a single page with inline stages rather than separate routes, so the system
  /// back gesture — the primary way people navigate on Android — exits the whole flow at once. A
  /// verified phone, a verified email and everything typed go with it, silently.
  bool get _hasProgress =>
      _emailCodeStage ||
      _emailController.text.trim().isNotEmpty ||
      _nameController.text.trim().isNotEmpty ||
      _usernameController.text.trim().isNotEmpty;

  Future<void> _confirmLeave() async {
    final leave = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Leave sign-up?'),
        content: const Text(
          "What you've entered so far won't be saved, and you'll start again from the beginning.",
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Keep going')),
          TextButton(onPressed: () => Navigator.of(ctx).pop(true), child: const Text('Leave')),
        ],
      ),
    );
    if (leave == true && mounted) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      // `canPop: false` only while there is something to lose, so an untouched form still closes on
      // the first gesture — a confirmation that fires when nothing is at stake teaches people to
      // dismiss it without reading.
      canPop: !_hasProgress,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _confirmLeave();
      },
      child: Scaffold(
      appBar: AppBar(title: const Text('Create your account')),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(KSpace.lg),
            child: ContentWidth(
              maxWidth: 420,
              child: switch (_step) {
                _Step.loading => const Padding(
                    padding: EdgeInsets.all(KSpace.xl),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                _Step.email => _buildEmail(context),
                _Step.profile => _buildProfile(context),
                _Step.password => _buildPassword(context),
                _Step.success => _buildSuccess(context),
              },
            ),
          ),
        ),
      ),
      ),
    );
  }

  Widget _checklist(BuildContext context) {
    final c = context.kurx;
    final s = _status;
    if (s == null) return const SizedBox.shrink();
    Widget row(String label, bool done, bool active) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 3),
          child: Row(
            children: [
              Icon(
                done ? Icons.check_circle_rounded : Icons.radio_button_unchecked,
                size: 18,
                color: done ? c.success : (active ? c.accent : c.muted),
              ),
              const SizedBox(width: KSpace.sm),
              Text(label,
                  style: TextStyle(
                    color: done ? c.muted : c.text,
                    fontSize: 13.5,
                    fontWeight: active ? FontWeight.w700 : FontWeight.w500,
                    decoration: done ? TextDecoration.lineThrough : null,
                  )),
            ],
          ),
        );
    return Container(
      margin: const EdgeInsets.only(bottom: KSpace.lg),
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        border: Border.all(color: c.border),
        borderRadius: BorderRadius.circular(KRadius.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Same order as the steps themselves, and as the server's `remaining` list: the two
          // blocking items, then the optional one.
          row('Phone verified', s.phoneVerified, false),
          row('Profile complete', !s.needsProfile, _step == _Step.profile),
          row('Password set', s.hasPassword, _step == _Step.password),
          row('Email verified', s.emailVerified, _step == _Step.email),
        ],
      ),
    );
  }

  Widget _buildEmail(BuildContext context) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        _checklist(context),
        Text('Verify your email',
            style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
        const SizedBox(height: KSpace.xs),
        Text('Used to recover your account and for important notifications.',
            style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35)),
        const SizedBox(height: KSpace.lg),
        TextField(
          controller: _emailController,
          enabled: !_emailCodeStage,
          keyboardType: TextInputType.emailAddress,
          autofillHints: const [AutofillHints.email],
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(labelText: 'Email', hintText: 'you@example.com'),
        ),
        if (_emailCodeStage) ...[
          const SizedBox(height: KSpace.md),
          TextField(
            controller: _emailCodeController,
            keyboardType: TextInputType.number,
            autofillHints: const [AutofillHints.oneTimeCode],
            inputFormatters: [
              FilteringTextInputFormatter.digitsOnly,
              LengthLimitingTextInputFormatter(6),
            ],
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(
              labelText: '6-digit code',
              helperText: 'Sent to ${_emailController.text.trim()}',
            ),
          ),
        ],
        if (_emailError != null) ...[
          const SizedBox(height: KSpace.sm),
          Text(_emailError!, style: TextStyle(color: c.danger, fontSize: 13, fontWeight: FontWeight.w600)),
        ],
        const SizedBox(height: KSpace.lg),
        KurxButton(
          label: _emailCodeStage ? 'Verify email' : 'Send code',
          expand: true,
          loading: _emailBusy,
          onPressed: _emailBusy
              ? null
              : (_emailCodeStage
                  ? (_emailCodeController.text.trim().length == 6 ? _verifyEmail : null)
                  : (_emailController.text.contains('@') ? _sendEmail : null)),
        ),
        const SizedBox(height: KSpace.sm),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            if (_emailCodeStage)
              TextButton(
                onPressed: _emailBusy ? null : () => setState(() => _emailCodeStage = false),
                child: const Text('Change email'),
              )
            else
              const SizedBox.shrink(),
            TextButton(onPressed: _emailBusy ? null : _skipEmail, child: const Text('Skip for now')),
          ],
        ),
      ],
    );
  }

  Widget _buildProfile(BuildContext context) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        _checklist(context),
        Text('Complete your profile',
            style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
        const SizedBox(height: KSpace.xs),
        Text('Your name, a public username, and your date of birth.',
            style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35)),
        const SizedBox(height: KSpace.lg),
        TextField(
          controller: _nameController,
          textCapitalization: TextCapitalization.words,
          // The platform already knows the account holder's name and can offer it; without a hint it
          // never does, and registration asks for what it could have filled in.
          autofillHints: const [AutofillHints.name],
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(labelText: 'Name', hintText: 'Your full name'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: _usernameController,
          autofillHints: const [AutofillHints.newUsername],
          inputFormatters: [
            LengthLimitingTextInputFormatter(20),
            FilteringTextInputFormatter.allow(RegExp(r'[a-zA-Z0-9_]')),
          ],
          onChanged: _checkUsername,
          decoration: InputDecoration(prefixText: '@', hintText: 'yourname', suffixIcon: _usernameSuffix()),
        ),
        const SizedBox(height: KSpace.sm),
        _usernameStatusLine(),
        const SizedBox(height: KSpace.md),
        // Required: age-restricted events carry MinAge/MaxAge eligibility, so this is the only fact
        // on the account a booking rule can check.
        InkWell(
          onTap: _profileBusy ? null : _pickDateOfBirth,
          borderRadius: BorderRadius.circular(KRadius.sm),
          child: InputDecorator(
            decoration: InputDecoration(
              labelText: 'Date of birth',
              helperText: _dateOfBirth == null
                  ? 'You must be at least $_minimumAgeYears to use Kurx'
                  : null,
            ),
            child: Row(
              children: [
                Icon(Icons.cake_outlined, size: 20, color: c.muted),
                const SizedBox(width: KSpace.sm),
                Expanded(
                  child: Text(
                    _dateOfBirth == null
                        ? 'Select your date of birth'
                        : DateFormat('d MMMM yyyy', 'en_IN').format(_dateOfBirth!),
                    style: TextStyle(color: _dateOfBirth == null ? c.muted : c.text),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: _bioController,
          maxLines: 3,
          maxLength: 300,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(
            labelText: 'Bio',
            hintText: 'A line or two about you',
            helperText: 'Optional — you can add this later',
            alignLabelWithHint: true,
          ),
        ),
        const SizedBox(height: KSpace.lg),
        KurxButton(
          label: 'Continue',
          expand: true,
          loading: _profileBusy,
          onPressed: _canSaveProfile ? _saveProfile : null,
        ),
      ],
    );
  }

  Widget _buildPassword(BuildContext context) {
    final c = context.kurx;
    final pwd = _passwordController.text;
    final confirm = _confirmController.text;
    final mismatch = confirm.isNotEmpty && pwd != confirm;
    final tooShort = pwd.isNotEmpty && pwd.length < _passwordMinLength;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        _checklist(context),
        Text('Set a password',
            style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
        const SizedBox(height: KSpace.xs),
        Text('Required — it is how you sign in, and how you get back in if you lose this number.',
            style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35)),
        const SizedBox(height: KSpace.lg),
        TextField(
          controller: _passwordController,
          obscureText: true,
          autofillHints: const [AutofillHints.newPassword],
          onChanged: (_) => setState(() {}),
          decoration: InputDecoration(
            labelText: 'Password',
            errorText: tooShort ? 'At least $_passwordMinLength characters' : null,
          ),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: _confirmController,
          obscureText: true,
          autofillHints: const [AutofillHints.newPassword],
          onChanged: (_) => setState(() {}),
          decoration: InputDecoration(
            labelText: 'Confirm password',
            errorText: mismatch ? 'Passwords do not match' : null,
          ),
        ),
        if (_passwordError != null) ...[
          const SizedBox(height: KSpace.sm),
          Text(_passwordError!, style: TextStyle(color: c.danger, fontSize: 13)),
        ],
        const SizedBox(height: KSpace.lg),
        KurxButton(
          label: 'Set password',
          expand: true,
          loading: _passwordBusy,
          onPressed:
              pwd.length >= _passwordMinLength && pwd == confirm && !_passwordBusy ? _savePassword : null,
        ),
        // No "Skip for now": the server counts an account without a password as unfinished, so
        // skipping only bounced the user back into this flow on the next launch.
      ],
    );
  }

  Future<void> _savePassword() async {
    setState(() {
      _passwordBusy = true;
      _passwordError = null;
    });
    try {
      await ref.read(authRemoteDataSourceProvider).setPassword(_passwordController.text);
      if (!mounted) return;
      setState(() => _passwordBusy = false);
      // Re-read the checklist and let the ladder decide, exactly as the other steps do. This was
      // `_step = _Step.success`, which jumped past `_Step.email` — so on mobile the email step was
      // built, listed in the checklist, and unreachable in a single sitting. Web never had the bug
      // because it always refreshed here.
      await _refreshAndAdvance();
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _passwordBusy = false;
        // `e.userMessage`, not a local switch. The switch here matched `weak_password`, which the
        // backend does not emit, so `password_breached` and `password_contains_identifier` — the two
        // refusals a real user actually hits — both rendered as "Could not set your password."
        _passwordError = e.userMessage;
      });
    }
  }

  Widget? _usernameSuffix() => switch (_uCheck) {
        _UsernameCheck.checking => const Padding(
            padding: EdgeInsets.all(14),
            child: SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2)),
          ),
        _UsernameCheck.available => Icon(Icons.check_circle_rounded, color: context.kurx.success),
        _UsernameCheck.taken || _UsernameCheck.tooShort =>
          Icon(Icons.error_outline_rounded, color: context.kurx.danger),
        _UsernameCheck.idle => null,
      };

  Widget _usernameStatusLine() {
    final (text, color) = switch (_uCheck) {
      _UsernameCheck.checking => ('Checking availability…', context.kurx.muted),
      _UsernameCheck.available => ('@${_usernameController.text.trim().toLowerCase()} is available', context.kurx.success),
      _UsernameCheck.taken => ('That username is taken or reserved. Try another.', context.kurx.danger),
      _UsernameCheck.tooShort => ('Usernames must be at least 3 characters.', context.kurx.danger),
      _UsernameCheck.idle => ('Letters, numbers, and underscores only.', context.kurx.muted),
    };
    return Text(text, style: TextStyle(color: color, fontSize: 13, fontWeight: FontWeight.w600));
  }

  Widget _buildSuccess(BuildContext context) {
    final c = context.kurx;
    final s = _status;
    final suggestPassword = s == null || !s.hasPassword;
    final suggestDevice = s == null || !s.hasTrustedDevice;
    final showTip = suggestPassword || suggestDevice;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.check_circle_rounded, size: 56, color: c.success),
        const SizedBox(height: KSpace.md),
        Text("You're all set!",
            style: TextStyle(color: c.text, fontSize: 22, fontWeight: FontWeight.w800),
            textAlign: TextAlign.center),
        const SizedBox(height: KSpace.xs),
        Text('Your Kurx account is ready.',
            style: TextStyle(color: c.muted, fontSize: 13.5), textAlign: TextAlign.center),
        if (showTip) ...[
          const SizedBox(height: KSpace.lg),
          Container(
            padding: const EdgeInsets.all(KSpace.md),
            decoration: BoxDecoration(
              border: Border.all(color: c.border),
              borderRadius: BorderRadius.circular(KRadius.md),
            ),
            child: Text(
              'To secure your account, you can also'
              '${suggestPassword ? ' set a password' : ''}'
              '${suggestPassword && suggestDevice ? ' and' : ''}'
              '${suggestDevice ? ' add this device' : ''}'
              ' anytime in Security settings.',
              style: TextStyle(color: c.muted, fontSize: 13, height: 1.35),
            ),
          ),
        ],
        const SizedBox(height: KSpace.xl),
        KurxButton(
          label: 'Continue to Kurx',
          expand: true,
          loading: _finishBusy,
          onPressed: _finishBusy ? null : _finish,
        ),
      ],
    );
  }
}
