import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../password_policy.dart';
import '../providers/auth_providers.dart';
import '../widgets/password_field.dart';

/// Create / change password (D-126/D-129) — the mobile twin of the web security page's password card.
/// `GET /password/status` decides which: OTP-era accounts have none until they create one; a change
/// requires the current password so a stolen access token can't silently take the account.
class PasswordPage extends ConsumerStatefulWidget {
  const PasswordPage({super.key});

  @override
  ConsumerState<PasswordPage> createState() => _PasswordPageState();
}

class _PasswordPageState extends ConsumerState<PasswordPage> {
  bool? _hasPassword;
  final _currentController = TextEditingController();
  final _nextController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _busy = false;
  String? _error;
  bool _done = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _currentController.dispose();
    _nextController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final status = await ref.read(authRemoteDataSourceProvider).passwordStatus();
      if (mounted) setState(() => _hasPassword = status.hasPassword);
    } on ApiError {
      if (mounted) setState(() => _hasPassword = false);
    }
  }

  Future<void> _submit() async {
    setState(() => _error = null);
    final issue = passwordIssue(_nextController.text);
    if (issue != null) {
      setState(() => _error = issue);
      return;
    }
    if (_nextController.text != _confirmController.text) {
      setState(() => _error = "The two passwords don't match.");
      return;
    }
    setState(() => _busy = true);
    try {
      final remote = ref.read(authRemoteDataSourceProvider);
      if (_hasPassword ?? false) {
        await remote.changePassword(_currentController.text, _nextController.text);
      } else {
        await remote.setPassword(_nextController.text);
      }
      if (mounted) {
        setState(() {
          _done = true;
          _hasPassword = true;
          _currentController.clear();
          _nextController.clear();
          _confirmController.clear();
        });
      }
    } on ApiError catch (e) {
      if (mounted) setState(() => _error = passwordErrorCopy(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final has = _hasPassword;

    return Scaffold(
      appBar: AppBar(title: Text(has ?? false ? 'Change password' : 'Create a password')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(KSpace.lg),
          child: ContentWidth(
            maxWidth: 420,
            child: has == null
                ? const Padding(
                    padding: EdgeInsets.all(KSpace.xl),
                    child: Center(child: CircularProgressIndicator()),
                  )
                : _done
                    ? _buildDone(context)
                    : _buildForm(context, has),
          ),
        ),
      ),
    );
  }

  Widget _buildForm(BuildContext context, bool has) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          has
              ? "You'll be asked for your current password. Changing it signs out untrusted browsers."
              : 'Add a password so you can sign in without a one-time code. At least $passwordMin characters.',
          style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35),
        ),
        const SizedBox(height: KSpace.lg),
        if (has) ...[
          PasswordField(
            controller: _currentController,
            label: 'Current password',
            autofillHints: const [AutofillHints.password],
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: KSpace.md),
        ],
        PasswordField(
          controller: _nextController,
          label: has ? 'New password' : 'Password',
          showStrength: true,
          autofillHints: const [AutofillHints.newPassword],
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: KSpace.md),
        PasswordField(
          controller: _confirmController,
          label: 'Confirm password',
          autofillHints: const [AutofillHints.newPassword],
          textInputAction: TextInputAction.done,
          onChanged: (_) => setState(() {}),
          onSubmitted: _busy ? null : _submit,
        ),
        if (_error != null) ...[
          const SizedBox(height: KSpace.sm),
          Text(_error!, style: TextStyle(color: c.danger, fontSize: 13, fontWeight: FontWeight.w600)),
        ],
        const SizedBox(height: KSpace.lg),
        KurxButton(
          label: has ? 'Change password' : 'Create password',
          expand: true,
          loading: _busy,
          onPressed: _canSubmit(has) ? _submit : null,
        ),
      ],
    );
  }

  bool _canSubmit(bool has) =>
      !_busy &&
      _nextController.text.isNotEmpty &&
      _confirmController.text.isNotEmpty &&
      (!has || _currentController.text.isNotEmpty);

  Widget _buildDone(BuildContext context) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.check_circle_rounded, size: 48, color: c.success),
        const SizedBox(height: KSpace.md),
        Text('Password updated',
            style: TextStyle(color: c.text, fontSize: 18, fontWeight: FontWeight.w800),
            textAlign: TextAlign.center),
        const SizedBox(height: KSpace.lg),
        KurxButton(
          label: 'Change it again',
          variant: KurxButtonVariant.secondary,
          expand: true,
          onPressed: () => setState(() => _done = false),
        ),
      ],
    );
  }
}
