import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../providers/auth_providers.dart';

class OtpVerifyPage extends ConsumerStatefulWidget {
  const OtpVerifyPage({super.key});

  @override
  ConsumerState<OtpVerifyPage> createState() => _OtpVerifyPageState();
}

class _OtpVerifyPageState extends ConsumerState<OtpVerifyPage> {
  final _controller = TextEditingController();
  final _formKey = GlobalKey<FormState>();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    // On success the session flips to authenticated and the router redirects us out.
    await ref.read(authControllerProvider.notifier).verifyOtp(_controller.text.trim());
  }

  @override
  Widget build(BuildContext context) {
    final phone = ref.watch(pendingPhoneProvider);
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
      appBar: AppBar(
        leading: BackButton(onPressed: () => context.go(Routes.login)),
        title: const Text('Enter code'),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      phone == null ? 'Enter the 6-digit code' : 'Enter the code sent to $phone',
                      style: Theme.of(context).textTheme.bodyLarge,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 28),
                    TextFormField(
                      controller: _controller,
                      keyboardType: TextInputType.number,
                      // Web's Phase 16 found this exact defect and fixed it there: without the
                      // one-time-code hint the platform never offers the SMS it just delivered, so
                      // the code has to be memorised, left, and retyped. On mobile it is worse than
                      // on web — the message arrives on the same device, a notification away.
                      autofillHints: const [AutofillHints.oneTimeCode],
                      autofocus: true,
                      textAlign: TextAlign.center,
                      style: const TextStyle(fontSize: 24, letterSpacing: 8),
                      inputFormatters: [
                        FilteringTextInputFormatter.digitsOnly,
                        LengthLimitingTextInputFormatter(6),
                      ],
                      decoration: const InputDecoration(labelText: '6-digit code'),
                      validator: (v) {
                        final t = (v ?? '').trim();
                        if (t.length != 6) return 'Enter the 6-digit code';
                        return null;
                      },
                      onFieldSubmitted: (_) => loading ? null : _submit(),
                    ),
                    const SizedBox(height: 20),
                    FilledButton(
                      onPressed: loading ? null : _submit,
                      child: loading
                          ? const SizedBox(
                              height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('Verify & continue'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
