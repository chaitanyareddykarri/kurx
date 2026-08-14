import '../../../core/network/api_error.dart';

/// Client-side password policy + strength, mirroring the backend's NIST SP 800-63B rules
/// (`PasswordPolicy.cs`, D-129): **length is the only hard control** (12–128 chars), with NO
/// composition rules. The server stays authoritative for the breach deny-list, identifier checks and
/// reuse history; those come back as error codes mapped by [passwordErrorCopy]. Kept in lock-step with
/// the web `lib/password.ts` so the two clients never drift.
const int passwordMin = 12;
const int passwordMax = 128;

/// A hard, submit-blocking issue — length only, the same gate the backend applies first. Null = OK.
String? passwordIssue(String password) {
  if (password.length < passwordMin) return 'Use at least $passwordMin characters.';
  if (password.length > passwordMax) return 'Use at most $passwordMax characters.';
  return null;
}

class PasswordStrength {
  const PasswordStrength(this.score, this.label);
  final int score; // 0–4
  final String label;
}

/// A soft hint only — never a submit gate (the backend has no composition rules). Rewards length.
PasswordStrength passwordStrength(String password) {
  if (password.isEmpty) return const PasswordStrength(0, '');
  if (password.length < passwordMin) return const PasswordStrength(1, 'Too short');

  var score = 2;
  if (password.length >= 16) score++;
  final classes = [
    RegExp(r'[a-z]'),
    RegExp(r'[A-Z]'),
    RegExp(r'[0-9]'),
    RegExp(r'[^A-Za-z0-9]'),
  ].where((re) => re.hasMatch(password)).length;
  if (password.length >= 20 || classes >= 3) score++;

  final clamped = score.clamp(0, 4);
  return PasswordStrength(clamped, const ['', 'Too short', 'Fair', 'Good', 'Strong'][clamped]);
}

/// Maps the backend's stable password error codes to copy the user can act on.
String passwordErrorCopy(ApiError e) {
  switch (e.code) {
    case 'password_too_short':
      return 'Use at least $passwordMin characters.';
    case 'password_too_long':
      return 'That password is too long (max $passwordMax).';
    case 'password_breached':
      return 'That password is too common. Choose something less guessable.';
    case 'password_contains_identifier':
      return "Don't use your name, email, or phone number in your password.";
    case 'password_reused':
      return "You've used that password recently. Choose a new one.";
    case 'password_already_set':
      return 'You already have a password. Use Change password instead.';
    case 'password_not_set':
      return "You don't have a password yet. Create one first.";
    case 'invalid_credentials':
      return 'Your current password is incorrect.';
    case 'account_locked':
      return 'Too many attempts — try again in a few minutes.';
    case 'invalid_reset':
      return "That code or recovery code didn't work. Check both and try again.";
    case 'second_factor_required':
      // Both factors named, because this is the refusal a user with neither actually hits, and
      // "a recovery code is required" sent them looking for something they never had.
      return "Add a recovery code, or reset from a device you've already verified.";
    default:
      return e.userMessage;
  }
}
