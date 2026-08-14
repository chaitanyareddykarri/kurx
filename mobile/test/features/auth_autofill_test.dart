import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// One-time codes must be offerable, and secrets must not be.
///
/// Web's Phase 16 found its OTP field had no `autocomplete="one-time-code"`, so the SMS autofill
/// never fired. Phase 36 found the same defect on Flutter, where it costs more: the message arrives
/// on the *same device*, a notification away, and without the hint the platform never offers it.
///
/// The inverse matters just as much, which is why the second test exists. Two fields here must stay
/// un-hinted, and adding a hint to either would be a security regression, not an improvement:
///
///  * the **recovery code** is a secret the holder keeps out of band;
///  * the **D-181 approval challenge** is typed from the *other* screen on purpose — "a request they
///    did not start cannot be approved, which is the whole defence".
void main() {
  String read(String p) => File('lib/features/auth/presentation/pages/$p').readAsStringSync();

  group('one-time codes are offerable', () {
    test('every OTP field carries the one-time-code hint', () {
      for (final page in ['otp_verify_page.dart', 'password_reset_page.dart', 'recovery_page.dart']) {
        expect(
          read(page),
          contains('AutofillHints.oneTimeCode'),
          reason: '$page asks for a code the platform could offer and does not say so.',
        );
      }
    });
  });

  group('secrets and challenges stay un-hinted', () {
    test('the approval challenge is never autofilled', () {
      // Autofilling it would let a request the user did not start be approved from this screen.
      expect(read('approve_login_page.dart'), isNot(contains('autofillHints')));
    });

    test('the recovery code field is never autofilled', () {
      final src = read('password_reset_page.dart');
      final recovery = src.indexOf('_recoveryController');
      expect(recovery, greaterThan(-1));
      // Look only at the field's own block, not the whole file — the OTP above it is hinted, rightly.
      final block = src.substring(recovery, (recovery + 400).clamp(0, src.length));
      expect(block, isNot(contains('autofillHints')));
    });
  });
}
