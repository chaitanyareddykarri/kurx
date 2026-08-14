import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/phone_field.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../providers/auth_providers.dart';

class PhoneEntryPage extends ConsumerStatefulWidget {
  const PhoneEntryPage({super.key});

  @override
  ConsumerState<PhoneEntryPage> createState() => _PhoneEntryPageState();
}

class _PhoneEntryPageState extends ConsumerState<PhoneEntryPage> {
  String _phone = '';
  bool _phoneValid = false;

  Future<void> _submit() async {
    if (!_phoneValid) return;
    FocusScope.of(context).unfocus();
    final ok = await ref.read(authControllerProvider.notifier).requestOtp(_phone);
    if (ok && mounted) context.go(Routes.verify);
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(authControllerProvider);
    final loading = state.isLoading;

    ref.listen(authControllerProvider, (_, next) {
      if (next.hasError && !next.isLoading) {
        final err = next.error;
        final msg = err is ApiError ? err.userMessage : 'Something went wrong. Please try again.';
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(msg)));
      }
    });

    return Scaffold(
      // "Sign in" was only half true: this same code both signs in a known number and creates an
      // account for an unknown one, and the screen never said so.
      appBar: AppBar(title: const Text('Continue with a code')),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Welcome to Kurx',
                      style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
                  const SizedBox(height: 8),
                  // States the branch up front instead of letting the user discover it. The
                  // alternative — telling them here whether the number is already registered —
                  // would answer "does this person have a Kurx account" to anyone who asks.
                  Text(
                    'Enter your phone number and we will text you a code. '
                    "We'll sign you in, or set up a new account if you don't have one yet.",
                    style: Theme.of(context).textTheme.bodyMedium,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 28),
                  PhoneField(
                    autofocus: true,
                    onChanged: (e164, valid) => setState(() {
                      _phone = e164;
                      _phoneValid = valid;
                    }),
                  ),
                  const SizedBox(height: 20),
                  FilledButton(
                    onPressed: (loading || !_phoneValid) ? null : _submit,
                    child: loading
                        ? const SizedBox(
                            height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('Send code'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
