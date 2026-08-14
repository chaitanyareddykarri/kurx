import 'package:phone_numbers_parser/phone_numbers_parser.dart';

/// Canonical phone helpers (Phase 6, D-089) — the Dart twin of the web/admin `@kurx/ui` phone-utils.
/// One parsing/validation strategy (`phone_numbers_parser`, a libphonenumber port) so mobile agrees
/// with the backend's libphonenumber and the web/admin libphonenumber-js.

/// A flag emoji from an ISO 3166-1 alpha-2 code, via regional-indicator code points.
String flagForIso(String iso) => iso
    .toUpperCase()
    .split('')
    .map((c) => String.fromCharCode(0x1F1E6 + c.codeUnitAt(0) - 65))
    .join();

/// Best-effort canonicalization of a **mixed** sign-in identifier (phone OR email OR username) to E.164.
/// Email/username and anything not phone-shaped is returned unchanged, so it is safe on a field that
/// accepts all three. Replaces the legacy "10 digits ⇒ +91" hack with real parsing; still defaults an
/// ambiguous national-format number to [defaultCountry].
String toE164Identifier(String raw, {IsoCode defaultCountry = IsoCode.IN}) {
  final trimmed = raw.trim();
  if (trimmed.isEmpty || trimmed.contains('@')) return trimmed;
  if (!RegExp(r'^[+\d()\-\s]+$').hasMatch(trimmed)) return trimmed; // e.g. a username
  try {
    final parsed = trimmed.startsWith('+')
        ? PhoneNumber.parse(trimmed)
        : PhoneNumber.parse(trimmed, callerCountry: defaultCountry);
    return parsed.isValid() ? parsed.international : trimmed;
  } catch (_) {
    return trimmed;
  }
}

/// True when [e164] is a valid, complete number per the parser. The backend stays authoritative.
bool isValidPhone(String e164) {
  try {
    return PhoneNumber.parse(e164).isValid();
  } catch (_) {
    return false;
  }
}
