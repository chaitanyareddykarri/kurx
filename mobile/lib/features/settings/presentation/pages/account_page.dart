import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/phone_field.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../providers/account_providers.dart';

/// Account settings (D-263) — email, phone, username history and language.
///
/// Mirrors the web `/settings/account` page so the two surfaces offer the same things. Deletion,
/// notifications and blocking live on their own screens because each is a decision, not a field.
class AccountPage extends ConsumerStatefulWidget {
  const AccountPage({super.key});

  @override
  ConsumerState<AccountPage> createState() => _AccountPageState();
}

class _AccountPageState extends ConsumerState<AccountPage> {
  final _email = TextEditingController();
  final _emailCode = TextEditingController();
  final _phoneCode = TextEditingController();
  String _phoneE164 = '';
  bool _phoneValid = false;

  bool _emailSent = false;
  bool _phoneSent = false;
  bool _busy = false;
  String? _language;

  @override
  void dispose() {
    _email.dispose();
    _emailCode.dispose();
    _phoneCode.dispose();
    super.dispose();
  }

  void _say(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _run(Future<void> Function() body, {String? success}) async {
    setState(() => _busy = true);
    try {
      await body();
      if (success != null) _say(success);
    } catch (e) {
      _say('$e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Phone change gets an explicit confirm because it is the one action here that decides who can
  /// sign in afterwards, and it signs every other device out. A one-tap version of that is a support
  /// incident (D-263 §5.9); deletion has the same guard for the same reason.
  Future<bool> _confirmPhoneChange() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Change your phone number?'),
        content: const Text(
          'Your phone is how you sign in. Changing it signs you out of every other device, and the '
          'new number becomes the only one that can log in to this account.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Change it')),
        ],
      ),
    );
    return ok ?? false;
  }

  @override
  Widget build(BuildContext context) {
    final history = ref.watch(usernameHistoryProvider);
    final api = ref.read(accountDataSourceProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Account')),
      body: ContentWidth(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // ── Account created ──────────────────────────────────────────────
            // Read-only: the stamp is server-generated on insert and never written again, so there
            // is nothing here to edit. Read from the already-loaded `/v1/me` record rather than a
            // second request — the value is on the user the app is holding.
            Text('Account created', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            Builder(
              builder: (context) {
                final createdAt = ref.watch(currentUserProvider)?.createdAt;
                return Text(
                  createdAt == null
                      ? 'Not available'
                      // `.toLocal()` and nothing else: the server sends UTC, the device knows the
                      // offset. Adding or assuming hours anywhere here is how a timestamp ends up
                      // right in one timezone and wrong in every other.
                      : DateFormat('d MMMM yyyy, h:mm a', 'en_IN').format(createdAt.toLocal()),
                  style: const TextStyle(fontSize: 13),
                );
              },
            ),
            const SizedBox(height: 24),

            // ── Email ────────────────────────────────────────────────────────
            Text('Email', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            const Text(
              'The code goes to the new address. The address on file is told a change was requested.',
              style: TextStyle(fontSize: 12),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              decoration: const InputDecoration(labelText: 'New email', border: OutlineInputBorder()),
            ),
            const SizedBox(height: 8),
            FilledButton(
              onPressed: _busy
                  ? null
                  : () => _run(
                        () async {
                          await api.startEmailChange(_email.text.trim());
                          if (mounted) setState(() => _emailSent = true);
                        },
                        success: 'Code sent.',
                      ),
              child: Text(_emailSent ? 'Resend code' : 'Send code'),
            ),
            if (_emailSent) ...[
              const SizedBox(height: 8),
              TextField(
                controller: _emailCode,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Code', border: OutlineInputBorder()),
              ),
              const SizedBox(height: 8),
              FilledButton(
                onPressed: _busy
                    ? null
                    : () => _run(
                          () async {
                            await api.completeEmailChange(_email.text.trim(), _emailCode.text.trim());
                            if (mounted) setState(() => _emailSent = false);
                          },
                          success: 'Email updated.',
                        ),
                child: const Text('Confirm'),
              ),
            ],

            const Divider(height: 32),

            // ── Phone ────────────────────────────────────────────────────────
            Text('Phone', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            const Text(
              'Your phone is how you sign in, so changing it signs you out everywhere else.',
              style: TextStyle(fontSize: 12),
            ),
            const SizedBox(height: 8),
            // Country picker, not a bare text box. This value is the new sign-in identity: the backend
            // reads digits with no '+' in the legacy region, so a Singapore number typed as "6591234567"
            // — also a well-formed Indian mobile — texted the confirmation code to a real stranger in
            // India and, once verified, moved the account onto their number.
            PhoneField(
              label: 'New phone',
              onChanged: (e164, valid) => setState(() {
                _phoneE164 = e164;
                _phoneValid = valid;
              }),
            ),
            const SizedBox(height: 8),
            FilledButton(
              onPressed: _busy || !_phoneValid
                  ? null
                  : () => _run(
                        () async {
                          await api.requestPhoneOtp(_phoneE164);
                          if (mounted) setState(() => _phoneSent = true);
                        },
                        success: 'Code sent.',
                      ),
              child: Text(_phoneSent ? 'Resend code' : 'Send code'),
            ),
            if (_phoneSent) ...[
              const SizedBox(height: 8),
              TextField(
                controller: _phoneCode,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Code', border: OutlineInputBorder()),
              ),
              const SizedBox(height: 8),
              FilledButton(
                onPressed: _busy
                    ? null
                    : () async {
                        if (!await _confirmPhoneChange()) return;
                        await _run(
                          () async {
                            await api.changePhone(_phoneE164, _phoneCode.text.trim());
                            if (mounted) setState(() => _phoneSent = false);
                          },
                          success: 'Phone updated. Other sessions were signed out.',
                        );
                      },
                child: const Text('Confirm'),
              ),
            ],

            const Divider(height: 32),

            // ── Language ─────────────────────────────────────────────────────
            Text('Language', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            const Text(
              'Saved on your account, so it follows you to your next sign-in.',
              style: TextStyle(fontSize: 12),
            ),
            const SizedBox(height: 8),
            SegmentedButton<String>(
              segments: const [
                ButtonSegment(value: 'en', label: Text('English')),
                ButtonSegment(value: 'hi', label: Text('हिन्दी')),
              ],
              selected: {_language ?? 'en'},
              onSelectionChanged: _busy
                  ? null
                  : (s) => _run(
                        () async {
                          await api.setLanguage(s.first);
                          if (mounted) setState(() => _language = s.first);
                        },
                        success: 'Language saved.',
                      ),
            ),

            const Divider(height: 32),

            // ── Username history ─────────────────────────────────────────────
            Text('Previous usernames', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            const Text(
              'Released handles stay reserved for 30 days before anyone else can take them.',
              style: TextStyle(fontSize: 12),
            ),
            const SizedBox(height: 8),
            history.when(
              loading: () => const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: LinearProgressIndicator(),
              ),
              error: (e, _) => Text('$e'),
              data: (entries) => entries.isEmpty
                  ? const Text('None yet.')
                  : Column(
                      children: [
                        for (final h in entries)
                          ListTile(
                            dense: true,
                            contentPadding: EdgeInsets.zero,
                            title: Text('@${h.username}'),
                            trailing: Text(
                              h.releasedAt?.toLocal().toString().split(' ').first ?? '',
                            ),
                          ),
                      ],
                    ),
            ),
          ],
        ),
      ),
    );
  }
}
