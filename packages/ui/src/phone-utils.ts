import { parsePhoneNumberFromString, type CountryCode } from "libphonenumber-js";

/**
 * Canonical phone helpers shared by web + admin (Phase 6, D-089). One parsing/validation strategy so
 * every surface agrees with the backend's libphonenumber `PhoneCanonicalizer`.
 */

/**
 * Best-effort canonicalization of a **mixed** sign-in identifier (phone OR email OR username) to E.164.
 * Email/username and anything not phone-shaped is returned unchanged, so it is safe to call on a field
 * that accepts all three. Replaces the legacy "10 digits ⇒ +91" hack with real libphonenumber parsing;
 * still defaults an ambiguous national-format number to `defaultCountry`.
 */
export function toE164Identifier(raw: string, defaultCountry: CountryCode = "IN"): string {
  const trimmed = raw.trim();
  if (!trimmed || trimmed.includes("@")) return trimmed;
  if (!/^[+\d()\-\s]+$/.test(trimmed)) return trimmed; // not phone-shaped (e.g. a username)
  const parsed = parsePhoneNumberFromString(trimmed, trimmed.startsWith("+") ? undefined : defaultCountry);
  // Validity, not just parseability — matching the backend's PhoneCanonicalizer and the Flutter twin,
  // both of which reject a number that parses but cannot exist. Without this check web was the odd one
  // out: it rewrote an impossible number into E.164 shape and sent it on, so the same input produced a
  // different request from web than from mobile.
  return parsed?.isValid() ? parsed.number : trimmed;
}

/** True when `e164` is a valid, complete number per libphonenumber. The backend stays authoritative. */
export function isValidPhone(e164: string): boolean {
  return parsePhoneNumberFromString(e164)?.isValid() ?? false;
}
