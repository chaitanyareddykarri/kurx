"use server";

import {
  apiErrorMessage,
  apiErrorStatus,
  changePassword,
  completePasswordReset,
  passwordStatus,
  setPassword,
  startPasswordReset
} from "@/lib/api";
import { clearSession, currentSession, saveSession } from "@/lib/session";
import { deriveRoles, isStaff } from "@/lib/roles";

/**
 * Staff password management (D-126/D-127/D-129). Create/change run server-side with the session token
 * (the admin console never exposes the token to the client). The reset ceremony is anonymous and, on
 * success, confirms the account is staff before keeping the session — a reset that logs in a non-staff
 * account is useless in the console.
 */

type Result = { ok: true } | { ok: false; error: string; status?: number };

export async function passwordStatusAction(): Promise<{ ok: true; hasPassword: boolean } | { ok: false }> {
  const session = await currentSession();
  if (!session) return { ok: false };
  try {
    const status = await passwordStatus(session.accessToken);
    return { ok: true, hasPassword: status.has_password };
  } catch {
    return { ok: false };
  }
}

export async function setOrChangePasswordAction(
  hasPassword: boolean,
  currentPassword: string,
  newPassword: string
): Promise<Result> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    if (hasPassword) {
      await changePassword(session.accessToken, currentPassword, newPassword);
    } else {
      await setPassword(session.accessToken, newPassword);
    }
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}

/** Anonymous — always reports success (anti-enumeration) unless the network itself fails. */
export async function startPasswordResetAction(identifier: string): Promise<Result> {
  try {
    await startPasswordReset(identifier.trim());
    return { ok: true };
  } catch {
    return { ok: false, error: "start_failed" };
  }
}

export async function completePasswordResetAction(
  identifier: string,
  otpCode: string,
  newPassword: string,
  recoveryCode: string
): Promise<Result> {
  try {
    const tokens = await completePasswordReset(identifier.trim(), otpCode.trim(), newPassword, recoveryCode.trim());
    await saveSession(tokens.access_token, tokens.refresh_token);
    const session = await currentSession();
    if (!session || !isStaff(deriveRoles(session.me))) {
      await clearSession();
      return { ok: false, error: "not_staff" };
    }
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}
