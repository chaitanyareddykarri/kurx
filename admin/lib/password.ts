/**
 * Client-side password policy + strength, mirroring the backend's NIST SP 800-63B rules
 * (`PasswordPolicy.cs`, D-129) and the web app's `lib/password.ts`: **length is the only hard
 * control** (12–128 chars), no composition rules. The server stays authoritative for the breach
 * deny-list, identifier checks and reuse history, returned as error codes mapped below.
 */

export const PASSWORD_MIN = 12;
export const PASSWORD_MAX = 128;

/** A hard, submit-blocking issue — length only, the same gate the backend applies first. Null = OK. */
export function passwordIssue(password: string): string | null {
  if (password.length < PASSWORD_MIN) return `Use at least ${PASSWORD_MIN} characters.`;
  if (password.length > PASSWORD_MAX) return `Use at most ${PASSWORD_MAX} characters.`;
  return null;
}

export type Strength = { score: 0 | 1 | 2 | 3 | 4; label: string };

/** A soft hint only — never a submit gate. Rewards length (what actually resists guessing). */
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

/** Maps the backend's stable password error codes to copy the user can act on. */
export function passwordErrorCopy(code: string, status?: number): string {
  switch (code) {
    case "password_too_short":
      return `Use at least ${PASSWORD_MIN} characters.`;
    case "password_too_long":
      return `That password is too long (max ${PASSWORD_MAX}).`;
    case "password_breached":
      return "That password is too common. Choose something less guessable.";
    case "password_contains_identifier":
      return "Don't use your name, email, or phone number in your password.";
    case "password_reused":
      return "You've used that password recently. Choose a new one.";
    case "password_already_set":
      return "You already have a password. Use Change password instead.";
    case "password_not_set":
      return "You don't have a password yet. Create one first.";
    case "invalid_credentials":
      return "Your current password is incorrect.";
    case "account_locked":
      return "Too many attempts — try again in a few minutes.";
    case "invalid_reset":
      return "That code or recovery code didn't work. Check both and try again.";
    case "second_factor_required":
      // Both factors named, because this is the refusal a user with neither actually hits, and
      // "a recovery code is required" sent them looking for something they never had.
      return "Add a recovery code, or reset from a device you've already verified.";
    default:
      return status === 423 ? "Too many attempts — try again shortly." : "Couldn't update your password. Please try again.";
  }
}
