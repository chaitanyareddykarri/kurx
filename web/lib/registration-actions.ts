"use server";

import {
  apiErrorMessage,
  completeEmailVerification,
  getRegistrationStatus,
  requestOtp,
  startEmailVerification,
  verifyOtp,
  type RegistrationStatus
} from "@/lib/api";
import { passwordStatus, setPassword } from "@/lib/auth-api";
import { currentSession, saveSession } from "@/lib/session";

/**
 * Registration ceremony server actions (Phase 2D). Registration is authenticated: it begins with the
 * phone-OTP signup (which mints the session), then the client walks the `remaining` checklist from
 * `/registration/status`. Tokens stay httpOnly server-side, matching the rest of the web auth surface.
 */

type Ok = { ok: true };
type Err = { ok: false; error: string };

/** Step 1 of signup — send the phone OTP. Same endpoint the login OTP fallback uses. */
export async function sendSignupOtpAction(phone: string): Promise<Ok | Err> {
  try {
    await requestOtp(phone);
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

/** Step 2 — verify the OTP, which creates the account (if new) and mints the session. */
export async function verifySignupOtpAction(phone: string, code: string): Promise<Ok | Err> {
  try {
    const tokens = await verifyOtp(phone, code);
    await saveSession(tokens.access_token, tokens.refresh_token);
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

export async function registrationStatusAction(): Promise<
  { ok: true; status: RegistrationStatus } | Err
> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    return { ok: true, status: await getRegistrationStatus(session.accessToken) };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

export async function startEmailVerificationAction(email: string): Promise<Ok | Err> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    await startEmailVerification(session.accessToken, email.trim());
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

export async function completeEmailVerificationAction(email: string, code: string): Promise<Ok | Err> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    await completeEmailVerification(session.accessToken, email.trim(), code.trim());
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

/// `completeProfileAction` lived here — an unused second copy of `completeOnboardingAction`
/// (`lib/actions.ts`), which is what the wizard's form actually calls. It is deleted rather than
/// updated: it did not send the now-required `dateOfBirth`, so wiring it up would have left
/// `complete_profile` outstanding and looped the wizard. Two copies of one rule drifting is exactly
/// the failure D-311 exists to fix.

/**
 * Sets the account's first password — a required registration step (D-311), not an optional extra.
 * The endpoint refuses a second call (`password_already_set`), so this cannot overwrite one.
 */
export async function setPasswordAction(password: string): Promise<Ok | Err> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    await setPassword(session.accessToken, password);
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

/**
 * The server's password policy, so the form never enforces a weaker one of its own — an 8-character
 * minimum on the client against a 12-character backend lets someone type and confirm a password that
 * is then refused on submit.
 */
export async function passwordPolicyAction(): Promise<
  { ok: true; minLength: number } | Err
> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    const status = await passwordStatus(session.accessToken);
    return { ok: true, minLength: status.min_length };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}
