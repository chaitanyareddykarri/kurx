/**
 * Client-side password policy + strength, mirroring the backend's NIST SP 800-63B rules
 * (`PasswordPolicy.cs`, D-129): **length is the only hard control** — 12–128 chars, and deliberately
 * NO composition rules (uppercase/symbol requirements push users to predictable substitutions). The
 * server remains authoritative for the breach deny-list, identifier checks and reuse history; those
 * come back as error codes mapped by `passwordErrorCopy`.
 */

import { problemMessage } from "@kurx/ui";

export const PASSWORD_MIN = 12;
export const PASSWORD_MAX = 128;

/** A hard, submit-blocking issue the client can check without a round-trip. Length only — the same
 *  gate the backend applies first. Returns null when the password is submittable. */
export function passwordIssue(password: string): string | null {
  if (password.length < PASSWORD_MIN) return `Use at least ${PASSWORD_MIN} characters.`;
  if (password.length > PASSWORD_MAX) return `Use at most ${PASSWORD_MAX} characters.`;
  return null;
}

export type Strength = { score: 0 | 1 | 2 | 3 | 4; label: string };

/**
 * A soft hint only — never a submit gate, since the backend has no composition rules. Rewards length
 * (the thing that actually resists guessing) and a little variety, so a long passphrase scores well
 * without being told to add a symbol.
 */
export function passwordStrength(password: string): Strength {
  if (password.length === 0) return { score: 0, label: "" };
  if (password.length < PASSWORD_MIN) return { score: 1, label: "Too short" };

  let score = 2;
  if (password.length >= 16) score += 1;
  const classes = [/[a-z]/, /[A-Z]/, /[0-9]/, /[^A-Za-z0-9]/].filter((re) => re.test(password)).length;
  if (password.length >= 20 || classes >= 3) score += 1;

  const clamped = Math.min(score, 4) as 0 | 1 | 2 | 3 | 4;
  return { score: clamped, label: ["", "Too short", "Fair", "Good", "Strong"][clamped] };
}

/**
 * Copy for the password codes whose wording depends on WHERE you are — "your current password is
 * incorrect" only means anything on a change form.
 *
 * The policy codes (`password_breached`, `password_contains_identifier`, …) deliberately do NOT live
 * here any more: they moved into `PROBLEM_COPY` in `@kurx/ui`, because the signup surfaces render
 * whatever `apiErrorMessage` returns and never called this helper. Two tables for one vocabulary is
 * how registration ended up showing "Something went wrong. Please try again." for a refusal this
 * file already had the right sentence for.
 */
const CONTEXTUAL: Record<string, string> = {
  invalid_credentials: "Your current password is incorrect.",
  account_locked: "Too many attempts — try again in a few minutes.",
  invalid_reset: "That code or recovery code didn't work. Check both and try again.",
  // Both factors named, because this is the refusal a user with neither actually hits, and
  // "a recovery code is required" sent them looking for something they never had.
  second_factor_required: "Add a recovery code, or reset from a device you've already verified."
};

/** Maps the backend's stable password error codes to copy the user can act on. */
export function passwordErrorCopy(code: string, status?: number): string {
  if (CONTEXTUAL[code]) return CONTEXTUAL[code];
  if (status === 423) return "Too many attempts — try again shortly.";
  // Everything else resolves through the one shared table, so this helper and the signup screens
  // can no longer disagree about what `password_breached` says.
  return problemMessage({ code, status });
}
