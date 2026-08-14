/// Maps the backend `security_events` type codes to friendly copy — kept in lock-step with the web
/// `lib/security-activity.ts`. Unknown types are humanized rather than dropped.
const Map<String, String> _labels = {
  'login.succeeded': 'Signed in',
  'login.approved': 'Sign-in approved on a device',
  'login.rejected': 'Sign-in declined on a device',
  'login.password_browser': 'Signed in with password on a trusted browser',
  'password.created': 'Password created',
  'password.changed': 'Password changed',
  'password.reset': 'Password reset',
  'password.locked_out': 'Account locked after failed sign-ins',
  'email.verified': 'Email verified',
  'passkey.registered': 'Passkey added',
  'passkey.registration_failed': 'Passkey setup failed',
  'passkey.assertion_failed': 'Passkey sign-in failed',
  'recovery.codes_generated': 'Recovery codes generated',
  'recovery.code_rejected': 'Recovery code rejected',
  'refresh.reuse_detected': 'Suspicious session reuse blocked',
  'refresh.pop_failed': 'Session security check failed',
  'risk.denied': 'Sign-in blocked by risk checks',
  'challenge.approved': 'Device challenge approved',
  'challenge.signature_invalid': 'Invalid device signature',
  'challenge.match_number_invalid': 'Wrong match number entered',
  'challenge.match_attempts_exhausted': 'Too many match-number attempts',
  'otp.issued': 'One-time code sent',
  'otp.verified': 'One-time code verified',
};

String securityActivityLabel(String type) {
  final known = _labels[type];
  if (known != null) return known;
  final spaced = type.replaceAll(RegExp(r'[._]'), ' ');
  return spaced.isEmpty ? spaced : spaced[0].toUpperCase() + spaced.substring(1);
}

/// A short relative time for the activity feed.
String relativeTime(DateTime when) {
  final secs = DateTime.now().difference(when).inSeconds;
  if (secs < 60) return 'just now';
  final mins = (secs / 60).round();
  if (mins < 60) return '${mins}m ago';
  final hours = (mins / 60).round();
  if (hours < 24) return '${hours}h ago';
  final days = (hours / 24).round();
  if (days < 30) return '${days}d ago';
  return '${when.day}/${when.month}/${when.year}';
}
